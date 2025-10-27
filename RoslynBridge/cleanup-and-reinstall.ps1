#Requires -Version 5.0

<#
.SYNOPSIS
    Cleanup and reinstall the RoslynBridge Visual Studio extension

.DESCRIPTION
    This script automates the process of:
    1. Closing Visual Studio
    2. Removing the extension cache
    3. Deleting the installed extension
    4. Rebuilding and reinstalling the extension

.PARAMETER Action
    The action to perform: Clean, Reinstall, or Both (default)

.PARAMETER SkipBuild
    Skip the build step when reinstalling

.EXAMPLE
    .\cleanup-and-reinstall.ps1
    Cleans and reinstalls the extension

.EXAMPLE
    .\cleanup-and-reinstall.ps1 -Action Clean
    Only removes the extension and cache

.EXAMPLE
    .\cleanup-and-reinstall.ps1 -Action Reinstall -SkipBuild
    Reinstalls without rebuilding (uses existing VSIX)
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Clean", "Reinstall", "Both")]
    [string]$Action = "Both",

    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Write-Step {
    param([string]$Message)
    Write-ColorOutput "`n>>> $Message" "Cyan"
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "    ✓ $Message" "Green"
}

function Write-Warning {
    param([string]$Message)
    Write-ColorOutput "    ⚠ $Message" "Yellow"
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-ColorOutput "    ✗ $Message" "Red"
}

# Configuration
$VSInstancePath = "C:\Users\AJ\AppData\Local\Microsoft\VisualStudio\17.0_875bdf7a"
$ExtensionsPath = Join-Path $VSInstancePath "Extensions"
$ExtensionFolder = "ulxnn4r3.rql"
$ExtensionFullPath = Join-Path $ExtensionsPath $ExtensionFolder
$ProjectPath = $PSScriptRoot
$SolutionFile = Join-Path $ProjectPath "RoslynBridge.csproj"

Write-ColorOutput "`n========================================" "Cyan"
Write-ColorOutput "  RoslynBridge Extension Manager" "Cyan"
Write-ColorOutput "========================================`n" "Cyan"

# Step 1: Close Visual Studio
function Stop-VisualStudio {
    Write-Step "Closing Visual Studio..."

    $vsProcesses = Get-Process | Where-Object { $_.Name -like "devenv*" }

    if ($vsProcesses) {
        Write-ColorOutput "    Found $($vsProcesses.Count) Visual Studio instance(s) running" "Gray"

        foreach ($proc in $vsProcesses) {
            try {
                Write-ColorOutput "    Closing Visual Studio (PID: $($proc.Id))..." "Gray"
                $proc.CloseMainWindow() | Out-Null
                Start-Sleep -Seconds 2

                if (!$proc.HasExited) {
                    Write-ColorOutput "    Force closing..." "Gray"
                    $proc.Kill()
                    Start-Sleep -Seconds 1
                }

                Write-Success "Visual Studio closed"
            }
            catch {
                Write-Warning "Failed to close Visual Studio: $_"
            }
        }

        # Wait for processes to fully terminate
        Start-Sleep -Seconds 3
    }
    else {
        Write-Success "Visual Studio is not running"
    }
}

# Step 2: Clean extension and cache
function Clear-ExtensionCache {
    Write-Step "Cleaning extension cache..."

    $filesToDelete = @(
        (Join-Path $ExtensionsPath "ExtensionMetadataCache.sqlite"),
        (Join-Path $ExtensionsPath "ExtensionMetadata.mpack"),
        (Join-Path $VSInstancePath "ComponentModelCache")
    )

    foreach ($file in $filesToDelete) {
        if (Test-Path $file) {
            try {
                Remove-Item $file -Recurse -Force -ErrorAction Stop
                Write-Success "Deleted: $(Split-Path $file -Leaf)"
            }
            catch {
                Write-Warning "Could not delete $file: $_"
            }
        }
        else {
            Write-ColorOutput "    Skipped (not found): $(Split-Path $file -Leaf)" "Gray"
        }
    }
}

function Remove-InstalledExtension {
    Write-Step "Removing installed extension..."

    if (Test-Path $ExtensionFullPath) {
        try {
            Remove-Item $ExtensionFullPath -Recurse -Force -ErrorAction Stop
            Write-Success "Extension removed: $ExtensionFolder"
        }
        catch {
            Write-ErrorMsg "Failed to remove extension: $_"
            return $false
        }
    }
    else {
        Write-Success "Extension not installed"
    }

    return $true
}

