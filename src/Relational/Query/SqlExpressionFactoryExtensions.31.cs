using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Query
{
    public static partial class BulkSqlExpressionFactoryExtensions
    {
        public static IRelationalCommand RentAndPopulateRelationalCommand(
            this RelationalCommandCache relationalCommandCache,
            QueryContext queryContext)
            => relationalCommandCache.GetRelationalCommand(queryContext.ParameterValues);

        public static void ReplaceProjection(
            this SelectExpression selectExpression,
            IReadOnlyDictionary<ProjectionMember, Expression> projectionMapping)
            => selectExpression.ReplaceProjectionMapping((IDictionary<ProjectionMember, Expression>)projectionMapping);

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

        public static readonly Func<ParameterExpression, RelationalTypeMapping, SqlParameterExpression> SqlParameterExpressionConstructor
            = typeof(SqlParameterExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<ParameterExpression, RelationalTypeMapping, SqlParameterExpression>;

        public static readonly Func<SqlExpression, string, ProjectionExpression> ProjectionExpressionConstructor
            = (SqlExpression expression, string alias) => new(expression, alias);

        public static readonly Func<string, string, string, TableExpression> TableExpressionConstructor
            = typeof(TableExpression)
                .GetConstructors(GeneralBindingFlags)[0]
                .CreateFactory()
              as Func<string, string, string, TableExpression>;

        public static readonly Func<SelectExpression, List<SelectExpression>> AccessPendingCollections
            = ExpressionBuilder
                .Begin<SelectExpression>()
                .AccessField("_pendingCollections")
                .Compile<Func<SelectExpression, List<SelectExpression>>>();

        public static readonly Func<string, TableExpressionBase, Type, RelationalTypeMapping, bool, ColumnExpression> ColumnExpressionConstructor
            = typeof(ColumnExpression)
                .GetConstructors(GeneralBindingFlags)
                .Single(c => c.GetParameters().Length == 5)
                .CreateFactory()
              as Func<string, TableExpressionBase, Type, RelationalTypeMapping, bool, ColumnExpression>;

        public static readonly Func<string, List<ProjectionExpression>, List<TableExpressionBase>, List<SqlExpression>, List<OrderingExpression>, SelectExpression> SelectExpressionConstructor
            = typeof(SelectExpression)
                .GetConstructors(GeneralBindingFlags)
                .Single(c => c.GetParameters().Length == 5)
                .CreateFactory()
              as Func<string, List<ProjectionExpression>, List<TableExpressionBase>, List<SqlExpression>, List<OrderingExpression>, SelectExpression>;

        public static readonly Func<SelectExpression, IDictionary<EntityProjectionExpression, IDictionary<IProperty, int>>> AccessEntityProjectionCache
            = ExpressionBuilder
                .Begin<SelectExpression>()
                .AccessField("_entityProjectionCache")
                .Compile<Func<SelectExpression, IDictionary<EntityProjectionExpression, IDictionary<IProperty, int>>>>();

        public static readonly Func<SelectExpression, IDictionary<ProjectionMember, Expression>> AccessProjectionMapping
            = ExpressionBuilder
                .Begin<SelectExpression>()
                .AccessField("_projectionMapping")
                .Compile<Func<SelectExpression, IDictionary<ProjectionMember, Expression>>>();

        public static readonly Action<SelectExpression, SqlExpression> ApplyPredicate
            = new Func<Expression<Action<SelectExpression, SqlExpression>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(SelectExpression), "select");
                var para2 = Expression.Parameter(typeof(SqlExpression), "sql");
                var body = Expression.Assign(Expression.Property(para1, "Predicate"), para2);
                return Expression.Lambda<Action<SelectExpression, SqlExpression>>(body, para1, para2);
            })
            .Invoke().Compile();

        public static readonly Action<SelectExpression, SqlExpression> ApplyHaving
            = new Func<Expression<Action<SelectExpression, SqlExpression>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(SelectExpression), "select");
                var para2 = Expression.Parameter(typeof(SqlExpression), "sql");
                var body = Expression.Assign(Expression.Property(para1, "Having"), para2);
                return Expression.Lambda<Action<SelectExpression, SqlExpression>>(body, para1, para2);
            })
            .Invoke().Compile();

        public static readonly Action<TableExpressionBase, string> ApplyAlias
            = new Func<Expression<Action<TableExpressionBase, string>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(TableExpressionBase), "table");
                var para2 = Expression.Parameter(typeof(string), "alias");
                var body = Expression.Assign(Expression.Property(para1, "Alias"), para2);
                return Expression.Lambda<Action<TableExpressionBase, string>>(body, para1, para2);
            })
            .Invoke().Compile();

        public static readonly Action<SelectExpression, SelectExpression> ApplyCopyIdentifiersFrom
            = new Func<Expression<Action<SelectExpression, SelectExpression>>>(delegate
            {
                var para1 = Expression.Parameter(typeof(SelectExpression), "left");
                var para2 = Expression.Parameter(typeof(SelectExpression), "right");
                var _identifier = typeof(SelectExpression).GetField("_identifier", GeneralBindingFlags);
                var left = Expression.Field(para1, _identifier);
                var right = Expression.Field(para2, _identifier);
                var clearup = Expression.Call(left, left.Type.GetMethod("Clear"));
                var addrange = Expression.Call(left, left.Type.GetMethod("AddRange"), right);
                var body = Expression.Block(clearup, addrange);
                return Expression.Lambda<Action<SelectExpression, SelectExpression>>(body, para1, para2);
            })
            .Invoke().Compile();
    }
}
