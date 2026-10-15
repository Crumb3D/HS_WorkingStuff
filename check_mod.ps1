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
if ($names.Count -ne 96) { Fail "expected 96 recipes, found $($names.Count)" } else { Ok "96 recipes" }
if (-not (Test-Path (Join-Path $Root "TV\README.txt"))) { Fail "TV\README.txt missing" } else { Ok "TV folder" }
foreach ($i in @("hsFuse", "hsFuseBlown", "hsBreaker")) {
    if (-not (Test-Path (Join-Path $Root "UIAtlases\ItemIconAtlas\$i.png"))) { Fail "item icon $i.png missing" } else { Ok "item icon $i" }
}
foreach ($i in @("hsFuse5", "hsFuse15", "hsFuse30", "hsFuse45", "hsBreaker5", "hsBreaker15", "hsBreaker30", "hsBreaker45")) {
    if (-not $known.ContainsKey($i)) { Fail "missing item $i" }
    elseif ($i -notmatch '^hs(Fuse|Breaker)\d+$') { Fail "$i has no amp rating in its name" } else { Ok "$i rated" }
}
$grillRecipes = [regex]::Matches($recipeXml, 'craft_area="hsGrill"').Count
if ($grillRecipes -ne 6) { Fail "expected 6 grill recipes, found $grillRecipes" } else { Ok "6 grill recipes" }
if ([regex]::Matches($recipeXml, 'craft_area="hsGrill"[^>]*craft_tool').Count -gt 0) { Fail "grill recipe needs a tool" } else { Ok "grill recipes need no tool" }
foreach ($m in [regex]::Matches($recipeXml, '<recipe name="(hs[A-Za-z0-9]+)"[^>]*>')) {
    $r = $m.Groups[1].Value
    if ($m.Value -notmatch 'learnable') { Fail "$r is craftable without learning it" } else { Ok "$r is learnable" }
}
$coffeeRecipes = [regex]::Matches($recipeXml, 'craft_area="hsCoffeeMaker"').Count
if ($coffeeRecipes -ne 3) { Fail "expected 3 coffee maker recipes, found $coffeeRecipes" } else { Ok "3 coffee maker recipes" }
if ([regex]::Matches($recipeXml, 'craft_area="hsKettle"').Count -ne 0) { Fail "the kettle is filled and poured by hand: no hsKettle recipes" } else { Ok "kettle has no recipes" }
foreach ($a in @(,@("hsToaster", 1))) {
    $n = [regex]::Matches($recipeXml, "craft_area=`"$($a[0])`"").Count
    if ($n -ne $a[1]) { Fail "expected $($a[1]) $($a[0]) recipes, found $n" } else { Ok "$n $($a[0]) recipes" }
    if ($blocksXml -notmatch "CraftingAreaRecipes`" value=`"$($a[0])`"") { Fail "no block crafts in $($a[0])" }
}
$tradersXml = Get-Content (Join-Path $Root "Config\traders.xml") -Raw
try { [xml]$tradersXml | Out-Null; Ok "traders.xml" } catch { Fail "traders.xml: $_" }
foreach ($m in [regex]::Matches($itemsXml, '<item name="(hs[A-Za-z0-9]+)">([\s\S]*?)</item>')) {
    $i = $m.Groups[1].Value
    if ($m.Groups[2].Value -match 'name="CustomIcon"') { continue }
    if (-not (Test-Path (Join-Path $Root "UIAtlases\ItemIconAtlas\$i.png"))) { Fail "item icon $i.png missing" } else { Ok "item icon $i" }
}
foreach ($m in [regex]::Matches($itemsXml, 'name="Create_item" value="([^"]+)"')) {
    if (-not $known.ContainsKey($m.Groups[1].Value)) { Fail "drink gives back unknown $($m.Groups[1].Value)" }
}
if ([regex]::Match($itemsXml, '<item name="hsMugTeaPJ">[\s\S]*?</item>').Value -match 'Use_jar_refund') { Fail "mug tea only sometimes gives the mug back" } else { Ok "mugs always come back" }
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
foreach ($s in @("hsMicrowaveHum", "hsMicrowaveDing", "hsFlush", "hsStream", "hsSoil", "hsSoilDry", "hsWasherHum")) {
    if ($soundsXml -notmatch "name=`"$s`"") { Fail "sounds.xml missing $s" } else { Ok "sound $s" }
}
$unlocks = [regex]::Matches($progXml, '<passive_effect name="RecipeTagUnlocked"[^>]*tags="([^"]+)">\s*<requirement name="ProgressionLevel" progression_name="perk')
$gated = @{}
foreach ($u in $unlocks) { foreach ($t in $u.Groups[1].Value.Split(',')) { $gated[$t] = $true } }
foreach ($r in ([regex]::Matches($recipeXml, '<recipe name="(hs[A-Za-z0-9]+)"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)) {
    if (-not $gated.ContainsKey($r)) { Fail "progression never unlocks $r behind a magazine and a perk" } else { Ok "unlock $r (magazine + perk)" }
}
if ($progXml -match '<passive_effect name="RecipeTagUnlocked"[^>]*/>') { Fail "an unlock has no perk requirement" } else { Ok "every unlock needs a perk" }

foreach ($cls in @("HSFridge, HSWorkingStuff", "HSStorage, HSWorkingStuff", "HSSink, HSWorkingStuff", "HSTank, HSWorkingStuff", "HSRainCatch, HSWorkingStuff", "HSHeater, HSWorkingStuff", "HSStove, HSWorkingStuff", "HSMicrowave, HSWorkingStuff", "HSRadiator, HSWorkingStuff", "HSCoffeeMaker, HSWorkingStuff", "HSWaterCooler, HSWorkingStuff", "HSShower, HSWorkingStuff", "HSAirCon, HSWorkingStuff", "HSKettle, HSWorkingStuff", "HSToaster, HSWorkingStuff", "HSTV, HSWorkingStuff", "HSPanel, HSWorkingStuff", "HSDryer, HSWorkingStuff", "HSToilet, HSWorkingStuff", "HSWallThermostat, HSWorkingStuff", "HSHifi, HSWorkingStuff", "HSHifiSpeaker, HSWorkingStuff", "HSMug, HSWorkingStuff")) {
    if ($blocksXml -notmatch [regex]::Escape($cls)) { Fail "blocks.xml missing class '$cls'" } else { Ok "class $cls" }
}

$ingameXml = Get-Content (Join-Path $Root "Config\XUi_InGame\windows.xml") -Raw
$locPath = Join-Path $Root "Config\Localization.csv"
if (Test-Path (Join-Path $Root "Config\Localization.txt")) { Fail "Config\Localization.txt is never read by the game (it loads Localization.csv)" }
if (-not (Test-Path $locPath)) { Fail "Config\Localization.csv missing" }
$locTxt = Get-Content $locPath -Raw
$locRows = Import-Csv $locPath
$badRows = @($locRows | Where-Object { -not $_.Key -or $null -eq $_.english -or @($_.PSObject.Properties).Count -ne 7 })
$extraCols = @(Get-Content $locPath | Select-Object -Skip 1 | Where-Object { ($_ -replace '"[^"]*"', '').Split(',').Count -ne 7 })
if ($badRows.Count -gt 0 -or $extraCols.Count -gt 0) { Fail "Localization.csv has $($extraCols.Count) row(s) without exactly 7 columns (quote text with commas): $($extraCols | Select-Object -First 3)" } else { Ok "Localization.csv columns" }
if ($menuXml -notmatch "btnConnectToIp") { Fail "main menu missing btnConnectToIp" } else { Ok "btnConnectToIp" }
if ($menuXml -notmatch 'caption="Connect to IP"') { Fail "Connect to IP caption is not human text" } else { Ok "Connect to IP caption" }
if ($ingameXml -notmatch 'text="Fridge"') { Fail "fridge window missing Fridge title" } else { Ok "fridge window title" }
$xuiXml = Get-Content (Join-Path $Root "Config\XUi_InGame\xui.xml") -Raw
if ($xuiXml -notmatch "workstation_hsMicrowave" -or $blocksXml -notmatch "workstation_hsMicrowave") { Fail "microwave window group missing" } else { Ok "microwave window" }
if ($xuiXml -notmatch 'workstation_hsGrill[\s\S]*?windowFuel' -or $blocksXml -notmatch "workstation_hsGrill") { Fail "grill window group missing" } else { Ok "grill window" }
$freezer = [regex]::Match($blocksXml, '<block name="hsFreezer">[\s\S]*?</block>').Value
if ($freezer -notmatch 'HSChestFreezer' -or $freezer -notmatch 'HSFridge, HSWorkingStuff' -or $freezer -notmatch 'name="ModelOffset" value="0,0,0"') { Fail "freezer is not the chest freezer model" } else { Ok "chest freezer" }
if ($xuiXml -notmatch "HSStorageWindowGroup, HSWorkingStuff") { Fail "fridge window group missing assembly controller" } else { Ok "fridge window group controller" }
if ($xuiXml -notmatch 'window_group name="hsCircuitName" controller="HSCircuitNameWindowGroup, HSWorkingStuff"' -or $ingameXml -notmatch 'windowHSCircuitName[\s\S]*?<textfield name="nameInput"') { Fail "circuit name window missing" } else { Ok "circuit name window" }
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
if ($blocksXml -notmatch "workstation_hsWasher") { Fail "washer missing cooker-style window" } else { Ok "washer cooker window" }
if ($blocksXml -notmatch 'CraftingAreaRecipes" value="hsWasher"' -or $blocksXml -notmatch 'CraftingAreaRecipes" value="hsDryer"') { Fail "washer/dryer missing craft areas" } else { Ok "washer and dryer craft areas" }
$displayXml = Get-Content (Join-Path $Root "Config\ui_display.xml") -Raw
try { [xml]$displayXml | Out-Null; Ok "ui_display.xml" } catch { Fail "ui_display.xml: $_" }
foreach ($w in @("hsWasher", "hsWasherVanilla", "hsDryer", "hsDryerVanilla")) {
    if ($displayXml -notmatch "display_type=`"$w`"") { Fail "ui_display missing $w category list" } else { Ok "ui_display $w" }
}
if ($blocksXml -notmatch 'hsStoveOld[\s\S]*RequiredPower') { Fail "stove missing RequiredPower" } else { Ok "stove RequiredPower" }

$buffsXml = Get-Content (Join-Path $Root "Config\buffs.xml") -Raw
try { [xml]$buffsXml | Out-Null; Ok "buffs.xml" } catch { Fail "buffs.xml: $_" }
foreach ($b in @("buffHSRadiatorWarm", "buffHSRadiatorWarmEffect", "buffHSAirConCool", "buffHSAirConCoolEffect", "buffHSWashed", "buffHSWashedBath", "buffHSSmelly", "buffHSDirtyClothes", "buffHSWetClothes", "buffHSWashedSink", "buffHSNeedPee", "buffHSNeedPoo")) {
    if ($buffsXml -notmatch "name=`"$b`"") { Fail "buffs.xml missing $b" } else { Ok "buff $b" }
}
foreach ($b in @("buffHSSmelly", "buffHSDirtyClothes", "buffHSWetClothes", "buffHSNeedPee", "buffHSNeedPoo")) {
    $def = [regex]::Match($buffsXml, "<buff name=`"$b`"[^>]*>[\s\S]*?</buff>").Value
    if ($def -notmatch "display_value") { Fail "buff $b missing display_value" } else { Ok "buff $b number" }
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
foreach ($b in @("hsFridgeWhite", "hsWasher", "hsWasherVanilla", "hsDryer", "hsDryerVanilla", "hsThermometer", "hsSocketUKSingle", "hsSocketUKDouble", "hsSocketUSSingle", "hsSocketUSDouble", "hsLightSwitchUK", "hsLightSwitchUS", "hsCookerSwitchUK", "hsRangeOutletUS", "hsKettle", "hsToaster", "hsToasterUS", "hsPlantedWheat1", "hsFlour", "hsDough", "hsBreadLoaf", "hsBreadSlice", "hsToast", "hsMug", "hsMugCoffee", "hsTVStand", "hsTVWall", "hsConsumerUnitUK", "hsBreakerPanelUS", "hsFuse5", "hsFuse15", "hsFuse30", "hsFuse45", "hsBreaker5", "hsBreaker15", "hsBreaker30", "hsBreaker45", "hsWashingLine", "hsClothesAirer", "hsClothesPegs", "hsToilet", "hsUrinal", "hsWallThermostat", "hsHifi", "hsHifiSpeaker", "hsHifiSpeakerWall")) {
    if ($locTxt -notmatch "(?m)^$b,") { Fail "missing loc $b" }
    if ($progXml -notmatch "\b$b\b") { Fail "$b is not unlocked anywhere" } else { Ok "$b unlocked" }
}
foreach ($k in @("hsMugTeaPJ", "hsMugTeaYorkie", "hsMugPlaced", "hsMugBagPJ", "hsMugBagYorkie", "hsWasherDesc", "hsWasherStarted", "hsWasherRunning", "hsWasherBusy", "hsDryerDesc", "hsThermometerDesc", "hsFridgeWhiteDesc", "hsSocketUKDesc", "hsSocketUSDesc", "hsLightSwitchUKDesc", "hsCookerSwitchUKDesc", "blockcommand_hswash", "blockcommand_hsdry", "hsThermoRoom", "hsClothesDirt", "hsClothesWet", "hsWashed", "hsPlugFullOne", "hsPlugUsed", "buffHSSmellyName", "buffHSSmellyValue", "buffHSDirtyClothesName", "buffHSDirtyClothesValue", "buffHSWetClothesName", "buffHSWetClothesValue", "buffHSNeedPeeValue", "buffHSNeedPooValue", "hsLightSwitchUSDesc", "blockcommand_washup", "hsLblWash", "hsSinkWashed", "hsWashLakeside", "hsWashTooSoon", "hsWashSoakStart", "hsWashSoakDone", "blockcommand_hsoutlet0", "blockcommand_hsoutlet1", "hsOutletState", "hsRangeOutletUSDesc", "hsSinkDrained", "hsKettleDesc", "hsToasterDesc", "hsToasterUS", "hsToasterUSDesc", "hsPlantedWheat2", "hsPlantedWheat3", "hsPlantedWheatDesc", "hsCropWheat", "hsCropWheatDesc", "hsTeaBagsPJ", "hsTeaBagsYorkie", "hsTeaBagsPJDesc", "hsTeaBagsYorkieDesc", "hsMugTeaPJDesc", "hsKettlePowered", "hsKettleBoiling", "hsToasterPowered", "hsToasterToasting", "hsFrozenHours", "hsFrozenMins", "hsFrozenNoEat", "hsdefrost", "blockcommand_hsdefrost", "hsDefrostNone", "hsDefrosted", "hsTVDesc", "hsTVNoSocket", "hsTVNoPower", "hsTVOff", "hsTVNoVideos", "hsTVChannel", "hsTVSocketOnly", "hstvpower", "blockcommand_hstvpower", "hstvnext", "hstvprev", "hsFuseBlown", "hsConsumerUnitUKDesc", "hsBreakerPanelUSDesc", "hsFuseDesc", "hsFuseBlownDesc", "hsBreakerDesc", "blockcommand_hscircnext", "blockcommand_hscircfit", "blockcommand_hscircout", "blockcommand_hscircswitch", "blockcommand_hscircname", "blockcommand_hspanelmain", "hscircnext", "hscircfit", "hscircout", "hscircswitch", "hscircname", "hspanelmain", "hsPanelMainOn", "hsPanelMainOff", "hsPanelNoPower", "hsPanelNoCircuits", "hsPanelWays", "hsPanelHoldFuse", "hsPanelFitted", "hsPanelFull", "hsBreakerRating", "hsFuseRating", "hsCircuitN", "hsCircuitEmpty", "hsCircuitTripped", "hsCircuitBlown", "hsCircuitOff", "hsCircuitLoad", "hsCircuitMainOff", "hsCircuitLine", "hsCircuitNoFuse", "hsCircuitTrippedShort", "hsCircuitBlownShort", "hsCircuitOffShort", "hsCircuitNameTitle", "hsCircuitNameHint", "hsCircuitNamed", "hsCircuitNameCleared", "hsSocketChained", "hsChainFull", "hsSwitchChained", "hsSwitchChainFull", "hsSwitchRun", "hsSwitchChainMax", "hsWashingLineDesc", "hsClothesAirerDesc", "hsClothesPegsDesc", "hsDrySun", "hsDryNight", "hsDryRain", "hsDryCovered", "hsDryWarm", "hsDryIndoors", "hsDryHung", "hsDryEmpty", "hsSocketRun", "hsChainMax", "hsToiletDesc", "hsUrinalDesc", "blockcommand_hspee", "blockcommand_hspoo", "blockcommand_hsflush", "blockcommand_hsfill", "hsBladderLine", "hsBowelLine", "hsBowlDirty", "hsNoFlush", "hsFlushed", "hsTreePee", "hsAccidentPee", "hsAccidentPoo", "hsUrgePee", "hsUrgePoo", "buffHSNeedPeeName", "buffHSNeedPooName", "hsFridgeFreezerPowered", "blockcommand_hsopenfreezer", "hsWallThermostatDesc", "hsHifiDesc", "hsMugBagPJDesc", "hsMugBagYorkieDesc", "blockcommand_hskettlefill", "blockcommand_hskettleboil", "blockcommand_hskettlepour", "blockcommand_hskettlejar", "blockcommand_hsmugbag", "blockcommand_hsmugpickup", "hsKettleWater", "hsKettleHot", "hsKettleCold", "hsKettleNoSocket", "hsKettleSocketOnly", "hsMugEmpty", "hsHifiSpeakerDesc", "hsHifiSpeakerWallDesc", "blockcommand_hshifipower", "blockcommand_hshifiplay", "blockcommand_hshifipause", "blockcommand_hshifinext", "blockcommand_hshifiprev", "blockcommand_hshifiup", "blockcommand_hshifidown", "blockcommand_hsspkup", "blockcommand_hsspkdown", "hsHifiNoMusic", "hsHifiPlaying", "hsHifiNoSpeakers", "hsHifiSocketOnly", "hsSpeakerHifiOnly", "hsSpeakerNoHifi", "blockcommand_hsstatup", "blockcommand_hsstatdown", "hsStatLevel", "hsStatNoRadiator", "hsStatNoPower", "hsStatSocketOnly", "hsFridgeTemp", "hsFreezerTemp")) {
    if ($locTxt -notmatch "(?m)^$k,") { Fail "missing loc $k" } else { Ok "loc $k" }
}
foreach ($c in @(@("HSSocketUKSingle", "HSSocketUKSingle_Plug0"), @("HSSocketUKDouble", "HSSocketUKDouble_Plug1"), @("HSSocketUKDouble", "HSSocketUKDouble_SwOn1"), @("HSSocketUSDouble", "HSSocketUSDouble_Plug1"), @("HSLightSwitchUS", "HSLightSwitchUS_SwOn"), @("HSLightSwitchUK", "HSLightSwitchUK_SwOff"), @("HSCookerSwitchUK", "HSCookerSwitchUK_Lamp"), @("HSRangeOutletUS", "HSRangeOutletUS_Plug0"), @("HSConsumerUnitUK", "HSConsumerUnitUK_W0Fuse"), @("HSConsumerUnitUK", "HSConsumerUnitUK_W5Blown"), @("HSConsumerUnitUK", "HSConsumerUnitUK_W5Off"), @("HSConsumerUnitUK", "HSConsumerUnitUK_MainOff"), @("HSBreakerPanelUS", "HSBreakerPanelUS_W0On"), @("HSBreakerPanelUS", "HSBreakerPanelUS_W7Blown"), @("HSBreakerPanelUS", "HSBreakerPanelUS_W7Fuse"), @("HSBreakerPanelUS", "HSBreakerPanelUS_MainOn"), @("HSWashingLine", "HSWashingLine_Cloth0"), @("HSWashingLine", "HSWashingLine_Cloth7"), @("HSClothesAirer", "HSClothesAirer_Cloth5"))) {
    $pf = Join-Path $Root "_unity\Assets\HSWorkingStuff\Prefabs\$($c[0]).prefab"
    if (-not (Test-Path $pf) -or -not (Select-String -Path $pf -Pattern "m_Name: $($c[1])" -SimpleMatch -Quiet)) { Fail "$($c[0]) is missing child $($c[1])" } else { Ok "$($c[0]) has $($c[1])" }
}
$linePf = Join-Path $Root "_unity\Assets\HSWorkingStuff\Prefabs\HSWashingLine.prefab"
if (-not (Test-Path $linePf) -or (Select-String -Path $linePf -Pattern "^BoxCollider:" ).Count -lt 3) { Fail "washing line needs its own collider per post and the line (walk-through gaps)" } else { Ok "washing line colliders" }
foreach ($b in @("hsWashingLine", "hsClothesAirer")) {
    $def = [regex]::Match($blocksXml, "<block name=`"$b`">[\s\S]*?</block>").Value
    if ($b -eq "hsWashingLine" -and ($def -notmatch "TEFeatureStorage" -or $def -notmatch "MultiBlockDim`" value=`"5,2,1`"" -or $def -notmatch "HSOutdoor`" value=`"true`"")) { Fail "$b is not a 5x2 outdoor storage line" }
    elseif ($b -eq "hsClothesAirer" -and ($def -notmatch "HSOutdoor`" value=`"false`"" -or $def -notmatch "MultiBlockDim`" value=`"2,1,1`"")) { Fail "$b is not a 2-wide indoor airer" }
    else { Ok "$b set up" }
}
if ($lootXml -notmatch "lootcontainer name=`"hsWashingLine`"" -or $lootXml -notmatch "lootcontainer name=`"hsClothesAirer`"") { Fail "drying storage loot lists missing" } else { Ok "drying storage" }
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
foreach ($k in @("HotWaterSeconds", "RadiatorWatts", "RadiatorRadius", "FreezerMultiplier", "AirConRadius", "ShowerJars", "BathJars", "GasCanGrillSeconds", "EnableRoomTemperature", "IndoorInsulation", "IndoorLagHours", "RadiatorHeat", "ThermostatHeat", "AirConCool", "ThermostatCool", "EnableBodySmell", "BodySmellStartHours", "BodySmellFullHours", "BodySmellRadius", "EnableLaundry", "ClothesDirtyHours", "DirtyClothesSmell", "WasherJars", "WasherSeconds", "DryerSeconds", "AirDryHours", "WetClothesCold", "WaterWashHours", "WattsPerAmp", "OverloadSeconds", "LineDryHours", "AirerDryHours", "MaxSocketsPerRun", "EnableNeeds", "BladderHours", "BowelHours", "DrinkBladder", "EatBowels", "UrgeAt", "FlushJars", "UrinalUsesPerJar", "CisternJars", "ToiletPeesBeforeFlush", "DirtyToiletSmell", "WaterWashClothes", "FridgeFreezerMultiplier", "FridgeFreezerFreezeHours", "FridgeTemp", "FreezerTemp", "FridgeFreezerTemp", "ThermostatRange", "HifiVolume", "HifiHearRange", "KettleBoilSeconds", "KettleHotMinutes")) {
    if ($settings -notmatch "`"$k`"") { Fail "JSON missing $k" } else { Ok "json $k" }
}

foreach ($k in @("hsLaundryWashInfo", "hsLaundryDryInfo", "hsLaundryNeedDirty", "hsLaundryNeedWet", "hsLaundryQueueFull", "hsLaundryQueued", "hsLaundryPaused", "hsLaundryDone", "hsDryingWindow", "hsDryingWet", "hsDryingDry", "hsFridgeFoodOnly", "hsToiletDryPees", "hsToiletNeedsFlush", "hsPeeNeedsFlush", "hsSalvageToilet", "hsSalvageBath", "blockcommand_hsuse", "hsWashLakesideClothes", "hsSinkWashedClothes", "hsWaterHeaterVanilla", "hsWaterHeaterVanillaDesc")) {
    if ($locTxt -notmatch "(?m)^$k,") { Fail "missing loc $k" } else { Ok "loc $k" }
}
if ($blocksXml -notmatch 'cntToaster[\s\S]{0,500}hsToasterUS') { Fail "toaster is not repairable" } else { Ok "toaster repairs" }
if ($blocksXml -notmatch 'cntMicrowaveOvenVer1Open[\s\S]{0,600}resourceElectricParts') { Fail "microwave repair has no electric parts" } else { Ok "microwave electric parts" }
if ($blocksXml -notmatch 'cntCoffeeMaker[\s\S]{0,400}resourceElectricParts') { Fail "coffee maker repair has no electric parts" } else { Ok "coffee maker electric parts" }
if ($blocksXml -notmatch 'cntBathTubEmpty[\s\S]{0,400}HSShower') { Fail "empty bath is not a working bath" } else { Ok "empty bath" }
if ($blocksXml -notmatch 'decoClawFootBathTub[\s\S]{0,400}HSShower') { Fail "clawfoot bath is not a working bath" } else { Ok "clawfoot bath" }
if ($blocksXml -notmatch 'cntCabinetBottomBrokenDoor[\s\S]{0,500}resourceWood') { Fail "broken cabinets do not repair with wood" } else { Ok "broken cabinets" }
if ($blocksXml -notmatch 'cntCabinetBottomOpen[\s\S]{0,500}ToBlock" value="cntCabinetBottom"') { Fail "open cabinets are not repairable" } else { Ok "open cabinets" }
if ($blocksXml -notmatch 'cntCupboardCabinetOldTopOpen[\s\S]{0,500}ToBlock" value="cntCupboardCabinetOldTopClosed"') { Fail "wall cupboards are not repairable" } else { Ok "wall cupboards" }
if ($blocksXml -notmatch 'cntDeskWoodEmpty[\s\S]{0,500}ToBlock" value="cntDeskWoodFull_Player"') { Fail "empty wood desks are not repairable" } else { Ok "wood desks" }
if ($blocksXml -notmatch 'cntBookcaseEmpty[\s\S]{0,500}ToBlock" value="cntBookcaseFull"') { Fail "empty bookcases are not repairable" } else { Ok "bookcases" }
if ($itemsXml -notmatch 'resourceElectricParts,resourceMechanicalParts') { Fail "repair tools still reject electric parts" } else { Ok "repair tools allow electric parts" }
if ($blocksXml -notmatch 'waterHeaterWhite[\s\S]{0,500}hsWaterHeaterVanilla') { Fail "POI water heater is not repairable" } else { Ok "POI water heater" }
if ($blocksXml -notmatch 'radiatorHouse01Player[\s\S]{0,500}hsRadiatorCastIron') { Fail "house radiator is not repairable" } else { Ok "house radiator" }
$heater = [regex]::Match($blocksXml, '<block name="hsWaterHeaterVanilla">[\s\S]*?</block>').Value
if ($heater -notmatch 'HSHeater, HSWorkingStuff' -or $heater -notmatch 'waterHeaterWhite') { Fail "vanilla heater is not a working boiler" } else { Ok "vanilla heater boiler" }
if ($locTxt -notmatch "hsAuthServerStarting") { Fail "missing hsAuthServerStarting loc" } else { Ok "join-hold loc" }
foreach ($flag in @("EnableSpoil", "EnableFridge", "EnableStove", "EnablePlumbing", "EnableConnectToIp", "HideNews", "HoldJoinUntilReady")) {
    if ($settings -notmatch [regex]::Escape("`"$flag`"")) { Fail "JSON missing $flag" } else { Ok "json $flag" }
}

$dll = Join-Path $Root "HSWorkingStuff.dll"
if (-not (Test-Path $dll)) { Fail "HSWorkingStuff.dll missing" }
else {
    $ascii = [Text.Encoding]::ASCII.GetString([IO.File]::ReadAllBytes($dll))
    foreach ($t in @("HSWorkingStuffMod", "HSWorkingStuffSettings", "BlockHSFridge", "TileEntityHSFridge", "HSSpoil", "HSWater", "HSWorkingStuffNet", "HSPatchConnectToIp", "EnableConnectToIp", "HSLoc", "HSPatchLocGet", "BlockHSStove", "HSStovePower", "HideNews", "HSPatchHideNewsSplash", "HSPatchHideNewsCount", "HSJoinHold", "HSPatchJoinHoldAuthorize", "HoldJoinUntilReady", "HSPatchJoinHoldAuthState", "HSPatchJoinHoldGameStarted", "HSPatchJoinHoldAuthStart", "HSItem", "HSInvData", "HSFridgeInv", "IHSFridgeInv", "CreateTe33", "Load33", "HSPatchFridgeRead", "HSRadiatorValve", "GaugeLine", "ColdReason", "FillFromSupply", "UnderSink", "DrainMurky", "XUiC_HSRainCatchWindowGroup", "XUiC_HSRainToolGrid", "BlockHSCoffeeMaker", "HSPatchRainQuickMove", "TryQuickAdd", "HSPatchStoveWireHold", "HSPatchStoveWindowHalt", "HSPatchSwitchText", "EnsureTe33", "BlockHSMicrowave", "HSMicrowaveSound", "BlockHSRainCatch", "FindTaps", "HSPatchRadialGrey", "HSPatchLegacyCollectorTick", "ItemActionHSPipeTool", "HSPipeHud", "HSPipeToolInteract", "HSStovePowerWindow", "HSPowerDemand", "HSPatchIdleDemand", "HSBoiler", "BlockHSRadiator", "HSPipeNet", "TakeUp", "HSModelHit", "RootTransformRefParent", "RepairWorkstation", "NewConnection", "NewSystem", "UseSystem", "Hugs", "Ports", "IntoFixtures", "FindBoiler", "Covers", "FloorCell", "getFacesDrawnFullBitfield", "RouteUnder", "RouteSurface", "UseFloorPipe", "Joins", "Relayout", "SetPreview", "BlockHSWaterCooler", "BlockHSShower", "HSWash", "HSPatchWashedSmell", "SmellUpdateItemsAndBlood", "BlockHSAirCon", "HSAirCon", "HSPatchGasGrillFuel", "GetFuelTime", "BuildCold", "HSRoomTemp", "HSPatchRoomTemp", "BlockHSThermometer", "HSLaundry", "BlockHSLaundry", "HandleLaundry", "HSPlugs", "HSPatchSocketPlugs", "HSPatchSocketPlugShow", "HSOriginalMaterials", "HSOutlets", "HSPatchOutletPower", "HSPatchWaterWash", "HSPatchWaterWashPrompt", "NaturalWash", "QuickWashHours", "BlockHSWallSwitch", "BlockHSWallRelay", "HSPatchSinkDrain", "BlockHSKettle", "BlockHSToaster", "HSPatchFrozenEatHeld", "HSPatchFrozenEatBag", "HSPatchFrozenCraft", "FridgeThawHours", "BlockHSTV", "HSTVScreen", "TVViewRange", "BlockHSPanel", "HSCircuits", "HSPanelView", "XUiC_HSCircuitNameWindowGroup", "HSPatchSocketPass", "HSPatchCutTrigger", "HSPatchCutToggle", "HSPatchCutTimer", "WattsPerAmp", "OverloadSeconds", "OfText", "BlockHSDryer", "HSDrying", "HSDryerView", "LineDryHours", "AirerDryHours", "MaxSocketsPerRun", "RunPosition", "BlockHSToilet", "HSNeeds", "FromDrink", "BladderHours", "HSPatchEatNeeds", "TreePee", "HSParentSync", "HSPatchParentSyncSet", "CompartmentColdRate", "CompartmentRate", "BlockHSWallThermostat", "HSThermostat", "BlockHSHifi", "BlockHSHifiSpeaker", "HSHifiPlayer", "BlockHSMug", "HSKettle", "BlocksHeat", "OwnerOf", "WasherHum", "HoldingWire", "HSLaundryCraft", "HasWashWater", "TakeWashWater", "XUiC_HSDryingWindowGroup", "WetSlots", "DryerSeconds", "PipedRadiator", "ShowInUi", "ResetForNewGame", "BindQueue", "AddRecipeToCraft", "CanFridge", "KickJunk", "HSSalvage", "RinseWorn", "ToiletPeesBeforeFlush", "WaterWashClothes", "MarkPlaced", "HSGod", "VanillaHeaterName", "HSFlipper", "FillLoot", "InjectFurniture", "AfterUpgrade", "MarkPlayerEmpty", "PreferEmpty")) {
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
