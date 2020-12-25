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
        private Func<ColumnExpression, bool> change = c => false;

        public EnhancedQuerySqlGenerator(
            QuerySqlGeneratorDependencies dependencies,
            ISqlExpressionFactory sqlExpressionFactory,
            bool reverseNullOrderingEnabled,
            Version postgresVersion)
            : base(dependencies, reverseNullOrderingEnabled, postgresVersion)
        {
            Generator = sqlExpressionFactory;
        }

        public ISqlExpressionFactory Generator { get; }

        public ISqlGenerationHelper Helper => Dependencies.SqlGenerationHelper;

        public QuerySqlGenerator Self => this;

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

        protected override Expression VisitColumn(ColumnExpression columnExpression)
        {
            if (!change(columnExpression))
                base.VisitColumn(columnExpression);
            return columnExpression;
        }

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
                _ => base.VisitExtension(extensionExpression),
            };
        }

        protected virtual Expression VisitValues(ValuesExpression tableExpression)
        {
            if (tableExpression.Alias != null)
                Sql.Append("(").IncrementIndent();
            
            Sql.Append("VALUES")
                .AppendLine();

            Sql.GenerateList(
                tableExpression.Values,
                a => Sql.Append("(").GenerateList(a, e => Visit(e)).Append(")"),
                sql => sql.Append(",").AppendLine());

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

        public virtual IRelationalCommand GetCommand(SelectIntoExpression selectIntoExpression)
        {
            RelationalInternals.InitQuerySqlGenerator(this);

            var selectExpression = selectIntoExpression.Expression;
            GenerateTagsHeaderComment(selectExpression);

            Sql.Append("INSERT INTO ")
                .Append(Helper.DelimitIdentifier(selectIntoExpression.Table.Name, selectIntoExpression.Table.Schema))
                .Append(" (");

            Sql.GenerateList(
                selectExpression.Projection,
                e => Sql.Append(Helper.DelimitIdentifier(e.Alias)));

            Sql.AppendLine(")");

            VisitSelect(selectExpression);

            return Sql.Build();
        }

        public virtual IRelationalCommand GetCommand(UpdateExpression updateExpression)
        {
            if (updateExpression.Limit != null)
            {
                throw new NotSupportedException("PostgreSQL doesn't support LIMIT while executing UPDATE.");
            }

            RelationalInternals.InitQuerySqlGenerator(this);
            Sql.Append("UPDATE ");

            if (updateExpression.Tables.Count == 1)
            {
                change = columnExpression =>
                {
                    if (columnExpression.Table != updateExpression.Table) return false;
                    Sql.Append(Helper.DelimitIdentifier(columnExpression.Name));
                    return true;
                };

                Sql.AppendLine(Helper.DelimitIdentifier(updateExpression.Table.Name));
                updateExpression.Tables = Array.Empty<TableExpressionBase>();
            }
            else if (updateExpression.Tables[1] is InnerJoinExpression innerJoin)
            {
                Sql.Append(Helper.DelimitIdentifier(updateExpression.Table.Name))
                    .Append(AliasSeparator)
                    .AppendLine(Helper.DelimitIdentifier(updateExpression.Table.Alias));

                var newTables = updateExpression.Tables.Skip(1).ToList();
                newTables[0] = innerJoin.Table;
                updateExpression.Tables = newTables;

                if (updateExpression.Predicate == null)
                    updateExpression.Predicate = innerJoin.JoinPredicate;
                else
                    updateExpression.Predicate = Generator.AndAlso(updateExpression.Predicate, innerJoin.JoinPredicate);
            }
            else
            {
                throw new NotSupportedException(
                    "Translation failed for this kind of entity update. " +
                    "If you'd like to provide more information on this, " +
                    "please contact the plugin author.");
            }

            Sql.Append("SET ");

            Sql.GenerateList(
                updateExpression.SetFields,
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

            return Sql.Build();
        }

        public virtual IRelationalCommand GetCommand(DeleteExpression deleteExpression)
        {
            if (deleteExpression.Limit != null)
            {
                throw new NotSupportedException("PostgreSQL doesn't support LIMIT while executing DELETE.");
            }

            RelationalInternals.InitQuerySqlGenerator(this);
            Sql.Append("DELETE ");

            // Sql.Append(Helper.DelimitIdentifier(deleteExpression.Table.Alias));

            if (deleteExpression.Tables.Any())
            {
                Sql.AppendLine().Append("FROM ");
                Sql.GenerateList(
                    deleteExpression.Tables,
                    e => Visit(e),
                    sql => sql.AppendLine());
            }

            if (deleteExpression.Predicate != null)
            {
                Sql.AppendLine().Append("WHERE ");
                Visit(deleteExpression.Predicate);
            }

            return Sql.Build();
        }

        public virtual IRelationalCommand GetCommand(InsertExpression insertExpression)
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

                change = columnExpression =>
                {
                    if (!columnExpression.Table.Equals(insertExpression.TableChanges))
                        return false;
                    Sql.Append(Helper.DelimitIdentifier(columnExpression.Table.Alias))
                        .Append(".")
                        .Append(Helper.DelimitIdentifier(insertExpression.ColumnChanges[columnExpression.Name]));
                    return true;
                };
            }

            Sql.Append("INSERT INTO ");
            Visit(insertExpression.TargetTable);
            Sql.AppendLine();

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
                change = columnExpression =>
                {
                    if (columnExpression.Table.Equals(insertExpression.TableChanges))
                    {
                        Sql.Append(Helper.DelimitIdentifier("excluded"))
                            .Append(".")
                            .Append(Helper.DelimitIdentifier(insertExpression.ColumnChanges[columnExpression.Name]));
                        return true;
                    }
                    else if (columnExpression.Table.Equals(insertExpression.SourceTable))
                    {
                        Sql.Append(Helper.DelimitIdentifier("excluded"))
                            .Append(".")
                            .Append(Helper.DelimitIdentifier(columnExpression.Name));
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                };

                Sql.Append("ON CONFLICT ON CONSTRAINT ")
                    .Append(Helper.DelimitIdentifier(insertExpression.EntityType.FindPrimaryKey().GetName()))
                    .AppendLine(" DO UPDATE")
                    .Append("SET ")
                    .GenerateList(insertExpression.OnConflictUpdate,
                    e =>
                    {
                        Sql.Append(Helper.DelimitIdentifier(e.Alias)).Append(" = ");
                        Visit(e.Expression);
                    });
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
    }
}