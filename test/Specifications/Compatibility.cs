using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace Microsoft.EntityFrameworkCore
{
    internal static class BulkTestCompatibility
    {
        public static EntityTypeBuilder<TEntity> ToTable<TEntity>(
            this EntityTypeBuilder<TEntity> entityTypeBuilder,
            string name)
            where TEntity : class
        {
            entityTypeBuilder.Metadata.SetAnnotation("Relational:TableName", name);
            entityTypeBuilder.Metadata.SetAnnotation("Relational:Schema", null);
            return entityTypeBuilder;
        }

        public static PropertyBuilder HasComputedColumnSql(
            this PropertyBuilder propertyBuilder,
            string sql,
            bool? stored)
        {
            propertyBuilder.Metadata.SetOrRemoveAnnotation("Relational:ComputedColumnSql", sql);
            propertyBuilder.Metadata.SetOrRemoveAnnotation("Relational:IsStored", stored);
            return propertyBuilder;
        }

        public static bool IsInMemory(this DatabaseFacade database)
            => database.ProviderName.Equals(
                "Microsoft.EntityFrameworkCore.InMemory",
                StringComparison.Ordinal);

        public static bool IsSqlServer(this DatabaseFacade database)
            => database.ProviderName.Equals(
                "Microsoft.EntityFrameworkCore.SqlServer",
                StringComparison.Ordinal);

        public static bool IsNpgsql(this DatabaseFacade database)
            => database.ProviderName.Equals(
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal);
    }
}
