# Builds ProtoVerseApp/Assets/Schematics/ from the KiCad sources.
#
# Produces two things per board:
#
#   {CODE}_schematic.pdf  - the ORIGINAL, complete schematic PDF straight from
#                           `Finished Modules`, border and title block intact.
#                           This is what the manual's "Open schematic" button
#                           opens: if someone is opening the full drawing they
#                           want the full drawing, revision block and all.
#
#   {CODE}_circuit.png    - a cropped raster of just the circuit, for the image
#                           embedded in the manual's Overview.
#
# The PNG is made by exporting SVG with kicad-cli's exclude-drawing-sheet
# option (no border, no title block), tightening its viewBox to the drawn
# content, and rasterising with headless Edge - there is no SVG rasteriser or
# PDF tool on this machine (no Inkscape, Ghostscript, ImageMagick or Python).
#
# THE BOUNDS MUST INCLUDE TEXT. An earlier version measured only <path> and
# <circle> geometry and clipped reference designators, pin names and component
# values off the edges - the symbols were there but their labels were shaved.
# Text is measured here from its anchor plus a generous allowance, and the
# margin is deliberately loose. Losing a few millimetres of whitespace is
# free; losing a resistor's value is not.
#
# Re-run after any schematic change. Requires KiCad (tested against 9.0) and
# Microsoft Edge.

param(
    [string]$OutDir     = "$PSScriptRoot\..\ProtoVerseApp\Assets\Schematics",
    [string]$SourceRoot = "$PSScriptRoot\..\..\..\Finished Modules",
    [string]$KiCadCli   = "E:\KiCad 9.0\bin\kicad-cli.exe",
    [string]$Edge       = "C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    [double]$MarginMm   = 4.0,
    [double]$PxPerMm    = 4.0
)

