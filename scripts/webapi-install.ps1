#Requires -RunAsAdministrator

<#
.SYNOPSIS
    Installs Roslyn Bridge Web API as a Windows Service

.DESCRIPTION
    Builds, publishes, and installs the Roslyn Bridge Web API as a Windows Service

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release

.PARAMETER ServiceName
    Windows Service name. Default: RoslynBridgeWebApi

.PARAMETER PublishPath
    Installation path. Default: C:\Services\RoslynBridge.WebApi

.EXAMPLE
    .\scripts\webapi-install.ps1
    Installs with default settings

.EXAMPLE
    .\scripts\webapi-install.ps1 -Configuration Debug
    Installs debug build
#>

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Release',

    [Parameter(Mandatory=$false)]
    [string]$ServiceName = 'RoslynBridgeWebApi',

    [Parameter(Mandatory=$false)]
    [string]$DisplayName = 'Roslyn Bridge Web API',

    [Parameter(Mandatory=$false)]
    [string]$PublishPath = 'C:\Services\RoslynBridge.WebApi'
)

$ErrorActionPreference = 'Stop'

# Resolve repo root relative to this script so it works from any CWD
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptRoot
Set-Location $RepoRoot

# Absolute path to the WebApi project file
$ProjectFile = Join-Path $RepoRoot 'RoslynBridge.WebApi\RoslynBridge.WebApi.csproj'

function Write-Step { param([string]$Message) Write-Host "`n>> $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "   $Message" -ForegroundColor Green }
function Write-Info { param([string]$Message) Write-Host "   $Message" -ForegroundColor Gray }

try {
    Write-Host "`n========================================" -ForegroundColor Cyan
    Write-Host "  Roslyn Bridge Web API Installer" -ForegroundColor Cyan
    Write-Host "========================================`n" -ForegroundColor Cyan

    # Check prerequisites
    Write-Step 'Checking prerequisites'
    $dotnetVersion = dotnet --version 2>$null
    if ($LASTEXITCODE -ne 0) { throw '.NET SDK not found. Install from https://dot.net' }
    Write-Success ".NET SDK v$dotnetVersion"

    if (-not (Test-Path $ProjectFile)) { throw "Project file not found: $ProjectFile" }
    Write-Success 'Project file found'

    # Stop existing service before file operations
    $existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existingService) {
        Write-Step 'Stopping existing service'
        if ($existingService.Status -eq 'Running') {
            Stop-Service -Name $ServiceName -Force
            Start-Sleep -Seconds 2
            Write-Success 'Service stopped'
        }
    }

    # Build
    Write-Step "Building project ($Configuration)"
    dotnet build $ProjectFile -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
    Write-Success 'Build completed'

    # Publish
    Write-Step "Publishing to $PublishPath"
    if (Test-Path $PublishPath) { Remove-Item -Path $PublishPath -Recurse -Force }
    dotnet publish $ProjectFile -c $Configuration -o $PublishPath
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }

    $exePath = Join-Path $PublishPath 'RoslynBridge.WebApi.exe'
    if (-not (Test-Path $exePath)) { throw "Executable not found: $exePath" }
    Write-Success "Published to $PublishPath"

    # Remove existing service registration
    if ($existingService) {
        Write-Step 'Removing existing service registration'
        sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 2
        Write-Success 'Service registration removed'
    }

    # Create service
    Write-Info 'Creating service...'
    $result = sc.exe create $ServiceName binPath= "`"$exePath`"" start= auto DisplayName= "$DisplayName"
    if ($LASTEXITCODE -ne 0) { throw "Failed to create service: $result" }

    # Configure service
    sc.exe description $ServiceName 'Web API middleware for Roslyn Bridge' | Out-Null
    sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null
    Write-Success 'Service installed'

    # Start service
    Write-Step 'Starting service'
    Start-Service -Name $ServiceName
    Start-Sleep -Seconds 3

    $service = Get-Service -Name $ServiceName
    if ($service.Status -eq 'Running') {
        Write-Success 'Service started successfully'
        Write-Host "`n========================================" -ForegroundColor Green
        Write-Host '  Installation Complete!' -ForegroundColor Green
        Write-Host '========================================' -ForegroundColor Green

        Write-Host "`nAPI Endpoints:" -ForegroundColor Cyan
        Write-Host '  http://localhost:5001' -ForegroundColor White
        Write-Host '  Swagger UI: http://localhost:5001' -ForegroundColor White

        Write-Host "`nService Management:" -ForegroundColor Cyan
        Write-Host "  Start:     Start-Service $ServiceName" -ForegroundColor Gray
        Write-Host "  Stop:      Stop-Service $ServiceName" -ForegroundColor Gray
        Write-Host "  Restart:   Restart-Service $ServiceName" -ForegroundColor Gray
        Write-Host "  Status:    Get-Service $ServiceName" -ForegroundColor Gray
        Write-Host "  Uninstall: sc.exe delete $ServiceName" -ForegroundColor Gray
        Write-Host ''
    }
    else { throw "Service failed to start. Status: $($service.Status)" }

    exit 0
}
catch {
    Write-Host "`n========================================" -ForegroundColor Red
    Write-Host '  Installation Failed!' -ForegroundColor Red
    Write-Host '========================================' -ForegroundColor Red
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}
