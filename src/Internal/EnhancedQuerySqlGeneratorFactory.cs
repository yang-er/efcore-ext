using Microsoft.EntityFrameworkCore.Query;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class EnhancedQuerySqlGeneratorFactory : IQuerySqlGeneratorFactory
    {
        private readonly QuerySqlGeneratorDependencies _dependencies;

        public EnhancedQuerySqlGeneratorFactory(QuerySqlGeneratorDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        public virtual QuerySqlGenerator Create()
            => new EnhancedQuerySqlGenerator(_dependencies);
    }
}
