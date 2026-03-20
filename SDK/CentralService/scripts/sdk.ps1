param(
    [switch]$Build,
    [switch]$E2E,
    [switch]$Pack,
    [switch]$All,
    [string]$BaseUrl = 'http://127.0.0.1:5000',
    [string[]]$Languages = @('dotnet', 'javascript', 'python', 'rust', 'java', 'go'),
    [string[]]$SdkKinds = @('service', 'client')
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

$scriptPath = Join-Path $PSScriptRoot 'sdk.py'
$args = @(
    '-BaseUrl', $BaseUrl,
    '-Languages', ($Languages -join ','),
    '-SdkKinds', ($SdkKinds -join ',')
)

if ($Build) { $args += '-Build' }
if ($E2E) { $args += '-E2E' }
if ($Pack) { $args += '-Pack' }
if ($All) { $args += '-All' }

python -X utf8 $scriptPath @args
Assert-LastExitCode 'sdk.py'
