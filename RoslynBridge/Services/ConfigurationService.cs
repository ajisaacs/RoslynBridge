#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace RoslynBridge.Services
{
    /// <summary>
    /// Service for reading configuration from appsettings.json
    /// </summary>
    public class ConfigurationService
    {
        private static ConfigurationService? _instance;
        private readonly RoslynBridgeConfig _config;

        private ConfigurationService()
        {
            _config = LoadConfiguration();
        }

        public static ConfigurationService Instance => _instance ??= new ConfigurationService();

        public string WebApiUrl => _config.RoslynBridge.WebApiUrl;
        public int DefaultPort => _config.RoslynBridge.DefaultPort;
        public int MaxPortRange => _config.RoslynBridge.MaxPortRange;
        public int HeartbeatIntervalSeconds => _config.RoslynBridge.HeartbeatIntervalSeconds;

        private RoslynBridgeConfig LoadConfiguration()
        {
            try
            {
                // Get the directory where the extension is installed
                var assemblyPath = Assembly.GetExecutingAssembly().Location;
                var assemblyDir = Path.GetDirectoryName(assemblyPath);
                var configPath = Path.Combine(assemblyDir ?? "", "appsettings.json");

                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var config = JsonSerializer.Deserialize<RoslynBridgeConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (config != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Loaded configuration from {configPath}");
                        return config;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Configuration file not found at {configPath}, using defaults");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading configuration: {ex.Message}, using defaults");
            }

            // Return default configuration
            return new RoslynBridgeConfig
            {
                RoslynBridge = new RoslynBridgeSettings
                {
                    WebApiUrl = "http://localhost:5001",
                    DefaultPort = 59123,
                    MaxPortRange = 10,
                    HeartbeatIntervalSeconds = 60
                }
            };
        }
    }

    public class RoslynBridgeConfig
    {
        public RoslynBridgeSettings RoslynBridge { get; set; } = new();
    }

    public class RoslynBridgeSettings
    {
        public string WebApiUrl { get; set; } = "http://localhost:5001";
        public int DefaultPort { get; set; } = 59123;
        public int MaxPortRange { get; set; } = 10;
        public int HeartbeatIntervalSeconds { get; set; } = 60;
    }
}
