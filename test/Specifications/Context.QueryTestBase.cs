using System;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    [Collection("DatabaseCollection")]
    public abstract class QueryTestBase<TContext, TFactory> : IClassFixture<TFactory>
        where TContext : DbContext
        where TFactory : class, IDbContextFactory<TContext>
    {
        private readonly TFactory _factory;

        protected TContext CreateContext()
        {
            return _factory.Create();
        }

        protected void AssertSql(string sql)
        {
            _factory.CommandTracer.AssertSql(sql.Replace("{{schema}}", _factory.UniqueId));
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
