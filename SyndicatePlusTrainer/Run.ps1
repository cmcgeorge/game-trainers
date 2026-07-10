#Requires -Version 5.1
<#
.SYNOPSIS
    Builds and launches the Syndicate Plus trainer.

.DESCRIPTION
    Restores/builds the WPF project (x64) with `dotnet build`, then starts the
    produced executable. Start Syndicate Plus in DOSBox-X first, then run this
    script; in the trainer, pick the DOSBox process and click Attach.

.PARAMETER Configuration
    Build configuration: Debug or Release. Default: Release.

.PARAMETER Clean
    Delete bin/obj for the project before building.

.PARAMETER NoBuild
    Skip building; launch the most recent build directly.

.PARAMETER NoRun
    Build only; do not launch the app.

.PARAMETER Test
    Run the verification harness after building (warns if this trainer has none).

.PARAMETER Publish
    Publish a single self-contained win-x64 exe; skips launch.

.EXAMPLE
    .\Run.ps1
    Builds Release and launches the trainer.

.EXAMPLE
    .\Run.ps1 -Configuration Debug -NoRun
    Builds a Debug binary without launching.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean,
    [switch]$NoBuild,
    [switch]$NoRun,
    [switch]$Test,
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# ---- trainer-specific configuration ------------------------------------------
$root        = $PSScriptRoot
$project     = Join-Path $root 'Trainer\SyndicateTrainer\SyndicateTrainer.csproj'
$testProject = $null
$exeName     = 'SyndicateTrainer.exe'
$exePath     = Join-Path $root "Trainer\SyndicateTrainer\bin\x64\$Configuration\net8.0-windows\SyndicateTrainer.exe"
$binConfigDir = 'bin\x64'
$buildArgs   = @('-p:Platform=x64')
$launchHint  = 'Select the DOSBox process and click Attach in the trainer.'
# ------------------------------------------------------------------------------

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "The .NET SDK ('dotnet') was not found on PATH. Install it from https://dotnet.microsoft.com/download"
}

if (-not (Test-Path -LiteralPath $project)) {
    throw "Project not found at '$project'. Run this script from the repository root."
}

$binDir = Join-Path (Split-Path -Parent $project) (Join-Path $binConfigDir $Configuration)

function Resolve-Exe {
    if ($exePath -and (Test-Path -LiteralPath $exePath)) { return (Resolve-Path -LiteralPath $exePath).Path }
    if ($binDir -and (Test-Path -LiteralPath $binDir)) {
        $hit = Get-ChildItem -LiteralPath $binDir -Recurse -Filter $exeName -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

if ($Clean) {
    Write-Step 'Cleaning bin/obj'
    $cleanDirs = @([System.IO.Path]::GetDirectoryName($project))
    if ($testProject) { $cleanDirs += [System.IO.Path]::GetDirectoryName($testProject) }
    foreach ($dir in $cleanDirs) {
        foreach ($sub in @('bin', 'obj')) {
            $p = Join-Path $dir $sub
            if (Test-Path -LiteralPath $p) { Remove-Item -LiteralPath $p -Recurse -Force }
        }
    }
}

if ($Test) {
    if ($testProject -and (Test-Path -LiteralPath $testProject)) {
        Write-Step 'Running verification harness'
        dotnet run --project $testProject -c $Configuration
        if ($LASTEXITCODE -ne 0) { throw "Verification harness reported failures (exit code $LASTEXITCODE)." }
    }
    else {
        Write-Warning 'This trainer has no verification harness; -Test was ignored.'
    }
}

if ($Publish) {
    Write-Step "Publishing ($Configuration, win-x64 self-contained single file)"
    dotnet publish $project -c $Configuration -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true @buildArgs -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Publish failed (exit code $LASTEXITCODE)." }
    Write-Step 'Publish complete.'
    return
}

if (-not $NoBuild) {
    Write-Step "Building ($Configuration)"
    dotnet build $project -c $Configuration @buildArgs -v minimal
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)." }
}

if ($NoRun) {
    Write-Step 'Build complete. Skipping launch (-NoRun).'
    $built = Resolve-Exe
    if ($built) { Write-Host "Executable: $built" }
    return
}

$exe = Resolve-Exe
if (-not $exe) {
    throw "Executable '$exeName' not found. Build the trainer first (drop -NoBuild)."
}

Write-Step 'Launching the trainer'
Write-Host "A UAC prompt will appear - the trainer needs admin rights to access the game's memory." -ForegroundColor Yellow
Start-Process -FilePath $exe
Write-Step "Started. ($launchHint)"
