namespace RoslynBridge.WebApi.Services;

/// <summary>
/// Background service that periodically removes stale VS instances
/// </summary>
public class InstanceCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InstanceCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _staleTimeout = TimeSpan.FromMinutes(5);

    public InstanceCleanupService(
        IServiceProvider serviceProvider,
        ILogger<InstanceCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Instance cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                // Get the registry service from scope
                using var scope = _serviceProvider.CreateScope();
                var registryService = scope.ServiceProvider.GetRequiredService<IInstanceRegistryService>();

                registryService.RemoveStaleInstances(_staleTimeout);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during instance cleanup");
            }
        }

        _logger.LogInformation("Instance cleanup service stopped");
    }
}
