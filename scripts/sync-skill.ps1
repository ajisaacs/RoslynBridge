#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Syncs the Roslyn Bridge skill from project .claude directory to user-level and project scripts

.DESCRIPTION
    Compares and syncs:
    - .claude/skills/roslyn-bridge/ -> ~/.claude/skills/roslyn-bridge/
    - .claude/skills/roslyn-bridge/scripts/rb -> scripts/rb

    Shows diffs and prompts before overwriting to prevent accidental data loss.

.EXAMPLE
    .\scripts\sync-skill.ps1

.EXAMPLE
    .\scripts\sync-skill.ps1 -Force
    Sync without prompting (overwrites destination files)
#>

param(
    [switch]$Force,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Define source and destination paths
$projectRoot = $PSScriptRoot | Split-Path -Parent
$sourceSkillDir = Join-Path $projectRoot ".claude/skills/roslyn-bridge"
$destSkillDir = Join-Path $env:USERPROFILE ".claude/skills/roslyn-bridge"
$sourceRbScript = Join-Path $sourceSkillDir "scripts/rb"
$destRbScript = Join-Path $projectRoot "scripts/rb"

Write-Host "Roslyn Bridge Skill Sync" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""

# Function to compare and sync files
function Sync-File {
    param(
        [string]$SourcePath,
        [string]$DestPath,
        [string]$Description
    )

    Write-Host "Checking: $Description" -ForegroundColor Yellow
    Write-Host "  Source: $SourcePath"
    Write-Host "  Dest:   $DestPath"

    if (-not (Test-Path $SourcePath)) {
        Write-Host "  [!] Source file not found!" -ForegroundColor Red
        return $false
    }

    # Ensure destination directory exists
    $destDir = Split-Path $DestPath -Parent
    if (-not (Test-Path $destDir)) {
        Write-Host "  Creating destination directory: $destDir" -ForegroundColor Gray
        if (-not $DryRun) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
    }

    # Check if destination exists and compare
    if (Test-Path $DestPath) {
        $sourceHash = Get-FileHash $SourcePath -Algorithm SHA256
        $destHash = Get-FileHash $DestPath -Algorithm SHA256

        if ($sourceHash.Hash -eq $destHash.Hash) {
            Write-Host "  [OK] Files are identical - no sync needed" -ForegroundColor Green
            Write-Host ""
            return $true
        }

        Write-Host "  [!] Files are different!" -ForegroundColor Yellow

        # Show file sizes and modification times
        $sourceInfo = Get-Item $SourcePath
        $destInfo = Get-Item $DestPath
        Write-Host "  Source: $($sourceInfo.Length) bytes, Modified: $($sourceInfo.LastWriteTime)" -ForegroundColor Gray
        Write-Host "  Dest:   $($destInfo.Length) bytes, Modified: $($destInfo.LastWriteTime)" -ForegroundColor Gray

        if (-not $Force -and -not $DryRun) {
            Write-Host ""
            Write-Host "  Show diff? [Y/n]: " -NoNewline -ForegroundColor Cyan
            $showDiff = Read-Host

            if ($showDiff -ne 'n' -and $showDiff -ne 'N') {
                # Try to show diff using git if available
                if (Get-Command git -ErrorAction SilentlyContinue) {
                    Write-Host ""
                    git diff --no-index --color=always $DestPath $SourcePath | Out-Host
                    Write-Host ""
                } else {
                    Write-Host "  (git not available for diff)" -ForegroundColor Gray
                }
            }

            Write-Host ""
            Write-Host "  Overwrite destination with source? [y/N]: " -NoNewline -ForegroundColor Cyan
            $response = Read-Host

            if ($response -ne 'y' -and $response -ne 'Y') {
                Write-Host "  [SKIP] Skipped" -ForegroundColor Gray
                Write-Host ""
                return $false
            }
        }
    } else {
        Write-Host "  [INFO] Destination does not exist - will create" -ForegroundColor Cyan
        if (-not $Force -and -not $DryRun) {
            Write-Host "  Create destination file? [Y/n]: " -NoNewline -ForegroundColor Cyan
            $response = Read-Host
            if ($response -eq 'n' -or $response -eq 'N') {
                Write-Host "  [SKIP] Skipped" -ForegroundColor Gray
                Write-Host ""
                return $false
            }
        }
    }

    if ($DryRun) {
        Write-Host "  [DRY RUN] Would copy: $SourcePath -> $DestPath" -ForegroundColor Magenta
    } else {
        Copy-Item $SourcePath $DestPath -Force
        Write-Host "  [OK] Synced successfully" -ForegroundColor Green
    }

    Write-Host ""
    return $true
}

# Function to sync entire directory
function Sync-Directory {
    param(
        [string]$SourceDir,
        [string]$DestDir,
        [string]$Description
    )

    Write-Host "Syncing Directory: $Description" -ForegroundColor Yellow
    Write-Host "  Source: $SourceDir"
    Write-Host "  Dest:   $DestDir"
    Write-Host ""

    if (-not (Test-Path $SourceDir)) {
        Write-Host "  [!] Source directory not found!" -ForegroundColor Red
        return $false
    }

    # Get all files in source directory
    $sourceFiles = Get-ChildItem -Path $SourceDir -Recurse -File

    foreach ($sourceFile in $sourceFiles) {
        $relativePath = $sourceFile.FullName.Substring($SourceDir.Length).TrimStart('\', '/')
        $destFile = Join-Path $DestDir $relativePath

        Sync-File -SourcePath $sourceFile.FullName -DestPath $destFile -Description "  $relativePath"
    }

    return $true
}

# Main sync logic
Write-Host "Source: $sourceSkillDir" -ForegroundColor Gray
Write-Host "Destinations:" -ForegroundColor Gray
Write-Host "  - User skill: $destSkillDir" -ForegroundColor Gray
Write-Host "  - Project script: $destRbScript" -ForegroundColor Gray
Write-Host ""

if ($DryRun) {
    Write-Host "[DRY RUN MODE - No files will be modified]" -ForegroundColor Magenta
    Write-Host ""
}

if ($Force) {
    Write-Host "[FORCE MODE - Will overwrite without prompting]" -ForegroundColor Yellow
    Write-Host ""
}

# Sync skill directory to user-level
$syncedSkill = Sync-Directory -SourceDir $sourceSkillDir -DestDir $destSkillDir -Description "Skill to user-level"

# Sync rb script to project scripts directory
$syncedRb = Sync-File -SourcePath $sourceRbScript -DestPath $destRbScript -Description "rb script to project scripts/"

Write-Host "Sync Complete!" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Host "This was a dry run. Run without -DryRun to actually sync files." -ForegroundColor Magenta
}
