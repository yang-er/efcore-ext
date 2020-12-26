using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class SqlServerBatchOperationProvider : RelationalBatchOperationProvider
    {
        public override int Merge<TTarget, TSource, TJoinKey>(
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
            var (sql, parameters) = GetSqlMerge(context, targetTable, sourceTable, typeof(TJoinKey), targetKey, sourceKey, updateExpression, insertExpression, delete);
            return context.Database.ExecuteSqlRaw(sql, parameters);
        }

        public override Task<int> MergeAsync<TTarget, TSource, TJoinKey>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TTarget, TJoinKey>> targetKey,
            Expression<Func<TSource, TJoinKey>> sourceKey,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression,
            Expression<Func<TSource, TTarget>> insertExpression,
            bool delete,
            CancellationToken cancellationToken)
            where TTarget : class
            where TSource : class
        {
            var (sql, parameters) = GetSqlMerge(context, targetTable, sourceTable, typeof(TJoinKey), targetKey, sourceKey, updateExpression, insertExpression, delete);
            return context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
        }

        public override int Upsert<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>> updateExpression)
            where TTarget : class
            where TSource : class
        {
            var (sql, parameters) = GetSqlUpsert(context, set, sources, insertExpression, updateExpression);
            return context.Database.ExecuteSqlRaw(sql, parameters);
        }

        public override Task<int> UpsertAsync<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> set,
            IEnumerable<TSource> sources,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>> updateExpression,
            CancellationToken cancellationToken)
            where TTarget : class
            where TSource : class
        {
            var (sql, parameters) = GetSqlUpsert(context, set, sources, insertExpression, updateExpression);
            return context.Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
        }

        private static (string, IEnumerable<object>) GetSqlUpsert<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>> updateExpression2)
            where TTarget : class
            where TSource : class
        {
            if (!(insertExpression.Body is MemberInitExpression keyBody) ||
                keyBody.NewExpression.Constructor.GetParameters().Length != 0)
                throw new InvalidOperationException("Insert expression must be empty constructor and contain member initialization.");

            var updateExpression = UpsertTttToTstVisitor.Parse(insertExpression, updateExpression2);

            AnonymousObjectExpressionFactory.GetTransparentIdentifier(
                Expression.Parameter(typeof(TTarget), "t"), context.Model.FindEntityType(typeof(TTarget)),
                insertExpression.Parameters[0], keyBody.Bindings,
                out var tJoinKey, out var targetKey, out var sourceKey);

            MergeQueryRewriter.ParseMerge(
                context, targetTable, sourceTable,
                tJoinKey, targetKey, sourceKey,
                updateExpression, insertExpression, false,
                out var exp, out var execution);

            if (exp == null)
                return ("SELECT 0", Array.Empty<object>());

            var (command, parameters) = execution.Generate("MERGE", null,
                _ => ((EnhancedQuerySqlGenerator)_).GetCommand(exp));
            return (command.CommandText, parameters);
        }

        private static (string, IEnumerable<object>) GetSqlMerge<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable2,
            Type joinKeyType,
            LambdaExpression targetKey,
            LambdaExpression sourceKey,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression,
            Expression<Func<TSource, TTarget>> insertExpression,
            bool delete)
            where TTarget : class
            where TSource : class
        {
            MergeQueryRewriter.ParseMerge(
                context, targetTable, sourceTable2,
                joinKeyType, targetKey, sourceKey,
                updateExpression, insertExpression, delete,
                out var exp, out var execution);

            if (exp == null)
                return (delete
                    ? $"TRUNCATE TABLE [{context.Model.FindEntityType(typeof(TTarget)).GetTableName()}]"
                    : $"SELECT 0", Array.Empty<object>());

            var (command, parameters) = execution.Generate("MERGE", null,
                _ => ((EnhancedQuerySqlGenerator)_).GetCommand(exp));
            return (command.CommandText, parameters);
        }

        private class UpsertTttToTstVisitor : ExpressionVisitor
        {
            private readonly ParameterExpression _excluded;
            private readonly IReadOnlyDictionary<MemberInfo, Expression> _placeholders;

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Expression != _excluded)
                {
                    // This is not member from excluded
                    return base.VisitMember(node);
                }
                else if (_placeholders.TryGetValue(node.Member, out var exp))
                {
                    return exp;
                }
                else
                {
                    // no such member from placeholders
                    return Expression.Default(node.Type);
                }
            }

            private UpsertTttToTstVisitor(ParameterExpression excluded, IEnumerable<MemberAssignment> assignments)
            {
                _excluded = excluded;
                _placeholders = assignments.ToDictionary(k => k.Member, k => k.Expression);
            }

            public static Expression<Func<TTarget, TSource, TTarget>> Parse<TTarget, TSource>(
                Expression<Func<TSource, TTarget>> insertExpression,
                Expression<Func<TTarget, TTarget, TTarget>> updateExpression)
            {
                if (updateExpression == null) return null;
                var memberInit = (MemberInitExpression)insertExpression.Body;

                var visitor = new UpsertTttToTstVisitor(
                    updateExpression.Parameters[1],
                    memberInit.Bindings.OfType<MemberAssignment>());

                var newBody = visitor.Visit(updateExpression.Body);

                return Expression.Lambda<Func<TTarget, TSource, TTarget>>(
                    newBody,
                    updateExpression.Parameters[0],
                    insertExpression.Parameters[0]);
            }
        }
    }
}
