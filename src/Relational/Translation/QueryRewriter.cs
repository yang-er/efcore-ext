using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    internal static class QueryRewriter
    {
        /// <summary>
        /// Convert the source local table to a fake subquery or real-query itself.
        /// </summary>
        /// <typeparam name="TTarget">Any real queryable type.</typeparam>
        /// <typeparam name="TSource">The source entity type.</typeparam>
        /// <param name="targetTable">The target table.</param>
        /// <param name="sourceEnumerable">The source table.</param>
        /// <param name="sourceQuery">The source query created for join or other usages.</param>
        /// <param name="callback">The action to normalize the fake query.</param>
        /// <returns>Whether to fallback to empty source table.</returns>
        private static bool DetectSourceTable<TTarget, TSource>(
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceEnumerable,
            out IQueryable<TSource> sourceQuery)
            where TTarget : class
            where TSource : class
        {
            sourceQuery = sourceEnumerable as IQueryable<TSource>;

            // normal subquery or table
            if (sourceQuery != null) return true;

            if (!typeof(TSource).IsAnonymousType())
                throw new InvalidOperationException($"The source entity for upsert/merge must be anonymous objects.");

            var sourceTable = sourceEnumerable as IReadOnlyList<TSource>;
            sourceTable ??= sourceEnumerable.ToList();
            if (sourceTable.Count == 0) return false;

            sourceQuery = targetTable.CreateCommonTable(sourceTable);
            return true;
        }


        /// <summary>
        /// Process for insert / update fields.
        /// </summary>
        private static void ParseInsertOrUpdateFields(
            IEntityType entityType,
            SelectExpression selectExpression,
            out IReadOnlyList<ProjectionExpression> insert,
            out IReadOnlyList<ProjectionExpression> update)
        {
            insert = new List<ProjectionExpression>();
            update = new List<ProjectionExpression>();

            var columnNames = entityType.GetColumns();
            var projectionMapping = RelationalInternals.AccessProjectionMapping(selectExpression);
            foreach (var (projectionMember, _id) in projectionMapping)
            {
                if (!(_id is ConstantExpression constant) || !(constant.Value is int id))
                {
                    throw new NotSupportedException("Translation failed for parsing these projection mapping.");
                }

                var name = projectionMember.ToString();
                var target = name.Substring(0, 7) switch
                {
                    "Insert." => (List<ProjectionExpression>)insert,
                    "Update." => (List<ProjectionExpression>)update,
                    _ => throw new InvalidOperationException("Unknown projection member"),
                };

                target.Add(RelationalInternals.CreateProjectionExpression(
                    selectExpression.Projection[id].Expression,
                    columnNames[name.Substring(7)]));
            }
        }


        /// <remarks>
        /// <para>
        /// This is provided for PostgreSQL / MySQL UPSERT.
        /// </para>
        /// <para>
        /// Note that: when sourceTable is a local array and the array is empty, this
        /// will return <c>null</c>, which means you may apply other SQL queries to fix this scenario.
        /// </para>
        /// </remarks>
        public static void ParseUpsert<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>> updateExpression,
            out UpsertExpression upsertExpression,
            out QueryRewritingContext queryRewritingContext)
            where TTarget : class
            where TSource : class
        {
            if (!(insertExpression.Body is MemberInitExpression keyBody) ||
                keyBody.NewExpression.Constructor.GetParameters().Length != 0)
                throw new InvalidOperationException("Insert expression must be empty constructor and contain member initialization.");

            upsertExpression = null;
            queryRewritingContext = null;

            if (!DetectSourceTable(targetTable, sourceTable, out var sourceQuery))
                return;

            var query = targetTable
                .IgnoreQueryFilters()
                .Join(sourceQuery, a => 998244353, a => 1000000007, (t, s) => new { t, s })
                .Join(targetTable.IgnoreQueryFilters(), a => 998244853, a => 100000007, (a, t0) => new { a.t, a.s, t0 });

            var entityType = context.Model.FindEntityType(typeof(TTarget));
            if (!entityType.TryGuessKey(keyBody.Bindings, out var key))
                throw new NotSupportedException($"No corresponding key found for {entityType}.");

            queryRewritingContext = TranslationStrategy.Go(context, MergeResult(query, updateExpression, insertExpression));
            var selectExpression = queryRewritingContext.SelectExpression;
            var queryContext = queryRewritingContext.QueryContext;

            static bool CheckJoinPredicate(SqlExpression sqlExpression, int left, int right)
                => sqlExpression is SqlBinaryExpression binary
                    && binary.OperatorType == ExpressionType.Equal
                    && binary.Left is SqlConstantExpression should998244353
                    && binary.Right is SqlConstantExpression should1000000007
                    && should998244353.Type == typeof(int)
                    && should1000000007.Type == typeof(int)
                    && (int)should998244353.Value == left
                    && (int)should1000000007.Value == right;

            if (selectExpression.Tables.Count != 3
                || selectExpression.Predicate != null
                || !(selectExpression.Tables[0] is TableExpression table)
                || !(selectExpression.Tables[1] is InnerJoinExpression innerJoin)
                || !(selectExpression.Tables[2] is InnerJoinExpression selfJoin)
                || !CheckJoinPredicate(innerJoin.JoinPredicate, 998244353, 1000000007)
                || !CheckJoinPredicate(selfJoin.JoinPredicate, 998244853, 100000007)
                || !(selfJoin.Table is TableExpression excludedTable)
                || excludedTable.Name != table.Name)
                throw new NotSupportedException("Unknown entity configured.");

            ParseInsertOrUpdateFields(
                entityType, selectExpression,
                out var inserts, out var updates);

            if (updateExpression != null)
            {
                var processor = new ExcludedTableColumnRewritingVisitor(excludedTable);
                var updateFields = (List<ProjectionExpression>)updates;
                for (int i = 0; i < updateFields.Count; i++)
                {
                    updateFields[i] = processor.VisitAndConvert(updateFields[i], null);
                }
            }

            upsertExpression = new UpsertExpression(
                table, innerJoin.Table, inserts,
                updateExpression == null ? null : updates,
                key.GetName());

            static IQueryable<GenericUtility.Result<T1>> MergeResult<T0, T1, T2>(
                IQueryable<T0> query,
                Expression<Func<T1, T1, T1>> updateExpression,
                Expression<Func<T2, T1>> insertExpression)
            {
                var tst0 = Expression.Parameter(typeof(T0), "tst0");
                var t = Expression.Property(tst0, "t");
                var s = Expression.Property(tst0, "s");
                var t0 = Expression.Property(tst0, "t0");
                var res = Expression.New(typeof(GenericUtility.Result<T1>));
                var binding = Array.Empty<MemberBinding>().AsEnumerable();

                if (updateExpression != null)
                    binding = binding.Append(Expression.Bind(
                        member: typeof(GenericUtility.Result<T1>).GetProperty("Update"),
                        expression: new ParameterReplaceVisitor(
                            (updateExpression.Parameters[0], t),
                            (updateExpression.Parameters[1], t0))
                        .Visit(updateExpression.Body)));

                if (insertExpression != null)
                    binding = binding.Append(Expression.Bind(
                        member: typeof(GenericUtility.Result<T1>).GetProperty("Insert"),
                        expression: new ParameterReplaceVisitor(
                            (insertExpression.Parameters[0], s))
                        .Visit(insertExpression.Body)));

                var body = Expression.MemberInit(res, binding);
                var selector = Expression.Lambda<Func<T0, GenericUtility.Result<T1>>>(body, tst0);
                return query.Select(selector);
            }
        }


        /// <remarks>
        /// <para>
        /// This is provided for SQL Server MERGE INTO, so there's <c>NOT MATCHED</c>,
        /// <c>MATCHED BY SOURCE</c>, <c>MATCHED BY TARGET</c>.
        /// </para>
        /// <para>
        /// Note that: when sourceTable is a local array and the array is empty, this
        /// will return <c>null</c>, which means you may apply other SQL queries to fix this scenario.
        /// </para>
        /// </remarks>
        public static void ParseMerge<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable2,
            Type joinKeyType,
            LambdaExpression targetKey,
            LambdaExpression sourceKey,
            Expression<Func<TTarget, TSource, TTarget>> updateExpression,
            Expression<Func<TSource, TTarget>> insertExpression,
            bool delete,
            out MergeExpression mergeExpression,
            out QueryRewritingContext queryRewritingContext)
            where TTarget : class
            where TSource : class
        {
            mergeExpression = null;
            queryRewritingContext = null;

            var e = DetectSourceTable(targetTable, sourceTable2, out var sourceQuery);
            if (!e && delete) return;

            var query = targetTable
                .IgnoreQueryFilters()
                .Join(sourceQuery, joinKeyType, targetKey, sourceKey, MergeResult(updateExpression, insertExpression));

            var entityType = context.Model.FindEntityType(typeof(TTarget));
            queryRewritingContext = TranslationStrategy.Go(context, query);
            var selectExpression = queryRewritingContext.SelectExpression;
            var queryContext = queryRewritingContext.QueryContext;

            if (selectExpression.Tables.Count != 2
                || selectExpression.Predicate != null
                || !(selectExpression.Tables[1] is InnerJoinExpression innerJoin)
                || !(selectExpression.Tables[0] is TableExpression table))
                throw new NotSupportedException("Unknown entity configured.");

            ParseInsertOrUpdateFields(
                entityType, selectExpression,
                out var inserts, out var updates);

            mergeExpression = new MergeExpression(
                table, innerJoin.Table, innerJoin.JoinPredicate,
                updateExpression == null ? null : updates,
                insertExpression == null ? null : inserts,
                delete);

            static Expression<Func<T1, T2, GenericUtility.Result<T1>>> MergeResult<T1, T2>(
                Expression<Func<T1, T2, T1>> updateExpression,
                Expression<Func<T2, T1>> insertExpression)
            {
                var para1 = Expression.Parameter(typeof(T1), "t");
                var para2 = Expression.Parameter(typeof(T2), "s");
                var res = Expression.New(typeof(GenericUtility.Result<T1>));
                var binding = Array.Empty<MemberBinding>().AsEnumerable();

                if (updateExpression != null)
                    binding = binding.Append(Expression.Bind(
                        member: typeof(GenericUtility.Result<T1>).GetProperty("Update"),
                        expression: new ParameterReplaceVisitor(
                            (updateExpression.Parameters[0], para1),
                            (updateExpression.Parameters[1], para2))
                        .Visit(updateExpression.Body)));

                if (insertExpression != null)
                    binding = binding.Append(Expression.Bind(
                        member: typeof(GenericUtility.Result<T1>).GetProperty("Insert"),
                        expression: new ParameterReplaceVisitor(
                            (insertExpression.Parameters[0], para2))
                        .Visit(insertExpression.Body)));

                var body = Expression.MemberInit(res, binding);
                return Expression.Lambda<Func<T1, T2, GenericUtility.Result<T1>>>(body, para1, para2);
            }
        }
    }
}
