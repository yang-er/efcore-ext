using Microsoft.EntityFrameworkCore.Metadata;
using System;
using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public abstract class RelationalContextFactoryBase<TContext> :
        ContextFactoryBase<TContext>
        where TContext : DbContext
    {
        protected string Suffix { get; }

        protected abstract string ScriptSplit { get; }

        protected abstract string DropTableCommand { get; }

        protected RelationalContextFactoryBase()
        {
#if EFCORE31
            Suffix = "v31";
#elif EFCORE50
            Suffix = "v50";
#elif EFCORE60
            Suffix = "v60";
#endif
        }

        protected override void PostConfigure(DbContextOptionsBuilder optionsBuilder)
        {
            base.PostConfigure(optionsBuilder);
            optionsBuilder.AddInterceptors(new CommandInterceptor(CommandTracer));
        }

        protected override void EnsureCreated(TContext context)
        {
            if (!context.Database.EnsureCreated())
            {
                var script = context.Database.GenerateCreateScript();
                foreach (var line in script.Trim().Split(ScriptSplit, StringSplitOptions.RemoveEmptyEntries))
                {
                    context.Database.ExecuteSqlRaw(line.Trim());
                }
            }
        }

        protected override void EnsureDeleted(TContext context)
        {
            List<IEntityType> entityTypes = new();

            void RecursiveSearch(IEntityType entityType)
            {
                foreach (IForeignKey foreignKey in entityType.GetForeignKeys())
                {
                    if (foreignKey.PrincipalEntityType != entityType)
                    {
                        if (!entityTypes.Contains(foreignKey.PrincipalEntityType))
                        {
                            RecursiveSearch(foreignKey.PrincipalEntityType);
                        }
                    }
                }

                if (!entityTypes.Contains(entityType))
                {
                    entityTypes.Add(entityType);
                }
            }

            foreach (IEntityType entityType in context.Model.GetEntityTypes())
            {
                RecursiveSearch(entityType);
            }

            entityTypes.Reverse();
            foreach (var item in entityTypes)
            {
                var tableName = item.GetTableName();
                if (tableName != null)
                {
                    context.Database.ExecuteSqlRaw(string.Format(DropTableCommand, tableName));
                }
            }
        }
    }
}
