using System;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

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

        private static DatabaseProvider GetCurrentProvider(ITestMethod method)
        {
            var dir = method.Method.Type.Assembly.Name;

            return dir.Contains("InMemory") ? DatabaseProvider.InMemory
                : dir.Contains("PostgreSql") ? DatabaseProvider.PostgreSQL
                : dir.Contains("SqlServer") ? DatabaseProvider.SqlServer
                : DatabaseProvider.None;
        }

        public string SkipReason => "Test cannot run on this provider.";

        public ValueTask<bool> IsMetAsync(XunitTestCase testcase)
        {
            return new ValueTask<bool>(
                (GetCurrentProvider(testcase.TestMethod) & _excludedProviders)
                    == DatabaseProvider.None);
        }
    }
}