# Step 3: Build the extension
function Build-Extension {
    Write-Step "Building RoslynBridge extension..."

    if (!(Test-Path $SolutionFile)) {
        Write-ErrorMsg "Solution file not found: $SolutionFile"
        return $false
    }

    Write-ColorOutput "    Building in Release mode..." "Gray"

    try {
        # Use MSBuild to build the project
        $msbuildPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

        if (!(Test-Path $msbuildPath)) {
            Write-ErrorMsg "MSBuild not found at: $msbuildPath"
            return $false
        }

        $buildOutput = & $msbuildPath $SolutionFile /t:Rebuild /p:Configuration=Release /v:minimal 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Success "Build completed successfully"
            return $true
        }
        else {
            Write-ErrorMsg "Build failed with exit code: $LASTEXITCODE"
            Write-ColorOutput "Build output:" "Gray"
            $buildOutput | ForEach-Object { Write-ColorOutput "    $_" "Gray" }
            return $false
        }
    }
    catch {
        Write-ErrorMsg "Build error: $_"
        return $false
    }
}

# Step 4: Install the extension
function Install-Extension {
    Write-Step "Installing extension..."

    # Find the VSIX file
    $vsixFiles = Get-ChildItem -Path $ProjectPath -Filter "*.vsix" -Recurse | Where-Object {
        $_.FullName -like "*\bin\Release\*" -or $_.FullName -like "*\bin\Debug\*"
    } | Sort-Object LastWriteTime -Descending

    if (!$vsixFiles) {
        Write-ErrorMsg "No VSIX file found. Please build the project first."
        return $false
    }

    $vsixFile = $vsixFiles[0].FullName
    Write-ColorOutput "    Found VSIX: $vsixFile" "Gray"

    # Use VSIXInstaller to install
    $vsixInstallerPath = "C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe"

    if (!(Test-Path $vsixInstallerPath)) {
        Write-ErrorMsg "VSIXInstaller not found at: $vsixInstallerPath"
        return $false
    }

    Write-ColorOutput "    Installing extension..." "Gray"

    try {
        $installArgs = @("/quiet", "/admin", $vsixFile)
        $installOutput = & $vsixInstallerPath $installArgs 2>&1

        if ($LASTEXITCODE -eq 0 -or $LASTEXITCODE -eq 1001) {
            # 1001 = already installed (which is fine, we're reinstalling)
            Write-Success "Extension installed successfully"
            return $true
        }
        else {
            Write-ErrorMsg "Installation failed with exit code: $LASTEXITCODE"
            $installOutput | ForEach-Object { Write-ColorOutput "    $_" "Gray" }
            return $false
        }
    }
    catch {
        Write-ErrorMsg "Installation error: $_"
        return $false
    }
}

# Step 5: Verify installation
function Test-ExtensionInstalled {
    Write-Step "Verifying installation..."

    if (Test-Path $ExtensionFullPath) {
        $dllPath = Join-Path $ExtensionFullPath "RoslynBridge.dll"
        if (Test-Path $dllPath) {
            Write-Success "Extension files found at: $ExtensionFolder"

            # Check file version
            try {
                $dllInfo = Get-Item $dllPath
                $version = $dllInfo.VersionInfo.FileVersion
                Write-ColorOutput "    Version: $version" "Gray"
            }
            catch {
                Write-ColorOutput "    Version: Unable to determine" "Gray"
            }

            return $true
        }
    }

    Write-Warning "Extension files not found"
    return $false
}

# Main execution
try {
    $startTime = Get-Date

    if ($Action -eq "Clean" -or $Action -eq "Both") {
        Stop-VisualStudio
        Clear-ExtensionCache
        $cleanSuccess = Remove-InstalledExtension

        if (!$cleanSuccess) {
            Write-ErrorMsg "`nCleanup failed!"
            exit 1
        }
    }

    if ($Action -eq "Reinstall" -or $Action -eq "Both") {
        if (!$SkipBuild) {
            $buildSuccess = Build-Extension
            if (!$buildSuccess) {
                Write-ErrorMsg "`nBuild failed! Cannot reinstall."
                exit 1
            }
        }
        else {
            Write-Warning "Skipping build as requested"
        }

        $installSuccess = Install-Extension
        if (!$installSuccess) {
            Write-ErrorMsg "`nInstallation failed!"
            exit 1
        }

        Test-ExtensionInstalled | Out-Null
    }

    $elapsed = (Get-Date) - $startTime

    Write-ColorOutput "`n========================================" "Cyan"
    Write-ColorOutput "  ✓ Completed in $($elapsed.TotalSeconds.ToString('F1')) seconds" "Green"
    Write-ColorOutput "========================================`n" "Cyan"

    if ($Action -eq "Reinstall" -or $Action -eq "Both") {
        Write-ColorOutput "Next steps:" "Yellow"
        Write-ColorOutput "  1. Open Visual Studio" "White"
        Write-ColorOutput "  2. Go to Extensions → Manage Extensions" "White"
        Write-ColorOutput "  3. Check 'Installed' tab for 'Roslyn Bridge'" "White"
        Write-ColorOutput "  4. Open a solution and verify the extension loads" "White"
        Write-ColorOutput "  5. Test: curl -X POST http://localhost:59123/health -H 'Content-Type: application/json' -d '{}'" "White"
        Write-ColorOutput ""
    }
}
catch {
    Write-ErrorMsg "`nUnexpected error: $_"
    Write-ColorOutput $_.ScriptStackTrace "Gray"
    exit 1
}
