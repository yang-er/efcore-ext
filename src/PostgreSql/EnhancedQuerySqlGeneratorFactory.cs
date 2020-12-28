using Microsoft.EntityFrameworkCore.Query;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.Internal;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class EnhancedQuerySqlGeneratorFactory :
        IEnhancedQuerySqlGeneratorFactory<NpgsqlQuerySqlGeneratorFactory>
    {
        private readonly QuerySqlGeneratorDependencies _dependencies;
        private readonly INpgsqlOptions _npgsqlOptions;

        public EnhancedQuerySqlGeneratorFactory(
            QuerySqlGeneratorDependencies dependencies,
            INpgsqlOptions npgsqlOptions)
        {
            _dependencies = dependencies;
            _npgsqlOptions = npgsqlOptions;
        }

        public virtual QuerySqlGenerator Create()
            => new EnhancedQuerySqlGenerator(
                _dependencies,
                _npgsqlOptions.ReverseNullOrderingEnabled,
                _npgsqlOptions.PostgresVersion);
    }
}