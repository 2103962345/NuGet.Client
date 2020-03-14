// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.VisualStudio.OnlineEnvironments.Client
{
    /// <summary>
    /// Specifies the command groups handled in this project.
    /// </summary>
    internal static class CommandGroup
    {
        public const string NuGetOnlineEnvironmentsClientProjectCommandSet = "{760B5FEF-9F63-4788-A9FC-6B0186690DD1}";

        public static readonly Guid NuGetOnlineEnvironmentsClientProjectCommandSetGuid = Guid.Parse(NuGetOnlineEnvironmentsClientProjectCommandSet);
    }
}
