#nullable enable
using System.Threading.Tasks;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;

namespace RoslynBridge.Services
{
    public class WorkspaceProvider : IWorkspaceProvider
    {
        private readonly AsyncPackage _package;
        private VisualStudioWorkspace? _workspace;

        public VisualStudioWorkspace? Workspace => _workspace;

        public WorkspaceProvider(AsyncPackage package)
        {
            _package = package;
        }

        public async Task InitializeAsync()
        {
            await _package.JoinableTaskFactory.SwitchToMainThreadAsync();

            // Get the workspace through MEF
            var componentModel = await _package.GetServiceAsync(typeof(SComponentModel)) as IComponentModel;
            if (componentModel != null)
            {
                _workspace = componentModel.GetService<VisualStudioWorkspace>();
            }
        }
    }
}
