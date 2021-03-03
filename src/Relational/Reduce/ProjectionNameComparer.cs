using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.Query
{
    internal class ProjectionNameComparer : IEqualityComparer<ProjectionExpression>
    {
        public static IEqualityComparer<ProjectionExpression> Default = new ProjectionNameComparer();

        public bool Equals(ProjectionExpression x, ProjectionExpression y)
        {
            return x.Alias == y.Alias && x.Expression.Equals(y.Expression);
        }

        public int GetHashCode(ProjectionExpression obj)
        {
            return HashCode.Combine(obj.Alias, obj.Expression);
        }
    }
}
