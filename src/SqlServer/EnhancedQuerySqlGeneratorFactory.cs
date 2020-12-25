using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class EnhancedQuerySqlGeneratorFactory :
        IEnhancedQuerySqlGeneratorFactory<SqlServerQuerySqlGeneratorFactory>
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
