#if EFCORE50

using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Sqlite.Query
{
    public class SqliteBulkQueryCompilationContextFactory :
        RelationalQueryCompilationContextFactory,
        IBulkQueryCompilationContextFactory,
        IServiceAnnotation<IQueryCompilationContextFactory, RelationalQueryCompilationContextFactory>
    {
        public SqliteBulkQueryCompilationContextFactory(
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