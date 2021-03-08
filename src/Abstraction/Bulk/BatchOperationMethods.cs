using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    internal static class BatchOperationMethods
    {
        public static MethodInfo CreateCommonTable { get; }
            = new Func<IQueryable<object>,
                       List<object>,
                       IQueryable<object>>(
                BatchOperationExtensions.CreateCommonTable)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo BatchUpdateExpanded { get; }
            = new Func<IQueryable<object>,
                       Expression<Func<object, object>>,
                       int>(
                BatchOperationExtensions.BatchUpdate<object, object>)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo Merge { get; }
            = new Func<DbSet<object>,
                       IEnumerable<object>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object, object>>,
                       Expression<Func<object, object>>,
                       bool,
                       int>(
                BatchOperationExtensions.Merge)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo MergeCollapsed { get; }
            = new Func<IQueryable<object>,
                       IQueryable<object>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object, object>>,
                       Expression<Func<object, object>>,
                       bool,
                       int>(
                BatchOperationExtensions.Merge)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo BatchDelete { get; }
            = new Func<IQueryable<object>,
                       int>(
                BatchOperationExtensions.BatchDelete)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo BatchUpdate { get; }
            = new Func<IQueryable<object>,
                       Expression<Func<object, object>>,
                       int>(
                BatchOperationExtensions.BatchUpdate<object>)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo BatchUpdateJoin { get; }
            = new Func<IQueryable<object>,
                       IQueryable<object>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object, object>>,
                       Expression<Func<object, object, bool>>,
                       int>(
                BatchOperationExtensions.BatchUpdateJoin)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo BatchUpdateJoinQueryable { get; }
            = new Func<DbSet<object>,
                       IQueryable<object>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object, object>>,
                       Expression<Func<object, object, bool>>,
                       int>(
                BatchOperationExtensions.BatchUpdateJoin)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo BatchUpdateJoinReadOnlyList { get; }
            = new Func<DbSet<object>,
                       IReadOnlyList<object>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object, object>>,
                       Expression<Func<object, object, bool>>,
                       int>(
                BatchOperationExtensions.BatchUpdateJoin)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo BatchInsertInto { get; }
            = new Func<IQueryable<object>,
                       DbSet<object>,
                       int>(
                BatchOperationExtensions.BatchInsertInto)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo BatchInsertIntoCollapsed { get; }
            = new Func<IQueryable<object>,
                       int>(
                BatchOperationExtensions.BatchInsertInto)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo UpsertCollapsed { get; }
            = new Func<IQueryable<object>,
                       IEnumerable<object>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object, object>>,
                       int>(
                BatchOperationExtensions.Upsert)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo UpsertOneCollapsed { get; }
            = new Func<IQueryable<object>,
                       Expression<Func<object>>,
                       Expression<Func<object, object>>,
                       int>(
                BatchOperationExtensions.Upsert)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo Upsert { get; }
            = new Func<DbSet<object>,
                       IEnumerable<object>,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object, object>>,
                       int>(
                BatchOperationExtensions.Upsert)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        [Obsolete]
        public static MethodInfo UpsertOne { get; }
            = new Func<DbSet<object>,
                       object,
                       Expression<Func<object, object>>,
                       Expression<Func<object, object, object>>,
                       int>(
                BatchOperationExtensions.Upsert)
            .GetMethodInfo()
            .GetGenericMethodDefinition();


        public static MethodInfo UpsertOneNew { get; }
            = new Func<DbSet<object>,
                       Expression<Func<object>>,
                       Expression<Func<object, object>>,
                       int>(
                BatchOperationExtensions.Upsert)
            .GetMethodInfo()
            .GetGenericMethodDefinition();
    }
}
