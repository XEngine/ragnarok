$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$destination = Join-Path $root 'release/icon.png'
$bitmap = New-Object Drawing.Bitmap(256, 256)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$background = New-Object Drawing.Drawing2D.LinearGradientBrush(
    (New-Object Drawing.Rectangle(0, 0, 256, 256)),
    [Drawing.ColorTranslator]::FromHtml('#0C1417'),
    [Drawing.ColorTranslator]::FromHtml('#202A29'), 45)
$gold = [Drawing.ColorTranslator]::FromHtml('#D4AF67')
$edge = New-Object Drawing.Pen([Drawing.Color]::FromArgb(150, $gold), 1.5)
$inner = New-Object Drawing.Pen([Drawing.Color]::FromArgb(55, $gold), 1)
$rune = New-Object Drawing.Pen($gold, 7)
$shadow = New-Object Drawing.Pen([Drawing.Color]::FromArgb(70, 0, 0, 0), 13)
$brush = New-Object Drawing.SolidBrush($gold)
try {
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.FillRectangle($background, 0, 0, 256, 256)
    $diamond = [Drawing.PointF[]]@((New-Object Drawing.PointF(128, 14)), (New-Object Drawing.PointF(242, 128)),
        (New-Object Drawing.PointF(128, 242)), (New-Object Drawing.PointF(14, 128)))
    $graphics.DrawPolygon($edge, $diamond)
    $graphics.DrawEllipse($inner, 38, 38, 180, 180)
    $stem = [Drawing.PointF[]]@((New-Object Drawing.PointF(96, 66)), (New-Object Drawing.PointF(96, 193)))
    $bowl = [Drawing.PointF[]]@((New-Object Drawing.PointF(96, 66)), (New-Object Drawing.PointF(164, 105)), (New-Object Drawing.PointF(96, 144)))
    $leg = [Drawing.PointF[]]@((New-Object Drawing.PointF(96, 144)), (New-Object Drawing.PointF(165, 193)))
    foreach ($points in @($stem, $bowl, $leg)) { $graphics.DrawLines($shadow, $points); $graphics.DrawLines($rune, $points) }
    foreach ($y in @(34, 222)) {
        $mark = [Drawing.PointF[]]@((New-Object Drawing.PointF(128, ($y - 4))), (New-Object Drawing.PointF(132, $y)),
            (New-Object Drawing.PointF(128, ($y + 4))), (New-Object Drawing.PointF(124, $y)))
        $graphics.FillPolygon($brush, $mark)
    }
    $bitmap.Save($destination, [Drawing.Imaging.ImageFormat]::Png)
    Write-Host "Created $destination (256x256 original procedural artwork)."
} finally {
    $brush.Dispose(); $shadow.Dispose(); $rune.Dispose(); $inner.Dispose(); $edge.Dispose()
    $background.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}
