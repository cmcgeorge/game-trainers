#Requires -Version 5.1
<#
.SYNOPSIS
    Interactive launcher for every trainer in this repository.

.DESCRIPTION
    Discovers each trainer (any top-level folder that contains its own Run.ps1),
    lets you pick one from a menu, and forwards the shared build/run options to that
    trainer's Run.ps1. All trainer scripts share the same option surface:
    -Configuration, -Clean, -NoBuild, -NoRun, -Test, -Publish.

.PARAMETER Trainer
    Name (or list number) of the trainer to run, skipping the interactive menu.
    Accepts an exact folder name, a unique partial match, or a menu index. A partial
    match that hits more than one trainer is rejected with the candidates listed --
    e.g. 'Sword' matches both SwordOfAragonTrainer and SwordOfTheSamuraiTrainer, so
    pass enough of the name to be unique ('SwordOfAragon') or use the menu index.

.PARAMETER Configuration
    Build configuration: Debug or Release. Default: Release.

.PARAMETER Clean
    Delete bin/obj for the selected trainer before building.

.PARAMETER NoBuild
    Skip building; launch the most recent build directly.

.PARAMETER NoRun
    Build only; do not launch the app.

.PARAMETER Test
    Run the trainer's verification harness after building (warns if it has none).

.PARAMETER Publish
    Publish a single self-contained win-x64 exe; skips launch.

.PARAMETER List
    List the available trainers and exit.

.EXAMPLE
    .\Run.ps1
    Shows the menu, then builds and launches the chosen trainer.

.EXAMPLE
    .\Run.ps1 -Trainer Shogun -Configuration Debug
    Builds and launches the Shogun trainer in Debug without prompting.

.EXAMPLE
    .\Run.ps1 -Trainer 4 -Clean
    Cleans and runs the 4th trainer in the list.

.EXAMPLE
    .\Run.ps1 -Trainer SwordOfAragon -Test -NoRun
    Builds the Sword of Aragon trainer and runs its verification harness without
    launching the GUI. New trainers are picked up automatically: any top-level
    folder containing a Run.ps1 appears in the menu with no change to this script.

.EXAMPLE
    .\Run.ps1 -Trainer Pirates
    Builds and launches the Sid Meier's Pirates! trainer. 'Pirates' is enough to
    be unique, so the partial match resolves without the menu.

.EXAMPLE
    .\Run.ps1 -Trainer AlternateReality
    Builds and launches the Alternate Reality: The City trainer. Start CITY.EXE in
    DOSBox and resume a character first -- the trainer locates it automatically.

.EXAMPLE
    .\Run.ps1 -Trainer Airborne
    Builds and launches the Airborne Ranger trainer. Start AR.EXE in DOSBox and get
    into a mission first -- the trainer locates the game's data segment automatically.
    Note that 'A' alone is ambiguous now that both AirborneRangerTrainer and
    AlternateRealityTrainer exist; 'Airborne' and 'Alternate' each resolve uniquely.

.EXAMPLE
    .\Run.ps1 -Trainer Hillsfar
    Builds and launches the Hillsfar trainer. Start MAIN.EXE in DOSBox, answer its
    graphics-mode and disk-drive prompts, and load or generate a character at the
    camp menu first -- the trainer then locates the data segment automatically and
    needs no value searching. Its 'Character files' tab edits .HIL/.PRE files
    offline and works with the game closed.
#>
[CmdletBinding()]
param(
    [string]$Trainer,
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Clean,
    [switch]$NoBuild,
    [switch]$NoRun,
    [switch]$Test,
    [switch]$Publish,
    [switch]$List
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = $PSScriptRoot

# Discover trainers: top-level folders (excluding dot-prefixed) with their own Run.ps1.
$trainers = @(
    Get-ChildItem -LiteralPath $root -Directory |
        Where-Object { $_.Name -notlike '.*' } |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'Run.ps1') } |
        Sort-Object Name
)

if ($trainers.Count -eq 0) {
    throw "No trainers found under '$root' (looked for subfolders containing a Run.ps1)."
}

if ($List) {
    for ($i = 0; $i -lt $trainers.Count; $i++) {
        '{0,2}. {1}' -f ($i + 1), $trainers[$i].Name | Write-Host
    }
    return
}

function Resolve-Trainer([string]$name) {
    if ([string]::IsNullOrWhiteSpace($name)) { return $null }
    $name = $name.Trim()

    $index = 0
    if ([int]::TryParse($name, [ref]$index)) {
        if ($index -ge 1 -and $index -le $trainers.Count) { return $trainers[$index - 1] }
        throw "Selection '$name' is out of range (1-$($trainers.Count))."
    }

    $exact = @($trainers | Where-Object { $_.Name -ieq $name })
    if ($exact.Count -eq 1) { return $exact[0] }

    $pattern = '*' + [System.Management.Automation.WildcardPattern]::Escape($name) + '*'
    $partial = @($trainers | Where-Object { $_.Name -ilike $pattern })
    if ($partial.Count -eq 1) { return $partial[0] }
    if ($partial.Count -gt 1) {
        throw "Trainer '$name' is ambiguous. Matches: $($partial.Name -join ', ')."
    }

    throw "No trainer matches '$name'. Use -List to see the available trainers."
}

$selected = $null
if ($Trainer) {
    $selected = Resolve-Trainer $Trainer
}
else {
    Write-Host 'Select a trainer to run:' -ForegroundColor Cyan
    for ($i = 0; $i -lt $trainers.Count; $i++) {
        '{0,2}. {1}' -f ($i + 1), $trainers[$i].Name | Write-Host
    }
    $choice = Read-Host "Enter a number (1-$($trainers.Count)) or name"
    $selected = Resolve-Trainer $choice
}

if (-not $selected) { throw 'No trainer selected.' }

# Forward only the options the caller actually supplied.
$forward = @{ Configuration = $Configuration }
foreach ($switchName in 'Clean', 'NoBuild', 'NoRun', 'Test', 'Publish') {
    if ($PSBoundParameters.ContainsKey($switchName)) { $forward[$switchName] = $true }
}

$script = Join-Path $selected.FullName 'Run.ps1'
Write-Host "==> Running $($selected.Name)\Run.ps1" -ForegroundColor Cyan
& $script @forward
