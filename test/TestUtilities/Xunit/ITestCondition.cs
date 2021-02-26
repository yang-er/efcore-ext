// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Xunit.Sdk;

namespace Microsoft.EntityFrameworkCore.TestUtilities.Xunit
{
    public interface ITestCondition
    {
        ValueTask<bool> IsMetAsync(XunitTestCase testcase);

        string SkipReason { get; }
    }
}
