using System;
using System.Collections.Generic;
using HarmonyLib;

public static class HSLoc
{
    static readonly Dictionary<string, string> english = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "hsConnectToIp", "Connect to IP" },
        { "hsFridge", "Working Fridge" },
        { "hsFridgeWindow", "Fridge" },
        { "hsFridgeDesc", "A powered fridge. Keep it wired and food lasts four times longer. Unpowered it is just a box. E to open, store perishables, or pull spoiled food." },
        { "hsCooler", "Working Cooler" },
        { "hsCoolerDesc", "An ice chest. Food inside lasts twice as long. No power needed." },
        { "hsDryingRack", "Drying Rack" },
        { "hsDryingRackDesc", "Hang raw meat here for about a day to make dried meat that keeps longer." },
        { "hsCompost", "Compost Box" },
        { "hsCompostDesc", "Spoiled food left in here turns into nitrate powder after about a day." },
        { "hsPipeStraight", "Water Pipe" },
        { "hsPipeCorner", "Water Pipe Corner" },
        { "hsPipeJoint", "Water Pipe Joint" },
        { "hsPipeDesc", "Joins rain catch to tank, tank to sink, tank to boiler, boiler to sink and radiators. Vanilla metal pipes work too. Better: carry these and let the Pipe Tool lay them as hidden pipe you can build over." },
        { "hsPipeTool", "Pipe Tool" },
        { "hsPipeToolDesc", "Lays water pipe for you. Hold E: New Water System or Use Water System Here on a rain catch, tank, boiler, sink, radiator, intake or laid pipe, then New Connection on each thing to pipe to it, then Lay pipes. Left click is Use Water System Here, right click is New Connection. Pipe runs along floors and walls, radiators are fed under the floor, and it lays the run if you carry enough water pipes. Laid pipe is not a block: you can build walls, floors and furniture right over it, and you only see it where nothing covers it. E > Take up pipes gives a run back." },
        { "hsWaterIntake", "Water Intake" },
        { "hsWaterIntakeDesc", "Place against a lake, river, or other water block. Pipe from here to a sink for unlimited murky water." },
        { "hsWaterTank", "Water Tank" },
        { "hsWaterTankDesc", "Holds 40 jars of water. It does not collect on its own: pipe a rain catch into it, or pour jars and buckets in. Pipe it on to a sink for the cold tap, and through a combi boiler for the hot tap." },
        { "hsRainCatch", "Rain Catch" },
        { "hsRainCatchDesc", "Collects dew and rain under open sky, no jars needed. Bare it gives murky water. Install a dew filter for clean water; a dew gatherer and tarp speed it up. Pipe it to a water tank and it fills the tank by itself." },
        { "hsSink", "Working Sink" },
        { "hsSinkDesc", "Two taps. Cold comes straight from a piped tank or lake intake, as clean or murky as that water is. Hot comes through a powered combi boiler and is always boiled. A tap with no supply shows greyed out with what it needs. POI kitchen sinks work the same way if you pipe them." },
        { "hsWaterHeater", "Combi Boiler" },
        { "hsWaterHeaterDesc", "Wall-hung combi boiler. Place it against a wall. Pipe a tank into it and on to a sink for a hot tap; it only fires (and draws power) while hot water runs. Pipe radiators to it and switch central heating on to warm the house: 15W plus about 5W per radiator while heating (more with its valve turned up)." },
        { "hsRadiator", "Panel Radiator" },
        { "hsRadiatorCastIron", "Cast Iron Radiator" },
        { "hsRadiatorDesc", "Place against a wall and pipe it to a combi boiler (or on from another radiator). With central heating on and the boiler powered it keeps you warm within about 5 blocks, like standing by a campfire. Hold E to turn its valve up or down (off, 1 to 5): higher reaches further and draws more power. The gauge shows how hot it is." },
        { "buffHSRadiatorWarmName", "Radiator Warmth" },
        { "buffHSRadiatorWarmDesc", "A warm radiator nearby is keeping the cold out." },
        { "hsBoilerNoPower", "No power: no hot water or heating" },
        { "hsBoilerFiring", "Firing: heating water" },
        { "hsBoilerIdle", "Idle: heats only when a hot tap runs (0W)" },
        { "hsBoilerHeatingOff", "Central heating: off" },
        { "hsBoilerNoRadiators", "Central heating: on, no radiators piped in" },
        { "hsBoilerHeatingOn", "Central heating: on, {0} radiators" },
        { "hsBoilerToldOn", "Central heating on" },
        { "hsBoilerToldOff", "Central heating off" },
        { "hsRadiatorWarm", "Warm: keeps you warm nearby" },
        { "hsRadiatorCold", "Cold: pipe to a combi boiler with heating on" },
        { "hsRadiatorNoPipe", "Cold: not piped to a combi boiler" },
        { "hsRadiatorHeatingOff", "Cold: piped to the boiler, but its central heating is off. Turn it on at the boiler" },
        { "hsRadiatorNoPower", "Cold: the boiler has no power" },
        { "hsRadiatorWarming", "Cold: the boiler is on, warming up" },
        { "hsRadiatorOff", "Off at the valve: hold E and turn it up" },
        { "hsRadiatorDry", "Cold: no water in the heating. Needs {0} more jars in the tank piped to the boiler (1 per radiator)" },
        { "hsBoilerNeedsWater", "Heating water: {0} of {1} radiators filled. Needs {2} more jars in the tank piped to the boiler" },
        { "hsBoilerFilled", "Heating water: all {0} radiators filled" },
        { "hsRadiatorGauge", "Temp: {0} {1}" },
        { "hsRadiatorValve", "Valve: {0} of {1}" },
        { "hsRadiatorValveOff", "Valve: off" },
        { "hsRadiatorValveMax", "The valve is fully open" },
        { "hsRadiatorValveMin", "The valve is already off" },
        { "blockcommand_valveup", "Turn up" },
        { "blockcommand_valvedown", "Turn down" },
        { "blockcommand_heatingon", "Central heating on" },
        { "blockcommand_heatingoff", "Central heating off" },
        { "hsStoveOld", "Electric Cooker" },
        { "hsStoveModern", "Electric Cooker" },
        { "hsStoveWall", "Electric Wall Oven" },
        { "hsStoveDesc", "An electric cooker. Same recipes and tools as a campfire (pot, grill, beaker). No wood. Wire it to power or it will not cook." },
        { "hsStovePowered", "Powered: cooking like a campfire" },
        { "hsStoveUnpowered", "No power: wire this cooker to cook" },
        { "hsSpoiledFood", "Spoiled Food" },
        { "hsSpoiledFoodDesc", "This food sat too long. Compost it for nitrate, or eat it if you like a gamble." },
        { "hsDriedMeat", "Dried Meat" },
        { "hsDriedMeatDesc", "Dried on a rack. Keeps about three weeks." },
        { "hsFridgePowered", "Powered: food lasts 4x longer" },
        { "hsFridgeUnpowered", "Unpowered: spoils at normal speed" },
        { "hsFridgeEmptyFood", "No perishable food stored" },
        { "hsFridgeOldest", "Oldest food: {0} days left" },
        { "hsHeaterOn", "Heating: water from this line is boiled" },
        { "hsHeaterOff", "No power: water stays as it is" },
        { "hsWaterClean", "clean" },
        { "hsWaterMurky", "murky" },
        { "hsWaterEmpty", "empty" },
        { "hsTankPrompt", "{0}/{1} {2}" },
        { "hsTankModsBare", "No mods: collecting murky water" },
        { "hsTankFilterOn", "Filter on: collecting clean water" },
        { "hsTankModsOn", "Dew mods installed" },
        { "hsSinkDry", "No water. Pipe this to a tank or an intake in a lake." },
        { "hsSinkLake", "Lake feed, {0}" },
        { "hsSinkTank", "Tank {0} {1}" },
        { "hsSpoilWarn", "{0} will spoil soon. Get it in a fridge." },
        { "hsSpoilSoon", "Spoiling now" },
        { "hsSpoilHours", "Spoils in {0}h" },
        { "hsSpoilDays", "Spoils in {0} days" },
        { "storefood", "Store perishables" },
        { "takespoiled", "Take spoiled" },
        { "filljar", "Fill jar" },
        { "fillall", "Fill all" },
        { "pour", "Pour" },
        { "pourall", "Pour all" },
        { "drink", "Drink" },
        { "blockcommand_storefood", "Store perishables" },
        { "blockcommand_takespoiled", "Take spoiled" },
        { "blockcommand_filljar", "Fill jars in hand" },
        { "blockcommand_fillbucket", "Fill bucket" },
        { "blockcommand_fillall", "Fill all on you" },
        { "blockcommand_pour", "Pour" },
        { "blockcommand_pourall", "Pour all" },
        { "blockcommand_drink", "Drink" },
        { "hsAuthServerStarting", "Server is Starting up, Please Wait ..." },
        { "hsMicrowave", "Microwave" },
        { "hsMicrowaveRetro", "Retro Microwave" },
        { "hsMicrowaveDesc", "A cheap powered microwave. Boils water, makes coffee and tea, bakes potatoes, corn on the cob, vegetable stew, sham chowder, spaghetti and gumbo. No pot or grill needed, but you still have to know the recipe. Wire it to power, 5W. Close the door to cook, look at it for the time left. Dings when it is done." },
        { "hsMicrowavePowered", "Powered: ready to cook" },
        { "hsMicrowaveUnpowered", "No power: wire this microwave to cook" },
        { "hsMicrowaveCooking", "Cooking {0}: {1} left" },
        { "hsMicrowaveDoorOpen", "Door open: paused, {0} left" },
        { "hsMicrowaveNoPowerPaused", "No power: paused, {0} left" },
        { "hsMicrowaveDone", "Done: your food is ready" },
        { "hsCoffeeMaker", "Coffee Maker" },
        { "hsCoffeeMakerCommercial", "Commercial Coffee Maker" },
        { "hsCoffeeMakerDesc", "A powered coffee maker. Put in a jar of water (murky is fine, it heats it) and coffee beans and it brews a jar of coffee. Gunpowder makes black strap coffee. You still have to know the recipe. Wire it to power, 5W. It only draws power while it brews or while you have it open." },
        { "hsCoffeePowered", "Powered: add a jar of water and coffee beans" },
        { "hsCoffeeUnpowered", "No power: wire this coffee maker to brew" },
        { "hsCoffeeBrewing", "Brewing {0}: {1} left" },
        { "hsCoffeeDone", "Done: your coffee is ready" },
        { "hsSwitchOn", "{0} Switch On" },
        { "hsSwitchOff", "{0} Switch Off" },
        { "hsSwitchTurnOn", "{0} Turn {1} On" },
        { "hsSwitchTurnOff", "{0} Turn {1} Off" },
        { "hsSwitchDevices", "{0} Devices" },
        { "blockcommand_filljarhot", "Fill jars in hand (hot)" },
        { "blockcommand_fillbuckethot", "Fill bucket (hot)" },
        { "blockcommand_fillallhot", "Fill all on you (hot)" },
        { "blockcommand_drinkhot", "Drink (hot)" },
        { "blockcommand_colddry", "Cold: no tank piped in" },
        { "blockcommand_hotnoheater", "Hot: needs a combi boiler" },
        { "blockcommand_hotnopower", "Hot: boiler has no power" },
        { "blockcommand_hotdry", "Hot: boiler has no water behind it" },
        { "blockcommand_coldempty", "Cold: the tank is empty" },
        { "blockcommand_hotempty", "Hot: the tank is empty" },
        { "blockcommand_addtools", "Install dew tools" },
        { "blockcommand_notools", "No dew gatherer, tarp or filter on you" },
        { "blockcommand_removetools", "Remove dew tools" },
        { "hsStovePowerTitle", "POWER" },
        { "hsStovePanelOn", "Powered" },
        { "hsStovePanelOff", "Unpowered" },
        { "hsRainNoTools", "No dew tools: murky water" },
        { "hsRainTools", "Tools: {0} (murky water)" },
        { "hsRainToolsClean", "Tools: {0} (clean water)" },
        { "hsRainFeeds", "Piped to a tank" },
        { "hsRainNoFeed", "Not piped to a tank" },
        { "hsRainCovered", "Covered: needs open sky" },
        { "hsRainDrained", "Filter in: drained {0} jars of murky water so the clean water stays clean" },
        { "hsSinkCold", "Cold: {0}" },
        { "hsSinkColdNone", "Cold: no tank or intake piped in" },
        { "hsSinkHot", "Hot: {0}" },
        { "hsSinkHotNoHeater", "Hot: needs a combi boiler piped in" },
        { "hsSinkHotNoPower", "Hot: the boiler has no power" },
        { "hsSinkHotDry", "Hot: the boiler has no tank or intake behind it" },
        { "hsSinkColdEmpty", "Cold: the tank is empty. Pipe a rain catch to it or pour water in" },
        { "hsSinkHotEmpty", "Hot: the tank behind the boiler is empty" },
        { "hsPipeToolHint", "Pipe Tool: E for menu, left click use water system here, right click new connection" },
        { "hsPipeRadialNewSystem", "New Water System" },
        { "hsPipeRadialUseSystem", "Use Water System Here" },
        { "hsPipeRadialLay", "Lay pipes" },
        { "hsPipeRadialNew", "New Connection" },
        { "hsPipeExisting", "Laid pipe" },
        { "hsPipeNoFloor", "Radiator pipe runs under the floor: the radiator must stand on a full block (or a half block in the top of its cell), and the other end needs a wall to run down to the floor" },
        { "hsPipeUnderFloor", "{0} of them hidden in floors or walls" },
        { "hsPipeAimNewSystem", "Aim at a rain catch, tank, boiler, sink, radiator or intake to start a water system there" },
        { "hsPipeAimSystem", "Aim at a rain catch, tank, boiler, sink, radiator, intake or laid pipe to use that water system" },
        { "hsPipeAimPlumbing", "Aim at a rain catch, tank, boiler, sink, radiator, intake or laid pipe to connect it" },
        { "hsPipeSystemNew", "New water system at {0}. Look at the next thing and pick New Connection" },
        { "hsPipeSystemExisting", "{0} is already in a water system ({1} things), using that one. Look at the next thing and pick New Connection" },
        { "hsPipeSystemUse", "Using the water system at {0} ({1} things). New connections come from here" },
        { "hsPipeNeedSystem", "Pick New Water System or Use Water System Here first" },
        { "hsPipeNeedConnection", "Look at the thing to connect and pick New Connection first" },
        { "hsPipeConnectTo", "New connection: {0} from {1}. Pick Lay pipes" },
        { "hsPipeAlreadyJoined", "{0} is already piped to this water system" },
        { "hsPipeNotEnough", "Need {0} water pipes, you have {1}" },
        { "hsPipeClaimed", "Part of the route is on someone else's land claim" },
        { "hsPipeLaid", "Laid {0} pipes: {1} to {2}. Look at the next thing and pick New Connection" },
        { "hsPipeSameBlock", "That is the water system itself" },
        { "hsPipeEndGone", "The water system or the thing you are connecting is gone" },
        { "hsPipeTouching", "Those two already touch, no pipe needed" },
        { "hsPipeNoRoute", "No route along walls or floors: blocked, or longer than 64 pipes" },
        { "hsPipeNotSet", "not set" },
        { "hsPipeRouteNoSystem", "Route: pick New Water System or Use Water System Here" },
        { "hsPipeRoutePending", "Route: look at a thing and pick New Connection" },
        { "hsPipeRoute", "Route: {0} pipes, {1} bends. {2}" },
        { "hsPipeEnough", "You have enough." },
        { "hsPipeShort", "Short by {0}." },
        { "hsPipeHudTitle", "PIPE TOOL" },
        { "hsPipeHudSystem", "Water system:" },
        { "hsPipeHudParts", "({0} things)" },
        { "hsPipeHudConnect", "Connecting:" },
        { "hsPipeHudHave", "Water pipes on you: {0}" },
        { "hsPipeHudHelp", "Left click: use water system here. Right click: new connection. E for menu" },
        { "hsPipeRadialTakeUp", "Take up pipes" },
        { "hsPipeNoneHere", "No laid pipe here: look at it, or at the block it joins" },
        { "hsPipeRemoved", "Took up {0} pipes" },
        { "hsFreezer", "Chest Freezer" },
        { "hsFreezerDesc", "A powered chest freezer. Keep it wired (10W) and food in it does not spoil at all. Unpowered it is just a box. E to open, store perishables, or pull spoiled food." },
        { "hsFreezerWindow", "Freezer" },
        { "hsFreezerPowered", "Powered: frozen food does not spoil" },
        { "hsWaterCooler", "Plumbed Water Cooler" },
        { "hsWaterCoolerDesc", "A water cooler plumbed into your water system. Pipe it to a tank like a sink. It has its own filter, so even murky tank water comes out clean: drink from it or fill jars." },
        { "hsCoolerFiltered", "Built-in filter: comes out clean" },
        { "hsShower", "Shower" },
        { "hsShowerDesc", "Pipe it to a powered combi boiler for hot water. A shower uses 2 jars of hot water and washes off all your smell for 10 minutes: carried meat, food and sickness will not draw zombies by smell." },
        { "hsBath", "Bath" },
        { "hsBathDesc", "Pipe it to a powered combi boiler for hot water. A bath uses 4 jars of hot water and washes off all your smell for 20 minutes: carried meat, food and sickness will not draw zombies by smell." },
        { "blockcommand_wash", "Wash" },
        { "hsWashUses", "A wash uses {0} jars of hot water" },
        { "hsWashShort", "Not enough hot water: a wash needs {0} jars" },
        { "hsWashShower", "Showered: nearly all of your body smell is gone, and nothing smells on you for 10 minutes" },
        { "hsWashBath", "Bathed: all of your body smell is gone, and nothing smells on you for 20 minutes" },
        { "buffHSWashedName", "Freshly Showered" },
        { "buffHSWashedBathName", "Freshly Bathed" },
        { "buffHSWashedDesc", "You are clean. Nothing you carry or ate gives off a smell zombies can follow." },
        { "hsAirCon", "Wall Air Conditioner" },
        { "hsAirConDesc", "A wall-mounted home air conditioner. Place it on a wall at head height or above. Wire it to power (20W) and it keeps you cool within about 6 blocks, the summer version of a radiator." },
        { "hsAirConCeiling", "Ceiling Air Conditioner" },
        { "hsAirConCeilingDesc", "A commercial ceiling cassette air conditioner. Place it against a ceiling. Wire it to power (30W) and it keeps you cool within about 6 blocks." },
        { "hsAirConOn", "Powered: cooling within {0} blocks" },
        { "hsAirConOff", "No power: wire it up to cool the room" },
        { "buffHSAirConCoolName", "Air Conditioned" },
        { "buffHSAirConCoolDesc", "A running air conditioner nearby is keeping the heat off." },
        { "hsSocketUKSingle", "UK Socket (Single, Switched)" },
        { "hsSocketUKDouble", "UK Socket (Double, Switched)" },
        { "hsSocketUSSingle", "US Outlet (Single)" },
        { "hsSocketUSDouble", "US Outlet (Duplex)" },
        { "hsLightSwitchUK", "UK Light Switch" },
        { "hsCookerSwitchUK", "UK Cooker Switch (45A)" },
        { "hsSocketUKDesc", "A UK 13A wall socket with its own switch. Wire power into it, then wire from it to the appliance: one plug for a single socket, two for a double. Switch it off and everything plugged in goes off." },
        { "hsSocketUSDesc", "A US wall outlet, always live with no switch. Wire power into it, then wire from it to the appliance: one plug for a single outlet, two for a duplex." },
        { "hsLightSwitchUKDesc", "A UK rocker light switch. Wire power into it and from it to your lights; it switches them all on and off." },
        { "hsLightSwitchUS", "US Light Switch" },
        { "hsLightSwitchUSDesc", "A US toggle light switch: lever up for on, down for off. Wire power into it and from it to your lights." },
        { "washup", "Wash up" },
        { "blockcommand_washup", "Wash up (1 jar, about 5 m less smell)" },
        { "hsLblWash", "Wash" },
        { "hsSinkWashed", "Washed up at the sink: about 5 m less smell" },
        { "hsWashLakeside", "Splashed yourself clean: about 5 m less smell" },
        { "hsWashTooSoon", "You have just had a quick wash. Another one won't help for an hour; a shower or bath will" },
        { "hsWashSoakStart", "In the water: your smell is slowly washing off" },
        { "hsWashSoaking", "Washing in the water: {0}% of your body smell off so far. Stay in to keep washing" },
        { "hsWashSoakIdle", "The water has done what it can for now. A shower or bath gets you properly clean" },
        { "hsWashSoakLeft", "Out of the water: {0}% of your body smell washed off" },
        { "hsWashSoakDone", "Washed in the water: {0}% of your body smell gone" },
        { "hsoutlet0", "Left socket" },
        { "hsoutlet1", "Right socket" },
        { "blockcommand_hsoutlet0", "Switch the left socket" },
        { "blockcommand_hsoutlet1", "Switch the right socket" },
        { "hsOutletState", "Left socket: {0}    Right socket: {1}" },
        { "hsOutletOn", "On" },
        { "hsOutletOff", "Off" },
        { "hsRangeOutletUS", "US Range Outlet (240V)" },
        { "hsRangeOutletUSDesc", "The US way to power a cooker: a big 240V range outlet, always live with no switch. Wire power into it and from it to one cooker." },
        { "hsCookerSwitchUKDesc", "A UK 45A cooker switch with a red rocker and neon. Wire power into it and from it to one cooker." },
        { "hsPlugFullOne", "This socket takes one plug and it is already in use" },
        { "hsPlugFullMany", "This socket takes {0} plugs and both are in use" },
        { "hsPlugUsed", "Plugs: {0} of {1} in use" },
        { "hsPlugPowered", "Live" },
        { "hsPlugNoPower", "No power: wire it from a generator, battery bank or solar bank" },
        { "hsFridgeWhite", "Fridge-Freezer (White)" },
        { "hsFridgeWhiteDesc", "A tall white UK fridge-freezer, two blocks high. Works like the fridge: keep it wired (5W) and food lasts four times longer. E to open, store perishables, or pull spoiled food." },
        { "hsWasher", "Washing Machine" },
        { "hsWasherVanilla", "Washing Machine (Old)" },
        { "hsWasherDesc", "Put dirty clothes and armour in, then Wash. Needs power (15W) and a cold water pipe from a tank, like a sink; a wash uses 4 jars. Clothes come out clean but wet." },
        { "hsDryer", "Tumble Dryer" },
        { "hsDryerVanilla", "Tumble Dryer (Old)" },
        { "hsDryerDesc", "Put wet clothes and armour in, then Dry. Needs power (20W), no water." },
        { "hsThermometer", "Wall Thermometer" },
        { "hsThermometerDesc", "Hang it on a wall and look at it for the room temperature, the temperature outside, and whether the heating or air con is working on the room." },
        { "blockcommand_hswash", "Wash" },
        { "blockcommand_hsdry", "Dry" },
        { "hsThermoRoom", "Room: {0}" },
        { "hsThermoOutside", "Outside: {0}" },
        { "hsThermoHeating", "Radiator heating the room" },
        { "hsThermoCooling", "Air con cooling the room" },
        { "hsThermoCold", "Cold: put the heating on" },
        { "hsThermoHot", "Hot: an air conditioner would help" },
        { "hsThermoComfy", "Comfortable" },
        { "hsLaundryPowered", "Powered" },
        { "hsLaundryNoPower", "No power: wire it up first" },
        { "hsWasherHolds", "{0} clothes or armour inside" },
        { "hsDryerHolds", "{0} wet items inside" },
        { "hsWasherEmpty", "Nothing to wash: put clothes or armour in first" },
        { "hsDryerEmpty", "Nothing wet to dry" },
        { "hsWasherShort", "Not enough water: a wash needs {0} jars" },
        { "hsWashed", "Washed {0} items. They are wet: dry them in a dryer, or they dry on you in {1} hours" },
        { "hsDried", "Dried {0} items" },
        { "hsClothesClean", "Clean" },
        { "hsClothesWorn", "Worn" },
        { "hsClothesDirty", "Dirty" },
        { "hsClothesFilthy", "Filthy" },
        { "hsClothesDirt", "{0}: {1}% dirty" },
        { "hsClothesSmell", "Adds to your smell: wash it in a washing machine" },
        { "hsClothesWet", "Wet: cold to wear, dries in {0} h or use a dryer" },
        { "buffHSSmellyName", "Unwashed" },
        { "buffHSSmellyDesc", "You have not washed for a day or more. Zombies can smell you further away the longer it goes on. Have a shower or a bath." },
        { "buffHSDirtyClothesName", "Dirty Clothes" },
        { "buffHSDirtyClothesDesc", "Some of what you are wearing is dirty and adds to your smell. Wash it in a washing machine." },
        { "buffHSWetClothesName", "Wet Clothes" },
        { "buffHSWetClothesDesc", "Some of what you are wearing is still wet from the wash and makes you colder. Use a tumble dryer, or it dries on you." },
        { "hsGasGrill", "Gas Grill" },
        { "hsGasGrillDesc", "A barbecue. Grills corn, meat, steak and potato, wings, jerky and fish tacos without a separate grill tool. Burns wood or coal like a campfire, and a gas can lasts a full minute." },
        { "hsCharcoalGrill", "Charcoal Grill" },
        { "hsCharcoalGrillDesc", "A barbecue. Grills corn, meat, steak and potato, wings, jerky and fish tacos without a separate grill tool. Burns wood or coal like a campfire." }
    };

    public static string Get(string key, string fallback)
    {
        string mapped;
        if (TryEnglish(key, out mapped) && !string.IsNullOrEmpty(mapped))
            fallback = mapped;
        if (string.IsNullOrEmpty(key))
            return fallback ?? "";
        try
        {
            if (Localization.Exists(key))
            {
                string s = Localization.Get(key);
                if (!Missing(key, s))
                    return s;
            }
        }
        catch { }
        return fallback ?? key;
    }

    public static string Format(string key, string fallback, params object[] args)
    {
        string s = Get(key, fallback);
        if (args == null || args.Length == 0)
            return s;
        try
        {
            return string.Format(s, args);
        }
        catch
        {
            return s;
        }
    }

    public static bool TryEnglish(string key, out string englishText)
    {
        if (!string.IsNullOrEmpty(key) && english.TryGetValue(key, out englishText))
            return true;
        englishText = null;
        return false;
    }

    public static bool Missing(string key, string value)
    {
        if (string.IsNullOrEmpty(value))
            return true;
        if (string.Equals(value, key, StringComparison.OrdinalIgnoreCase))
            return true;
        if (value.StartsWith("UL_", StringComparison.OrdinalIgnoreCase) && value.Length > 3
            && string.Equals(value.Substring(3), key, StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }
}

[HarmonyPatch(typeof(Localization), "Get", new Type[] { typeof(string), typeof(bool), typeof(string) })]
public static class HSPatchLocGet
{
    static void Postfix(string _key, ref string __result)
    {
        if (_key == "ttCantHaveParent")
        {
            string why = HSPlugs.DeniedText();
            if (why != null)
            {
                __result = why;
                return;
            }
        }
        if (!HSLoc.Missing(_key, __result))
            return;
        string english;
        if (HSLoc.TryEnglish(_key, out english))
            __result = english;
    }
}
