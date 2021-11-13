using Microsoft.EntityFrameworkCore.Utilities;
using System;
using System.Data.Common;

namespace Microsoft.EntityFrameworkCore.Storage.Internal
{
    public class ValuesRelationalParameter : RelationalParameterBase
    {
        public virtual AnonymousExpressionType Type { get; }

        public virtual string NamePrefix { get; }

        public override string InvariantName { get; }

        public ValuesRelationalParameter(AnonymousExpressionType type, string namePrefix, string invariantName)
#if EFCORE60
            : base(invariantName)
#endif
        {
            Check.NotNull(type, nameof(type));
            Check.NotNull(namePrefix, nameof(namePrefix));
            Check.NotNull(invariantName, nameof(invariantName));

            Type = type;
            NamePrefix = namePrefix;
            InvariantName = invariantName;
        }

        public override void AddDbParameter(DbCommand command, object value)
        {
            Check.NotNull(command, nameof(command));
            Check.NotNull(value, nameof(value));

            if (value is not System.Collections.IList list)
            {
                throw new InvalidOperationException("Parameter corrupt.");
            }

            for (int i = 0; i < list.Count; i++)
            {
                Type.AddDbParameter(command, $"{NamePrefix}_{i}", list[i]);
            }
        }
    }
}
