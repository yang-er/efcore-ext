using Microsoft.EntityFrameworkCore.Bulk;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Utilities;

#if EFCORE31
using ThirdParameter = Microsoft.EntityFrameworkCore.Metadata.IModel;
using RelationalEntityShaperExpression = Microsoft.EntityFrameworkCore.Query.EntityShaperExpression;
#elif EFCORE50
using ThirdParameter = Microsoft.EntityFrameworkCore.Query.QueryCompilationContext;
#endif

namespace Microsoft.EntityFrameworkCore.Query
{
    public class RelationalBulkQueryableMethodTranslatingExpressionVisitor : RelationalQueryableMethodTranslatingExpressionVisitor
    {
        private static readonly Func<RelationalQueryableMethodTranslatingExpressionVisitor, RelationalSqlTranslatingExpressionVisitor> _sqlTranslatorAccessor
            = ExpressionBuilder
                .Begin<RelationalQueryableMethodTranslatingExpressionVisitor>()
                .AccessField("_sqlTranslator")
                .Compile<Func<RelationalQueryableMethodTranslatingExpressionVisitor, RelationalSqlTranslatingExpressionVisitor>>();

        private readonly RelationalSqlTranslatingExpressionVisitor _sqlTranslator;
        private readonly ISqlExpressionFactory _sqlExpressionFactory;
        private readonly IAnonymousExpressionFactory _anonymousExpressionFactory;
        private readonly IModel _model;

        public RelationalBulkQueryableMethodTranslatingExpressionVisitor(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
            ThirdParameter thirdParameter,
            IAnonymousExpressionFactory anonymousExpressionFactory)
            : base(dependencies, relationalDependencies, thirdParameter)
        {
            _sqlExpressionFactory = relationalDependencies.SqlExpressionFactory;
            _anonymousExpressionFactory = anonymousExpressionFactory;
            _model = AccessModel(thirdParameter);
            _sqlTranslator = _sqlTranslatorAccessor(this);
        }

        public RelationalBulkQueryableMethodTranslatingExpressionVisitor(
            RelationalBulkQueryableMethodTranslatingExpressionVisitor parentVisitor)
            : base(parentVisitor)
        {
            _sqlExpressionFactory = parentVisitor._sqlExpressionFactory;
            _anonymousExpressionFactory = parentVisitor._anonymousExpressionFactory;
            _sqlTranslator = parentVisitor._sqlTranslator;
        }

        protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
            => new RelationalBulkQueryableMethodTranslatingExpressionVisitor(this);

#if EFCORE31

        private IModel AccessModel(ThirdParameter thirdParameter) => thirdParameter;

        private void UpdateAffectedRowsCardinality(ref ShapedQueryExpression newShaped)
            => newShaped.ResultCardinality = BulkQueryCompiler.AffectedRows;

        private bool IsSameTable(IEntityType entityType, TableExpression tableExpression)
            => entityType.GetTableName() == tableExpression.Name && entityType.GetSchema() == tableExpression.Schema;

        protected virtual ShapedQueryExpression Fail(string message) => null;

#elif EFCORE50

        private IModel AccessModel(ThirdParameter thirdParameter) => thirdParameter.Model;

        private void UpdateAffectedRowsCardinality(ref ShapedQueryExpression newShaped)
            => newShaped = newShaped.UpdateResultCardinality(BulkQueryCompiler.AffectedRows);

        private bool IsSameTable(IEntityType entityType, TableExpression tableExpression)
            => entityType.GetViewOrTableMappings().Single().Table == tableExpression.Table;

        protected virtual ShapedQueryExpression Fail(string message)
        {
            AddTranslationErrorDetails(message);
            return null;
        }

#endif

        private void ManualReshape(Expression body, IEntityType entityType, Action<IProperty, Expression> fieldCallback)
        {
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
                    Fail($"Type of {body.NodeType} is not supported in upsert yet.");
                    return;
            }

            if (newExpression.Constructor.GetParameters().Length > 0)
            {
                Fail("Non-simple constructor is not supported.");
                return;
            }

