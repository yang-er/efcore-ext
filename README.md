# EFCore.BulkExtensions

[![Build status](https://ci.appveyor.com/api/projects/status/8damo2nfqc2sbc2g?svg=true)](https://ci.appveyor.com/project/yang-er/efcore-bulkext)

Entity Framework Core extensions: Batch (**Delete, Update, Insert Into Select, Merge Into**).

Current version is EFCore 3.1 and Microsoft SQL Server (2008+).

Targeting `netcoreapp31` and used on .NET Core 3.1 projects.

## Setup in project

Configure this when creating an DbContext with DbContextOptionsBuilder.

Note that it can be used only with EFCore's internal service provider.

```csharp
options.UseSqlServer(connectionString).UseBulkExtensions();
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