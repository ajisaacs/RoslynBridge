<#
.SYNOPSIS
    Synchronizes the Roslyn Bridge skill from project to user-level and project scripts.

.DESCRIPTION
    This script copies the canonical skill files from .claude/skills/roslyn-bridge/
    to both:
    1. The user-level skill directory: ~/.claude/skills/roslyn-bridge/ (entire directory)
    2. The project scripts directory: scripts/rb.ps1 (just the script)

.PARAMETER Force
    Overwrite existing files without prompting

.EXAMPLE
    .\scripts\sync-skill.ps1 -Force
    Syncs the skill and overwrites without prompting
#>

[CmdletBinding()]
param(
    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Define paths
$projectRoot = Split-Path $PSScriptRoot -Parent
$skillSource = Join-Path $projectRoot ".claude\skills\roslyn-bridge"
$userSkillDest = Join-Path $env:USERPROFILE ".claude\skills\roslyn-bridge"
$projectScriptSource = Join-Path $skillSource "scripts\rb.ps1"
$projectScriptDest = Join-Path $projectRoot "scripts\rb.ps1"

Write-Host "Roslyn Bridge Skill Sync" -ForegroundColor Cyan
Write-Host "=========================" -ForegroundColor Cyan
Write-Host ""

# Verify source exists
if (-not (Test-Path $skillSource)) {
    Write-Error "Source skill directory not found: $skillSource"
    exit 1
}

# Sync 1: Project skill → User-level skill (entire directory)
Write-Host "1. Syncing to user-level skill directory..." -ForegroundColor Cyan
Write-Host "   Source: $skillSource" -ForegroundColor Gray
Write-Host "   Dest:   $userSkillDest" -ForegroundColor Gray
Write-Host ""

try {
    # Remove existing directory if it exists
    if (Test-Path $userSkillDest) {
        Remove-Item -Path $userSkillDest -Recurse -Force
        Write-Host "   Removed existing: $userSkillDest" -ForegroundColor Yellow
    }

    # Copy entire directory structure
    Copy-Item -Path $skillSource -Destination $userSkillDest -Recurse -Force
    Write-Host "   [SUCCESS] User-level skill synced successfully" -ForegroundColor Green
} catch {
    Write-Host "   [ERROR] Failed to sync user-level skill: $_" -ForegroundColor Red
    exit 1
}

Write-Host ""

# Sync 2: Skill script → Project script
Write-Host "2. Syncing rb.ps1 to project scripts directory..." -ForegroundColor Cyan
Write-Host "   Source: $projectScriptSource" -ForegroundColor Gray
Write-Host "   Dest:   $projectScriptDest" -ForegroundColor Gray
Write-Host ""

if (-not (Test-Path $projectScriptSource)) {
    Write-Warning "rb.ps1 not found in skill directory: $projectScriptSource"
    Write-Warning "Skipping project script sync"
} else {
    try {
        Copy-Item -Path $projectScriptSource -Destination $projectScriptDest -Force
        Write-Host "   [SUCCESS] Project script synced successfully" -ForegroundColor Green
    } catch {
        Write-Host "   [ERROR] Failed to sync project script: $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=========================" -ForegroundColor Cyan
Write-Host "Sync complete!" -ForegroundColor Green
Write-Host ""
Write-Host "The skill is now available at:" -ForegroundColor Gray
Write-Host "  - Project skill:    $skillSource" -ForegroundColor Gray
Write-Host "  - User-level skill: $userSkillDest" -ForegroundColor Gray
Write-Host "  - Project script:   $projectScriptDest" -ForegroundColor Gray
Write-Host ""
