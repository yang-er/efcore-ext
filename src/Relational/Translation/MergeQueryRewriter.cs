using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    internal static class MergeQueryRewriter
    {
        /// <summary>
        /// Convert the source local table to a fake subquery or real-query itself.
        /// </summary>
        /// <typeparam name="TTarget">Any real queryable type.</typeparam>
        /// <typeparam name="TSource">The source entity type.</typeparam>
        /// <param name="targetTable">The target table.</param>
        /// <param name="sourceEnumerable">The source table.</param>
        /// <param name="sourceQuery">The source query created for join or other usages.</param>
        /// <param name="normalize">The action to normalize the fake query.</param>
        /// <returns>Whether to fallback to empty source table.</returns>
        private static bool DetectSourceTable<TTarget, TSource>(
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceEnumerable,
            out IQueryable<TSource> sourceQuery,
            out Action<IFakeSubselectExpression, RelationalQueryContext> normalize)
            where TTarget : class
            where TSource : class
        {
            sourceQuery = sourceEnumerable as IQueryable<TSource>;
            List<TSource> sourceTable = null;

            if (sourceQuery != null)
            {
                // normal subquery or table
                normalize = (_, __) => { };
                return true;
            }

            if (!typeof(TSource).IsAnonymousType())
                throw new InvalidOperationException($"The source entity for upsert/merge must be anonymous objects.");

            sourceTable = sourceEnumerable.ToList();
            if (sourceTable.Count == 0)
            {
                normalize = (_, __) => { };
                return false;
            }

            var selector = Expression.Lambda<Func<TTarget, TSource>>(
                body: AnonymousObjectExpressionFactory.Create(typeof(TSource)),
                Expression.Parameter(typeof(TTarget), "t"));
            sourceQuery = targetTable.Select(selector).Distinct();

            normalize = (exp, queryContext) =>
            {
                if (!(exp.FakeTable is SelectExpression subquery))
                    throw new InvalidOperationException("Translate failed.");
                var props = typeof(TSource).GetProperties();

                var projects =
                    (from p in subquery.Projection
                     let c = (SqlConstantExpression)p.Expression
                     let i = c.ReadBack()
                     select new { p, c.TypeMapping, i, a = props[i] }).ToList();
                projects.Sort((a, b) => a.i.CompareTo(b.i));
                if (projects.Last().i + 1 != projects.Count)
                    throw new InvalidOperationException("Translate failed.");
                var items = new SqlParameterExpression[sourceTable.Count, props.Length];

                for (int i = 0; i < sourceTable.Count; i++)
                {
                    for (int j = 0; j < props.Length; j++)
                    {
                        var paraName = $"__ap_{queryContext.ParameterValues.Count}";
                        queryContext.AddParameter(paraName, props[j].GetValue(sourceTable[i]));
                        items[i, j] = Expression.Parameter(props[j].PropertyType, paraName).ToSql(projects[j].TypeMapping);
                    }
                }

                exp.Update(
                    real: new ValuesExpression(items, projects.Select(a => a.a.Name), subquery.Alias),
                    fake: subquery,
                    columnMapping: projects.ToDictionary(a => a.p.Alias, a => a.a.Name));
            };

            return true;
        }

        /// <summary>
        /// Process for insert / update fields.
        /// </summary>
        private static void AddUpsertFields(
            IFakeSubselectExpression exp,
            IEntityType entityType,
            SelectExpression selectExpression)
        {
            var columns = entityType.GetColumns();
            var map = RelationalInternals.AccessProjectionMapping(selectExpression);
            foreach (var (a, b) in map)
            {
                if (!(b is ConstantExpression constant))
                    throw new NotSupportedException("Wrong query.");
                var name = a.ToString();
                if (name.StartsWith("Insert.") || name.StartsWith("Update."))
                    exp.AddUpsertField(
                        insert: name.StartsWith("Insert."),
                        sqlExpression: selectExpression.Projection[(int)constant.Value].Expression,
                        columnName: columns[name.Substring(7)]);
                else
                    throw new InvalidOperationException("Translate failed.");
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
            out QueryGenerationContext<Result<TTarget>> execution)
            where TTarget : class
            where TSource : class
        {
            if (!(insertExpression.Body is MemberInitExpression keyBody) ||
                keyBody.NewExpression.Constructor.GetParameters().Length != 0)
                throw new InvalidOperationException("Insert expression must be empty constructor and contain member initialization.");

            upsertExpression = null;
            execution = null;

            if (!DetectSourceTable(
                targetTable,
                sourceTable,
                out var sourceQuery,
                out var normalize))
                return;

            var query = targetTable
                .IgnoreQueryFilters()
                .Join(sourceQuery, a => 998244353, a => 1000000007, (t, s) => new { t, s })
                .Join(targetTable.IgnoreQueryFilters(), a => 998244853, a => 100000007, (a, t0) => new { a.t, a.s, t0 });

            var entityType = context.Model.FindEntityType(typeof(TTarget));
            execution = TranslationStrategy.Go(context, MergeResult(query, updateExpression, insertExpression));
            var selectExpression = execution.SelectExpression;
            var queryContext = execution.QueryContext;

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
                || !(selfJoin.Table is TableExpression tableAgain)
                || tableAgain.Name != table.Name)
                throw new NotSupportedException("Unknown entity configured.");

            upsertExpression = new UpsertExpression
            {
                TargetTable = table,
                SourceTable = innerJoin.Table,
                OnConflictUpdate = updateExpression == null ? null : new List<ProjectionExpression>(),
                Columns = new List<ProjectionExpression>(),
                EntityType = entityType,
                ExcludedTable = tableAgain,
            };

            normalize.Invoke(upsertExpression, queryContext);
            AddUpsertFields(upsertExpression, entityType, selectExpression);

            static IQueryable<Result<T1>> MergeResult<T0, T1, T2>(
                IQueryable<T0> query,
                Expression<Func<T1, T1, T1>> updateExpression,
                Expression<Func<T2, T1>> insertExpression)
            {
                var tst0 = Expression.Parameter(typeof(T0), "tst0");
                var t = Expression.Property(tst0, "t");
                var s = Expression.Property(tst0, "s");
                var t0 = Expression.Property(tst0, "t0");
                var res = Expression.New(typeof(Result<T1>));
                var binding = Array.Empty<MemberBinding>().AsEnumerable();

                if (updateExpression != null)
                    binding = binding.Append(Expression.Bind(
                        member: typeof(Result<T1>).GetProperty("Update"),
                        expression: new ParameterReplaceVisitor(
                            (updateExpression.Parameters[0], t),
                            (updateExpression.Parameters[1], t0))
                        .Visit(updateExpression.Body)));

                if (insertExpression != null)
                    binding = binding.Append(Expression.Bind(
                        member: typeof(Result<T1>).GetProperty("Insert"),
                        expression: new ParameterReplaceVisitor(
                            (insertExpression.Parameters[0], s))
                        .Visit(insertExpression.Body)));

                var body = Expression.MemberInit(res, binding);
                var selector = Expression.Lambda<Func<T0, Result<T1>>>(body, tst0);
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
            out QueryGenerationContext<Result<TTarget>> execution)
            where TTarget : class
            where TSource : class
        {
            mergeExpression = null;
            execution = null;

            var e = DetectSourceTable(
                targetTable,
                sourceTable2,
                out var sourceQuery,
                out var normalize);
            if (!e && delete) return;

            var query = targetTable
                .IgnoreQueryFilters()
                .Join(
                    inner: sourceQuery,
                    joinKeyType: joinKeyType,
                    outerKeySelector: targetKey,
                    innerKeySelector: sourceKey,
                    resultSelector: MergeResult(updateExpression, insertExpression));

            var entityType = context.Model.FindEntityType(typeof(TTarget));
            execution = TranslationStrategy.Go(context, query);
            var selectExpression = execution.SelectExpression;
            var queryContext = execution.QueryContext;

            if (selectExpression.Tables.Count != 2
                || selectExpression.Predicate != null
                || !(selectExpression.Tables[1] is InnerJoinExpression innerJoin)
                || !(selectExpression.Tables[0] is TableExpression table))
                throw new NotSupportedException("Unknown entity configured.");

            mergeExpression = new MergeExpression
            {
                TargetEntityType = entityType,
                JoinPredicate = innerJoin.JoinPredicate,
                TargetTable = table,
                SourceTable = innerJoin.Table,
                NotMatchedBySource = delete,
                Matched = updateExpression == null ? null : new List<ProjectionExpression>(),
                NotMatchedByTarget = insertExpression == null ? null : new List<ProjectionExpression>()
            };

            normalize.Invoke(mergeExpression, queryContext);
            AddUpsertFields(mergeExpression, entityType, selectExpression);

            static Expression<Func<T1, T2, Result<T1>>> MergeResult<T1, T2>(
                Expression<Func<T1, T2, T1>> updateExpression,
                Expression<Func<T2, T1>> insertExpression)
            {
                var para1 = Expression.Parameter(typeof(T1), "t");
                var para2 = Expression.Parameter(typeof(T2), "s");
                var res = Expression.New(typeof(Result<T1>));
                var binding = Array.Empty<MemberBinding>().AsEnumerable();

                if (updateExpression != null)
                    binding = binding.Append(Expression.Bind(
                        member: typeof(Result<T1>).GetProperty("Update"),
                        expression: new ParameterReplaceVisitor(
                            (updateExpression.Parameters[0], para1),
                            (updateExpression.Parameters[1], para2))
                        .Visit(updateExpression.Body)));

                if (insertExpression != null)
                    binding = binding.Append(Expression.Bind(
                        member: typeof(Result<T1>).GetProperty("Insert"),
                        expression: new ParameterReplaceVisitor(
                            (insertExpression.Parameters[0], para2))
                        .Visit(insertExpression.Body)));

                var body = Expression.MemberInit(res, binding);
                return Expression.Lambda<Func<T1, T2, Result<T1>>>(body, para1, para2);
            }
        }

        public class Result<T>
        {
            public T Insert { get; set; }

            public T Update { get; set; }
        }
    }
}
