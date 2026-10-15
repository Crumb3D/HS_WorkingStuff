# HS Working Stuff

Food spoils in about 7 days. Craft a powered fridge, chest freezer, cooler, stoves, microwave, coffee maker, grills, pipes, sink, water cooler, shower, bath, tank, rain catch, combi boiler, radiators, air conditioners, washing machine, tumble dryer, wall thermometer, and UK/US sockets and switches. Rooms follow the weather, you get smelly, and clothes get dirty. Everything has to be learned: a magazine level and a perk rank. Works on 7 Days To Die **3.2 and 3.3**. Load it on the dedicated server and on every client.

## JSON switches

Copy `HSWorkingStuffSettings.json` next to the DLL, or override it from `%AppData%\Roaming\7DaysToDie\HSWorkingStuff\HSWorkingStuffSettings.json`.

| Key | Default | What it does |
| --- | --- | --- |
| `EnableSpoil` | true | Food ages, morning warning, spoil tooltip |
| `EnableFridge` | true | Fridge 0.25x and cooler 0.5x spoil rate |
| `EnableStove` | true | Electric cookers: campfire recipes, no wood, needs power |
| `EnablePlumbing` | true | Pipes, tanks, sinks, drink/fill/pour |
| `EnableConnectToIp` | true | Main menu **Connect to IP** button |
| `HideNews` | true | Hide the News menu button, corner news, and news splash |
| `HoldJoinUntilReady` | true | Hold connecting players until the dedicated server finishes starting, instead of kicking them |
| `SpoilDays` | 7 | Days until food spoils at 1x |
| `FridgeMultiplier` | 0.25 | Powered fridge spoil rate |
| `CoolerMultiplier` | 0.5 | Cooler spoil rate |
| `TankCapacity` | 40 | Jar-equivalents per tank |
| `RainCatchCapacity` | 10 | Jar-equivalents a rain catch holds when nothing is piped to it |
| `BucketUnits` | 4 | How many jar units a bucket is |
| `DewPerHour` | 0.125 | Dew a rain catch collects per game hour under open sky |
| `RainPerHour` | 0.25 | Extra water while it rains |
| `HotWaterSeconds` | 30 | How long the boiler keeps firing (and drawing power) after a hot tap runs |
| `RadiatorWatts` | 5 | Extra watts per radiator at valve 3 while central heating runs (scales with the valve) |
| `RadiatorRadius` | 5 | Blocks from a warm radiator at valve 3 that keep you warm (scales with the valve) |
| `FreezerMultiplier` | 0 | Powered chest freezer spoil rate (0 = never spoils) |
| `FridgeFreezerMultiplier` | 0.1 | Spoil rate in a powered fridge-freezer's freezer compartment until the food freezes (frozen food does not spoil) |
| `FridgeFreezerFreezeHours` | 4 | Game hours in the fridge-freezer's freezer compartment until food is frozen |
| `FridgeTemp` | 39 | Temperature a powered fridge shows: 39°F / 4°C. The number in the file is Fahrenheit |
| `FreezerTemp` | -4 | Temperature a powered chest freezer shows: -4°F / -20°C. The number in the file is Fahrenheit |
| `FridgeFreezerTemp` | 0 | Temperature the fridge-freezer's freezer shows: 0°F / -18°C. The number in the file is Fahrenheit |
| `AirConRadius` | 6 | Blocks from a running air conditioner that keep you cool |
| `ShowerJars` | 2 | Hot water jars a shower uses |
| `BathJars` | 4 | Hot water jars a bath uses |
| `GasCanGrillSeconds` | 60 | Seconds one gas can burns in a gas grill |
| `EnableRoomTemperature` | true | Indoor temperature follows the weather slowly; radiators and air con heat or cool the room |
| `IndoorInsulation` | 15 | How much a house takes off the weather: 15°F / 8°C (vanilla shelter is 30°F / 17°C). The number in the file is Fahrenheit |
| `IndoorLagHours` | 3 | Game hours for a house to catch up with the weather outside |
| `RadiatorHeat` | 40 | How much a warm radiator can add to a room: 40°F / 22°C. The number in the file is Fahrenheit |
| `ThermostatHeat` | 68 | Radiators heat a room up to 68°F / 20°C. The number in the file is Fahrenheit |
| `AirConCool` | 30 | How much a running air conditioner can take off a room: 30°F / 17°C. The number in the file is Fahrenheit |
| `ThermostatCool` | 72 | Air con cools a room down to 72°F / 22°C. The number in the file is Fahrenheit |
| `EnableBodySmell` | true | You get smelly when you don't wash |
| `BodySmellStartHours` | 24 | Game hours after a wash before you start to smell |
| `BodySmellFullHours` | 72 | Game hours after a wash until you smell your worst |
| `BodySmellRadius` | 50 | How far zombies can smell you at your worst (m, before shelter) |
| `EnableLaundry` | true | Worn clothes and armour get dirty, add smell, and need washing |
| `ClothesDirtyHours` | 72 | Game hours of wear until a piece is filthy |
| `DirtyClothesSmell` | 6 | Smell (m) each filthy piece adds |
| `WasherJars` | 4 | Cold water jars a wash uses |
| `AirDryHours` | 6 | Game hours for washed clothes to dry on their own |
| `WaterWashHours` | 0.5 | Game hours standing in a lake or river takes to wash 50-70% of your body smell off |
| `WetClothesCold` | 6 | How much colder you feel per wet piece you wear: 6°F / 3°C. The number in the file is Fahrenheit |
| `EnableFreezing` | true | Food freezes solid in a powered chest freezer |
| `FreezeHours` | 2 | Game hours in a powered chest freezer until food is frozen |
| `ThawHours` | 2 | Game hours frozen food takes to thaw at room temperature |
| `FridgeThawHours` | 8 | Game hours frozen food takes to thaw in a powered fridge |
| `TVVolume` | 0.8 | TV sound volume (0 to 1) |
| `TVHearRange` | 15 | How far away you can hear a TV (m) |
| `TVViewRange` | 40 | TVs further away than this (m) stop playing |
| `HifiVolume` | 0.8 | Hi-fi loudness at full volume (0 to 1) |
| `HifiHearRange` | 20 | How far away you can hear a hi-fi speaker (m) |
| `KettleBoilSeconds` | 30 | Seconds a full kettle (3 cups) takes to boil; fewer cups boil quicker |
| `KettleHotMinutes` | 10 | Minutes boiled water stays hot in the kettle before it needs boiling again |
| `WattsPerAmp` | 5 | How many watts each amp of a fuse or breaker carries (a 15A fuse = 75W) |
| `OverloadSeconds` | 2 | Seconds a circuit can be overloaded before its fuse blows or its breaker trips |
| `MaxSocketsPerRun` | 10 | Most sockets one run (one circuit) can chain together; 0 = no limit |
| `ThermostatRange` | 8 | Blocks a wall thermostat looks for the radiator whose heating loop it runs |
| `LineDryHours` | 2 | Game hours wet clothes take to dry on a washing line in daylight (twice that at night) |
| `AirerDryHours` | 3 | Game hours wet clothes take on a clothes airer or a covered line (half that by a warm radiator) |
| `EnableNeeds` | true | Bladder and bowels: drinking, eating, toilets, urinals and trees |
| `BladderHours` | 3 | Game hours for an empty bladder to fill on its own |
| `BowelHours` | 8 | Game hours for empty bowels to fill on their own |
| `DrinkBladder` | 25 | Bladder points one drink adds (100 is an accident) |
| `EatBowels` | 20 | Bowel points one meal adds (100 is an accident) |
| `UrgeAt` | 60 | Points before you are told you need to go |
| `FlushJars` | 1 | Jars one toilet flush takes |
| `UrinalUsesPerJar` | 2 | Pees at a urinal that share one jar |
| `CisternJars` | 4 | Jars you can pour into a toilet cistern when it is not piped |
| `DirtyToiletSmell` | 8 | Smell (m) an unflushed toilet or urinal adds while you stand by it |

