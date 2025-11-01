# RoslynBridge Project Notes

## CRITICAL: Building the Solution

**DO NOT use `dotnet build` for this project!**

This is a Visual Studio Extension project (.vsix) and MUST be built using MSBuild, not dotnet CLI.

### Correct Build Command:
```bash
# Use MSBuild from Visual Studio installation
msbuild RoslynBridge.sln /t:Restore,Build /p:Configuration=Debug
```

Or use the full path:
```bash
"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" RoslynBridge.sln /t:Restore,Build /p:Configuration=Debug
```

### Why?
- This project targets .NET Framework 4.8 (not .NET Core/.NET 5+)
- It requires Visual Studio SDK assemblies
- `dotnet build` will fail with missing assembly references (Microsoft.VisualStudio.*, etc.)

## Project Structure

This solution contains:
1. **RoslynBridge** - Visual Studio Extension (.vsix)
   - Runs inside VS process
   - Provides Roslyn analysis capabilities via JSON RPC

2. **RoslynBridge.WebApi** - ASP.NET Core Web API
   - Central routing service (port 5001)
   - Routes requests to appropriate VS instances

## Architecture

```
Claude → rb.ps1 → WebAPI (:5001) → VS Instance (auto-routed by solution)
```

## Testing

After building, the VS extension needs to be running in a VS instance with a solution open. Use:
```bash
powershell -Command "& ~/.claude/skills/roslyn-bridge/scripts/rb.ps1 instances"
```

To verify VS instances are registered and ready.
