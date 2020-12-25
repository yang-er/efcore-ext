using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Microsoft.EntityFrameworkCore.Bulk
{
    public class TableInfoBase
    {
        /// <summary>
        /// Schema info (e.g. <c>dbo</c>)
        /// </summary>
        public string Schema { get; }

        /// <summary>
        /// Schema info (e.g. <c>[dbo].</c>)
        /// </summary>
        public string SchemaFormated => Schema != null ? $"[{Schema}]." : "";

        /// <summary>
        /// Table name (e.g. <c>AspNetUsers</c>)
        /// </summary>
        public string TableName { get; }

        /// <summary>
        /// Table name (e.g. <c>[dbo].[AspNetUsers]</c>)
        /// </summary>
        public string FullTableName => $"{SchemaFormated}[{TableName}]";

        /// <summary>
        /// Names for the primary keys
        /// </summary>
        public IReadOnlyList<string> PrimaryKeys { get; }

        /// <summary>
        /// The name of identity column
        /// </summary>
        public string IdentityColumnName { get; }

        /// <summary>
        /// Whether this table has an identity column
        /// </summary>
        public bool HasIdentity => IdentityColumnName != null;

        /// <summary>
        /// Whether this table has owned types
        /// </summary>
        public bool HasOwnedTypes => OwnedTypes.Any();

        /// <summary>
        /// Owned types from this entity type
        /// </summary>
        public IReadOnlyDictionary<string, INavigation> OwnedTypes { get; }

        /// <summary>
        /// The name of timestamp column
        /// </summary>
        public string TimeStampColumnName { get; set; }

        /// <summary>
        /// Navigations from properties to column names
        /// </summary>
        public IReadOnlyDictionary<string, string> ColumnNavigation { get; }

        /// <summary>
        /// Shadow properties
        /// </summary>
        public ISet<string> ShadowProperties { get; }

        /// <summary>
        /// Property converting
        /// </summary>
        public IReadOnlyDictionary<string, ValueConverter> ConvertibleProperties { get; }


        public TableInfoBase([NotNull] IEntityType entityType)
        {
            Schema = entityType.GetSchema() ?? "dbo";
            TableName = entityType.GetTableName();

            var pkeys = entityType.FindPrimaryKey();
            var allProps = entityType.GetProperties();
            PrimaryKeys = pkeys.Properties.Select(a => a.Name).ToList();

            var ownedTypes = entityType.GetNavigations()
                .Where(a => a.GetTargetType().IsOwned());
            OwnedTypes = ownedTypes.ToDictionary(a => a.Name, a => a);

            IdentityColumnName = pkeys.Properties
                .Where(t => t.GetValueGenerationStrategy() == SqlServerValueGenerationStrategy.IdentityColumn)
                .SingleOrDefault()?.Name;

            var timeStampProp = allProps.Where(a => 
                (a.IsConcurrencyToken && a.ValueGenerated == ValueGenerated.OnAddOrUpdate)
                || a.GetColumnType() == "timestamp");
            TimeStampColumnName = timeStampProp
                .SingleOrDefault()?
                .GetColumnName();

            var props = allProps.Except(timeStampProp)
                .Where(a => a.GetComputedColumnSql() == null);
            allProps = allProps.Except(timeStampProp).Concat(timeStampProp);

            ShadowProperties = props.Where(p => p.IsShadowProperty()).Select(p => p.GetColumnName()).ToHashSet();

            var normalProps =
                from p in allProps
                let conv = p.GetValueConverter()
                where conv != null
                select (p.GetColumnName(), conv);
            var ownerProps =
                from nav in ownedTypes
                let prop = nav.PropertyInfo
                from col in nav.GetTargetType().GetProperties()
                let conv = col.GetValueConverter()
                where conv != null
                select (col.GetColumnName(), conv);
            ConvertibleProperties = normalProps.Concat(ownerProps)
                .Distinct()
                .ToDictionary(k => k.Item1, v => v.Item2);

            var normalProps2 =
                from p in allProps
                select (p.Name, p.GetColumnName());
            var ownerProps2 =
                from nav in ownedTypes
                let prop = nav.PropertyInfo
                from col in nav.GetTargetType().GetProperties()
                where !col.IsPrimaryKey()
                select ($"{prop.Name}.{col.Name}", col.GetColumnName());
            ColumnNavigation = normalProps2.Concat(ownerProps2)
                .ToDictionary(k => k.Item1, v => v.Item2);
        }
    }
}
