using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class SqlServerBatchOperationProvider : RelationalBatchOperationProvider
    {
        protected override IBulkQueryExecutor GetSqlUpsert<TTarget, TSource>(
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

            var entityType = context.Model.FindEntityType(typeof(TTarget));
            if (!entityType.TryGuessKey(keyBody.Bindings, out var key))
                throw new NotSupportedException($"No corresponding key found for {entityType}.");

            var updateExpression = UpsertTttToTstVisitor.Parse(insertExpression, updateExpression2);

            GenericUtility.CreateJoinKey(
                Expression.Parameter(typeof(TTarget), "t"), key,
                insertExpression.Parameters[0], keyBody.Bindings,
                out var tJoinKey, out var targetKey, out var sourceKey);

            QueryRewriter.ParseMerge(
                context, targetTable, sourceTable,
                tJoinKey, targetKey, sourceKey,
                updateExpression, insertExpression, false,
                out var mergeExpression, out var queryRewritingContext);

            if (mergeExpression == null) return new NullBulkQueryExecutor();
            return queryRewritingContext.Generate(mergeExpression);
        }

        protected override IBulkQueryExecutor GetSqlMerge<TTarget, TSource>(
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
            QueryRewriter.ParseMerge(
                context, targetTable, sourceTable2,
                joinKeyType, targetKey, sourceKey,
                updateExpression, insertExpression, delete,
                out var mergeExpression, out var queryRewritingContext);

            if (mergeExpression == null)
            {
                if (delete)
                {
                    var builder = context.GetService<IRawSqlCommandBuilder>();
                    var queryContext = (RelationalQueryContext)context.GetService<IQueryContextFactory>().Create();

                    return new RelationalBulkQueryExecutor(
                        queryContext,
                        builder.Build($"TRUNCATE TABLE [{context.Model.FindEntityType(typeof(TTarget)).GetTableName()}]"));
                }
                else
                {
                    return new NullBulkQueryExecutor();
                }
            }

            return queryRewritingContext.Generate(mergeExpression);
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
