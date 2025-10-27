# RoslynBridge Extension Management Guide

Quick reference for managing the RoslynBridge Visual Studio extension.

## Common Issues

### "Extension Already Installed" but Not Visible

This happens when Visual Studio's extension cache is out of sync. Use the cleanup script:

```powershell
cd C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge
.\cleanup-and-reinstall.ps1
```

## Scripts

### cleanup-and-reinstall.ps1

Automates extension cleanup and reinstallation.

**Basic Usage:**

```powershell
# Clean and reinstall everything (default)
.\cleanup-and-reinstall.ps1

# Only clean (remove extension and cache)
.\cleanup-and-reinstall.ps1 -Action Clean

# Only reinstall (skip cleanup)
.\cleanup-and-reinstall.ps1 -Action Reinstall

# Reinstall without rebuilding (use existing VSIX)
.\cleanup-and-reinstall.ps1 -Action Reinstall -SkipBuild
```

**What it does:**

1. **Closes Visual Studio** - Ensures no file locks
2. **Cleans Cache** - Removes extension metadata cache
3. **Removes Extension** - Deletes installed extension files
4. **Rebuilds** - Compiles the extension in Release mode
5. **Reinstalls** - Installs the new VSIX
6. **Verifies** - Checks installation succeeded

## Manual Management

### Check if Extension is Installed

```powershell
# Check extension folder
Test-Path "C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\Extensions\ulxnn4r3.rql"

# View extension files
ls "C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\Extensions\ulxnn4r3.rql"
```

### Manual Cleanup

```powershell
# Close Visual Studio first!

# Remove cache files
Remove-Item "C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\Extensions\ExtensionMetadataCache.sqlite" -Force
Remove-Item "C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\Extensions\ExtensionMetadata.mpack" -Force

# Remove extension
Remove-Item "C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\Extensions\ulxnn4r3.rql" -Recurse -Force

# Remove component cache (optional, full reset)
Remove-Item "C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\ComponentModelCache" -Recurse -Force
```

### Manual Build

```powershell
cd C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge

# Build using MSBuild
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
    RoslynBridge.csproj `
    /t:Rebuild `
    /p:Configuration=Release `
    /v:minimal