            foreach (var item in bindings)
            {
                if (item is not MemberAssignment assignment)
                {
                    Fail("Non-assignment binding is not supported.");
                    return;
                }

                var memberInfo = entityType.FindProperty(assignment.Member);
                if (memberInfo != null)
                {
                    fieldCallback(memberInfo, assignment.Expression);
                    continue;
                }

                var navigation = entityType.FindNavigation(assignment.Member);
                if (navigation == null)
                {
                    Fail($"Unknown member \"{assignment.Member}\" is not supported in update yet.");
                    return;
                }
                else if (!navigation.ForeignKey.IsOwnership)
                {
                    Fail($"Wrong member \"{navigation.ForeignKey}\". Only owned-navigation member can be upserted.");
                    return;
                }
                else
                {
                    ManualReshape(assignment.Expression, navigation.ForeignKey.DeclaringEntityType, fieldCallback);
                }
            }
        }

        protected virtual ShapedQueryExpression TranslateCommonTable(ParameterExpression param)
        {
            var entityType = _anonymousExpressionFactory.GetType(param.Type.GetGenericArguments()[0]);
            var values = new ValuesExpression(param, entityType.Fields.Select(a => a.Name).ToArray(), entityType);

            var select = _sqlExpressionFactory.Select(
                alias: null,
                projections: new List<ProjectionExpression>(),
                tables: new List<TableExpressionBase> { values },
                groupBy: new List<SqlExpression>(),
                orderings: new List<OrderingExpression>());

            var mapping = new Dictionary<ProjectionMember, Expression>();
            var rootMember = new ProjectionMember();
            var shaperArguments = new List<Expression>(entityType.Fields.Count);

            foreach (var field in entityType.Fields)
            {
                var projectionMember = rootMember.Append(field.PropertyInfo);
                mapping[projectionMember] = _sqlExpressionFactory.Column(field.Name, values, field.Type, field.TypeMapping, field.Nullable);
                shaperArguments.Add(field.CreateProjectionBinding(select, projectionMember));
            }

            select.ReplaceProjectionMapping(mapping);
            var shaper = entityType.CreateShaper(shaperArguments);
            return new ShapedQueryExpression(select, shaper);
        }

        protected virtual ShapedQueryExpression TranslateSingleTuple(LambdaExpression tuple)
        {
            var tupleSql = new List<SqlExpression>();
            var tupleNames = new List<string>();
            var values = new ValuesExpression(new[] { tupleSql }, tupleNames);

            var select = _sqlExpressionFactory.Select(
                alias: null,
                projections: new List<ProjectionExpression>(),
                tables: new List<TableExpressionBase> { values },
                groupBy: new List<SqlExpression>(),
                orderings: new List<OrderingExpression>());

            var newExpression = (NewExpression)tuple.Body;
            var mapping = new Dictionary<ProjectionMember, Expression>();
            var rootMember = new ProjectionMember();
            var shaperArguments = new List<Expression>(newExpression.Arguments.Count);

            for (int i = 0; i < newExpression.Arguments.Count; i++)
            {
                var sql = _sqlTranslator.Translate(newExpression.Arguments[i]);
                var projectionMember = rootMember.Append(newExpression.Members[i]);
                var type = newExpression.Arguments[i].Type;

                var column = _sqlExpressionFactory.Column(
                    name: projectionMember.Last.Name,
                    table: values,
                    type: type,
                    typeMapping: sql.TypeMapping,
                    nullable: type.IsNullableType());

                tupleSql.Add(sql);
                tupleNames.Add(projectionMember.Last.Name);
                mapping[projectionMember] = column;
                shaperArguments.Add(new ProjectionBindingExpression(select, projectionMember, type));
            }

            select.ReplaceProjectionMapping(mapping);
            var shaper = Expression.New(newExpression.Constructor, shaperArguments, newExpression.Members);
            return new ShapedQueryExpression(select, shaper);
        }

