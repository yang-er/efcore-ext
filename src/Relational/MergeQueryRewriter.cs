using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    internal static class MergeQueryRewriter
    {
        public static void ParseUpsert<TTarget, TSource>(
            DbContext context,
            DbSet<TTarget> targetTable,
            IEnumerable<TSource> sourceTable,
            Expression<Func<TSource, TTarget>> insertExpression,
            Expression<Func<TTarget, TTarget, TTarget>> updateExpression,
            out UpsertExpression mergeExpression,
            out QueryGenerationContext<Result<TTarget>> execution)
            where TTarget : class
            where TSource : class
        {
            if (!(insertExpression.Body is MemberInitExpression keyBody) ||
                keyBody.NewExpression.Constructor.GetParameters().Length != 0)
                throw new InvalidOperationException("Insert expression must be empty constructor and contain member initialization.");

            AnonymousObjectExpressionFactory.GetTransparentIdentifier(
                Expression.Parameter(typeof(TTarget), "t"), context.Model.FindEntityType(typeof(TTarget)),
                insertExpression.Parameters[0], keyBody.Bindings,
                out var tJoinKey, out var targetKey, out var sourceKey);

            mergeExpression = null;
            execution = null;
        }

        /// <remarks>
        /// <para>
        /// This is provided for SQL Server MERGE INTO, so there's <c>NOT MATCHED</c>,
        /// <c>MATCHED BY SOURCE</c>, <c>MATCHED BY TARGET</c>.
        /// </para>
        /// <para>
        /// However, it provides the ability to translate one local or remote table
        /// and the insert / update parts. So this is moved to relational project.
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
            var sourceQuery = sourceTable2 as IQueryable<TSource>;
            List<TSource> sourceTable = null;

            if (sourceQuery == null)
            {
                if (!typeof(TSource).IsAnonymousType())
                    throw new InvalidOperationException();
                sourceTable = sourceTable2.ToList();

                if (sourceTable.Count == 0)
                {
                    mergeExpression = null;
                    execution = null;
                    return;
                }

                var selector = Expression.Lambda<Func<TTarget, TSource>>(
                    body: AnonymousObjectExpressionFactory.Create(typeof(TSource)),
                    Expression.Parameter(typeof(TTarget), "t"));
                sourceQuery = targetTable.Select(selector).Distinct();
            }

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

            // TODO: This logic will be changed after the fix of table-splitting in EFCore 5.0 preview
            if (selectExpression.Tables.Count != 2
                || selectExpression.Predicate != null
                || !(selectExpression.Tables[1] is InnerJoinExpression innerJoin)
                || !(selectExpression.Tables[0] is TableExpression table))
                throw new NotSupportedException("Unknown entity configured.");

            var exp = new MergeExpression
            {
                TargetEntityType = entityType,
                JoinPredicate = innerJoin.JoinPredicate,
                TargetTable = table,
                SourceTable = innerJoin.Table,
                NotMatchedBySource = delete,
                Matched = updateExpression == null ? null : new List<ProjectionExpression>(),
                NotMatchedByTarget = insertExpression == null ? null : new List<ProjectionExpression>()
            };

            if (sourceTable != null)
            {
                if (!(innerJoin.Table is SelectExpression subquery))
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

                exp.TableChanges = subquery;
                exp.SourceTable = new ValuesExpression(items, projects.Select(a => a.a.Name), subquery.Alias);
                exp.ColumnChanges = projects.ToDictionary(a => a.p.Alias, a => a.a.Name);
            }

            var columns = entityType.GetColumns();
            var map = RelationalInternals.AccessProjectionMapping(selectExpression);
            foreach (var (a, b) in map)
            {
                if (!(b is ConstantExpression constant))
                    throw new NotSupportedException("Wrong query.");
                var name = a.ToString();
                if (name.StartsWith("Insert.") || name.StartsWith("Update."))
                    ((List<ProjectionExpression>)(name.StartsWith("Insert.") ? exp.NotMatchedByTarget : exp.Matched))
                        .Add(RelationalInternals.CreateProjectionExpression(
                            selectExpression.Projection[(int)constant.Value].Expression,
                            columns[name.Substring(7)]));
                else
                    throw new InvalidOperationException("Translate failed.");
            }

            mergeExpression = exp;
        }

        public class Result<T>
        {
            public T Insert { get; set; }

            public T Update { get; set; }
        }

        private static Expression<Func<T1, T2, Result<T1>>> MergeResult<T1, T2>(
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
}
