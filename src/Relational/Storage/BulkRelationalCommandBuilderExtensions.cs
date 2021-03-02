using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    }
}
