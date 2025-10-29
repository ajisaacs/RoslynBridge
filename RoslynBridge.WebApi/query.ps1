<#
.SYNOPSIS
    Query Roslyn Bridge API with automatic solution context

.DESCRIPTION
    Convenience wrapper for Roslyn Bridge WebAPI that automatically detects
    the solution name from the current directory and includes it in all queries.

.PARAMETER SolutionName
    Override automatic solution detection with a specific solution name

.PARAMETER ApiUrl
    Base URL of the Roslyn Bridge WebAPI. Default: http://localhost:5001

.EXAMPLE
    .\query.ps1 diagnostics
    Get diagnostics for the current solution

.EXAMPLE
    .\query.ps1 projects -SolutionName "RoslynBridge"
    Get projects for a specific solution

.EXAMPLE
    .\query.ps1 errors
    Get only errors for the current solution
#>

param(
    [Parameter(Position = 0, Mandatory = $false)]
    [string]$Command = "help",

    [Parameter(Mandatory = $false)]
    [string]$SolutionName,

    [Parameter(Mandatory = $false)]
    [string]$ApiUrl = "http://localhost:5001"
)

$ErrorActionPreference = "Stop"

# Color output helpers
function Write-Info { param([string]$Message) Write-Host $Message -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host $Message -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host $Message -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host $Message -ForegroundColor Red }

# Detect solution name from current directory
function Get-CurrentSolution {
    $currentDir = Get-Location

    # Look for .sln file in current directory
    $slnFiles = Get-ChildItem -Path $currentDir -Filter "*.sln" -ErrorAction SilentlyContinue

    if ($slnFiles.Count -gt 0) {
        return [System.IO.Path]::GetFileNameWithoutExtension($slnFiles[0].Name)
    }

    # Look in parent directories
    $parentDir = Split-Path $currentDir -Parent
    while ($parentDir) {
        $slnFiles = Get-ChildItem -Path $parentDir -Filter "*.sln" -ErrorAction SilentlyContinue
        if ($slnFiles.Count -gt 0) {
            return [System.IO.Path]::GetFileNameWithoutExtension($slnFiles[0].Name)
        }
        $parentDir = Split-Path $parentDir -Parent
    }

    return $null
}

# Make API request with solution context
function Invoke-RoslynApi {
    param(
        [string]$Endpoint,
        [string]$Method = "GET",
        [object]$Body = $null
    )

    $solution = if ($SolutionName) { $SolutionName } else { Get-CurrentSolution }

    if (-not $solution) {
        Write-Warning "Could not detect solution. Use -SolutionName parameter to specify."
        return $null
    }

    # Add solution name to URL
    $separator = if ($Endpoint.Contains("?")) { "&" } else { "?" }
    $url = "$ApiUrl$Endpoint$separator`solutionName=$solution"

    try {
        $params = @{
            Uri = $url
            Method = $Method
            ContentType = "application/json"
        }

        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }

        $response = Invoke-RestMethod @params
        return $response
    }
    catch {
        Write-Error "API request failed: $_"
        return $null
    }
}

# Format diagnostic output
function Format-Diagnostics {
    param([object]$Data, [string]$SeverityFilter = $null)

    if (-not $Data -or $Data.Count -eq 0) {
        Write-Success "No diagnostics found!"
        return
    }

    foreach ($diag in $Data) {
        $color = switch ($diag.severity) {
            "Error" { "Red" }
            "Warning" { "Yellow" }
            "Info" { "Cyan" }
            default { "Gray" }
        }

        $location = $diag.location
        $file = [System.IO.Path]::GetFileName($location.filePath)
        $line = $location.startLine

        Write-Host "[$($diag.severity)] " -ForegroundColor $color -NoNewline
        Write-Host "$file`:$line " -ForegroundColor Gray -NoNewline
        Write-Host $diag.id -ForegroundColor DarkGray -NoNewline
        Write-Host ""
        Write-Host "  $($diag.message)" -ForegroundColor White
    }
}

