using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;

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

        public static bool IsSqlite(this DatabaseFacade database)
            => database.ProviderName.Equals(
                "Microsoft.EntityFrameworkCore.Sqlite",
                StringComparison.Ordinal);

        public static bool IsNpgsql(this DatabaseFacade database)
            => database.ProviderName.Equals(
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal);

        public static string ToSQL<TSource>(this IQueryable<TSource> queryable) where TSource : class
        {
            var enumerable = queryable.Provider.Execute<IEnumerable<TSource>>(queryable.Expression);
            var type = enumerable.GetType().GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var queryContext = (QueryContext)type.Single(e => e.Name == "_relationalQueryContext").GetValue(enumerable);
            var commandCache = type.Single(e => e.Name == "_relationalCommandCache").GetValue(enumerable);
            var command = commandCache.GetType().GetMethod("GetRelationalCommand").Invoke(commandCache, new[] { queryContext.ParameterValues });
            return (string)command.GetType().GetProperty("CommandText").GetValue(command);
        }

        public static IQueryable<T> FromSqlRaw<T>(this DbSet<T> dbset, string sql, params object[] parameters) where T : class
        {
            return (IQueryable<T>)AppDomain.CurrentDomain.GetAssemblies()
                .Single(a => a.GetName().Name == "Microsoft.EntityFrameworkCore.Relational")
                .GetType("Microsoft.EntityFrameworkCore.RelationalQueryableExtensions")
                .GetMethod("FromSqlRaw")
                .MakeGenericMethod(typeof(T))
                .Invoke(null, new object[] { dbset, sql, parameters });
        }
    }
}
