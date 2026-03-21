param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ForwardedArgs
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDirectory
$launcherScript = Join-Path $repoRoot "Examples/NodeGraph.DemoClient.JavaScript/scripts/interactive-demo.py"

if (-not (Test-Path $launcherScript)) {
    throw "Cannot find launcher script: $launcherScript"
}

$pythonCommand = Get-Command python -ErrorAction SilentlyContinue | Select-Object -First 1
$command = $null
$commandArgs = @()

if ($null -ne $pythonCommand) {
    $command = $pythonCommand.Source
    $commandArgs = @("-X", "utf8", $launcherScript)
} else {
    $pyLauncher = Get-Command py -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $pyLauncher) {
        throw "Cannot find Python launcher. Install Python or ensure 'python'/'py' is available in PATH."
    }

    $command = $pyLauncher.Source
    $commandArgs = @("-3", "-X", "utf8", $launcherScript)
}

if ($ForwardedArgs) {
    $commandArgs += $ForwardedArgs
}

Push-Location $repoRoot
try {
    & $command @commandArgs
    exit $LASTEXITCODE
} finally {
    Pop-Location
}
