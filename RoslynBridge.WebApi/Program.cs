using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi.Models;
using RoslynBridge.WebApi.Middleware;
using RoslynBridge.WebApi.Services;
using System.IO.Compression;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add Windows Service support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "RoslynBridge Web API";
});

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add controllers
builder.Services.AddControllers();

// Configure response compression for reduced bandwidth usage
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

// Register history service as singleton for in-memory storage
builder.Services.AddSingleton<IHistoryService, HistoryService>();

// Register instance registry service as singleton
builder.Services.AddSingleton<IInstanceRegistryService, InstanceRegistryService>();

// Register background service for cleaning up stale instances
builder.Services.AddHostedService<InstanceCleanupService>();

// Configure HttpClient for Roslyn Bridge
var roslynBridgeUrl = builder.Configuration.GetValue<string>("RoslynBridge:BaseUrl") ?? "http://localhost:59123";
builder.Services.AddHttpClient<IRoslynBridgeClient, RoslynBridgeClient>(client =>
{
    client.BaseAddress = new Uri(roslynBridgeUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Roslyn Bridge Web API",
        Version = "v1.0",
        Description = "Modern web API middleware for Roslyn Bridge - connecting Claude AI to Visual Studio code analysis",
        Contact = new OpenApiContact
        {
            Name = "Roslyn Bridge",
            Url = new Uri("https://github.com/yourusername/roslynbridge")
        }
    });

    // Include XML comments for better documentation
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Add operation tags
    options.TagActionsBy(api =>
    {
        if (api.GroupName != null)
        {
            return new[] { api.GroupName };
        }

        if (api.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor controllerActionDescriptor)
        {
            return new[] { controllerActionDescriptor.ControllerName };
        }

        return new[] { "Unknown" };
    });
});

// Add health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Enable response compression (should be early in pipeline)
app.UseResponseCompression();

// Enable Swagger in all environments for easy access
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Roslyn Bridge API v1");
    options.RoutePrefix = string.Empty; // Serve Swagger UI at root
    options.DocumentTitle = "Roslyn Bridge API";
    options.DisplayRequestDuration();
});

app.UseHttpsRedirection();

// Enable CORS
app.UseCors("AllowAll");

// Enable history tracking middleware (must be before authorization and controllers)
app.UseHistoryTracking();

app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map health check endpoint
app.MapHealthChecks("/health");

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Roslyn Bridge Web API started");
logger.LogInformation("Swagger UI available at: {Url}", app.Environment.IsDevelopment() ? "https://localhost:7001" : "/");
logger.LogInformation("Connected to Roslyn Bridge at: {Url}", roslynBridgeUrl);

app.Run();
