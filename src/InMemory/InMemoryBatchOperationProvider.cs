using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class InMemoryBatchOperationProvider : IBatchOperationProvider
    {
        private static Action<T, T2> CompileUpdate<T, T2>(Expression<Func<T, T2, T>> expression)
        {
            if (expression == null) return null;
            var body = expression.Body;
            var param = expression.Parameters[0];

            if (body is MemberInitExpression memberInit)
            {
                if (memberInit.NewExpression.Constructor.GetParameters().Any())
                    throw new InvalidOperationException("Non-simple constructor is not supported.");
                var sentences = new List<Expression>();
                foreach (var item in memberInit.Bindings)
                {
                    if (!(item is MemberAssignment assignment))
                        throw new InvalidOperationException("Non-assignment binding is not supported.");
                    var m = Expression.MakeMemberAccess(param, assignment.Member);
                    sentences.Add(Expression.Assign(m, assignment.Expression));
                }

                return Expression.Lambda<Action<T, T2>>(
                    Expression.Block(sentences),
                    expression.Parameters).Compile();
            }
            else if (body is NewExpression @new)
            {
                if (@new.Constructor.GetParameters().Any())
                    throw new InvalidOperationException("Non-simple constructor is not supported.");
                return (_, __) => { };
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private static Func<TTarget, TSource, bool> CompileEquals<TTarget, TSource, TJoinKey>(
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey)
        {
            if (targetKey.Body.NodeType != sourceKey.Body.NodeType)
                throw new InvalidOperationException("Invalid join key type.");
            Expression body;

            if (targetKey.Body.NodeType == ExpressionType.MemberAccess)
                body = Expression.Equal(targetKey.Body, sourceKey.Body);

            else if (
                targetKey.Body.NodeType == ExpressionType.New &&
                targetKey.Body.Type.IsAnonymousType() &&
                targetKey.Body is NewExpression newTarget &&
                sourceKey.Body is NewExpression newSource &&
                newTarget.Constructor == newSource.Constructor)
                body = newTarget.Arguments
                    .Zip(newSource.Arguments, Expression.Equal)
                    .Aggregate(Expression.AndAlso);

            else
                throw new NotImplementedException("Not supported this kind of join yet.");

            return Expression.Lambda<Func<TTarget, TSource, bool>>(body,
                targetKey.Parameters[0], sourceKey.Parameters[0]).Compile();
        }

        private static void SolveMerge<TTarget, TSource, TJoinKey>(
            DbContext context,
            List<TTarget> targetTable,
            List<TSource> sourceTable,
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey,
            Expression<Func<TTarget, TSource, bool>> condition,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression,
            Expression<Func<TSource, TTarget>> insertExpression,
            bool delete)
            where TTarget : class
            where TSource : class
        {
            var set = context.Set<TTarget>();
            var insert = insertExpression?.Compile();
            var update = CompileUpdate(updateExpression);
            var cond = condition?.Compile();
            var equals = CompileEquals(targetKey, sourceKey);
            var usedSource = new HashSet<TSource>();

            foreach (var target in targetTable)
            {
                TSource hit = null;
                foreach (var source in sourceTable)
                {
                    if (equals(target, source))
                    {
                        hit = source;
                        break;
                    }
                }

                if (hit != null && update != null)
                {
                    if (cond != null && !cond(target, hit))
                        continue;
                    update(target, hit);
                    usedSource.Add(hit);
                    set.Update(target);
                }

                if (hit == null && delete)
                {
                    set.Remove(target);
                }
            }

            foreach (var item in sourceTable)
            {
                if (usedSource.Add(item) && insert != null)
                {
                    set.Add(insert(item));
                }
            }
        }

        public int Merge<TTarget, TSource, TJoinKey>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression,
            Expression<Func<TSource, TTarget>> insertExpression,
            bool delete)
            where TTarget : class
            where TSource : class
        {
            var target = targetTable.ToList();
            var source = sourceTable.ToList();
            SolveMerge(context, target, source, targetKey, sourceKey, null, updateExpression, insertExpression, delete);
            return context.SaveChanges();
        }

        public async Task<int> MergeAsync<TTarget, TSource, TJoinKey>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression,
            Expression<Func<TSource, TTarget>> insertExpression,
            bool delete,
            CancellationToken cancellationToken = default)
            where TTarget : class
            where TSource : class
        {
            var target = await targetTable.ToListAsync(cancellationToken);
            var source = sourceTable.ToList();
            SolveMerge(context, target, source, targetKey, sourceKey, null, updateExpression, insertExpression, delete);
            return await context.SaveChangesAsync(cancellationToken);
        }

        private static Func<TTarget, TSource, bool> GetPredicates<TTarget, TSource>(
            IEntityType innerType,
            ParameterExpression outerParam, IReadOnlyList<MemberBinding> outerBindings)
        {
            if (!innerType.TryGuessKey(outerBindings, out var keys))
                throw new NotSupportedException($"No corresponding upsert equality key found for {innerType}.");

            var innerParam = Expression.Parameter(typeof(TTarget), "<>_t");
            var inner = new Expression[keys.Properties.Count];
            var outer = new Expression[keys.Properties.Count];
            for (int i = 0; i < keys.Properties.Count; i++)
            {
                var prop = keys.Properties[i];
                var member = (MemberInfo)prop.PropertyInfo ?? prop.FieldInfo;
                if (prop.IsShadowProperty())
                    throw new NotSupportedException("Shadow property in this key is not supported yet.");

                var binding = outerBindings.OfType<MemberAssignment>().FirstOrDefault(m => m.Member == member);
                if (binding == null)
                    throw new ArgumentException("The outer member binding doesn't contains the property in this key.");

                inner[i] = Expression.MakeMemberAccess(innerParam, member);
                outer[i] = binding.Expression;
            }

            var body = inner.Zip(outer, Expression.Equal).Aggregate(Expression.AndAlso);
            var lambda = Expression.Lambda<Func<TTarget, TSource, bool>>(body, innerParam, outerParam);
            return lambda.Compile();
        }

        public int Upsert<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>> updateExpression)
            where TTarget : class
            where TSource : class
        {
            if (!(insertExpression.Body is MemberInitExpression keyBody) ||
                keyBody.NewExpression.Constructor.GetParameters().Length != 0)
                throw new InvalidOperationException("Insert expression must be empty constructor and contain member initialization.");

            var predicate = GetPredicates<TTarget, TSource>(
                context.Model.FindEntityType(typeof(TTarget)),
                insertExpression.Parameters[0], keyBody.Bindings);
            var factory = insertExpression.Compile();
            var updater = CompileUpdate(updateExpression);

            var target = set.ToList();
            var source = sources.ToList();

            foreach (var item in source)
            {
                var t = target.FirstOrDefault(_t => predicate.Invoke(_t, item));
                var excluded = factory.Invoke(item);
                if (t == null)
                {
                    set.Add(excluded);
                }
                else if (updater != null)
                {
                    updater.Invoke(t, excluded);
                    set.Update(t);
                }
            }

            return context.SaveChanges();
        }

        public async Task<int> UpsertAsync<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>> updateExpression,
            CancellationToken cancellationToken)
            where TTarget : class
            where TSource : class
        {
            if (!(insertExpression.Body is MemberInitExpression keyBody) ||
                keyBody.NewExpression.Constructor.GetParameters().Length != 0)
                throw new InvalidOperationException("Insert expression must be empty constructor and contain member initialization.");

            var predicate = GetPredicates<TTarget, TSource>(
                context.Model.FindEntityType(typeof(TTarget)),
                insertExpression.Parameters[0], keyBody.Bindings);
            var factory = insertExpression.Compile();
            var updater = CompileUpdate(updateExpression);

            var target = await set.ToListAsync(cancellationToken);
            var source = sources.ToList();

            foreach (var item in source)
            {
                var t = target.FirstOrDefault(_t => predicate.Invoke(_t, item));
                var excluded = factory.Invoke(item);
                if (t == null)
                {
                    set.Add(excluded);
                }
                else if (updater != null)
                {
                    updater.Invoke(t, excluded);
                    set.Update(t);
                }
            }

            return await context.SaveChangesAsync(cancellationToken);
        }
    }
}
