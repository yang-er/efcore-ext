#if EFCORE50 || EFCORE60

using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Query;
using Pomelo.EntityFrameworkCore.MySql.Query.Internal;
using System;
using System.Linq.Expressions;

namespace Pomelo.EntityFrameworkCore.MySql.Query
{
    public class MySqlBulkQueryCompilationContextFactory :
        MySqlQueryCompilationContextFactory,
        IBulkQueryCompilationContextFactory,
        IServiceAnnotation<IQueryCompilationContextFactory, MySqlQueryCompilationContextFactory>
    {
        public MySqlBulkQueryCompilationContextFactory(
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