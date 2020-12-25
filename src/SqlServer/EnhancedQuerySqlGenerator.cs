using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class EnhancedQuerySqlGenerator : SqlServerQuerySqlGenerator, IEnhancedQuerySqlGenerator
    {
        private Func<ColumnExpression, bool> change = c => false;

        public EnhancedQuerySqlGenerator(QuerySqlGeneratorDependencies dependencies)
            : base(dependencies)
        {
        }

        public ISqlGenerationHelper Helper => Dependencies.SqlGenerationHelper;

        public QuerySqlGenerator Self => this;

        private void GenerateList<T>(
            IReadOnlyList<T> items,
            Action<T> generationAction,
            Action<IRelationalCommandBuilder> joinAction = null)
        {
            joinAction ??= (isb => isb.Append(", "));

            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0) joinAction(Sql);
                generationAction(items[i]);
            }
        }

        protected override Expression VisitColumn(ColumnExpression columnExpression)
        {
            if (!change(columnExpression))
                base.VisitColumn(columnExpression);
            return columnExpression;
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
            Sql.Append("(VALUES ")
                .IncrementIndent()
                .AppendLine();

            GenerateList(tableExpression.Values, a =>
            {
                Sql.Append("(");
                GenerateList(a, e => Visit(e));
                Sql.Append(")");
            },
            sql => sql.Append(",").AppendLine());

            Sql.DecrementIndent()
                .AppendLine()
                .Append(")")
                .Append(AliasSeparator)
                .Append(Helper.DelimitIdentifier(tableExpression.Alias))
                .Append(" (");

            GenerateList(tableExpression.ColumnNames,
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
                .Append(Helper.DelimitIdentifier(selectIntoExpression.Table.Name, selectIntoExpression.Table.Schema))
                .Append(" (");

            GenerateList(selectExpression.Projection, e => Sql.Append(Helper.DelimitIdentifier(e.Alias)));

            Sql.AppendLine(")");

            VisitSelect(selectExpression);

            return Sql.Build();
        }

        public virtual IRelationalCommand GetCommand(UpdateExpression updateExpression)
        {
            RelationalInternals.InitQuerySqlGenerator(this);
            Sql.Append("UPDATE ");

            if (updateExpression.Limit != null)
            {
                Sql.Append("TOP(");
                Visit(updateExpression.Limit);
                Sql.Append(") ");
            }

            string tableExp = Helper.DelimitIdentifier(updateExpression.Table.Alias);

            Sql.AppendLine(tableExp).Append("SET ");

            GenerateList(updateExpression.SetFields, e =>
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
                GenerateList(updateExpression.Tables, e => Visit(e), sql => sql.AppendLine());
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
            Sql.Append("DELETE ");

            if (deleteExpression.Limit != null)
            {
                Sql.Append("TOP(");
                Visit(deleteExpression.Limit);
                Sql.Append(") ");
            }

            Sql.Append(Helper.DelimitIdentifier(deleteExpression.Table.Alias));

            if (deleteExpression.Tables.Any())
            {
                Sql.AppendLine().Append("FROM ");
                GenerateList(deleteExpression.Tables, e => Visit(e), sql => sql.AppendLine());
            }

            if (deleteExpression.Predicate != null)
            {
                Sql.AppendLine().Append("WHERE ");
                Visit(deleteExpression.Predicate);
            }

            return Sql.Build();
        }

        public virtual IRelationalCommand GetCommand(MergeExpression mergeExpression)
        {
            RelationalInternals.InitQuerySqlGenerator(this);

            var targetAlias = mergeExpression.TargetTable.Alias;
            var sourceAlias = mergeExpression.SourceTable.Alias;

            change = columnExpression =>
            {
                if (!columnExpression.Table.Equals(mergeExpression.TableChanges))
                    return false;
                Sql.Append(Helper.DelimitIdentifier(columnExpression.Table.Alias))
                    .Append(".")
                    .Append(Helper.DelimitIdentifier(mergeExpression.ColumnChanges[columnExpression.Name]));
                return true;
            };

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
                    GenerateList(mergeExpression.Matched, e =>
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
                    Sql.AppendLine().Append("THEN INSERT (");
                    GenerateList(mergeExpression.NotMatchedByTarget,
                        e => Sql.Append(Helper.DelimitIdentifier(e.Alias)));
                    Sql.Append(") VALUES (");
                    GenerateList(mergeExpression.NotMatchedByTarget,
                        e => Visit(e.Expression));
                    Sql.Append(")");
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
    }
}
