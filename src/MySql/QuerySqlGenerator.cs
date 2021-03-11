using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure.Internal;
using Pomelo.EntityFrameworkCore.MySql.Query.ExpressionVisitors.Internal;
using Pomelo.EntityFrameworkCore.MySql.Query.Internal;
using Pomelo.EntityFrameworkCore.MySql.Storage;
using System;
using System.Linq.Expressions;

namespace Pomelo.EntityFrameworkCore.MySql.Query
{
    public class MySqlBulkQuerySqlGenerator : MySqlQuerySqlGenerator
    {
        private readonly ServerVersion _serverVersion;
        private readonly ISqlExpressionFactory _sqlExpressionFactory;

        public MySqlBulkQuerySqlGenerator(
            QuerySqlGeneratorDependencies dependencies,
            MySqlSqlExpressionFactory sqlExpressionFactory,
            IMySqlOptions options)
            : base(dependencies, sqlExpressionFactory, options)
        {
            _serverVersion = options.ServerVersion;
            _sqlExpressionFactory = sqlExpressionFactory;
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
                    Sql.Append("ROW(");

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
                    Sql.Append("ROW(")
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
            // MariaDB >= 10.3.3 -> VALUE(`identifier`)
            // MariaDB <= 10.3.2 -> VALUES(`identifier`)
            // MySQL >= 8.0.19 -> `excluded`.`identifier`
            // MySQL <= 8.0.18 -> VALUES(`identifier`)

            if (_serverVersion.Type == ServerType.MySql)
            {
                if (_serverVersion.Version < new Version(8, 0, 19))
                {
                    Sql.Append("VALUES(")
                        .Append(Helper.DelimitIdentifier(excludedTableColumnExpression.Name))
                        .Append(")");
                }
                else
                {
                    Sql.Append(Helper.DelimitIdentifier("excluded"))
                        .Append(".")
                        .Append(Helper.DelimitIdentifier(excludedTableColumnExpression.Name));
                }
            }
            else if (_serverVersion.Type == ServerType.MariaDb)
            {
                if (_serverVersion.Version < new Version(10, 3, 3))
                {
                    Sql.Append("VALUES(")
                        .Append(Helper.DelimitIdentifier(excludedTableColumnExpression.Name))
                        .Append(")");
                }
                else
                {
                    Sql.Append("VALUE(")
                        .Append(Helper.DelimitIdentifier(excludedTableColumnExpression.Name))
                        .Append(")");
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "The plugin currently doesn't support " + _serverVersion + ", " +
                    "please contact the plugin author to provide further details.");
            }

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
            if (updateExpression.Expanded)
            {
                throw new InvalidOperationException("Update expression accidently expanded.");
            }

            string tableExp = Helper.DelimitIdentifier(updateExpression.Tables[0].Alias);

            Sql.Append("UPDATE ")
                .GenerateList(
                    updateExpression.Tables,
                    e => Visit(e),
                    sql => sql.AppendLine());

            Sql.AppendLine().Append("SET ");

            Sql.GenerateList(updateExpression.Fields, e =>
            {
                Sql.Append(tableExp)
                    .Append(".")
                    .Append(Helper.DelimitIdentifier(e.Alias))
                    .Append(" = ");
                Visit(e.Expression);
            });

            if (updateExpression.Predicate != null)
            {
                Sql.AppendLine().Append("WHERE ");
                Visit(updateExpression.Predicate);
            }

            return updateExpression;
        }

        protected virtual Expression VisitDelete(DeleteExpression deleteExpression)
        {
            Sql.Append("DELETE ");

            if (deleteExpression.JoinedTables.Count > 0)
            {
                Sql.Append("FROM ");
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
            Sql.Append("INSERT ")
                .AppendIf(upsertExpression.OnConflictUpdate == null, "IGNORE ")
                .Append("INTO ")
                .Append(Helper.DelimitIdentifier(upsertExpression.TargetTable.Name))
                .Append(" (")
                .GenerateList(upsertExpression.Columns, e => Sql.Append(Helper.DelimitIdentifier(e.Alias)))
                .AppendLine(")");

            if (upsertExpression.SourceTable is ValuesExpression valuesExpression)
            {
                TransientExpandValuesExpression.Process(
                    valuesExpression,
                    upsertExpression.Columns,
                    this, Sql, Helper);
            }
            else if (upsertExpression.SourceTable != null)
            {
                Sql.Append("SELECT ")
                    .GenerateList(upsertExpression.Columns, e => Visit(e.Expression))
                    .AppendLine();

                Sql.Append("FROM ");
                Visit(upsertExpression.SourceTable);
            }
            else
            {
                Sql.Append("VALUES (")
                    .GenerateList(upsertExpression.Columns, e => Visit(e.Expression))
                    .Append(")");
            }

            if (upsertExpression.OnConflictUpdate != null)
            {
                Sql.AppendLine()
                    .Append("ON DUPLICATE KEY UPDATE ")
                    .GenerateList(
                        upsertExpression.OnConflictUpdate,
                        e => Sql.Append(Helper.DelimitIdentifier(e.Alias)).Append(" = ").Then(() => Visit(e.Expression)));
            }

            return upsertExpression;
        }
    }

    public class MySqlBulkQuerySqlGeneratorFactory :
        IBulkQuerySqlGeneratorFactory,
        IServiceAnnotation<IQuerySqlGeneratorFactory, MySqlQuerySqlGeneratorFactory>
    {
        private readonly QuerySqlGeneratorDependencies _dependencies;
        private readonly IMySqlOptions _mysqlOptions;
        private readonly MySqlSqlExpressionFactory _sqlExpressionFactory;

        public MySqlBulkQuerySqlGeneratorFactory(
            QuerySqlGeneratorDependencies dependencies,
            ISqlExpressionFactory sqlExpressionFactory,
            IMySqlOptions mysqlOptions)
        {
            _dependencies = dependencies;
            _mysqlOptions = mysqlOptions;
            _sqlExpressionFactory = (MySqlSqlExpressionFactory)sqlExpressionFactory;
        }

        public virtual QuerySqlGenerator Create()
            => new MySqlBulkQuerySqlGenerator(
                _dependencies,
                _sqlExpressionFactory,
                _mysqlOptions);
    }
}
