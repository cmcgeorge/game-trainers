#Requires -Version 5.1
<#
.SYNOPSIS
    Builds the Might & Magic 1 trainer and launches it.

.DESCRIPTION
    Restores/builds the WPF project with `dotnet build`, then starts the produced
    MM1Trainer.exe. The app's manifest requests administrator rights (needed to
    read/write the game process's memory), so Windows shows a UAC prompt on launch.

.PARAMETER Configuration
    Build configuration: Debug or Release. Default: Release.

.PARAMETER NoRun
    Build only; do not launch the app.

.PARAMETER Clean
    Delete bin/obj for the project before building.

.PARAMETER Test
    Run the FormatCheck verification harness after building (does not need the game).

.EXAMPLE
    .\build-and-run.ps1
    Builds Release and launches the trainer (UAC prompt appears).

.EXAMPLE
    .\build-and-run.ps1 -Configuration Debug -Test -NoRun
    Builds Debug, runs the format checks, and does not launch the GUI.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$NoRun,
    [switch]$Clean,
    [switch]$Test
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Resolve paths relative to this script so it works from any working directory.
$root        = $PSScriptRoot
$project     = Join-Path $root 'src\MightAndMagic1Trainer\MightAndMagic1Trainer.csproj'
$testProject = Join-Path $root 'test\FormatCheck\FormatCheck.csproj'
$tfm         = 'net8.0-windows'
$exePath     = Join-Path $root "src\MightAndMagic1Trainer\bin\$Configuration\$tfm\MM1Trainer.exe"

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

# Verify the .NET SDK is available.
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK ('dotnet') was not found on PATH. Install .NET 8 SDK or newer from https://dotnet.microsoft.com/download"
}

if (-not (Test-Path $project)) {
    throw "Project not found at '$project'. Run this script from the repository root."
}

if ($Clean) {
    Write-Step "Cleaning bin/obj"
    foreach ($dir in @(
            (Join-Path $root 'src\MightAndMagic1Trainer\bin'),
            (Join-Path $root 'src\MightAndMagic1Trainer\obj'))) {
        if (Test-Path $dir) { Remove-Item $dir -Recurse -Force }
    }
}

Write-Step "Building ($Configuration)"
dotnet build $project -c $Configuration -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }

if ($Test) {
    Write-Step "Running FormatCheck verification harness"
    dotnet run --project $testProject -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "FormatCheck reported failures (exit code $LASTEXITCODE)." }
}

if ($NoRun) {
    Write-Step "Build complete. Skipping launch (-NoRun)."
    Write-Host "Executable: $exePath"
    return
}

if (-not (Test-Path $exePath)) {
    throw "Build succeeded but the executable was not found at '$exePath'."
}

Write-Step "Launching the trainer"
Write-Host "A UAC prompt will appear — the trainer needs admin rights to access the game's memory." -ForegroundColor Yellow
# Start-Process uses ShellExecute, which honours the app's requireAdministrator
# manifest and triggers the elevation prompt automatically.
Start-Process -FilePath $exePath
Write-Step "Started. (Launch the game in your emulator, then Attach in the trainer.)"
