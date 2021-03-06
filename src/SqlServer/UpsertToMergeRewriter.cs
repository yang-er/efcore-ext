using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.SqlServer.Query
{
    public class UpsertToMergeRewriter : ExpressionVisitor
    {
        private readonly Dictionary<ProjectionMember, Expression> _excluded;
        private readonly ParameterExpression _excludedParameter;
        private readonly HashSet<string> _errorTargets;
        private readonly HashSet<string> _errorSnippets;

        private class ExcludedMemberExpression : Expression
        {
            public override Type Type { get; }

            public ProjectionMember ProjectionMember { get; }

            public ExcludedMemberExpression(Type type, ProjectionMember projectionMember)
            {
                Type = type;
                ProjectionMember = projectionMember;
            }
        }

        private UpsertToMergeRewriter(ParameterExpression excludedParameter)
        {
            _excluded = new Dictionary<ProjectionMember, Expression>();
            _excludedParameter = excludedParameter;
            _errorTargets = new HashSet<string>();
            _errorSnippets = new HashSet<string>();
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _excludedParameter
                ? new ExcludedMemberExpression(node.Type, new ProjectionMember())
                : node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            var expression = Visit(node.Expression);

            if (expression is not ExcludedMemberExpression excludedMember)
            {
                return node.Update(expression);
            }

            var newMember = excludedMember.ProjectionMember.Append(node.Member);
            if (_excluded.TryGetValue(newMember, out var projection))
            {
                return projection;
            }
            else
            {
                return new ExcludedMemberExpression(node.Type, newMember);
            }
        }

        protected override Expression VisitExtension(Expression node)
        {
            if (node is ExcludedMemberExpression excludedMember)
            {
                _errorTargets.Add(excludedMember.ToString());
            }

            return base.VisitExtension(node);
        }

        public void Discover(Expression body, ProjectionMember current)
        {
            _excluded.Add(current, body);
            NewExpression newExpression;
            IEnumerable<MemberBinding> bindings;

            switch (body)
            {
                case MemberInitExpression memberInit:
                    newExpression = memberInit.NewExpression;
                    bindings = memberInit.Bindings;
                    break;

                case NewExpression @new:
                    newExpression = @new;
                    bindings = Enumerable.Empty<MemberBinding>();
                    break;

                default:
                    return;
            }

            if (newExpression.Constructor.GetParameters().Length > 0)
            {
                _errorSnippets.Add("Non-simple constructor is not supported.");
                return;
            }

            foreach (var item in bindings)
            {
                if (item is not MemberAssignment assignment)
                {
                    _errorSnippets.Add("Non-assignment binding is not supported.");
                    return;
                }

                var subItem = current.Append(assignment.Member);
                Discover(assignment.Expression, subItem);
            }
        }

        public static LambdaExpression Process(LambdaExpression insert, LambdaExpression update, out HashSet<string> errorlogs)
        {
            var visitor = new UpsertToMergeRewriter(update.Parameters[1]);
            errorlogs = visitor._errorSnippets;
            visitor.Discover(insert.Body, new ProjectionMember());

            var newBody = visitor.Visit(update.Body);

            // again for checking
            visitor.Visit(newBody);
            if (visitor._errorTargets.Count > 0)
            {
                visitor._errorSnippets.Add(
                    "The following members used in update expression didn't appear in insert expression: " +
                    string.Join(", ", visitor._errorTargets));
            }

            return errorlogs.Count > 0
                ? null
                : Expression.Lambda(
                    newBody,
                    update.Parameters[0],
                    insert.Parameters[0]);
        }
    }
}
