using Pomelo.EntityFrameworkCore.MySql.Storage;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class MySqlContextFactory<TContext> : RelationalContextFactoryBase<TContext>
        where TContext : DbContext
    {
        public ServerVersion ServerVersion { get; private set; }

        protected override string ScriptSplit => ";\r\n\r\n";

        protected override string DropTableCommand => "DROP TABLE IF EXISTS `{0}`";

        protected override void Configure(DbContextOptionsBuilder optionsBuilder)
        {
            var connectionString =
                $"Server=localhost;" +
                $"Port=3306;" +
                $"Database=efcorebulktest{Suffix};" +
                $"User=root;" +
                $"Password=Password12!;" +
                $"Character Set=utf8;" +
                $"TreatTinyAsBoolean=true;";

            ServerVersion = ServerVersion.AutoDetect(connectionString);

#if EFCORE50 || EFCORE60
            optionsBuilder.UseMySql(
                connectionString,
                ServerVersion,
                s => s.UseBulk());
#elif EFCORE31
            optionsBuilder.UseMySql(
                connectionString,
                s => s.UseBulk()
                      .ServerVersion(ServerVersion));
#endif
        }
    }
}
