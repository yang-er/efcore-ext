using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Cacheable
{
    /// <summary>
    /// The cached queryable extension methods.
    /// </summary>
    public static class CachedQueryable
    {
        /// <summary>
        /// Support storing context data by using <see cref="IMemoryCache"/>.
        /// </summary>
        /// <param name="builder">The options builder.</param>
        /// <returns>The options builder.</returns>
        public static DbContextOptionsBuilder UseCache(this DbContextOptionsBuilder builder)
        {
            var builder1 = builder as IDbContextOptionsBuilderInfrastructure;
            builder1.AddOrUpdateExtension(new CacheDbContextOptionsExtension());
            return builder;
        }

        /// <summary>
        /// The <see cref="IMemoryCache"/> type to get from the service provider.
        /// </summary>
        internal interface IDbContextCache : IMemoryCache
        {
            IServiceProvider Services { get; }
        }

        /// <summary>
        /// The default implement.
        /// </summary>
        internal class DefaultDbContextCache : MemoryCache, IDbContextCache
        {
            private static readonly MemoryCacheOptions _options =
                new MemoryCacheOptions { Clock = new SystemClock() };

            public IServiceProvider Services { get; }

            public DefaultDbContextCache(IServiceProvider sp) : base(_options)
            {
                Services = sp;
            }
        }

        /// <summary>
        /// The extension to add to the options.
        /// </summary>
        internal class CacheDbContextOptionsExtension : IDbContextOptionsExtension
        {
            private DbContextOptionsExtensionInfo _info;

            public DbContextOptionsExtensionInfo Info =>
                _info ??= new CacheDbContextOptionsExtensionInfo(this);

            public void ApplyServices(IServiceCollection services)
            {
                services.AddSingleton<IDbContextCache, DefaultDbContextCache>();
            }

            public void Validate(IDbContextOptions options)
            {
            }

            private class CacheDbContextOptionsExtensionInfo : DbContextOptionsExtensionInfo
            {
                public CacheDbContextOptionsExtensionInfo(
                    CacheDbContextOptionsExtension extension) :
                    base(extension)
                {
                }

                public override bool IsDatabaseProvider => false;

                public override string LogFragment => string.Empty;

                public override long GetServiceProviderHashCode() => 0;

                public override void PopulateDebugInfo(IDictionary<string, string> debugInfo) { }
            }
        }

        /// <summary>
        /// Get the memory cache.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>The memory cache.</returns>
        public static IMemoryCache GetCache(this DbContext context) =>
            context.GetService<IDbContextCache>();

        /// <summary>
        /// Remove one cache entry from store.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="tag">The entry tag.</param>
        public static void RemoveCacheEntry(this DbContext context, string tag)
        {
            context.GetCache().Remove(tag);
        }

        /// <summary>
        /// Cached running some context operation.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="_">The context.</param>
        /// <param name="tag">The cache tag.</param>
        /// <param name="timeSpan">The relative expiration time span.</param>
        /// <param name="factory">The value factory.</param>
        /// <returns>The final value.</returns>
        [Obsolete("Use another CachedGet please.")]
        public static TResult CachedGet<TResult>(
            this DbContext _, string tag, TimeSpan timeSpan, Func<TResult> factory)
        {
            return _.GetCache().GetOrCreate(tag, entry =>
            {
                var result = factory();
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        /// <summary>
        /// Cached running some context operation.
        /// </summary>
        /// <typeparam name="TContext">The context type.</typeparam>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="_">The context.</param>
        /// <param name="tag">The cache tag.</param>
        /// <param name="timeSpan">The relative expiration time span.</param>
        /// <param name="factory">The value factory.</param>
        /// <returns>The final value.</returns>
        public static TResult CachedGet<TContext, TResult>(
            this TContext _, string tag, TimeSpan timeSpan, Func<TContext, TResult> factory)
            where TContext : DbContext
        {
            return _.GetCache().GetOrCreate(tag, entry =>
            {
                var result = factory(_);
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        /// <summary>
        /// Cached running some context operation.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="_">The context.</param>
        /// <param name="tag">The cache tag.</param>
        /// <param name="timeSpan">The relative expiration time span.</param>
        /// <param name="factory">The value factory.</param>
        /// <returns>The task for final value.</returns>
        [Obsolete("Use another CachedGet please.")]
        public static Task<TResult> CachedGetAsync<TResult>(
            this DbContext _, string tag, TimeSpan timeSpan, Func<Task<TResult>> factory)
        {
            return _.GetCache().GetOrCreateAsync(tag, async entry =>
            {
                var result = await factory();
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        /// <summary>
        /// Cached running some context operation.
        /// </summary>
        /// <typeparam name="TContext">The context type.</typeparam>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="_">The context.</param>
        /// <param name="tag">The cache tag.</param>
        /// <param name="timeSpan">The relative expiration time span.</param>
        /// <param name="factory">The value factory.</param>
        /// <returns>The task for final value.</returns>
        public static Task<TResult> CachedGetAsync<TContext, TResult>(
            this TContext _, string tag, TimeSpan timeSpan, Func<TContext, Task<TResult>> factory)
            where TContext : DbContext
        {
            return _.GetCache().GetOrCreateAsync(tag, async entry =>
            {
                var result = await factory(_);
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        /// <summary>
        /// Asynchronously returns the number of elements in a sequence with caching.
        /// </summary>
        /// <typeparam name="T">The query type.</typeparam>
        /// <param name="query">An <see cref="IQueryable{T}"/> that contains the elements to be counted.</param>
        /// <param name="tag">The cache tag.</param>
        /// <param name="timeSpan">The expiration time span.</param>
        /// <returns>The task for counting.</returns>
        /// <remarks>Multiple active operations on the same context instance are not supported. Use 'await' to ensure that any asynchronous operations have completed before calling another method on this context.</remarks>
        public static Task<int> CachedCountAsync<T>(
            this IQueryable<T> query, string tag, TimeSpan timeSpan)
            where T : class
        {
            return query.GetDbContext().GetCache().GetOrCreateAsync(tag, async entry =>
            {
                var result = await query.CountAsync();
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        /// <summary>
        /// Asynchronously returns the only element of a sequence, or a default value if the sequence is empty; this method throws an exception if there is more than one element in the sequence with caching.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query">An <see cref="IQueryable{T}"/> to return the single element of.</param>
        /// <param name="tag">The cache tag.</param>
        /// <param name="timeSpan">The expiration time span.</param>
        /// <returns>The task for fetching single.</returns>
        /// <remarks>Multiple active operations on the same context instance are not supported. Use 'await' to ensure that any asynchronous operations have completed before calling another method on this context.</remarks>
        public static Task<T> CachedSingleOrDefaultAsync<T>(
            this IQueryable<T> query, string tag, TimeSpan timeSpan)
            where T : class
        {
            return query.GetDbContext().GetCache().GetOrCreateAsync(tag, async entry =>
            {
                var result = await query.SingleOrDefaultAsync();
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        /// <summary>
        /// Asynchronously returns the first element of a sequence, or a default value if the sequence is empty with caching.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query">An <see cref="IQueryable{T}"/> to return the first element of.</param>
        /// <param name="tag">The cache tag.</param>
        /// <param name="timeSpan">The expiration time span.</param>
        /// <returns>The task for fetching single.</returns>
        /// <remarks>Multiple active operations on the same context instance are not supported. Use 'await' to ensure that any asynchronous operations have completed before calling another method on this context.</remarks>
        public static Task<T> CachedFirstOrDefaultAsync<T>(
            this IQueryable<T> query, string tag, TimeSpan timeSpan)
            where T : class
        {
            return query.GetDbContext().GetCache().GetOrCreateAsync(tag, async entry =>
            {
                var result = await query.FirstOrDefaultAsync();
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        /// <summary>
        /// Asynchronously creates a <see cref="List{T}"/> from an <see cref="IQueryable{T}"/> by enumerating it asynchronously with caching.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of source.</typeparam>
        /// <param name="query">An <see cref="IQueryable{T}"/> to create a list from.</param>
        /// <param name="tag">The cache tag.</param>
        /// <param name="timeSpan">The expiration time span.</param>
        /// <returns>The task for fetching single.</returns>
        /// <remarks>Multiple active operations on the same context instance are not supported. Use 'await' to ensure that any asynchronous operations have completed before calling another method on this context.</remarks>
        public static Task<List<TSource>> CachedToListAsync<TSource>(
            this IQueryable<TSource> query, string tag, TimeSpan timeSpan)
            where TSource : class
        {
            return query.GetDbContext().GetCache().GetOrCreateAsync(tag, async entry =>
            {
                var result = await query.ToListAsync();
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        /// <summary>
        /// Creates a <see cref="Dictionary{TKey, TValue}"/> from an <see cref="IQueryable{TSource}"/> by enumerating it asynchronously according to a specified key selector function with caching.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of source.</typeparam>
        /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector"/>.</typeparam>
        /// <param name="query">An <see cref="IQueryable{T}"/> to create a dictionary from.</param>
        /// <param name="keySelector">A function to extract a key from each element.</param>
        /// <param name="tag">The cache tag.</param>
        /// <param name="timeSpan">The expiration time span.</param>
        /// <returns>The task for fetching dictionary.</returns>
        /// <remarks>Multiple active operations on the same context instance are not supported. Use 'await' to ensure that any asynchronous operations have completed before calling another method on this context.</remarks>
        public static Task<Dictionary<TKey, TSource>> CachedToDictionaryAsync<TSource, TKey>(
            this IQueryable<TSource> query, Func<TSource, TKey> keySelector, string tag, TimeSpan timeSpan)
            where TSource : class
        {
            return query.GetDbContext().GetCache().GetOrCreateAsync(tag, async entry =>
            {
                var result = await query.ToDictionaryAsync(keySelector);
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        /// <summary>
        /// Creates a <see cref="Dictionary{TKey, TValue}"/> from an <see cref="IQueryable{TSource}"/> by enumerating it asynchronously according to a specified key selector function with caching.
        /// </summary>
        /// <typeparam name="TSource">The type of the elements of source.</typeparam>
        /// <typeparam name="TKey">The type of the key returned by <paramref name="keySelector"/>.</typeparam>
        /// <typeparam name="TElement">The type of the element returned by <paramref name="valueSelector"/>.</typeparam>
        /// <param name="query">An <see cref="IQueryable{T}"/> to create a dictionary from.</param>
        /// <param name="keySelector">A function to extract a key from each element.</param>
        /// <param name="valueSelector">A function to extract a value from each element.</param>
        /// <param name="tag">The cache tag.</param>
        /// <param name="timeSpan">The expiration time span.</param>
        /// <returns>The task for fetching dictionary.</returns>
        /// <remarks>Multiple active operations on the same context instance are not supported. Use 'await' to ensure that any asynchronous operations have completed before calling another method on this context.</remarks>
        public static Task<Dictionary<TKey, TElement>> CachedToDictionaryAsync<TSource, TKey, TElement>(
            this IQueryable<TSource> query, Func<TSource, TKey> keySelector, Func<TSource, TElement> valueSelector, string tag, TimeSpan timeSpan)
            where TSource : class
        {
            return query.GetDbContext().GetCache().GetOrCreateAsync(tag, async entry =>
            {
                var result = await query.ToDictionaryAsync(keySelector, valueSelector);
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }
    }
}
