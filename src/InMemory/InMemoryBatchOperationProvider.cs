using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class InMemoryBatchOperationProvider : IBatchOperationProvider
    {
        private bool EnsureType<T>(IQueryable<T> query) where T : class
        {
            var expression = query.Expression;
            while (expression.NodeType == ExpressionType.Call)
            {
                // expression = Call someFunc(IQueryable<T> some, ..., ...)
                expression = ((MethodCallExpression)expression).Arguments[0];
            }

            return expression.NodeType == ExpressionType.Constant
                && expression.Type == typeof(EntityQueryable<T>);
        }

        private Action<T> CompileUpdate<T>(Expression<Func<T, T>> expression)
        {
            var body = expression.Body;
            var param = expression.Parameters.Single();

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

                return Expression.Lambda<Action<T>>(
                    Expression.Block(sentences),
                    expression.Parameters).Compile();
            }
            else if (body is NewExpression @new)
            {
                if (@new.Constructor.GetParameters().Any())
                    throw new InvalidOperationException("Non-simple constructor is not supported.");
                return _ => { };
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        private Action<T, T2> CompileUpdate<T, T2>(Expression<Func<T, T2, T>> expression)
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

        private Func<TTarget, TSource, bool> CompileEquals<TTarget, TSource, TJoinKey>(
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

        public int BatchDelete<T>(
            DbContext context,
            IQueryable<T> query)
            where T : class
        {
            if (!EnsureType(query))
                throw new ArgumentException("Query invalid. The operation entity must be in DbContext.");
            context.Set<T>().RemoveRange(query.ToArray());
            return context.SaveChanges();
        }

        public async Task<int> BatchDeleteAsync<T>(
            DbContext context,
            IQueryable<T> query,
            CancellationToken cancellationToken = default)
            where T : class
        {
            if (!EnsureType(query))
                throw new ArgumentException("Query invalid. The operation entity must be in DbContext.");
            context.Set<T>().RemoveRange(await query.ToArrayAsync());
            return await context.SaveChangesAsync();
        }

        public int BatchInsertInto<T>(
            DbContext context,
            IQueryable<T> query,
            DbSet<T> to)
            where T : class
        {
            to.AddRange(query.ToArray());
            return context.SaveChanges();
        }

        public async Task<int> BatchInsertIntoAsync<T>(
            DbContext context,
            IQueryable<T> query,
            DbSet<T> to,
            CancellationToken cancellationToken = default)
            where T : class
        {
            to.AddRange(await query.ToArrayAsync(cancellationToken));
            return await context.SaveChangesAsync(cancellationToken);
        }

        public int BatchUpdate<T>(
            DbContext context,
            IQueryable<T> query,
            Expression<Func<T, T>>
            updateExpression)
            where T : class
        {
            if (!EnsureType(query))
                throw new ArgumentException("Query invalid. The operation entity must be in DbContext.");
            var entities = query.ToList();
            entities.ForEach(CompileUpdate(updateExpression));
            context.Set<T>().UpdateRange(entities);
            return context.SaveChanges();
        }

        public async Task<int> BatchUpdateAsync<T>(
            DbContext context,
            IQueryable<T> query,
            Expression<Func<T, T>> updateExpression,
            CancellationToken cancellationToken = default)
            where T : class
        {
            if (!EnsureType(query))
                throw new ArgumentException("Query invalid. The operation entity must be in DbContext.");
            var entities = await query.ToListAsync();
            entities.ForEach(CompileUpdate(updateExpression));
            context.Set<T>().UpdateRange(entities);
            return await context.SaveChangesAsync();
        }

        private void SolveMerge<TTarget, TSource, TJoinKey>(
            DbContext context,
            List<TTarget> targetTable,
            List<TSource> sourceTable,
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression,
            Expression<Func<TSource, TTarget>> insertExpression,
            bool delete)
            where TTarget : class
            where TSource : class
        {
            var set = context.Set<TTarget>();
            var insert = insertExpression?.Compile();
            var update = CompileUpdate(updateExpression);
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
            SolveMerge(context, target, source, targetKey, sourceKey, updateExpression, insertExpression, delete);
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
            var target = await targetTable.ToListAsync();
            var source = sourceTable.ToList();
            SolveMerge(context, target, source, targetKey, sourceKey, updateExpression, insertExpression, delete);
            return await context.SaveChangesAsync();
        }

        public (string, IEnumerable<object>) ToParametrizedSql<TEntity>(
            DbContext context,
            IQueryable<TEntity> query)
            where TEntity : class
        {
            throw new NotSupportedException();
        }
    }
}
