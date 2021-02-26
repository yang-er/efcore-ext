using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;
using System;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class EnhancedQuerySqlGenerator : NpgsqlQuerySqlGenerator, IEnhancedQuerySqlGenerator
    {
        public EnhancedQuerySqlGenerator(
            QuerySqlGeneratorDependencies dependencies,
            bool reverseNullOrderingEnabled,
            Version postgresVersion)
            : base(dependencies, reverseNullOrderingEnabled, postgresVersion)
        {
        }

        public ISqlGenerationHelper Helper => Dependencies.SqlGenerationHelper;

#if EFCORE31
        private static readonly Regex _composableSql
            = new Regex(@"^\s*?SELECT\b", RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(value: 1000.0));

        private void CheckComposableSql(string sql)
        {
            if (!_composableSql.IsMatch(sql))
            {
                throw new InvalidOperationException("FromSqlNonComposable");
            }
        }
#endif

        protected override Expression VisitFromSql(FromSqlExpression fromSqlExpression)
        {
            if (fromSqlExpression.Alias != null)
                return base.VisitFromSql(fromSqlExpression);

            CheckComposableSql(fromSqlExpression.Sql);
            RelationalInternals.ApplyGenerateFromSql(this, fromSqlExpression);
            return fromSqlExpression;
        }

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

        protected virtual Expression VisitValues(ValuesExpression tableExpression)
        {
            if (tableExpression.Alias != null)
                Sql.Append("(").IncrementIndent();
            
            Sql.AppendLine()
                .AppendLine("VALUES");

            var paramName = tableExpression.RuntimeParameter;
            Sql.AddParameter(
                new ValuesRelationalParameter(
                    tableExpression.AnonymousType,
                    Helper.GenerateParameterName(paramName),
                    paramName));

            tableExpression.Generate(
                Sql,
                Helper.GenerateParameterNamePlaceholder(paramName));

            if (tableExpression.Alias != null)
            {
                Sql.DecrementIndent()
                    .AppendLine()
                    .Append(")");

                Sql.Append(AliasSeparator)
                    .Append(Helper.DelimitIdentifier(tableExpression.Alias))
                    .Append(" (");

                Sql.GenerateList(
                    tableExpression.ColumnNames,
                    a => Sql.Append(Helper.DelimitIdentifier(a)));

                Sql.Append(")");
            }

            return tableExpression;
        }

        protected virtual Expression VisitExcludedTableColumn(ExcludedTableColumnExpression excludedTableColumnExpression)
        {
            Sql.Append("excluded.").Append(Helper.DelimitIdentifier(excludedTableColumnExpression.Name));
            return excludedTableColumnExpression;
        }

        public virtual IRelationalCommand GetCommand(SelectIntoExpression selectIntoExpression)
        {
            RelationalInternals.InitQuerySqlGenerator(this);
            VisitSelectInto(selectIntoExpression);
            return Sql.Build();
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

        public virtual IRelationalCommand GetCommand(UpdateExpression updateExpression)
        {
            RelationalInternals.InitQuerySqlGenerator(this);
            VisitUpdate(updateExpression);
            return Sql.Build();
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

        public virtual IRelationalCommand GetCommand(DeleteExpression deleteExpression)
        {
            RelationalInternals.InitQuerySqlGenerator(this);
            VisitDelete(deleteExpression);
            return Sql.Build();
        }

        protected virtual Expression VisitDelete(DeleteExpression deleteExpression)
        {
            Sql.Append("DELETE ");

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

        public virtual IRelationalCommand GetCommand(UpsertExpression insertExpression)
        {
            RelationalInternals.InitQuerySqlGenerator(this);

            string cteName = null;
            if (!(insertExpression.SourceTable is TableExpression))
            {
                // WITH temp_table_efcore AS
                cteName = "temp_table_efcore";

                Sql.Append("WITH ")
                    .Append(cteName);

                if (insertExpression.SourceTable is ValuesExpression values)
                {
                    Sql.Append(" (");

                    Sql.GenerateList(
                        values.ColumnNames,
                        a => Sql.Append(Helper.DelimitIdentifier(a)));

                    Sql.Append(")");
                }

                Sql.Append(AliasSeparator)
                    .Append("(")
                    .IncrementIndent()
                    .AppendLine();

                var originalAlias = insertExpression.SourceTable.Alias;
                RelationalInternals.ApplyAlias(insertExpression.SourceTable, null);
                Visit(insertExpression.SourceTable);
                RelationalInternals.ApplyAlias(insertExpression.SourceTable, originalAlias);

                Sql.DecrementIndent()
                    .AppendLine()
                    .AppendLine(")");
            }

            Sql.Append("INSERT INTO ");
            Visit(insertExpression.TargetTable);
            Sql.AppendLine();

            Sql.Append("(")
                .GenerateList(insertExpression.Columns, e => Sql.Append(Helper.DelimitIdentifier(e.Alias)))
                .AppendLine(")");

            Sql.Append("SELECT ")
                .GenerateList(insertExpression.Columns, e => Visit(e))
                .AppendLine();

            Sql.Append("FROM ")
                .Append(Helper.DelimitIdentifier(cteName ?? ((TableExpression)insertExpression.SourceTable).Name))
                .Append(AliasSeparator)
                .AppendLine(Helper.DelimitIdentifier(insertExpression.SourceTable.Alias));

            if (insertExpression.OnConflictUpdate == null)
            {
                Sql.AppendLine("ON CONFLICT DO NOTHING");
            }
            else
            {
                Sql.Append("ON CONFLICT ON CONSTRAINT ")
                    .Append(Helper.DelimitIdentifier(insertExpression.ConflictConstraintName))
                    .AppendLine(" DO UPDATE")
                    .Append("SET ")
                    .GenerateList(
                        insertExpression.OnConflictUpdate,
                        e => Sql.Append(Helper.DelimitIdentifier(e.Alias)).Append(" = ").Then(() => Visit(e.Expression)));
            }

            return Sql.Build();
        }

        public object CreateParameter(QueryContext context, TypeMappedRelationalParameter parInfo)
        {
            var typeMap = RelationalInternals.AccessRelationalTypeMapping(parInfo);
            var value = context.ParameterValues[parInfo.InvariantName];
            if (typeMap.Converter != null)
                value = typeMap.Converter.ConvertToProvider(value);
            var nullable = RelationalInternals.AccessIsNullable(parInfo);

            var parameter = new NpgsqlParameter
            {
                ParameterName = parInfo.Name,
                Direction = ParameterDirection.Input,
                Value = value ?? DBNull.Value,
            };

            if (nullable.HasValue)
                parameter.IsNullable = nullable.Value;
            if (typeMap.DbType.HasValue)
                parameter.DbType = typeMap.DbType.Value;
            return parameter;
        }

        public IRelationalCommand GetCommand(MergeExpression mergeExpression)
        {
            throw new NotSupportedException();
        }
    }
}