using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.EntityFrameworkCore.TestUtilities.Xunit
{
    public class ContextualTestCaseRunner : XunitTestCaseRunner
    {
        public ContextualTestCaseRunner(
            IXunitTestCase testCase,
            string displayName,
            string skipReason,
            object[] constructorArguments,
            object[] testMethodArguments,
            IMessageBus messageBus,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
            : base(
                  testCase,
                  displayName,
                  skipReason,
                  constructorArguments,
                  testMethodArguments,
                  messageBus,
                  aggregator,
                  cancellationTokenSource)
        {
        }

        protected override XunitTestRunner CreateTestRunner(
            ITest test,
            IMessageBus messageBus,
            Type testClass,
            object[] constructorArguments,
            MethodInfo testMethod,
            object[] testMethodArguments,
            string skipReason,
            IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
        {
            return new ContextualTestRunner(
                test,
                messageBus,
                testClass,
                constructorArguments,
                testMethod,
                testMethodArguments,
                skipReason,
                beforeAfterAttributes,
                new ExceptionAggregator(aggregator),
                cancellationTokenSource);
        }

        private class ContextualTestRunner : XunitTestRunner
        {
            public ContextualTestRunner(
                ITest test,
                IMessageBus messageBus,
                Type testClass,
                object[] constructorArguments,
                MethodInfo testMethod,
                object[] testMethodArguments,
                string skipReason,
                IReadOnlyList<BeforeAfterTestAttribute> beforeAfterAttributes,
                ExceptionAggregator aggregator,
                CancellationTokenSource cancellationTokenSource)
                : base(
                      test,
                      messageBus,
                      testClass,
                      constructorArguments,
                      testMethod,
                      testMethodArguments,
                      skipReason,
                      beforeAfterAttributes,
                      aggregator,
                      cancellationTokenSource)
            {
            }

            protected override async Task<Tuple<decimal, string>> InvokeTestAsync(
                ExceptionAggregator aggregator)
            {
                string output = string.Empty;
                TestOutputHelper testOutputHelper = new TestOutputHelper();
                testOutputHelper.Initialize(MessageBus, Test);
                Output.SetInstance(testOutputHelper);

                decimal item = await InvokeTestMethodAsync(aggregator);

                if (testOutputHelper != null)
                {
                    output = testOutputHelper.Output;
                    Output.SetInstance(null);
                    testOutputHelper.Uninitialize();
                }

                return Tuple.Create(item, output.Trim());
            }
        }
    }
}
