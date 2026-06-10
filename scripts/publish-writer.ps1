<#
.SYNOPSIS
  Publishes the Writing Workspace (Writer) as a self-contained app for pilot testers.

.DESCRIPTION
  Produces a folder that runs without a separate .NET install (self-contained). The folder
  includes the common /policies templates. At runtime the program root is the folder itself,
  so settings/providers.json and project data are created next to the executable.

  Extract/run from a writable folder (e.g. Desktop, Documents) — not Program Files.

.PARAMETER Runtime
  The .NET runtime identifier. Default: win-x64.

.PARAMETER OutDir
  The output folder. Default: dist/LlmIde.Writer.

.PARAMETER Zip
  When set, also creates <OutDir>.zip for distribution.

.EXAMPLE
  pwsh scripts/publish-writer.ps1 -Zip
#>
param(
    [string]$Runtime = "win-x64",
    [string]$OutDir = "dist/LlmIde.Writer",
    [switch]$Zip
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$project = "src/LlmIde.Wpf.Writer/LlmIde.Wpf.Writer.csproj"

if (Test-Path $OutDir) {
    Remove-Item $OutDir -Recurse -Force
}

Write-Host "게시 중: $project -> $OutDir ($Runtime, self-contained)..."

dotnet publish $project `
    -c Release `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -o $OutDir

if ($LASTEXITCODE -ne 0) {
    throw "게시 실패 (exit $LASTEXITCODE)"
}

if ($Zip) {
    $zipPath = "$OutDir.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$OutDir/*" -DestinationPath $zipPath
    Write-Host "압축 완료: $zipPath"
}

Write-Host ""
Write-Host "게시 완료: $OutDir"
Write-Host "실행: $OutDir\LlmIde.Wpf.Writer.exe"
