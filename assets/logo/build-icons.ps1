# Renders the logo masters into the app's MSIX assets and AppIcon.ico.
# Uses headless Edge as the SVG rasteriser, so nothing extra has to be installed.
# Run from anywhere: pwsh assets/logo/build-icons.ps1
$ErrorActionPreference = 'Stop'
$here = $PSScriptRoot
$assets = Join-Path $here '..\..\src\YtDownloader.App\Assets' | Resolve-Path
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$work = Join-Path ([IO.Path]::GetTempPath()) 'yt-downloader-icons'
New-Item -ItemType Directory -Force $work | Out-Null

# Draws one SVG at (w x h) inside a (canvasW x canvasH) transparent PNG, centred.
function Render([string]$svg, [int]$canvasW, [int]$canvasH, [int]$w, [int]$h, [string]$out) {
    $svgUri = ([Uri](Join-Path $here $svg)).AbsoluteUri
    $html = Join-Path $work 'frame.html'
    @"
<!doctype html><html><head><style>
html,body{margin:0;background:transparent;width:${canvasW}px;height:${canvasH}px;overflow:hidden}
body{display:grid;place-items:center}
img{width:${w}px;height:${h}px;display:block}
</style></head><body><img src="$svgUri"></body></html>
"@ | Set-Content -Encoding utf8 $html
    $userData = Join-Path $work 'profile'
    & $edge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=1 `
        --default-background-color=00000000 --user-data-dir="$userData" `
        --window-size="$canvasW,$canvasH" --screenshot="$out" ([Uri]$html).AbsoluteUri 2>$null | Out-Null
    $deadline = (Get-Date).AddSeconds(20)
    while (-not (Test-Path $out) -and (Get-Date) -lt $deadline) { Start-Sleep -Milliseconds 200 }
    if (-not (Test-Path $out)) { throw "Render failed: $out" }
}

function Icon([int]$size, [string]$out) {
    $svg = if ($size -le 24) { 'icon-small.svg' } else { 'icon.svg' }
    Render $svg $size $size $size $size $out
}

Get-ChildItem $assets -Filter *.png | Remove-Item

# Square44x44: app list, taskbar, title bar. Plated everywhere, so the yellow tile is the icon in every place.
foreach ($s in 100, 125, 150, 200, 400) { Icon ([int](44 * $s / 100)) (Join-Path $assets "Square44x44Logo.scale-$s.png") }
foreach ($t in 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96, 256) {
    Icon $t (Join-Path $assets "Square44x44Logo.targetsize-$t.png")
    Copy-Item (Join-Path $assets "Square44x44Logo.targetsize-$t.png") (Join-Path $assets "Square44x44Logo.targetsize-${t}_altform-unplated.png")
    Copy-Item (Join-Path $assets "Square44x44Logo.targetsize-$t.png") (Join-Path $assets "Square44x44Logo.targetsize-${t}_altform-lightunplated.png")
}
# Medium tile and Store logo.
foreach ($s in 100, 200, 400) { $c = [int](150 * $s / 100); Render 'icon.svg' $c $c ([int]($c * 0.6)) ([int]($c * 0.6)) (Join-Path $assets "Square150x150Logo.scale-$s.png") }
foreach ($s in 100, 200, 400) { Icon ([int](50 * $s / 100)) (Join-Path $assets "StoreLogo.scale-$s.png") }
# Wide tile: the icon alone, centred. Windows prints the app name (YT Downloader) under it.
foreach ($s in 100, 200) { $cw = 310 * $s / 100; $ch = 150 * $s / 100; $i = [int]($ch * 0.6); Render 'icon.svg' $cw $ch $i $i (Join-Path $assets "Wide310x150Logo.scale-$s.png") }
# Splash: the icon on transparent; the manifest supplies the background colour.
foreach ($s in 100, 200) { $cw = 620 * $s / 100; $ch = 300 * $s / 100; $i = [int]($ch * 0.5); Render 'icon.svg' $cw $ch $i $i (Join-Path $assets "SplashScreen.scale-$s.png") }
# Lock screen badge must be a white silhouette.
foreach ($s in 100, 200) { $c = 24 * $s / 100; Render 'mark-white.svg' $c $c $c $c (Join-Path $assets "LockScreenLogo.scale-$s.png") }

# AppIcon.ico: PNG-compressed entries (Vista and later), small cut at 24 px and below.
$sizes = 16, 20, 24, 32, 40, 48, 64, 96, 128, 256
$pngs = foreach ($s in $sizes) { $p = Join-Path $work "ico-$s.png"; Remove-Item $p -ErrorAction SilentlyContinue; Icon $s $p; , [IO.File]::ReadAllBytes($p) }
$ms = New-Object IO.MemoryStream
$bw = New-Object IO.BinaryWriter $ms
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $bw.Write([byte]($s % 256)); $bw.Write([byte]($s % 256)); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32); $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $bw.Write($p) }
[IO.File]::WriteAllBytes((Join-Path $assets 'AppIcon.ico'), $ms.ToArray())

# Edge can hold its temp profile open for a moment after the last render; a leftover temp folder is harmless.
try { Remove-Item -Recurse -Force $work -ErrorAction Stop } catch { }
Get-ChildItem $assets | Measure-Object | ForEach-Object { "Wrote $($_.Count) files to $assets" }
