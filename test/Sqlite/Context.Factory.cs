using Microsoft.Data.Sqlite;
using System;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public static class SqliteConnectionHolder
    {
        public static SqliteConnection Singleton { get; }
            = new Func<SqliteConnection>(delegate
            {
                var connection = new SqliteConnection("Filename=:memory:");
                connection.Open();
                return connection;
            })
            .Invoke();
    }

    public class SqliteContextFactory<TContext> : RelationalContextFactoryBase<TContext>
        where TContext : DbContext
    {
        protected override string ScriptSplit => ";\r\n\r\n";

        protected override string DropTableCommand => "DROP TABLE IF EXISTS \"{0}\"";

        protected override void Configure(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(
                SqliteConnectionHolder.Singleton,
                s => s.UseBulk());
        }
    }
}
