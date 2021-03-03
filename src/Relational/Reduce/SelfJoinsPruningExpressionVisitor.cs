using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
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

        public SelfJoinsPruningExpressionVisitor(
            IModel model,
            ISqlExpressionFactory sqlExpressionFactory)
        {
            Check.NotNull(model, nameof(model));
            Check.NotNull(sqlExpressionFactory, nameof(sqlExpressionFactory));

            _comparer = new SelfJoinsPredicateComparer(model);
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
            if (query is not ShapedQueryExpression shaped)
            {
                throw new InvalidOperationException("Only shaped query expressions can be passed here.");
            }

            var newQuery = Visit(shaped.QueryExpression);
            if (newQuery != shaped.QueryExpression)
            {
                throw new NotImplementedException();
            }

            return shaped;
        }

        protected override Expression VisitSelect(SelectExpression selectExpression)
        {
            var newTables = selectExpression.Tables
                .Select(i => Visit(i))
                .Cast<TableExpressionBase>()
                .ToList();

            // For UNION sequences, it is likely to be:
            // SELECT ( UNION ( SELECT , SELECT ) )
            if (selectExpression.Tables.Count == 1
                && selectExpression.Tables[0] is UnionExpression
                && newTables[0] is SelectExpression unionToSelect)
            {
                // should here update the shaper expression?
                return unionToSelect;
            }

            for (int _i = 0; _i < selectExpression.Tables.Count; _i++)
            {
                var pendingTable = selectExpression.Tables[_i] switch
                {
                    TableExpression _table1 => _table1,
                    PredicateJoinExpressionBase _join when _join.Table is TableExpression _table2 => _table2,
                    _ => null,
                };

                if (pendingTable == null) continue;
                int startId = _i + 1;

                for (int i = startId; i < selectExpression.Tables.Count; i++)
                {
                    if (selectExpression.Tables[i] is not PredicateJoinExpressionBase join)
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

                        var selectTables = (List<TableExpressionBase>)selectExpression.Tables;
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
                && table1.Name == table2.Name)
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
        }
    }
}
