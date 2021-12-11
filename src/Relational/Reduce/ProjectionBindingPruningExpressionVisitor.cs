using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    public class ProjectionBindingPruningExpressionVisitor : ExpressionVisitor
    {
        private int[] _remappedIndex;
        private readonly HashSet<IDictionary<IProperty, int>> _processed = new HashSet<IDictionary<IProperty, int>>();

        protected override Expression VisitExtension(Expression node)
        {
            return node switch
            {
                ShapedQueryExpression shapedQuery => VisitShapedQuery(shapedQuery),
                ProjectionBindingExpression projectionBinding => VisitProjectionBinding(projectionBinding),
                _ => base.VisitExtension(node),
            };
        }

        protected virtual Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
        {
            if (_remappedIndex != null)
            {
                throw new InvalidOperationException("VisitShapedQuery can be visited only once.");
            }

            var selectExpression = (SelectExpression)shapedQueryExpression.QueryExpression;
            var reprojection = new Dictionary<ProjectionExpression, int>(ProjectionExpressionComparer.Instance);
            _remappedIndex = new int[selectExpression.Projection.Count];
            var reverseMapping = new List<List<ProjectionExpression>>();

            for (int i = 0; i < selectExpression.Projection.Count; i++)
            {
                if (!reprojection.TryGetValue(selectExpression.Projection[i], out var remapped))
                {
                    reprojection.Add(selectExpression.Projection[i], remapped = reprojection.Count);
                    reverseMapping.Add(new List<ProjectionExpression>());
                }

                _remappedIndex[i] = remapped;
                reverseMapping[remapped].Add(selectExpression.Projection[i]);
            }

            var projectionMapping = selectExpression.GetProjectionMapping();
            foreach (var member in projectionMapping.Keys.ToList())
            {
                var value = projectionMapping[member];
                if (value is ConstantExpression constantExpression && constantExpression.Type == typeof(int))
                {
                    projectionMapping[member] = Expression.Constant(_remappedIndex[(int)constantExpression.Value]);
                }
            }

            var upcastProjection = (List<ProjectionExpression>)selectExpression.Projection;
            upcastProjection.Clear();
            for (int i = 0; i < reverseMapping.Count; i++)
            {
                if (reverseMapping[i].Count == 1)
                {
                    upcastProjection.Add(reverseMapping[i][0]);
                    continue;
                }

                if (reverseMapping[i][0].Expression is not ColumnExpression)
                {
                    upcastProjection.Add(reverseMapping[i][0]);
                    continue;
                }

                var tryGetNotNullable = reverseMapping[i]
                    .Where(a => !((ColumnExpression)a.Expression).IsNullable)
                    .FirstOrDefault();

                upcastProjection.Add(tryGetNotNullable ?? reverseMapping[i][0]);
            }

            var newShaperExpression = Visit(shapedQueryExpression.ShaperExpression);
            return shapedQueryExpression.Update(selectExpression, newShaperExpression);
        }

        protected virtual Expression VisitProjectionBinding(ProjectionBindingExpression projectionBindingExpression)
        {
            if (projectionBindingExpression.Index.HasValue)
            {
                return new ProjectionBindingExpression(
                    projectionBindingExpression.QueryExpression,
                    _remappedIndex[projectionBindingExpression.Index.Value],
                    projectionBindingExpression.Type);
            }
#if EFCORE31 || EFCORE50
            else if (projectionBindingExpression.IndexMap != null)
            {
                var links = projectionBindingExpression.IndexMap;
                if (_processed.Add(links))
                {
                    var properties = links.ToList();
                    foreach (var link in properties)
                    {
                        var rawIndex = link.Value;
                        var property = link.Key;
                        links[property] = _remappedIndex[rawIndex];
                    }
                }

                return projectionBindingExpression;
            }
#endif
            else
            {
                return projectionBindingExpression;
            }
        }

        protected class ProjectionExpressionComparer : IEqualityComparer<ProjectionExpression>
        {
            public static ProjectionExpressionComparer Instance { get; } = new ProjectionExpressionComparer();

            public bool Equals(ProjectionExpression x, ProjectionExpression y)
            {
                if (x.Expression is ColumnExpression cx && y.Expression is ColumnExpression cy)
                {
                    return cx.Type == cy.Type && cx.TypeMapping == cy.TypeMapping
                        && cx.Name == cy.Name && ReferenceEquals(cx.Table, cy.Table);
                }
                else
                {
                    return x.Expression.Equals(y.Expression);
                }
            }

            public int GetHashCode(ProjectionExpression obj)
            {
                return obj.Expression is ColumnExpression c
                    ? HashCode.Combine(c.Type, c.TypeMapping, c.Table, c.Name)
                    : HashCode.Combine(obj.Alias, obj.Expression);
            }
        }
    }
}
