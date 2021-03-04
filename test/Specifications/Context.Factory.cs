using Microsoft.Extensions.Logging;
using System;
using System.Linq.Expressions;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public interface IDbContextFactory<TContext> : IDisposable
        where TContext : DbContext
    {
        ILoggerFactory LoggerFactory { get; }

        CommandTracer CommandTracer { get; }

        string UniqueId { get; }

        DbContextOptions Options { get; }

        TContext Create();
    }

    public abstract class ContextFactoryBase<TContext> :
        IDbContextFactory<TContext>,
        IClassFixture<ContextLoggerFactory>
        where TContext : DbContext
    {
        private Func<TContext> _factory;
        private DbContextOptions _options;

        public ILoggerFactory LoggerFactory { get; }

        public CommandTracer CommandTracer { get; }

        public string UniqueId { get; }

        public DbContextOptions Options => _options;

        public virtual TContext Create()
        {
            return _factory();
        }

        protected ContextFactoryBase(ContextLoggerFactory loggerFactory)
        {
            var contextType = typeof(TContext);
            var constructor = contextType.GetConstructor(new[] { typeof(DbContextOptions), typeof(string) });
            if (constructor == null)
            {
                throw new InvalidOperationException("The DbContext type must have a constructor of .ctor(string schema, DbContextOptions options).");
            }

            LoggerFactory = loggerFactory.Instance;
            CommandTracer = new CommandTracer();
            UniqueId = Guid.NewGuid().ToString()[0..6];

            var optionsBuilder = new DbContextOptionsBuilder<TContext>();
            Configure(optionsBuilder);
            PostConfigure(optionsBuilder);
            _options = optionsBuilder.Options;

            _factory = Expression.Lambda<Func<TContext>>(
                Expression.New(
                    constructor,
                    Expression.Constant(_options),
                    Expression.Constant(UniqueId)))
                .Compile();

            using (var context = _factory())
            {
                EnsureCreated(context);
            }
        }

        protected virtual void PostConfigure(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLoggerFactory(LoggerFactory);
        }

        protected abstract void Configure(DbContextOptionsBuilder optionsBuilder);

        protected abstract void EnsureCreated(TContext context);

        protected abstract void EnsureDeleted(TContext context);

        public void Dispose()
        {
            using (var context = _factory())
            {
                EnsureDeleted(context);
            }

            _factory = null;
            _options = null;
        }
    }
}
