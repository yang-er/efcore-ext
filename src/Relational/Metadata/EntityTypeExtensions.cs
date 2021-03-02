using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.Metadata
{
    public static class BulkEntityTypeExtensions
    {
        public static Dictionary<string, string> GetColumns(this IEntityType entityType)
        {
            var store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table).Value;
            var result = new Dictionary<string, string>();

            void DiscoverOwnedEntity(IEntityType currentNavigated, string baseName)
            {
                foreach (var property in currentNavigated.GetProperties())
                {
                    var name = baseName + "." + property.Name;
                    result.Add(name.Trim('.'), property.GetColumnName(store));
                }

                foreach (var navigation in currentNavigated.GetNavigations())
                {
                    if (!navigation.ForeignKey.IsOwnership) continue;
                    var name = baseName + "." + navigation.Name;
                    DiscoverOwnedEntity(navigation.ForeignKey.DeclaringEntityType, name);
                }
            }

            DiscoverOwnedEntity(entityType, "");
            return result;
        }

        public static Dictionary<string, ValueConverter> GetValueConverters(this IEntityType entityType)
        {
            var store = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table).Value;
            var result = new Dictionary<string, ValueConverter>();

            void DiscoverOwnedEntity(IEntityType currentNavigated, string baseName)
            {
                foreach (var property in currentNavigated.GetProperties())
                {
                    var name = baseName + "." + property.Name;
                    var converter = property.GetValueConverter();
                    if (converter != null)
                    {
                        result.Add(name.Trim('.'), converter);
                    }
                }

                foreach (var navigation in currentNavigated.GetNavigations())
                {
                    if (!navigation.ForeignKey.IsOwnership) continue;
                    var name = baseName + "." + navigation.Name;
                    DiscoverOwnedEntity(navigation.ForeignKey.DeclaringEntityType, name);
                }
            }

            DiscoverOwnedEntity(entityType, "");
            return result;
        }
    }
}
