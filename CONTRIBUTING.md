# Contributing to RoslynBridge

Thank you for your interest in contributing to RoslynBridge! This guide will help you get started with development, testing, and submitting contributions.

## Development Setup

### Prerequisites

- Visual Studio 2022 (version 17.0 or later)
- .NET 8.0 SDK
- Git
- PowerShell (for build scripts)

### Getting Started

1. **Fork and clone the repository**
   ```bash
   git clone https://github.com/YOUR_USERNAME/RoslynBridge.git
   cd RoslynBridge
   ```

2. **Open the solution in Visual Studio**
   ```bash
   start RoslynBridge.sln
   ```

3. **Restore NuGet packages**
   Visual Studio will automatically restore packages, or run:
   ```bash
   dotnet restore
   ```

## Building from Source

### Build the Visual Studio Extension

Using MSBuild:
```bash
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" RoslynBridge\RoslynBridge.csproj /t:Build /p:Configuration=Debug
```

The VSIX package will be created at: `RoslynBridge\bin\Debug\RoslynBridge.vsix`

### Build the WebAPI Service

Using .NET CLI:
```bash
dotnet build RoslynBridge.WebApi\RoslynBridge.WebApi.csproj --configuration Debug
```

### Build Everything

To build the entire solution:
```bash
dotnet build RoslynBridge.sln --configuration Debug
```

## Running and Testing

### Run the WebAPI Locally (Without Service)

For development, you can run the WebAPI directly without installing it as a Windows service:

```bash
dotnet run --project RoslynBridge.WebApi\RoslynBridge.WebApi.csproj
```

The API will be available at `http://localhost:5001`

### Install the Extension for Testing

Use the helper script to build and install the VSIX:

```powershell
scripts\reinstall-vsix.ps1 -Configuration Debug

# Options:
# -SkipBuild     Skip the build step and use existing VSIX
# -NoUninstall   Install without uninstalling first
# -VerboseOutput Show detailed installer output
```

Or manually:
1. Build the extension
2. Close all Visual Studio instances
3. Double-click `RoslynBridge\bin\Debug\RoslynBridge.vsix`
4. Restart Visual Studio

### Running Tests

Run all tests:
```bash
dotnet test
```

Run specific test project:
```bash
dotnet test RoslynBridge.Tests\RoslynBridge.Tests.csproj
```

Run tests with coverage:
```bash
dotnet test --collect:"XPlat Code Coverage"
```

Run tests with verbose output:
```bash
dotnet test --verbosity detailed
```

## Development Workflow

### Typical Development Cycle

1. **Make changes** to the code
2. **Build** the project
3. **Install** the VSIX (for extension changes)
4. **Test** in Visual Studio
5. **Debug** if needed
6. **Run tests**
7. **Commit** changes

### Quick Iteration Script

For rapid testing of extension changes:

```powershell
# Build, install, and restart VS in one command
scripts\reinstall-vsix.ps1 -Configuration Debug
```

### Debugging the Extension

1. Set `RoslynBridge` project as startup project
2. Press F5 to launch experimental VS instance
3. Open a solution in the experimental instance
4. Set breakpoints in your code
5. Trigger the functionality you're testing

### Debugging the WebAPI

1. Set `RoslynBridge.WebApi` as startup project
2. Press F5 to launch with debugger
3. Use tools like `curl` or Postman to send requests
4. Inspect logs and breakpoints

### Viewing Extension Logs

In Visual Studio with the extension loaded:
1. Go to **View → Output**
2. In the dropdown, select **RoslynBridge**
3. View real-time logs from the extension

### Viewing WebAPI Logs

When running as a service:
```powershell
Get-EventLog -LogName Application -Source "RoslynBridge Web API" -Newest 50
```

When running in development:
Logs are written to console output.

## Project Structure

```
RoslynBridge/
├── RoslynBridge/              # Visual Studio extension (VSIX)
│   ├── Server/                # HTTP server implementation
│   ├── Services/              # Roslyn query services
│   └── RoslynBridgePackage.cs # Extension entry point
│
├── RoslynBridge.WebApi/       # WebAPI service
│   ├── Controllers/           # API endpoints
│   ├── Services/              # Instance registry, routing
│   └── Program.cs             # Service configuration
│
├── RoslynBridge.Tests/        # Unit tests
│   ├── ServerTests/           # Extension tests
│   └── WebApiTests/           # WebAPI tests
│
├── scripts/                   # Build and install scripts
│   ├── reinstall-vsix.ps1     # Quick VSIX reinstall
│   ├── webapi-install.ps1     # Service installer
│   └── sync-skill.ps1         # Sync Claude skill
│
└── .claude/                   # Claude Code integration
    └── skills/roslyn-bridge/  # Claude skill definition
```

