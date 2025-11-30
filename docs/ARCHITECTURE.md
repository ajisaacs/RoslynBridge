# RoslynBridge Architecture

This document provides detailed technical information about RoslynBridge's architecture, components, and communication flows.

## System Architecture

```
┌─────────────────┐
│  External Tool  │  (Claude Code, Scripts, CI/CD)
│  (Port 5001)    │
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────────────────────────┐
│          RoslynBridge.WebApi                        │
│     (Windows Service - Port 5001)                   │
│  ┌──────────────────────────────────────────────┐   │
│  │  Instance Registry & Router                  │   │
│  │  - Tracks all VS instances                   │   │
│  │  - Routes by solution name                   │   │
│  │  - Manages heartbeats                        │   │
│  └──────────────────────────────────────────────┘   │
└────────┬─────────────────────────┬──────────────────┘
         │                         │
         ▼                         ▼
┌─────────────────┐       ┌─────────────────┐
│  VS Instance 1  │       │  VS Instance 2  │
│ (Port 59123)    │       │ (Port 59124)    │
│                 │       │                 │
│ Solution A      │       │ Solution B      │
│ RoslynBridge    │       │ RoslynBridge    │
│ Extension       │       │ Extension       │
└─────────────────┘       └─────────────────┘
```

## Components

### 1. RoslynBridge (Visual Studio Extension)

**Technology:** VSIX package using Visual Studio SDK

**Responsibilities:**
- Loads automatically when Visual Studio starts
- Hosts HTTP server on dynamically-assigned port (59123, 59124, etc.)
- Provides direct access to Roslyn workspace API
- Registers with WebAPI on startup
- Sends heartbeats every 60 seconds to maintain registration
- Processes Roslyn queries (diagnostics, symbols, references)

**Port Assignment:**
Ports are assigned dynamically starting from 59123 to avoid conflicts when running multiple Visual Studio instances.

### 2. RoslynBridge.WebApi (Windows Service)

**Technology:** ASP.NET Core Web API running as Windows Service

**Responsibilities:**
- Listens on fixed port 5001 for external requests
- Maintains in-memory registry of all active VS instances
- Routes requests to correct instance based on solution name
- Automatically cleans up stale instances (>180s without heartbeat)
- Provides unified API interface regardless of how many VS instances are running

**Benefits:**
- Clients don't need to know which port a specific solution is on
- Automatic failover if a VS instance crashes
- Single endpoint for all Roslyn operations

## Communication Flows

### Instance Registration Flow

```
VS Starts
    │
    ├─→ Load RoslynBridge Extension
    │
    ├─→ Start HTTP Server (port 59123)
    │
    ├─→ POST /api/instances/register
    │       {
    │         "solutionName": "RoslynBridge",
    │         "port": 59123,
    │         "vsVersion": "17.8.0"
    │       }
    │
    ├─→ WebAPI Registers Instance
    │
    └─→ Start Heartbeat Timer (every 60s)
            │
            └─→ POST /api/instances/heartbeat
                    {
                      "solutionName": "RoslynBridge",
                      "port": 59123
                    }
```

**Stale Instance Cleanup:**
A background service in WebAPI runs every 60 seconds and removes any instances that haven't sent a heartbeat in the last 180 seconds.

### Query Routing Flow

```
External Request
    │
    ├─→ GET /api/roslyn/diagnostics?solutionName=MySolution
    │
    ├─→ WebAPI Receives Request
    │
    ├─→ Lookup Instance by Solution Name
    │       │
    │       ├─→ Found: Get port (59123)
    │       └─→ Not Found: Return 404
    │
    ├─→ Forward Request to VS Instance
    │       http://localhost:59123/api/diagnostics
    │
    ├─→ VS Extension Queries Roslyn Workspace
    │       │
    │       ├─→ Access Solution object
    │       ├─→ Iterate through projects
    │       ├─→ Compile and get diagnostics
    │       └─→ Format results as JSON
    │
    ├─→ VS Extension Returns Results
    │
    └─→ WebAPI Proxies Response to Client
```

## Multi-Instance Support

### How Multiple Instances Work

When you have multiple Visual Studio instances open:

