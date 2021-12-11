using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class SelfJoinsPredicateComparer
    {
        private readonly IModel _model;

        public SelfJoinsPredicateComparer(IModel model)
        {
            _model = model;
        }

        public static bool IsColumnBelongTo(ColumnExpression column, TableExpressionBase table)
        {
            return column.Table == table
#if EFCORE60
                || (column.Table is JoinExpressionBase joinExpression && joinExpression.Table == table)
#endif
                ;
        }

        public static bool IsOnlyNotNull(TableExpressionBase tableOwner, SqlExpression sql)
        {
            return sql is SqlBinaryExpression binaryExpression && (
                (binaryExpression.OperatorType == ExpressionType.NotEqual
                    && binaryExpression.Left is ColumnExpression columnExpression
                    && IsColumnBelongTo(columnExpression, tableOwner)
                    && binaryExpression.Right is SqlConstantExpression constantExpression
                    && constantExpression.Value == null)
                || (binaryExpression.OperatorType == ExpressionType.AndAlso
                    && IsOnlyNotNull(tableOwner, binaryExpression.Left)
                    && IsOnlyNotNull(tableOwner, binaryExpression.Right)));
        }

        protected virtual HashSet<string> TakeOutKeys(TableExpressionBase left, TableExpressionBase right, SqlExpression predicate, Func<ColumnExpression, string> accessor)
        {
            var discovered = new HashSet<string>();

            bool VisitSql(SqlExpression sql)
            {
                if (sql is not SqlBinaryExpression binaryExpression)
                {
                    return false;
                }
                else if (binaryExpression.OperatorType == ExpressionType.Equal)
                {
                    if (binaryExpression.Left is ColumnExpression col
                        && binaryExpression.Right is ColumnExpression col2
                        && col.Name == accessor(col2)
                        && col.Table == left
                        && IsColumnBelongTo(col2, right))
                    {
                        discovered.Add(col.Name);
                        return true;
                    }
#if EFCORE60
                    else if (binaryExpression.Left is ColumnExpression col1
                        && binaryExpression.Right is CaseExpression caseExpr
                        && caseExpr.Operand == null
                        && caseExpr.ElseResult == null
                        && caseExpr.WhenClauses.Count == 1
                        && IsOnlyNotNull(right, caseExpr.WhenClauses[0].Test)
                        && caseExpr.WhenClauses[0].Result is ColumnExpression col3
                        && col1.Name == accessor(col3)
                        && col1.Table == left
                        && IsColumnBelongTo(col3, right))
                    {
                        discovered.Add(col1.Name);
                        return true;
                    }
#endif
                    else
                    {
                        return false;
                    }
                }
                else if (binaryExpression.OperatorType == ExpressionType.AndAlso)
                {
                    return VisitSql(binaryExpression.Left) && VisitSql(binaryExpression.Right);
                }
                else
                {
                    return false;
                }
            }

            return VisitSql(predicate) ? discovered : null;
        }

        protected virtual bool VisitSelect(TableExpression table, SelectExpression select, SqlExpression predicate, bool isLeft)
        {
            if (select.Tables.Count != 1
                || select.Tables[0] is not TableExpression pushedDownTable
                || table.Name != pushedDownTable.Name)
            {
                // The joined table have several tables joined.
                // If such situation should be reduced, that would be looked into first.
                return false;
            }

            var entityType = _model.FindEntityTypeByTable(table.Name);
            if (entityType == null)
            {
                // There is no such entity type found
                return false;
            }

            var store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);
            var key = entityType.FindPrimaryKey();
            if (!store.HasValue || key == null)
            {
                // There is no such table found or primary key defined
                return false;
            }

            var joinFields = TakeOutKeys(table, select, predicate, column =>
            {
                // The select expression is pushed down, so the projection fields should have their column name.
                if (!IsColumnBelongTo(column, select)) return null;
                var projection = select.Projection.Single(p => p.Alias == column.Name);
                return (projection.Expression as ColumnExpression)?.Name;
            });

            if (joinFields == null)
            {
                // Some problem occurred during join-field discovery
                return false;
            }

            return joinFields.SetEquals(key.Properties.Select(p => p.GetColumnName(store.Value)));
        }

        protected virtual bool VisitTable(TableExpression table, TableExpression table2, SqlExpression predicate, bool isLeft)
        {
            if (table.Name != table2.Name) return false;
            var entityType = _model.FindEntityTypeByTable(table.Name);

            if (entityType == null)
            {
                // There is no such entity type found
                return false;
            }

            var store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);
            var key = entityType.FindPrimaryKey();
            if (!store.HasValue || key == null)
            {
                // There is no such table found or primary key defined
                return false;
            }

            var joinFields = TakeOutKeys(table, table2, predicate, c => c.Name);
            if (joinFields == null)
            {
                // Some problem occurred during join-field discovery
                return false;
            }

            return joinFields.SetEquals(key.Properties.Select(p => p.GetColumnName(store.Value)));
        }

        protected virtual bool Visit(TableExpression table, PredicateJoinExpressionBase join)
        {
            bool isLeft = join is LeftJoinExpression;

            return join.Table switch
            {
                SelectExpression s => VisitSelect(table, s, join.JoinPredicate, isLeft),
                TableExpression tb => VisitTable(table, tb, join.JoinPredicate, isLeft),
                _ => false, // other check?
            };
        }

        public bool Compare(TableExpression table, PredicateJoinExpressionBase join)
        {
            return Visit(table, join);
        }
    }
}
