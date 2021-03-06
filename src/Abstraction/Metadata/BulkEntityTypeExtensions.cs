using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Microsoft.EntityFrameworkCore.Metadata
{
    internal static class BulkEntityTypeExtensions
    {
        public static bool TryGuessKey(this IEntityType entityType, IReadOnlyList<MemberBinding> bindings, out IKey key)
        {
            var assignments = new HashSet<MemberInfo>(bindings.OfType<MemberAssignment>().Select(a => a.Member));

            foreach (var ikey in entityType.GetKeys())
            {
                bool fulfill = true;
                foreach (var prop in ikey.Properties)
                {
                    var member = (MemberInfo)prop.PropertyInfo ?? prop.FieldInfo;
                    if (!assignments.Contains(member)) fulfill = false;
                    if (prop.ValueGenerated != ValueGenerated.Never) fulfill = false;
                }

                if (fulfill)
                {
                    key = ikey;
                    return true;
                }
            }

            key = null;
            return false;
        }
    }
}
