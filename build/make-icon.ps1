# Draws the Cloudbreak app icon into assets/cloudbreak.ico:
# the round ring (long white hold arc, short violet break arc, violet dot) on a dark rounded tile,
# so it reads on both light and dark Windows surfaces. Same mark as the tray icon and the in-app logo.
# Run with Windows PowerShell 5.1: powershell -ExecutionPolicy Bypass -File build\make-icon.ps1
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root 'assets\cloudbreak.ico'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null

$tile = [System.Drawing.Color]::FromArgb(255, 0x1C, 0x1E, 0x24)
$hold = [System.Drawing.Color]::FromArgb(255, 0xF2, 0xF3, 0xF5)
$violet = [System.Drawing.Color]::FromArgb(255, 0xB4, 0xA6, 0xF6)

# Ring geometry in a 32-unit box (matches IconFactory.DrawRound): r 11.5, stroke 3.4, gaps 5.2, round 25/5
$circ = 2 * [Math]::PI * 11.5
$gapDeg = 360 * 5.2 / $circ
$capDeg = 360 * 3.4 / $circ
$usable = 360 - 2 * $gapDeg
$holdSweep = $usable * 25 / 30
$breakSweep = $usable * 5 / 30
$holdStart = -90
$breakStart = $holdStart + $holdSweep + $gapDeg

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$frames = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.PixelOffsetMode = 'HighQuality'
    $g.Clear([System.Drawing.Color]::Transparent)

    # Rounded dark tile
    $u = $s / 32.0
    $r = 7.5 * $u
    $w = $s - 0.5
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($w - 2 * $r, 0, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($w - 2 * $r, $w - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc(0, $w - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush $tile), $path)
    if ($s -ge 32) {
        $edge = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(20, 255, 255, 255)), ([Math]::Max(1, $u))
        $g.DrawPath($edge, $path)
    }

    # The mark, scaled to 82% of the tile
    $k = $u * 0.82
    $cx = $s / 2.0; $cy = $s / 2.0
    $ringR = 11.5 * $k
    $penW = [Math]::Max(1.3, 3.4 * $k)
    $box = New-Object System.Drawing.RectangleF ([single]($cx - $ringR)), ([single]($cy - $ringR)), ([single](2 * $ringR)), ([single](2 * $ringR))

    $pHold = New-Object System.Drawing.Pen $hold, $penW
    $pHold.StartCap = 'Round'; $pHold.EndCap = 'Round'
    $g.DrawArc($pHold, $box, [single]($holdStart + $capDeg / 2), [single]($holdSweep - $capDeg))

    $pBreak = New-Object System.Drawing.Pen $violet, $penW
    $pBreak.StartCap = 'Round'; $pBreak.EndCap = 'Round'
    $g.DrawArc($pBreak, $box, [single]($breakStart + $capDeg / 2), [single]([Math]::Max(1, $breakSweep - $capDeg)))

    $dotR = [Math]::Max(1.2, 3.6 * $k)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush $violet), [single]($cx - $dotR), [single]($cy - $dotR), [single](2 * $dotR), [single](2 * $dotR))

    $g.Dispose()
    if ($s -eq 256) {
        # Also used by the README
        New-Item -ItemType Directory -Force (Join-Path $root 'docs') | Out-Null
        $bmp.Save((Join-Path $root 'docs\icon.png'), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += , @($s, $ms.ToArray())
    $bmp.Dispose()
}

# ICO container with PNG frames
$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $s = $f[0]; $data = $f[1]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$data.Length); $bw.Write([UInt32]$offset)
    $offset += $data.Length
}
foreach ($f in $frames) { $bw.Write([byte[]]$f[1]) }
$bw.Close()
Write-Output "Wrote $out"
