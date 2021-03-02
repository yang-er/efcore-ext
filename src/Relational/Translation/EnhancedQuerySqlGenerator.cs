using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public interface IEnhancedQuerySqlGenerator
    {
        IRelationalCommand GetCommand(MergeExpression mergeExpression);

        IRelationalCommand GetCommand(UpsertExpression upsertExpression);
    }

    public interface IEnhancedQuerySqlGeneratorFactory<TOldFactory> : IQuerySqlGeneratorFactory
    {
    }
}
