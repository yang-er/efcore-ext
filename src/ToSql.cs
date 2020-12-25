using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Microsoft.EntityFrameworkCore
{
    public static class QueryableToSqlExtensions
    {
        private static readonly Func<IQueryProvider, QueryContextDependencies> _loader;


        static QueryableToSqlExtensions()
        {
            var bindingFlags = BindingFlags.NonPublic | BindingFlags.Instance;
            var param = Expression.Parameter(typeof(IQueryProvider), "query");
            var queryCompiler = Expression.MakeMemberAccess(
                expression: Expression.Convert(param, typeof(EntityQueryProvider)),
                member: typeof(EntityQueryProvider).GetField("_queryCompiler", bindingFlags));
            var queryContextFactory = Expression.MakeMemberAccess(
                expression: Expression.Convert(queryCompiler, typeof(QueryCompiler)),
                member: typeof(QueryCompiler).GetField("_queryContextFactory", bindingFlags));
            var dependencies = Expression.MakeMemberAccess(
                expression: Expression.Convert(queryContextFactory, typeof(RelationalQueryContextFactory)),
                member: typeof(RelationalQueryContextFactory).GetField("_dependencies", bindingFlags));
            var result = Expression.ConvertChecked(dependencies,
                typeof(DbContext).Assembly.GetType(typeof(QueryContextDependencies).FullName));
            var lambda = Expression.Lambda<Func<IQueryProvider, QueryContextDependencies>>(result, param);
            _loader = lambda.Compile();
        }


        public static (string, IEnumerable<object>) ToParametrizedSql<TEntity>(this IQueryable<TEntity> query) where TEntity : class
        {
            var context = query.GetDbContext();
            return BatchOperationExtensions.GetSqlCommand(query, context, "SELECT");
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DbContext GetDbContext(this IQueryable query)
        {
            return _loader(query.Provider).StateManager.Context;
        }


        public static DbContextOptionsBuilder UseBulkExtensions(this DbContextOptionsBuilder optionsBuilder)
        {
            return optionsBuilder.ReplaceService<IQuerySqlGeneratorFactory, EnhancedQuerySqlGeneratorFactory>();
        }


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
