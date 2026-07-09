#Requires -Version 5.1
<#
.SYNOPSIS
    Build (if needed) and launch the Sword of the Samurai WPF age trainer.

.DESCRIPTION
    The trainer is a WPF app under SwordSamuraiTrainer\ that freezes the protagonist's life-stage
    at "Youth" by writing to the live DOSBox process (a true freeze, unlike save-editing which only
    resets the age at load time - see SAVE_GAME_EDITING.md).

    This script lives in the solution root. It:
      * ensures the trainer is built (builds Release on first run, or when -Rebuild is given),
      * finds the produced SotSAgeTrainer.exe (whatever RID subfolder it lands in),
      * notes whether DOSBox is currently running (the trainer waits for it either way),
      * launches the trainer.

    Once the window is open: load your game in DOSBox (Restore/Continue), then click "Freeze age"
    with Target = Youth. The status dot turns green when it has attached and located the age byte.

.PARAMETER Configuration
    Build configuration to build/run. Default: Release.

.PARAMETER Rebuild
    Force a rebuild even if an up-to-date executable already exists.

.PARAMETER NoBuild
    Skip building entirely and just launch the existing executable. Fails if none is found.

.PARAMETER Wait
    Wait for the trainer window to close before returning (default: launch and return immediately).

.EXAMPLE
    .\Run.ps1
    Build if necessary, then launch the trainer.

.EXAMPLE
    .\Run.ps1 -Rebuild
    Force a fresh Release build, then launch.

.EXAMPLE
    .\Run.ps1 -NoBuild
    Launch the already-built trainer without touching the build.
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',
    [switch]$Rebuild,
    [switch]$NoBuild,
    [switch]$Wait
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root       = $PSScriptRoot
$project    = Join-Path $root 'SwordSamuraiTrainer\src\SotSAgeTrainer\SotSAgeTrainer.csproj'
$binDir     = Join-Path $root "SwordSamuraiTrainer\src\SotSAgeTrainer\bin\$Configuration"
$exeName    = 'SotSAgeTrainer.exe'

if (-not (Test-Path $project)) {
    throw "Trainer project not found at '$project'. Run this script from the solution root."
}

function Find-TrainerExe {
    if (-not (Test-Path $binDir)) { return $null }
    Get-ChildItem -Path $binDir -Recurse -Filter $exeName -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1 -ExpandProperty FullName
}

$exe = Find-TrainerExe

# ---- build -------------------------------------------------------------------
if (-not $NoBuild -and ($Rebuild -or -not $exe)) {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
        throw "The .NET SDK ('dotnet') is not on PATH. Install .NET 8 SDK, then re-run."
    }
    Write-Host "Building trainer ($Configuration)..." -ForegroundColor Cyan
    & dotnet build -c $Configuration $project
    if ($LASTEXITCODE -ne 0) { throw "Build failed (dotnet exit code $LASTEXITCODE)." }
    $exe = Find-TrainerExe
}

if (-not $exe) {
    throw "Could not find $exeName under '$binDir'. Build the solution first (drop -NoBuild)."
}

# ---- DOSBox status (informational) -------------------------------------------
$dosbox = Get-Process -ErrorAction SilentlyContinue |
    Where-Object { $_.ProcessName -like 'dosbox*' }
if ($dosbox) {
    $pids = ($dosbox | ForEach-Object { $_.Id }) -join ', '
    Write-Host "DOSBox is running (PID $pids). Make sure a game is loaded (Restore/Continue)." -ForegroundColor Green
} else {
    Write-Host "DOSBox is not running yet - the trainer will wait for it. Launch the game and load a save." -ForegroundColor Yellow
}

# ---- launch ------------------------------------------------------------------
Write-Host "Launching trainer: $exe" -ForegroundColor Cyan
if ($Wait) {
    Start-Process -FilePath $exe -Wait
} else {
    $p = Start-Process -FilePath $exe -PassThru
    Write-Host "Trainer started (PID $($p.Id)). Close its window to stop freezing." -ForegroundColor Green
}
