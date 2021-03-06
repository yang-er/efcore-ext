using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Npgsql.EntityFrameworkCore.PostgreSQL.Query.Internal;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Npgsql.EntityFrameworkCore.PostgreSQL.Query
{
    public class DateTimeOffsetTranslationPlugin : IMemberTranslator, IMethodCallTranslator, IMemberTranslatorPlugin, IMethodCallTranslatorPlugin
    {
        private DateTimeOffsetTranslationPlugin[] Translators { get; }

        IEnumerable<IMemberTranslator> IMemberTranslatorPlugin.Translators => Translators;

        IEnumerable<IMethodCallTranslator> IMethodCallTranslatorPlugin.Translators => Translators;

        NpgsqlSqlExpressionFactory Sql { get; }

        IReadOnlyList<IReadOnlyList<bool>> TrueArrays { get; }

        public DateTimeOffsetTranslationPlugin(ISqlExpressionFactory sqlExpressionFactory)
        {
            Sql = (NpgsqlSqlExpressionFactory)sqlExpressionFactory;
            Translators = new[] { this };

            var trueArrays = new IReadOnlyList<bool>[5];
            TrueArrays = trueArrays;
            for (int i = 0; i < 5; i++)
            {
                var it = new bool[i];
                for (int j = 0; j < i; j++) it[j] = true;
                trueArrays[i] = it;
            }
        }

        private SqlExpression GetDatePartExpression(SqlExpression instance, string partName, bool floor = false)
        {
            var result = Sql.Function("DATE_PART", new[] { Sql.Constant(partName), instance }, true, TrueArrays[2], typeof(double));
            if (floor) result = Sql.Function("FLOOR", new[] { result }, true, TrueArrays[1], typeof(double));
            return Sql.Convert(result, typeof(int));
        }

        public SqlExpression Translate(SqlExpression instance, MemberInfo member, Type returnType)
        {
            var type = member.DeclaringType;
            if (type != typeof(DateTimeOffset)) return null;

            return member.Name switch
            {
                nameof(DateTimeOffset.Now) => Now(),
                nameof(DateTimeOffset.UtcNow) => Sql.AtTimeZone(Now(), Sql.Constant("UTC"), returnType),

                nameof(DateTimeOffset.Year) => GetDatePartExpression(instance, "year"),
                nameof(DateTimeOffset.Month) => GetDatePartExpression(instance, "month"),
                nameof(DateTimeOffset.DayOfYear) => GetDatePartExpression(instance, "doy"),
                nameof(DateTimeOffset.Day) => GetDatePartExpression(instance, "day"),
                nameof(DateTimeOffset.Hour) => GetDatePartExpression(instance, "hour"),
                nameof(DateTimeOffset.Minute) => GetDatePartExpression(instance, "minute"),
                nameof(DateTimeOffset.Second) => GetDatePartExpression(instance, "second"),

                nameof(DateTimeOffset.Millisecond) => null, // Too annoying

                // .NET's DayOfWeek is an enum, but its int values happen to correspond to PostgreSQL
                nameof(DateTimeOffset.DayOfWeek) => GetDatePartExpression(instance, "dow", true),

                nameof(DateTimeOffset.Date) => Sql.Function("DATE_TRUNC", new[] { Sql.Constant("day"), instance }, true, TrueArrays[2], returnType),

                // TODO: Technically possible simply via casting to PG time, should be better in EF Core 3.0
                // but ExplicitCastExpression only allows casting to PG types that
                // are default-mapped from CLR types (timespan maps to interval,
                // which timestamp cannot be cast into)
                nameof(DateTime.TimeOfDay) => null,

                // TODO: Should be possible
                nameof(DateTime.Ticks) => null,

                _ => null
            };

            SqlFunctionExpression Now() => Sql.Function("NOW", Array.Empty<SqlExpression>(), false, TrueArrays[0], returnType);
        }

        public SqlExpression Translate(SqlExpression instance, MemberInfo member, Type returnType, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
            => Translate(instance, member, returnType);

        public SqlExpression Translate(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments)
            => ((method.DeclaringType == typeof(DateTimeOffset))
                && (method.Name == nameof(DateTimeOffset.ToUniversalTime)))
                ? Sql.AtTimeZone(instance, Sql.Constant("UTC"), method.ReturnType)
                : null;

        public SqlExpression Translate(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
            => Translate(instance, method, arguments);
    }
}
