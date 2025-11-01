#Requires -Version 5.1
param(
    [ValidateSet('Debug','Release')]
    [string]$Configuration = 'Debug',

    [switch]$SkipBuild,

    # Attempt uninstall before install (default: true)
    [switch]$NoUninstall,

    # Show VSIXInstaller UI/log output
    [switch]$VerboseOutput,

    # Optional explicit log path; defaults to scripts\logs with timestamp
    [string]$LogPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info($msg) { Write-Host "[reinstall-vsix] $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "[reinstall-vsix] $msg" -ForegroundColor Yellow }
function Write-Err($msg)  { Write-Host "[reinstall-vsix] $msg" -ForegroundColor Red }

# Repo root is the script's parent directory two levels up
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptRoot
Set-Location $RepoRoot

$SolutionPath = Join-Path $RepoRoot 'RoslynBridge.sln'
if (-not (Test-Path $SolutionPath)) {
    Write-Err "Could not find solution at $SolutionPath"
    exit 1
}

function Get-MSBuildPath() {
    # Try vswhere
    $vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $vs = & $vswhere -latest -requires Microsoft.Component.MSBuild -property installationPath -format value 2>$null | Select-Object -First 1
        if ($vs) {
            $candidate = Join-Path $vs 'MSBuild\Current\Bin\MSBuild.exe'
            if (Test-Path $candidate) { return $candidate }
        }
    }
    # Common defaults by edition
    $candidates = @(
        'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe'
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    # Fallback to PATH
    $cmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
    if ($cmd -and $cmd.Source) { return $cmd.Source }
    return $null
}

function Get-VSIXInstallerPath() {
    # Try vswhere first
    $vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $vs = & $vswhere -latest -property installationPath -format value 2>$null | Select-Object -First 1
        if ($vs) {
            $candidate = Join-Path $vs 'Common7\IDE\VSIXInstaller.exe'
            if (Test-Path $candidate) { return $candidate }
        }
    }
    # Common defaults by edition
    $candidates = @(
        'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\VSIXInstaller.exe',
        'C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\VSIXInstaller.exe',
        'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\VSIXInstaller.exe'
    )
    foreach ($c in $candidates) { if (Test-Path $c) { return $c } }
    return $null
}

$msbuild = Get-MSBuildPath
if (-not $msbuild) {
    Write-Err 'MSBuild.exe not found. Ensure Visual Studio 2022 is installed.'
    exit 1
}
Write-Info "MSBuild: $msbuild"

# Build VSIX (unless skipped)
if (-not $SkipBuild) {
    Write-Info "Building VSIX ($Configuration)"
    & $msbuild $SolutionPath /t:Restore /t:Build /p:Configuration=$Configuration
}

# Locate VSIX output
$vsixDir = Join-Path $RepoRoot ("RoslynBridge\\bin\\$Configuration")
if (-not (Test-Path $vsixDir)) {
    Write-Err "Build output not found: $vsixDir"
    exit 1
}
$vsix = Get-ChildItem -Path $vsixDir -Filter *.vsix -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $vsix) {
    Write-Err "No VSIX found in $vsixDir"
    exit 1
}
Write-Info "VSIX: $($vsix.FullName)"

$vsixInstaller = Get-VSIXInstallerPath
if (-not $vsixInstaller) {
    Write-Err 'VSIXInstaller.exe not found. Ensure Visual Studio 2022 is installed.'
    exit 1
}
Write-Info "VSIXInstaller: $vsixInstaller"

# Extension identity from source.extension.vsixmanifest
$manifestPath = Join-Path $RepoRoot 'RoslynBridge\source.extension.vsixmanifest'
[xml]$manifest = Get-Content $manifestPath
$extensionId = $manifest.PackageManifest.Metadata.Identity.Id

$commonArgs = @('/shutdownprocesses')
if (-not $VerboseOutput) { $commonArgs += '/quiet' }

# Setup log path
if (-not $LogPath) {
    $logDir = Join-Path $RepoRoot 'scripts\logs'
    if (-not (Test-Path $logDir)) { New-Item -Path $logDir -ItemType Directory | Out-Null }
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $LogPath = Join-Path $logDir "VSIXInstall-$stamp.log"
}
Write-Info "Log file: $LogPath"

function Invoke-VSIXInstaller {
    param([object[]]$ArgumentList)

    # Filter out null/empty entries
    $clean = @($ArgumentList | Where-Object { $_ -ne $null -and $_ -ne '' })

    # Build a single argument string with quoting for spaces
    $parts = @()
    foreach ($a in $clean) {
        $s = [string]$a
        if ([string]::IsNullOrWhiteSpace($s)) { continue }
        if ($s.Contains(' ')) {
            $parts += '"' + $s + '"'
        } else {
            $parts += $s
        }
    }
    $argString = ($parts -join ' ')

    Write-Info ("VSIXInstaller args:")
    if ($parts.Count -gt 0) {
        Write-Host ("  " + $argString) -ForegroundColor Gray
    } else {
        Write-Host "  <none>" -ForegroundColor DarkGray
    }

    if ([string]::IsNullOrWhiteSpace($argString)) {
        throw "Internal error: computed empty ArgumentList for VSIXInstaller"
    }

    $proc = Start-Process -FilePath $vsixInstaller -ArgumentList $argString -Wait -PassThru
    return $proc.ExitCode
}

if (-not $NoUninstall) {
    Write-Info "Uninstalling existing extension ($extensionId) if present"
    try {
        $unArgs = $commonArgs + @("/logFile:$LogPath", "/uninstall:$extensionId")
        $code = Invoke-VSIXInstaller -ArgumentList $unArgs
        Write-Info "Uninstall exit code: $code"
    } catch {
        Write-Warn "Uninstall step reported an error (continuing): $($_.Exception.Message)"
    }
}

Write-Info 'Installing VSIX'
$inArgs = $commonArgs + @("/logFile:$LogPath", $vsix.FullName)
$installCode = Invoke-VSIXInstaller -ArgumentList $inArgs

if ($installCode -ne 0) {
    Write-Err "VSIX installation failed with exit code $installCode"
    if (Test-Path $LogPath) {
        Write-Warn '--- VSIXInstaller log (tail) ---'
        Get-Content $LogPath -Tail 80 | ForEach-Object { Write-Host $_ -ForegroundColor DarkGray }
        Write-Warn '--- end log ---'
    }
    # Fallback: try shell-opening the VSIX to show UI
    if (-not $VerboseOutput) {
        Write-Warn 'Falling back to interactive install (UI)'
        Start-Process -FilePath $vsix.FullName
    }
    exit $installCode
}

Write-Info 'Done. Launch Visual Studio to verify the extension.'