1. Each VS instance runs its own RoslynBridge extension
2. Each extension starts an HTTP server on a unique port
3. All extensions register with the central WebAPI service
4. WebAPI maintains a registry mapping solution names to ports
5. External clients query WebAPI using solution name
6. WebAPI routes to the correct VS instance automatically

### Example Multi-Instance Scenario

```
Developer Workflow:
- Opens VS Instance 1 with "MyWebApp.sln" → Registers on port 59123
- Opens VS Instance 2 with "MyAPI.sln" → Registers on port 59124
- Opens VS Instance 3 with "MyLibrary.sln" → Registers on port 59125

Client Queries:
curl "localhost:5001/api/roslyn/diagnostics?solutionName=MyAPI"
  → WebAPI routes to port 59124

curl "localhost:5001/api/roslyn/diagnostics?solutionName=MyWebApp"
  → WebAPI routes to port 59123
```

## Configuration

### WebAPI Configuration (`appsettings.json`)

```json
{
  "Urls": "http://localhost:5001",
  "RoslynBridge": {
    "BaseUrl": "http://localhost:59123"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Configuration Options:**
- `Urls`: The address WebAPI listens on (default: `http://localhost:5001`)
- `RoslynBridge.BaseUrl`: Fallback URL for VS instance (used when registry is empty)
- `Logging.LogLevel`: Controls log verbosity

### VS Extension Configuration (`appsettings.json`)

```json
{
  "Server": {
    "Port": 0,
    "EnableAutoStart": true
  },
  "WebApi": {
    "BaseUrl": "http://localhost:5001",
    "RegistrationEndpoint": "/api/instances/register",
    "HeartbeatEndpoint": "/api/instances/heartbeat",
    "HeartbeatIntervalSeconds": 60
  }
}
```

**Configuration Options:**
- `Server.Port`: Port for HTTP server (0 = auto-assign starting from 59123)
- `Server.EnableAutoStart`: Start server automatically when VS loads
- `WebApi.BaseUrl`: URL of the WebAPI service to register with
- `WebApi.HeartbeatIntervalSeconds`: How often to send heartbeats (default: 60)

## Performance Characteristics

### Query Performance

| Operation | Typical Time | Notes |
|-----------|-------------|-------|
| Initial connection | ~50ms | WebAPI → VS extension handshake |
| Diagnostics summary | ~100-200ms | Depends on solution size |
| Full diagnostics | ~200-500ms | Iterates all projects and documents |
| Symbol search | ~50-200ms | Uses Roslyn's symbol index |
| Find references | ~100-300ms | Depends on symbol usage |
| Solution overview | ~100ms | Lightweight metadata query |

### Why It's Fast

1. **In-Memory Workspace**: Roslyn maintains a fully-compiled representation of your solution in memory
2. **No Rebuild Required**: Unlike `dotnet build`, queries use the existing compilation
3. **Incremental Compilation**: Roslyn only recompiles changed files
4. **Direct API Access**: No serialization/deserialization overhead within VS

### Scaling Considerations

- WebAPI can handle 100+ concurrent requests
- Each VS instance can handle 10-20 concurrent queries
- Registry lookup is O(1) in-memory hash table
- Heartbeat processing is non-blocking
- Stale cleanup runs in background thread

## API Endpoint Reference

### Diagnostics Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/roslyn/diagnostics/summary` | GET | Count of errors, warnings, info by severity |
| `/api/roslyn/diagnostics` | GET | All diagnostics with file locations |
| `/api/roslyn/diagnostics?severity=error` | GET | Only errors |
| `/api/roslyn/diagnostics?severity=warning` | GET | Only warnings |

### Symbol Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/roslyn/symbol/search` | GET | Find symbol by name |
| `/api/roslyn/symbol` | GET | Get symbol at file location |
| `/api/roslyn/references` | GET | Find all references to symbol |

### Project & Solution Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/roslyn/projects` | GET | List all projects in solution |
| `/api/roslyn/solution/overview` | GET | Solution statistics and metadata |

### Instance Management Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/instances` | GET | List all registered VS instances |
| `/api/instances/register` | POST | Register new VS instance (internal) |
| `/api/instances/heartbeat` | POST | Update instance heartbeat (internal) |
| `/api/health/ping` | GET | Health check endpoint |

