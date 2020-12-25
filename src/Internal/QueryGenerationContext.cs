using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class QueryGenerationContext<T>
    {
        const string SELECT = "SELECT";
        const string INSERT = "INSERT";
        const string DELETE = "DELETE";
        const string UPDATE = "UPDATE";

        public QueryGenerationContext(IEnumerable<T> execution, Expression expression)
        {
            InternalExpression = expression;
            var enumerator = (QueryingEnumerable<T>)execution;
            QueryContext = enumerator.Private<RelationalQueryContext>("_relationalQueryContext");
            CommandCache = enumerator.Private<RelationalCommandCache>("_relationalCommandCache");
            var selectExpression = CommandCache.Private<SelectExpression>("_selectExpression");

            (SelectExpression, _) = CommandCache
                .Private<ParameterValueBasedSelectExpressionOptimizer>("_parameterValueBasedSelectExpressionOptimizer")
                .Optimize(selectExpression, QueryContext.ParameterValues);
        }

        public Expression InternalExpression { get; }

        public RelationalCommandCache CommandCache { get; }

        public RelationalQueryContext QueryContext { get; }

        public SelectExpression SelectExpression { get; }

        public IRelationalCommand GetMergeCommand(MergeExpression mergeExpression)
        {
            return CreateGenerator().GetCommand(mergeExpression);
        }

        public IRelationalCommand GetCommand(string target, IEntityType entityType)
        {
            return target switch
            {
                SELECT => GetSelectCommand(entityType),
                UPDATE => GetUpdateCommand(entityType),
                DELETE => GetDeleteCommand(entityType),
                INSERT => GetSelectIntoCommand(entityType),
                _ => throw new NotImplementedException(),
            };
        }

        public IRelationalCommand GetSelectCommand(IEntityType entityType)
        {
            return CreateGenerator().GetCommand(SelectExpression);
        }

        public IRelationalCommand GetSelectIntoCommand(IEntityType entityType)
        {
            return CreateGenerator().GetCommand(
                new SelectIntoExpression(SelectExpression, entityType));
        }

        public IRelationalCommand GetUpdateCommand(IEntityType entityType)
        {
            return CreateGenerator().GetCommand(
                new UpdateExpression(SelectExpression, entityType, InternalExpression));
        }

        public IRelationalCommand GetDeleteCommand(IEntityType entityType)
        {
            return CreateGenerator().GetCommand(
                new DeleteExpression(SelectExpression, entityType));
        }

        public EnhancedQuerySqlGenerator CreateGenerator()
        {
            return CommandCache
                .Private<IQuerySqlGeneratorFactory>("_querySqlGeneratorFactory")
                .Create() as EnhancedQuerySqlGenerator;
        }

        private SqlParameter CreateParameter(TypeMappedRelationalParameter parInfo)
        {
            var typeMap = Internals.AccessRelationalTypeMapping(parInfo);
            var value = QueryContext.ParameterValues[parInfo.InvariantName];
            if (typeMap.Converter != null)
                value = typeMap.Converter.ConvertToProvider(value);
            var nullable = Internals.AccessIsNullable(parInfo);

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

        private void AddParameter(List<SqlParameter> para, IRelationalParameter parInfo)
        {
            if (parInfo is TypeMappedRelationalParameter parInfo1)
                para.Add(CreateParameter(parInfo1));
            else if (parInfo is CompositeRelationalParameter compo)
                foreach (var smallPar in compo.RelationalParameters)
                    AddParameter(para, smallPar);
            else
                throw new NotSupportedException(parInfo.GetType().Name + " not supported yet.");
        }

        public List<SqlParameter> CreateParameter(IRelationalCommand command)
        {
            var @params = new List<SqlParameter>();
            foreach (var para in command.Parameters)
                AddParameter(@params, para);
            return @params;
        }
    }
}
