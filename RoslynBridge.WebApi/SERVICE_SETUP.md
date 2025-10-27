# Roslyn Bridge Web API - Windows Service Setup

This guide explains how to install and run the Roslyn Bridge Web API as a Windows Service for always-on operation.

## Prerequisites

- **Administrator privileges** required for service installation
- **.NET 8.0 Runtime** or SDK installed
- **Visual Studio** with Roslyn Bridge extension must be running (the VS plugin on port 59123 is required)

## Quick Start

### Option 1: Automated Installation (Recommended)

Open PowerShell **as Administrator** and run:

```powershell
# Navigate to the project directory
cd C:\Path\To\RoslynBridge.WebApi

# Complete installation (build, publish, install service, and start)
.\install.ps1 -InstallService -StartService
```

This single command will:
1. Check prerequisites (.NET SDK)
2. Restore NuGet packages
3. Build the project
4. Publish to `./publish`
5. Install as Windows Service
6. Start the service

### Option 2: Manual Step-by-Step Installation

#### 1. Publish the Application

```powershell
# Navigate to the project directory
cd C:\Path\To\RoslynBridge.WebApi

# Publish the application for Release
dotnet publish -c Release -o publish
```

This creates a self-contained deployment in the `publish` folder.

#### 2. Install as Windows Service

Open PowerShell **as Administrator**:

```powershell
# Install the service
.\install-service.ps1 -Action Install

# Start the service
.\install-service.ps1 -Action Start

# Check service status
.\install-service.ps1 -Action Status
```

### 3. Verify Installation

1. Open Windows Services (`services.msc`)
2. Look for "Roslyn Bridge Web API"
3. Verify it's running
4. Test the API: http://localhost:5000/api/health

## Installation Script Options

The `install.ps1` script provides several options for different scenarios:

```powershell
# Full automated installation
.\install.ps1 -InstallService -StartService

# Build and publish only (no service installation)
.\install.ps1

# Skip build, just publish and install
.\install.ps1 -SkipBuild -InstallService

# Debug build instead of Release
.\install.ps1 -Configuration Debug

# Custom publish path
.\install.ps1 -PublishPath "C:\MyApp\Publish" -InstallService

# Reinstall after code changes
.\install.ps1 -InstallService
```

## Service Management Commands

```powershell
# Install service
.\install-service.ps1 -Action Install

# Start service
.\install-service.ps1 -Action Start

# Stop service
.\install-service.ps1 -Action Stop

# Restart service
.\install-service.ps1 -Action Restart

# Check status
.\install-service.ps1 -Action Status

# Uninstall service
.\install-service.ps1 -Action Uninstall
```

## Configuration

### Service Settings

The service is configured with:
- **Service Name**: `RoslynBridgeWebApi`
- **Display Name**: Roslyn Bridge Web API
- **Startup Type**: Automatic (starts with Windows)
- **Port**: 5000 (HTTP)

### Changing the Port

Edit `appsettings.json` before publishing:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:YOUR_PORT"
      }
    }
  }
}
```

### Logging

When running as a service, logs are written to:
- **Windows Event Log**: Application → "Roslyn Bridge Web API"
- **Console**: Not available when running as service

To view logs:
1. Open Event Viewer
2. Navigate to: Windows Logs → Application
3. Filter by source: "Roslyn Bridge Web API"

## Troubleshooting

### Service Won't Start

**Symptom**: Service starts then immediately stops

**Possible Causes**:
1. Port 5000 is already in use
2. Visual Studio plugin (port 59123) is not running
3. Missing .NET runtime

**Solutions**:
```powershell
# Check if port 5000 is in use
netstat -ano | findstr :5000

# Check if VS plugin is accessible
curl -X POST http://localhost:59123/health -H "Content-Type: application/json" -d "{}"

