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
| `AirConRadius` | 6 | Blocks from a running air conditioner that keep you cool |
| `ShowerJars` | 2 | Hot water jars a shower uses |
| `BathJars` | 4 | Hot water jars a bath uses |
| `GasCanGrillSeconds` | 60 | Seconds one gas can burns in a gas grill |
| `EnableRoomTemperature` | true | Indoor temperature follows the weather slowly; radiators and air con heat or cool the room |
| `IndoorInsulation` | 15 | Degrees F a house takes off the weather (vanilla shelter is 30) |
| `IndoorLagHours` | 3 | Game hours for a house to catch up with the weather outside |
| `RadiatorHeat` | 40 | Degrees F a warm radiator can add to a room |
| `ThermostatHeat` | 68 | Radiators heat a room up to this (F) |
| `AirConCool` | 30 | Degrees F a running air conditioner can take off a room |
| `ThermostatCool` | 72 | Air con cools a room down to this (F) |
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
| `WetClothesCold` | 6 | Degrees F colder you feel per wet piece you wear |

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
- **Sink** — the menu shows what the pipes give it. Cold comes straight from a tank (or lake intake). Hot comes through a powered combi boiler and is always boiled. A tap you can't use is greyed out and says why: no water, needs a boiler, boiler has no power. What the tap offers depends on your hand: empty jars in hand **fill those jars**, an empty bucket **fills the bucket**, anything else (or nothing) **drinks**. **Fill all on you** fills every empty jar and bucket in your bag and toolbelt. Filling swings your arm with the vanilla water-fill sound; drinking plays the drink sound.
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
  - **Valve and gauge.** Hold E on a radiator for **Turn up** / **Turn down**: off, or 1 to 5 (new radiators start at 3). Higher reaches further (3 to 7 blocks) and draws more power; off takes it out of the heating. Looking at it shows its own temperature on a gauge, climbing to about 40°C (104°F) at valve 1 up to 80°C (176°F) at valve 5 while heating, and cooling back to the room temperature when off. It follows your °C/°F setting.
- **Electric cooker (old / modern / wall)** — same recipes and tools as a campfire (cooking pot, grill, beaker). No wood. Wire it to a generator or it will not cook. Draws 10W only while food is queued or its window is open. The cooker window shows **Powered** or **Unpowered** where the campfire fuel slot was.
- **Microwave (modern / retro)** — cheap, 5W only while cooking with the door shut, a short microwave-safe menu with no pot or grill: boiled water, coffee, goldenrod tea, red tea, honey tea, baked potato, corn on the cob, vegetable stew, sham chowder, spaghetti, gumbo stew. Each one needs the same unlock as at the campfire. Queue the food and close the door to cook; opening the door pauses it. Look at it to see what's cooking and the time left. It hums while cooking and dings when the queue is done.
- **Coffee maker (home / commercial)** — put in a jar of water and coffee beans and it brews a jar of coffee; add 5 gunpowder for black strap coffee. Murky water is fine, the machine heats it, so no boiling first and no pot. Same unlocks as campfire coffee. 5W, only while brewing or while its window is open. Look at it for what's brewing and the time left.
- **Chest freezer** — a 2-block-wide chest freezer. Opens like the fridge with or without power; wired (10W), food in it does not spoil at all.
- **Plumbed water cooler** — the vanilla water cooler, piped to a tank like a sink. Cold tap only, and it has its own filter, so even murky tank water comes out clean.
- **Shower / bath** — pipe to a powered combi boiler. **Wash** uses 2 jars (shower) or 4 jars (bath) of hot water. A shower takes off 90-100% of your body smell and a bath all of it, and for 10 or 20 minutes carried meat, smelly food and sickness won't let zombies track you by smell.
- **Air conditioners** — a wall split unit (20W) and a ceiling cassette (30W, place it on a ceiling). Wired, standing within about 6 blocks keeps the heat off, the summer version of a radiator.
- **Fridge-freezer (white)** — a tall white UK fridge-freezer, two blocks high, to match the boiler and radiators. Works exactly like the fridge.
- **Washing machine / tumble dryer** — white UK front-loaders (plus the old vanilla-model versions). Put worn clothes and armour in and pick **Wash**: it needs power (15W) and a cold water pipe from a tank like a sink, and uses 4 jars. Clothes come out clean but **wet**, which makes you colder while you wear them, until they dry on you (6 hours) or go through the tumble dryer (power only, 20W, **Dry**).
- **Wall thermometer** — look at it for the room temperature, the temperature outside, and whether a radiator or air con is working on the room. Shows °F or °C to match your game options.
- **Sockets and switches** — UK switched sockets (single and double), US outlets (single and duplex, always live, no switch), UK and US light switches, a UK 45A cooker switch and a US 240V range outlet (the US has no cooker switch: a range plugs straight into its own always-live outlet). Wire power into one, then wire from it to the appliance. A socket only takes as many plugs as it has outlets: one wire out of a single, two out of a double, one out of the cooker switch or range outlet; the wire tool says so when it's full. A plug shows in the outlet for each appliance, with a black lead to it. Each outlet of a UK double socket has its own switch (the first appliance wired is the left plug); a UK single socket and the switches turn everything wired from them off and on. The rockers flick up and down with the switch. Hang sockets in the cell above a floor or worktop and switches in the cell one up from the floor.
- **Gas grill / charcoal grill** — barbecue workstations with the vanilla grill menu (corn, grilled meat, steak and potato, wings, jerky, fish tacos) and no grill tool needed. They burn wood or coal like a campfire; a gas can lasts a minute in the gas grill. Same food unlocks as the campfire grill.

