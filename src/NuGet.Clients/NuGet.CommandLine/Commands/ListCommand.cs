// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace NuGet.CommandLine
{
    [Command(
        typeof(NuGetCommand),
        "list",
        "ListCommandDescription",
        UsageSummaryResourceName = "ListCommandUsageSummary",
        UsageDescriptionResourceName = "ListCommandUsageDescription",
        UsageExampleResourceName = "ListCommandUsageExamples")]
    [DeprecatedCommand(typeof(SearchCommand))]
    [Obsolete(message: "Use SearchCommand class. This class will disappear in upcoming releases", error: false)]
    public class ListCommand : SearchCommand
    {
        public async override Task ExecuteCommandAsync()
        {
            await base.ExecuteCommandAsync();
        }
    }
}