        protected virtual ShapedQueryExpression TranslateWrapped(WrappedExpression wrappedExpression)
        {
            var selectExpression = _sqlExpressionFactory.Select(
                alias: null,
                projections: new List<ProjectionExpression>(),
                tables: new List<TableExpressionBase> { wrappedExpression },
                groupBy: new List<SqlExpression>(),
                orderings: new List<OrderingExpression>());

            selectExpression.ReplaceProjectionMapping(
                new Dictionary<ProjectionMember, Expression>
                {
                    [new ProjectionMember()] = new AffectedRowsExpression(),
                });

            var newShaped = new ShapedQueryExpression(
                selectExpression,
                Expression.Convert(
                    new ProjectionBindingExpression(selectExpression, new ProjectionMember(), typeof(int?)),
                    typeof(int)));

            UpdateAffectedRowsCardinality(ref newShaped);
            return newShaped;
        }

        protected virtual ShapedQueryExpression TranslateDelete(Expression shaped)
        {
            if (shaped is not ShapedQueryExpression shapedQueryExpression
                || shapedQueryExpression.QueryExpression is not SelectExpression selectExpression)
            {
                return null;
            }

            if (selectExpression.Offset != null
                || selectExpression.Limit != null
                || (selectExpression.GroupBy?.Count ?? 0) != 0
                || selectExpression.Having != null)
            {
                return Fail("The query can't be aggregated or be with .Take() or .Skip() filters.");
            }

            if (selectExpression?.Tables?[0] is not TableExpression table)
            {
                return Fail("The query root should be main entity.");
            }

            return TranslateWrapped(
                new DeleteExpression(
                    table: table,
                    predicate: selectExpression.Predicate,
                    joinedTables: selectExpression.Tables));
        }

        protected virtual ShapedQueryExpression TranslateUpdate(Expression shaped, LambdaExpression updateBody)
        {
            if (shaped is not ShapedQueryExpression shapedQueryExpression
                || shapedQueryExpression.QueryExpression is not SelectExpression selectExpression
                || selectExpression.Tables[0] is not TableExpression updateRoot)
            {
                return null;
            }

            if (selectExpression.Offset != null
                || selectExpression.Limit != null
                || (selectExpression.GroupBy?.Count ?? 0) != 0
                || selectExpression.Having != null)
            {
                return Fail("The query can't be aggregated or be with .Take() or .Skip() filters.");
            }

            if (updateBody.Body is not MemberInitExpression memberInitExpression
                || memberInitExpression.NewExpression.Arguments.Count != 0)
            {
                return Fail("Invalid update body expression.");
            }

            var entityType = _model.FindEntityType(updateBody.Body.Type);
            if (entityType == null)
            {
                return Fail("Query tree root type not found.");
            }

            if (!IsSameTable(entityType, updateRoot))
            {
                return Fail("Update query root type mismatch.");
            }

            shaped = TranslateSelect(shapedQueryExpression, updateBody);
            //Check.DebugAssert(shaped == shapedQueryExpression, "Should be the same instance.");
            Check.DebugAssert(selectExpression == ((ShapedQueryExpression)shaped).QueryExpression, "Should be the same instance.");

            // Get the concrete update field expression
            var projectionMapping = selectExpression.GetProjectionMapping();
            var setFields = new List<ProjectionExpression>(projectionMapping.Count);
            var columnNames = entityType.GetColumns();

            foreach (var mapping in projectionMapping)
            {
                if (mapping.Value is not SqlExpression sqlExpression
                    || !columnNames.TryGetValue(mapping.Key.ToString(), out var fieldName))
                {
                    throw new NotImplementedException("Unknown projection mapping failed.");
                }

                setFields.Add(_sqlExpressionFactory.Projection(sqlExpression, fieldName));
            }

            return TranslateWrapped(
                new UpdateExpression(
                    expanded: false,
                    expandedTable: null,
                    predicate: selectExpression.Predicate,
                    fields: setFields,
                    tables: selectExpression.Tables));
        }

