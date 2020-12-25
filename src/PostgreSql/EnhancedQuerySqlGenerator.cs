using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;

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

            GenerateList(updateExpression.SetFields, e =>
            {
                Sql.Append(Helper.DelimitIdentifier(e.Alias))
                    .Append(" = ");
                Visit(e.Expression);
            });

            if (updateExpression.Tables.Count > 0)
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
                GenerateList(deleteExpression.Tables, e => Visit(e), sql => sql.AppendLine());
            }

            if (deleteExpression.Predicate != null)
            {
                Sql.AppendLine().Append("WHERE ");
                Visit(deleteExpression.Predicate);
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