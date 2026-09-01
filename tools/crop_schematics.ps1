# Regenerates ProtoVerseApp/Assets/Schematics/*.pdf from the KiCad sources.
#
# Two steps:
#
#   1. Export each schematic with kicad-cli's exclude-drawing-sheet option,
#      which suppresses the border and title block, leaving only the circuit.
#
#   2. Crop the result down to the drawn content. Step 1 still emits an A4
#      page, so the circuit floats in whitespace. There is no PDF tooling on
#      this machine (no gs/qpdf/python), so the crop rewrites the page's
#      /MediaBox in place, PADDED TO THE SAME BYTE LENGTH - that keeps every
#      xref offset in the file valid, which inserting a /CropBox would not.
#      The content bounds come from a throwaway SVG export of the same
#      schematic, whose geometry is plain-text millimetres; the PDF's own
#      content stream is Flate-compressed and not worth decoding for this.
#
# Re-run after any schematic change. Takes seconds - schematics are a build
# step here, not per-manual work. Requires KiCad (tested against 9.0).

param(
    [string]$Dir = "$PSScriptRoot\..\ProtoVerseApp\Assets\Schematics",
    [string]$SourceRoot = "$PSScriptRoot\..\..\..\Finished Modules",
    [string]$KiCadCli = "E:\KiCad 9.0\bin\kicad-cli.exe",
    [double]$MarginMm = 2.0
)

$enc = [System.Text.Encoding]::GetEncoding(28591)
$MM_TO_PT = 72.0 / 25.4

# code -> schematic file, relative to $SourceRoot. The .kicad_sch names don't
# follow the circuit codes, hence the explicit map.
#
# NB: this must not be called $sources. PowerShell variable names are
# case-insensitive, so $sources and the $SourceRoot parameter would be the same
# variable, and assigning the hashtable would silently destroy the path - which
# is exactly what happened the first time this script was written.
$schematicSources = @{
    "A01" = "PC01_A01_ProtoMod_DDS\Rev01\Protomod_DDS.kicad_sch"
    "E03" = "PC01_E03_ProtoMod_Accelerometer\Rev01\Protomod_Accel.kicad_sch"
    "E05" = "PC01_E05_ProtoMod_ElectronicLoad\Rev01\Protomod_ElectronicLoad.kicad_sch"
    "F00" = "PC01_F00_ProtoMod_Headers\Rev01\Protomod_Headers.kicad_sch"
    "F01" = "PC01_F01_ProtoMod_Blinky\Rev01\Protomod_Starter.kicad_sch"
    "F02" = "PC01_F02_ProtoMod_simpleLED\Rev01\Protomod_simpleLED.kicad_sch"
}

$map = @{}
New-Item -ItemType Directory -Force -Path $Dir | Out-Null

if (-not (Test-Path $KiCadCli)) {
    Write-Warning "kicad-cli not found at $KiCadCli - skipping export, will only crop any PDFs already present."
} else {
    foreach ($code in $schematicSources.Keys | Sort-Object) {
        $src = Join-Path $SourceRoot $schematicSources[$code]
        if (-not (Test-Path $src)) { "$code : source schematic missing"; continue }
        $pdf = Join-Path $Dir "$code`_schematic.pdf"
        & $KiCadCli sch export pdf --exclude-drawing-sheet --no-background-color -o $pdf $src 2>&1 | Out-Null
        # SVG is only produced to measure the content bounds; deleted below.
        & $KiCadCli sch export svg --exclude-drawing-sheet --no-background-color -o $Dir $src 2>&1 | Out-Null
        $map[$code] = [System.IO.Path]::GetFileNameWithoutExtension($src) + ".svg"
    }
}

