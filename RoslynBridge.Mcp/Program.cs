using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RoslynBridge.Mcp.Configuration;
using RoslynBridge.Mcp.Services;

var builder = Host.CreateApplicationBuilder(args);

// Load configuration
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables("ROSLYN_MCP_");

// Configure options
builder.Services.Configure<RoslynBridgeOptions>(
    builder.Configuration.GetSection(RoslynBridgeOptions.SectionName));

// Configure logging to stderr (stdout is used for MCP communication)
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Configure HttpClient for WebAPI communication
var roslynOptions = builder.Configuration.GetSection(RoslynBridgeOptions.SectionName).Get<RoslynBridgeOptions>() ?? new RoslynBridgeOptions();

builder.Services.AddHttpClient<IRoslynWebApiClient, RoslynWebApiClient>(client =>
{
    client.BaseAddress = new Uri(roslynOptions.WebApiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(roslynOptions.TimeoutSeconds);
});

// Configure MCP server
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
