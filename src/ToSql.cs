using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Microsoft.EntityFrameworkCore
{
    public static class QueryableToSqlExtensions
    {
        public static (string, IEnumerable<object>) ToParametrizedSql<TEntity>(this IQueryable<TEntity> query) where TEntity : class
        {
            var context = query.GetDbContext();
            return BatchOperationExtensions.GetSqlCommand(query, context, "SELECT");
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static DbContext GetDbContext(this IQueryable query)
        {
            return Internals.AccessDependencies(query.Provider).StateManager.Context;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static DbContextOptionsBuilder UseBulkExtensions(this DbContextOptionsBuilder optionsBuilder)
        {
            return optionsBuilder.ReplaceService<IQuerySqlGeneratorFactory, EnhancedQuerySqlGeneratorFactory>();
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static Dictionary<string, string> GetColumns(
            this IEntityType entityType)
        {
            var normalProps = entityType.GetProperties()
                .Select(p => (p.Name, p.GetColumnName()));
            var ownerProps =
                from nav in entityType.GetNavigations()
                where nav.GetTargetType().IsOwned()
                let prop = nav.PropertyInfo
                from col in nav.GetTargetType().GetProperties()
                select ($"{prop.Name}.{col.Name}", col.GetColumnName());
            return normalProps.Concat(ownerProps)
                .ToDictionary(k => k.Item1, v => v.Item2);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static Dictionary<string, ValueConverter> GetValueConverters(
            this IEntityType entityType)
        {
            var normalProps =
                from p in entityType.GetProperties()
                let conv = p.GetValueConverter()
                where conv != null
                select (p.GetColumnName(), conv);
            var ownerProps =
                from nav in entityType.GetNavigations()
                where nav.GetTargetType().IsOwned()
                let prop = nav.PropertyInfo
                from col in nav.GetTargetType().GetProperties()
                let conv = col.GetValueConverter()
                where conv != null
                select (col.GetColumnName(), conv);
            return normalProps.Concat(ownerProps)
                .Distinct()
                .ToDictionary(k => k.Item1, v => v.Item2);
        }
    }
}
