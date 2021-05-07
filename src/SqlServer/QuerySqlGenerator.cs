using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query
{
    public class SqlServerBulkQuerySqlGenerator : SqlServerQuerySqlGenerator
    {
        public SqlServerBulkQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies)
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
                MergeExpression mergeExpression => VisitMerge(mergeExpression),
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
            if (valuesExpression.TupleCount == 0)
            {
                Sql.Append("(")
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
                    .Append(")")
                    .Append(AliasSeparator)
                    .Append(Helper.DelimitIdentifier(valuesExpression.Alias));

                return valuesExpression;
            }

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

            return updateExpression;
        }

        protected virtual Expression VisitDelete(DeleteExpression deleteExpression)
        {
            Sql.Append("DELETE ");

            Sql.Append(Helper.DelimitIdentifier(deleteExpression.Table.Alias));

            if (deleteExpression.JoinedTables.Count > 0)
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

        protected virtual Expression VisitMerge(MergeExpression mergeExpression)
        {
            var targetAlias = mergeExpression.TargetTable.Alias;
            var sourceAlias = mergeExpression.SourceTable.Alias;

            Sql.Append("MERGE INTO ");
            VisitTable(mergeExpression.TargetTable);
            Sql.AppendLine().Append("USING ");
            Visit(mergeExpression.SourceTable);

            using (Sql.Indent())
            {
                Sql.AppendLine().Append("ON ");
                Visit(mergeExpression.JoinPredicate);
            }

            if (mergeExpression.Matched != null)
            {
                Sql.AppendLine().Append("WHEN MATCHED");
                using (Sql.Indent())
                {
                    Sql.AppendLine().Append("THEN UPDATE SET ");

                    Sql.GenerateList(mergeExpression.Matched, e =>
                    {
                        Sql.Append(Helper.DelimitIdentifier(targetAlias))
                            .Append(".")
                            .Append(Helper.DelimitIdentifier(e.Alias))
                            .Append(" = ");
                        Visit(e.Expression);
                    });
                }
            }

            if (mergeExpression.NotMatchedByTarget != null)
            {
                Sql.AppendLine().Append("WHEN NOT MATCHED BY TARGET");
                using (Sql.Indent())
                {
                    Sql.AppendLine().Append("THEN INSERT (")
                        .GenerateList(
                            mergeExpression.NotMatchedByTarget,
                            e => Sql.Append(Helper.DelimitIdentifier(e.Alias)))
                        .Append(") VALUES (")
                        .GenerateList(
                            mergeExpression.NotMatchedByTarget,
                            e => Visit(e.Expression))
                        .Append(")");
                }
            }

            if (mergeExpression.NotMatchedBySource)
            {
                Sql.AppendLine().Append("WHEN NOT MATCHED BY SOURCE");
                using (Sql.Indent())
                    Sql.AppendLine().Append("THEN DELETE");
            }

            Sql.Append(";");
            return mergeExpression;
        }
    }

    public class SqlServerBulkQuerySqlGeneratorFactory :
        IBulkQuerySqlGeneratorFactory,
        IServiceAnnotation<IQuerySqlGeneratorFactory, SqlServerQuerySqlGeneratorFactory>
    {
        private readonly QuerySqlGeneratorDependencies _dependencies;

        public SqlServerBulkQuerySqlGeneratorFactory(
            QuerySqlGeneratorDependencies dependencies)
        {
            Check.NotNull(dependencies, nameof(dependencies));

            _dependencies = dependencies;
        }

        public virtual QuerySqlGenerator Create()
        {
            return new SqlServerBulkQuerySqlGenerator(_dependencies);
        }
    }
}
