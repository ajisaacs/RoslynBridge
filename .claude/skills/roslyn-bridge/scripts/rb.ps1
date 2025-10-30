param(
    [Parameter(Position = 0)]
    [ValidateSet('help','instances','health','ping','summary','errors','warnings','diagnostics','projects','overview','symbol','symbolAt','refs','build','addpkg','rmpkg','clean','restore','mkdir','typemembers','typehierarchy','implementations','callhierarchy','symbolcontext','namespacetypes','searchcode','format','query')]
    [string]$Command = 'help',

    [string]$SolutionName,
    [string]$BaseUrl = 'http://localhost:5001',
    [string]$ApiUrl,

    [string]$SymbolName,
    [string]$FilePath,
    [int]$Line,
    [int]$Column,
    [ValidateSet('error','warning')]
    [string]$Severity,
    [int]$Limit,
    [int]$Offset,
    [string]$ProjectName,
    [string]$PackageName,
    [string]$Version,

    # Generic query support
    [string]$QueryType,
    [hashtable]$Fields,

    # Extended command options
    [switch]$IncludeInherited,
    [ValidateSet('up','down','both')] [string]$HierarchyDirection,
    [ValidateSet('callers','callees')] [string]$CallDirection,
    [ValidateSet('all','methods','classes','properties')] [string]$Scope,
    [string]$Pattern,
    [string]$DirectoryPath,
    [string]$Configuration,

    [switch]$Raw
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Show-Help {
    @'
rb.ps1 - Roslyn Bridge WebAPI helper

Usage:
  ./scripts/rb.ps1 instances
  ./scripts/rb.ps1 health
  ./scripts/rb.ps1 ping
  ./scripts/rb.ps1 summary [-SolutionName YourSolution]
  ./scripts/rb.ps1 errors  [-SolutionName YourSolution]
  ./scripts/rb.ps1 warnings[-SolutionName YourSolution]
  ./scripts/rb.ps1 diagnostics [-SolutionName YourSolution] [-Severity error|warning] [-FilePath path] [-Limit N] [-Offset M]
  ./scripts/rb.ps1 projects [-SolutionName YourSolution]
  ./scripts/rb.ps1 overview [-SolutionName YourSolution]
  ./scripts/rb.ps1 symbol -SymbolName Name [-SolutionName YourSolution]
  ./scripts/rb.ps1 symbolAt -FilePath path -Line N -Column M [-SolutionName YourSolution]
  ./scripts/rb.ps1 refs -FilePath path -Line N -Column M [-SolutionName YourSolution]
  ./scripts/rb.ps1 build -ProjectName Name [-SolutionName YourSolution]
  ./scripts/rb.ps1 addpkg -ProjectName Name -PackageName Package [-SolutionName YourSolution]
  ./scripts/rb.ps1 rmpkg -ProjectName Name -PackageName Package [-SolutionName YourSolution]
  ./scripts/rb.ps1 clean -ProjectName Name [-SolutionName YourSolution]
  ./scripts/rb.ps1 restore -ProjectName Name [-SolutionName YourSolution]
  ./scripts/rb.ps1 mkdir -DirectoryPath path [-SolutionName YourSolution]
  ./scripts/rb.ps1 typemembers -SymbolName Full.Type.Name [-IncludeInherited] [-SolutionName YourSolution]
  ./scripts/rb.ps1 typehierarchy -SymbolName Full.Type.Name [-HierarchyDirection up|down|both] [-SolutionName YourSolution]
  ./scripts/rb.ps1 implementations [-SymbolName Full.Type.Name | -FilePath path -Line N -Column M] [-SolutionName YourSolution]
  ./scripts/rb.ps1 callhierarchy -FilePath path -Line N -Column M [-CallDirection callers|callees] [-SolutionName YourSolution]
  ./scripts/rb.ps1 symbolcontext -FilePath path -Line N -Column M [-SolutionName YourSolution]
  ./scripts/rb.ps1 namespacetypes -SymbolName Namespace.Name [-SolutionName YourSolution]
  ./scripts/rb.ps1 searchcode -Pattern regex [-Scope all|methods|classes|properties] [-SolutionName YourSolution]
  ./scripts/rb.ps1 format -FilePath path [-SolutionName YourSolution]
  ./scripts/rb.ps1 query -QueryType getprojects [-SolutionName YourSolution] [-Fields @{ key='value' }]

Examples:
  ./scripts/rb.ps1 summary
  ./scripts/rb.ps1 errors
  ./scripts/rb.ps1 projects
  ./scripts/rb.ps1 symbol -SymbolName MyController
  ./scripts/rb.ps1 symbolAt -FilePath .\MyApp\Controllers\HomeController.cs -Line 10 -Column 5
  ./scripts/rb.ps1 refs -FilePath .\MyApp\Controllers\HomeController.cs -Line 10 -Column 5
  ./scripts/rb.ps1 build -ProjectName MyApp
  ./scripts/rb.ps1 query -QueryType getdiagnostics -Fields @{ severity='error' }
  ./scripts/rb.ps1 addpkg -ProjectName App -PackageName Newtonsoft.Json -Version 13.0.3
'@
}

if ($ApiUrl) {
    # Allow -ApiUrl to override -BaseUrl for consistency with other scripts
    $BaseUrl = $ApiUrl
}

function Get-DefaultSolutionName {
    try {
        $current = Get-Location
        # Search current directory first
        $sln = Get-ChildItem -Path $current -Filter *.sln -File -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($sln) { return [System.IO.Path]::GetFileNameWithoutExtension($sln.Name) }

        # Walk up parent directories
        $parent = Split-Path $current -Parent
        while ($parent) {
            $sln = Get-ChildItem -Path $parent -Filter *.sln -File -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($sln) { return [System.IO.Path]::GetFileNameWithoutExtension($sln.Name) }
            $parent = Split-Path $parent -Parent
        }
    } catch {}
    return $null
}

function Join-QueryString([hashtable]$Query) {
    $pairs = @()
    foreach ($k in $Query.Keys) {
        $v = $Query[$k]
        if ($null -ne $v -and $v -ne '') {
            $pairs += ("{0}={1}" -f $k, [uri]::EscapeDataString([string]$v))
        }
    }
    if ($pairs.Count -gt 0) { return '?' + ($pairs -join '&') }
    return ''
}

function Invoke-RoslynBridge {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [ValidateSet('GET','POST')] [string]$Method = 'GET',
        [hashtable]$Query = @{},
        [object]$Body = $null
    )

    $qs = Join-QueryString -Query $Query
    $uri = "$BaseUrl$Path$qs"
    try {
        $params = @{ Uri = $uri; Method = $Method }
        if ($Body) {
            $params.ContentType = 'application/json'
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
        }
        if ($Raw) {
            $resp = Invoke-WebRequest @params
            $resp.Content
        } else {
            $obj = Invoke-RestMethod @params
            $obj | ConvertTo-Json -Depth 10
        }
    } catch {
        Write-Error ("Request failed: {0}" -f $_.Exception.Message)
        exit 2
    }
}

