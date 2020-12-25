using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Microsoft.EntityFrameworkCore
{
    public static class TableSplittingJoinsRemovalExtensions
    {
        private class TableSplittingJoinsRemovalDbContextOptionsExtension : IDbContextOptionsExtension
        {
            private DbContextOptionsExtensionInfo _info;

            public DbContextOptionsExtensionInfo Info =>
                _info ??= new TableSplittingJoinsRemovalDbContextOptionsExtensionInfo(this);

            public void ApplyServices(IServiceCollection services)
            {
                var sd = services.FirstOrDefault(d => d.ServiceType == typeof(IQueryTranslationPostprocessorFactory));
                if (sd == null) throw new InvalidOperationException("No such IQueryTranslationPostprocessorFactory.");
                var newType = typeof(WrappingQueryTranslationPostprocessorFactory<>).MakeGenericType(sd.ImplementationType);
                services[services.IndexOf(sd)] = ServiceDescriptor.Singleton(sd.ServiceType, newType);
                services.AddSingleton(sd.ImplementationType);
            }

            public void Validate(IDbContextOptions options)
            {
            }

            private class TableSplittingJoinsRemovalDbContextOptionsExtensionInfo : DbContextOptionsExtensionInfo
            {
                public TableSplittingJoinsRemovalDbContextOptionsExtensionInfo(
                    TableSplittingJoinsRemovalDbContextOptionsExtension extension) : base(extension)
                {
                }

                public override bool IsDatabaseProvider => false;

                public override string LogFragment => "using TableSplittingJoinsRemoval ";

                public override long GetServiceProviderHashCode() => 0;

                public override void PopulateDebugInfo(IDictionary<string, string> debugInfo)
                    => debugInfo["TableSplittingJoinsRemoval"] = "1";
            }
        }

        public static DbContextOptionsBuilder UseTableSplittingJoinsRemoval(
            this DbContextOptionsBuilder builder)
        {
            ((IDbContextOptionsBuilderInfrastructure)builder)
                .AddOrUpdateExtension(new TableSplittingJoinsRemovalDbContextOptionsExtension());
            return builder;
        }
    }

    namespace Query.Internal
    {
        public class WrappingQueryTranslationPostprocessorFactory
            <TQueryTranslationPostprocessorFactory> :
            IQueryTranslationPostprocessorFactory
            where TQueryTranslationPostprocessorFactory : IQueryTranslationPostprocessorFactory
        {
            private readonly QueryTranslationPostprocessorDependencies _dependencies;
            private readonly TQueryTranslationPostprocessorFactory _factory;

            public WrappingQueryTranslationPostprocessorFactory(
                QueryTranslationPostprocessorDependencies dependencies,
                TQueryTranslationPostprocessorFactory realFactory)
            {
                _dependencies = dependencies;
                _factory = realFactory;
            }

            public virtual QueryTranslationPostprocessor Create(QueryCompilationContext queryCompilationContext)
                => new TableSplittingJoinsRemovalWrappingQueryTranslationPostprocessor(
                    _dependencies,
                    _factory.Create(queryCompilationContext),
                    queryCompilationContext);
        }

        public class TableSplittingJoinsRemovalWrappingQueryTranslationPostprocessor : QueryTranslationPostprocessor
        {
            private IModel Model { get; }

            private QueryTranslationPostprocessor RealProcessor { get; }

            public TableSplittingJoinsRemovalWrappingQueryTranslationPostprocessor(
                QueryTranslationPostprocessorDependencies dependencies,
                QueryTranslationPostprocessor realPostprocessor,
                QueryCompilationContext queryCompilationContext)
                : base(dependencies)
            {
                RealProcessor = realPostprocessor;
                Model = queryCompilationContext.Model;
            }

            public override Expression Process(Expression query)
            {
                query = new TableSplittingJoinsRemovalVisitor(Model).Visit(query);
                query = RealProcessor.Process(query);
                return query;
            }
        }

        internal class ColumnJoinsReduceVisitor : ExpressionVisitor
        {
            private Func<ColumnExpression, Expression> Factory { get; }

            public ColumnJoinsReduceVisitor(Func<ColumnExpression, Expression> t) => Factory = t;

            protected override Expression VisitExtension(Expression node)
            {
                return node switch
                {
                    ColumnExpression col => Factory(col),
                    _ => base.VisitExtension(node),
                };
            }
        }

        internal class SelfJoinPredicateChecker
        {
            public IModel Model { get; }

            private IEntityType GetEntityByTableName(string tableName)
                => Model.GetEntityTypes()
                    .Where(e => e.GetTableName() == tableName && !e.IsOwned())
                    .FirstOrDefault();

            public SelfJoinPredicateChecker(IModel model) => Model = model;

            private static HashSet<string> TakeOutKeys(SqlExpression predicate, Func<ColumnExpression, string> translator)
            {
                var set = new HashSet<string>();
                bool fail = false;

                void Parse(SqlExpression exp)
                {
                    if (exp is SqlBinaryExpression binaryExpression)
                    {
                        if (binaryExpression.OperatorType == ExpressionType.Equal)
                        {
                            if (binaryExpression.Left is ColumnExpression col
                                && binaryExpression.Right is ColumnExpression col2
                                && col.Name == translator(col2))
                                set.Add(col.Name);
                            else
                                fail = true;
                        }
                        else if (binaryExpression.OperatorType == ExpressionType.AndAlso)
                        {
                            Parse(binaryExpression.Left);
                            Parse(binaryExpression.Right);
                        }
                    }
                    else
                    {
                        fail = true;
                    }
                }

                Parse(predicate);
                return fail ? null : set;
            }

            private bool Access(TableExpression table, SelectExpression select, SqlExpression predicate, bool isLeft)
            {
                if (!(select.Tables.Count == 1 && select.Tables[0] is TableExpression tbl2))
                    return false;

                if (table.Name != tbl2.Name)
                    return false;

                var s = TakeOutKeys(predicate, c =>
                {
                    var prj = select.Projection.Single(p => p.Alias == c.Name);
                    return (prj.Expression as ColumnExpression)?.Name;
                });

                if (s == null) return false;
                var e = GetEntityByTableName(tbl2.Name);
                if (s.SetEquals(e.FindPrimaryKey().Properties.Select(p => p.Name)))
                    return true; // self-join is the primary keys

                // other check?
                return false;
            }

            private bool Access(TableExpression table, TableExpression table2, SqlExpression predicate, bool isLeft)
            {
                if (table.Name != table2.Name) return false;

                var s = TakeOutKeys(predicate, c => c.Name);
                if (s == null) return false;
                var e = GetEntityByTableName(table.Name);
                if (s.SetEquals(e.FindPrimaryKey().Properties.Select(p => p.Name)))
                    return true;

                // other check?
                return false;
            }

            public bool Access(TableExpression table, PredicateJoinExpressionBase join)
            {
                bool isLeft = join is LeftJoinExpression;

                return join.Table switch
                {
                    SelectExpression s => Access(table, s, join.JoinPredicate, isLeft),
                    TableExpression tb => Access(table, tb, join.JoinPredicate, isLeft),
                    _ => false, // other check?
                };
            }
        }

        internal class ProjectionNameComparer : IEqualityComparer<ProjectionExpression>
        {
            public static IEqualityComparer<ProjectionExpression> Default = new ProjectionNameComparer();

            public bool Equals([AllowNull] ProjectionExpression x, [AllowNull] ProjectionExpression y)
            {
                return x.Alias == y.Alias;
            }

            public int GetHashCode([DisallowNull] ProjectionExpression obj)
            {
                return obj.Alias.GetHashCode();
            }
        }

        public class TableSplittingJoinsRemovalVisitor : ExpressionVisitor
        {
            private SelfJoinPredicateChecker Checker { get; }

            public TableSplittingJoinsRemovalVisitor(IModel model)
                => Checker = new SelfJoinPredicateChecker(model);

            private static Func<ColumnExpression, Expression> Change(TableExpressionBase tbl1)
                => c => RelationalInternals.CreateColumnExpression(c.Name, tbl1, c.Type, c.TypeMapping, c.IsNullable);

            private SqlExpression Merge(ExpressionType type, SqlExpression left, SqlExpression right)
            {
                if (left == null && right == null)
                    return null;
                else if (left != null && right != null)
                    return new SqlBinaryExpression(type, left, right, left.Type, left.TypeMapping);
                else
                    return left ?? right; // at least catch one
            }

            private TableExpressionBase Reduce(
                SelectExpression select,
                PredicateJoinExpressionBase tbl2,
                Func<ColumnExpression, Expression> reducer)
            {
                var lst = (List<ProjectionExpression>)select.Projection;
                var tbls = (List<TableExpressionBase>)select.Tables;
                tbls.Remove(tbl2);

                var visitor2 = new ColumnJoinsReduceVisitor(c => c.Table == tbl2.Table ? reducer(c) : c);

                for (int i = 0; i < lst.Count; i++)
                    lst[i] = (ProjectionExpression)visitor2.Visit(lst[i]);

                var dict = RelationalInternals.AccessProjectionMapping(select);
                foreach (var item in dict.Keys.ToList())
                    dict[item] = visitor2.Visit(dict[item]);

                if (!(tbl2 is LeftJoinExpression) && tbl2.Table is SelectExpression sel2)
                {
                    var pred = (SqlExpression)visitor2.Visit(select.Predicate);
                    var pred2 = (SqlExpression)visitor2.Visit(sel2.Predicate);
                    pred = Merge(ExpressionType.AndAlso, pred, pred2);
                    RelationalInternals.ApplyPredicate(select, pred);
                }

                return select;
            }

            private PredicateJoinExpressionBase Renew(
                SelectExpression select,
                PredicateJoinExpressionBase join,
                TableExpressionBase newTable)
            {
                if (!(newTable is SelectExpression s)) throw new NotImplementedException();
                RelationalInternals.ApplyAlias(s, join.Table.Alias);
                var oldTable = join.Table;

                var visitor = new ColumnJoinsReduceVisitor(c => c.Table == oldTable ? Change(s)(c) : c);
                var pred = (SqlExpression)visitor.Visit(join.JoinPredicate);
                var lsts = (List<ProjectionExpression>)select.Projection;
                for (int i = 0; i < lsts.Count; i++)
                    lsts[i] = (ProjectionExpression)visitor.Visit(lsts[i]);

                var newT = Get(join, newTable, pred);
                var tbls = (List<TableExpressionBase>)select.Tables;
                for (int i = 0; i < tbls.Count; i++)
                    if (tbls[i] == join)
                        return (PredicateJoinExpressionBase)(tbls[i] = newT);
                throw new InvalidOperationException();

                static PredicateJoinExpressionBase Get(
                    PredicateJoinExpressionBase join,
                    TableExpressionBase newTable,
                    SqlExpression pred)
                {
                    if (join is LeftJoinExpression)
                        return new LeftJoinExpression(newTable, pred);
                    if (join is InnerJoinExpression)
                        return new InnerJoinExpression(newTable, pred);
                    throw new NotImplementedException();
                }
            }

            private SelectExpression VisitSelect(SelectExpression selectExpression)
            {
                var newTables = selectExpression.Tables
                    .Select(i => Visit(i))
                    .Cast<TableExpressionBase>()
                    .ToList();

                // old table 1 is UNION, it may be changed
                if (selectExpression.Tables.Count == 1
                    && selectExpression.Tables[0] is UnionExpression
                    && newTables[0] is SelectExpression sel)
                    return sel;

                void GoReduce(TableExpression table1, int startId)
                {
                    for (int i = startId; i < selectExpression.Tables.Count; i++)
                    {
                        if (!(selectExpression.Tables[i] is PredicateJoinExpressionBase pjeb))
                            continue;

                        var t2 = (TableExpressionBase)Visit(pjeb.Table);
                        if (t2 != pjeb.Table)
                            pjeb = Renew(selectExpression, pjeb, t2);

                        if (!Checker.Access(table1, pjeb))
                            continue;
                        i--;

                        if (pjeb.Table is TableExpression)
                        {
                            Reduce(selectExpression, pjeb, Change(table1));
                        }
                        else if (pjeb.Table is SelectExpression tbl3)
                        {
                            var visitor1 = new ColumnJoinsReduceVisitor(Change(table1));

                            var tbl3proj = tbl3.Projection
                                .Distinct(ProjectionNameComparer.Default)
                                .ToDictionary(p => p.Alias, p => visitor1.Visit(p.Expression));

                            var pred2 = (SqlExpression)visitor1.Visit(tbl3.Predicate);
                            RelationalInternals.ApplyPredicate(tbl3, pred2);

                            Reduce(selectExpression, pjeb, c => tbl3proj[c.Name]);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }
                }

                TableExpression TakeExplicit(TableExpressionBase @base)
                {
                    if (@base is TableExpression tbl)
                        return tbl;
                    else if (@base is PredicateJoinExpressionBase join
                        && join.Table is TableExpression tbl2)
                        return tbl2;
                    return null;
                }

                for (int i = 0; i < selectExpression.Tables.Count; i++)
                {
                    var tbl = TakeExplicit(selectExpression.Tables[i]);
                    if (tbl == null) continue;
                    GoReduce(tbl, i + 1);
                }

                return selectExpression;
            }

            private Expression VisitShaped(ShapedQueryExpression shaped)
            {
                Visit(shaped.QueryExpression);
                //shaped.ShaperExpression = Visit(shaped.ShaperExpression);
                return shaped;
            }

            private Expression VisitSet(UnionExpression sets)
            {
                var s1 = VisitSelect(sets.Source1);
                var s2 = VisitSelect(sets.Source2);
                sets = sets.Update(s1, s2);

                if (sets.IsDistinct
                    && sets.Source1.Tables.Count == 1
                    && sets.Source2.Tables.Count == 1)
                {
                    var t1 = sets.Source1.Tables.Single();
                    var t2 = sets.Source2.Tables.Single();
                    if (t1 is TableExpression tt1
                        && t2 is TableExpression tt2
                        && tt1.Name == tt2.Name)
                    {
                        var slt = sets.Source1;
                        var changer = Change(tt1);
                        var visitor = new ColumnJoinsReduceVisitor(c => c.Table == tt2 ? changer(c) : c);
                        var pred2 = (SqlExpression)visitor.Visit(sets.Source2.Predicate);
                        var pred = Merge(ExpressionType.OrElse, sets.Source1.Predicate, pred2);
                        RelationalInternals.ApplyPredicate(slt, pred);
                        return slt;
                    }
                }

                return sets;
            }

            protected override Expression VisitExtension(Expression node)
            {
                return node switch
                {
                    SelectExpression e => VisitSelect(e),
                    ShapedQueryExpression e => VisitShaped(e),
                    UnionExpression e => VisitSet(e),
                    _ => node,
                };
            }
        }
    }
}
