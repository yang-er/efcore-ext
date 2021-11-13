#if EFCORE50 || EFCORE60

namespace Microsoft.EntityFrameworkCore.Query
{
    public interface IRelationalBulkParameterBasedSqlProcessorFactory : IRelationalParameterBasedSqlProcessorFactory
    {
    }
}

#endif