using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using RoslynBridge.Server;
using Task = System.Threading.Tasks.Task;

namespace RoslynBridge
{
    /// <summary>
    /// Visual Studio package that provides Roslyn API access via HTTP bridge
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(RoslynBridgePackage.PackageGuidString)]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class RoslynBridgePackage : AsyncPackage
    {
        public const string PackageGuidString = "b2c3d4e5-f6a7-4b5c-9d8e-0f1a2b3c4d5e";
        private BridgeServer? _bridgeServer;
        private Services.RegistrationService? _registrationService;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            try
            {
                // Initialize the HTTP bridge server
                _bridgeServer = new BridgeServer(this);
                await _bridgeServer.StartAsync();

                // Register with WebAPI
                _registrationService = new Services.RegistrationService(this, _bridgeServer.Port);
                await _registrationService.RegisterAsync();

                await base.InitializeAsync(cancellationToken, progress);
            }
            catch (Exception ex)
            {
                // Log error - you might want to add proper logging here
                System.Diagnostics.Debug.WriteLine($"Failed to initialize Roslyn Bridge: {ex}");
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _registrationService?.UnregisterAsync().Wait(TimeSpan.FromSeconds(5));
                _registrationService?.Dispose();
                _bridgeServer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