Set a bool to `false` to turn that feature off without removing the mod.

## Connect to IP

With `EnableConnectToIp` true, the main menu gets **Connect to IP** under Play Game. It opens Direct Connect in one click instead of Play Game → Connect to Server → Direct Connect.

## Blocks

- **Working Fridge** — 24 slots, needs power for the 4x keep time. Store perishables / take spoiled.
- **Working Cooler** — 12 slots, 2x keep time, no power.
- **Drying rack** — raw meat becomes dried meat in about a day.
- **Compost box** — spoiled food becomes nitrate in about a day.
- **Plumbing** — rain catch → tank → sink, with a combi boiler on the way for hot water. Vanilla metal pipes and kitchen sinks count. An intake in a lake can feed a sink too.
- **Rain catch** — the only thing that collects water. It needs open sky, no jars. Bare it collects murky water. Install the dew collector tools from its menu: `toolDewFilter` makes the water clean, `toolDewGatherer` and `toolDewTarp` each double the rate. Pipe it to a tank and it fills the tank; otherwise it holds a little itself.
- **Tank** — storage only. It fills from a piped rain catch, or pour jars and buckets into it. Water keeps the quality it came in with.
- **Sink** — the menu shows what the pipes give it. Cold comes straight from a tank (or lake intake). Hot comes through a powered combi boiler and is always boiled. A tap you can't use is greyed out and says why: no water, needs a boiler, boiler has no power. What the tap offers depends on your hand: empty jars in hand **fill those jars**, an empty bucket **fills the bucket**, anything else (or nothing) **drinks**. **Fill all on you** fills every empty jar and bucket in your bag and toolbelt. Filling swings your arm with the vanilla water-fill sound. Drinking and washing use the same empty-hand drink anim as a lake (lean in and `player_drinking`). Empty a full bucket of water at a sink, shower or bath and it goes down the drain instead of onto the floor.
- **Pipe tool** — craft it from 2 forged iron and 1 metal pipe. It works like the lift and escalator tools: hold E for the menu.
  - **New Water System** — aim at a rain catch, tank, boiler, sink, radiator or intake to start a water system there.
  - **Use Water System Here** — aim at anything already in a system (or a laid pipe). New connections come from there: use it on the boiler for hot runs and radiators, on the tank for cold.
  - **New Connection** — aim at the thing to connect. The route shows as green pipe when you have enough, red when you don't, and the HUD says how many it needs.
  - **Lay pipes** — lays it, using your pipes. The system stays picked, so aim at the next thing and pick New Connection again. Pipe as many things into one system as you like.
  - **Take up pipes** — see below.
  - Left click is Use Water System Here, right click is New Connection.
