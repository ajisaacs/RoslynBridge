using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using RoslynBridge.Constants;
using RoslynBridge.Models;
using RoslynBridge.Services;
using Task = System.Threading.Tasks.Task;

namespace RoslynBridge.Server
{
    public class BridgeServer : IDisposable
    {
        private HttpListener? _listener;
        private readonly AsyncPackage _package;
        private readonly IRoslynQueryService _queryService;
        private bool _isRunning;
        private readonly int _port;

        public BridgeServer(AsyncPackage package, int port = ServerConstants.DefaultPort)
        {
            _package = package;
            _port = port;
            _queryService = new RoslynQueryService(package);
        }

        public async Task StartAsync()
        {
            if (_isRunning)
            {
                return;
            }

            try
            {
                await _queryService.InitializeAsync();

                _listener = new HttpListener();
                _listener.Prefixes.Add(ServerConstants.GetServerUrl(_port));
                _listener.Start();
                _isRunning = true;

                System.Diagnostics.Debug.WriteLine($"Roslyn Bridge HTTP server started on port {_port}");

                // Start listening for requests in the background
#pragma warning disable CS4014
                Task.Run(() => ListenForRequestsAsync());
#pragma warning restore CS4014
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start HTTP server: {ex}");
                throw;
            }
        }

        private async Task ListenForRequestsAsync()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
#pragma warning disable CS4014
                    Task.Run(() => HandleRequestAsync(context));
#pragma warning restore CS4014
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in request listener: {ex}");
                }
            }
        }

        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            var response = context.Response;
            ConfigureCorsHeaders(response);

            try
            {
                // Handle CORS preflight
                if (context.Request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                if (context.Request.HttpMethod != "POST")
                {
                    await RespondWithError(response, 405, "Only POST requests are supported");
                    return;
                }

                // Read and parse request
                var request = await ParseRequestAsync(context.Request);
                if (request == null)
                {
                    await RespondWithError(response, 400, "Invalid request format");
                    return;
                }

                // Route request
                var queryResponse = await RouteRequestAsync(context.Request.Url?.AbsolutePath ?? "/", request);
                response.StatusCode = queryResponse.Success ? 200 : 400;
                await WriteResponseAsync(response, queryResponse);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling request: {ex}");
                await RespondWithError(response, 500, ex.Message);
            }
        }

        private static void ConfigureCorsHeaders(HttpListenerResponse response)
        {
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
        }

        private static async Task<QueryRequest?> ParseRequestAsync(HttpListenerRequest request)
        {
            try
            {
                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var requestBody = await reader.ReadToEndAsync();

                return JsonSerializer.Deserialize<QueryRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch
            {
                return null;
            }
        }

        private async Task<QueryResponse> RouteRequestAsync(string path, QueryRequest request)
        {
            return path.ToLowerInvariant() switch
            {
                ServerConstants.QueryEndpoint => await _queryService.ExecuteQueryAsync(request),
                ServerConstants.HealthEndpoint => new QueryResponse
                {
                    Success = true,
                    Message = "Roslyn Bridge is running"
                },
                _ => new QueryResponse
                {
                    Success = false,
                    Error = $"Unknown endpoint: {path}"
                }
            };
        }

        private static async Task RespondWithError(HttpListenerResponse response, int statusCode, string errorMessage)
        {
            response.StatusCode = statusCode;
            await WriteResponseAsync(response, new QueryResponse
            {
                Success = false,
                Error = errorMessage
            });
        }

        private static async Task WriteResponseAsync(HttpListenerResponse response, QueryResponse queryResponse)
        {
            try
            {
                response.ContentType = ServerConstants.ContentTypeJson;
                var json = JsonSerializer.Serialize(queryResponse, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                });

                var buffer = Encoding.UTF8.GetBytes(json);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error writing response: {ex}");
            }
        }

        public void Dispose()
        {
            _isRunning = false;

            if (_listener != null)
            {
                try
                {
                    _listener.Stop();
                    _listener.Close();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error stopping HTTP server: {ex}");
                }
            }
        }
    }
}
