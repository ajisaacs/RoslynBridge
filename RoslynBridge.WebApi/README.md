# Roslyn Bridge Web API

A modern ASP.NET Core web API that acts as middleware between Claude AI and the Roslyn Bridge Visual Studio plugin, providing RESTful access to C# code analysis capabilities.

## Overview

This web API serves as a bridge between external clients (like Claude AI) and the Visual Studio Roslyn Bridge plugin. It provides:

- **Modern RESTful API** with comprehensive Swagger/OpenAPI documentation
- **CORS-enabled** for web application access
- **Health monitoring** for both the web API and Visual Studio plugin connection
- **Simplified endpoints** for common Roslyn operations
- **Type-safe models** with validation

## Architecture

```
┌─────────────┐      HTTP/REST      ┌──────────────────┐      HTTP      ┌─────────────────────┐
│   Claude    │ ◄─────────────────► │  Web API (5000)  │ ◄────────────► │  VS Plugin (59123)  │
│     AI      │                     │   Middleware     │                │   Roslyn Bridge     │
└─────────────┘                     └──────────────────┘                └─────────────────────┘
```

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Visual Studio with Roslyn Bridge plugin running (default port: 59123)

### Quick Installation

**Option 1: Automated Installation**

Open PowerShell as Administrator:
```powershell
cd RoslynBridge.WebApi
.\install.ps1 -InstallService -StartService
```

This will build, publish, install as a Windows Service, and start the API automatically.

For more detailed service setup options, see [SERVICE_SETUP.md](SERVICE_SETUP.md).

**Option 2: Development Mode**

1. **Start the Visual Studio plugin** (it should be running on port 59123)

2. **Run the Web API:**
   ```bash
   cd RoslynBridge.WebApi
   dotnet run
   ```

3. **Access Swagger UI:**
   - Navigate to: `http://localhost:5000`
   - Or: `https://localhost:7001` (with HTTPS)

### Configuration

Edit `appsettings.json` to configure the connection:

```json
{
  "RoslynBridge": {
    "BaseUrl": "http://localhost:59123",
    "TimeoutSeconds": 30
  }
}
```

## API Endpoints

### Health Endpoints

- **GET /api/health** - Check health status of Web API and VS plugin
- **GET /api/health/ping** - Simple ping endpoint

### Roslyn Query Endpoints

- **POST /api/roslyn/query** - Execute any Roslyn query
- **GET /api/roslyn/projects** - Get all projects in solution
- **GET /api/roslyn/solution/overview** - Get solution statistics
- **GET /api/roslyn/diagnostics** - Get errors and warnings
- **GET /api/roslyn/symbol** - Get symbol information at position
- **GET /api/roslyn/references** - Find all references to symbol
- **GET /api/roslyn/symbol/search** - Search for symbols by name

### Refactoring Endpoints

- **POST /api/roslyn/format** - Format a document
- **POST /api/roslyn/project/package/add** - Add NuGet package
- **POST /api/roslyn/project/build** - Build a project

## Example Usage

### Using curl

```bash
# Health check
curl http://localhost:5000/api/health

# Get all projects
curl http://localhost:5000/api/roslyn/projects

# Get solution overview
curl http://localhost:5000/api/roslyn/solution/overview

# Execute custom query
curl -X POST http://localhost:5000/api/roslyn/query \
  -H "Content-Type: application/json" \
  -d '{
    "queryType": "getsymbol",
    "filePath": "C:\\path\\to\\file.cs",
    "line": 10,
    "column": 5
  }'

# Search for symbols
curl "http://localhost:5000/api/roslyn/symbol/search?symbolName=MyClass"

# Get diagnostics
curl "http://localhost:5000/api/roslyn/diagnostics"
```

### Using JavaScript/TypeScript

```typescript
const response = await fetch('http://localhost:5000/api/roslyn/query', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    queryType: 'getprojects'
  })
});

const result = await response.json();
console.log(result.data);
```

### Using C#

```csharp
using var client = new HttpClient();
client.BaseAddress = new Uri("http://localhost:5000");

var request = new RoslynQueryRequest
{
    QueryType = "getsolutionoverview"
};

var response = await client.PostAsJsonAsync("/api/roslyn/query", request);
var result = await response.Content.ReadFromJsonAsync<RoslynQueryResponse>();
```

## Query Types

The following query types are supported:

