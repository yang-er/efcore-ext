using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class SelfJoinsPruningExpressionVisitor : SqlExpressionVisitorV2
    {
        private readonly SelfJoinsPredicateComparer _comparer;
        private readonly ColumnRewritingExpressionVisitor _columnRewriting;
        private readonly bool _skipped;

        public SelfJoinsPruningExpressionVisitor(
            QueryCompilationContext queryCompilationContext,
            ISqlExpressionFactory sqlExpressionFactory)
        {
            Check.NotNull(queryCompilationContext, nameof(queryCompilationContext));
            Check.NotNull(sqlExpressionFactory, nameof(sqlExpressionFactory));

            _skipped = queryCompilationContext.Tags.Contains("SkipSelfJoinsPruning");
            if (_skipped) queryCompilationContext.Tags.Remove("SkipSelfJoinsPruning");
            _comparer = new SelfJoinsPredicateComparer(queryCompilationContext.Model);
            _columnRewriting = new ColumnRewritingExpressionVisitor(sqlExpressionFactory);
        }

        protected virtual SqlExpression MergePredicate(ExpressionType type, SqlExpression left, SqlExpression right)
        {
            return left != null && right != null
                ? new SqlBinaryExpression(type, left, right, left.Type, left.TypeMapping)
                : (left ?? right);
        }

        // Remove the join from parent select and reduce the column name with reducer
        protected virtual void RemoveJoin<T>(SelectExpression parentSelect, PredicateJoinExpressionBase join, T reducer) where T : class
        {
            using (_columnRewriting.Setup(join.Table, reducer))
            {
                var tables = (List<TableExpressionBase>)parentSelect.Tables;
                tables.Remove(join);

                var projections = (List<ProjectionExpression>)parentSelect.Projection;
                _columnRewriting.Visit(projections);

                var projectionMapping = parentSelect.GetProjectionMapping();
                _columnRewriting.Visit(projectionMapping);

                if (join is InnerJoinExpression && join.Table is SelectExpression reducedSelect)
                {
                    var predicate1 = _columnRewriting.Visit(parentSelect.Predicate);
                    var predicate2 = _columnRewriting.Visit(reducedSelect.Predicate);
                    var predicate = MergePredicate(ExpressionType.AndAlso, predicate1, predicate2);
                    parentSelect.SetPredicate(predicate);
                }
            }
        }

        public Expression Reduce(Expression query)
        {
            if (_skipped) return query;

            if (query is not ShapedQueryExpression shaped)
            {
                throw new InvalidOperationException("Only shaped query expressions can be passed here.");
            }

            var originQuery = (SelectExpression)shaped.QueryExpression;
            var newQuery = (SelectExpression)VisitSelect(originQuery);

            if (!ReferenceEquals(newQuery, originQuery))
            {
                // Only appears when SELECT ( UNION (..) ), so the shaping result is the same.
                if (originQuery.Projection.Count == 0)
                {
                    ((List<ProjectionExpression>)newQuery.Projection).Clear();
                    // In fact we need to process on newQuery's _projectionMapping
                    // However it's already the final state
                }

                var newShaper = new ShaperQueryExpressionReplacingVisitor(originQuery, newQuery).Visit(shaped.ShaperExpression);
                shaped = shaped.Update(newQuery, newShaper);
            }

            return shaped;
        }

        protected override Expression VisitSelect(SelectExpression selectExpression)
        {
            var selectTables = (List<TableExpressionBase>)selectExpression.Tables;
            var newTables = selectTables
                .Select(i => Visit(i))
                .Cast<TableExpressionBase>()
                .ToList();

            // For UNION sequences, it is likely to be:
            // SELECT ( UNION ( SELECT , SELECT ) )
            if (selectTables.Count == 1
                && selectTables[0] is UnionExpression
                && newTables[0] is SelectExpression unionToSelect
                && unionToSelect.Orderings.Count == 0
                && !unionToSelect.IsDistinct
                && unionToSelect.GroupBy.Count == 0
                && unionToSelect.Having == null
                && unionToSelect.Limit == null
                && unionToSelect.Offset == null)
            {
                return unionToSelect;
            }

            for (int i = selectTables.Count - 1; i >= 0; i--)
            {
                if (newTables[i] == selectTables[i]) continue;
                var resulting = newTables[i];

                if (newTables[i] is SelectExpression newSelect
                    && newSelect.Orderings.Count == 0
                    && !newSelect.IsDistinct
                    && newSelect.GroupBy.Count == 0
                    && newSelect.Having == null
                    && newSelect.Limit == null
                    && newSelect.Offset == null
                    && newSelect.Tables.Count == 1
                    && newSelect.Tables[0] is TableExpression table
                    && i == 0)
                {
                    resulting = table;
                    using (_columnRewriting.Setup(selectTables[i], table))
                    {
                        UpdateParent();

                        selectExpression.SetPredicate(
                            MergePredicate(
                                ExpressionType.AndAlso,
                                newSelect.Predicate,
                                selectExpression.Predicate));
                    }
                }
                else if (newTables[i] is JoinExpressionBase joinNew && selectTables[i] is JoinExpressionBase joinOld)
                {
                    if (joinNew.Table != joinOld.Table)
                    {
                        using (_columnRewriting.Setup(joinOld.Table, joinNew.Table))
                        {
                            UpdateParent();
                        }
                    }
                }
                else
                {
                    throw new NotImplementedException();
                }

                ((List<TableExpressionBase>)selectExpression.Tables)[i] = resulting;

                void UpdateParent()
                {
                    selectExpression.SetPredicate(_columnRewriting.Visit(selectExpression.Predicate));
                    selectExpression.SetHaving(_columnRewriting.Visit(selectExpression.Having));
                    _columnRewriting.Visit((List<TableExpressionBase>)selectExpression.Tables);
                    _columnRewriting.Visit((List<OrderingExpression>)selectExpression.Orderings);
                    _columnRewriting.Visit((List<SqlExpression>)selectExpression.GroupBy);
                    _columnRewriting.Visit((List<ProjectionExpression>)selectExpression.Projection);
                    _columnRewriting.Visit(selectExpression.GetProjectionMapping());
                }
            }

            for (int _i = 0; _i < selectTables.Count; _i++)
            {
                var pendingTable = selectTables[_i] switch
                {
                    TableExpression _table1 => _table1,
                    PredicateJoinExpressionBase _join when _join.Table is TableExpression _table2 => _table2,
                    _ => null,
                };

                if (pendingTable == null) continue;
                int startId = _i + 1;

                for (int i = startId; i < selectTables.Count; i++)
                {
                    if (selectTables[i] is not PredicateJoinExpressionBase join)
                    {
                        continue;
                    }

                    var newTable = (TableExpressionBase)Visit(join.Table);
                    if (newTable != join.Table)
                    {
                        if (newTable is not SelectExpression newSelect)
                        {
                            throw new NotImplementedException();
                        }

                        newSelect.SetAlias(join.Table.Alias);
                        var joinPredicate = join.JoinPredicate;

                        using (_columnRewriting.Setup(join.Table, newSelect))
                        {
                            joinPredicate = _columnRewriting.Visit(joinPredicate);
                            _columnRewriting.Visit((List<ProjectionExpression>)selectExpression.Projection);
                        }

                        var newJoin = join switch
                        {
                            LeftJoinExpression left => left.Update(newTable, joinPredicate),
                            InnerJoinExpression inner => (PredicateJoinExpressionBase)inner.Update(newTable, joinPredicate),
                            _ => throw new NotImplementedException(),
                        };

                        selectTables[selectTables.IndexOf(join)] = newJoin;
                        join = newJoin;
                    }

                    if (!_comparer.Compare(pendingTable, join))
                    {
                        continue;
                    }

                    i--;

                    if (join.Table is TableExpression)
                    {
                        RemoveJoin(selectExpression, join, pendingTable);
                    }
                    else if (join.Table is SelectExpression removalSelect)
                    {
                        Check.DebugAssert(
                            removalSelect.Tables.Count == 1 &&
                            removalSelect.Tables[0] is TableExpression,
                            "Should be only one table's join");

                        var previousProjections = removalSelect.Projection
                            //.Distinct(ProjectionNameComparer.Default)
                            .ToDictionary(p => p.Alias, p => (Expression)p.Expression);

                        using (_columnRewriting.Setup(removalSelect.Tables[0], pendingTable))
                        {
                            removalSelect.SetPredicate(_columnRewriting.Visit(removalSelect.Predicate));
                            _columnRewriting.Visit(previousProjections);
                        }

                        RemoveJoin(selectExpression, join, previousProjections);
                    }
                    else
                    {
                        throw new NotImplementedException();
                    }
                }
            }

            return selectExpression;
        }

        protected override Expression VisitUnion(UnionExpression unionExpression)
        {
            var select1 = (SelectExpression)Visit(unionExpression.Source1);
            var select2 = (SelectExpression)Visit(unionExpression.Source2);
            unionExpression = unionExpression.Update(select1, select2);

            if (unionExpression.IsDistinct
                && unionExpression.Source1.Tables.Count == 1
                && unionExpression.Source2.Tables.Count == 1
                && unionExpression.Source1.Tables[0] is TableExpression table1
                && unionExpression.Source2.Tables[0] is TableExpression table2
                && table1.Name == table2.Name
                && unionExpression.Source1.Orderings.Count == 0
                && unionExpression.Source2.Orderings.Count == 0
                && !unionExpression.Source1.IsDistinct
                && !unionExpression.Source2.IsDistinct
                && unionExpression.Source1.GroupBy.Count == 0
                && unionExpression.Source2.GroupBy.Count == 0
                && unionExpression.Source1.Having == null
                && unionExpression.Source2.Having == null
                && unionExpression.Source1.Limit == null
                && unionExpression.Source2.Limit == null
                && unionExpression.Source1.Offset == null
                && unionExpression.Source2.Offset == null
                && unionExpression.Source1.Projection.Count == unionExpression.Source2.Projection.Count
                && unionExpression.Source1.Projection.Count != 0
                && SameSelectFields(select1, select2, table1, table2))
            {
                var selectExpression = unionExpression.Source1;
                var predicate2 = unionExpression.Source2.Predicate;

                using (_columnRewriting.Setup(table2, table1))
                {
                    predicate2 = _columnRewriting.Visit(predicate2);
                }

                var predicate = MergePredicate(ExpressionType.OrElse, unionExpression.Source1.Predicate, predicate2);
                selectExpression.SetPredicate(predicate);
                return selectExpression;
            }
            else
            {
                return unionExpression;
            }

            bool SameSelectFields(SelectExpression select1, SelectExpression select2, TableExpression table1, TableExpression table2)
            {
                using (_columnRewriting.Setup(table2, table1))
                {
                    for (int i = 0; i < select1.Projection.Count; i++)
                    {
                        var projA = select1.Projection[i];
                        var projB = select2.Projection[i];

                        if (projA.Alias != projB.Alias)
                        {
                            return false;
                        }

                        var tmpExprB = _columnRewriting.Visit(projB.Expression);
                        if (!projA.Expression.Equals(tmpExprB))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }
        }

        protected override Expression VisitInnerJoin(InnerJoinExpression innerJoinExpression)
        {
            var result = (InnerJoinExpression)base.VisitInnerJoin(innerJoinExpression);
            if (result.Table == innerJoinExpression.Table) return result;

            if (result.Table.Alias == null) result.Table.SetAlias(innerJoinExpression.Table.Alias);

            using (_columnRewriting.Setup(innerJoinExpression.Table, result.Table))
            {
                result = result.Update(result.Table, _columnRewriting.Visit(result.JoinPredicate));
            }

            return result;
        }

        protected override Expression VisitLeftJoin(LeftJoinExpression leftJoinExpression)
        {
            var result = (LeftJoinExpression)base.VisitLeftJoin(leftJoinExpression);
            if (result.Table == leftJoinExpression.Table) return result;

            if (result.Table.Alias == null) result.Table.SetAlias(leftJoinExpression.Table.Alias);

            using (_columnRewriting.Setup(leftJoinExpression.Table, result.Table))
            {
                result = result.Update(result.Table, _columnRewriting.Visit(result.JoinPredicate));
            }

            return result;
        }
    }
}
