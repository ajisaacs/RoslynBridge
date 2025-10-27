using System.Diagnostics;
using System.Text;
using System.Text.Json;
using RoslynBridge.WebApi.Models;
using RoslynBridge.WebApi.Services;

namespace RoslynBridge.WebApi.Middleware;

/// <summary>
/// Middleware to capture and log all Roslyn API requests and responses
/// </summary>
public class HistoryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<HistoryMiddleware> _logger;

    public HistoryMiddleware(RequestDelegate next, ILogger<HistoryMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IHistoryService historyService)
    {
        // Only track Roslyn API endpoints
        if (!context.Request.Path.StartsWithSegments("/api/roslyn"))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var historyEntry = new QueryHistoryEntry
        {
            Timestamp = DateTime.UtcNow,
            Path = context.Request.Path,
            Method = context.Request.Method,
            ClientIp = context.Connection.RemoteIpAddress?.ToString()
        };

        // Capture request
        RoslynQueryRequest? request = null;
        if (context.Request.Method == "POST" && context.Request.ContentLength > 0)
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;

            try
            {
                request = JsonSerializer.Deserialize<RoslynQueryRequest>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                historyEntry.Request = request;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize request for history");
            }
        }

        // Capture response
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            stopwatch.Stop();
            historyEntry.DurationMs = stopwatch.ElapsedMilliseconds;
            historyEntry.Success = context.Response.StatusCode < 400;

            // Read response
            responseBody.Seek(0, SeekOrigin.Begin);
            var responseText = await new StreamReader(responseBody).ReadToEndAsync();
            responseBody.Seek(0, SeekOrigin.Begin);

            try
            {
                var response = JsonSerializer.Deserialize<RoslynQueryResponse>(responseText, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                historyEntry.Response = response;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize response for history");
            }

            // Copy response back
            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            historyEntry.DurationMs = stopwatch.ElapsedMilliseconds;
            historyEntry.Success = false;
            _logger.LogError(ex, "Error in request processing");
            throw;
        }
        finally
        {
            context.Response.Body = originalBodyStream;

            // Add to history
            try
            {
                historyService.Add(historyEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add entry to history");
            }
        }
    }
}

/// <summary>
/// Extension methods for adding history middleware
/// </summary>
public static class HistoryMiddlewareExtensions
{
    public static IApplicationBuilder UseHistoryTracking(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<HistoryMiddleware>();
    }
}
