#if EFCORE31
#pragma warning disable IDE0060

namespace Microsoft.EntityFrameworkCore.Metadata
{
    internal enum StoreObjectType
    {
        Table,
    }

    internal readonly struct StoreObjectIdentifier
    {
        public static StoreObjectIdentifier? Create(IEntityType _, StoreObjectType __)
        {
            return new StoreObjectIdentifier();
        }
    }

    internal static class StoreObjectIdentifierCompatibility
    {
        public static string GetColumnName(this IProperty property, in StoreObjectIdentifier _)
        {
            return property.GetColumnName();
        }
    }
}

#pragma warning restore IDE0060
#endif