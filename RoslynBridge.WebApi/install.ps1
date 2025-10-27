#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Complete installation script for Roslyn Bridge Web API

.DESCRIPTION
    This script performs a complete installation of the Roslyn Bridge Web API:
    - Checks prerequisites (.NET SDK)
    - Restores NuGet packages
    - Builds the project
    - Publishes the release build
    - Optionally installs as Windows Service
    - Tests the installation

.PARAMETER SkipBuild
    Skip the build step (use existing build)

.PARAMETER SkipPublish
    Skip the publish step (use existing publish)

.PARAMETER InstallService
    Install as Windows Service after publishing

.PARAMETER StartService
    Start the service after installation (requires -InstallService)

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release

.PARAMETER PublishPath
    Path where the application will be published. Default: ./publish

.EXAMPLE
    .\install.ps1
    Full installation without service setup

.EXAMPLE
    .\install.ps1 -InstallService -StartService
    Full installation with automatic service setup and start

.EXAMPLE
    .\install.ps1 -Configuration Debug
    Install debug build instead of release
#>

param(
    [Parameter(Mandatory=$false)]
    [switch]$SkipBuild,

    [Parameter(Mandatory=$false)]
    [switch]$SkipPublish,

    [Parameter(Mandatory=$false)]
    [switch]$InstallService,

    [Parameter(Mandatory=$false)]
    [switch]$StartService,

    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [string]$PublishPath = ".\publish"
)

# Script configuration
$ErrorActionPreference = "Stop"
$ProjectName = "RoslynBridge.WebApi"
$ProjectFile = ".\RoslynBridge.WebApi.csproj"

# Color output helper
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

# Banner
function Show-Banner {
    Write-ColorOutput "`n============================================================" "Cyan"
    Write-ColorOutput "     Roslyn Bridge Web API - Installation Script" "Cyan"
    Write-ColorOutput "============================================================`n" "Cyan"
}

# Step counter
$script:stepNumber = 0
function Write-Step {
    param([string]$Message)
    $script:stepNumber++
    Write-ColorOutput "`n[$script:stepNumber] $Message" "Yellow"
    Write-ColorOutput ("-" * 60) "DarkGray"
}

# Check prerequisites
function Test-Prerequisites {
    Write-Step "Checking Prerequisites"

    # Check for .NET SDK
    Write-Host "Checking for .NET SDK... " -NoNewline
    try {
        $dotnetVersion = dotnet --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-ColorOutput "Found v$dotnetVersion" "Green"
        } else {
            throw "dotnet command failed"
        }
    }
    catch {
        Write-ColorOutput "NOT FOUND" "Red"
        Write-ColorOutput "`nError: .NET SDK is not installed or not in PATH." "Red"
        Write-ColorOutput "Please download and install from: https://dot.net" "Yellow"
        exit 1
    }

    # Check for project file
    Write-Host "Checking for project file... " -NoNewline
    if (Test-Path $ProjectFile) {
        Write-ColorOutput "Found" "Green"
    } else {
        Write-ColorOutput "NOT FOUND" "Red"
        Write-ColorOutput "`nError: Project file not found: $ProjectFile" "Red"
        Write-ColorOutput "Please run this script from the RoslynBridge.WebApi directory." "Yellow"
        exit 1
    }

    Write-ColorOutput "`nAll prerequisites satisfied!" "Green"
}

# Restore NuGet packages
function Restore-Packages {
    Write-Step "Restoring NuGet Packages"

    try {
        dotnet restore $ProjectFile
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet restore failed with exit code $LASTEXITCODE"
        }
        Write-ColorOutput "`nPackages restored successfully!" "Green"
    }
    catch {
        Write-ColorOutput "`nError during package restore: $_" "Red"
        exit 1
    }
}

# Build project
function Build-Project {
    Write-Step "Building Project ($Configuration)"

    try {
        dotnet build $ProjectFile -c $Configuration --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE"
        }
        Write-ColorOutput "`nBuild completed successfully!" "Green"
    }
    catch {
        Write-ColorOutput "`nError during build: $_" "Red"
        exit 1
    }
}

# Publish project
function Publish-Project {
    Write-Step "Publishing Project"

    Write-ColorOutput "Configuration: $Configuration" "Gray"
    Write-ColorOutput "Output Path:   $PublishPath" "Gray"

    try {
        # Clean publish directory if it exists
        if (Test-Path $PublishPath) {
            Write-Host "`nCleaning existing publish directory... " -NoNewline
            Remove-Item -Path $PublishPath -Recurse -Force
            Write-ColorOutput "Done" "Green"
        }

        # Publish
        dotnet publish $ProjectFile -c $Configuration -o $PublishPath --no-build --no-restore
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet publish failed with exit code $LASTEXITCODE"
        }

        # Verify executable exists
        $exePath = Join-Path $PublishPath "$ProjectName.exe"
        if (Test-Path $exePath) {
            Write-ColorOutput "`nProject published successfully!" "Green"
            Write-ColorOutput "Executable: $exePath" "Gray"

            # Show publish directory size
            $publishSize = (Get-ChildItem $PublishPath -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
            Write-ColorOutput ("Publish size: {0:N2} MB" -f $publishSize) "Gray"
        } else {
            throw "Executable not found after publish: $exePath"
        }
    }
    catch {
        Write-ColorOutput "`nError during publish: $_" "Red"
        exit 1
    }
}

