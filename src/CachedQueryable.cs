using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore
{
    public static class CachedQueryable
    {
        private static readonly MemoryCache _cache
            = new MemoryCache(new MemoryCacheOptions { Clock = new Extensions.Internal.SystemClock() });

        public static IMemoryCache Cache => _cache;

        public static IMemoryCache GetCache(this DbContext _) => _cache;

        public static void RemoveCacheEntry(this DbContext _, string tag)
        {
            _cache.Remove(tag);
        }

        public static T CachedGet<T>(
            this DbContext _, string tag, TimeSpan timeSpan, Func<T> factory)
        {
            return _cache.GetOrCreate(tag, entry =>
            {
                var result = factory();
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        public static Task<T> CachedGetAsync<T>(
            this DbContext _, string tag, TimeSpan timeSpan, Func<Task<T>> factory)
        {
            return _cache.GetOrCreateAsync(tag, async entry =>
            {
                var result = await factory();
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        public static Task<int> CachedCountAsync<T>(
            this IQueryable<T> query, string tag, TimeSpan timeSpan)
        {
            return _cache.GetOrCreateAsync(tag, async entry =>
            {
                var result = await query.CountAsync();
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        public static Task<T> CachedSingleOrDefaultAsync<T>(
            this IQueryable<T> query, string tag, TimeSpan timeSpan)
        {
            return _cache.GetOrCreateAsync(tag, async entry =>
            {
                var result = await query.SingleOrDefaultAsync();
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        public static Task<List<T>> CachedToListAsync<T>(
            this IQueryable<T> query, string tag, TimeSpan timeSpan)
        {
            return _cache.GetOrCreateAsync(tag, async entry =>
            {
                var result = await query.ToListAsync();
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        public static Task<Dictionary<T2, T>> CachedToDictionaryAsync<T, T2>(
            this IQueryable<T> query,
            Func<T, T2> keySelector,
            string tag, TimeSpan timeSpan)
        {
            return _cache.GetOrCreateAsync(tag, async entry =>
            {
                var result = await query.ToDictionaryAsync(keySelector);
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }

        public static Task<Dictionary<T2, T3>> CachedToDictionaryAsync<T, T2, T3>(
            this IQueryable<T> query,
            Func<T, T2> keySelector,
            Func<T, T3> valueSelector,
            string tag, TimeSpan timeSpan)
        {
            return _cache.GetOrCreateAsync(tag, async entry =>
            {
                var result = await query.ToDictionaryAsync(keySelector, valueSelector);
                entry.AbsoluteExpirationRelativeToNow = timeSpan;
                return result;
            });
        }
    }
}