## Security Considerations

### Current Security Model

- **Local-only access**: WebAPI binds to `localhost:5001` only
- **No authentication**: Assumes trusted local environment
- **No encryption**: Uses HTTP, not HTTPS

### Future Enhancements

For production/remote scenarios, consider:
- HTTPS with self-signed certificates
- API key authentication
- Request rate limiting
- IP whitelisting

## Error Handling

### Common Error Scenarios

1. **Solution Not Found (404)**
   - Solution name doesn't match any registered instance
   - VS instance crashed and hasn't re-registered
   - Typo in solution name

2. **Service Unavailable (503)**
   - VS instance registered but HTTP server not responding
   - Instance port changed but registry not updated

3. **Internal Server Error (500)**
   - Roslyn API threw exception
   - Compilation error prevented query
   - File path invalid or not in solution

### Error Response Format

```json
{
  "success": false,
  "error": {
    "message": "Solution 'MySolution' not found in registry",
    "code": "SOLUTION_NOT_FOUND",
    "details": "Check /api/instances for available solutions"
  }
}
```

## Use Cases and Integration Patterns

### CI/CD Integration

Query diagnostics during builds and fail pipeline if errors exist:

```bash
#!/bin/bash
response=$(curl -s "http://localhost:5001/api/roslyn/diagnostics/summary?solutionName=MyApp")
errors=$(echo $response | jq '.errorCount')

if [ "$errors" -gt 0 ]; then
  echo "Build failed: $errors compilation errors found"
  curl "http://localhost:5001/api/roslyn/diagnostics?solutionName=MyApp&severity=error"
  exit 1
fi
```

### Pre-commit Hooks

Prevent commits with compilation errors:

```bash
#!/bin/bash
errors=$(curl -s "http://localhost:5001/api/roslyn/diagnostics?solutionName=MyApp&severity=error" | jq '.data.diagnostics | length')

if [ "$errors" -gt 0 ]; then
  echo "Cannot commit: $errors compilation errors exist"
  exit 1
fi
```

### AI-Powered Code Review

Claude Code can use RoslynBridge to:
- Analyze diagnostics and suggest fixes
- Find symbol definitions and usages
- Navigate code relationships
- Suggest refactoring opportunities

### Editor Integration

Build custom code editors or plugins that leverage Roslyn's semantic understanding without embedding VS.

## Deployment Scenarios

### Development Machine

- Single developer
- Multiple VS instances for different projects
- WebAPI runs as Windows service
- All communication over localhost

### Build Server

- CI/CD agent
- Single VS instance
- WebAPI queries during build pipeline
- Results logged or fail build

### Team Collaboration

- Shared development server
- Multiple developers with VS instances
- Central WebAPI routes to correct instance
- Each developer queries their own solution

## Troubleshooting Guide

### Debugging Registration Issues

```powershell
# Check WebAPI is running
Get-Service RoslynBridgeWebApi

# Check what instances are registered
curl http://localhost:5001/api/instances | jq

# Check VS extension logs
# In VS: View → Output → Show output from: RoslynBridge
```

### Debugging Query Issues

```powershell
# Test WebAPI directly
curl http://localhost:5001/api/health/ping

# Test specific VS instance directly (bypass WebAPI)
curl http://localhost:59123/api/diagnostics

# Check instance heartbeat
curl http://localhost:5001/api/instances | jq '.[] | {solution, lastSeen}'
```

### Network Diagnostics

```powershell
# Check if port 5001 is listening
netstat -ano | findstr :5001

# Check if VS instance ports are listening
netstat -ano | findstr :59123
```

## Future Enhancements

Potential improvements under consideration:

- **Remote access support**: HTTPS + authentication for network access
- **WebSocket support**: Real-time diagnostic updates
- **Code actions**: Apply quick fixes via API
- **Refactoring operations**: Trigger renames, extractions, etc.
- **Test execution**: Run tests and get results via API
- **Docker support**: Run in containerized environment
- **Cross-platform**: Support VS Code / OmniSharp

## References

- [Roslyn API Documentation](https://github.com/dotnet/roslyn/wiki)
- [Visual Studio SDK](https://docs.microsoft.com/en-us/visualstudio/extensibility/)
- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core/)
