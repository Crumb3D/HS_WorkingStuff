# HS_WorkingStuff - rebuild FBX from Blender, the inventory icons, then the Unity asset bundle.
# Requires Unity 2022.3.62f2 (the game's version). Do not use Unity 6.

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$BlenderScript = Join-Path $Root "_blender\hsws_models.py"
$Models = Join-Path $Root "_unity\Assets\HSWorkingStuff\Models"
$Blend = Join-Path $Root "_blender\hsws_models.blend"
$IconPy = Join-Path $Root "_blender\hsws_icons.py"
$IconOut = Join-Path $Root "UIAtlases\ItemIconAtlas"
$UnityProj = Join-Path $Root "_unity"
$UnityLog = Join-Path $Root "_unity_build.log"
$UnityExe = "C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe"

function Find-OfficialBlender {
    $candidates = @()
    foreach ($dir in @("C:\Program Files\Blender Foundation", (Join-Path $env:LOCALAPPDATA "Programs\Blender"))) {
        if (Test-Path $dir) {
            $candidates += Get-ChildItem $dir -Recurse -Filter blender.exe -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -notmatch '\\WindowsApps\\' } |
                Sort-Object FullName -Descending |
                Select-Object -ExpandProperty FullName
        }
    }
    foreach ($p in $candidates) {
        if ($p -and (Test-Path $p)) { return $p }
    }
    return $null
}

$blender = Find-OfficialBlender
if (-not $blender) { throw "Official blender.exe not found. Install from blender.org (not the Microsoft Store)." }
if (-not (Test-Path $UnityExe)) { throw "Unity 2022.3.62f2 not found at $UnityExe" }

Write-Host "Blender: $blender"
& $blender --background --factory-startup --python $BlenderScript -- $Models $Blend
if ($LASTEXITCODE -ne 0) { throw "Blender export failed ($LASTEXITCODE)" }

Write-Host "Icons: $IconOut"
& $blender --background $Blend --python $IconPy -- $IconOut
if ($LASTEXITCODE -ne 0) { throw "Icon render failed ($LASTEXITCODE)" }

Write-Host "Unity: $UnityExe"
& $UnityExe -batchmode -nographics -projectPath $UnityProj -executeMethod HSWorkingStuffBuild.Build -quit -logFile $UnityLog
if ($LASTEXITCODE -ne 0) { throw "Unity bundle build failed ($LASTEXITCODE). See $UnityLog" }
$report = Join-Path $UnityProj "build_report.txt"
if (Test-Path $report) { Get-Content $report }
if (-not (Test-Path (Join-Path $Root "Resources\HSWorkingStuff.unity3d"))) { throw "Bundle missing. See $UnityLog" }
Write-Host "Done. Bundle: $(Join-Path $Root 'Resources\HSWorkingStuff.unity3d')"