# NB: none of these may be named $sources / $outDir etc. differing only by case
# from a parameter - PowerShell variable names are case-insensitive, so that
# silently overwrites the parameter (which broke an earlier version of this).
$boards = @{
    "A01" = @{ Sch = "PC01_A01_ProtoMod_DDS\Rev01\Protomod_DDS.kicad_sch";                       Pdf = "PC01_A01_ProtoMod_DDS\Rev01\Protomod_DDS.pdf" }
    "E03" = @{ Sch = "PC01_E03_ProtoMod_Accelerometer\Rev01\Protomod_Accel.kicad_sch";           Pdf = "PC01_E03_ProtoMod_Accelerometer\Rev01\Protomod_Accel_Rev01.pdf" }
    "E05" = @{ Sch = "PC01_E05_ProtoMod_ElectronicLoad\Rev01\Protomod_ElectronicLoad.kicad_sch"; Pdf = "PC01_E05_ProtoMod_ElectronicLoad\Rev01\Protomod_ElectronicLoad.pdf" }
    "F00" = @{ Sch = "PC01_F00_ProtoMod_Headers\Rev01\Protomod_Headers.kicad_sch";               Pdf = "PC01_F00_ProtoMod_Headers\Rev01\Protomod_Headers_Rev01.pdf" }
    "F01" = @{ Sch = "PC01_F01_ProtoMod_Blinky\Rev01\Protomod_Starter.kicad_sch";                Pdf = "PC01_F01_ProtoMod_Blinky\Rev01\Protomod_Blinky_Rev01.pdf" }
    "F02" = @{ Sch = "PC01_F02_ProtoMod_simpleLED\Rev01\Protomod_simpleLED.kicad_sch";           Pdf = "PC01_F02_ProtoMod_simpleLED\Rev01\Protomod_simpleLED_Rev01.pdf" }
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$work = Join-Path $env:TEMP "protoverse_schematics"
New-Item -ItemType Directory -Force -Path $work | Out-Null

foreach ($code in $boards.Keys | Sort-Object) {
    $board = $boards[$code]

    # ---------- 1. the full original PDF, copied verbatim ----------
    $srcPdf = Join-Path $SourceRoot $board.Pdf
    if (Test-Path $srcPdf) {
        $destPdf = Join-Path $OutDir "$code`_schematic.pdf"
        Copy-Item $srcPdf $destPdf -Force
        # Copy-Item preserves the source's timestamp, and these schematics are dated
        # 2025. MSBuild's CopyToOutputDirectory="PreserveNewest" would then decide the
        # copy already in bin\ is newer and keep it - which silently left a stale file
        # beside the .exe after this script had "successfully" updated the asset. Stamp
        # it now so the build actually picks it up.
        (Get-Item $destPdf).LastWriteTime = Get-Date
    } else {
        "$code : original PDF missing ($($board.Pdf))"
    }

    # ---------- 2. cropped circuit raster ----------
    $srcSch = Join-Path $SourceRoot $board.Sch
    if (-not (Test-Path $srcSch)) { "$code : schematic source missing"; continue }
    if (-not (Test-Path $KiCadCli)) { "$code : kicad-cli missing, skipped PNG"; continue }

    Get-ChildItem $work -Filter *.svg -ErrorAction SilentlyContinue | Remove-Item -Force
    & $KiCadCli sch export svg --exclude-drawing-sheet --no-background-color -o $work $srcSch 2>&1 | Out-Null
    $svgFile = Get-ChildItem $work -Filter *.svg | Select-Object -First 1
    if (-not $svgFile) { "$code : SVG export produced nothing"; continue }

    $svg = Get-Content $svgFile.FullName -Raw

    $minX = [double]::MaxValue; $minY = [double]::MaxValue
    $maxX = [double]::MinValue; $maxY = [double]::MinValue
    function Grow([double]$x, [double]$y) {
        if ($x -lt $script:minX) { $script:minX = $x }; if ($x -gt $script:maxX) { $script:maxX = $x }
        if ($y -lt $script:minY) { $script:minY = $y }; if ($y -gt $script:maxY) { $script:maxY = $y }
    }

    # Path geometry: "M x,y" then bare "x,y" pairs, absolute, in mm.
    foreach ($d in [regex]::Matches($svg, 'd="([^"]*)"')) {
        foreach ($pair in [regex]::Matches($d.Groups[1].Value, '(-?\d+\.?\d*),(-?\d+\.?\d*)')) {
            Grow ([double]$pair.Groups[1].Value) ([double]$pair.Groups[2].Value)
        }
    }
    foreach ($c in [regex]::Matches($svg, '<circle\s+cx="(-?\d+\.?\d*)"\s+cy="(-?\d+\.?\d*)"\s+r="(-?\d+\.?\d*)"')) {
        $x = [double]$c.Groups[1].Value; $y = [double]$c.Groups[2].Value; $r = [double]$c.Groups[3].Value
        Grow ($x-$r) ($y-$r); Grow ($x+$r) ($y+$r)
    }
    foreach ($r in [regex]::Matches($svg, '<rect[^>]*x="(-?\d+\.?\d*)"[^>]*y="(-?\d+\.?\d*)"[^>]*width="(-?\d+\.?\d*)"[^>]*height="(-?\d+\.?\d*)"')) {
        $x = [double]$r.Groups[1].Value; $y = [double]$r.Groups[2].Value
        Grow $x $y; Grow ($x + [double]$r.Groups[3].Value) ($y + [double]$r.Groups[4].Value)
    }
    # Text is handled in a second pass, against the geometry bounds just
    # measured. Two reasons it can't simply join the first pass:
    #
    #   - It MUST be included, or labels get shaved. Measuring only paths and
    #     circles clipped reference designators and pin names off the edges in
    #     an earlier version - the symbols were there, their labels weren't.
    #
    #   - But kicad-cli leaves the sheet's own title text on the page even with
    #     exclude-drawing-sheet ("E05 - Electronic Load #1", the date, the sheet
    #     number). That sits far below the circuit and, included naively,
    #     stretched the crop by ~60mm of pure whitespace.
    #
    # So: take text that sits near the circuit, ignore text stranded away from
    # it. A label belongs to a symbol it is next to.
    $coreMinX = $minX; $coreMaxX = $maxX; $coreMinY = $minY; $coreMaxY = $maxY
    $near = 20.0
    foreach ($t in [regex]::Matches($svg, '<text\s+x="(-?\d+\.?\d*)"\s+y="(-?\d+\.?\d*)"(?:[^>]*?textLength="(-?\d+\.?\d*)")?(?:[^>]*?font-size="(-?\d+\.?\d*)")?')) {
        $x = [double]$t.Groups[1].Value; $y = [double]$t.Groups[2].Value
        if ($x -lt ($coreMinX - $near) -or $x -gt ($coreMaxX + $near) -or
            $y -lt ($coreMinY - $near) -or $y -gt ($coreMaxY + $near)) { continue }
        $len = if ($t.Groups[3].Success) { [double]$t.Groups[3].Value } else { 6.0 }
        $fs  = if ($t.Groups[4].Success) { [double]$t.Groups[4].Value } else { 2.0 }
        Grow ($x - $len) ($y - $fs); Grow ($x + $len) ($y + $fs)
    }

    if ($minX -ge $maxX) { "$code : could not determine bounds"; continue }

    $minX -= $MarginMm; $maxX += $MarginMm; $minY -= $MarginMm; $maxY += $MarginMm
    $w = $maxX - $minX; $h = $maxY - $minY
    $pxW = [int][Math]::Ceiling($w * $PxPerMm); $pxH = [int][Math]::Ceiling($h * $PxPerMm)

    # Retarget the SVG at just the circuit, then let Edge rasterise it.
    $cropped = [regex]::Replace($svg,
        'width="[^"]*"\s+height="[^"]*"\s+viewBox="[^"]*"',
        ('width="{0}px" height="{1}px" viewBox="{2:F3} {3:F3} {4:F3} {5:F3}"' -f $pxW, $pxH, $minX, $minY, $w, $h),
        [System.Text.RegularExpressions.RegexOptions]::Singleline)

    $cropSvg = Join-Path $work "$code`_crop.svg"
    Set-Content -Path $cropSvg -Value $cropped -Encoding UTF8

    $png = Join-Path $OutDir "$code`_circuit.png"
    if (Test-Path $png) { Remove-Item $png -Force }
    if (Test-Path $Edge) {
        $profile = Join-Path $work "edgeprofile"
        & $Edge --headless=new --disable-gpu --hide-scrollbars --no-first-run `
                "--user-data-dir=$profile" "--window-size=$pxW,$pxH" `
                "--screenshot=$png" ("file:///" + $cropSvg.Replace('\','/')) 2>&1 | Out-Null
        Start-Sleep -Milliseconds 400
    }

    $ok = Test-Path $png
    "{0} : pdf copied, png {1} ({2}x{3}px, {4}x{5}mm)" -f $code,
        $(if ($ok) { "ok" } else { "FAILED" }), $pxW, $pxH, [Math]::Round($w,1), [Math]::Round($h,1)
}

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue

