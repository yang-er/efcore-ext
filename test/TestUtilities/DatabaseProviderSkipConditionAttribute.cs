using System;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.TestUtilities.Xunit
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    public sealed class DatabaseProviderSkipConditionAttribute : Attribute, ITestCondition
    {
        private readonly DatabaseProvider _excludedProviders;

        public DatabaseProviderSkipConditionAttribute(DatabaseProvider excludedProviders)
        {
            _excludedProviders = excludedProviders;
        }

        public string SkipReason => "Test cannot run on this provider.";

        public ValueTask<bool> IsMetAsync()
        {
            throw new NotImplementedException();
        }
    }
}
