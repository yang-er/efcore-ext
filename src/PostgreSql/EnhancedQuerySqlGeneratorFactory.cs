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
        private readonly ISqlExpressionFactory _sqlExpressionFactory;

        public EnhancedQuerySqlGeneratorFactory(
            QuerySqlGeneratorDependencies dependencies,
            ISqlExpressionFactory sqlExpressionFactory,
            INpgsqlOptions npgsqlOptions)
        {
            _dependencies = dependencies;
            _npgsqlOptions = npgsqlOptions;
            _sqlExpressionFactory = sqlExpressionFactory;
        }

        public virtual QuerySqlGenerator Create()
            => new EnhancedQuerySqlGenerator(
                _dependencies,
                _sqlExpressionFactory,
                _npgsqlOptions.ReverseNullOrderingEnabled,
                _npgsqlOptions.PostgresVersion);
    }
}