using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Sqlite.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Linq;
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
                ExcludedTableColumnExpression _ => throw new InvalidOperationException(),
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
            if (updateExpression.Expanded)
            {
                throw new InvalidOperationException("Update expression accidently expanded.");
            }

            Sql.Append("UPDATE ");

            string tableExp = Helper.DelimitIdentifier(updateExpression.Tables[0].Alias);

            Sql.AppendLine(tableExp).Append("SET ");

            Sql.GenerateList(updateExpression.Fields, e =>
            {
                Sql.Append(tableExp)
                    .Append(".")
                    .Append(Helper.DelimitIdentifier(e.Alias))
                    .Append(" = ");
                Visit(e.Expression);
            });

            if (updateExpression.Tables.Any())
            {
                Sql.AppendLine().Append("FROM ");
                Sql.GenerateList(updateExpression.Tables, e => Visit(e), sql => sql.AppendLine());
            }

            if (updateExpression.Predicate != null)
            {
                Sql.AppendLine().Append("WHERE ");
                Visit(updateExpression.Predicate);
            }

            return updateExpression;
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
