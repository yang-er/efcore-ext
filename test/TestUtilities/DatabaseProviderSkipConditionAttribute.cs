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

        public EFCoreVersion SkipVersion { get; set; }

        public DatabaseProviderSkipConditionAttribute(DatabaseProvider excludedProviders)
        {
            ExcludedProviders = excludedProviders;
        }

        private static DatabaseProvider GetCurrentProvider(ITestMethod method)
        {
            var dir = method.Method.Type.Assembly.Name;

            return dir.Contains("InMemory") ? DatabaseProvider.InMemory
                : dir.Contains("PostgreSql") ? DatabaseProvider.PostgreSQL
                : dir.Contains("SqlServer") ? DatabaseProvider.SqlServer
                : DatabaseProvider.None;
        }

        private static EFCoreVersion GetCurrentVersion(ITestMethod method)
        {
            var dir = method.Method.Type.Assembly.Name;

            return dir.Contains("3.1") ? EFCoreVersion.Version_3_1
                : dir.Contains("5.0") ? EFCoreVersion.Version_5_0
                : EFCoreVersion.None;
        }

        public string SkipReason => "Test cannot run on this provider.";

        public ValueTask<bool> IsMetAsync(XunitTestCase testcase)
        {
            return new ValueTask<bool>(
                (GetCurrentProvider(testcase.TestMethod) & ExcludedProviders) == DatabaseProvider.None
                && (GetCurrentVersion(testcase.TestMethod) & SkipVersion) == EFCoreVersion.None);
        }
    }
}
