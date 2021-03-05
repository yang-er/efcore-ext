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

        protected TContext CreateContext()
        {
            return _factory.Create();
        }

        protected void AssertSql(string sql)
        {
            _factory.CommandTracer.AssertSql(sql.Replace("{{schema}}", _factory.UniqueId));
        }

        [Conditional("EFCORE31")]
        protected void AssertSql31(string sql) => AssertSql(sql);

        [Conditional("EFCORE50")]
        protected void AssertSql50(string sql) => AssertSql(sql);

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
