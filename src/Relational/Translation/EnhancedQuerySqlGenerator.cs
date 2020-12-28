using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public interface IEnhancedQuerySqlGenerator
    {
        object CreateParameter(QueryContext context, TypeMappedRelationalParameter parInfo);

        IRelationalCommand GetCommand(SelectExpression selectExpression);

        IRelationalCommand GetCommand(SelectIntoExpression selectIntoExpression);

        IRelationalCommand GetCommand(UpdateExpression updateExpression);

        IRelationalCommand GetCommand(DeleteExpression deleteExpression);

        IRelationalCommand GetCommand(MergeExpression mergeExpression);

        IRelationalCommand GetCommand(UpsertExpression upsertExpression);
    }

    public interface IEnhancedQuerySqlGeneratorFactory<TOldFactory> : IQuerySqlGeneratorFactory
    {
    }
}