# Command handlers
function Show-Help {
    Write-Info "`nRoslyn Bridge Query Tool - Available Commands:"
    Write-Host ""
    Write-Host "  diagnostics       " -ForegroundColor White -NoNewline
    Write-Host "Get all diagnostics (errors, warnings, etc.)" -ForegroundColor Gray
    Write-Host "  errors            " -ForegroundColor White -NoNewline
    Write-Host "Get only compilation errors" -ForegroundColor Gray
    Write-Host "  warnings          " -ForegroundColor White -NoNewline
    Write-Host "Get only warnings" -ForegroundColor Gray
    Write-Host "  summary           " -ForegroundColor White -NoNewline
    Write-Host "Get diagnostics summary (counts by severity)" -ForegroundColor Gray
    Write-Host "  projects          " -ForegroundColor White -NoNewline
    Write-Host "List all projects in the solution" -ForegroundColor Gray
    Write-Host "  overview          " -ForegroundColor White -NoNewline
    Write-Host "Get solution overview and statistics" -ForegroundColor Gray
    Write-Host "  instances         " -ForegroundColor White -NoNewline
    Write-Host "List all registered VS instances" -ForegroundColor Gray
    Write-Host "  health            " -ForegroundColor White -NoNewline
    Write-Host "Check API health status" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Parameters:" -ForegroundColor Cyan
    Write-Host "  -SolutionName <name>  Override solution detection" -ForegroundColor Gray
    Write-Host "  -ApiUrl <url>         Roslyn Bridge API URL (default: http://localhost:5001)" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Cyan
    Write-Host "  .\query.ps1 errors" -ForegroundColor Gray
    Write-Host "  .\query.ps1 projects -SolutionName 'RoslynBridge'" -ForegroundColor Gray
    Write-Host "  .\query.ps1 summary" -ForegroundColor Gray
    Write-Host ""
}

function Get-Diagnostics {
    param([string]$Severity = $null)

    $solution = if ($SolutionName) { $SolutionName } else { Get-CurrentSolution }
    Write-Info "Getting diagnostics for solution: $solution"

    $endpoint = if ($Severity) {
        "/api/roslyn/diagnostics?severity=$Severity"
    } else {
        "/api/roslyn/diagnostics"
    }

    $response = Invoke-RoslynApi -Endpoint $endpoint

    if ($response -and $response.success) {
        Write-Host ""
        Format-Diagnostics -Data $response.data
        Write-Host ""
        Write-Success "Total: $($response.data.Count) diagnostic(s)"
    }
    else {
        Write-Error "Failed to get diagnostics: $($response.error)"
    }
}

function Get-DiagnosticsSummary {
    $solution = if ($SolutionName) { $SolutionName } else { Get-CurrentSolution }
    Write-Info "Getting diagnostics summary for solution: $solution"

    $response = Invoke-RoslynApi -Endpoint "/api/roslyn/diagnostics/summary"

    if ($response -and $response.success) {
        $summary = $response.data
        Write-Host ""
        Write-Host "Diagnostics Summary:" -ForegroundColor Cyan
        Write-Host "  Errors:   " -ForegroundColor Red -NoNewline
        Write-Host $summary.errors
        Write-Host "  Warnings: " -ForegroundColor Yellow -NoNewline
        Write-Host $summary.warnings
        Write-Host "  Info:     " -ForegroundColor Cyan -NoNewline
        Write-Host $summary.info
        Write-Host "  Hidden:   " -ForegroundColor Gray -NoNewline
        Write-Host $summary.hidden
        Write-Host "  --------"
        Write-Host "  Total:    " -ForegroundColor White -NoNewline
        Write-Host $summary.total
        Write-Host ""
    }
    else {
        Write-Error "Failed to get summary: $($response.error)"
    }
}

function Get-Projects {
    $solution = if ($SolutionName) { $SolutionName } else { Get-CurrentSolution }
    Write-Info "Getting projects for solution: $solution"

    $response = Invoke-RoslynApi -Endpoint "/api/roslyn/projects"

    if ($response -and $response.success) {
        Write-Host ""
        foreach ($project in $response.data) {
            Write-Host $project.name -ForegroundColor Cyan
            Write-Host "  Path: $($project.filePath)" -ForegroundColor Gray
            Write-Host "  Documents: $($project.documents.Count)" -ForegroundColor Gray
            Write-Host ""
        }
        Write-Success "Total: $($response.data.Count) project(s)"
    }
    else {
        Write-Error "Failed to get projects: $($response.error)"
    }
}

