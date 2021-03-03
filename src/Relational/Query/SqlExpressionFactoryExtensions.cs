using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Query
{
    public static class BulkSqlExpressionFactoryExtensions
    {
        public static SqlParameterExpression Parameter(
            this ISqlExpressionFactory factory,
            ParameterExpression parameterExpression,
            RelationalTypeMapping typeMapping)
        {
            Check.NotNull(factory, nameof(factory));
            return SqlParameterExpressionConstructor(parameterExpression, typeMapping);
        }

        public static ProjectionExpression Projection(
            this ISqlExpressionFactory factory,
            SqlExpression expression,
            string alias)
        {
            Check.NotNull(factory, nameof(factory));
            return ProjectionExpressionConstructor(expression, alias);
        }

        public static ColumnExpression Column(
            this ISqlExpressionFactory factory,
            string name,
            TableExpressionBase table,
            Type type,
            RelationalTypeMapping typeMapping,
            bool nullable)
        {
            Check.NotNull(factory, nameof(factory));
            return ColumnExpressionConstructor(name, table, type, typeMapping, nullable);
        }

        public static SelectExpression Select(
            this ISqlExpressionFactory factory,
            string alias,
            List<ProjectionExpression> projections,
            List<TableExpressionBase> tables,
            List<SqlExpression> groupBy,
            List<OrderingExpression> orderings)
        {
            Check.NotNull(factory, nameof(factory));
            return SelectExpressionConstructor(alias, projections, tables, groupBy, orderings);
        }

        public static IDictionary<ProjectionMember, Expression> GetProjectionMapping(
            this SelectExpression selectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            return AccessProjectionMapping(selectExpression);
        }

        public static void SetPredicate(
            this SelectExpression selectExpression,
            SqlExpression predicate)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            ApplyPredicate(selectExpression, predicate);
        }

        public static void CopyIdentifiersFrom(
            this SelectExpression selectExpression,
            SelectExpression otherSelectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));
            Check.NotNull(otherSelectExpression, nameof(otherSelectExpression));

            ApplyCopyIdentifiersFrom(selectExpression, otherSelectExpression);
        }

        public static void SetAlias(
            this TableExpressionBase tableExpressionBase,
            string alias)
        {
            Check.NotNull(tableExpressionBase, nameof(tableExpressionBase));
            ApplyAlias(tableExpressionBase, alias);
        }

#if EFCORE50

        public static TableExpression Table(
            this ISqlExpressionFactory factory,
            ITableBase table)
        {
            Check.NotNull(factory, nameof(factory));
            return TableExpressionConstructor(table);
        }

        internal static Expression AddCrossJoinForMerge(
            this SelectExpression outerSelectExpression,
            ShapedQueryExpression innerSource,
            Expression outerShaper)
        {
            return outerSelectExpression.AddCrossJoin(innerSource, outerShaper);
        }

#elif EFCORE31

        public static TableExpression Table(
            this ISqlExpressionFactory factory,
            string name,
            string schema,
            string alias)
        {
            Check.NotNull(factory, nameof(factory));
            return TableExpressionConstructor(name, schema, alias);
        }

        public static SqlFunctionExpression Function(
            this ISqlExpressionFactory factory,
            string name,
            IEnumerable<SqlExpression> arguments,
            bool nullable,
            IEnumerable<bool> argumentsPropagateNullability,
            Type returnType,
            RelationalTypeMapping typeMapping = null)
        {
            return factory.Function(name, arguments, returnType, typeMapping);
        }

        public static ShapedQueryExpression Update(
            this ShapedQueryExpression shaped,
            Expression queryExpression,
            Expression shaperExpression)
        {
            Check.NotNull(queryExpression, nameof(queryExpression));
            Check.NotNull(shaperExpression, nameof(shaperExpression));

            shaped.QueryExpression = queryExpression;
            shaped.ShaperExpression = shaperExpression;
            return shaped;
        }

        internal static Expression AddCrossJoinForMerge(
            this SelectExpression outerSelectExpression,
            ShapedQueryExpression innerSource,
            Expression outerShaper)
        {
            Check.NotNull(outerSelectExpression, nameof(outerSelectExpression));
            Check.NotNull(innerSource, nameof(innerSource));
            Check.NotNull(outerShaper, nameof(outerShaper));

            var transparentIdentifierType = TransparentIdentifierFactory.Create(
                outerShaper.Type,
                innerSource.ShaperExpression.Type);
            var outerMemberInfo = transparentIdentifierType.GetTypeInfo().GetDeclaredField("Outer");
            var innerMemberInfo = transparentIdentifierType.GetTypeInfo().GetDeclaredField("Inner");

            var innerSelectExpression = (SelectExpression)innerSource.QueryExpression;
            var innerShaper = innerSource.ShaperExpression;
            outerSelectExpression.AddCrossJoin(innerSelectExpression, transparentIdentifierType);

            if (outerSelectExpression.Projection.Count > 0
                || innerSelectExpression.Projection.Count > 0)
            {
                throw new NotImplementedException("Investigate why outerClientEval or innerClientEval.");
            }

            var remapper = new ProjectionBindingExpressionRemappingExpressionVisitor(outerSelectExpression);
            outerShaper = remapper.RemapProjectionMember(outerShaper, outerMemberInfo);
            innerShaper = remapper.RemapProjectionMember(innerShaper, innerMemberInfo, AccessPendingCollections(outerSelectExpression).Count);

            return Expression.New(
                transparentIdentifierType.GetConstructors()[0],
                new[] { outerShaper, innerShaper },
                new[] { outerMemberInfo, innerMemberInfo });
        }

        private sealed class ProjectionBindingExpressionRemappingExpressionVisitor : ExpressionVisitor
        {
            private readonly Expression _queryExpression;
            private int _pendingCollectionOffset;
            private MemberInfo _prependMember;

            public ProjectionBindingExpressionRemappingExpressionVisitor(Expression queryExpression)
            {
                _queryExpression = queryExpression;
            }

            public Expression RemapProjectionMember(
                Expression expression,
                MemberInfo prepend,
                int pendingCollectionOffset = 0)
            {
                _pendingCollectionOffset = pendingCollectionOffset;
                _prependMember = prepend;
                return Visit(expression);
            }

            protected override Expression VisitExtension(Expression extensionExpression)
            {
                return extensionExpression switch
                {
                    ProjectionBindingExpression projectionBindingExpression => Remap(projectionBindingExpression),
                    CollectionShaperExpression collectionShaperExpression => Remap(collectionShaperExpression),
                    _ => base.VisitExtension(extensionExpression)
                };
            }

            private CollectionShaperExpression Remap(CollectionShaperExpression collectionShaperExpression)
                => new CollectionShaperExpression(
                    new ProjectionBindingExpression(
                        _queryExpression,
                        ((ProjectionBindingExpression)collectionShaperExpression.Projection).Index.Value + _pendingCollectionOffset,
                        typeof(object)),
                    collectionShaperExpression.InnerShaper,
                    collectionShaperExpression.Navigation,
                    collectionShaperExpression.ElementType);

            private ProjectionBindingExpression Remap(ProjectionBindingExpression projectionBindingExpression)
            {
                var currentProjectionMember = projectionBindingExpression.ProjectionMember;
                var newBinding = _prependMember == null ? currentProjectionMember : currentProjectionMember.Prepend(_prependMember);
                return new ProjectionBindingExpression(_queryExpression, newBinding, projectionBindingExpression.Type);
            }
        }

