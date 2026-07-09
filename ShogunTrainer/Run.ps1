<#
.SYNOPSIS
    Build (and optionally run) the Shogun Trainer WPF app.

.EXAMPLE
    .\run.ps1                 # build Release + launch
    .\run.ps1 -NoRun          # build Release only; print the exe path
    .\run.ps1 -Configuration Debug
    .\run.ps1 -Publish        # single self-contained exe (win-x64)
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoRun,
    [switch]$Publish,
    [switch]$Clean
)

$ErrorActionPreference = 'Stop'
$proj = Join-Path $PSScriptRoot 'src\ShogunTrainer\ShogunTrainer.csproj'

if ($Clean) {
    Get-ChildItem $PSScriptRoot -Recurse -Directory -Include bin, obj |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Write-Host 'Cleaned bin/obj.' -ForegroundColor Yellow
}

if ($Publish) {
    dotnet publish $proj -c $Configuration -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
    $exe = Join-Path $PSScriptRoot "src\ShogunTrainer\bin\$Configuration\net8.0-windows\win-x64\publish\ShogunTrainer.exe"
    Write-Host "Published: $exe" -ForegroundColor Green
    return
}

dotnet build $proj -c $Configuration
$exe = Join-Path $PSScriptRoot "src\ShogunTrainer\bin\$Configuration\net8.0-windows\ShogunTrainer.exe"
Write-Host "Built: $exe" -ForegroundColor Green

if (-not $NoRun) {
    Write-Host 'Launching (will prompt for Administrator)...' -ForegroundColor Cyan
    Start-Process $exe
}
