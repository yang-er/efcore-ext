using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Npgsql.EntityFrameworkCore.PostgreSQL.Query
{
    public class NpgsqlBulkQuerySqlGenerator : NpgsqlQuerySqlGenerator
    {
        public NpgsqlBulkQuerySqlGenerator(
            QuerySqlGeneratorDependencies dependencies,
            bool reverseNullOrderingEnabled,
            Version postgresVersion)
            : base(dependencies, reverseNullOrderingEnabled, postgresVersion)
        {
        }

        public ISqlGenerationHelper Helper => Dependencies.SqlGenerationHelper;

        protected override Expression VisitExtension(Expression extensionExpression)
        {
            return extensionExpression switch
            {
                ValuesExpression values => VisitValues(values),
                ExcludedTableColumnExpression excludedTableColumnExpression => VisitExcludedTableColumn(excludedTableColumnExpression),
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

        protected virtual Expression VisitValues(ValuesExpression valuesExpression)
        {
            Sql.Append("(")
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
                .Append(")")
                .Append(AliasSeparator)
                .Append(Helper.DelimitIdentifier(valuesExpression.Alias))
                .Append(" (")
                .GenerateList(
                    valuesExpression.ColumnNames,
                    a => Sql.Append(Helper.DelimitIdentifier(a)))
                .Append(")");

            return valuesExpression;
        }

        protected virtual Expression VisitExcludedTableColumn(ExcludedTableColumnExpression excludedTableColumnExpression)
        {
            Sql.Append("excluded.").Append(Helper.DelimitIdentifier(excludedTableColumnExpression.Name));
            return excludedTableColumnExpression;
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
            Sql.Append("DELETE");

            if (deleteExpression.JoinedTables.Any())
            {
                Sql.AppendLine().Append("FROM ");
                Sql.GenerateList(
                    deleteExpression.JoinedTables,
                    e => Visit(e),
                    sql => sql.AppendLine());
            }

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
                TransientExpandValuesExpression.Process(
                    valuesExpression,
                    upsertExpression.Columns,
                    this, Sql, Helper);

                Sql.AppendLine();
            }
            else if (upsertExpression.SourceTable != null)
            {
                Sql.Append("SELECT ")
                    .GenerateList(upsertExpression.Columns, e => Visit(e.Expression))
                    .AppendLine();

                Sql.Append("FROM ");
                Visit(upsertExpression.SourceTable);
                Sql.AppendLine();
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
                Sql.Append("ON CONFLICT ON CONSTRAINT ")
                    .Append(Helper.DelimitIdentifier(upsertExpression.ConflictConstraint.GetName()))
                    .Append(" DO UPDATE SET ")
                    .GenerateList(
                        upsertExpression.OnConflictUpdate,
                        e => Sql.Append(Helper.DelimitIdentifier(e.Alias)).Append(" = ").Then(() => Visit(e.Expression)));
            }

            return upsertExpression;
        }
    }

    public class NpgsqlBulkQuerySqlGeneratorFactory :
        IBulkQuerySqlGeneratorFactory,
        IServiceAnnotation<IQuerySqlGeneratorFactory, NpgsqlQuerySqlGeneratorFactory>
    {
        private readonly QuerySqlGeneratorDependencies _dependencies;
        private readonly INpgsqlOptions _npgsqlOptions;

        public NpgsqlBulkQuerySqlGeneratorFactory(
            QuerySqlGeneratorDependencies dependencies,
            INpgsqlOptions npgsqlOptions)
        {
            _dependencies = dependencies;
            _npgsqlOptions = npgsqlOptions;
        }

        public virtual QuerySqlGenerator Create()
            => new NpgsqlBulkQuerySqlGenerator(
                _dependencies,
                _npgsqlOptions.ReverseNullOrderingEnabled,
                _npgsqlOptions.PostgresVersion);
    }
}
