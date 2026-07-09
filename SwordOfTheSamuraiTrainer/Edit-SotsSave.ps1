<#
.SYNOPSIS
    Edit a Sword of the Samurai save (talltale.dat): list characters, max your samurai, and
    cripple rival daimyo. A friendly PowerShell front-end for save_edit.py.

.DESCRIPTION
    Sword of the Samurai keeps its game state in talltale.dat as an array of 0x60-byte character
    records (record 0 is YOU). Every attribute / honor / land field is a 0-128 scaled value; money
    and army size are DERIVED from them, so you raise the scaled fields rather than a "koku" number.

    This script drives trainer\build\save_edit.py to:
      * list every character record (tagged YOU / kin / other) with its stats,
      * max your own record,
      * minimise (or maximise) any records by number,
      * optionally install the result into your game folder (backing up the old save first).

    It lives in the solution root and calls the Python editor under trainer\build. Editing NEVER
    modifies the input file - results go to a new -Out file. When you combine -MaxMe with -Min/-Max
    the stages are chained automatically through temp files. See SAVE_GAME_EDITING.md for the full
    field map and the manual (hex-editor) method.

.PARAMETER Save
    Path to the talltale.dat to read or edit. Defaults to TALLTALE.DAT in the solution root
    (next to this script). Point it at your game folder's save to edit the real thing.

.PARAMETER List
    Show every character record and exit. This is the default when no edit switch is given.
    Run it first to find the record numbers of your rivals.

.PARAMETER MaxMe
    Max your samurai (record 0) - all stats to 0x80 (128).

.PARAMETER Youth
    Reset your samurai (record 0) to youth (age byte -> 0). The life-stage field lives at R+0x33
    (0=youth, 1=mature adult, 2=old) and is separate from the stats, so this composes with -MaxMe.
    Save-editing sets the age at load time; re-run it if the game ages your character again.

.PARAMETER Min
    Record number(s) to cripple (stats -> 1). Take these from -List, e.g. -Min 1,2,3.

.PARAMETER Max
    Record number(s) to max (stats -> 0x80). Handy for buffing your heir/kin, e.g. -Max 4.

.PARAMETER Out
    Output path for the edited save. Defaults to "<Save-folder>\<name>.edited.dat".
    Must differ from -Save; the input is always preserved.

.PARAMETER InstallTo
    Optional game folder. If given, the edited save is copied there as talltale.dat after any
    existing talltale.dat is backed up to talltale.dat.bak.

.PARAMETER PythonExe
    Python executable. Defaults to C:\Python314\python.exe, then falls back to python/py on PATH.

.PARAMETER ScriptPath
    Path to save_edit.py. Defaults to trainer\build\save_edit.py under the solution root.

.EXAMPLE
    .\Edit-SotsSave.ps1 -List

    List the character records in the root TALLTALE.DAT (the default save).

.EXAMPLE
    .\Edit-SotsSave.ps1 -Save 'C:\Games\Samurai\talltale.dat' -List

    List the records in your real game save so you can see which "other" records are your rivals.

.EXAMPLE
    .\Edit-SotsSave.ps1 -Save 'C:\Games\Samurai\talltale.dat' -MaxMe -Min 1,2,3

    Max your samurai and cripple rivals 1, 2 and 3, writing talltale.edited.dat next to the input.

.EXAMPLE
    .\Edit-SotsSave.ps1 -Save 'C:\Games\Samurai\talltale.dat' -MaxMe -Min 1,2,3 -InstallTo 'C:\Games\Samurai'

    Same edit, then installed straight into the game folder (old save -> talltale.dat.bak).

.EXAMPLE
    .\Edit-SotsSave.ps1 -Save 'C:\Games\Samurai\talltale.dat' -Max 4

    Buff your kinsman (record 4) instead of a rival.

.EXAMPLE
    .\Edit-SotsSave.ps1 -Save 'C:\Games\Samurai\talltale.dat' -MaxMe -Youth -InstallTo 'C:\Games\Samurai'

    Max your samurai, reset him to youth, and install the result into the game folder.

.NOTES
    Location : solution root; calls trainer\build\save_edit.py.
    Requires : Python (C:\Python314\python.exe) and save_edit.py. The save must stay 7650 bytes;
               the tool preserves that. Back up your real save before installing. Record numbers
               change as the game progresses - re-run -List on each new save.