foreach ($code in $map.Keys | Sort-Object) {
    $svgPath = Join-Path $Dir $map[$code]
    $pdfPath = Join-Path $Dir "$code`_schematic.pdf"
    if (-not (Test-Path $svgPath) -or -not (Test-Path $pdfPath)) { "$code : missing input"; continue }

    $svg = Get-Content $svgPath -Raw

    # Page height in mm, needed to flip SVG's top-down Y into PDF's bottom-up Y.
    $vb = [regex]::Match($svg, 'viewBox="([\d\.\-]+)\s+([\d\.\-]+)\s+([\d\.\-]+)\s+([\d\.\-]+)"')
    $pageH = [double]$vb.Groups[4].Value

    $minX = [double]::MaxValue; $minY = [double]::MaxValue
    $maxX = [double]::MinValue; $maxY = [double]::MinValue

    # Path data: "M x,y" then bare "x,y" pairs, all absolute, all mm.
    foreach ($d in [regex]::Matches($svg, 'd="([^"]*)"')) {
        foreach ($pair in [regex]::Matches($d.Groups[1].Value, '(-?\d+\.?\d*),(-?\d+\.?\d*)')) {
            $x = [double]$pair.Groups[1].Value; $y = [double]$pair.Groups[2].Value
            if ($x -lt $minX) { $minX = $x }; if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }; if ($y -gt $maxY) { $maxY = $y }
        }
    }
    # Circles (junction dots) - include the radius.
    foreach ($c in [regex]::Matches($svg, '<circle\s+cx="(-?\d+\.?\d*)"\s+cy="(-?\d+\.?\d*)"\s+r="(-?\d+\.?\d*)"')) {
        $x = [double]$c.Groups[1].Value; $y = [double]$c.Groups[2].Value; $r = [double]$c.Groups[3].Value
        if (($x-$r) -lt $minX) { $minX = $x-$r }; if (($x+$r) -gt $maxX) { $maxX = $x+$r }
        if (($y-$r) -lt $minY) { $minY = $y-$r }; if (($y+$r) -gt $maxY) { $maxY = $y+$r }
    }

    if ($minX -ge $maxX -or $minY -ge $maxY) { "$code : could not determine bounds"; continue }

    $minX -= $MarginMm; $maxX += $MarginMm; $minY -= $MarginMm; $maxY += $MarginMm

    # SVG y grows downward, PDF y grows upward.
    $x0 = [Math]::Round($minX * $MM_TO_PT, 2)
    $x1 = [Math]::Round($maxX * $MM_TO_PT, 2)
    $y0 = [Math]::Round(($pageH - $maxY) * $MM_TO_PT, 2)
    $y1 = [Math]::Round(($pageH - $minY) * $MM_TO_PT, 2)

    $bytes = [System.IO.File]::ReadAllBytes($pdfPath)
    $txt = $enc.GetString($bytes)
    $m = [regex]::Match($txt, '/MediaBox\s*\[[^\]]*\]')
    if (-not $m.Success) { "$code : no MediaBox"; continue }

    $new = "/MediaBox [$x0 $y0 $x1 $y1]"
    if ($new.Length -gt $m.Value.Length) {
        # Trim precision until it fits the original byte length.
        $new = "/MediaBox [$([Math]::Round($x0)) $([Math]::Round($y0)) $([Math]::Round($x1)) $([Math]::Round($y1))]"
    }
    if ($new.Length -gt $m.Value.Length) { "$code : replacement too long, skipped"; continue }
    # Pad inside the brackets so total byte length is unchanged and all xref
    # offsets stay valid.
    $new = $new.Substring(0, $new.Length - 1).PadRight($m.Value.Length - 1) + "]"

    $txt = $txt.Remove($m.Index, $m.Length).Insert($m.Index, $new)
    [System.IO.File]::WriteAllBytes($pdfPath, $enc.GetBytes($txt))

    $wmm = [Math]::Round($maxX - $minX, 1); $hmm = [Math]::Round($maxY - $minY, 1)
    "$code : cropped to ${wmm}mm x ${hmm}mm  (was 297 x 210)"
}

# The SVGs existed only to measure content bounds - they're ~600-900KB each and
# nothing ships them.
Remove-Item (Join-Path $Dir "*.svg") -ErrorAction SilentlyContinue