# Install Windows Service
function Install-WindowsService {
    Write-Step "Installing Windows Service"

    if (-not (Test-Path ".\install-service.ps1")) {
        Write-ColorOutput "Error: install-service.ps1 not found" "Red"
        return $false
    }

    try {
        & ".\install-service.ps1" -Action Install -PublishPath $PublishPath
        return $true
    }
    catch {
        Write-ColorOutput "Error installing service: $_" "Red"
        return $false
    }
}

# Start Windows Service
function Start-WindowsService {
    Write-Step "Starting Windows Service"

    if (-not (Test-Path ".\install-service.ps1")) {
        Write-ColorOutput "Error: install-service.ps1 not found" "Red"
        return $false
    }

    try {
        & ".\install-service.ps1" -Action Start
        return $true
    }
    catch {
        Write-ColorOutput "Error starting service: $_" "Red"
        return $false
    }
}

# Test installation
function Test-Installation {
    Write-Step "Installation Summary"

    $publishFullPath = Resolve-Path $PublishPath -ErrorAction SilentlyContinue
    if ($publishFullPath) {
        Write-ColorOutput "`nPublished to: $publishFullPath" "Green"

        $exePath = Join-Path $publishFullPath "$ProjectName.exe"
        if (Test-Path $exePath) {
            Write-ColorOutput "Executable:   $exePath" "Green"
        }
    }

    if ($InstallService) {
        $service = Get-Service -Name "RoslynBridgeWebApi" -ErrorAction SilentlyContinue
        if ($service) {
            Write-ColorOutput "`nWindows Service Status:" "Cyan"
            Write-ColorOutput "  Name:   $($service.Name)" "Gray"
            Write-ColorOutput "  Status: $($service.Status)" $(if ($service.Status -eq "Running") { "Green" } else { "Yellow" })
            Write-ColorOutput "  Type:   $($service.StartType)" "Gray"
        }
    }
}

# Show next steps
function Show-NextSteps {
    Write-Step "Next Steps"

    if ($InstallService) {
        if ($StartService) {
            Write-ColorOutput "`nThe service is now running!" "Green"
            Write-ColorOutput "`nAPI should be available at:" "Cyan"
            Write-ColorOutput "  http://localhost:5000" "White"
            Write-ColorOutput "  https://localhost:7001 (HTTPS)" "White"
            Write-ColorOutput "`nSwagger UI:" "Cyan"
            Write-ColorOutput "  http://localhost:5000" "White"
            Write-ColorOutput "`nTo manage the service:" "Yellow"
            Write-ColorOutput "  .\install-service.ps1 -Action Status" "White"
            Write-ColorOutput "  .\install-service.ps1 -Action Stop" "White"
            Write-ColorOutput "  .\install-service.ps1 -Action Restart" "White"
            Write-ColorOutput "  .\install-service.ps1 -Action Uninstall" "White"
        } else {
            Write-ColorOutput "`nService installed but not started." "Yellow"
            Write-ColorOutput "`nTo start the service:" "Cyan"
            Write-ColorOutput "  .\install-service.ps1 -Action Start" "White"
        }
    } else {
        Write-ColorOutput "`nTo run the application manually:" "Cyan"
        Write-ColorOutput "  cd $PublishPath" "White"
        Write-ColorOutput "  .\$ProjectName.exe" "White"
        Write-ColorOutput "`nTo install as a Windows Service:" "Cyan"
        Write-ColorOutput "  .\install-service.ps1 -Action Install -PublishPath $PublishPath" "White"
        Write-ColorOutput "  .\install-service.ps1 -Action Start" "White"
        Write-ColorOutput "`nOr run this script again with -InstallService -StartService flags" "Gray"
    }

    Write-ColorOutput "`nFor more information, see README.md" "Gray"
}

# Main execution
try {
    Show-Banner

    # Show configuration
    Write-ColorOutput "Installation Configuration:" "Cyan"
    Write-ColorOutput "  Configuration:   $Configuration" "Gray"
    Write-ColorOutput "  Publish Path:    $PublishPath" "Gray"
    Write-ColorOutput "  Install Service: $InstallService" "Gray"
    Write-ColorOutput "  Start Service:   $StartService" "Gray"
    Write-ColorOutput "  Skip Build:      $SkipBuild" "Gray"
    Write-ColorOutput "  Skip Publish:    $SkipPublish" "Gray"

    # Execute installation steps
    Test-Prerequisites

    if (-not $SkipBuild) {
        Restore-Packages
        Build-Project
    } else {
        Write-ColorOutput "`nSkipping build (using existing build)" "Yellow"
    }

    if (-not $SkipPublish) {
        Publish-Project
    } else {
        Write-ColorOutput "`nSkipping publish (using existing publish)" "Yellow"
    }

    # Optional service installation
    if ($InstallService) {
        $serviceInstalled = Install-WindowsService

        if ($serviceInstalled -and $StartService) {
            Start-WindowsService
        }
    }

    # Show results
    Test-Installation
    Show-NextSteps

    Write-ColorOutput "`n============================================================" "Cyan"
    Write-ColorOutput "          Installation Completed Successfully!" "Cyan"
    Write-ColorOutput "============================================================`n" "Cyan"

    exit 0
}
catch {
    Write-ColorOutput "`n============================================================" "Red"
    Write-ColorOutput "                 Installation Failed!" "Red"
    Write-ColorOutput "============================================================`n" "Red"
    Write-ColorOutput "Error: $_" "Red"
    Write-ColorOutput "`nStack Trace:" "DarkGray"
    Write-ColorOutput $_.ScriptStackTrace "DarkGray"
    exit 1
}
