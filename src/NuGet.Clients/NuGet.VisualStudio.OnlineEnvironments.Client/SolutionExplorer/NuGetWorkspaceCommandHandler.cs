// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.RpcContracts.OpenDocument;
using Microsoft.VisualStudio.Shell.ServiceBroker;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Workspace.VSIntegration.UI;

namespace NuGet.VisualStudio.OnlineEnvironments.Client
{
    /// <summary>
    /// Extends the Solution Explorer in cloud-connected scenarios.
    /// </summary>
    internal class NuGetWorkspaceCommandHandler : IWorkspaceCommandHandler
    {
        private readonly JoinableTaskContext _taskContext;
        private readonly IServiceProvider _serviceProvider;

        public NuGetWorkspaceCommandHandler(JoinableTaskContext taskContext, IServiceProvider serviceProvider)
        {
            _taskContext = taskContext;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// The command handlers priority. If there are multiple handlers for a given node
        /// then they are called in order of decreasing priority.
        /// </summary>
        public int Priority => 2000;

        /// <summary>
        /// Whether or not this handler should be ignored when multiple nodes are selected.
        /// </summary>
        public bool IgnoreOnMultiselect => true;

        public int Exec(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            if (pguidCmdGroup == CommandGroup.NuGetOnlineEnvironmentsClientProjectCommandSetGuid)
            {
                var nCmdIDInt = (int)nCmdID;

                if (IsSolutionOnlySelection(selection))
                {
                    switch (nCmdIDInt)
                    {
                        case PkgCmdIDList.CmdidRestorePackages:
                            OpenFile(selection.SingleOrDefault());

                            return 0;
                    }
                }
            }
            return 1;
        }

        public bool QueryStatus(List<WorkspaceVisualNodeBase> selection, Guid pguidCmdGroup, uint nCmdID, ref uint cmdf, ref string customTitle)
        {
            bool handled = false;

            if (pguidCmdGroup == CommandGroup.NuGetOnlineEnvironmentsClientProjectCommandSetGuid)
            {
                var nCmdIDInt = (int)nCmdID;

                if (IsSolutionOnlySelection(selection))
                {
                    switch (nCmdIDInt)
                    {
                        case PkgCmdIDList.CmdidRestorePackages:
                            cmdf = (uint)(Microsoft.VisualStudio.OLE.Interop.OLECMDF.OLECMDF_ENABLED | Microsoft.VisualStudio.OLE.Interop.OLECMDF.OLECMDF_SUPPORTED);
                            handled = true;
                            break;
                    }
                }
            }

            return handled;
        }

        private static bool IsSolutionOnlySelection(List<WorkspaceVisualNodeBase> selection)
        {
            return selection.Count().Equals(1) && selection.First().NodeMoniker.Equals(string.Empty);
        }
        /// <summary>
        /// Handles opening the file associated with the given <paramref name="node"/>.
        /// </summary>
        /// <param name="node"></param>
        private void OpenFile(WorkspaceVisualNodeBase node)
        {
            if (node == null
                || string.IsNullOrEmpty(node.NodeMoniker))
            {
                return;
            }

            _taskContext.Factory.RunAsync(async () =>
            {
                var serviceContainer = _serviceProvider.GetService<SVsBrokeredServiceContainer, IBrokeredServiceContainer>();
                var serviceBroker = serviceContainer.GetFullAccessServiceBroker();

                var openDocumentService = await serviceBroker.GetProxyAsync<IOpenDocumentService>(VisualStudioServices.VS2019_4.OpenDocumentService);

                try
                {
                    await openDocumentService.OpenDocumentAsync(node.NodeMoniker, cancellationToken: default);
                }
                finally
                {
                    (openDocumentService as IDisposable)?.Dispose();
                }
            });
        }
    }
}
