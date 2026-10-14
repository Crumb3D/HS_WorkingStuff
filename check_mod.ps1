# Pre-boot check. Exit 1 if anything is wrong.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Game = "C:\Program Files (x86)\Steam\steamapps\common\7 Days To Die"
$fail = 0

function Fail($msg) {
    Write-Host "FAIL  $msg" -ForegroundColor Red
    $script:fail++
}
function Ok($msg) { Write-Host "ok    $msg" -ForegroundColor Green }

$vanillaBlocks = [regex]::Matches((Get-Content (Join-Path $Game "Data\Config\blocks.xml") -Raw), '<block name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$vanillaItems = [regex]::Matches((Get-Content (Join-Path $Game "Data\Config\items.xml") -Raw), '<item name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
$known = @{}
foreach ($n in ($vanillaItems + $vanillaBlocks)) { $known[$n] = $true }

$blocksXml = Get-Content (Join-Path $Root "Config\blocks.xml") -Raw
$recipeXml = Get-Content (Join-Path $Root "Config\recipes.xml") -Raw
$itemsXml = Get-Content (Join-Path $Root "Config\items.xml") -Raw
$lootXml = Get-Content (Join-Path $Root "Config\loot.xml") -Raw
$menuXml = Get-Content (Join-Path $Root "Config\XUi_Menu\windows.xml") -Raw
$settings = Get-Content (Join-Path $Root "HSWorkingStuffSettings.json") -Raw

try { [xml]$blocksXml | Out-Null; Ok "blocks.xml" } catch { Fail "blocks.xml: $_" }
try { [xml]$recipeXml | Out-Null; Ok "recipes.xml" } catch { Fail "recipes.xml: $_" }
try { [xml]$itemsXml | Out-Null; Ok "items.xml" } catch { Fail "items.xml: $_" }
try { [xml]$lootXml | Out-Null; Ok "loot.xml" } catch { Fail "loot.xml: $_" }
try { [xml]$menuXml | Out-Null; Ok "XUi_Menu/windows.xml" } catch { Fail "XUi_Menu/windows.xml: $_" }

foreach ($n in ([regex]::Matches($blocksXml, '<block name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })) {
    $known[$n] = $true
    Ok "block $n"
}
foreach ($n in ([regex]::Matches($itemsXml, '<item name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })) {
    $known[$n] = $true
    Ok "item $n"
}

$names = @()
foreach ($n in ([regex]::Matches($recipeXml, '<recipe name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })) {
    $names += $n
    if (-not $known.ContainsKey($n)) { Fail "recipe for unknown '$n'" } else { Ok "recipe $n" }
}
foreach ($n in ([regex]::Matches($recipeXml, '<ingredient name="([^"]+)"') | ForEach-Object { $_.Groups[1].Value })) {
    if (-not $known.ContainsKey($n)) { Fail "ingredient unknown '$n'" }
}
if ($names.Count -ne 64) { Fail "expected 64 recipes, found $($names.Count)" } else { Ok "64 recipes" }
$grillRecipes = [regex]::Matches($recipeXml, 'craft_area="hsGrill"').Count
if ($grillRecipes -ne 6) { Fail "expected 6 grill recipes, found $grillRecipes" } else { Ok "6 grill recipes" }
if ([regex]::Matches($recipeXml, 'craft_area="hsGrill"[^>]*craft_tool').Count -gt 0) { Fail "grill recipe needs a tool" } else { Ok "grill recipes need no tool" }
foreach ($m in [regex]::Matches($recipeXml, '<recipe name="(hs[A-Za-z]+)"[^>]*>')) {
    $r = $m.Groups[1].Value
    if ($m.Value -notmatch 'learnable') { Fail "$r is craftable without learning it" } else { Ok "$r is learnable" }
}
$coffeeRecipes = [regex]::Matches($recipeXml, 'craft_area="hsCoffeeMaker"').Count
if ($coffeeRecipes -ne 2) { Fail "expected 2 coffee maker recipes, found $coffeeRecipes" } else { Ok "2 coffee maker recipes" }
if ([regex]::Matches($recipeXml, 'craft_area="hsCoffeeMaker"[^>]*craft_tool').Count -gt 0) { Fail "coffee maker recipe needs a tool" } else { Ok "coffee maker recipes need no tool" }
$microRecipes = [regex]::Matches($recipeXml, 'craft_area="hsMicrowave"').Count
if ($microRecipes -ne 11) { Fail "expected 11 microwave recipes, found $microRecipes" } else { Ok "11 microwave recipes" }
foreach ($bad in @("foodChiliDog", "foodChickenNuggets", "drinkJarBlackStrapCoffee")) {
    if ($recipeXml -match "name=`"$bad`" count=`"1`" craft_area=`"hsMicrowave`"") { Fail "microwave still cooks $bad" } else { Ok "microwave skips $bad" }
}
if ([regex]::Matches($recipeXml, 'craft_area="hsMicrowave"[^>]*craft_tool').Count -gt 0) { Fail "microwave recipe needs a tool" } else { Ok "microwave recipes need no tool" }

$soundsXml = Get-Content (Join-Path $Root "Config\sounds.xml") -Raw
$progXml = Get-Content (Join-Path $Root "Config\progression.xml") -Raw
try { [xml]$soundsXml | Out-Null; Ok "sounds.xml" } catch { Fail "sounds.xml: $_" }
try { [xml]$progXml | Out-Null; Ok "progression.xml" } catch { Fail "progression.xml: $_" }
foreach ($s in @("hsMicrowaveHum", "hsMicrowaveDing")) {
    if ($soundsXml -notmatch "name=`"$s`"") { Fail "sounds.xml missing $s" } else { Ok "sound $s" }
}
$unlocks = [regex]::Matches($progXml, '<passive_effect name="RecipeTagUnlocked"[^>]*tags="([^"]+)">\s*<requirement name="ProgressionLevel" progression_name="perk')
$gated = @{}
foreach ($u in $unlocks) { foreach ($t in $u.Groups[1].Value.Split(',')) { $gated[$t] = $true } }
foreach ($r in ([regex]::Matches($recipeXml, '<recipe name="(hs[A-Za-z]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)) {
    if (-not $gated.ContainsKey($r)) { Fail "progression never unlocks $r behind a magazine and a perk" } else { Ok "unlock $r (magazine + perk)" }
}
if ($progXml -match '<passive_effect name="RecipeTagUnlocked"[^>]*/>') { Fail "an unlock has no perk requirement" } else { Ok "every unlock needs a perk" }

foreach ($cls in @("HSFridge, HSWorkingStuff", "HSStorage, HSWorkingStuff", "HSSink, HSWorkingStuff", "HSTank, HSWorkingStuff", "HSRainCatch, HSWorkingStuff", "HSHeater, HSWorkingStuff", "HSStove, HSWorkingStuff", "HSMicrowave, HSWorkingStuff", "HSRadiator, HSWorkingStuff", "HSCoffeeMaker, HSWorkingStuff", "HSWaterCooler, HSWorkingStuff", "HSShower, HSWorkingStuff", "HSAirCon, HSWorkingStuff")) {
    if ($blocksXml -notmatch [regex]::Escape($cls)) { Fail "blocks.xml missing class '$cls'" } else { Ok "class $cls" }
}

$ingameXml = Get-Content (Join-Path $Root "Config\XUi_InGame\windows.xml") -Raw
$locTxt = Get-Content (Join-Path $Root "Config\Localization.txt") -Raw
if ($menuXml -notmatch "btnConnectToIp") { Fail "main menu missing btnConnectToIp" } else { Ok "btnConnectToIp" }
if ($menuXml -notmatch 'caption="Connect to IP"') { Fail "Connect to IP caption is not human text" } else { Ok "Connect to IP caption" }
if ($ingameXml -notmatch 'text="Fridge"') { Fail "fridge window missing Fridge title" } else { Ok "fridge window title" }
$xuiXml = Get-Content (Join-Path $Root "Config\XUi_InGame\xui.xml") -Raw
if ($xuiXml -notmatch "workstation_hsMicrowave" -or $blocksXml -notmatch "workstation_hsMicrowave") { Fail "microwave window group missing" } else { Ok "microwave window" }
if ($xuiXml -notmatch 'workstation_hsGrill[\s\S]*?windowFuel' -or $blocksXml -notmatch "workstation_hsGrill") { Fail "grill window group missing" } else { Ok "grill window" }
$freezer = [regex]::Match($blocksXml, '<block name="hsFreezer">[\s\S]*?</block>').Value
if ($freezer -notmatch 'HSChestFreezer' -or $freezer -notmatch 'HSFridge, HSWorkingStuff' -or $freezer -notmatch 'name="ModelOffset" value="0,0,0"') { Fail "freezer is not the chest freezer model" } else { Ok "chest freezer" }
if ($xuiXml -notmatch "HSStorageWindowGroup, HSWorkingStuff") { Fail "fridge window group missing assembly controller" } else { Ok "fridge window group controller" }
if ($ingameXml -notmatch "HSStorageGrid, HSWorkingStuff") { Fail "fridge grid missing assembly controller" } else { Ok "fridge grid controller" }
if ($locTxt -notmatch "blockcommand_storefood") { Fail "missing blockcommand_storefood" } else { Ok "blockcommand loc" }
foreach ($b in @("hsWaterTank", "hsRainCatch")) {
    $m = [regex]::Match($blocksXml, "<block name=`"$b`">[\s\S]*?</block>")
    if (-not $m.Success) { Fail "missing block $b" }
    elseif ($m.Value -match 'FuelTypes|CollectorType|RequiredMods') { Fail "$b is still a jar collector" }
    else { Ok "$b needs no jars" }
}
if ($xuiXml -notmatch 'workstation_hsStove[\s\S]*?windowHSStovePower[\s\S]*?workstation_hsMicrowave[\s\S]*?windowHSStovePower') { Fail "cooker windows missing power panel" } else { Ok "cooker power panel" }
if ($ingameXml -notmatch "HSStovePowerWindow, HSWorkingStuff") { Fail "power panel missing controller" } else { Ok "power panel controller" }
if ($ingameXml -notmatch "HSPipeHud, HSWorkingStuff" -or $xuiXml -notmatch 'HUDHSPipe') { Fail "pipe tool HUD missing" } else { Ok "pipe tool HUD" }
if ($itemsXml -notmatch 'name="hsPipeTool"' -or $itemsXml -notmatch "HSPipeTool, HSWorkingStuff") { Fail "pipe tool item missing" } else { Ok "pipe tool item" }
foreach ($k in @("blockcommand_filljarhot", "blockcommand_fillbucket", "blockcommand_fillbuckethot", "blockcommand_colddry", "blockcommand_hotnoheater", "blockcommand_hotnopower", "blockcommand_hotdry", "blockcommand_addtools", "blockcommand_notools", "blockcommand_removetools", "hsPipeToolHint", "hsPipeRoute", "hsStovePanelOn", "hsSinkHotNoHeater", "hsRadiator", "hsRadiatorDesc", "blockcommand_heatingon", "blockcommand_heatingoff", "hsBoilerIdle", "buffHSRadiatorWarmName", "hsPipeRadialTakeUp", "hsPipeRemoved", "hsPipeRadialNew", "hsPipeRadialNewSystem", "hsPipeRadialUseSystem", "hsPipeSystemNew", "hsPipeSystemUse", "hsPipeNeedSystem", "hsPipeAlreadyJoined", "hsPipeHudSystem", "hsPipeExisting", "hsPipeNoFloor", "hsPipeUnderFloor", "blockcommand_valveup", "blockcommand_valvedown", "hsRadiatorGauge", "hsRadiatorValve", "hsRadiatorOff", "blockcommand_coldempty", "blockcommand_hotempty", "hsSinkColdEmpty", "hsSinkHotEmpty", "hsRadiatorDry", "hsBoilerNeedsWater", "hsBoilerFilled", "hsRainDrained", "hsCoffeeMaker", "hsCoffeeMakerCommercial", "hsCoffeeMakerDesc", "hsCoffeePowered", "hsCoffeeUnpowered", "hsCoffeeBrewing", "hsCoffeeDone", "hsWaterEmpty", "hsFreezer", "hsFreezerDesc", "hsFreezerPowered", "hsFreezerWindow", "hsWaterCooler", "hsWaterCoolerDesc", "hsCoolerFiltered", "hsShower", "hsShowerDesc", "hsBath", "hsBathDesc", "blockcommand_wash", "hsWashUses", "hsWashShort", "hsWashShower", "hsWashBath", "buffHSWashedName", "buffHSWashedBathName", "buffHSWashedDesc", "hsAirCon", "hsAirConDesc", "hsAirConOn", "hsAirConOff", "hsAirConCeiling", "hsAirConCeilingDesc", "buffHSAirConCoolName", "buffHSAirConCoolDesc", "hsGasGrill", "hsGasGrillDesc", "hsCharcoalGrill", "hsCharcoalGrillDesc")) {
    if ($locTxt -notmatch "(?m)^$k,") { Fail "missing loc $k" } else { Ok "loc $k" }
}
if ([regex]::Match($blocksXml, '<block name="hsStoveOld">[\s\S]*?</block>').Value -match 'Modules" value="[^"]*fuel') { Fail "stove still has fuel module" } else { Ok "stove has no fuel" }
if ($blocksXml -notmatch "workstation_hsStove") { Fail "stove missing electric cooker window" } else { Ok "electric cooker window" }
if ($blocksXml -notmatch 'hsStoveOld[\s\S]*RequiredPower') { Fail "stove missing RequiredPower" } else { Ok "stove RequiredPower" }

$buffsXml = Get-Content (Join-Path $Root "Config\buffs.xml") -Raw
try { [xml]$buffsXml | Out-Null; Ok "buffs.xml" } catch { Fail "buffs.xml: $_" }
foreach ($b in @("buffHSRadiatorWarm", "buffHSRadiatorWarmEffect", "buffHSAirConCool", "buffHSAirConCoolEffect", "buffHSWashed", "buffHSWashedBath", "buffHSSmelly", "buffHSDirtyClothes", "buffHSWetClothes", "buffHSWashedSink")) {
    if ($buffsXml -notmatch "name=`"$b`"") { Fail "buffs.xml missing $b" } else { Ok "buff $b" }
}

$newBlocks = @{ "hsFridgeWhite" = "HSFridge"; "hsWasher" = "HSLaundry"; "hsWasherVanilla" = "HSLaundry"; "hsDryerVanilla" = "HSLaundry"; "hsThermometer" = "HSThermometer"; "hsSocketUKSingle" = "HSWallSwitch"; "hsSocketUSSingle" = "HSWallRelay" }
foreach ($b in $newBlocks.Keys) {
    $def = [regex]::Match($blocksXml, "<block name=`"$b`">[\s\S]*?</block>").Value
    if ($def -notmatch "value=`"$($newBlocks[$b]), HSWorkingStuff`"") { Fail "$b is not class $($newBlocks[$b])" } else { Ok "$b class" }
}
foreach ($b in @("hsDryer", "hsSocketUKDouble", "hsSocketUSDouble", "hsLightSwitchUK", "hsLightSwitchUS", "hsCookerSwitchUK", "hsRangeOutletUS")) {
    if ($blocksXml -notmatch "<block name=`"$b`">") { Fail "missing block $b" } else { Ok "block $b" }
}
foreach ($p in @(@("hsSocketUKSingle", 1), @("hsSocketUKDouble", 2), @("hsSocketUSSingle", 1), @("hsSocketUSDouble", 2), @("hsCookerSwitchUK", 1), @("hsRangeOutletUS", 1))) {
    $def = [regex]::Match($blocksXml, "<block name=`"$($p[0])`">[\s\S]*?</block>").Value
    if ($def -notmatch "name=`"HSPlugs`" value=`"$($p[1])`"") { Fail "$($p[0]) should take $($p[1]) plugs" } else { Ok "$($p[0]) takes $($p[1]) plugs" }
}
foreach ($b in @("hsFridgeWhite", "hsWasher", "hsWasherVanilla", "hsDryer", "hsDryerVanilla", "hsThermometer", "hsSocketUKSingle", "hsSocketUKDouble", "hsSocketUSSingle", "hsSocketUSDouble", "hsLightSwitchUK", "hsLightSwitchUS", "hsCookerSwitchUK", "hsRangeOutletUS")) {
    if ($locTxt -notmatch "(?m)^$b,") { Fail "missing loc $b" }
    if ($progXml -notmatch "\b$b\b") { Fail "$b is not unlocked anywhere" } else { Ok "$b unlocked" }
}
foreach ($k in @("hsWasherDesc", "hsDryerDesc", "hsThermometerDesc", "hsFridgeWhiteDesc", "hsSocketUKDesc", "hsSocketUSDesc", "hsLightSwitchUKDesc", "hsCookerSwitchUKDesc", "blockcommand_hswash", "blockcommand_hsdry", "hsThermoRoom", "hsClothesDirt", "hsClothesWet", "hsWashed", "hsPlugFullOne", "hsPlugUsed", "buffHSSmellyName", "buffHSDirtyClothesName", "buffHSWetClothesName", "hsLightSwitchUSDesc", "blockcommand_washup", "hsLblWash", "hsSinkWashed", "hsWashLakeside", "hsWashTooSoon", "hsWashSoakStart", "hsWashSoakDone", "blockcommand_hsoutlet0", "blockcommand_hsoutlet1", "hsOutletState", "hsRangeOutletUSDesc", "hsSinkDrained")) {
    if ($locTxt -notmatch "(?m)^$k,") { Fail "missing loc $k" } else { Ok "loc $k" }
}
foreach ($c in @(@("HSSocketUKSingle", "HSSocketUKSingle_Plug0"), @("HSSocketUKDouble", "HSSocketUKDouble_Plug1"), @("HSSocketUKDouble", "HSSocketUKDouble_SwOn1"), @("HSSocketUSDouble", "HSSocketUSDouble_Plug1"), @("HSLightSwitchUS", "HSLightSwitchUS_SwOn"), @("HSLightSwitchUK", "HSLightSwitchUK_SwOff"), @("HSCookerSwitchUK", "HSCookerSwitchUK_Lamp"), @("HSRangeOutletUS", "HSRangeOutletUS_Plug0"))) {
    $pf = Join-Path $Root "_unity\Assets\HSWorkingStuff\Prefabs\$($c[0]).prefab"
    if (-not (Test-Path $pf) -or -not (Select-String -Path $pf -Pattern "m_Name: $($c[1])" -SimpleMatch -Quiet)) { Fail "$($c[0]) is missing child $($c[1])" } else { Ok "$($c[0]) has $($c[1])" }
}
$bundle = Join-Path $Root "Resources\HSWorkingStuff.unity3d"
if (-not (Test-Path $bundle) -or (Get-Item $bundle).Length -lt 10000) { Fail "model bundle missing (run build_assets.ps1)" } else { Ok "model bundle" }
foreach ($m in ([regex]::Matches($blocksXml, 'HSWorkingStuff\.unity3d\?([A-Za-z0-9_]+)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)) {
    if (-not (Test-Path (Join-Path $Root "_unity\Assets\HSWorkingStuff\Prefabs\$m.prefab"))) { Fail "no prefab built for $m" } else { Ok "model $m" }
}
foreach ($i in ([regex]::Matches($blocksXml, 'name="CustomIcon" value="(hs[A-Za-z0-9_]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)) {
    if ($known.ContainsKey($i)) { continue }
    if (-not (Test-Path (Join-Path $Root "UIAtlases\ItemIconAtlas\$i.png"))) { Fail "icon $i.png missing" } else { Ok "icon $i" }
}
$wall = [regex]::Match($blocksXml, '<block name="hsWaterHeater">[\s\S]*?</block>').Value
if ($wall -notmatch 'TowardsPlacerInverted' -or $wall -notmatch 'HSCombiBoiler' -or $wall -match 'cntWasher') { Fail "boiler is not the wall-hung model" } else { Ok "boiler is wall-hung" }
foreach ($b in @("hsWaterHeater", "hsRadiator")) {
    $def = [regex]::Match($blocksXml, "<block name=`"$b`">[\s\S]*?</block>").Value
    if ($def -notmatch 'name="ModelOffset" value="0,0,0"') { Fail "$b needs ModelOffset 0,0,0 (bundle model origin is the cell bottom)" } else { Ok "$b sits on the cell floor" }
}
foreach ($k in @("HotWaterSeconds", "RadiatorWatts", "RadiatorRadius", "FreezerMultiplier", "AirConRadius", "ShowerJars", "BathJars", "GasCanGrillSeconds", "EnableRoomTemperature", "IndoorInsulation", "IndoorLagHours", "RadiatorHeat", "ThermostatHeat", "AirConCool", "ThermostatCool", "EnableBodySmell", "BodySmellStartHours", "BodySmellFullHours", "BodySmellRadius", "EnableLaundry", "ClothesDirtyHours", "DirtyClothesSmell", "WasherJars", "AirDryHours", "WetClothesCold", "WaterWashHours")) {
    if ($settings -notmatch "`"$k`"") { Fail "JSON missing $k" } else { Ok "json $k" }
}

if ($locTxt -notmatch "hsAuthServerStarting") { Fail "missing hsAuthServerStarting loc" } else { Ok "join-hold loc" }
foreach ($flag in @("EnableSpoil", "EnableFridge", "EnableStove", "EnablePlumbing", "EnableConnectToIp", "HideNews", "HoldJoinUntilReady")) {
    if ($settings -notmatch [regex]::Escape("`"$flag`"")) { Fail "JSON missing $flag" } else { Ok "json $flag" }
}

$dll = Join-Path $Root "HSWorkingStuff.dll"
if (-not (Test-Path $dll)) { Fail "HSWorkingStuff.dll missing" }
else {
    $ascii = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dll))
    foreach ($t in @("HSWorkingStuffMod", "HSWorkingStuffSettings", "BlockHSFridge", "TileEntityHSFridge", "HSSpoil", "HSWater", "HSWorkingStuffNet", "HSPatchConnectToIp", "EnableConnectToIp", "HSLoc", "HSPatchLocGet", "BlockHSStove", "HSStovePower", "HideNews", "HSPatchHideNewsSplash", "HSPatchHideNewsCount", "HSJoinHold", "HSPatchJoinHoldAuthorize", "HoldJoinUntilReady", "HSPatchJoinHoldAuthState", "HSPatchJoinHoldGameStarted", "HSPatchJoinHoldAuthStart", "HSItem", "HSInvData", "HSFridgeInv", "IHSFridgeInv", "CreateTe33", "Load33", "HSPatchFridgeRead", "HSRadiatorValve", "GaugeLine", "ColdReason", "FillFromSupply", "UnderSink", "DrainMurky", "XUiC_HSRainCatchWindowGroup", "XUiC_HSRainToolGrid", "BlockHSCoffeeMaker", "HSPatchRainQuickMove", "TryQuickAdd", "HSPatchStoveWireHold", "HSPatchStoveWindowHalt", "HSPatchSwitchText", "EnsureTe33", "BlockHSMicrowave", "HSMicrowaveSound", "BlockHSRainCatch", "FindTaps", "HSPatchRadialGrey", "HSPatchLegacyCollectorTick", "ItemActionHSPipeTool", "HSPipeHud", "HSPipeToolInteract", "HSStovePowerWindow", "HSPowerDemand", "HSPatchIdleDemand", "HSBoiler", "BlockHSRadiator", "HSPipeNet", "TakeUp", "HSModelHit", "RootTransformRefParent", "RepairWorkstation", "NewConnection", "NewSystem", "UseSystem", "Hugs", "Ports", "IntoFixtures", "FindBoiler", "Covers", "FloorCell", "getFacesDrawnFullBitfield", "RouteUnder", "RouteSurface", "UseFloorPipe", "Joins", "Relayout", "SetPreview", "BlockHSWaterCooler", "BlockHSShower", "HSWash", "HSPatchWashedSmell", "SmellUpdateItemsAndBlood", "BlockHSAirCon", "HSAirCon", "HSPatchGasGrillFuel", "GetFuelTime", "BuildCold", "HSRoomTemp", "HSPatchRoomTemp", "BlockHSThermometer", "HSLaundry", "BlockHSLaundry", "HandleLaundry", "HSPlugs", "HSPatchSocketPlugs", "HSPatchSocketPlugShow", "HSOriginalMaterials", "HSOutlets", "HSPatchOutletPower", "HSPatchWaterWash", "HSPatchWaterWashPrompt", "NaturalWash", "QuickWashHours", "BlockHSWallSwitch", "BlockHSWallRelay")) {
        if ($ascii.IndexOf($t) -lt 0) { Fail "DLL missing '$t'" } else { Ok "dll $t" }
    }
}

powershell -NoProfile -File (Join-Path $Root "check_refs.ps1") -Dll $dll
if ($LASTEXITCODE -ne 0) { Fail "DLL references a game member missing on 3.2 or 3.3" } else { Ok "game references resolve on 3.2 and 3.3" }

if ($fail -gt 0) {
    Write-Host ("{0} check(s) failed." -f $fail) -ForegroundColor Red
    exit 1
}
Write-Host "All checks passed." -ForegroundColor Green
exit 0
