using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Utilities
{
    internal static class MergeJoinExtensions
    {
        public static MethodInfo Enumerable { get; }
            = new Func<IEnumerable<object>,
                       IEnumerable<object>,
                       Func<object, object>,
                       Func<object, object>,
                       Func<object, object, object>,
                       object,
                       object,
                       IEnumerable<object>>(MergeJoin)
            .GetMethodInfo()
            .GetGenericMethodDefinition();

        public static MethodInfo Queryable { get; }
            = new Func<IQueryable<object>,
                       IQueryable<object>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object, object>>,
                       IQueryable<object>>(MergeJoin)
            .GetMethodInfo()
            .GetGenericMethodDefinition();

        public static MethodInfo Queryable2 { get; }
            = new Func<IQueryable<object>,
                       IQueryable<object>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object, object>>,
                       Expression<Func<object, object>>,
                       IQueryable<object>>(MergeJoin)
            .GetMethodInfo()
            .GetGenericMethodDefinition();

        public static IQueryable<TResult> MergeJoin<TOuter, TInner, TKey, TResult>(
            this IQueryable<TOuter> outer,
            IQueryable<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TResult>> resultSelector)
            => throw new InvalidOperationException("Only created from QueryCompiler.");

        public static IQueryable<TResult> MergeJoin<TOuter, TInner, TKey, TResult, TFinalResult>(
            this IQueryable<TOuter> outer,
            IQueryable<TInner> inner,
            Expression<Func<TOuter, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TOuter, TInner, TResult>> resultSelector,
            Expression<Func<TResult, TFinalResult>> finalizeSelector)
            => throw new InvalidOperationException("Only created from QueryCompiler.");

        public static IEnumerable<TResult> MergeJoin<TOuter, TInner, TKey, TResult>(
            this IEnumerable<TOuter> outer,
            IEnumerable<TInner> inner,
            Func<TOuter, TKey> outerKeySelector,
            Func<TInner, TKey> innerKeySelector,
            Func<TOuter, TInner, TResult> resultSelector,
            TOuter outerDefaultIfEmpty,
            TInner innerDefaultIfEmpty)
        {
            var mergeResult = new Dictionary<TKey, MergeResult<TOuter, TInner>>();

            foreach (var item in outer)
            {
                TKey key = outerKeySelector(item);
                if (!mergeResult.TryGetValue(key, out MergeResult<TOuter, TInner> merger))
                {
                    merger = new MergeResult<TOuter, TInner>();
                    mergeResult.Add(key, merger);
                }

                merger.Outer.Add(item);
            }

            foreach (var item in inner)
            {
                TKey key = innerKeySelector(item);
                if (!mergeResult.TryGetValue(key, out MergeResult<TOuter, TInner> merger))
                {
                    merger = new MergeResult<TOuter, TInner>();
                    mergeResult.Add(key, merger);
                }

                merger.Inner.Add(item);

                if (merger.Inner.Count > 1 && merger.Outer.Count > 1)
                {
                    throw new InvalidOperationException(
                        "The outer items for this key matches multiple inner items. " +
                        "This is not acceptable for merge join.");
                }
            }

            foreach (var (_, value) in mergeResult)
            {
                if (value.Inner.Count == 0)
                {
                    value.Inner.Add(innerDefaultIfEmpty);
                }
                else if (value.Outer.Count == 0)
                {
                    value.Outer.Add(outerDefaultIfEmpty);
                }
            }

            return MergeResultIterator(mergeResult.Values, resultSelector);
        }

        private class MergeResult<TOuter, TInner>
        {
            public List<TOuter> Outer { get; }

            public List<TInner> Inner { get; }

            public MergeResult()
            {
                Outer = new List<TOuter>();
                Inner = new List<TInner>();
            }
        }

        private static IEnumerable<TResult> MergeResultIterator<TOuter, TInner, TResult>(
            IEnumerable<MergeResult<TOuter, TInner>> mergedItems,
            Func<TOuter, TInner, TResult> resultSelector)
        {
            foreach (var merge in mergedItems)
            {
                foreach (var outer in merge.Outer)
                {
                    foreach (var inner in merge.Inner)
                    {
                        yield return resultSelector(outer, inner);
                    }
                }
            }
        }
    }
}
