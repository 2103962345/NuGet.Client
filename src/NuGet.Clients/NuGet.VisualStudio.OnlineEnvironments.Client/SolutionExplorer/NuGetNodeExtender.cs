// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;

namespace NuGet.VisualStudio.OnlineEnvironments.Client
{
    /// <summary>
    /// Extends the Solution Explorer in cloud-connected scenarios by adding command handlers
    /// </summary>
    [ExportNodeExtender(CloudEnvironment.LiveShareSolutionView)]
    internal sealed class NuGetNodeExtender : INodeExtender
    {
        /// <summary>
        /// The shared command handler for all nodes NuGet cares about.
        /// </summary>
        private readonly IWorkspaceCommandHandler _commandHandler;

        [ImportingConstructor]
        public NuGetNodeExtender(
            JoinableTaskContext taskContext,
            [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider)
        {
            _commandHandler = new NuGetWorkspaceCommandHandler(taskContext, serviceProvider);
        }

        public IChildrenSource ProvideChildren(WorkspaceVisualNodeBase parentNode)
        {
            return null;
        }

        /// <summary>
        /// Provides our <see cref="IWorkspaceCommandHandler"/> for nodes representing
        /// managed projects.
        /// </summary>
        public IWorkspaceCommandHandler ProvideCommandHandler(WorkspaceVisualNodeBase parentNode)
        {
            return _commandHandler;
        }
    }
}
