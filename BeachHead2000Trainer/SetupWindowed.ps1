<#
.SYNOPSIS
    Configures BeachHead 2000 to run windowed (dgVoodoo) instead of fullscreen (DDrawCompat).

.DESCRIPTION
    BeachHead 2000 runs fullscreen by default via the bundled DDrawCompat wrapper (ddraw.dll
    in the game root). This script swaps in the dgVoodoo DirectDraw wrapper and sets its config
    to windowed mode so you can freely alt-tab between the trainer and the game. It automates
    the steps documented in README.md ("Full-screen" section):

      1. Back up the DDrawCompat ddraw.dll as ddraw_DDrawCompat.bak.
      2. Copy dgVoodoo\DDraw.dll to the game root as ddraw.dll.
      3. Copy dgVoodoo\D3D9.dll to the game root.
      4. Copy dgVoodoo\dgVoodoo.conf to the game root.
      5. Set FullScreenMode = false and CaptureMouse = false in the copied dgVoodoo.conf.

    Use -Revert to restore DDrawCompat fullscreen (renames the .bak back and removes the
    dgVoodoo copies from the game root).

.PARAMETER GamePath
    The BeachHead 2000 install folder (the one containing Bh.exe and the dgVoodoo\ and
    beachhead\ subfolders - the Steam Gold Edition installs it as the 509610 subfolder).
    If omitted, the script tries to auto-detect it from the Steam registry and
    libraryfolders.vdf, then from common Steam library locations on every drive.

.PARAMETER Revert
    Undo the windowed setup: restore the original DDrawCompat ddraw.dll and remove the
    dgVoodoo DLLs/config copied into the game root.

.EXAMPLE
    .\SetupWindowed.ps1
    Auto-detect the game and switch it to windowed mode.

.EXAMPLE
    .\SetupWindowed.ps1 -GamePath 'D:\SteamLibrary\steamapps\common\509610'
    Switch a specific install to windowed mode.

.EXAMPLE
    .\SetupWindowed.ps1 -Revert
    Restore fullscreen DDrawCompat for the auto-detected install.
#>
[CmdletBinding()]
param(
    [string]$GamePath,
    [switch]$Revert
)

$ErrorActionPreference = 'Stop'
$AppFolder = '509610'
$InstallFolder = 'BeachHead Gold Edition'

