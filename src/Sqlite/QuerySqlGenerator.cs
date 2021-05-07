using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Sqlite.Query
{
    public class SqliteBulkQuerySqlGenerator : SqliteQuerySqlGenerator
    {
        public SqliteBulkQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies)
            : base(dependencies)
        {
        }

        public ISqlGenerationHelper Helper => Dependencies.SqlGenerationHelper;

        protected override Expression VisitExtension(Expression extensionExpression)
        {
            return extensionExpression switch
            {
                ValuesExpression values => VisitValues(values),
                ExcludedTableColumnExpression excluded => VisitExcludedTableColumn(excluded),
                AffectedRowsExpression _ => throw new InvalidOperationException(),
                _ => base.VisitExtension(extensionExpression),
            };
        }

        protected virtual Expression VisitWrapped(WrappedExpression wrappedExpression)
        {
            return wrappedExpression switch
            {
                DeleteExpression deleteExpression => VisitDelete(deleteExpression),
                UpdateExpression updateExpression => VisitUpdate(updateExpression),
                SelectIntoExpression selectIntoExpression => VisitSelectInto(selectIntoExpression),
                UpsertExpression upsertExpression => VisitUpsert(upsertExpression),
                _ => throw new NotImplementedException(),
            };
        }

        protected virtual Expression VisitExcludedTableColumn(ExcludedTableColumnExpression excludedTableColumnExpression)
        {
            Sql.Append("\"excluded\".").Append(Helper.DelimitIdentifier(excludedTableColumnExpression.Name));
            return excludedTableColumnExpression;
        }

        protected override Expression VisitSelect(SelectExpression selectExpression)
        {
            if (selectExpression.Projection.Count == 1
                && selectExpression.Tables.Count == 1
                && selectExpression.Tables[0] is WrappedExpression wrappedExpression
                && selectExpression.Projection[0].Expression is AffectedRowsExpression)
            {
                VisitWrapped(wrappedExpression);
                return selectExpression;
            }

            return base.VisitSelect(selectExpression);
        }

        protected virtual Expression VisitValuesAsCommonTableExpression(ValuesExpression valuesExpression)
        {
            if (valuesExpression.TupleCount == 0)
            {
                Sql.Append("WITH ")
                    .Append(Helper.DelimitIdentifier(valuesExpression.Alias))
                    .Append(AliasSeparator)
                    .Append("(")
                    .IncrementIndent()
                    .AppendLine()
                    .Append("SELECT ")
                    .GenerateList(
                        valuesExpression.ColumnNames,
                        e => Sql.Append("NULL AS ").Append(Helper.DelimitIdentifier(e)))
                    .AppendLine()
                    .Append("WHERE 1=0")
                    .DecrementIndent()
                    .AppendLine()
                    .AppendLine(")");

                return valuesExpression;
            }

            Sql.Append("WITH ")
                .Append(Helper.DelimitIdentifier(valuesExpression.Alias))
                .Append(" (")
                .GenerateList(
                    valuesExpression.ColumnNames,
                    a => Sql.Append(Helper.DelimitIdentifier(a)))
                .Append(") AS ")
                .Append("(")
                .IncrementIndent()
                .AppendLine()
                .AppendLine("VALUES");

            if (valuesExpression.TupleCount.HasValue)
            {
                Sql.AddParameter(
                    new ValuesRelationalParameter(
                        valuesExpression.AnonymousType,
                        Helper.GenerateParameterName(valuesExpression.RuntimeParameter),
                        valuesExpression.RuntimeParameter));

                var paramName = Helper.GenerateParameterNamePlaceholder(valuesExpression.RuntimeParameter);

                for (int i = 0; i < valuesExpression.TupleCount.Value; i++)
                {
                    if (i != 0) Sql.Append(",").AppendLine();
                    Sql.Append("(");

                    for (int j = 0; j < valuesExpression.ColumnNames.Count; j++)
                    {
                        if (j != 0) Sql.Append(", ");
                        Sql.Append($"{paramName}_{i}_{j}");
                    }

                    Sql.Append(")");
                }
            }
            else if (valuesExpression.ImmediateValues != null)
            {
                for (int i = 0; i < valuesExpression.ImmediateValues.Count; i++)
                {
                    if (i != 0) Sql.Append(",").AppendLine();
                    Sql.Append("(")
                        .GenerateList(valuesExpression.ImmediateValues[i], e => Visit(e))
                        .Append(")");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "This instance of values expression is not concrete.");
            }

            Sql.DecrementIndent()
                .AppendLine()
                .AppendLine(")");

            return valuesExpression;
        }

        protected virtual Expression VisitValues(ValuesExpression tableExpression)
        {
            Sql.Append(Helper.DelimitIdentifier(tableExpression.Alias));
            return tableExpression;
        }

        protected virtual Expression VisitSelectInto(SelectIntoExpression selectIntoExpression)
        {
            Sql.Append("INSERT INTO ")
                .Append(Helper.DelimitIdentifier(selectIntoExpression.TableName, selectIntoExpression.Schema))
                .Append(" (");

            Sql.GenerateList(
                selectIntoExpression.Expression.Projection,
                e => Sql.Append(Helper.DelimitIdentifier(e.Alias)));

            Sql.AppendLine(")");

            VisitSelect(selectIntoExpression.Expression);
            return selectIntoExpression;
        }

        protected virtual Expression VisitUpdate(UpdateExpression updateExpression)
        {
            var original = updateExpression;
            if (!updateExpression.Expanded)
            {
                // This is a strange behavior for PostgreSQL. I don't like it.
                updateExpression = updateExpression.Expand();
            }

            foreach (var table in updateExpression.Tables)
            {
                if (table is ValuesExpression values)
                {
                    VisitValuesAsCommonTableExpression(values);
                }
                else if (table is JoinExpressionBase join && join.Table is ValuesExpression values2)
                {
                    VisitValuesAsCommonTableExpression(values2);
                }
            }

            Sql.Append("UPDATE ")
                .Append(Helper.DelimitIdentifier(updateExpression.ExpandedTable.Name))
                .Append(AliasSeparator)
                .AppendLine(Helper.DelimitIdentifier(updateExpression.ExpandedTable.Alias))
                .Append("SET ");

            Sql.GenerateList(
                updateExpression.Fields,
                e => Sql.Append(Helper.DelimitIdentifier(e.Alias))
                    .Append(" = ")
                    .Then(() => Visit(e.Expression)));

            if (updateExpression.Tables.Count > 0)
            {
                Sql.AppendLine().Append("FROM ");
                Sql.GenerateList(updateExpression.Tables, e => Visit(e), sql => sql.AppendLine());
            }

            if (updateExpression.Predicate != null)
            {
                Sql.AppendLine().Append("WHERE ");
                Visit(updateExpression.Predicate);
            }

            return original;
        }

        protected virtual Expression VisitDelete(DeleteExpression deleteExpression)
        {
            Sql.Append("DELETE FROM ");

            Sql.GenerateList(
                deleteExpression.JoinedTables,
                e => Visit(e),
                sql => sql.AppendLine());

            if (deleteExpression.Predicate != null)
            {
                Sql.AppendLine().Append("WHERE ");
                Visit(deleteExpression.Predicate);
            }

            return deleteExpression;
        }

        protected virtual Expression VisitUpsert(UpsertExpression upsertExpression)
        {
            Sql.Append("INSERT INTO ");
            Visit(upsertExpression.TargetTable);

            Sql.Append(" (")
                .GenerateList(upsertExpression.Columns, e => Sql.Append(Helper.DelimitIdentifier(e.Alias)))
                .AppendLine(")");

            if (upsertExpression.SourceTable is ValuesExpression valuesExpression)
            {
                if (valuesExpression.TupleCount == 0)
                {
                    Sql.Append("SELECT ")
                        .GenerateList(
                            upsertExpression.Columns,
                            e => Sql.Append("NULL"))
                        .AppendLine(" WHERE FALSE");
                }
                else
                {
                    TransientExpandValuesExpression.Process(
                        valuesExpression,
                        upsertExpression.Columns,
                        this, Sql, Helper);

                    Sql.AppendLine();
                }
            }
            else if (upsertExpression.SourceTable != null)
            {
                Sql.Append("SELECT ")
                    .GenerateList(upsertExpression.Columns, e => Visit(e.Expression))
                    .AppendLine();

                Sql.Append("FROM ");
                Visit(upsertExpression.SourceTable);
                Sql.AppendLine(" WHERE TRUE");
            }
            else
            {
                Sql.Append("VALUES (")
                    .GenerateList(upsertExpression.Columns, e => Visit(e.Expression))
                    .AppendLine(")");
            }

            if (upsertExpression.OnConflictUpdate == null)
            {
                Sql.Append("ON CONFLICT DO NOTHING");
            }
            else
            {
                var soi = StoreObjectIdentifier.Create(upsertExpression.ConflictConstraint.DeclaringEntityType, StoreObjectType.Table).Value;

                Sql.Append("ON CONFLICT (")
                    .GenerateList(
                        upsertExpression.ConflictConstraint.Properties,
                        e => Sql.Append(Helper.DelimitIdentifier(e.GetColumnName(soi))))
                    .Append(") DO UPDATE SET ")
                    .GenerateList(
                        upsertExpression.OnConflictUpdate,
                        e => Sql.Append(Helper.DelimitIdentifier(e.Alias)).Append(" = ").Then(() => Visit(e.Expression)));
            }

            return upsertExpression;
        }
    }

    public class SqliteBulkQuerySqlGeneratorFactory :
        IBulkQuerySqlGeneratorFactory,
        IServiceAnnotation<IQuerySqlGeneratorFactory, SqliteQuerySqlGeneratorFactory>
    {
        private readonly QuerySqlGeneratorDependencies _dependencies;

        public SqliteBulkQuerySqlGeneratorFactory(
            QuerySqlGeneratorDependencies dependencies)
        {
            Check.NotNull(dependencies, nameof(dependencies));

            _dependencies = dependencies;
        }

        public virtual QuerySqlGenerator Create()
        {
            return new SqliteBulkQuerySqlGenerator(_dependencies);
        }
    }
}
