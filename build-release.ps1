#Requires -Version 5.1
<#
.SYNOPSIS
  一键发布并打包 ClassRT Translator Windows 版为可分发包。

.DESCRIPTION
  1) 检查 .NET 8 SDK
  2) dotnet publish（Release / win-x64 / 自包含 / 单文件）
  3) 拷贝产物到 dist\ClassRTTranslator-Windows-x64\
  4) 生成 SHA256 校验文件，并压缩为 dist\ClassRTTranslator-Windows-x64.zip

  运行环境：Windows 10/11 + PowerShell 5.1 或更高。
  使用方式：在仓库根目录右键「使用 PowerShell 运行」，或：
      .\build-release.ps1
      .\build-release.ps1 -SkipPublish   # 跳过发布，仅用现有产物重新打包

  产物目录：dist\
    ├── ClassRTTranslator-Windows-x64\    # 解压即用（单文件 exe）
    ├── ClassRTTranslator-Windows-x64.zip # 可分发的压缩包
    └── ClassRTTranslator-Windows-x64.sha256  # 校验文件
#>
[CmdletBinding()]
param(
  [switch]$SkipPublish
)
$ErrorActionPreference = 'Stop'

$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$AppProject = Join-Path $Root 'src\ClassRTTranslator.App\ClassRTTranslator.App.csproj'
$PublishDir = Join-Path $Root 'dist\publish'
$OutDir     = Join-Path $Root 'dist\ClassRTTranslator-Windows-x64'
$ZipPath    = Join-Path $Root 'dist\ClassRTTranslator-Windows-x64.zip'
$ShaPath    = Join-Path $Root 'dist\ClassRTTranslator-Windows-x64.sha256'

# ---------- 1. 检查 .NET SDK ----------
Write-Host ''
Write-Host '===== ClassRT Translator Windows 版 打包 =====' -ForegroundColor Cyan
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
  Write-Host '[错误] 未找到 .NET SDK。请先安装 .NET 8 SDK（含 .NET 桌面开发）：' -ForegroundColor Red
  Write-Host '       https://dotnet.microsoft.com/download/dotnet/8.0' -ForegroundColor Yellow
  exit 1
}
$sdkList = (& dotnet --list-sdks 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $sdkList) {
  Write-Host '[错误] 无法读取 .NET SDK 列表，请确认安装完成。' -ForegroundColor Red
  exit 1
}
Write-Host ('[信息] .NET SDK：' + ($sdkList -join '；')) -ForegroundColor Green

# ---------- 2. 发布 ----------
if (-not $SkipPublish) {
  Write-Host ''
  Write-Host '[步骤 1/3] 发布单文件 exe（Release / win-x64 / 自包含）...' -ForegroundColor Cyan
  & dotnet publish $AppProject -c Release -p:PublishProfile=Win-x64-SingleFile
  if ($LASTEXITCODE -ne 0) {
    Write-Host '[错误] dotnet publish 失败，请查看上方错误信息。' -ForegroundColor Red
    exit 1
  }
  Write-Host '[完成] 发布成功。' -ForegroundColor Green

  Write-Host ''
  Write-Host '[步骤 2/3] 拷贝产物到 dist\ClassRTTranslator-Windows-x64\ ...' -ForegroundColor Cyan
  if (Test-Path $OutDir) { Remove-Item $OutDir -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
  Copy-Item (Join-Path $PublishDir '*') $OutDir -Recurse -Force
} else {
  Write-Host ''
  Write-Host '[信息] 已跳过发布（-SkipPublish），使用现有产物重新打包。' -ForegroundColor Yellow
  if (-not (Test-Path $OutDir)) {
    Write-Host '[错误] 找不到 dist\ClassRTTranslator-Windows-x64\，请先正常发布一次。' -ForegroundColor Red
    exit 1
  }
}

# ---------- 3. 校验 + 压缩 ----------
Write-Host ''
Write-Host '[步骤 3/3] 生成 SHA256 校验文件并压缩为 zip ...' -ForegroundColor Cyan
$files = Get-ChildItem $OutDir -File -Recurse
if (-not $files) {
  Write-Host '[错误] 产物目录为空，打包中止。' -ForegroundColor Red
  exit 1
}
# 生成校验文件（内容：哈希 + 两空格 + 文件名）
$lines = foreach ($f in $files) {
  $hash = (Get-FileHash $f.FullName -Algorithm SHA256).Hash.ToLower()
  ('{0}  {1}' -f $hash, $f.Name)
}
$lines | Set-Content -Path $ShaPath -Encoding ascii

if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }
Compress-Archive -Path (Join-Path $OutDir '*') -DestinationPath $ZipPath -CompressionLevel Optimal

# ---------- 4. 汇总 ----------
Write-Host ''
Write-Host '===== 打包完成 =====' -ForegroundColor Cyan
Write-Host ('  exe 目录：' + $OutDir) -ForegroundColor Green
Write-Host ('  zip 压缩包：' + $ZipPath) -ForegroundColor Green
Write-Host ('  SHA256：' + $ShaPath) -ForegroundColor Green
$sizeMB = [math]::Round((Get-Item $ZipPath).Length / 1MB, 1)
Write-Host ('  zip 体积：' + $sizeMB + ' MB') -ForegroundColor Green
Write-Host ''
Write-Host '校验 zip：Get-FileHash dist\ClassRTTranslator-Windows-x64.zip -Algorithm SHA256' -ForegroundColor Yellow