### Code Analysis
- `getprojects` - Get all projects
- `getdocument` - Get document information
- `getsymbol` - Get symbol at position
- `getsemanticmodel` - Get semantic model
- `getsyntaxtree` - Get syntax tree
- `getdiagnostics` - Get compilation errors/warnings
- `findreferences` - Find all references
- `findsymbol` - Find symbols by name
- `gettypemembers` - Get type members
- `gettypehierarchy` - Get type hierarchy
- `findimplementations` - Find implementations
- `getnamespacetypes` - Get namespace types
- `getcallhierarchy` - Get call hierarchy
- `getsolutionoverview` - Get solution overview
- `getsymbolcontext` - Get symbol context
- `searchcode` - Search code patterns

### Refactoring
- `formatdocument` - Format document
- `organizeusings` - Organize using statements
- `renamesymbol` - Rename symbol
- `addmissingusing` - Add missing using
- `applycodefix` - Apply code fix

### Project Operations
- `addnugetpackage` - Add NuGet package
- `removenugetpackage` - Remove NuGet package
- `buildproject` - Build project
- `cleanproject` - Clean project
- `restorepackages` - Restore packages
- `createdirectory` - Create directory

## Response Format

All responses follow this structure:

```json
{
  "success": true,
  "message": "Optional message",
  "data": { ... },
  "error": null
}
```

Error responses:

```json
{
  "success": false,
  "message": null,
  "data": null,
  "error": "Error description"
}
```

## Features

### CORS Support
The API is configured with CORS enabled for all origins, making it accessible from web applications.

### Swagger/OpenAPI
Comprehensive API documentation is available at the root URL (`/`) with:
- Detailed endpoint descriptions
- Request/response models
- Try-it-out functionality
- XML documentation comments

### Health Checks
Built-in health checks monitor:
- Web API service status
- Visual Studio plugin connectivity
- Request/response timing

### Logging
Structured logging with different levels:
- Information: General operations
- Warning: Non-critical issues
- Error: Failed operations
- Debug: Detailed tracing (Development only)

## Development

### Project Structure

```
RoslynBridge.WebApi/
├── Controllers/
│   ├── HealthController.cs      # Health check endpoints
│   └── RoslynController.cs      # Roslyn operation endpoints
├── Models/
│   ├── RoslynQueryRequest.cs    # Request DTOs
│   ├── RoslynQueryResponse.cs   # Response DTOs
│   └── HealthCheckResponse.cs   # Health check models
├── Services/
│   ├── IRoslynBridgeClient.cs   # Client interface
│   └── RoslynBridgeClient.cs    # HTTP client implementation
├── Program.cs                    # Application configuration
├── appsettings.json             # Configuration
└── README.md                    # This file
```

### Building

```bash
dotnet build
```

### Running Tests

```bash
dotnet test
```

### Publishing

```bash
dotnet publish -c Release -o ./publish
```

## Deployment

### Docker (Optional)

Create a `Dockerfile`:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["RoslynBridge.WebApi.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build

FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "RoslynBridge.WebApi.dll"]
```

Build and run:

```bash
docker build -t roslyn-bridge-api .
docker run -p 5000:5000 roslyn-bridge-api
```

## Integration with Claude

This API is designed to work seamlessly with Claude AI for code analysis tasks:

1. Claude can query project structure
2. Analyze code for errors and warnings
3. Search for symbols and references
4. Suggest refactorings
5. Navigate code hierarchies

Example Claude prompt:
```
"Using the Roslyn Bridge API at http://localhost:5000,
analyze the current solution and identify any code quality issues."
```

## Troubleshooting

### Visual Studio Plugin Not Connected

Error: `{"vsPluginStatus": "Disconnected"}`

**Solution:**
1. Ensure Visual Studio is running
2. Verify Roslyn Bridge plugin is installed and active
3. Check plugin is listening on port 59123
4. Update `appsettings.json` if using a different port

### CORS Errors

If accessing from a web app:
- Verify CORS is enabled in `Program.cs`
- Check browser console for specific CORS errors
- Ensure the origin is allowed

### Timeout Errors

Increase timeout in `appsettings.json`:
```json
{
  "RoslynBridge": {
    "TimeoutSeconds": 60
  }
}
```

## License

This project is part of the Roslyn Bridge suite.

## Contributing

Contributions are welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

## Support

For issues and questions:
- Check the Swagger documentation at `/`
- Review the logs in the console output
- Verify the Visual Studio plugin is running