.LINK
    SAVE_GAME_EDITING.md
.LINK
    TRAINER_OVERVIEW.md
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Save = (Join-Path $PSScriptRoot 'TALLTALE.DAT'),

    [switch]$List,
    [switch]$MaxMe,
    [switch]$Youth,
    [int[]]$Min = @(),
    [int[]]$Max = @(),
    [string]$Out,
    [string]$InstallTo,
    [string]$PythonExe,
    [string]$ScriptPath = (Join-Path $PSScriptRoot 'trainer\build\save_edit.py')
)

$ErrorActionPreference = 'Stop'

function Resolve-Python {
    param([string]$Explicit)
    if ($Explicit) {
        if (Test-Path $Explicit) { return $Explicit }
        throw "PythonExe not found: $Explicit"
    }
    if (Test-Path 'C:\Python314\python.exe') { return 'C:\Python314\python.exe' }
    foreach ($c in 'python', 'py') {
        $cmd = Get-Command $c -ErrorAction SilentlyContinue
        if ($cmd) { return $cmd.Source }
    }
    throw 'Python not found. Install Python or pass -PythonExe.'
}

function Invoke-SaveEdit {
    param([string[]]$Arguments)
    & $Python $ScriptPath @Arguments
    if ($LASTEXITCODE -ne 0) { throw "save_edit.py failed (exit $LASTEXITCODE): $($Arguments -join ' ')" }
}

# --- validate inputs -------------------------------------------------------
$Python = Resolve-Python $PythonExe
if (-not (Test-Path $ScriptPath)) { throw "save_edit.py not found at: $ScriptPath" }
if (-not (Test-Path $Save))       { throw "Save file not found: $Save" }
$Save = (Resolve-Path $Save).Path

$editRequested = $MaxMe -or $Youth -or $Min.Count -or $Max.Count

# --- LIST (default action) -------------------------------------------------
if ($List -or -not $editRequested) {
    Invoke-SaveEdit @('list', $Save)
    return
}

# --- EDIT: build the chain of operations -----------------------------------
if (-not $Out) {
    $dir  = Split-Path $Save -Parent
    $base = [System.IO.Path]::GetFileNameWithoutExtension($Save)
    $Out  = Join-Path $dir "$base.edited.dat"
}
if ([System.IO.Path]::GetFullPath($Out) -eq $Save) {
    throw "-Out must differ from -Save (the input is always preserved)."
}

$ops = @()
if ($MaxMe)     { $ops += , @{ cmd = 'max-me';   ids = @() } }
if ($Youth)     { $ops += , @{ cmd = 'youth-me'; ids = @() } }
if ($Max.Count) { $ops += , @{ cmd = 'max';      ids = $Max } }
if ($Min.Count) { $ops += , @{ cmd = 'min';      ids = $Min } }

$temps = @()
$cur   = $Save
try {
    for ($i = 0; $i -lt $ops.Count; $i++) {
        $op     = $ops[$i]
        $isLast = ($i -eq $ops.Count - 1)
        if ($isLast) {
            $dest = $Out
        } else {
            $tmp    = New-TemporaryFile
            $temps += $tmp
            $dest   = $tmp.FullName
        }
        $argList = @($op.cmd, $cur, $dest) + ($op.ids | ForEach-Object { "$_" })
        Invoke-SaveEdit $argList
        $cur = $dest
    }
}
finally {
    $temps | ForEach-Object { Remove-Item $_.FullName -ErrorAction SilentlyContinue }
}

Write-Host ''
Write-Host "Edited save written to: $Out" -ForegroundColor Green
Write-Host '--- resulting records ---'
Invoke-SaveEdit @('list', $Out)

# --- optional install into the game folder ---------------------------------
if ($InstallTo) {
    if (-not (Test-Path $InstallTo -PathType Container)) { throw "InstallTo folder not found: $InstallTo" }
    $live = Join-Path $InstallTo 'talltale.dat'
    if (Test-Path $live) {
        Copy-Item $live "$live.bak" -Force
        Write-Host "Backed up existing save -> $live.bak"
    }
    Copy-Item $Out $live -Force
    Write-Host "Installed edited save -> $live" -ForegroundColor Green
    Write-Host 'Load the game and Restore/Continue to apply.'
}