#endif

        #region Reflection Getting

        private const BindingFlags GeneralBindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        public static readonly Func<ParameterExpression, RelationalTypeMapping, SqlParameterExpression> SqlParameterExpressionConstructor
            = typeof(SqlParameterExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<ParameterExpression, RelationalTypeMapping, SqlParameterExpression>;

        public static readonly Func<SqlExpression, string, ProjectionExpression> ProjectionExpressionConstructor
            = typeof(ProjectionExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<SqlExpression, string, ProjectionExpression>;

        public static readonly Func<string, TableExpressionBase, Type, RelationalTypeMapping, bool, ColumnExpression> ColumnExpressionConstructor
            = typeof(ColumnExpression)
                .GetConstructors(GeneralBindingFlags)
                .Single(c => c.GetParameters().Length == 5)
                .CreateFactory()
              as Func<string, TableExpressionBase, Type, RelationalTypeMapping, bool, ColumnExpression>;

#if EFCORE50

        public static readonly Func<ITableBase, TableExpression> TableExpressionConstructor
            = typeof(TableExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<ITableBase, TableExpression>;

#elif EFCORE31

        public static readonly Func<string, string, string, TableExpression> TableExpressionConstructor
            = typeof(TableExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<string, string, string, TableExpression>;

        public static readonly Func<SelectExpression, List<SelectExpression>> AccessPendingCollections
            = Internals.CreateLambda<SelectExpression, List<SelectExpression>>(
                param => param.AccessField("_pendingCollections"))
            .Compile();

#endif

        public static readonly Func<string, List<ProjectionExpression>, List<TableExpressionBase>, List<SqlExpression>, List<OrderingExpression>, SelectExpression> SelectExpressionConstructor
            = typeof(SelectExpression)
                .GetConstructors(GeneralBindingFlags)
                .Single(c => c.GetParameters().Length == 5)
                .CreateFactory()
              as Func<string, List<ProjectionExpression>, List<TableExpressionBase>, List<SqlExpression>, List<OrderingExpression>, SelectExpression>;

        public static readonly Func<SelectExpression, IDictionary<ProjectionMember, Expression>> AccessProjectionMapping
            = Internals.CreateLambda<SelectExpression, IDictionary<ProjectionMember, Expression>>(
                param => param.AccessField("_projectionMapping"))
            .Compile();

        public static readonly Action<SelectExpression, SqlExpression> ApplyPredicate
            = new Func<Expression<Action<SelectExpression, SqlExpression>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(SelectExpression), "select");
                var para2 = Expression.Parameter(typeof(SqlExpression), "sql");
                var body = Expression.Assign(para1.AccessProperty("Predicate"), para2);
                return Expression.Lambda<Action<SelectExpression, SqlExpression>>(body, para1, para2);
            })
            .Invoke().Compile();

        public static readonly Action<TableExpressionBase, string> ApplyAlias
            = new Func<Expression<Action<TableExpressionBase, string>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(TableExpressionBase), "table");
                var para2 = Expression.Parameter(typeof(string), "alias");
                var body = Expression.Assign(para1.AccessProperty("Alias"), para2);
                return Expression.Lambda<Action<TableExpressionBase, string>>(body, para1, para2);
            })
            .Invoke().Compile();

        public static readonly Action<SelectExpression, SelectExpression> ApplyCopyIdentifiersFrom
            = new Func<Expression<Action<SelectExpression, SelectExpression>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(SelectExpression), "left");
                var para2 = Expression.Parameter(typeof(SelectExpression), "right");
                var left = para1.AccessField("_identifier");
                var right = para2.AccessField("_identifier");
                var clearup = Expression.Call(left, left.Type.GetMethod("Clear"));
                var addrange = Expression.Call(left, left.Type.GetMethod("AddRange"), right);
                var body = Expression.Block(clearup, addrange);
                return Expression.Lambda<Action<SelectExpression, SelectExpression>>(body, para1, para2);
            })
            .Invoke().Compile();

        #endregion
    }
}
