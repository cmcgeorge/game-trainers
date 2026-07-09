#Requires -Version 5.1
<#
.SYNOPSIS
    Build (and optionally run or publish) the Shogun Trainer WPF app.

.DESCRIPTION
    Restores, builds, and launches src\ShogunTrainer\ShogunTrainer.csproj.
    Attach to a running DOSBox-X instance from within the app once it opens.

.PARAMETER Configuration
    Build configuration: Debug or Release (default Release).

.PARAMETER NoRun
    Build only; do not launch the application.

.PARAMETER Clean
    Remove bin/ and obj/ before building.

.PARAMETER Publish
    Publish a single self-contained win-x64 exe; skips launch.

.EXAMPLE
    .\Run.ps1                       # build Release and launch
    .\Run.ps1 -NoRun                # build Release only; print the exe path
    .\Run.ps1 -Configuration Debug
    .\Run.ps1 -Publish              # single self-contained exe (win-x64)
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
Set-StrictMode -Version Latest

$root    = $PSScriptRoot
$project = Join-Path $root 'src\ShogunTrainer\ShogunTrainer.csproj'
$tfm     = 'net8.0-windows'
$exePath = Join-Path $root "src\ShogunTrainer\bin\$Configuration\$tfm\ShogunTrainer.exe"

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK ('dotnet') was not found on PATH. Install .NET 8 SDK or newer from https://dotnet.microsoft.com/download"
}

if (-not (Test-Path $project)) {
    throw "Project not found at '$project'. Run this script from the repository root."
}

if ($Clean) {
    Write-Step "Cleaning bin/obj"
    Get-ChildItem $root -Recurse -Directory -Include bin, obj |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

if ($Publish) {
    Write-Step "Publishing ($Configuration, win-x64 self-contained)"
    dotnet publish $project -c $Configuration -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Publish failed (exit code $LASTEXITCODE)." }
    $publishExe = Join-Path $root "src\ShogunTrainer\bin\$Configuration\$tfm\win-x64\publish\ShogunTrainer.exe"
    Write-Step "Published: $publishExe"
    return
}

Write-Step "Building ($Configuration)"
dotnet build $project -c $Configuration -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }

if ($NoRun) {
    Write-Step "Build complete. Skipping launch (-NoRun)."
    Write-Host "Executable: $exePath"
    return
}

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Build succeeded but the executable was not found at '$exePath'."
}

Write-Step "Launching the trainer"
Write-Host "A UAC prompt will appear — the trainer needs admin rights to access the game's memory." -ForegroundColor Yellow
Start-Process -FilePath $exePath
Write-Step "Started. (Launch the game in DOSBox-X, then Attach in the trainer.)"
