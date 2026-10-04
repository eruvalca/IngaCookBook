# Windows-only asset export; uses the selected artwork without altering its design.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Export-AppIcons.ps1 requires Windows System.Drawing.' }
Add-Type -AssemblyName System.Drawing
$repository = Split-Path $PSScriptRoot -Parent
$sourcePath = Join-Path $repository 'assets/branding/kitchen-notebook.png'
$webRoot = Join-Path $repository 'src/IngaCookBook/wwwroot'
New-Item -ItemType Directory -Path (Join-Path $webRoot 'icons') -Force | Out-Null
$source = [System.Drawing.Image]::FromFile($sourcePath)
try {
    if ($source.Width -ne $source.Height) { throw 'The source icon must be square.' }
    $exports = @{
        'favicon.png' = 32
        'icons/apple-touch-icon.png' = 180
        'icons/icon-192.png' = 192
        'icons/icon-512.png' = 512
        'icons/icon-maskable-512.png' = 512
    }
    foreach ($entry in $exports.GetEnumerator()) {
        $size = $entry.Value
        $bitmap = [System.Drawing.Bitmap]::new($size, $size)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $rectangle = [System.Drawing.Rectangle]::new(0, 0, $size, $size)
            $graphics.DrawImage($source, $rectangle, 0, 0, $source.Width, $source.Height,
                [System.Drawing.GraphicsUnit]::Pixel, $attributes)
            $bitmap.Save((Join-Path $webRoot $entry.Key), [System.Drawing.Imaging.ImageFormat]::Png)
            Write-Output "$($entry.Key): $size x $size"
        }
        finally {
            $attributes.Dispose()
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
}
finally { $source.Dispose() }
