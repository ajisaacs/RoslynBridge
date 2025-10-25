using Microsoft.VisualStudio.LanguageServices;

namespace RoslynBridge.Services
{
    public interface IWorkspaceProvider
    {
        VisualStudioWorkspace? Workspace { get; }
    }
}
