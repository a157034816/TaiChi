param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Info([string]$Message) { Write-Host "[INFO] $Message" }

function Ensure-Dir([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

$root = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$srcRoot = Join-Path $root 'src'
$outLibDir = Join-Path $root ("bin\\" + $Configuration + "\\net40")
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $csc)) {
    throw "Missing .NET Framework 4.0 C# compiler: $csc"
}

Ensure-Dir $outLibDir

$variants = @(
    @{
        Name = 'service'
        SourceDir = Join-Path $srcRoot 'CentralService.Service'
        OutputDll = Join-Path $outLibDir 'CentralService.Service.dll'
        OutputXml = Join-Path $outLibDir 'CentralService.Service.xml'
    },
    @{
        Name = 'client'
        SourceDir = Join-Path $srcRoot 'CentralService.Client'
        OutputDll = Join-Path $outLibDir 'CentralService.Client.dll'
        OutputXml = Join-Path $outLibDir 'CentralService.Client.xml'
    }
)

foreach ($variant in $variants) {
    if (-not (Test-Path -LiteralPath $variant.SourceDir)) {
        throw "Missing net40 SDK sources: $($variant.SourceDir)"
    }

    $sources = Get-ChildItem -LiteralPath $variant.SourceDir -Recurse -File -Filter '*.cs' | Select-Object -ExpandProperty FullName
    if ($null -eq $sources -or $sources.Count -eq 0) {
        throw "No C# files found: $($variant.SourceDir)"
    }

    Write-Info ("Building net40 " + $variant.Name + " SDK: " + $variant.OutputDll)
    & $csc `
        /nologo `
        /target:library `
        /out:$($variant.OutputDll) `
        /doc:$($variant.OutputXml) `
        /optimize+ `
        /platform:anycpu `
        /langversion:4 `
        /warn:4 `
        /reference:System.dll `
        /reference:System.Core.dll `
        /reference:System.Runtime.Serialization.dll `
        $sources

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build net40 $($variant.Name) SDK"
    }
}

$exampleSrc = Join-Path $root 'examples\CentralService.Net40E2e\Program.cs'
if (Test-Path -LiteralPath $exampleSrc) {
    $exampleOutDir = Join-Path $root ("examples\\CentralService.Net40E2e\\bin\\" + $Configuration)
    Ensure-Dir $exampleOutDir

    $exampleOut = Join-Path $exampleOutDir 'CentralService.Net40E2e.exe'
    Write-Info "Building net40 example: $exampleOut"
    & $csc `
        /nologo `
        /target:exe `
        /out:$exampleOut `
        /optimize+ `
        /platform:anycpu `
        /langversion:4 `
        /warn:4 `
        /reference:System.dll `
        /reference:System.Core.dll `
        /reference:System.Runtime.Serialization.dll `
        /reference:$($variants[0].OutputDll) `
        /reference:$($variants[1].OutputDll) `
        $exampleSrc

    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to build net40 example'
    }

    foreach ($variant in $variants) {
        Copy-Item -LiteralPath $variant.OutputDll -Destination (Join-Path $exampleOutDir (Split-Path -Leaf $variant.OutputDll)) -Force
        if (Test-Path -LiteralPath $variant.OutputXml) {
            Copy-Item -LiteralPath $variant.OutputXml -Destination (Join-Path $exampleOutDir (Split-Path -Leaf $variant.OutputXml)) -Force
        }
    }
}

Write-Info 'net40 build completed'