- **Branching.** A new connection tees into the system's existing pipe wherever that is shortest, so one tank can feed several sinks, or one boiler several radiators.
- **Pipe never crosses open air.** It runs tight against a floor, wall or ceiling, or hidden inside full blocks (walls, floors, terrain) where cutting through saves a long way round. It never goes through doors or other part blocks. Half blocks and plates count when they face the right way: one filling the top of its cell is a floor (radiators can stand on it and their pipe runs through it), one standing upright with its flat side toward the pipe is a wall.
- **A filter drains murky water.** Putting a dew filter in a rain catch pours away the murky water in it and in the tanks it fills, so the clean water it collects is not mixed down to murky. While the filter is in, any murky water that turns up later in those tanks (poured in by hand, buckets) is poured away when clean water arrives. Shift-click a dew tool in your bag to put it straight into the rain catch window.
- **Sinks are fed from below.** A sink on a cabinet or floor block gets its pipe up through the cabinet (or floor) underneath, like real plumbing, not from the side.
- **Radiators are fed under the floor.** Pipe a boiler to a radiator and the pipe drops from the boiler to the floor, runs hidden under the floor, and comes up into the radiator. The radiator must stand on a block. A second radiator branches off the pipe already under the floor.
- **Laid pipe is not a block.** You see it as pipe wherever the cell is open, but you can build walls, floors, furniture or anything else straight over it, and the water still flows. A cell with a full block or terrain in it hides its bit of pipe. Pipe only joins things it was laid into: a run passing under a sink does not feed it. **E > Take up pipes** while looking at a laid pipe (or at the block it joins) removes everything joined to it and gives the pipes back. Pipe blocks you placed by hand still work as before.
- **Combi boiler** — hangs on the wall: place it against a wall and its back sits flat on it. Pipe a tank in and a sink out for the hot tap. It draws no power while idle and only fires (15W) for 30 seconds after a hot tap runs. Pipe radiators to it and pick **Central heating on**: it then draws 15W plus about 5W per radiator (more or less with each radiator's valve). Look at it to see if it is idle, firing, and how many radiators it heats.
- **Radiators (panel / cast iron)** — place against a wall and pipe to a combi boiler, or on from another radiator. While central heating is on and the boiler has power, standing within about 5 blocks keeps you warm like a campfire.
  - **Heating water.** The heating is a sealed loop of water. Each radiator holds 1 jar: while central heating is on, the boiler fills dry radiators from a tank on its pipes (or a lake intake), nearest radiator first. Once filled the loop keeps its water even if the tank runs dry. Only filled radiators heat; adding a radiator needs another jar. The boiler and any cold radiator say how many jars are still needed.
  - **Valve and gauge.** Hold E on a radiator for **Turn up** / **Turn down**: off, or 1 to 5 (new radiators start at 3). Higher reaches further (3 to 7 blocks) and draws more power; off takes it out of the heating. Looking at it shows its own temperature on a gauge, climbing to about 40°C / 104°F at valve 1 up to 80°C / 176°F at valve 5 while heating, and cooling back to the room temperature when off. It follows your °C or °F game option.
- **Electric cooker (old / modern / wall)** — same recipes and tools as a campfire (cooking pot, grill, beaker). No wood. Wire it to a generator or it will not cook. Draws 10W only while food is queued or its window is open. The cooker window shows **Powered** or **Unpowered** where the campfire fuel slot was.
- **Microwave (modern / retro)** — cheap, 5W only while cooking with the door shut, a short microwave-safe menu with no pot or grill: boiled water, coffee, goldenrod tea, red tea, honey tea, baked potato, corn on the cob, vegetable stew, sham chowder, spaghetti, gumbo stew. Each one needs the same unlock as at the campfire. Queue the food and close the door to cook; opening the door pauses it. Look at it to see what's cooking and the time left. It hums while cooking and dings when the queue is done.
- **Coffee maker (home / commercial)** — put in a jar of water and coffee beans and it brews a jar of coffee; add 5 gunpowder for black strap coffee. Murky water is fine, the machine heats it, so no boiling first and no pot. Same unlocks as campfire coffee. 5W, only while brewing or while its window is open. It gurgles while it brews and dings when it's done. Look at it for what's brewing and the time left.
- **Chest freezer** — a 2-block-wide chest freezer. Opens like the fridge with or without power; wired (10W), food in it does not spoil at all.
  - **Frozen food.** After 2 hours in a powered freezer, food freezes solid (the tooltip says so). Frozen food can't be eaten, and recipes won't use it. Out of the freezer it thaws in 2 hours, or 8 in a powered fridge, and it doesn't spoil while frozen. For a quick thaw, hold E on a powered microwave and pick **Defrost**: everything frozen on you thaws at once.
- **Plumbed water cooler** — the vanilla water cooler, piped to a tank like a sink. Cold tap only, and it has its own filter, so even murky tank water comes out clean.
- **Toilet / urinal** — the game's own toilet and wall urinal models, made working. Pipe them like a sink. Hold E to **Pee**, and on a toilet **Poo**. A flush takes 1 jar from the tank (any water, you don't drink it) and you hear the flush. No pipe: hold a jar or bucket of water and **Fill cistern**, then **Flush**. Two pees at a urinal share one jar. A poo with no water leaves the bowl dirty: it smells, the toilet says **dirty** when you look at it, and you can't go again until you flush. A urinal will not take a poo.
- **Shower / bath** — pipe to a powered combi boiler. **Wash** uses 2 jars (shower) or 4 jars (bath) of hot water. A shower takes off 90-100% of your body smell and a bath all of it, and for 10 or 20 minutes carried meat, smelly food and sickness won't let zombies track you by smell.
- **Air conditioners** — a wall split unit (20W) and a ceiling cassette (30W, place it on a ceiling). Wired, standing within about 6 blocks keeps the heat off, the summer version of a radiator. It hums while it runs.
- **Fridge-freezer (white)** — a tall white UK fridge-freezer, two blocks high, to match the boiler and radiators. It has two compartments of 24 slots each. Tap E to open the fridge (food lasts 4x longer). Hold E and pick **Open freezer** for the freezer: food there freezes solid in about 4 hours, twice as long as a chest freezer, and barely spoils. **Store food** puts food in the fridge. Look at any fridge or freezer to see its temperature: a fridge runs at 39°F / 4°C, the fridge-freezer's freezer at 0°F / -18°C and a chest freezer at -4°F / -20°C. Unpowered, they sit at room temperature. The game shows the unit you picked in the options.
- **Washing machine / tumble dryer** — white UK front-loaders, plus the **US** versions which are the vanilla washer and dryer. Put worn clothes and armour in and pick **Wash**: it needs power (15W) and a cold water pipe from a tank like a sink, and uses 4 jars. Clothes come out clean but **wet**, which makes you colder while you wear them, until they dry on you (6 hours) or go through the tumble dryer (power only, 20W, **Dry**).
- **Washing line / clothes airer** — dry wet clothes and armour with no power. Open them like a box and hang the washing in. The line is two posts, a line, pegs and a clothes prop, 5 blocks wide: under open sky it dries clothes in about 2 hours in daylight and twice that at night, but rain soaks them again. Under a roof it dries like the airer. The indoor airer dries in about 3 hours in any weather, twice as fast next to a warm radiator. The washing you hang up shows on them. Pegs are made by hand, 10 at a time; a line needs 10.
- **Wall thermostat** — runs all the radiators on a heating loop from one place. Wire it from a socket; it shares the socket and doesn't use up a plug. Put it within 8 blocks of a radiator (`ThermostatRange`): it takes over that radiator's loop, every radiator piped to it, and the boiler. Hold E for **Turn all radiators up** / **Turn all radiators down** (sets every valve on the loop to the same 0 to 5) and **Central heating on / off**. Turn one radiator's own valve to override that radiator, until the thermostat is turned again. Look at it for the room temperature, its setting, how many radiators it runs, and whether the heating is on.
- **Wall thermometer** — look at it for the room temperature, the temperature outside, and whether a radiator or air con is working on the room. Shows °F or °C to match your game options.
- **Sockets and switches** — UK switched sockets (single and double), US outlets (single and duplex, always live, no switch), UK and US light switches, a UK 45A cooker switch and a US 240V range outlet (the US has no cooker switch: a range plugs straight into its own always-live outlet). Wire power into one, then wire from it to the appliance. A socket only takes as many plugs as it has outlets: one wire out of a single, two out of a double, one out of the cooker switch or range outlet; the wire tool says so when it's full. A plug shows in the outlet for each appliance, with a black lead to it. Each outlet of a UK double socket has its own switch (the first appliance wired is the left plug); a UK single socket turns everything plugged into it off and on. A light switch turns only its own lights or air con off and on, not the next switch on the run. The rockers flick up and down with the switch. Hang sockets in the cell above a floor or worktop and switches in the cell one up from the floor.
- **Socket and switch chains.** Besides its plugs, every socket can have one wire on to another socket, which doesn't use up a plug and has no lead. Light switches work the same way: chain a row of switches and they all stay live, and each one only switches the lights or air con wired from it. Power always goes through to the next switch, whether this one is on or off. Plates go in sequence: each one links on to only one more, so a new socket or switch goes on the end of the run, and a run takes at most 10 (`MaxSocketsPerRun`). Switching a UK socket off only kills its own plugs. Hover a socket or switch to see if it links on and where it is on its run.
- **Consumer unit (UK) / breaker panel (US)** — the fuse box. Wire it from a generator, battery bank or solar bank, then wire one circuit out of it per way (6 in the UK unit, 8 in the US panel): a socket, a chain of sockets, a light switch or a light. **A circuit is dead until it has a fuse or breaker.** Look at the panel to see every circuit, its fuse and its load. Hold E for:
  - **Next circuit** — picks which circuit the other options work on (marked `>`).
  - **Fit fuse / breaker** — hold a fuse or breaker and fit it in the picked circuit. Whatever was in there comes back to you.
  - **Take out** — gives back the fuse or breaker (a blown fuse comes out as a **Blown Fuse**).
  - **Reset / switch breaker** — resets a tripped breaker, or switches a breaker's circuit off and on.
  - **Name circuit** — type a name like `Kitchen sockets` and press Enter. The panel lists it by name, and every socket, switch and TV on that circuit says `Circuit: Kitchen sockets` when you look at it.
  - **Main switch** — kills every circuit at once.