```

### Manual Install

```powershell
# Find the VSIX
$vsixPath = (Get-ChildItem -Path . -Filter "*.vsix" -Recurse | Where-Object {
    $_.FullName -like "*\bin\Release\*"
} | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName

# Install using VSIXInstaller
& "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe" `
    /quiet `
    /admin `
    $vsixPath
```

## Testing the Extension

### 1. Check Visual Studio Extensions UI

1. Open Visual Studio
2. Go to **Extensions → Manage Extensions**
3. Click **Installed** tab
4. Look for "Roslyn Bridge"

### 2. Test HTTP Endpoint

```powershell
# Health check
curl -X POST http://localhost:59123/health -H "Content-Type: application/json" -d "{}"

# Get projects
curl -X POST http://localhost:59123/query -H "Content-Type: application/json" -d '{"queryType":"getprojects"}'
```

### 3. Check Output Window

In Visual Studio:
1. Open **View → Output**
2. Select "RoslynBridge" from the dropdown
3. Should see startup messages when opening a solution

### 4. Check Event Viewer (if issues)

1. Open Event Viewer
2. Navigate to: **Windows Logs → Application**
3. Filter by Source: "VSPackage"
4. Look for RoslynBridge-related messages

## Debugging

### Extension Won't Load

**Symptoms:**
- Extension shows in "Installed" but HTTP endpoint doesn't work
- No output in RoslynBridge Output window

**Solutions:**

1. **Check Extension Loading:**
   ```powershell
   # In Visual Studio, open Developer PowerShell
   Get-VSPackage | Where-Object { $_.Name -like "*Roslyn*" }
   ```

2. **Enable Diagnostic Logging:**
   - Run Visual Studio with: `devenv.exe /log`
   - Check log at: `%APPDATA%\Microsoft\VisualStudio\17.0_XXXXX\ActivityLog.xml`

3. **Reset Visual Studio:**
   ```cmd
   devenv.exe /ResetSettings
   ```

### Port 59123 Already in Use

```powershell
# Check what's using the port
netstat -ano | findstr :59123

# Kill the process if needed
taskkill /PID <PID> /F
```

### Extension Loads But Crashes

Check the ActivityLog:
```powershell
# Open ActivityLog with formatting
code "$env:APPDATA\Microsoft\VisualStudio\17.0_875bdf7a\ActivityLog.xml"
```

Look for entries with:
- `Type="Error"`
- `Source="RoslynBridge"`

## Development Workflow

### Quick Iteration During Development

```powershell
# 1. Make code changes

# 2. Quick reinstall (no need to close VS manually)
.\cleanup-and-reinstall.ps1

# 3. Open Visual Studio and test
```

### Debugging the Extension

1. Open the RoslynBridge solution in Visual Studio
2. Set breakpoints in your code
3. Press **F5** (or Debug → Start Debugging)
4. This launches a new "Experimental Instance" of VS
5. Open a solution in the experimental instance
6. Breakpoints will hit in the original VS instance

### Testing in Production VS (Not Experimental)

```powershell
# Install in production VS
.\cleanup-and-reinstall.ps1

# Attach debugger from another VS instance
# 1. Open a second Visual Studio
# 2. Debug → Attach to Process
# 3. Select "devenv.exe" (the first VS)
# 4. Set breakpoints in the RoslynBridge code
```

## Uninstalling

### Complete Removal

```powershell
# Use the cleanup script
.\cleanup-and-reinstall.ps1 -Action Clean

# OR manually remove via Extensions Manager
# 1. Open Visual Studio
# 2. Extensions → Manage Extensions
# 3. Find "Roslyn Bridge"
# 4. Click "Uninstall"
# 5. Restart Visual Studio
```

### Remove All Traces

```powershell
# Remove extension
Remove-Item "C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\Extensions\ulxnn4r3.rql" -Recurse -Force

# Remove all VS caches (nuclear option)
Remove-Item "C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\ComponentModelCache" -Recurse -Force
Remove-Item "C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\Extensions\ExtensionMetadataCache.sqlite" -Force
Remove-Item "C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\Extensions\ExtensionMetadata.mpack" -Force

# Reset Visual Studio
devenv.exe /ResetSettings
```

## Troubleshooting Quick Reference

| Issue | Solution |
|-------|----------|
| Extension not visible in UI | `.\cleanup-and-reinstall.ps1` |
| "Already installed" error | `.\cleanup-and-reinstall.ps1 -Action Clean` then reinstall |
| Port 59123 not responding | Check extension loaded in Output window |
| VS won't close | Use Task Manager to end `devenv.exe` |
| Build fails | Check MSBuild output, ensure .NET SDK installed |
| Install fails | Run as Administrator, check VSIXInstaller.exe exists |
| Extension crashes on load | Check ActivityLog.xml for errors |

## File Locations Reference

```
Extension Installation:
C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\Extensions\ulxnn4r3.rql\

Cache Files:
C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\Extensions\ExtensionMetadataCache.sqlite
C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\Extensions\ExtensionMetadata.mpack
C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a\ComponentModelCache\

Activity Log:
C:\Users\AJ\AppData\Roaming\Microsoft\VisualStudio\17.0_875bdf7a\ActivityLog.xml

Build Output:
C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge\bin\Release\

VSIX File:
C:\Users\AJ\Desktop\RoslynBridge\RoslynBridge\bin\Release\RoslynBridge.vsix
```

## Getting Help

If issues persist:

1. Check the ActivityLog.xml for detailed error messages
2. Run Visual Studio with logging: `devenv.exe /log`
3. Check Windows Event Viewer for crashes
4. Try the "nuclear option": Complete removal + reinstall
5. Check that .NET Framework 4.8 is installed
6. Ensure Visual Studio 2022 Community/Pro/Enterprise (17.0+)
