using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class EnhancedQuerySqlGenerator : SqlServerQuerySqlGenerator, IEnhancedQuerySqlGenerator
    {
        public EnhancedQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies)
            : base(dependencies)
        {
        }

        public ISqlGenerationHelper Helper => Dependencies.SqlGenerationHelper;

        protected override Expression VisitExtension(Expression extensionExpression)
        {
            return extensionExpression switch
            {
                ValuesExpression values => VisitValues(values),
                _ => base.VisitExtension(extensionExpression),
            };
        }

        protected virtual Expression VisitWrapped(WrappedExpression wrappedExpression)
        {
            return wrappedExpression switch
            {
                DeleteExpression deleteExpression => VisitDelete(deleteExpression),
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

            var paramName = tableExpression.RuntimeParameter;
            Sql.AddParameter(
                new ValuesRelationalParameter(
                    tableExpression.AnonymousType,
                    Helper.GenerateParameterName(paramName),
                    paramName));

            tableExpression.Generate(
                Sql,
                Helper.GenerateParameterNamePlaceholder(paramName));

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

        public virtual IRelationalCommand GetCommand(SelectIntoExpression selectIntoExpression)
        {
            RelationalInternals.InitQuerySqlGenerator(this);

            var selectExpression = selectIntoExpression.Expression;
            GenerateTagsHeaderComment(selectExpression);

            Sql.Append("INSERT INTO ")
                .Append(Helper.DelimitIdentifier(selectIntoExpression.TableName, selectIntoExpression.Schema))
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
            if (updateExpression.Expanded)
                throw new InvalidOperationException("Update expression accidently expanded.");

            RelationalInternals.InitQuerySqlGenerator(this);
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

            return Sql.Build();
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

            Sql.Append(Helper.DelimitIdentifier(deleteExpression.Table.Alias));

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

        public virtual IRelationalCommand GetCommand(MergeExpression mergeExpression)
        {
            RelationalInternals.InitQuerySqlGenerator(this);

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
            return Sql.Build();
        }

        public object CreateParameter(QueryContext context, TypeMappedRelationalParameter parInfo)
        {
            var typeMap = RelationalInternals.AccessRelationalTypeMapping(parInfo);
            var value = context.ParameterValues[parInfo.InvariantName];
            if (typeMap.Converter != null)
                value = typeMap.Converter.ConvertToProvider(value);
            var nullable = RelationalInternals.AccessIsNullable(parInfo);

            var parameter = new SqlParameter
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

        public IRelationalCommand GetCommand(UpsertExpression upsertExpression)
        {
            throw new NotSupportedException();
        }
    }
}