- **Fuses and breakers** — 5A, 15A, 30A and 45A (5W per amp: 25W, 75W, 150W, 225W). The icon colour shows the rating: white 5A, blue 15A, red 30A, green 45A. A circuit that asks for more than its rating for 2 seconds **blows its fuse** (bang, and the fuse is useless: take it out and fit a new one) or **trips its breaker** (reset it at the panel once you've unplugged something). The load is everything switched on along the circuit, so an idle cooker or a switched-off TV costs nothing until it runs. Fuses are cheap and made by hand; breakers cost more, are made at a workbench and unlock later. Both turn up in electrical loot.
- **Toaster** — plug it into a socket (5W only while it toasts). Put in bread slices; it buzzes while it toasts and **pops** when it's done. The UK toaster is our model; the **US toaster** is the vanilla toaster, same wiring and toast.
- **Kettle** - a worktop appliance: **plug it into a socket** with the wire tool (you see the plug and lead, same as a microwave). It draws 10W only while boiling. Hold a jar of water (murky is fine) and hold E on the kettle to **fill** it: one jar is one cup, up to **3 cups**, and you get the empty jar back. **Switch on to boil**: it bubbles while it boils (30 seconds for a full kettle, `KettleBoilSeconds`), only while the socket is live, then clicks off. For a **cuppa**: right-click with a mug to put it down on the worktop, hold a tea bag (**PJ Tops** or **Yorkie Brew**) and hold E on the mug to **put the bag in**, **pick up the mug**, then hold it and hold E on the boiled kettle to **pour**: that gives the mug of tea and uses one cup. Hold an empty jar to pour a **jar of boiled water** instead. The water stays hot for 10 minutes (`KettleHotMinutes`), then it has to be boiled again. Look at the kettle to see how many cups are in it and whether it is cold, boiling or hot.
- **TV (stand / wall)** — a 43 inch flat screen that plays your own videos. Wire it from a socket (the wire tool won't take anything else); it draws 15W only while on. Hold E for **On / Off**, **Next channel**, **Previous channel**. Every video file in `%AppData%\7DaysToDie\HSWorkingStuff\TV` (made the first time you look at a TV) or the mod's `TV` folder is a channel, in file name order, named after the file. `.mp4` (H.264 with AAC sound) works best. New files appear within a few seconds. Every TV on a channel shows the same moment. Each player's PC plays the video, so in multiplayer everyone needs the same files; the server needs none. TVs further than `TVViewRange` away stop playing to save your frame rate.
- **Hi-fi and speakers** - a micro hi-fi with separate speakers. Wire the hi-fi from a socket (it uses a plug), then wire up to **5 speakers** out of the hi-fi with the wire tool. A speaker stands on a unit, shelf or the floor; the **wall speaker** goes against a wall on a bracket at head height. The music only comes out of the speakers: with none wired the hi-fi still plays (the display says so) but you hear nothing. Hold E on the hi-fi for **Power**, **Play / Pause**, **Next track**, **Previous track**, and **Volume up / down**, which sets every speaker to the same level. Hold E on a speaker to turn just that one up or down; the hi-fi volume sets them all again. When a track ends the next one starts, round to the first. Put `.mp3`, `.ogg` or `.wav` files in `%AppData%\7DaysToDie\HSWorkingStuff\Music` (made the first time you look at a hi-fi) or the mod's `Music` folder; they play in file name order, up to 64. Each player's PC plays the music, so in multiplayer everyone needs the same files; the server needs none. A black speaker cable is fitted to the walls: it goes up from the hi-fi, along the ceiling, and down to each speaker.
- **Wheat, bread and tea.** Plant wheat seed like corn: it grows short and green, then tall, then golden and ready to harvest. 5 wheat makes a seed, 3 wheat grinds into flour, 2 flour and a jar of water make dough, and dough bakes into a loaf in the campfire cooking pot. Cut a loaf into 8 slices for the toaster. Mugs are fired from clay on the campfire. Tea bags come from kitchen loot, coffee machines and the traders. The coffee maker also brews coffee in a mug. Drinking from a mug always gives the mug back.
- **Gas grill / charcoal grill** — barbecue workstations with the vanilla grill menu (corn, grilled meat, steak and potato, wings, jerky, fish tacos) and no grill tool needed. They burn wood or coal like a campfire; a gas can lasts a minute in the gas grill. Same food unlocks as the campfire grill.

## Living in the house

- **Room temperature.** Indoors, the temperature follows the weather outside slowly (about 3 game hours behind) and a house only takes 15°F / 8°C off it, like a UK house: cold in winter, hot in summer, cooler after rain. In the snow or on a cold night you need the radiators on. A warm radiator heats the room up to 68°F / 20°C. A running air conditioner cools it to 72°F / 22°C. The thermometer, the radiator gauge and the temperature you feel all follow the °C or °F option in the game's video settings. The numbers in the settings file stay Fahrenheit, because that is the unit the game stores.
- **Smell.** After a day without a wash you start to smell (**Unwashed**), and it gets worse over three days. Zombies can follow it. **Wash up** at a sink (1 jar) or crouch at a lake or river and press the drink key to wash: same drink anim as the tap, each takes about 5 m off, once an hour. Standing in water up to your waist washes 50-70% off slowly. A shower takes 90-100% and a bath all of it. Indoors it is cut down like other smells; rain or water hides it.
- **Dirty clothes.** Clothes and armour you wear get dirty over about three days of wear (hover an item to see how dirty). Dirty pieces add to your smell. Wash them in the washing machine.
- **Bladder and bowels.** An empty bladder fills in about 3 game hours, faster when you drink (a jar is about a quarter). Bowels fill in about 8 hours, faster when you eat. At about 60% you get **Need a pee** or **Need the toilet**. Pee at a toilet, a urinal or a tree (look at the trunk and hold E). Poo only in a toilet. Leave it until you're full and you have an accident: a pee soaks your clothes, a poo makes them filthy, and both make you smell. Wash the clothes and have a shower.

## Unlocks

Every recipe in the mod needs **both** a magazine level and a perk rank. The crafting list shows which magazine and perk unlock each one.

| Magazine | Level | Perk | Unlocks |
| --- | --- | --- | --- |
| Electrician | 25 | Advanced Engineering 1 | Microwaves, coffee makers, kettle, UK and US toaster, TVs, hi-fi and speakers, electric cookers, sockets, UK and US light switches, cooker switch, range outlet, consumer unit, breaker panel, fuses |
| Electrician | 45 | Advanced Engineering 2 | Fridge, white fridge-freezer, chest freezer, combi boiler, wall and ceiling air conditioners, breakers |
| Workstations | 16 | Advanced Engineering 1 | Pipe tool, pipes, intake, tank, sink, rain catch, water cooler, wall thermometer, clothes pegs, washing line, clothes airer |
| Workstations | 26 | Advanced Engineering 2 | Radiators, wall thermostat, shower, bath, washing machines, tumble dryers, toilet, urinal |
| Home Cooking | 6 | Master Chef 1 | Drying rack, dried meat, charcoal grill, flour, dough, bread, mugs |
| Home Cooking | 10 | Master Chef 1 | Cooler, toast, mugs of coffee (tea is made by hand at the kettle) |
| Home Cooking | 26 | Master Chef 2 | Gas grill |
| Seeds | 10 | Living Off The Land 1 | Compost box |
| Seeds | 18 | Living Off The Land 1 | Wheat seed |

Food cooked in the microwave, coffee maker and grills uses the vanilla food unlocks.

## Power use

Appliances only draw from the generator while they work. The fridge draws 5W while it is switched on. Cookers and coffee makers draw while cooking, the microwave while cooking with its door shut, and the boiler while a hot tap has just run or central heating is on. An idle appliance still shows as powered when its circuit is live.
- Microwave food uses the vanilla food unlocks (Master Chef / cooking magazines). If you can't cook it on a campfire yet, you can't microwave it either.

Pickup keeps fridge contents and tank water.

Install on host and clients. After updating the dedicated copy, restart the **dedicated server and the client**.

See `CHANGELOG.md` for what changed in each version.