if ($Command -eq 'help') {
    Show-Help
    exit 0
}

# Ensure SolutionName when required
$requiresSolution = @('summary','errors','warnings','diagnostics','projects','overview','symbol','symbolAt','refs','build','addpkg','rmpkg','clean','restore','mkdir','typemembers','typehierarchy','implementations','callhierarchy','symbolcontext','namespacetypes','searchcode','format','query')
if ($requiresSolution -contains $Command -and -not $SolutionName) {
    $SolutionName = Get-DefaultSolutionName
    if (-not $SolutionName) {
        Write-Error 'Could not detect solution name. Run from a folder containing a .sln (or a subfolder) or specify -SolutionName explicitly.'
        exit 2
    }
}

switch ($Command) {
    'instances' {
        Invoke-RoslynBridge -Path '/api/instances'
    }
    'health' {
        Invoke-RoslynBridge -Path '/api/health'
    }
    'ping' {
        Invoke-RoslynBridge -Path '/api/health/ping'
    }
    'summary' {
        Invoke-RoslynBridge -Path '/api/roslyn/diagnostics/summary' -Query @{ solutionName = $SolutionName }
    }
    'errors' {
        Invoke-RoslynBridge -Path '/api/roslyn/diagnostics' -Query @{ solutionName = $SolutionName; severity = 'error' }
    }
    'warnings' {
        Invoke-RoslynBridge -Path '/api/roslyn/diagnostics' -Query @{ solutionName = $SolutionName; severity = 'warning' }
    }
    'diagnostics' {
        $q = @{ solutionName = $SolutionName }
        if ($Severity) { $q.severity = $Severity }
        if ($FilePath) {
            $full = [System.IO.Path]::GetFullPath($FilePath)
            $q.filePath = $full -replace '\\','/'
        }
        if ($PSBoundParameters.ContainsKey('Limit')) { $q.limit = $Limit }
        if ($PSBoundParameters.ContainsKey('Offset')) { $q.offset = $Offset }
        Invoke-RoslynBridge -Path '/api/roslyn/diagnostics' -Query $q
    }
    'projects' {
        Invoke-RoslynBridge -Path '/api/roslyn/projects' -Query @{ solutionName = $SolutionName }
    }
    'overview' {
        Invoke-RoslynBridge -Path '/api/roslyn/solution/overview' -Query @{ solutionName = $SolutionName }
    }
    'symbol' {
        if (-not $SymbolName) { Write-Error 'symbol requires -SymbolName'; exit 2 }
        Invoke-RoslynBridge -Path '/api/roslyn/symbol/search' -Query @{ solutionName = $SolutionName; symbolName = $SymbolName }
    }
    'symbolAt' {
        if (-not $FilePath -or -not $Line -or ($PSBoundParameters.ContainsKey('Column') -eq $false)) { Write-Error 'symbolAt requires -FilePath -Line -Column'; exit 2 }
        $full = [System.IO.Path]::GetFullPath($FilePath)
        Invoke-RoslynBridge -Path '/api/roslyn/symbol' -Query @{ solutionName = $SolutionName; filePath = ($full -replace '\\','/'); line = $Line; column = $Column }
    }
    'refs' {
        if (-not $FilePath -or -not $Line -or ($PSBoundParameters.ContainsKey('Column') -eq $false)) { Write-Error 'refs requires -FilePath -Line -Column'; exit 2 }
        $full = [System.IO.Path]::GetFullPath($FilePath)
        Invoke-RoslynBridge -Path '/api/roslyn/references' -Query @{ solutionName = $SolutionName; filePath = ($full -replace '\\','/'); line = $Line; column = $Column }
    }
    'build' {
        if (-not $ProjectName) { Write-Error 'build requires -ProjectName'; exit 2 }
        $q = @{ solutionName = $SolutionName; projectName = $ProjectName }
        if ($Configuration) { $q.configuration = $Configuration }
        Invoke-RoslynBridge -Path '/api/roslyn/project/build' -Method 'POST' -Query $q
    }
    'addpkg' {
        if (-not $ProjectName -or -not $PackageName) { Write-Error 'addpkg requires -ProjectName and -PackageName'; exit 2 }
        $q = @{ solutionName = $SolutionName; projectName = $ProjectName; packageName = $PackageName }
        if ($Version) { $q.version = $Version }
        Invoke-RoslynBridge -Path '/api/roslyn/project/package/add' -Method 'POST' -Query $q
    }
    'rmpkg' {
        if (-not $ProjectName -or -not $PackageName) { Write-Error 'rmpkg requires -ProjectName and -PackageName'; exit 2 }
        $body = @{ queryType = 'removenugetpackage'; projectName = $ProjectName; packageName = $PackageName }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
    'clean' {
        if (-not $ProjectName) { Write-Error 'clean requires -ProjectName'; exit 2 }
        $body = @{ queryType = 'cleanproject'; projectName = $ProjectName }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
    'restore' {
        if (-not $ProjectName) { Write-Error 'restore requires -ProjectName'; exit 2 }
        $body = @{ queryType = 'restorepackages'; projectName = $ProjectName }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
    'mkdir' {
        if (-not $DirectoryPath) { Write-Error 'mkdir requires -DirectoryPath'; exit 2 }
        $body = @{ queryType = 'createdirectory'; directoryPath = $DirectoryPath }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
    'typemembers' {
        if (-not $SymbolName) { Write-Error 'typemembers requires -SymbolName'; exit 2 }
        $body = @{ queryType = 'gettypemembers'; symbolName = $SymbolName }
        if ($IncludeInherited) { $body.parameters = @{ includeInherited = 'true' } }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
    'typehierarchy' {
        if (-not $SymbolName) { Write-Error 'typehierarchy requires -SymbolName'; exit 2 }
        $body = @{ queryType = 'gettypehierarchy'; symbolName = $SymbolName }
        if ($HierarchyDirection) { $body.parameters = @{ direction = $HierarchyDirection } }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
    'implementations' {
        if (-not $SymbolName -and (-not $FilePath -or -not $Line -or ($PSBoundParameters.ContainsKey('Column') -eq $false))) {
            Write-Error 'implementations requires -SymbolName or -FilePath -Line -Column'; exit 2 }
        $body = @{ queryType = 'findimplementations' }
        if ($SymbolName) { $body.symbolName = $SymbolName } else {
            $full = [System.IO.Path]::GetFullPath($FilePath)
            $body.filePath = ($full -replace '\\','/')
            $body.line = $Line
            $body.column = $Column
        }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
    'callhierarchy' {
        if (-not $FilePath -or -not $Line -or ($PSBoundParameters.ContainsKey('Column') -eq $false)) { Write-Error 'callhierarchy requires -FilePath -Line -Column'; exit 2 }
        $full = [System.IO.Path]::GetFullPath($FilePath)
        $body = @{ queryType = 'getcallhierarchy'; filePath = ($full -replace '\\','/'); line = $Line; column = $Column }
        if ($CallDirection) { $body.parameters = @{ direction = $CallDirection } }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
    'symbolcontext' {
        if (-not $FilePath -or -not $Line -or ($PSBoundParameters.ContainsKey('Column') -eq $false)) { Write-Error 'symbolcontext requires -FilePath -Line -Column'; exit 2 }
        $full = [System.IO.Path]::GetFullPath($FilePath)
        $body = @{ queryType = 'getsymbolcontext'; filePath = ($full -replace '\\','/'); line = $Line; column = $Column }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
    'namespacetypes' {
        if (-not $SymbolName) { Write-Error 'namespacetypes requires -SymbolName (namespace)'; exit 2 }
        $body = @{ queryType = 'getnamespacetypes'; symbolName = $SymbolName }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
    'searchcode' {
        if (-not $Pattern) { Write-Error 'searchcode requires -Pattern <regex>'; exit 2 }
        $body = @{ queryType = 'searchcode'; symbolName = $Pattern }
        if ($Scope) { $body.parameters = @{ scope = $Scope } }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
    'format' {
        if (-not $FilePath) { Write-Error 'format requires -FilePath'; exit 2 }
        $full = [System.IO.Path]::GetFullPath($FilePath)
        $body = @{ queryType = 'formatdocument'; filePath = ($full -replace '\\','/') }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
    'query' {
        if (-not $QueryType) { Write-Error 'query requires -QueryType'; exit 2 }
        $body = @{ queryType = $QueryType }
        if ($Fields) {
            foreach ($k in $Fields.Keys) { $body[$k] = $Fields[$k] }
        }
        Invoke-RoslynBridge -Path '/api/roslyn/query' -Method 'POST' -Query @{ solutionName = $SolutionName } -Body $body
    }
}
