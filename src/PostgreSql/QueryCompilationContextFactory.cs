#if EFCORE50 || EFCORE60

using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Query;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;
using System;
using System.Linq.Expressions;

namespace Npgsql.EntityFrameworkCore.PostgreSQL.Query
{
    public class NpgsqlBulkQueryCompilationContextFactory :
        NpgsqlQueryCompilationContextFactory,
        IBulkQueryCompilationContextFactory,
        IServiceAnnotation<IQueryCompilationContextFactory, NpgsqlQueryCompilationContextFactory>
    {
        public NpgsqlBulkQueryCompilationContextFactory(
            BulkQueryCompilationContextDependencies dependencies,
            RelationalQueryCompilationContextDependencies relationalDependencies)
            : base(dependencies.Dependencies, relationalDependencies)
        {
        }

        public Func<QueryContext, TResult> CreateQueryExecutor<TResult>(bool async, Expression query)
        {
            return Create(async).CreateQueryExecutor<TResult>(query);
        }
    }
}

#endif