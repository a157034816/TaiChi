param(
    [int]$Port,
    [string]$StateFile
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

$scriptPath = Join-Path $PSScriptRoot 'e2e_fault_proxy.py'
python -X utf8 $scriptPath -Port $Port -StateFile $StateFile
Assert-LastExitCode 'e2e_fault_proxy.py'
