$bytes = [System.IO.File]::ReadAllBytes('C:\Temp\Scratch\Win31DOSBox\C-DRIVE\GAMES\EOB1\EOBDATA.SAV')
Write-Host "File size: $($bytes.Length) bytes"
for ($i = 0; $i -lt 6; $i++) {
    $off = $i * 243
    $charId = $bytes[$off]
    $active = $bytes[$off + 1]
    $name = ''
    for ($j = 0; $j -lt 10; $j++) {
        $ch = $bytes[$off + 2 + $j]
        if ($ch -eq 0) { break }
        $name += [char]$ch
    }
    $str = $bytes[$off + 0x0D]
    $strExc = $bytes[$off + 0x0F]
    $int = $bytes[$off + 0x11]
    $wis = $bytes[$off + 0x13]
    $dex = $bytes[$off + 0x15]
    $con = $bytes[$off + 0x17]
    $cha = $bytes[$off + 0x19]
    $hpCur = $bytes[$off + 0x1B]
    $hpMax = $bytes[$off + 0x1C]
    $ac = [sbyte]$bytes[$off + 0x1D]
    $race = $bytes[$off + 0x1F]
    $cls = $bytes[$off + 0x20]
    $align = $bytes[$off + 0x21]
    $food = $bytes[$off + 0x23]
    $lvl1 = $bytes[$off + 0x24]
    $lvl2 = $bytes[$off + 0x25]
    $lvl3 = $bytes[$off + 0x26]
    $xp1 = [BitConverter]::ToUInt32($bytes, $off + 0x27)
    $xp2 = [BitConverter]::ToUInt32($bytes, $off + 0x2B)
    $xp3 = [BitConverter]::ToUInt32($bytes, $off + 0x2F)
    Write-Host "Slot ${i}: CharId=${charId} Active=${active} Name='${name}' STR=${str} ExcStr=${strExc} INT=${int} WIS=${wis} DEX=${dex} CON=${con} CHA=${cha} HP=${hpCur}/${hpMax} AC=${ac} Race=${race} Class=${cls} Align=${align} Food=${food} Lvl=${lvl1}/${lvl2}/${lvl3} XP=${xp1}/${xp2}/${xp3}"
}
