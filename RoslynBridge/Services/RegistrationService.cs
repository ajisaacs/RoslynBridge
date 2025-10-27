using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;

namespace RoslynBridge.Services
{
    /// <summary>
    /// Service for registering this VS instance with the WebAPI
    /// </summary>
    public class RegistrationService : IDisposable
    {
        private readonly string _webApiUrl;
        private readonly HttpClient _httpClient;
        private readonly int _port;
        private readonly AsyncPackage _package;
        private System.Threading.Timer? _heartbeatTimer;
        private bool _isRegistered;

        public RegistrationService(AsyncPackage package, int port)
        {
            _package = package;
            _port = port;
            _webApiUrl = ConfigurationService.Instance.WebApiUrl;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        }

        public async Task RegisterAsync()
        {
            try
            {
                var dte = await GetDTEAsync();
                var solutionPath = dte?.Solution?.FullName;
                var solutionName = string.IsNullOrEmpty(solutionPath)
                    ? null
                    : System.IO.Path.GetFileNameWithoutExtension(solutionPath);

                var registrationData = new
                {
                    port = _port,
                    processId = Process.GetCurrentProcess().Id,
                    solutionPath = string.IsNullOrEmpty(solutionPath) ? null : solutionPath,
                    solutionName = solutionName,
                    projects = new string[] { } // TODO: Get project names
                };

                var json = JsonSerializer.Serialize(registrationData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_webApiUrl}/api/instances/register", content);

                if (response.IsSuccessStatusCode)
                {
                    _isRegistered = true;
                    Debug.WriteLine($"Successfully registered with WebAPI at {_webApiUrl}");

                    // Start heartbeat timer (configurable interval)
                    var heartbeatInterval = TimeSpan.FromSeconds(ConfigurationService.Instance.HeartbeatIntervalSeconds);
                    _heartbeatTimer = new System.Threading.Timer(
                        async _ => await SendHeartbeatAsync(),
                        null,
                        heartbeatInterval,
                        heartbeatInterval);
                }
                else
                {
                    Debug.WriteLine($"Failed to register with WebAPI: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering with WebAPI: {ex.Message}");
                // Don't throw - registration is optional, VS extension should work standalone
            }
        }

        private async Task SendHeartbeatAsync()
        {
            if (!_isRegistered)
                return;

            try
            {
                var processId = Process.GetCurrentProcess().Id;
                var response = await _httpClient.PostAsync(
                    $"{_webApiUrl}/api/instances/heartbeat/{processId}",
                    null);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Heartbeat failed: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending heartbeat: {ex.Message}");
            }
        }

        public async Task UnregisterAsync()
        {
            if (!_isRegistered)
                return;

            try
            {
                var processId = Process.GetCurrentProcess().Id;
                var response = await _httpClient.PostAsync(
                    $"{_webApiUrl}/api/instances/unregister/{processId}",
                    null);

                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine("Successfully unregistered from WebAPI");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unregistering from WebAPI: {ex.Message}");
            }
        }

        private async Task<EnvDTE.DTE?> GetDTEAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            return await _package.GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
        }

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
            _httpClient?.Dispose();
        }
    }
}
