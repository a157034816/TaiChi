param(
    [string]$OutputDirectory = "release",
    [string]$CentralServiceSourceImage = "central-service-centralservice:latest",
    [string]$AdminSiteSourceImage = "central-service-admin-site:latest",
    [string]$CentralServiceTargetImage = "taichi/centralservice:latest",
    [string]$AdminSiteTargetImage = "taichi/centralservice-admin-site:latest",
    [string]$CaddyImage = "caddy:2",
    [switch]$SkipBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Info([string]$Message)
{
    Write-Host "[INFO] $Message" -ForegroundColor Cyan
}

function Resolve-DockerExecutable()
{
    $command = Get-Command docker -ErrorAction SilentlyContinue
    if ($command)
    {
        return $command.Source
    }

    $candidate = "C:\Program Files\Docker\Docker\resources\bin\docker.exe"
    if (Test-Path -LiteralPath $candidate)
    {
        return $candidate
    }

    throw "未找到 docker 可执行文件，请先安装 Docker Desktop / Docker Engine。"
}

function Invoke-Docker([string[]]$Arguments)
{
    & $script:DockerExe @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "docker 命令执行失败: docker $($Arguments -join ' ')"
    }
}

function Invoke-DockerComposeBuild([string]$ComposeFilePath)
{
    # 导出离线发布包前，先基于当前工作区源码重建前后端镜像，
    # 避免把旧标签镜像误打进发布包并上传到服务器。
    Write-Info "根据当前源码重建 centralservice / admin-site 镜像"
    Invoke-Docker @("compose", "-f", $ComposeFilePath, "build", "centralservice", "admin-site")
}

function Ensure-Image([string]$SourceImage, [string]$TargetImage)
{
    Write-Info "检查镜像: $SourceImage"
    Invoke-Docker @("image", "inspect", $SourceImage)

    if (-not [string]::Equals($SourceImage, $TargetImage, [StringComparison]::OrdinalIgnoreCase))
    {
        Write-Info "标记镜像: $SourceImage -> $TargetImage"
        Invoke-Docker @("tag", $SourceImage, $TargetImage)
    }
}

function Copy-ReleaseFile([string]$SourcePath, [string]$OutputPath)
{
    Copy-Item -LiteralPath $SourcePath -Destination $OutputPath -Force
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory))
{
    $OutputDirectory
}
else
{
    Join-Path $scriptRoot $OutputDirectory
}

$DockerExe = Resolve-DockerExecutable
$imageArchivePath = Join-Path $outputRoot "central-service-images.tar"
$hashPath = Join-Path $outputRoot "central-service-images.sha256"
$deployScriptPath = Join-Path $scriptRoot "deploy.sh"
$manageScriptPath = Join-Path $scriptRoot "manage.sh"
$composeFilePath = Join-Path $scriptRoot "compose.yml"

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

if (-not $SkipBuild)
{
    Invoke-DockerComposeBuild -ComposeFilePath $composeFilePath
}

Ensure-Image -SourceImage $CentralServiceSourceImage -TargetImage $CentralServiceTargetImage
Ensure-Image -SourceImage $AdminSiteSourceImage -TargetImage $AdminSiteTargetImage
Ensure-Image -SourceImage $CaddyImage -TargetImage $CaddyImage

Write-Info "导出镜像归档: $imageArchivePath"
Invoke-Docker @(
    "save",
    "-o",
    $imageArchivePath,
    $CentralServiceTargetImage,
    $AdminSiteTargetImage,
    $CaddyImage)

Write-Info "写入部署文件"
Copy-ReleaseFile -SourcePath (Join-Path $scriptRoot "compose.release.yml") -OutputPath (Join-Path $outputRoot "compose.release.yml")
Copy-ReleaseFile -SourcePath (Join-Path $scriptRoot "Caddyfile") -OutputPath (Join-Path $outputRoot "Caddyfile")
Copy-ReleaseFile -SourcePath (Join-Path $scriptRoot ".env.example") -OutputPath (Join-Path $outputRoot ".env.example")
Copy-ReleaseFile -SourcePath (Join-Path $scriptRoot "README.md") -OutputPath (Join-Path $outputRoot "README.md")
Copy-ReleaseFile -SourcePath $deployScriptPath -OutputPath (Join-Path $outputRoot "deploy.sh")
Copy-ReleaseFile -SourcePath $manageScriptPath -OutputPath (Join-Path $outputRoot "manage.sh")

$hash = Get-FileHash -LiteralPath $imageArchivePath -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  central-service-images.tar" | Set-Content -LiteralPath $hashPath -Encoding utf8

Write-Info "发布包已生成: $outputRoot"
Write-Info "镜像标签:"
Write-Host "  - $CentralServiceTargetImage"
Write-Host "  - $AdminSiteTargetImage"
Write-Host "  - $CaddyImage"