        protected virtual ShapedQueryExpression TranslateSelectInto(Expression shaped, Type rootType)
        {
            if (shaped is not ShapedQueryExpression shapedQueryExpression
                || shapedQueryExpression.QueryExpression is not SelectExpression selectExpression
                || shapedQueryExpression.ShaperExpression is not MemberInitExpression memberInitExpression
                || memberInitExpression.NewExpression.Arguments.Count > 0)
            {
                return null;
            }

            var entityType = _model.FindEntityType(rootType);
            if (entityType == null)
            {
                return Fail("Query tree root type not found.");
            }

            var tableName = entityType.GetTableName();
            var schema = entityType.GetSchema();
            var columnNames = entityType.GetColumns();
            var projectionMapping = selectExpression.GetProjectionMapping();
            var projections = (List<ProjectionExpression>)selectExpression.Projection;
            var newProjectionMapping = new Dictionary<ProjectionMember, Expression>();
            if (projections.Count > 0)
            {
                throw new NotImplementedException("Why is projection here expanded?");
            }

            foreach (var mapping in projectionMapping)
            {
                if (mapping.Value is not SqlExpression sqlExpression
                    || !columnNames.TryGetValue(mapping.Key.ToString(), out var fieldName))
                {
                    throw new NotImplementedException("Unknown projection mapping failed.");
                }

                newProjectionMapping.Add(mapping.Key, Expression.Constant(projections.Count));
                projections.Add(_sqlExpressionFactory.Projection(sqlExpression, fieldName));
            }

            selectExpression.ReplaceProjectionMapping(newProjectionMapping);

            return TranslateWrapped(
                new SelectIntoExpression(
                    tableName: tableName,
                    schema: schema,
                    selectExpression: selectExpression));
        }

        protected virtual ShapedQueryExpression TranslateUpsert(Expression target, Expression source, LambdaExpression insert, LambdaExpression update)
        {
            if (target is not ShapedQueryExpression targetShaped
                || source is not ShapedQueryExpression sourceShaped
                || targetShaped.QueryExpression is not SelectExpression targetSelect
                || targetSelect.Tables.Count != 1
                || sourceShaped.QueryExpression is not SelectExpression
                || targetShaped.ShaperExpression is not EntityShaperExpression)
            {
                return null;
            }

            var targetTable = targetSelect.Tables.Single();
            var entityType = ((EntityShaperExpression)targetShaped.ShaperExpression).EntityType;
            if (insert.Body is not MemberInitExpression insertMemberInit
                || insertMemberInit.NewExpression.Arguments.Count > 0)
            {
                return Fail("Insert expression should be member-init without constructor arguments.");
            }

            if (!entityType.TryGuessKey(insertMemberInit.Bindings, out var pkeyOrAkey))
            {
                return Fail(
                    "Only entity with primary key or alternative key specified can be upserted. " +
                    "Are you trying to normally insert one or lost your key fields?");
            }

            var newShaper = targetSelect.AddCrossJoinForMerge(sourceShaped, targetShaped.ShaperExpression);
            if (newShaper is not NewExpression newNewShaper
                || newNewShaper.Arguments.Count != 2
                || targetSelect.Tables.Count != 2
                || targetSelect.Tables[0] is not TableExpression tableExpression
                || targetSelect.Tables[1] is not CrossJoinExpression crossJoinExpression
                || targetSelect.IsDistinct
                || targetSelect.Having != null
                || targetSelect.Alias != null
                || targetSelect.GroupBy.Count != 0
                || targetSelect.Limit != null
                || targetSelect.Offset != null
                || targetSelect.Orderings.Count != 0
                || targetSelect.Predicate != null
                || targetSelect.Projection.Count != 0)
            {
                throw new NotImplementedException(
                    "Unknown produced select expression: \r\n" +
                    targetSelect.Print());
            }

            var soi = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table).Value;
            var insertFields = new List<ProjectionExpression>();
            List<ProjectionExpression> updateFields;

            ManualReshape(
                ReplacingExpressionVisitor.Replace(
                    insert.Parameters.Single(),
                    newNewShaper.Arguments[1],
                    insert.Body),
                entityType,
                (property, expression) =>
                    insertFields.Add(
                        _sqlExpressionFactory.Projection(
                            _sqlTranslator.Translate(expression),
                            property.GetColumnName(soi))));