function Get-SteamRoot {
    try {
        $k = Get-ItemProperty -Path 'HKCU:\Software\Valve\Steam' -ErrorAction SilentlyContinue
        if ($k -and $k.SteamPath) { return ($k.SteamPath -replace '/', '\') }
    } catch { }
    try {
        $k = Get-ItemProperty -Path 'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam' -ErrorAction SilentlyContinue
        if ($k -and $k.InstallPath) { return $k.InstallPath }
    } catch { }
    return $null
}

function Get-SteamLibraries {
    $libs = New-Object System.Collections.Generic.List[string]
    $root = Get-SteamRoot
    if ($root -and (Test-Path $root)) { $libs.Add(($root.TrimEnd('\'))) }

    if ($root) {
        $vdf = Join-Path $root 'config\libraryfolders.vdf'
        if (Test-Path $vdf) {
            try {
                $text = Get-Content -Raw -LiteralPath $vdf
                foreach ($m in [regex]::Matches($text, '"path"\s+"([^"]+)"')) {
                    $p = $m.Groups[1].Value -replace '\\\\', '\' -replace '/', '\'
                    if (Test-Path $p) { $libs.Add($p.TrimEnd('\')) }
                }
            } catch { }
        }
    }

    foreach ($drive in (Get-PSDrive -PSProvider FileSystem)) {
        $d = $drive.Root.TrimEnd('\')
        foreach ($c in @('Steam', 'SteamLibrary', 'Games\Steam')) {
            $p = Join-Path $d $c
            if (Test-Path $p) { $libs.Add($p) }
        }
    }
    return $libs | Select-Object -Unique
}

function Find-GamePath {
    foreach ($lib in (Get-SteamLibraries)) {
        $common = Join-Path $lib 'steamapps\common'
        # Confirmed Gold Edition layout: steamapps\common\BeachHead Gold Edition\509610
        $nested = Join-Path $common (Join-Path $InstallFolder $AppFolder)
        if ((Test-Path $nested) -and (Test-Path (Join-Path $nested 'Bh.exe'))) {
            return $nested
        }
        # Fallback: a flat steamapps\common\509610 layout.
        $flat = Join-Path $common $AppFolder
        if ((Test-Path $flat) -and (Test-Path (Join-Path $flat 'Bh.exe'))) {
            return $flat
        }
    }
    return $null
}

function Find-FileCi([string]$dir, [string]$name) {
    if (-not (Test-Path $dir)) { return $null }
    $match = Get-ChildItem -LiteralPath $dir -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ieq $name } | Select-Object -First 1
    if ($match) { return $match.FullName }
    return $null
}

if (-not $GamePath) {
    $GamePath = Find-GamePath
    if (-not $GamePath) {
        Write-Error "Could not auto-detect the BeachHead 2000 install. Pass -GamePath with the folder that contains Bh.exe (the 509610 subfolder under a Steam library)."
        exit 1
    }
}

$resolved = Resolve-Path -LiteralPath $GamePath -ErrorAction SilentlyContinue
if ($resolved) { $GamePath = $resolved.Path }
if (-not $GamePath -or -not (Test-Path (Join-Path $GamePath 'Bh.exe'))) {
    Write-Error "GamePath does not look like the BeachHead 2000 install (no Bh.exe found): '$GamePath'"
    exit 1
}

$bak      = Join-Path $GamePath 'ddraw_DDrawCompat.bak'
$ddraw    = Join-Path $GamePath 'ddraw.dll'
$d3d9     = Join-Path $GamePath 'D3D9.dll'
$conf     = Join-Path $GamePath 'dgVoodoo.conf'
$dgFolder = Join-Path $GamePath 'dgVoodoo'

if ($Revert) {
    Write-Host "Reverting to DDrawCompat fullscreen in: $GamePath"
    if (Test-Path $bak) {
        Copy-Item -LiteralPath $bak -Destination $ddraw -Force
        Remove-Item -LiteralPath $bak -Force
        Write-Host "  Restored original DDrawCompat ddraw.dll from backup."
    }
    else {
        Write-Host "  No ddraw_DDrawCompat.bak found - leaving ddraw.dll as-is."
    }
    foreach ($f in @($d3d9, $conf)) {
        if (Test-Path $f) {
            Remove-Item -LiteralPath $f -Force
            Write-Host "  Removed $(Split-Path $f -Leaf) from game root."
        }
    }
    Write-Host "Revert complete. Launch the game to confirm it runs fullscreen again."
    exit 0
}

Write-Host "Setting up windowed mode (dgVoodoo) in: $GamePath"

if (-not (Test-Path $dgFolder)) {
    Write-Error "The dgVoodoo\ subfolder was not found in the game root. The Steam Gold Edition ships it there; verify your install."
    exit 1
}

$srcDdraw = Find-FileCi $dgFolder 'DDraw.dll'
$srcD3d9  = Find-FileCi $dgFolder 'D3D9.dll'
$srcConf  = Find-FileCi $dgFolder 'dgVoodoo.conf'
if (-not $srcDdraw -or -not $srcD3d9 -or -not $srcConf) {
    Write-Error "Missing dgVoodoo files in $dgFolder (need DDraw.dll, D3D9.dll, dgVoodoo.conf)."
    exit 1
}

# 1. Back up the active DDrawCompat wrapper (only if no backup exists yet, so re-running
#    doesn't overwrite the original with the dgVoodoo copy).
if ((Test-Path $ddraw) -and -not (Test-Path $bak)) {
    Move-Item -LiteralPath $ddraw -Destination $bak -Force
    Write-Host "  Backed up DDrawCompat ddraw.dll -> ddraw_DDrawCompat.bak"
}
elseif (Test-Path $bak) {
    Write-Host "  Existing ddraw_DDrawCompat.bak kept (DDrawCompat already backed up)."
}
else {
    Write-Host "  No ddraw.dll present to back up (continuing)."
}

# 2-4. Copy the dgVoodoo wrapper DLLs and config into the game root.
Copy-Item -LiteralPath $srcDdraw -Destination $ddraw -Force
Write-Host "  Copied dgVoodoo DDraw.dll -> ddraw.dll"
Copy-Item -LiteralPath $srcD3d9 -Destination $d3d9 -Force
Write-Host "  Copied dgVoodoo D3D9.dll -> D3D9.dll"
Copy-Item -LiteralPath $srcConf -Destination $conf -Force
Write-Host "  Copied dgVoodoo dgVoodoo.conf -> dgVoodoo.conf"

# 5. Edit the copied config: windowed + mouse not captured.
$text = Get-Content -Raw -LiteralPath $conf
$text = [regex]::Replace($text, '(?m)^(\s*)FullScreenMode\s*=\s*\w+', '$1FullScreenMode = false')
$text = [regex]::Replace($text, '(?m)^(\s*)CaptureMouse\s*=\s*\w+',   '$1CaptureMouse = false')
Set-Content -LiteralPath $conf -Value $text -NoNewline
Write-Host "  Set FullScreenMode = false, CaptureMouse = false in dgVoodoo.conf"

Write-Host "Windowed mode configured. Launch the game - it should run in a 640x480 window."
Write-Host "To revert to DDrawCompat fullscreen: .\SetupWindowed.ps1 -Revert"
