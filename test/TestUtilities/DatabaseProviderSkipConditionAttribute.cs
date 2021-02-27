using System;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.EntityFrameworkCore.TestUtilities.Xunit
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
    public sealed class DatabaseProviderSkipConditionAttribute : Attribute, ITestCondition
    {
        public DatabaseProvider ExcludedProviders { get; }

        public DatabaseProviderSkipConditionAttribute(DatabaseProvider excludedProviders)
        {
            ExcludedProviders = excludedProviders;
        }

        private static DatabaseProvider GetCurrentProvider(ITestMethod method)
        {
            var dir = method.Method.Type.Assembly.Name;

            var dp = dir.Contains("InMemory") ? DatabaseProvider.InMemory
                : dir.Contains("PostgreSql") ? DatabaseProvider.PostgreSQL
                : dir.Contains("SqlServer") ? DatabaseProvider.SqlServer
                : DatabaseProvider.None;

            var ver = dir.Contains("3.1") ? DatabaseProvider.Version_31
                : dir.Contains("5.0") ? DatabaseProvider.Version_50
                : DatabaseProvider.None;

            return dp & ver;
        }

        public string SkipReason => "Test cannot run on this provider.";

        public ValueTask<bool> IsMetAsync(XunitTestCase testcase)
        {
            return new ValueTask<bool>(
                (GetCurrentProvider(testcase.TestMethod) & ExcludedProviders) == DatabaseProvider.None);
        }
    }
}
