# EFCore.BulkExtensions

[![Build status](https://ci.appveyor.com/api/projects/status/8damo2nfqc2sbc2g?svg=true)](https://ci.appveyor.com/project/yang-er/efcore-bulkext)

Entity Framework Core extensions: Batch (**Delete, Update, Insert Into Select, Merge Into**).

Current version supports EFCore 3.1 and EFCore 5.0.

Targeting `netcoreapp3.1` and used on .NET Core 3.1 projects.
Targeting `net5.0` and used on .NET 5.0 projects.

- [![](https://img.shields.io/endpoint?url=https%3A%2F%2Fnuget.xylab.fun%2Fv3%2Fpackage%2FMicrosoft.EntityFrameworkCore.Bulk%2Fshields-io.json)](https://nuget.xylab.fun/packages/Microsoft.EntityFrameworkCore.Bulk): EFCore Bulk extension definition
- [![](https://img.shields.io/endpoint?url=https%3A%2F%2Fnuget.xylab.fun%2Fv3%2Fpackage%2FMicrosoft.EntityFrameworkCore.Bulk.InMemory%2Fshields-io.json)](https://nuget.xylab.fun/packages/Microsoft.EntityFrameworkCore.Bulk.InMemory): InMemory bulk operation provider
- [![](https://img.shields.io/endpoint?url=https%3A%2F%2Fnuget.xylab.fun%2Fv3%2Fpackage%2FMicrosoft.EntityFrameworkCore.Bulk.Relational%2Fshields-io.json)](https://nuget.xylab.fun/packages/Microsoft.EntityFrameworkCore.Bulk.Relational): Basis of Relational providers
- [![](https://img.shields.io/endpoint?url=https%3A%2F%2Fnuget.xylab.fun%2Fv3%2Fpackage%2FMicrosoft.EntityFrameworkCore.Bulk.SqlServer%2Fshields-io.json)](https://nuget.xylab.fun/packages/Microsoft.EntityFrameworkCore.Bulk.SqlServer): SqlServer bulk operation provider

When you want to split EFCore definition and database type, you may reference to `Microsoft.EntityFrameworkCore.Bulk` in your storage implementation project, and reference to `Microsoft.EntityFrameworkCore.Bulk.SqlServer` in your host startup project.

## Setup in project

Configure this when creating an `DbContext` with `DbContextOptionsBuilder`.

```csharp
options.UseSqlServer(connectionString, b => b.UseBulk());
options.UseInMemoryDatabase(databaseName, o => o.UseBulk());
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

## Bulk\*\*\*\*

It is removed now. It takes time to clear up the logics.

## Cache

```csharp
options.UseCahce();

await context.Items.CountAsync("tag", TimeSpan.FromSeconds(10));
```
