using Microsoft.EntityFrameworkCore.TestUtilities.Xunit;
using System;
using System.Diagnostics;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    [Collection("DatabaseCollection")]
    public abstract class QueryTestBase<TContext, TFactory> : IClassFixture<TFactory>
        where TContext : DbContext
        where TFactory : class, IDbContextFactory<TContext>
    {
        private readonly TFactory _factory;

        protected const DatabaseProvider V31 = DatabaseProvider.Version_31;
        protected const DatabaseProvider V50 = DatabaseProvider.Version_50;
        protected const DatabaseProvider V60 = DatabaseProvider.Version_60;

        protected TContext CreateContext()
        {
            return _factory.Create();
        }

        protected void AssertSql(string sql)
        {
            _factory.CommandTracer.AssertSql(sql.Replace("{{schema}}", _factory.UniqueId));
        }

        protected void AssertSql(DatabaseProvider version, string sql)
        {
#if EFCORE31
            if ((version & V31) == V31) AssertSql(sql);
#elif EFCORE50
            if ((version & V50) == V50) AssertSql(sql);
#elif EFCORE60
            if ((version & V60) == V60) AssertSql(sql);
#endif
        }

        protected IDisposable CatchCommand()
        {
            return _factory.CommandTracer.BeginScope();
        }

        protected QueryTestBase(TFactory factory)
        {
            _factory = factory;
        }
    }
}
