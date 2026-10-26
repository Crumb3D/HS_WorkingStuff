# HS Working Stuff

**Player guide:** `PLAYER_HANDBOOK.md` in this folder, or open `Handbook.html` to print. Make a house a home: repair a POI and live in it; craft white goods and a consumer unit later.

Food spoils in about 3 days on you, 7 in a powered fridge, 30 in a fridge-freezer and 90 in a chest freezer. Craft a powered fridge, chest freezer, cooler, stoves, microwave, coffee maker, grills, pipes, sink, water cooler, shower, bath, tank, rain catch, combi boiler, radiators, air conditioners, washing machine, tumble dryer, wall thermometer, and UK/US sockets and switches. Rooms follow the weather, you get smelly, and clothes get dirty. Everything has to be learned: a magazine level and a perk rank. Works on 7 Days To Die **3.2 and 3.3**. Load it on the dedicated server and on every client.

## JSON switches

Copy `HSWorkingStuffSettings.json` next to the DLL, or override it from `%AppData%\Roaming\7DaysToDie\HSWorkingStuff\HSWorkingStuffSettings.json`.

| Key | Default | What it does |
| --- | --- | --- |
| `EnableSpoil` | true | Food ages, morning warning, spoil tooltip |
| `EnableFridge` | true | Fridge about 7 days (rate 0.428571) and cooler 0.5x spoil rate |
| `EnableStove` | true | Electric cookers: campfire recipes, no wood, needs power |
| `EnablePlumbing` | true | Pipes, tanks, sinks, drink/fill/pour |
| `EnableConnectToIp` | true | Main menu **Connect to IP** button |
| `HideNews` | true | Hide the News menu button, corner news, and news splash |
| `HoldJoinUntilReady` | true | Hold connecting players until the dedicated server finishes starting, instead of kicking them |
| `SpoilDays` | 3 | Days until food spoils on you |
| `FridgeMultiplier` | 0.428571 | Powered fridge spoil rate (about 7 days when SpoilDays is 3) |
| `CoolerMultiplier` | 0.5 | Cooler spoil rate |
| `TankCapacity` | 40 | Jar-equivalents per tank |
| `RainCatchCapacity` | 10 | Jar-equivalents a rain catch holds when nothing is piped to it |
| `BucketUnits` | 4 | How many jar units a bucket is |
| `DewPerHour` | 0.125 | Dew a rain catch collects per game hour under open sky |
| `RainPerHour` | 0.25 | Extra water while it rains |
| `HotWaterSeconds` | 30 | How long the boiler keeps firing (and drawing power) after a hot tap runs |
| `RadiatorWatts` | 5 | Extra watts per radiator at valve 3 while central heating runs (scales with the valve) |
| `RadiatorRadius` | 5 | Blocks from a warm radiator at valve 3 that keep you warm (scales with the valve) |
| `FreezerMultiplier` | 0.033333 | Powered chest freezer spoil rate (about 90 days when SpoilDays is 3). Food still freezes |
| `FridgeFreezerMultiplier` | 0.1 | Spoil rate in a powered fridge-freezer's freezer compartment (about 30 days when SpoilDays is 3). Food still freezes |
| `FridgeFreezerFreezeHours` | 4 | Game hours in the fridge-freezer's freezer compartment until food is frozen |
| `FridgeTemp` | 39 | Temperature a powered fridge shows: 39°F / 4°C. The number in the file is Fahrenheit |
| `FreezerTemp` | -4 | Temperature a powered chest freezer shows: -4°F / -20°C. The number in the file is Fahrenheit |
| `FridgeFreezerTemp` | 0 | Temperature the fridge-freezer's freezer shows: 0°F / -18°C. The number in the file is Fahrenheit |
| `AirConRadius` | 6 | Blocks from a running air conditioner that keep you cool |
| `ShowerJars` | 2 | Hot water jars a shower uses |
| `BathJars` | 4 | Hot water jars a bath uses |
| `GasCanGrillSeconds` | 60 | Seconds one gas can burns in a gas grill |
| `EnableRoomTemperature` | true | Indoor temperature follows the biome the room stands in. The walls hold it. Radiators and air con heat or cool that stored heat |
| `IndoorInsulation` | 15 | How many °F a normal wall takes off the weather. Brick and concrete keep more of the biome. Timber takes more off. The number in the file is Fahrenheit |
| `IndoorLagHours` | 3 | Game hours for a normal wall to catch the weather. Multiplied by the material below |
| `WoodHeatHold` | 0.55 | Timber. It lets the biome go sooner, so one radiator or one air con is usually enough |
| `StoneHeatHold` | 2.5 | Brick, concrete, cobble and stone. They hold desert heat and snow cold. A cold stone room wants more than one radiator |
| `SteelHeatHold` | 1.8 | Solid steel. It holds the biome, and it lets that go sooner than brick. Corrugated sheet follows the weather |
| `RadiatorHeat` | 40 | How much one warm radiator (valve 3) can add: 40°F / 22°C. A second radiator adds the same again, up to the thermostat. The number in the file is Fahrenheit |
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
| `WasherJars` | 1 | Cold water jars one garment uses in the washer |
| `WasherSeconds` | 30 | Seconds a filthy chest piece takes to wash (hats and gloves less; dirt scales it) |
| `DryerSeconds` | 20 | Seconds a soaking chest piece takes in the tumble dryer |
| `AirDryHours` | 6 | Game hours for washed clothes to dry on their own |
| `WaterWashHours` | 0.5 | Game hours standing in a lake or river takes to wash 50-70% of your body smell off |
| `WetClothesCold` | 6 | How much colder you feel per wet piece you wear: 6°F / 3°C. The number in the file is Fahrenheit |
| `EnableFreezing` | true | Food freezes solid in a powered chest freezer |
| `FreezeHours` | 2 | Game hours in a powered chest freezer until food is frozen |
| `ThawHours` | 24 | Game hours frozen food takes to thaw in your pack or on a chopping board |
| `FridgeThawHours` | 12 | Game hours frozen food takes to thaw in a powered fridge |
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
| `BladderHours` | 24 | Game hours for an empty bladder to fill on its own (drinking still adds `DrinkBladder`) |
| `BowelHours` | 72 | Game hours for empty bowels to fill on their own (eating still adds `EatBowels`) |
| `DrinkBladder` | 25 | Bladder points one drink adds (100 is an accident) |
| `EatBowels` | 20 | Bowel points one meal adds (100 is an accident) |
| `UrgeAt` | 60 | Points before you are told you need to go |
| `FlushJars` | 1 | Jars one toilet flush takes |
| `UrinalUsesPerJar` | 2 | Pees at a urinal that share one jar |
| `CisternJars` | 4 | Jars you can pour into a toilet cistern when it is not piped |
| `ToiletPeesBeforeFlush` | 3 | Wees a dry toilet takes before it needs a jar to flush |
| `DirtyToiletSmell` | 8 | Smell (m) an unflushed toilet or urinal adds while you stand by it |
| `WaterWashClothes` | 0.2 | Dirt washed off each worn piece at a lake or sink (0.2 = 20%) |
| `EnableHouseNoise` | true | Running washer, dryer, cooker, TV, hi-fi and other kit rings as a dinner bell. Fridge stays quiet. Pair with HS Better Ears so idle zombies walk to it |
| `IndoorSmell` | 0.25 | Unused. Smell is no longer cut by a flat indoor amount. HS Better Ears muffles each wall, window and shut door on the way to a zombie |
| `OpenDoorSmell` | 0.55 | Unused. An open door is a hole in that same per-surface check |