## Code Guidelines

### C# Coding Style

- Follow standard C# conventions
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep methods focused and single-purpose
- Avoid deep nesting (prefer early returns)

### Error Handling

- Use structured exception handling
- Log errors with appropriate context
- Return meaningful error messages to clients
- Don't swallow exceptions silently

### Logging

Use structured logging with appropriate levels:

```csharp
// Extension logging
OutputWindowLogger.Log("Processing diagnostics request");
OutputWindowLogger.LogError("Failed to load solution", ex);

// WebAPI logging
_logger.LogInformation("Routing request to instance {SolutionName}", solutionName);
_logger.LogError(ex, "Failed to forward request");
```

### Testing

- Write unit tests for new functionality
- Test error conditions and edge cases
- Use descriptive test names
- Keep tests isolated and independent

## Making Changes

### Before You Start

1. **Check existing issues** - See if someone is already working on it
2. **Create an issue** - Discuss major changes before implementing
3. **Create a branch** - Use descriptive branch names like `feature/symbol-search` or `fix/heartbeat-timeout`

### Commit Guidelines

Use clear, descriptive commit messages:

```
Good:
✓ Add symbol search endpoint to WebAPI
✓ Fix heartbeat timeout causing instance de-registration
✓ Improve error handling in diagnostic service

Bad:
✗ Update code
✗ Fix bug
✗ Changes
```

Use conventional commit format when possible:
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation changes
- `refactor:` - Code refactoring
- `test:` - Adding tests
- `chore:` - Maintenance tasks

### Pull Request Process

1. **Update your branch** with latest main
   ```bash
   git checkout main
   git pull upstream main
   git checkout your-branch
   git rebase main
   ```

2. **Run all tests** and ensure they pass
   ```bash
   dotnet test
   ```

3. **Update documentation** if needed
   - README.md for user-facing changes
   - ARCHITECTURE.md for technical changes
   - Code comments for implementation details

4. **Create a pull request**
   - Use a clear, descriptive title
   - Explain what changed and why
   - Reference related issues
   - Include screenshots for UI changes

5. **Respond to feedback**
   - Address review comments
   - Make requested changes
   - Push updates to your branch

## Common Development Tasks

### Adding a New API Endpoint

1. **Add controller method** in `RoslynBridge.WebApi/Controllers/RoslynController.cs`
2. **Implement service logic** in `RoslynBridge/Services/`
3. **Add tests** in `RoslynBridge.Tests/`
4. **Update documentation** in README or ARCHITECTURE
5. **Test end-to-end** with actual VS instance

### Modifying Extension Behavior

1. **Update relevant service** in `RoslynBridge/Services/`
2. **Update registration** if adding new services
3. **Add logging** for debugging
4. **Test in experimental VS instance**
5. **Check for memory leaks** (extensions run long-lived)

### Updating Claude Code Skill

1. **Edit skill definition** in `.claude/skills/roslyn-bridge/SKILL.md`
2. **Update helper script** in `.claude/skills/roslyn-bridge/scripts/rb`
3. **Test with Claude Code** to verify it works
4. **Run sync script** to update user-level skill:
   ```powershell
   scripts\sync-skill.ps1
   ```

## Release Process

(For maintainers)

1. **Update version** in `.csproj` files
2. **Update CHANGELOG** with release notes
3. **Create git tag** with version number
4. **Build release artifacts**
   ```bash
   scripts\reinstall-vsix.ps1 -Configuration Release -SkipInstall
   scripts\webapi-install.ps1 -Configuration Release
   ```
5. **Create GitHub release** with artifacts
6. **Update documentation** if needed

## Getting Help

- **Documentation**: Check README.md and ARCHITECTURE.md
- **Issues**: Search existing issues or create a new one
- **Discussions**: Use GitHub Discussions for questions
- **Code**: Read inline comments and XML docs

## Code of Conduct

- Be respectful and inclusive
- Welcome newcomers and help them learn
- Focus on constructive feedback
- Assume good intentions

## Recognition

Contributors will be recognized in:
- GitHub contributors list
- Release notes for significant contributions
- Project documentation for major features

## Additional Resources

- [Roslyn API Documentation](https://github.com/dotnet/roslyn/wiki)
- [Visual Studio SDK Docs](https://docs.microsoft.com/en-us/visualstudio/extensibility/)
- [ASP.NET Core Documentation](https://docs.microsoft.com/en-us/aspnet/core/)
- [Windows Service Tutorial](https://docs.microsoft.com/en-us/dotnet/core/extensions/windows-service)

---

Thank you for contributing to RoslynBridge! Your efforts help make C# development with AI assistants better for everyone.
