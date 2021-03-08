using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace Microsoft.EntityFrameworkCore.Storage
{
    public static class BulkRelationalCommandBuilderExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static IRelationalCommandBuilder GenerateList<T>(
            this IRelationalCommandBuilder sql,
            IReadOnlyList<T> items,
            Action<T> generationAction,
            Action<IRelationalCommandBuilder> joinAction = null)
        {
            joinAction ??= (isb => isb.Append(", "));

            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0) joinAction(sql);
                generationAction(items[i]);
            }

            return sql;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static ExpressionPrinter VisitCollection<T>(
            this ExpressionPrinter expressionPrinter,
            IReadOnlyList<T> items,
            Action<ExpressionPrinter, T> generateExpression,
            Action<ExpressionPrinter> joinAction = null)
        {
            joinAction ??= (isb => isb.Append(", "));

            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0) joinAction(expressionPrinter);
                generateExpression(expressionPrinter, items[i]);
            }

            return expressionPrinter;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static IRelationalCommandBuilder Then(this IRelationalCommandBuilder sql, Action then)
        {
            then.Invoke();
            return sql;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [DebuggerStepThrough]
        public static IReadOnlyList<TExpression> VisitCollection<TExpression>(
            this ExpressionVisitor visitor,
            IReadOnlyList<TExpression> items,
            string callerName)
            where TExpression : Expression
        {
            if (items == null) return null;

            bool changed = false;
            var newItems = items.ToList();
            for (int i = 0; i < newItems.Count; i++)
            {
                newItems[i] = visitor.VisitAndConvert(newItems[i], callerName);
                changed = changed || newItems[i] != items[i];
            }

            return changed ? newItems : items;
        }
    }
}
