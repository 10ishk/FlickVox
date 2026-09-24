# Generates the original FlickVox waveform mark at standard Windows icon sizes.
Add-Type -AssemblyName System.Drawing
$assetRoot = Join-Path $PSScriptRoot '..\src\FlickVox\Assets'
New-Item -ItemType Directory -Force -Path $assetRoot | Out-Null
function New-Icon([string]$name, [bool]$tray) {
    $images = [System.Collections.Generic.List[byte[]]]::new()
    $sizes = @(16,20,24,32,48,64,128,256)
    foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size,$size)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $scale = $size / 64.0
        $graphics.ScaleTransform($scale,$scale)
        $shape = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $shape.AddArc(2,2,16,16,180,90); $shape.AddArc(46,2,16,16,270,90)
        $shape.AddArc(46,46,16,16,0,90); $shape.AddArc(2,46,16,16,90,90)
        $shape.CloseFigure()
        if ($tray) { $fill = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(238,241,246)) }
        else { $fill = [System.Drawing.Drawing2D.LinearGradientBrush]::new([System.Drawing.Point]::new(0,0),[System.Drawing.Point]::new(64,64),[System.Drawing.Color]::FromArgb(108,99,255),[System.Drawing.Color]::FromArgb(61,219,181)) }
        $graphics.FillPath($fill,$shape)
        $barColor = if ($tray) { [System.Drawing.Color]::FromArgb(15,18,24) } else { [System.Drawing.Color]::White }
        $bar = [System.Drawing.SolidBrush]::new($barColor)
        foreach ($part in @(@(17,24,6,16),@(27,16,6,32),@(37,22,6,20))) {
            $graphics.FillRectangle($bar,$part[0],$part[1],$part[2],$part[3])
        }
        $graphics.TranslateTransform(49,33); $graphics.RotateTransform(-18)
        $graphics.FillRectangle($bar,-3,-12,6,24)
        $graphics.ResetTransform()
        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png)
        $images.Add($stream.ToArray())
        $stream.Dispose(); $bar.Dispose(); $fill.Dispose(); $shape.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
    }
    $output = [System.IO.File]::Create((Join-Path $assetRoot $name))
    $writer = [System.IO.BinaryWriter]::new($output)
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$images.Count)
    $offset = 6 + $images.Count * 16
    foreach ($index in 0..($images.Count-1)) {
        $size = $sizes[$index]
        $writer.Write([byte]($size % 256)); $writer.Write([byte]($size % 256))
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$index].Length); $writer.Write([uint32]$offset)
        $offset += $images[$index].Length
    }
    foreach ($bytes in $images) { $writer.Write($bytes) }
    $writer.Dispose()
}
New-Icon 'FlickVox.ico' $false
New-Icon 'FlickVoxTray.ico' $true