Set a bool to `false` to turn that feature off without removing the mod.

## Connect to IP

With `EnableConnectToIp` true, the main menu gets **Connect to IP** under Play Game. It opens Direct Connect in one click instead of Play Game → Connect to Server → Direct Connect.

## Blocks

- **Working Fridge** — 24 slots, needs power for the 4x keep time. Only food that spoils goes in (blocks and tools bounce back). Store perishables / take spoiled.
- **Working Cooler** — 12 slots, 2x keep time, no power.
- **Drying rack** — raw meat becomes dried meat in about a day.
- **Compost box** — spoiled food becomes nitrate in about a day.
- **Plumbing** — rain catch → tank → sink, with a combi boiler on the way for hot water. The tank only feeds the water stored in it; it does not join the pipes on one side to the pipes on the other. Vanilla metal pipes and kitchen sinks count. A lake / river intake can fill a rain catch or a tank, and can still feed a sink direct.
- **Rain catch** — collects dew and rain under open sky, no jars. Bare it collects murky water. Install the dew collector tools from its menu: `toolDewFilter` makes the water clean, `toolDewGatherer` and `toolDewTarp` each double dew and rain. Pipe a lake intake into it and it fills from the lake too — **clean only with a dew filter**, otherwise murky. Pipe it to a tank and it fills the tank; otherwise it holds a little itself.
- **Tank** — storage only. It fills from a piped rain catch, from a lake intake (**murky**, fine for toilets), or pour jars and buckets into it. Water keeps the quality it came in with.
- **Water pump** — craft it, place next to a lake / river / stream, wire it to a generator or battery bank (10W, same as other electrical blocks). While powered it pumps into a piped rain catch (cleaned only with a dew filter) or tank (murky). Aim the Pipe Tool at the water with a pump on you to place it on the bank. The old Water Intake flange still works for a sink without power.
- **Water intake** — simple flange on the bank for unlimited murky water at a sink. Prefer the pump for filling tanks.
- **Sink** — the menu shows what the pipes give it. Cold comes straight from a tank (or lake intake). Hot comes through a powered combi boiler and is always boiled. A tap you can't use is greyed out and says why: no water, needs a boiler, boiler has no power. What the tap offers depends on your hand: empty jars in hand **fill those jars**, an empty bucket **fills the bucket**, anything else (or nothing) **drinks**. **Fill all on you** fills every empty jar and bucket in your bag and toolbelt. Filling swings your arm with the vanilla water-fill sound. Drinking uses the empty-hand drink anim and `player_drinking`. Washing uses that same anim with a water splash, not the drinking sound. Empty a full bucket of water at a sink, shower or bath and it goes down the drain instead of onto the floor.
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
- **Combi boiler** — hangs on the wall: place it against a wall and its back sits flat on it. Pipe a tank in and a sink out for the hot tap. It draws no power while idle and only fires (15W) for 30 seconds after a hot tap runs. Pipe radiators to it and pick **Central heating on**: it then draws 15W plus about 5W per radiator (more or less with each radiator's valve). Two boilers on the same tank each only heat radiators piped to that boiler, not through the tank. A radiator in the house that you never piped does not count. Look at it to see if it is idle, firing, and how many radiators it heats.
- **Radiators (panel / cast iron)** — place against a wall and pipe to a combi boiler, or on from another radiator. While central heating is on and the boiler has power, standing within about 5 blocks keeps you warm like a campfire.
  - **Heating water.** The heating is a sealed loop of water. Each radiator holds 1 jar: while central heating is on, the boiler fills dry radiators from a tank on its pipes (or a lake intake), nearest radiator first. Once filled the loop keeps its water even if the tank runs dry. Only filled radiators heat; adding a radiator needs another jar. The boiler and any cold radiator say how many jars are still needed.
  - **Valve and gauge.** Hold E on a radiator for **Turn up** / **Turn down**: off, or 1 to 5 (new radiators start at 3). Higher reaches further (3 to 7 blocks) and draws more power; off takes it out of the heating. Looking at it shows its own temperature on a gauge, climbing to about 40°C / 104°F at valve 1 up to 80°C / 176°F at valve 5 while heating, and cooling back to the room temperature when off. It follows your °C or °F game option.
- **Electric cooker (old / modern / wall)** — same recipes and tools as a campfire (cooking pot, grill, beaker). No wood. Wire it to a generator or it will not cook. Draws 10W only while food is queued or its window is open. The cooker window shows **Powered** or **Unpowered** where the campfire fuel slot was.
- **Microwave (modern / retro)** — cheap, 5W only while cooking with the door shut, a short microwave-safe menu with no pot or grill: boiled water, coffee, goldenrod tea, red tea, honey tea, baked potato, corn on the cob, vegetable stew, sham chowder, spaghetti, gumbo stew. Each one needs the same unlock as at the campfire. Queue the food and close the door to cook; opening the door pauses it. Look at it to see what's cooking and the time left. Stand next to it while it cooks and you hear a quiet close-range hum; it dings when the queue is done.
- **Coffee maker (home / commercial)** — put in a jar of water and coffee beans and it brews a jar of coffee; add 5 gunpowder for black strap coffee. Murky water is fine, the machine heats it, so no boiling first and no pot. Same unlocks as campfire coffee. 5W, only while brewing or while its window is open. It gurgles while it brews and dings when it's done. Look at it for what's brewing and the time left.
- **Chest freezer** — a 2-block-wide chest freezer. Opens like the fridge with or without power; wired (10W), food lasts about 90 days and freezes solid. The look-at line and the tooltip show those days. Defrost at a microwave, cooker, campfire or grill. Same as the fridge: only food that spoils goes in.
  - **Frozen food.** After 2 hours in a powered freezer, food freezes solid and the name becomes Frozen. Frozen food can't be eaten, and recipes won't use it. It does not stack with the same food when that food is not frozen. In your pack, or on a chopping board, it defrosts in 24 hours. In a powered fridge it defrosts in 12 hours. The 3-day or 7-day spoil clock starts once it is soft. In a chest freezer it stays frozen and spoils over about 90 days. For a quick thaw, hold E on a powered microwave and pick **Defrost**: everything frozen on you thaws at once.
- **Chopping board** — its own wooden board, with a juice groove and a handle. Sits on a counter. Hold E and put frozen food on it. It defrosts over 24 hours. A tool breaks it. Unlocks with the drying rack.
- **Drying rack** — its own wooden frame, with hanging bars and hooks. Hold E and hang raw meat. It dries in about a day. A tool breaks it.
- **Noisy** — a running machine shows an icon with the smell icons. The number is how many metres zombies can hear it. Walls cut it.
- **Plumbed water cooler** — the vanilla water cooler, piped to a tank like a sink. Cold tap only, and it has its own filter, so even murky tank water comes out clean.
- **Toilet / urinal** — loot a POI toilet and it becomes a working one (urinals in POIs already work). Hold E to **Pee**, and on a toilet **Poo**. A dry toilet takes about 3 pees before it needs a flush; hold a jar of water and **Flush** (or fill the cistern). A poo always needs that jar, or a pipe. Piped, a flush takes 1 jar from the tank. Two pees at a urinal share one jar. A dirty bowl smells and will not take another poo until you flush. A urinal will not take a poo. Inside a land claim you can pick the toilet up.
- **Looted appliances** — empty a POI fridge, cooker, wall oven, microwave, coffee maker, toaster, washer or dryer, then repair it with a nailgun / hammer (right click) and 2 electric parts. It becomes the working version. Inside a land claim you can pick it up. POI TVs (wall and stand, including the big 2x2) repair the same way and keep the vanilla screen; wire them from a socket. Open or broken kitchen cabinets and wall cupboards repair with wood into closed **empty** storage (not an unsearched loot ping). Open medicine cabinets take scrap iron. Nightstands, empty desks, armoire drawers, bookcases, lockers, file cabinets, chests, footlockers and closets repair the same way. Office chairs are already intact: pick them up. POI water heaters repair into a working boiler (vanilla tank model); house radiators repair into the working cast-iron radiator.
- **Baths** — empty tubs and clawfoot baths work as soon as you find them (they tell you they need pipes and a boiler). Loot a gore bath and it becomes that empty tub.
- **Shower / bath** — pipe them for water. **Wash** uses 2 jars (shower) or 4 jars (bath). Hot water from a powered combi takes off 90-100% of your body smell (a bath takes all of it) for 10 or 20 minutes. Cold water still washes, and it takes about half the smell off for half as long.
- **Air conditioners** — a wall split unit (20W) and a ceiling cassette (30W, place it on a ceiling). Wired, standing within about 6 blocks keeps the heat off, the summer version of a radiator. It hums while it runs.
- **Fridge-freezer (white)** — a tall white UK fridge-freezer, two blocks high, to match the boiler and radiators. It has two compartments of 24 slots each. Tap E to open the fridge (food lasts about 7 days). Hold E and pick **Open freezer** for the freezer: food there lasts about 30 days and freezes solid in about 4 hours, twice as long as a chest freezer. **Store food** puts food in the fridge. Look at any fridge or freezer to see its temperature: a fridge runs at 39°F / 4°C, the fridge-freezer's freezer at 0°F / -18°C and a chest freezer at -4°F / -20°C. Unpowered, they sit at room temperature. The game shows the unit you picked in the options.
- **Washing machine / tumble dryer** — white UK front-loaders, plus the **US** versions which are the vanilla washer and dryer. Open them like a cooker: every clothing and armour piece in the game is a recipe. Wear it or carry it and it highlights. Click it to see how dirty or wet it is and how long it will take, then **Wash** or **Dry** queues it like cooking and it comes out the other side. Time depends on the piece (a hat is quicker than a coat) and on dirt or wetness (20% dirty is five times quicker than filthy). The washer needs power (15W) and a cold water pipe, one jar per garment, and clothes come out **wet**. The dryer is power only (20W) and faster. They hum while a piece is in and ding when it is done.
- **Washing line / clothes airer** — dry wet clothes and armour with no power. Wet on the left, dry on the right: hang washing on the left and it moves across when it is dry. The line is two posts, a line, pegs and a clothes prop, 5 blocks wide: under open sky it dries clothes in about 2 hours in daylight and twice that at night, but rain soaks them again. Under a roof it dries like the airer. The indoor airer dries in about 3 hours in any weather, twice as fast next to a warm radiator. The washing you hang up shows on them. Pegs are made by hand, 10 at a time; a line needs 10.
- **Wall thermostat** — runs all the radiators on a heating loop from one place. Wire it from a socket; it shares the socket and doesn't use up a plug. Put it within 8 blocks of a radiator (`ThermostatRange`): it takes over that radiator's loop, every radiator piped to it, and the boiler. Hold E for **Turn all radiators up** / **Turn all radiators down** (sets every valve on the loop to the same 0 to 5) and **Central heating on / off**. Turn one radiator's own valve to override that radiator, until the thermostat is turned again. Look at it for the room temperature, its setting, how many radiators it runs, and whether the heating is on.
- **Wall thermometer** — look at it for the room temperature, the temperature outside, and whether a radiator or air con is working on the room. Shows °F or °C to match your game options.
- **Sockets and switches** — UK switched sockets (single and double), US outlets (single and duplex, always live, no switch), UK and US light switches, a UK 45A cooker switch and a US 240V range outlet (the US has no cooker switch: a range plugs straight into its own always-live outlet). Wire power into one, then wire from it to the appliance. A socket only takes as many plugs as it has outlets: one wire out of a single, two out of a double, one out of the cooker switch or range outlet; the wire tool says so when it's full. A plug shows in the outlet for each appliance, with a black lead to it. Each outlet of a UK double socket has its own switch (the first appliance wired is the left plug); a UK single socket turns everything plugged into it off and on. A light switch turns only its own lights or air con off and on, not the next switch on the run. With the wire tool, click the switch and then a light already in the house: that light follows the switch, and it stays on only while the switch is on and the switch has power. The rockers flick up and down with the switch. A socket or switch only goes on a solid wall. Hang it on the wall behind a kettle, toaster or hi-fi, then place that appliance in the same block: the plate stays on the wall behind it. Pick the appliance up and the socket comes back. The black lead from a plug stays on a wall, down to the bottom of the appliance, and goes around windows.
- **Socket and switch chains.** Besides its plugs, every socket can have one wire on to another socket, which doesn't use up a plug and has no lead. Light switches work the same way: chain a row of switches and they all stay live, and each one only switches the lights or air con wired from it. Power always goes through to the next switch, whether this one is on or off. Plates go in sequence: each one links on to only one more, so a new socket or switch goes on the end of the run, and a run takes at most 10 (`MaxSocketsPerRun`). A wall plate does not use up the circuit's last watt, so chaining another kitchen socket does not kill the plugs. New UK sockets start on; a double that has never been flicked has both outlets live. Switching a UK socket off only kills its own plugs. Hover a socket or switch to see if it links on and where it is on its run.
- **Consumer unit (UK) / breaker panel (US)** — Wire it from a generator, battery bank or solar bank, then wire one circuit out of it per way (6 in the UK unit, 8 in the US panel): a socket, a chain of sockets, a light switch or a light. **A circuit is dead until it has a breaker.** UK ways show the breaker standing vertical. US ways show it lying horizontal. Look at the panel to see every circuit and its load. Hold E for:
  - **Next circuit** — picks which circuit the other options work on (marked `>`).
  - **Fit breaker** — hold a 5A, 15A, 30A or 45A breaker and fit it in the picked circuit. Whatever was in there comes back to you.
  - **Take out** — gives the breaker back.
  - **Reset / switch breaker** — resets a tripped breaker, or switches a breaker's circuit off and on.
  - **Name circuit** — type a name like `Kitchen sockets` and press Enter. The panel lists it by name, and every socket, switch and TV on that circuit says `Circuit: Kitchen sockets` when you look at it.
  - **Main switch** — kills every circuit at once.
- **Breakers** — 5A, 15A, 30A and 45A (5W per amp: 25W, 75W, 150W, 225W). The icon colour shows the rating: white 5A, blue 15A, red 30A, green 45A. Both boards take these. A circuit that asks for more than its rating for 2 seconds **trips its breaker** (reset it at the panel once you've unplugged something). The load is everything switched on along the circuit, so an idle cooker or a switched-off TV costs nothing until it runs. Breakers unlock with the consumer unit. A fuse already fitted in an old save becomes a breaker of the same amp, switched off if it had blown.
- **Toaster** — plug it into a socket (5W only while it toasts). Put in bread slices; it buzzes while it toasts and **pops** when it's done. The UK toaster is our model; the **US toaster** is the vanilla toaster, same wiring and toast.
- **Kettle** - a worktop appliance: **plug it into a socket** with the wire tool (you see the plug and lead, same as a microwave). It draws 10W only while boiling. Hold a jar of water (murky is fine) and hold E on the kettle to **fill** it: one jar is one cup, up to **3 cups**, and you get the empty jar back. **Switch on to boil**: it bubbles while it boils (30 seconds for a full kettle, `KettleBoilSeconds`), only while the socket is live, then clicks off. For a **cuppa**: right-click with a mug to put it down on the worktop, hold a tea bag (**PJ Tops** or **Yorkie Brew**) and hold E on the mug to **put the bag in**, **pick up the mug**, then hold it and hold E on the boiled kettle to **pour**: that gives the mug of tea and uses one cup. Hold an empty jar to pour a **jar of boiled water** instead. The water stays hot for 10 minutes (`KettleHotMinutes`), then it has to be boiled again. Look at the kettle to see how many cups are in it and whether it is cold, boiling or hot.
- **TV (stand / wall)** — a 43 inch flat screen that plays your own videos. The screen stays black until it is on, plugged in and the socket has power. Wire it from a socket (the wire tool won't take anything else); it draws 15W only while on. Hold E for **On / Off**, **Next channel**, **Previous channel**. Every video file in `%AppData%\7DaysToDie\HSWorkingStuff\TV` (made the first time you look at a TV) or the mod's `TV` folder is a channel, in file name order, named after the file. `.mp4` (H.264 with AAC sound) works best. New files appear within a few seconds. Every TV on a channel shows the same moment. Each player's PC plays the video, so in multiplayer everyone needs the same files; the server needs none. TVs further than `TVViewRange` away stop playing to save your frame rate.
- **Hi-fi and speakers** - a micro hi-fi with separate speakers. Wire the hi-fi from a socket (it uses a plug), then wire up to **5 speakers** out of the hi-fi with the wire tool. A speaker stands on a unit, shelf or the floor; the **wall speaker** goes against a wall on a bracket at head height. The music only comes out of the speakers: with none wired the hi-fi still plays (the display says so) but you hear nothing. Hold E on the hi-fi for **Power**, **Play / Pause**, **Next track**, **Previous track**, and **Volume up / down**, which sets every speaker to the same level. Hold E on a speaker to turn just that one up or down; the hi-fi volume sets them all again. When a track ends the next one starts, round to the first. Put `.mp3`, `.ogg` or `.wav` files in `%AppData%\7DaysToDie\HSWorkingStuff\Music` (made the first time you look at a hi-fi) or the mod's `Music` folder; they play in file name order, up to 64. Each player's PC plays the music, so in multiplayer everyone needs the same files; the server needs none. A black speaker cable stays on the walls and does not cross a window. A wall speaker’s cable goes up the wall behind the hi-fi, along the edge of the ceiling and down to the speaker. A stand speaker’s cable goes down that wall to the floor, along the edge of the floor and up to the speaker.
- **Wheat, bread and tea.** Plant wheat seed like corn: it grows short and green, then tall, then golden and ready to harvest. 5 wheat makes a seed, 3 wheat grinds into flour, 2 flour and a jar of water make dough, and dough bakes into a loaf in the campfire cooking pot. Cut a loaf into 8 slices for the toaster. Mugs are fired from clay on the campfire. Tea bags come from kitchen loot, coffee machines and the traders. The coffee maker also brews coffee in a mug. Drinking from a mug always gives the mug back.
- **Gas grill / charcoal grill** — barbecue workstations with the vanilla grill menu (corn, grilled meat, steak and potato, wings, jerky, fish tacos) and no grill tool needed. They burn wood or coal like a campfire; a gas can lasts a minute in the gas grill. Same food unlocks as the campfire grill.

## Living in the house

- **Room temperature.** The blocks the room is made of hold the biome. Brick, concrete and cobble keep desert heat and snow cold for a long time. Solid steel holds it too, and lets it go sooner than brick. Timber lets the weather through sooner. Corrugated sheet follows the weather. A warm radiator heats the nearest solid blocks first, then the warmth walks outward through the walls, and the air follows. Once that warmth reaches an outside face it draws zombies, weaker than a campfire. A warm radiator settles the room near 68°F / 20°C. A running air conditioner cools it to 72°F / 22°C. The thermometer, the radiator gauge and the temperature you feel all follow the °C or °F option in the game's video settings. The numbers in the settings file stay Fahrenheit, because that is the unit the game stores.
- **Smell.** After about a day without a wash you start to smell (**Unwashed**), and it gets worse over three days. Running or a hot body sweats, and that builds twice as fast. The icon shows extra metres zombies can smell you. **Wash up** at a sink (1 jar) or crouch at a lake or river and press the drink key to wash: same drink anim as the tap, each takes about 5 m off, once an hour, and rinses a bit of dirt off the clothes you are wearing. Standing in water up to your waist washes 50-70% off slowly (clothes too). A shower takes 90-100% and a bath all of it. **Indoors cuts the distance** (about a quarter with the doors shut). An open door or window next to you leaks smell outside. Walls stop it going through the brick. Rain or water hides it.
- **Dirty clothes.** Clothes and armour you wear get dirty over about three days. Sweat dirties them faster, and walking through fields, crops, mud or sand dirties them faster again. At about 20% the **Dirty Clothes** icon comes on and shows the percent. At about half dirty they add to your smell. Hover a piece to see how dirty it is. Wash them in the washing machine.
- **Bladder and bowels.** An empty bladder fills in about 24 game hours on its own, faster when you drink (a jar is about a quarter). Bowels fill in about 72 hours, faster when you eat. At about 60% you get **Need a pee** or **Need the toilet**, with how full you are on the icon. Pee at a toilet, a urinal or a tree (look at the trunk and hold E). Poo only in a toilet. Leave it until you're full and you have an accident: a pee soaks your clothes, a poo makes them filthy, and both make you smell. Wash the clothes and have a shower. God mode clears smell, dirty/wet clothes, and the need to go.

## Unlocks

Every recipe in the mod needs **both** a magazine level and a perk rank. Pipes, the pipe tool, the toilet, the shower and the bath are workstation magazine 6 and Advanced Engineering 1, crafted in your hands. The crafting list shows which magazine and perk unlock each one.

| Magazine | Level | Perk | Unlocks |
| --- | --- | --- | --- |
| Electrician | 25 | Advanced Engineering 1 | Microwaves, coffee makers, kettle, UK and US toaster, TVs, hi-fi and speakers, electric cookers, sockets, UK and US light switches, cooker switch, range outlet, consumer unit, breaker panel, fuses |
| Electrician | 45 | Advanced Engineering 2 | Fridge, white fridge-freezer, chest freezer, combi boiler, wall and ceiling air conditioners, breakers |
| Workstations | 6 | Advanced Engineering 1 | Pipe tool, pipes, intake, tank, sink, rain catch, toilet, urinal, shower, bath |
| Workstations | 16 | Advanced Engineering 1 | Water cooler, wall thermometer, clothes pegs, washing line, clothes airer |
| Workstations | 26 | Advanced Engineering 2 | Radiators, wall thermostat, washing machines, tumble dryers |
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
