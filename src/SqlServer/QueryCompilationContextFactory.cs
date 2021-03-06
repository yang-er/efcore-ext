#if EFCORE50

using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.SqlServer.Query.Internal;
using Microsoft.EntityFrameworkCore.SqlServer.Storage.Internal;
using System;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query
{
    public class SqlServerBulkQueryCompilationContextFactory :
        SqlServerQueryCompilationContextFactory,
        IBulkQueryCompilationContextFactory,
        IServiceAnnotation<IQueryCompilationContextFactory, SqlServerQueryCompilationContextFactory>
    {
        public SqlServerBulkQueryCompilationContextFactory(
            BulkQueryCompilationContextDependencies dependencies,
            RelationalQueryCompilationContextDependencies relationalDependencies,
            ISqlServerConnection sqlServerConnection)
            : base(dependencies.Dependencies, relationalDependencies, sqlServerConnection)
        {
        }

        public Func<QueryContext, TResult> CreateQueryExecutor<TResult>(bool async, Expression query)
        {
            return Create(async).CreateQueryExecutor<TResult>(query);
        }
    }
}

#endif