function Get-SolutionOverview {
    $solution = if ($SolutionName) { $SolutionName } else { Get-CurrentSolution }
    Write-Info "Getting overview for solution: $solution"

    $response = Invoke-RoslynApi -Endpoint "/api/roslyn/solution/overview"

    if ($response -and $response.success) {
        $overview = $response.data
        Write-Host ""
        Write-Host "Solution Overview:" -ForegroundColor Cyan
        Write-Host "  Projects:  " -ForegroundColor White -NoNewline
        Write-Host $overview.projectCount
        Write-Host "  Documents: " -ForegroundColor White -NoNewline
        Write-Host $overview.documentCount
        Write-Host ""
        Write-Host "Projects:" -ForegroundColor Cyan
        foreach ($proj in $overview.projects) {
            Write-Host "  $($proj.name) " -ForegroundColor White -NoNewline
            Write-Host "($($proj.fileCount) files)" -ForegroundColor Gray
        }
        Write-Host ""
    }
    else {
        Write-Error "Failed to get overview: $($response.error)"
    }
}

function Get-Instances {
    Write-Info "Getting registered VS instances"

    try {
        $response = Invoke-RestMethod -Uri "$ApiUrl/api/instances" -Method GET

        if ($response -and $response.success) {
            Write-Host ""
            foreach ($instance in $response.value) {
                $statusColor = if ($instance.solutionName) { "Green" } else { "Yellow" }
                Write-Host "Instance:" -ForegroundColor Cyan
                Write-Host "  Port:     " -ForegroundColor White -NoNewline
                Write-Host $instance.port
                Write-Host "  PID:      " -ForegroundColor White -NoNewline
                Write-Host $instance.processId
                Write-Host "  Solution: " -ForegroundColor White -NoNewline
                $solName = if ($instance.solutionName) { $instance.solutionName } else { "(none)" }
                Write-Host $solName -ForegroundColor $statusColor
                if ($instance.solutionPath) {
                    Write-Host "  Path:     " -ForegroundColor Gray -NoNewline
                    Write-Host $instance.solutionPath -ForegroundColor Gray
                }
                Write-Host ""
            }
            Write-Success "Total: $($response.value.Count) instance(s)"
        }
    }
    catch {
        Write-Error "Failed to get instances: $_"
    }
}

function Get-Health {
    Write-Info "Checking API health"

    try {
        $response = Invoke-RestMethod -Uri "$ApiUrl/api/health" -Method GET

        Write-Host ""
        Write-Host "Health Status:" -ForegroundColor Cyan
        Write-Host "  API:      " -ForegroundColor White -NoNewline
        Write-Host "Healthy" -ForegroundColor Green

        if ($response.vsInstances) {
            Write-Host "  VS Instances: $($response.vsInstances.Count)" -ForegroundColor White
            foreach ($instance in $response.vsInstances) {
                $status = if ($instance.healthy) { "Healthy" } else { "Unhealthy" }
                $color = if ($instance.healthy) { "Green" } else { "Red" }
                Write-Host "    Port $($instance.port): " -ForegroundColor Gray -NoNewline
                Write-Host $status -ForegroundColor $color
            }
        }
        Write-Host ""
    }
    catch {
        Write-Error "Failed to check health: $_"
    }
}

# Main command dispatcher
try {
    switch ($Command.ToLower()) {
        "help" { Show-Help }
        "diagnostics" { Get-Diagnostics }
        "errors" { Get-Diagnostics -Severity "error" }
        "warnings" { Get-Diagnostics -Severity "warning" }
        "summary" { Get-DiagnosticsSummary }
        "projects" { Get-Projects }
        "overview" { Get-SolutionOverview }
        "instances" { Get-Instances }
        "health" { Get-Health }
        default {
            Write-Warning "Unknown command: $Command"
            Show-Help
        }
    }
}
catch {
    Write-Error "Error: $_"
    exit 1
}
