// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Microsoft.EntityFrameworkCore.TestUtilities.Xunit
{
    public sealed class ConditionalFactTestCase : XunitTestCase
    {
        [Obsolete("Called by the de-serializer; should only be called by deriving classes for de-serialization purposes")]
        public ConditionalFactTestCase()
        {
        }

        public ConditionalFactTestCase(
            IMessageSink diagnosticMessageSink,
            TestMethodDisplay defaultMethodDisplay,
            TestMethodDisplayOptions defaultMethodDisplayOptions,
            ITestMethod testMethod,
            object[] testMethodArguments = null)
            : base(
                  diagnosticMessageSink,
                  defaultMethodDisplay,
                  defaultMethodDisplayOptions,
                  testMethod,
                  testMethodArguments)
        {
        }

        public override async Task<RunSummary> RunAsync(
            IMessageSink diagnosticMessageSink,
            IMessageBus messageBus,
            object[] constructorArguments,
            ExceptionAggregator aggregator,
            CancellationTokenSource cancellationTokenSource)
        {
            if (await XunitTestCaseExtensions.TrySkipAsync(this, messageBus))
            {
                return new RunSummary { Total = 1, Skipped = 1 };
            }
            else
            {
                var runner = new ContextualTestCaseRunner(
                    this,
                    DisplayName,
                    SkipReason,
                    constructorArguments,
                    TestMethodArguments,
                    messageBus,
                    aggregator,
                    cancellationTokenSource);

                return await runner.RunAsync();
            }
        }
    }
}
