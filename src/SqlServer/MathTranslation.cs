using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class MathTranslation : IMethodCallTranslator, IMethodCallTranslatorPlugin
    {
        private MathTranslation[] Translators { get; }

        IEnumerable<IMethodCallTranslator> IMethodCallTranslatorPlugin.Translators => Translators;

        ISqlExpressionFactory Sql { get; }

        public MathTranslation(ISqlExpressionFactory sqlExpressionFactory)
        {
            Sql = sqlExpressionFactory;
            Translators = new[] { this };
        }

        public SqlExpression Translate(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments)
        {
            if (arguments.Count != 2 || method.DeclaringType != typeof(Math)) return null;

            return method.Name switch
            {
                nameof(Math.Min) => IIF(Sql.LessThan(arguments[0], arguments[1]), arguments[0], arguments[1]),
                nameof(Math.Max) => IIF(Sql.GreaterThan(arguments[0], arguments[1]), arguments[0], arguments[1]),
                _ => null,
            };

            CaseExpression IIF(SqlExpression test, SqlExpression whenTrue, SqlExpression whenFalse) =>
                Sql.Case(new[] { new CaseWhenClause(test, whenTrue) }, whenFalse);
        }

        public SqlExpression Translate(SqlExpression instance, MethodInfo method, IReadOnlyList<SqlExpression> arguments, IDiagnosticsLogger<DbLoggerCategory.Query> logger)
            => Translate(instance, method, arguments);
    }
}
