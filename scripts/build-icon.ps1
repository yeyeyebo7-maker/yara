param(
    [string]$OutputPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "yara.ico")
)

Add-Type -AssemblyName System.Drawing

$size = 256
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::FromArgb(255, 14, 14, 22))

$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    (New-Object System.Drawing.Point 0, 0),
    (New-Object System.Drawing.Point 0, $size),
    [System.Drawing.Color]::FromArgb(255, 28, 30, 46),
    [System.Drawing.Color]::FromArgb(255, 10, 10, 18))
$g.FillRectangle($bgBrush, 0, 0, $size, $size)

$colors = @(
    [System.Drawing.Color]::FromArgb(255, 255, 84, 84),
    [System.Drawing.Color]::FromArgb(255, 255, 178, 72),
    [System.Drawing.Color]::FromArgb(255, 255, 235, 92),
    [System.Drawing.Color]::FromArgb(255, 90, 224, 120),
    [System.Drawing.Color]::FromArgb(255, 52, 200, 220),
    [System.Drawing.Color]::FromArgb(255, 96, 130, 255),
    [System.Drawing.Color]::FromArgb(255, 190, 96, 255),
    [System.Drawing.Color]::FromArgb(255, 255, 110, 200)
)
$heights = @(0.22, 0.44, 0.30, 0.68, 0.48, 0.86, 0.62, 0.36)

$barW = 20
$gap = 9
$x = 22
for ($i = 0; $i -lt $colors.Count; $i++) {
    $h = [int]($size * $heights[$i])
    $brush = New-Object System.Drawing.SolidBrush $colors[$i]
    $g.FillRectangle($brush, $x, $size - $h, $barW, $h)
    $x += $barW + $gap
}

$g.Dispose()

$rect = New-Object System.Drawing.Rectangle 0, 0, $size, $size
$bmpData = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$stride = $bmpData.Stride
$pixelBytes = $stride * $size
$px = New-Object byte[] $pixelBytes
[System.Runtime.InteropServices.Marshal]::Copy($bmpData.Scan0, $px, 0, $pixelBytes)
$bmp.UnlockBits($bmpData)
$bmp.Dispose()

# 32bpp DIB icon: BITMAPINFOHEADER(40) + BGRA pixels bottom-up + AND mask (ignored)
$dib = New-Object byte[] (40 + $pixelBytes)
$bw = New-Object System.IO.BinaryWriter (New-Object System.IO.MemoryStream)
$bw.Write([int32]40)
$bw.Write([int32]$size)
$bw.Write([int32]($size * 2))
$bw.Write([int16]1)
$bw.Write([int16]32)
$bw.Write([int32]0)
$bw.Write([int32]$pixelBytes)
$bw.Write([int32]0)
$bw.Write([int32]0)
$bw.Write([int32]0)
$bw.Write([int32]0)
$dibHeader = $bw.BaseStream.ToArray()
$bw.Close()
[System.Array]::Copy($dibHeader, 0, $dib, 0, 40)

for ($y = 0; $y -lt $size; $y++) {
    $srcRow = ($size - 1 - $y) * $stride
    $dstRow = 40 + $y * $stride
    [System.Array]::Copy($px, $srcRow, $dib, $dstRow, $stride)
}

$fs = [System.IO.File]::Create($OutputPath)
$ico = New-Object System.IO.BinaryWriter $fs
$ico.Write([uint16]0)
$ico.Write([uint16]1)
$ico.Write([uint16]1)
$ico.Write([byte]0)
$ico.Write([byte]0)
$ico.Write([byte]0)
$ico.Write([byte]0)
$ico.Write([uint16]1)
$ico.Write([uint16]32)
$ico.Write([uint32]$dib.Length)
$ico.Write([uint32]22)
$ico.Write($dib)
$ico.Close()
$fs.Close()

Write-Host "Icon written to $OutputPath"