# Check Event Log for errors
Get-EventLog -LogName Application -Source "Roslyn Bridge Web API" -Newest 10
```

### Service Installed but API Not Responding

**Check the following**:
1. Service is running: `.\install-service.ps1 -Action Status`
2. Visual Studio is open with a solution loaded
3. Firewall isn't blocking port 5000

### Updating the Service

When you update the code:

**Option 1: Using install.ps1 (Recommended)**

```powershell
# Stop service, rebuild, republish, and restart
.\install-service.ps1 -Action Stop
.\install.ps1
.\install-service.ps1 -Action Start
```

**Option 2: Manual Method**

```powershell
# 1. Stop the service
.\install-service.ps1 -Action Stop

# 2. Republish
dotnet publish -c Release -o publish

# 3. Restart the service
.\install-service.ps1 -Action Start
```

**Option 3: Full Reinstall**

```powershell
# Uninstall, republish, and reinstall
.\install-service.ps1 -Action Uninstall
.\install.ps1 -InstallService -StartService
```

## Alternative: Manual Service Installation

If you prefer to use `sc.exe` directly:

```cmd
# Install
sc create RoslynBridgeWebApi binPath="C:\Path\To\publish\RoslynBridge.WebApi.exe" start=auto

# Start
sc start RoslynBridgeWebApi

# Stop
sc stop RoslynBridgeWebApi

# Delete
sc delete RoslynBridgeWebApi
```

## Running in Development

For development, you don't need to install as a service. Just run:

```powershell
dotnet run --urls "http://localhost:5000"
```

The application will work the same way but won't persist after closing the terminal.

## Security Considerations

### Production Deployment

For production environments:

1. **Use HTTPS**: Configure SSL certificates in `appsettings.json`
2. **Restrict CORS**: Update the CORS policy to allow only specific origins
3. **Add Authentication**: Consider adding API key or OAuth authentication
4. **Firewall**: Only allow access from trusted IPs

### Network Access

By default, the service only listens on `localhost:5000`. To allow external access:

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      }
    }
  }
}
```

**Warning**: Only do this in trusted networks. Add authentication first!

## Service Lifecycle

The service:
1. **Starts automatically** when Windows boots
2. **Requires Visual Studio** to be running (with a solution loaded) for full functionality
3. **Tracks request history** in memory (lost on restart)
4. **Logs to Event Log** for monitoring

## Monitoring

### Check Service Status

```powershell
# PowerShell
Get-Service RoslynBridgeWebApi

# Or use the script
.\install-service.ps1 -Action Status
```

### View Logs

```powershell
# View recent logs
Get-EventLog -LogName Application -Source "Roslyn Bridge Web API" -Newest 20

# Monitor logs in real-time
Get-EventLog -LogName Application -Source "Roslyn Bridge Web API" -Newest 1 -AsString
# Press Ctrl+C to stop
```

### Test API Health

```powershell
# Quick ping
curl http://localhost:5000/api/health/ping

# Full health check (includes VS plugin status)
curl http://localhost:5000/api/health

# View Swagger documentation
Start-Process "http://localhost:5000"
```

## Uninstalling

To completely remove the service:

```powershell
# Stop and uninstall
.\install-service.ps1 -Action Uninstall

# Optional: Delete the publish folder
Remove-Item -Path ".\publish" -Recurse -Force
```

## Additional Resources

- **Swagger UI**: http://localhost:5000 (when service is running)
- **Health Check**: http://localhost:5000/api/health
- **History Stats**: http://localhost:5000/api/history/stats
- **Event Viewer**: Windows Logs → Application → "Roslyn Bridge Web API"

## FAQ

**Q: Do I need to keep Visual Studio open?**
A: Yes, the VS plugin (port 59123) must be running. The Web API is just a middleware layer.

**Q: Can I run multiple instances?**
A: Yes, but change the port in `appsettings.json` for each instance.

**Q: What happens if the service can't connect to VS plugin?**
A: API calls will return errors, but the service remains running. History and health endpoints still work.

**Q: Can I use this without the Web API?**
A: Yes, you can call the VS plugin directly on port 59123 using POST requests (see roslyn-bridge skill).

**Q: How do I backup request history?**
A: History is in-memory only. Consider implementing database persistence for production use.
