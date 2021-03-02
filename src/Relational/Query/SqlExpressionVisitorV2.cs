using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using System;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore.Query
{
    /// <inheritdoc />
    public class SqlExpressionVisitorV2 : SqlExpressionVisitor
    {
        /// <inheritdoc />
        protected override Expression VisitCase(CaseExpression caseExpression)
            => BaseVisitExtension(caseExpression);

        /// <inheritdoc />
        protected override Expression VisitCrossApply(CrossApplyExpression crossApplyExpression)
            => BaseVisitExtension(crossApplyExpression);

        /// <inheritdoc />
        protected override Expression VisitCrossJoin(CrossJoinExpression crossJoinExpression)
            => BaseVisitExtension(crossJoinExpression);

        /// <inheritdoc />
        protected override Expression VisitExcept(ExceptExpression exceptExpression)
            => BaseVisitExtension(exceptExpression);

        /// <inheritdoc />
        protected override Expression VisitExists(ExistsExpression existsExpression)
            => BaseVisitExtension(existsExpression);

        /// <inheritdoc />
        protected override Expression VisitIn(InExpression inExpression)
            => BaseVisitExtension(inExpression);

        /// <inheritdoc />
        protected override Expression VisitInnerJoin(InnerJoinExpression innerJoinExpression)
            => BaseVisitExtension(innerJoinExpression);

        /// <inheritdoc />
        protected override Expression VisitIntersect(IntersectExpression intersectExpression)
            => BaseVisitExtension(intersectExpression);

        /// <inheritdoc />
        protected override Expression VisitLeftJoin(LeftJoinExpression leftJoinExpression)
            => BaseVisitExtension(leftJoinExpression);

        /// <inheritdoc />
        protected override Expression VisitLike(LikeExpression likeExpression)
            => BaseVisitExtension(likeExpression);

        /// <inheritdoc />
        protected override Expression VisitOrdering(OrderingExpression orderingExpression)
            => BaseVisitExtension(orderingExpression);

        /// <inheritdoc />
        protected override Expression VisitOuterApply(OuterApplyExpression outerApplyExpression)
            => BaseVisitExtension(outerApplyExpression);

        /// <inheritdoc />
        protected override Expression VisitProjection(ProjectionExpression projectionExpression)
            => BaseVisitExtension(projectionExpression);

        /// <inheritdoc />
        protected override Expression VisitRowNumber(RowNumberExpression rowNumberExpression)
            => BaseVisitExtension(rowNumberExpression);

        /// <inheritdoc />
        protected override Expression VisitSqlBinary(SqlBinaryExpression sqlBinaryExpression)
            => BaseVisitExtension(sqlBinaryExpression);

        /// <inheritdoc />
        protected override Expression VisitUnion(UnionExpression unionExpression)
            => BaseVisitExtension(unionExpression);

        /// <inheritdoc />
        protected override Expression VisitSqlUnary(SqlUnaryExpression sqlUnaryExpression)
            => BaseVisitExtension(sqlUnaryExpression);

        /// <inheritdoc />
        protected override Expression VisitSqlFunction(SqlFunctionExpression sqlFunctionExpression)
            => BaseVisitExtension(sqlFunctionExpression);

        /// <inheritdoc />
        protected override Expression VisitSqlConstant(SqlConstantExpression sqlConstantExpression)
            => BaseVisitExtension(sqlConstantExpression);

        /// <inheritdoc />
        protected override Expression VisitTable(TableExpression tableExpression)
            => BaseVisitExtension(tableExpression);

        /// <inheritdoc />
        protected override Expression VisitSqlFragment(SqlFragmentExpression sqlFragmentExpression)
            => BaseVisitExtension(sqlFragmentExpression);

        /// <inheritdoc />
        protected override Expression VisitSqlParameter(SqlParameterExpression sqlParameterExpression)
            => BaseVisitExtension(sqlParameterExpression);

        /// <inheritdoc />
        protected override Expression VisitColumn(ColumnExpression columnExpression)
            => BaseVisitExtension(columnExpression);

        /// <inheritdoc />
        protected override Expression VisitFromSql(FromSqlExpression fromSqlExpression)
            => BaseVisitExtension(fromSqlExpression);

        /// <inheritdoc />
        protected override Expression VisitSelect(SelectExpression selectExpression)
            => BaseVisitExtension(selectExpression);

        /// <summary>
        /// Visits the children of the delete expression.
        /// </summary>
        /// <param name="deleteExpression">The expression to visit.</param>
        /// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
        protected virtual Expression VisitDelete(DeleteExpression deleteExpression)
            => BaseVisitExtension(deleteExpression);

        /// <summary>
        /// Visits the children of the merge expression.
        /// </summary>
        /// <param name="mergeExpression">The expression to visit.</param>
        /// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
        protected virtual Expression VisitMerge(MergeExpression mergeExpression)
            => BaseVisitExtension(mergeExpression);

        /// <summary>
        /// Visits the children of the select into expression.
        /// </summary>
        /// <param name="selectIntoExpression">The expression to visit.</param>
        /// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
        protected virtual Expression VisitSelectInto(SelectIntoExpression selectIntoExpression)
            => BaseVisitExtension(selectIntoExpression);

        /// <summary>
        /// Visits the children of the update expression.
        /// </summary>
        /// <param name="updateExpression">The expression to visit.</param>
        /// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
        protected virtual Expression VisitUpdate(UpdateExpression updateExpression)
            => BaseVisitExtension(updateExpression);

        /// <summary>
        /// Visits the children of the values expression.
        /// </summary>
        /// <param name="valuesExpression">The expression to visit.</param>
        /// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
        protected virtual Expression VisitValues(ValuesExpression valuesExpression)
            => BaseVisitExtension(valuesExpression);

        /// <summary>
        /// Visits the children of the upsert expression.
        /// </summary>
        /// <param name="upsertExpression">The expression to visit.</param>
        /// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
        protected virtual Expression VisitUpsert(UpsertExpression upsertExpression)
            => BaseVisitExtension(upsertExpression);

        /// <summary>
        /// Visits the children of the excluded table column expression.
        /// </summary>
        /// <param name="excludedTableColumnExpression">The expression to visit.</param>
        /// <returns>The modified expression, if it or any subexpression was modified; otherwise, returns the original expression.</returns>
        protected virtual Expression VisitExcludedTableColumn(ExcludedTableColumnExpression excludedTableColumnExpression)
            => BaseVisitExtension(excludedTableColumnExpression);

#if EFCORE31

        /// <inheritdoc />
        protected override Expression VisitSubSelect(ScalarSubqueryExpression scalarSubqueryExpression)
            => BaseVisitExtension(scalarSubqueryExpression);

#elif EFCORE50

        /// <inheritdoc />
        protected override Expression VisitScalarSubquery(ScalarSubqueryExpression scalarSubqueryExpression)
            => BaseVisitExtension(scalarSubqueryExpression);

        /// <inheritdoc />
        protected override Expression VisitCollate(CollateExpression collateExpression)
            => BaseVisitExtension(collateExpression);

        /// <inheritdoc />
        protected override Expression VisitDistinct(DistinctExpression distinctExpression)
            => BaseVisitExtension(distinctExpression);

        /// <inheritdoc />
        protected override Expression VisitTableValuedFunction(TableValuedFunctionExpression tableValuedFunctionExpression)
            => BaseVisitExtension(tableValuedFunctionExpression);

#endif

        protected override Expression VisitExtension(Expression extensionExpression)
        {
            return extensionExpression switch
            {
                DeleteExpression deleteExpression => VisitDelete(deleteExpression),
                MergeExpression mergeExpression => VisitMerge(mergeExpression),
                ValuesExpression valuesExpression => VisitValues(valuesExpression),
                UpdateExpression updateExpression => VisitUpdate(updateExpression),
                SelectIntoExpression selectIntoExpression => VisitSelectInto(selectIntoExpression),
                _ => base.VisitExtension(extensionExpression)
            };
        }

        #region .NET Expression Tree Nodes

        private static readonly Func<Expression, ExpressionVisitor, Expression> s_VisitChildren
            = typeof(Expression)
                .GetMethod("VisitChildren", Internals.FindPrivate)
                .CreateDelegate(typeof(Func<Expression, ExpressionVisitor, Expression>))
              as Func<Expression, ExpressionVisitor, Expression>;

        private Expression BaseVisitExtension(Expression expression) => s_VisitChildren.Invoke(expression, this);

        protected sealed override Expression VisitBinary(BinaryExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitBlock(BlockExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override CatchBlock VisitCatchBlock(CatchBlock node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitConditional(ConditionalExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitConstant(ConstantExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitDebugInfo(DebugInfoExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitDefault(DefaultExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitDynamic(DynamicExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override ElementInit VisitElementInit(ElementInit node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitGoto(GotoExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitIndex(IndexExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitInvocation(InvocationExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitLabel(LabelExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitLambda<T>(Expression<T> node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override LabelTarget VisitLabelTarget(LabelTarget node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitListInit(ListInitExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitLoop(LoopExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitMember(MemberExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override MemberAssignment VisitMemberAssignment(MemberAssignment node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override MemberBinding VisitMemberBinding(MemberBinding node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitMemberInit(MemberInitExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override MemberListBinding VisitMemberListBinding(MemberListBinding node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitMethodCall(MethodCallExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitNew(NewExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitNewArray(NewArrayExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitParameter(ParameterExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitRuntimeVariables(RuntimeVariablesExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitSwitch(SwitchExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override SwitchCase VisitSwitchCase(SwitchCase node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitTry(TryExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitTypeBinary(TypeBinaryExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        protected sealed override Expression VisitUnary(UnaryExpression node)
            => throw new InvalidOperationException("Invalid SQL Expression.");

        #endregion
    }
}
