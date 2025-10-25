using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using RoslynBridge.Models;

namespace RoslynBridge.Services
{
    /// <summary>
    /// Service for safe project operations (NuGet, build, etc.)
    /// Only allows specific, whitelisted operations
    /// </summary>
    public class ProjectOperationsService
    {
        private const int DefaultTimeout = 120000; // 2 minutes
        private const int MaxTimeout = 600000; // 10 minutes
        private readonly IWorkspaceProvider _workspaceProvider;

        public ProjectOperationsService(IWorkspaceProvider workspaceProvider)
        {
            _workspaceProvider = workspaceProvider;
        }

        /// <summary>
        /// Add a NuGet package to a project
        /// </summary>
        public async Task<QueryResponse> AddNuGetPackageAsync(string projectName, string packageName, string? version = null)
        {
            var projectPath = GetProjectPath(projectName);
            if (projectPath == null)
            {
                return new QueryResponse
                {
                    Success = false,
                    Error = $"Project not found: {projectName}. Use 'getprojects' to see available projects."
                };
            }

            if (string.IsNullOrWhiteSpace(packageName))
            {
                return new QueryResponse
                {
                    Success = false,
                    Error = "Package name is required"
                };
            }

            var command = string.IsNullOrWhiteSpace(version)
                ? $"add \"{projectPath}\" package {packageName}"
                : $"add \"{projectPath}\" package {packageName} --version {version}";

            return await ExecuteDotNetCommandAsync(command, $"Add {packageName} to {projectName}");
        }

        /// <summary>
        /// Remove a NuGet package from a project
        /// </summary>
        public async Task<QueryResponse> RemoveNuGetPackageAsync(string projectName, string packageName)
        {
            var projectPath = GetProjectPath(projectName);
            if (projectPath == null)
            {
                return new QueryResponse
                {
                    Success = false,
                    Error = $"Project not found: {projectName}. Use 'getprojects' to see available projects."
                };
            }

            if (string.IsNullOrWhiteSpace(packageName))
            {
                return new QueryResponse
                {
                    Success = false,
                    Error = "Package name is required"
                };
            }

            var command = $"remove \"{projectPath}\" package {packageName}";
            return await ExecuteDotNetCommandAsync(command, $"Remove {packageName} from {projectName}");
        }

        /// <summary>
        /// Build a project or solution
        /// </summary>
        public async Task<QueryResponse> BuildAsync(string projectName, string? configuration = null)
        {
            var projectPath = GetProjectPath(projectName);
            if (projectPath == null)
            {
                return new QueryResponse
                {
                    Success = false,
                    Error = $"Project not found: {projectName}. Use 'getprojects' to see available projects."
                };
            }

            var command = string.IsNullOrWhiteSpace(configuration)
                ? $"build \"{projectPath}\""
                : $"build \"{projectPath}\" --configuration {configuration}";

            return await ExecuteDotNetCommandAsync(command, $"Build {projectName}");
        }

        /// <summary>
        /// Clean build output
        /// </summary>
        public async Task<QueryResponse> CleanAsync(string projectName)
        {
            var projectPath = GetProjectPath(projectName);
            if (projectPath == null)
            {
                return new QueryResponse
                {
                    Success = false,
                    Error = $"Project not found: {projectName}. Use 'getprojects' to see available projects."
                };
            }

            var command = $"clean \"{projectPath}\"";
            return await ExecuteDotNetCommandAsync(command, $"Clean {projectName}");
        }

        /// <summary>
        /// Restore NuGet packages
        /// </summary>
        public async Task<QueryResponse> RestoreAsync(string projectName)
        {
            var projectPath = GetProjectPath(projectName);
            if (projectPath == null)
            {
                return new QueryResponse
                {
                    Success = false,
                    Error = $"Project not found: {projectName}. Use 'getprojects' to see available projects."
                };
            }

            var command = $"restore \"{projectPath}\"";
            return await ExecuteDotNetCommandAsync(command, $"Restore {projectName}");
        }

        /// <summary>
        /// Create a new directory
        /// </summary>
        public Task<QueryResponse> CreateDirectoryAsync(string directoryPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    return Task.FromResult(new QueryResponse
                    {
                        Success = false,
                        Error = "Directory path is required"
                    });
                }

                bool existed = Directory.Exists(directoryPath);
                if (!existed)
                {
                    Directory.CreateDirectory(directoryPath);
                }

                return Task.FromResult(new QueryResponse
                {
                    Success = true,
                    Message = existed ? $"Directory already exists: {directoryPath}" : $"Successfully created directory: {directoryPath}",
                    Data = new
                    {
                        DirectoryPath = directoryPath,
                        AlreadyExisted = existed
                    }
                });
            }
            catch (Exception ex)
            {
                return Task.FromResult(new QueryResponse
                {
                    Success = false,
                    Error = $"Error creating directory: {ex.Message}"
                });
            }
        }

        private string? GetProjectPath(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
            {
                return null;
            }

            var workspace = _workspaceProvider.Workspace;
            if (workspace?.CurrentSolution == null)
            {
                return null;
            }

            // Find project by name
            var project = workspace.CurrentSolution.Projects
                .FirstOrDefault(p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

            return project?.FilePath;
        }

        private async Task<QueryResponse> ExecuteDotNetCommandAsync(string arguments, string operationName)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using var process = new Process { StartInfo = processStartInfo };

                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        outputBuilder.AppendLine(args.Data);
                    }
                };

                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        errorBuilder.AppendLine(args.Data);
                    }
                };

                var startTime = DateTime.Now;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool completed = await Task.Run(() => process.WaitForExit(MaxTimeout));

                if (!completed)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch { }

                    return new QueryResponse
                    {
                        Success = false,
                        Error = $"{operationName} timed out after {MaxTimeout}ms"
                    };
                }

                var endTime = DateTime.Now;
                var duration = (endTime - startTime).TotalMilliseconds;

                var stdout = outputBuilder.ToString();
                var stderr = errorBuilder.ToString();
                var exitCode = process.ExitCode;

                return new QueryResponse
                {
                    Success = exitCode == 0,
                    Message = exitCode == 0 ? $"{operationName} completed successfully" : $"{operationName} failed with exit code {exitCode}",
                    Data = new
                    {
                        Operation = operationName,
                        Command = $"dotnet {arguments}",
                        ExitCode = exitCode,
                        Output = stdout,
                        Error = stderr,
                        Duration = Math.Round(duration, 2)
                    }
                };
            }
            catch (Exception ex)
            {
                return new QueryResponse
                {
                    Success = false,
                    Error = $"Error executing {operationName}: {ex.Message}"
                };
            }
        }
    }
}