            if (update.IsBodyConstantNull())
            {
                updateFields = null;
            }
            else
            {
                updateFields = new List<ProjectionExpression>();

                var excludedTable = _sqlExpressionFactory.Select(entityType);
                var excludedRewriter = new ExcludedTableColumnRewritingVisitor((TableExpression)excludedTable.Tables.Single());
                var excludedShaper = new RelationalEntityShaperExpression(
                        entityType,
                        new ProjectionBindingExpression(excludedTable, new ProjectionMember(), typeof(ValueBuffer)),
                        false);

                var replacing = update.Parameters.ToArray();
                var replacement = new[] { newNewShaper.Arguments[0], excludedShaper };

                ManualReshape(
                    new ReplacingExpressionVisitor(replacing, replacement).Visit(update.Body),
                    entityType,
                    (property, expression) =>
                        updateFields.Add(
                            _sqlExpressionFactory.Projection(
                                excludedRewriter.VisitAndConvert(_sqlTranslator.Translate(expression), null),
                                property.GetColumnName(soi))));
            }

            return TranslateWrapped(
                new UpsertExpression(
                    targetTable: tableExpression,
                    sourceTable: crossJoinExpression.Table,
                    columns: insertFields,
                    onConflict: updateFields,
                    conflictConstraintName: pkeyOrAkey.GetName()));
        }

        protected virtual ShapedQueryExpression TranslateUpsert(Expression target, LambdaExpression insert, LambdaExpression update)
        {
            if (target is not ShapedQueryExpression targetShaped
                || targetShaped.QueryExpression is not SelectExpression targetSelect
                || targetSelect.Tables.Count != 1
                || targetSelect.Tables[0] is not TableExpression targetTable
                || targetShaped.ShaperExpression is not EntityShaperExpression targetShaper)
            {
                return null;
            }

            var entityType = targetShaper.EntityType;
            if (insert.Body is not MemberInitExpression insertMemberInit
                || insertMemberInit.NewExpression.Arguments.Count > 0)
            {
                return Fail("Insert expression should be member-init without constructor arguments.");
            }

            if (!entityType.TryGuessKey(insertMemberInit.Bindings, out var pkeyOrAkey))
            {
                return Fail(
                    "Only entity with primary key or alternative key specified can be upserted. " +
                    "Are you trying to normally insert one or lost your key fields?");
            }

            var soi = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table).Value;
            var insertFields = new List<ProjectionExpression>();
            List<ProjectionExpression> updateFields;

            ManualReshape(
                insert.Body,
                entityType,
                (property, expression) =>
                    insertFields.Add(
                        _sqlExpressionFactory.Projection(
                            _sqlTranslator.Translate(expression),
                            property.GetColumnName(soi))));

            if (update.IsBodyConstantNull())
            {
                updateFields = null;
            }
            else
            {
                updateFields = new List<ProjectionExpression>();

                ManualReshape(
                    ReplacingExpressionVisitor.Replace(
                        update.Parameters[0],
                        targetShaper,
                        update.Body),
                    entityType,
                    (property, expression) =>
                        updateFields.Add(
                            _sqlExpressionFactory.Projection(
                                _sqlTranslator.Translate(expression),
                                property.GetColumnName(soi))));
            }

            return TranslateWrapped(
                new UpsertExpression(
                    targetTable: targetTable,
                    sourceTable: null,
                    columns: insertFields,
                    onConflict: updateFields,
                    conflictConstraintName: pkeyOrAkey.GetName()));
        }

        protected virtual ShapedQueryExpression TranslateMerge(Expression target, Expression source, LambdaExpression targetKeySelector, LambdaExpression sourceKeySelector, LambdaExpression updateExpression, LambdaExpression insertExpression, Expression doDelete)
        {
            if (target is not ShapedQueryExpression targetShaped
                || source is not ShapedQueryExpression sourceShaped
                || targetShaped.QueryExpression is not SelectExpression targetSelect
                || targetSelect.Tables.Count != 1
                || sourceShaped.QueryExpression is not SelectExpression
                || targetShaped.ShaperExpression is not EntityShaperExpression)
            {
                return null;
            }

            var entityType = ((EntityShaperExpression)targetShaped.ShaperExpression).EntityType;
            var soi = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table).Value;
            var targetType = targetShaped.ShaperExpression.Type;
            var sourceType = sourceShaped.ShaperExpression.Type;
            var targetParameter = Expression.Parameter(targetType, "t");
            var sourceParameter = Expression.Parameter(sourceType, "s");
            var targetSourceTiType = TransparentIdentifierFactory.Create(targetType, sourceType);
            var targetSourceTiOuter = targetSourceTiType.GetField("Outer");
            var targetSourceTiInner = targetSourceTiType.GetField("Inner");

            var targetSourceTiResult = Expression.Lambda(
                Expression.New(
                    targetSourceTiType.GetConstructors()[0],
                    new[] { targetParameter, sourceParameter },
                    new[] { targetSourceTiOuter, targetSourceTiInner }),
                targetParameter,
                sourceParameter);

            var joinedShaped = TranslateJoin(targetShaped, sourceShaped, targetKeySelector, sourceKeySelector, targetSourceTiResult);
            if (joinedShaped == null || joinedShaped.ShaperExpression.Type != targetSourceTiType)
            {
                return Fail("Fake join failed.");
            }

            var selectExpression = (SelectExpression)joinedShaped.QueryExpression;
            var shaperNewExpression = (NewExpression)joinedShaped.ShaperExpression;

            List<ProjectionExpression> insert = null, update = null;
            if (doDelete is not ConstantExpression doDeleteConstant
                || doDeleteConstant.Value is not bool delete)
            {
                return Fail("The delete option of merge queries must be a constant.");
            }

            if (!insertExpression.IsBodyConstantNull())
            {
                insert = new List<ProjectionExpression>();
                ManualReshape(
                    ReplacingExpressionVisitor.Replace(
                        insertExpression.Parameters.Single(),
                        shaperNewExpression.Arguments[1],
                        insertExpression.Body),
                    entityType,
                    (property, expression) =>
                        insert.Add(
                            _sqlExpressionFactory.Projection(
                                _sqlTranslator.Translate(expression),
                                property.GetColumnName(soi))));
            }

            if (!updateExpression.IsBodyConstantNull())
            {
                update = new List<ProjectionExpression>();
                var replacing = updateExpression.Parameters.ToArray();
                var replacement = shaperNewExpression.Arguments.ToArray();

                ManualReshape(
                    new ReplacingExpressionVisitor(replacing, replacement).Visit(updateExpression.Body),
                    entityType,
                    (property, expression) =>
                        update.Add(
                            _sqlExpressionFactory.Projection(
                                _sqlTranslator.Translate(expression),
                                property.GetColumnName(soi))));
            }

            if (selectExpression.Tables.Count != 2
                || selectExpression.Predicate != null
                || selectExpression.Tables[1] is not InnerJoinExpression innerJoin
                || selectExpression.Tables[0] is not TableExpression table)
                return Fail("Unknown joined expression: " + selectExpression.Print());

            return TranslateWrapped(
                new MergeExpression(
                    targetTable: table,
                    sourceTable: innerJoin.Table,
                    joinPredicate: innerJoin.JoinPredicate,
                    matched: update,
                    notMatchedByTarget: insert,
                    notMatchedBySource: delete));
        }

        protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
        {
            var method = methodCallExpression.Method;
            if (method.DeclaringType == typeof(BatchOperationExtensions))
            {
                var genericMethod = method.GetGenericMethodDefinition();
                switch (method.Name)
                {
                    case nameof(BatchOperationExtensions.CreateCommonTable)
                    when genericMethod == BatchOperationMethods.CreateCommonTable &&
                         methodCallExpression.Arguments[1] is ParameterExpression param:
                        return CheckTranslated(TranslateCommonTable(param));

                    case nameof(BatchOperationExtensions.CreateSingleTuple)
                    when genericMethod == BatchOperationMethods.CreateSingleTuple:
                        return CheckTranslated(TranslateSingleTuple(GetLambdaAt(1)));

                    case nameof(BatchOperationExtensions.BatchDelete)
                    when genericMethod == BatchOperationMethods.BatchDelete:
                        return CheckTranslated(TranslateDelete(GetShapedAt(0)));

                    case nameof(BatchOperationExtensions.BatchUpdate)
                    when genericMethod == BatchOperationMethods.BatchUpdateExpanded:
                        return CheckTranslated(TranslateUpdate(GetShapedAt(0), GetLambdaAt(1)));

                    case nameof(BatchOperationExtensions.BatchInsertInto)
                    when genericMethod == BatchOperationMethods.BatchInsertIntoCollapsed:
                        return CheckTranslated(TranslateSelectInto(GetShapedAt(0), method.GetGenericArguments()[0]));

                    case nameof(BatchOperationExtensions.Upsert)
                    when genericMethod == BatchOperationMethods.UpsertCollapsed:
                        return CheckTranslated(TranslateUpsert(GetShapedAt(0), GetShapedAt(1), GetLambdaAt(2), GetLambdaAt(3)));

                    case nameof(BatchOperationExtensions.Upsert)
                    when genericMethod == BatchOperationMethods.UpsertOneCollapsed:
                        return CheckTranslated(TranslateUpsert(GetShapedAt(0), GetLambdaAt(1), GetLambdaAt(2)));

                    case nameof(BatchOperationExtensions.Merge)
                    when genericMethod == BatchOperationMethods.MergeCollapsed:
                        return CheckTranslated(TranslateMerge(GetShapedAt(0), GetShapedAt(1), GetLambdaAt(2), GetLambdaAt(3), GetLambdaAt(4), GetLambdaAt(5), methodCallExpression.Arguments[6]));
                }
            }

            return base.VisitMethodCall(methodCallExpression);

            Expression GetShapedAt(int argumentIndex)
                => Visit(methodCallExpression.Arguments[argumentIndex]);

            LambdaExpression GetLambdaAt(int argumentIndex)
                => methodCallExpression.Arguments[argumentIndex].UnwrapLambdaFromQuote();

#if EFCORE31
            ShapedQueryExpression CheckTranslated(ShapedQueryExpression translated)
            {
                if (translated == null)
                {
                    throw new InvalidOperationException(
                        CoreStrings.TranslationFailed(methodCallExpression.Print()));
                }

                return translated;
            }
#elif EFCORE50
            ShapedQueryExpression CheckTranslated(ShapedQueryExpression translated)
            {
                return translated
                    ?? throw new InvalidOperationException(
                        TranslationErrorDetails == null
                            ? CoreStrings.TranslationFailed(methodCallExpression.Print())
                            : CoreStrings.TranslationFailedWithDetails(
                                methodCallExpression.Print(),
                                TranslationErrorDetails));
            }
#endif
        }
    }

    public class RelationalBulkQueryableMethodTranslatingExpressionVisitorFactory :
        IBulkQueryableMethodTranslatingExpressionVisitorFactory,
        IServiceAnnotation<IQueryableMethodTranslatingExpressionVisitorFactory, RelationalQueryableMethodTranslatingExpressionVisitorFactory>
    {
        private readonly QueryableMethodTranslatingExpressionVisitorDependencies _dependencies;
        private readonly RelationalQueryableMethodTranslatingExpressionVisitorDependencies _relationalDependencies;
        private readonly IAnonymousExpressionFactory _anonymousExpressionFactory;

        public RelationalBulkQueryableMethodTranslatingExpressionVisitorFactory(
            QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
            RelationalQueryableMethodTranslatingExpressionVisitorDependencies relationalDependencies,
            IAnonymousExpressionFactory anonymousExpressionFactory)
        {
            _dependencies = dependencies;
            _relationalDependencies = relationalDependencies;
            _anonymousExpressionFactory = anonymousExpressionFactory;
        }

        public QueryableMethodTranslatingExpressionVisitor Create(ThirdParameter thirdParameter)
        {
            return new RelationalBulkQueryableMethodTranslatingExpressionVisitor(
                _dependencies,
                _relationalDependencies,
                thirdParameter,
                _anonymousExpressionFactory);
        }
    }
}