## Living in the house

- **Room temperature.** Indoors, the temperature follows the weather outside slowly (about 3 game hours behind) and a house only takes 15°F (about 8°C) off it, like a UK house: cold in winter, hot in summer, cooler after rain. In the snow or on a cold night you need the radiators on. A warm radiator heats the room up to 68°F (20°C); a running air conditioner cools it to 72°F (22°C). Everything the mod shows in game follows your °C/°F option; the temperature settings below are in °F.
- **Smell.** After a day without a wash you start to smell (**Unwashed**), and it gets worse over three days. Zombies can follow it. **Wash up** at a sink (1 jar) or crouch at a lake or river and press the drink key to wash: each takes about 5 m off, once an hour. Standing in water up to your waist washes 50-70% off slowly. A shower takes 90-100% and a bath all of it. Indoors it is cut down like other smells; rain or water hides it.
- **Dirty clothes.** Clothes and armour you wear get dirty over about three days of wear (hover an item to see how dirty). Dirty pieces add to your smell. Wash them in the washing machine.

## Unlocks

Every recipe in the mod needs **both** a magazine level and a perk rank. The crafting list shows which magazine and perk unlock each one.

| Magazine | Level | Perk | Unlocks |
| --- | --- | --- | --- |
| Electrician | 25 | Advanced Engineering 1 | Microwaves, coffee makers, electric cookers, sockets, UK and US light switches, cooker switch, range outlet |
| Electrician | 45 | Advanced Engineering 2 | Fridge, white fridge-freezer, chest freezer, combi boiler, wall and ceiling air conditioners |
| Workstations | 16 | Advanced Engineering 1 | Pipe tool, pipes, intake, tank, sink, rain catch, water cooler, wall thermometer |
| Workstations | 26 | Advanced Engineering 2 | Radiators, shower, bath, washing machines, tumble dryers |
| Home Cooking | 6 | Master Chef 1 | Drying rack, dried meat, charcoal grill |
| Home Cooking | 10 | Master Chef 1 | Cooler |
| Home Cooking | 26 | Master Chef 2 | Gas grill |
| Seeds | 10 | Living Off The Land 1 | Compost box |

Food cooked in the microwave, coffee maker and grills uses the vanilla food unlocks.

## Power use

Appliances only draw from the generator while they work. The fridge draws 5W while it is switched on. Cookers and coffee makers draw while cooking, the microwave while cooking with its door shut, and the boiler while a hot tap has just run or central heating is on. An idle appliance still shows as powered when its circuit is live.
- Microwave food uses the vanilla food unlocks (Master Chef / cooking magazines). If you can't cook it on a campfire yet, you can't microwave it either.

Pickup keeps fridge contents and tank water.

Install on host and clients. After updating the dedicated copy, restart the **dedicated server and the client**.

See `CHANGELOG.md` for what changed in each version.
