using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class EnhancedQuerySqlGeneratorFactory : IQuerySqlGeneratorFactory
    {
        internal static readonly Func<string, string, string, TableExpression> CreateTable;
        internal static readonly Func<ParameterExpression, RelationalTypeMapping, SqlParameterExpression> Parameter;
        internal static readonly Func<string, TableExpressionBase, Type, RelationalTypeMapping, bool, ColumnExpression> Column;
        internal static readonly Func<TypeMappedRelationalParameter, RelationalTypeMapping> GetRTM;
        internal static readonly Func<TypeMappedRelationalParameter, bool?> GetIN;

        static EnhancedQuerySqlGeneratorFactory()
        {
            var bindingFlag = BindingFlags.NonPublic | BindingFlags.Instance;
            CreateTable = typeof(TableExpression).GetConstructors(bindingFlag)[0]
                .CreateFactory() as Func<string, string, string, TableExpression>;
            Parameter = typeof(SqlParameterExpression).GetConstructors(bindingFlag)[0]
                .CreateFactory() as Func<ParameterExpression, RelationalTypeMapping, SqlParameterExpression>;
            Column = typeof(ColumnExpression).GetConstructors(bindingFlag).Single(c => c.GetParameters().Length == 5)
                .CreateFactory() as Func<string, TableExpressionBase, Type, RelationalTypeMapping, bool, ColumnExpression>;
            var par = Expression.Parameter(typeof(TypeMappedRelationalParameter), "p");
            GetRTM = Expression.Lambda<Func<TypeMappedRelationalParameter, RelationalTypeMapping>>(
                Expression.Property(par, typeof(TypeMappedRelationalParameter)
                .GetProperty(nameof(RelationalTypeMapping), bindingFlag)), par).Compile();
            GetIN = Expression.Lambda<Func<TypeMappedRelationalParameter, bool?>>(
                Expression.Property(par, typeof(TypeMappedRelationalParameter)
                .GetProperty("IsNullable", bindingFlag)), par).Compile();
        }

        private readonly QuerySqlGeneratorDependencies _dependencies;

        public EnhancedQuerySqlGeneratorFactory(QuerySqlGeneratorDependencies dependencies)
        {
            _dependencies = dependencies;
        }

        internal static Expression<Action<QuerySqlGenerator>> CreateOne()
        {
            var para = Expression.Parameter(typeof(QuerySqlGenerator), "g");
            var builder = ReflectionExtensions
                .PrivateField<QuerySqlGenerator, IRelationalCommandBuilder>("_relationalCommandBuilder", para).Body;
            var builderFactory = ReflectionExtensions
                .PrivateField<QuerySqlGenerator, IRelationalCommandBuilderFactory>("_relationalCommandBuilderFactory", para).Body;
            var method = typeof(IRelationalCommandBuilderFactory).GetMethod("Create");
            var right = Expression.Call(builderFactory, method);
            var body = Expression.Assign(builder, right);
            return Expression.Lambda<Action<QuerySqlGenerator>>(body, para);
        }

        public virtual QuerySqlGenerator Create()
            => new EnhancedQuerySqlGenerator(_dependencies);
    }
}
