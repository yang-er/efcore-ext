using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
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

        protected virtual Expression VisitValues(ValuesExpression tableExpression)
        {
            Sql.Append("(")
                .IncrementIndent()
                .AppendLine()
                .AppendLine("VALUES");

            tableExpression.Generate(this, Sql, Helper);

            Sql.DecrementIndent()
                .AppendLine()
                .Append(")")
                .Append(AliasSeparator)
                .Append(Helper.DelimitIdentifier(tableExpression.Alias))
                .Append(" (");

            Sql.GenerateList(
                tableExpression.ColumnNames,
                a => Sql.Append(Helper.DelimitIdentifier(a)));

            Sql.Append(")");
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
            Sql.AppendLine();

            Sql.Append("(")
                .GenerateList(upsertExpression.Columns, e => Sql.Append(Helper.DelimitIdentifier(e.Alias)))
                .AppendLine(")");

            if (upsertExpression.SourceTable != null)
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
                    .AppendLine(") DO UPDATE")
                    .Append("SET ")
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
