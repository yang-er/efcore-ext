# EFCore.BulkExtensions

[![AppVeyor status](https://ci.appveyor.com/api/projects/status/8damo2nfqc2sbc2g?svg=true)](https://ci.appveyor.com/project/yang-er/efcore-bulkext) [![Travis CI status](https://travis-ci.com/yang-er/efcore-ext.svg?branch=dev&status=started)](https://travis-ci.com/github/yang-er/efcore-ext)

Entity Framework Core extensions: Batch (**Delete, Update, Insert Into Select, Merge Into, Upsert**).

Current version supports EFCore 3.1 and EFCore 5.0.

Targeting `netstandard2.0` and used on EFCore 3.1 projects.
Targeting `netstandard2.1` and used on EFCore 5.0 projects.

- [![](https://img.shields.io/endpoint?url=https%3A%2F%2Fnuget.xylab.fun%2Fv3%2Fpackage%2FMicrosoft.EntityFrameworkCore.Bulk%2Fshields-io.json)](https://nuget.xylab.fun/packages/Microsoft.EntityFrameworkCore.Bulk): EFCore Bulk extension definition
- [![](https://img.shields.io/endpoint?url=https%3A%2F%2Fnuget.xylab.fun%2Fv3%2Fpackage%2FMicrosoft.EntityFrameworkCore.Bulk.InMemory%2Fshields-io.json)](https://nuget.xylab.fun/packages/Microsoft.EntityFrameworkCore.Bulk.InMemory): InMemory bulk operation provider
- [![](https://img.shields.io/endpoint?url=https%3A%2F%2Fnuget.xylab.fun%2Fv3%2Fpackage%2FMicrosoft.EntityFrameworkCore.Bulk.Relational%2Fshields-io.json)](https://nuget.xylab.fun/packages/Microsoft.EntityFrameworkCore.Bulk.Relational): Basis of Relational providers
- [![](https://img.shields.io/endpoint?url=https%3A%2F%2Fnuget.xylab.fun%2Fv3%2Fpackage%2FMicrosoft.EntityFrameworkCore.Bulk.SqlServer%2Fshields-io.json)](https://nuget.xylab.fun/packages/Microsoft.EntityFrameworkCore.Bulk.SqlServer): SqlServer bulk operation provider
- [![](https://img.shields.io/endpoint?url=https%3A%2F%2Fnuget.xylab.fun%2Fv3%2Fpackage%2FMicrosoft.EntityFrameworkCore.Bulk.PostgreSql%2Fshields-io.json)](https://nuget.xylab.fun/packages/Microsoft.EntityFrameworkCore.Bulk.PostgreSql): Npgsql bulk operation provider
- [![](https://img.shields.io/endpoint?url=https%3A%2F%2Fnuget.xylab.fun%2Fv3%2Fpackage%2FMicrosoft.EntityFrameworkCore.Bulk.Sqlite%2Fshields-io.json)](https://nuget.xylab.fun/packages/Microsoft.EntityFrameworkCore.Bulk.Sqlite): Sqlite bulk operation provider

When you want to split EFCore definition and database type, you may reference to `Microsoft.EntityFrameworkCore.Bulk` in your storage implementation project, and reference to `Microsoft.EntityFrameworkCore.Bulk.SqlServer` in your host startup project.

## Setup in project

Configure this when creating an `DbContext` with `DbContextOptionsBuilder`.

```csharp
options.UseSqlServer(connectionString, b => b.UseBulk());
options.UseNpgsql(connectionString, s => s.UseBulk());
options.UseInMemoryDatabase(databaseName, o => o.UseBulk());
options.UseSqlite(connection, o => o.UseBulk());
```

If you want to try TableSplittingJoinsRemoval to remove useless self-joins, you may try

```csharp
options.UseTableSplittingJoinsRemoval();
```

## Delete

```csharp
affectedRows = context.Items
    .Where(a => something.Contains(a.Id))
    .BatchDelete();
```

## Update: Delta or SetValue

```csharp
affectedRows = context.Items
    .Where(a => a.ItemId <= 500)
    .BatchUpdate(a => new Item { Quantity = a.Quantity + 100 });

affectedRows = context.ItemAs.BatchUpdateJoin(
    inner: context.ItemBs,
    outerKeySelector: a => a.Id,
    innerKeySelector: b => b.Id,
    condition: (a, b) => a.Id == 1, // can be null
    updateSelector: (a, b) => new ItemA { Value = a.Value + b.Value - 3 });
```

## SelectInto: Repeat entities

```csharp
createdRows = context.Items
    .Where(a => a.ItemId <= 500)
    .Select(a => new OtherItem { ...other props except identity pkey... })
    .Top(100)
    .BatchInsertInto(context.OtherItems);
```

## Upsert: Update or insert

```csharp
targetSet.Upsert(
    sourceQuery,
    insertExpression: s => new Target { Key1 = s.Key1, Key2 = s.Key2, NormalProp = s.NormalProp },
    updateExpression: (existing, excluded) => new Target { ... });
```

The `sourceQuery` can be one of the following items:
- `IQueryable<TSource>`
- Local enumerable of `TSource`
- single anonymous object

Note that the conflict constraint is the primary key, so you should set all primary key fields in insert expression.

The two entities in update expression are both of type `TTarget`, where the existing means the previous row in the database, and the excluded means the item not inserted. You can also fill null with this field, which means `INSERT INTO IF NOT EXISTS`.

```csharp
targetSet.Upsert(
    insertExpression: () => new Target { ... },
    updateExpression: existing => new Target { ... });
```

For upserting only one entity, please consider the second usage.

## Merge: Upsert / Synchronize

```csharp
var newVals = new[] // note that should be distinct with keys!
{
    new { AId = 1, BId = 2, Something = "hello" },
    new { AId = 2, BId = 3, Something = "world" },
};

context.Items.Merge(
    sourceTable: newVals, // IEnumerable<f<>__AnonymousObject> or other IQueryable
    targetKey: item => new { item.AId, BId = item.TId },
    sourceKey: src => new { src.AId, src.BId },
    updateExpression: (item, src) => new Item { Description = item.Description + src.Something }, // can be null
    insertExpression: src => new Item { AId = src.AId, TId = src.BId, Description = src.Something }, // can be null, and ignore identity pkey
    delete: false); // when not matched by source, useful in sync
```

Note that when update/insert expressions are null, delete is true, it will become truncate.

This function is only available in InMemory and SqlServer providers, since PostgreSQL removed supports for SQL MERGE.

## Bulk\*\*\*\*

It is removed now. It takes time to clear up the logics.

## Cache

```csharp
options.UseCahce();

await context.Items.CachedCountAsync("tag", TimeSpan.FromSeconds(10));
```

## Developing this project

For release codes, please refer to branch **LKG**.

The branch **dev** may not work properly.
