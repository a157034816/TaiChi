param(
    [string]$RepoRoot = '',
    [string]$BaseUrl = '',
    [string[]]$Languages = @('dotnet', 'javascript', 'python', 'rust', 'java', 'go'),
    [string[]]$Scenarios = @(
        'smoke',
        'service_fanout',
        'transport_failover',
        'business_no_failover',
        'max_attempts',
        'circuit_open',
        'circuit_recovery',
        'half_open_reopen'
    ),
    [int]$HealthTimeoutSeconds = 45,
    [int]$BreakWaitSeconds = 65,
    [int]$TimeoutMs = 800
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-LastExitCode([string]$Action) {
    if ($LASTEXITCODE -ne 0) {
        throw "$Action failed with exit code $LASTEXITCODE"
    }
}

$python = Get-Command python -ErrorAction SilentlyContinue
if ($null -eq $python) {
    throw "Missing command: python. Install Python 3.10+ and ensure 'python' is in PATH."
}

$scriptPath = Join-Path $PSScriptRoot 'e2e.py'
$args = @()
if (-not [string]::IsNullOrWhiteSpace($RepoRoot)) {
    $args += @('-RepoRoot', $RepoRoot)
}
if (-not [string]::IsNullOrWhiteSpace($BaseUrl)) {
    $args += @('-BaseUrl', $BaseUrl)
}
$args += @(
    '-Languages', ($Languages -join ','),
    '-Scenarios', ($Scenarios -join ','),
    '-HealthTimeoutSeconds', [string]$HealthTimeoutSeconds,
    '-BreakWaitSeconds', [string]$BreakWaitSeconds,
    '-TimeoutMs', [string]$TimeoutMs
)
python -X utf8 $scriptPath @args
Assert-LastExitCode 'e2e.py'
