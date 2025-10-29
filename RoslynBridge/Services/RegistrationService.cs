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

                var projects = await GetProjectNamesAsync();

                var registrationData = new
                {
                    port = _port,
                    processId = Process.GetCurrentProcess().Id,
                    solutionPath = string.IsNullOrEmpty(solutionPath) ? null : solutionPath,
                    solutionName = solutionName,
                    projects = projects
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
                // Include current solution info in heartbeat to keep it up-to-date
                var dte = await GetDTEAsync();
                var solutionPath = dte?.Solution?.FullName;
                var solutionName = string.IsNullOrEmpty(solutionPath)
                    ? null
                    : System.IO.Path.GetFileNameWithoutExtension(solutionPath);

                var projects = await GetProjectNamesAsync();

                var heartbeatData = new
                {
                    port = _port,
                    processId = Process.GetCurrentProcess().Id,
                    solutionPath = string.IsNullOrEmpty(solutionPath) ? null : solutionPath,
                    solutionName = solutionName,
                    projects = projects
                };

                var json = JsonSerializer.Serialize(heartbeatData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_webApiUrl}/api/instances/heartbeat",
                    content);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"Heartbeat failed: {response.StatusCode}");

                    // If heartbeat fails (e.g., service restarted), re-register
                    if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        Debug.WriteLine("Instance not found, re-registering...");
                        _isRegistered = false;
                        await RegisterAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error sending heartbeat: {ex.Message}");
                // Try to re-register if connection fails
                _isRegistered = false;
                try
                {
                    await RegisterAsync();
                }
                catch
                {
                    // Silently fail - will retry on next heartbeat
                }
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

        private async Task<string[]> GetProjectNamesAsync()
        {
            try
            {
                var dte = await GetDTEAsync();
                if (dte?.Solution?.Projects == null)
                    return Array.Empty<string>();

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                var projectNames = new System.Collections.Generic.List<string>();

                foreach (EnvDTE.Project project in dte.Solution.Projects)
                {
                    try
                    {
                        // Skip solution folders and other non-project items
                        if (project.Kind == EnvDTE.Constants.vsProjectKindSolutionItems ||
                            project.Kind == EnvDTE.Constants.vsProjectKindMisc)
                        {
                            // Solution folders can contain nested projects
                            if (project.ProjectItems != null)
                            {
                                foreach (EnvDTE.ProjectItem item in project.ProjectItems)
                                {
                                    if (item.SubProject != null && !string.IsNullOrEmpty(item.SubProject.Name))
                                    {
                                        projectNames.Add(item.SubProject.Name);
                                    }
                                }
                            }
                            continue;
                        }

                        if (!string.IsNullOrEmpty(project.Name))
                        {
                            projectNames.Add(project.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Some projects might throw exceptions when accessing properties
                        Debug.WriteLine($"Error reading project: {ex.Message}");
                    }
                }

                return projectNames.ToArray();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error getting project names: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        public void Dispose()
        {
            _heartbeatTimer?.Dispose();
            _httpClient?.Dispose();
        }
    }
}
