# HS Working Stuff — Changelog

Works on 7 Days To Die **3.2 and 3.3** with the same download. Install on the dedicated server and on every client, and keep them on the same version.

## 1.0.49

- **Washer and dryer open like the cooker.** Clothes and armour you wear or carry highlight as washable. Click one to see how dirty or wet it is and how long it will take, then queue it. The same piece comes out the other side clean (and wet) or dry. Hats and gloves are quicker than a coat; 20% dirty takes a fifth of the time of filthy. One jar of cold water per wash. A tumble is faster than a wash (`DryerSeconds` 20, `WasherSeconds` 30 at 100%).
- **Washing line and airer: wet on the left, dry on the right.** Hang wet clothes on the left; when they are dry they move across. Line and airer times are unchanged (hours, weather). The dryer is still seconds.

## 1.0.48

- **Washing machine (and dryer) run for 30 seconds.** They hum, then ding, then the clothes are clean (and wet). A splash and instant finish was skipping the cycle, so dirt often stayed.
- **A tank only feeds what is in it.** Pipes on one side of the tank no longer join pipes on the other. Look at a tank and it only says how much water it holds.
- **Socket and switch hover is short unless you hold the wire tool.** Everyday: live or not, on or off, and what's plugged into that plate. Wire tool: the run count and circuit name as well.

## 1.0.47

- **A radiator belongs to the nearest boiler on the heating pipes.** Sharing a tank still left the upstairs boiler claiming the downstairs rads when the cold pipes teed together. Each radiator is counted only on the boiler it actually reaches first.
- **Wash no longer plays the drinking sound.** Sink wash-up, shower, bath and a lakeside wash keep the drink animation and use a water splash (`hsStream`) instead.

## 1.0.46

- **Each boiler only counts its own radiators.** Sharing a tank (or sinks, shower, bath, washer) no longer makes the upstairs boiler claim the downstairs heating loop. Central heating stays on the pipes from that boiler to its radiators.

## 1.0.45

- **Buff icons show how bad it is.** Unwashed and dirty clothes show extra smell in metres, wet clothes show hours left to dry, and pee / poo show how full you are.
- **Bathroom sinks no longer crash the radial.** A vanilla sink used to hit a missing take-command slot when you drank or washed. Bathroom taps now use the same menu as the kitchen sink.
- **Kitchen (and bathroom) taps play the drink animation.** Drink, wash and fill reset and fire UseItem so the empty-hand drink actually shows.

## 1.0.44

- **Kitchen sockets feed their plugs again.** A new socket on a run no longer eats the last watt and leaves every appliance saying no power while the plate and the consumer unit still look live. Double sockets that were never flicked now start with both outlets on. New UK sockets start switched on.

## 1.0.43

- **Microwave hum is close and quiet.** You only hear the low hum when you are next to it while it cooks. It still dings when the queue is done. Opening the door stops the hum.

## 1.0.42

- **Wash uses the drink animation.** Sink wash-up, shower, bath, and crouching at a lake all play the same empty-hand drink as a tap or water source (`player_drinking`), not the jar-fill swing.

## 1.0.41

- **Appliance sounds, except the fridge and freezer.** The toaster buzzes and pops when it finishes. The coffee maker gurgles and dings, cookers hiss while they cook, the washer and dryer ding when a load is done, air con hums, and the boiler whooshes while it fires. Microwave and kettle sounds are unchanged.
- **US toaster** uses the vanilla toaster model. No new mesh. The vanilla washer and dryer are now named US Washing Machine / US Tumble Dryer.

## 1.0.40

- **Kettle (and toaster) plug into a socket.** Wire them from a wall socket like a microwave: you get the plug and lead, they refuse a wire straight from the generator, and the kettle draws 10W only while it boils. Look at an unplugged kettle and it says so.

## 1.0.39

- **Light switches chain like sockets.** Wire switch to switch and power always goes through, whether that switch is on or off. Each switch only turns its own lights or air con on and off; looking at it says "Switch on Fluorescent Light", not the next switch as well. One onward switch per plate, same run limit as sockets.

## 1.0.38

- **Speaker cables stay on the walls.** They no longer hang across the room. Each run goes from the hi-fi to the wall, up to the ceiling, along the ceiling, and down to the speaker.

## 1.0.37

- **The kettle is made by hand now.** Fill it with jars of water (one jar is one cup, up to 3, and you get the jar back), switch it on and it bubbles until it boils, then clicks off. Put a mug down on the worktop (right-click with it), drop a tea bag in, pick it up and pour the boiled kettle onto it for the brew. An empty jar takes a jar of boiled water. Boiled water goes cold after a while. The kettle no longer has a crafting menu; anything left in an old kettle's output can still be taken out. New settings: `KettleBoilSeconds`, `KettleHotMinutes`.

## 1.0.36

- **Hi-fi and speakers.** A micro hi-fi that plugs into a socket, with separate speakers wired out of it (up to 5): standing speakers for a unit or shelf, and wall speakers on a bracket. Music only comes out of the speakers. Power, play / pause, next and previous track, and volume for every speaker from the hi-fi; each speaker can be turned up or down on its own. Plays .mp3, .ogg and .wav files from the Music folder, moving on to the next track when one ends. Made at a workbench; unlocked with the TVs. New settings: `HifiVolume`, `HifiHearRange`.
- **TVs switch on now.** The on/off flag was saved in a part of the block that only holds 4 bits, so it was dropped every time and the TV only clicked. It now lives where all 8 bits are kept.

## 1.0.35

- **Wall thermostat.** Wire it from a socket (it shares the socket and doesn't use a plug) and put it within 8 blocks of a radiator. Turn it up or down to set every radiator on that heating loop at once, and switch the central heating on or off from it. A radiator's own valve still overrides it until you turn the thermostat again. It shows the room temperature, its setting and how many radiators it runs. Made at a workbench; unlocked with the radiators. New setting: `ThermostatRange`.

## 1.0.34

- **Fridge-freezer: a separate fridge and freezer.** Tap E to open the fridge; hold E and pick **Open freezer** for the freezer. Each has 24 slots. Store food goes in the fridge. (In 1.0.33 the freezer was the bottom two rows of one window.)
- **Pipes never cross the middle of a room.** Pipe you can see only runs up or along a wall, or straight up into the thing it feeds. Anywhere else it goes inside the floor. Take up any old runs standing in the room with the pipe tool and lay them again.
- **TV says what it's doing.** The game log now says why a TV isn't showing anything (switched off, not wired from a socket, no power, too far away, no video files, or a video that won't load), and when it loads a video. Video files with spaces in the name now load.

## 1.0.33

- **All the mod's text now loads.** The text file had the wrong name, so the game never read it and some names and help showed as raw keys (like `hsToilet`). Every block, item, buff and help line now shows proper text.
- **TV works once it's wired.** A TV wired from a live socket said "Not plugged in" until you reloaded. It now sees its socket straight away, and so do the circuit name on sockets and appliances and the socket count on a run. With no video files it says where to put them.
- **The fridge-freezer has a freezer.** Food in its freezer compartment freezes in about 4 hours, twice as long as a chest freezer, and barely spoils until it does.
- **Fridge and freezer temperatures on hover.** A fridge shows 39°F / 4°C, the fridge-freezer shows its fridge and its freezer (0°F / -18°C), and a chest freezer shows -4°F / -20°C. Unpowered, they show the room temperature. The game shows the unit you picked in the options.
- New settings: `FridgeFreezerMultiplier`, `FridgeFreezerFreezeHours`, `FridgeTemp`, `FreezerTemp`, `FridgeFreezerTemp`.
- **Toilet and urinal use the game's own models.** The vanilla toilet and wall urinal, made working. Same plumbing, flushing and recipes.
- **Pipe drops stay on the wall.** When the floor under a boiler (or other thing) is already taken by another pipe, the drop now runs down the wall beside it instead of standing out in the room.

## 1.0.32

- **Toilet and urinal.** A close-coupled toilet and a wall urinal. Pipe either one like a sink. A flush takes 1 jar from the tank (dirty water is fine). With no pipe, pour a jar or a bucket into the cistern and flush from that. Two pees at a urinal share one jar.
- **Bladder and bowels.** You need a pee as time passes, and faster when you drink. You need a poo as time passes, and faster when you eat. A toilet takes either. A urinal or a tree takes a pee only. A dirty toilet bowl smells and will not take another poo until you flush it.
- **Accidents.** Leave it too long and you wet yourself (clothes soaked, and you smell) or soil yourself (clothes filthy, and you smell worse). Wash the clothes and wash yourself.
- **Sounds.** A stream when you pee, a splash in the bowl, a flush of water, a fill when you pour into the cistern, and a wet or filthy sound if you don't make it.
- New settings: `EnableNeeds`, `BladderHours`, `BowelHours`, `DrinkBladder`, `EatBowels`, `UrgeAt`, `FlushJars`, `UrinalUsesPerJar`, `CisternJars`, `DirtyToiletSmell`.

## 1.0.31

- **Washing line.** Two wooden posts with a line, pegs and a clothes prop, 5 blocks wide. Open it like a box and hang wet clothes and armour on it: they dry with no power, in about 2 hours in daylight and twice that at night. It needs open sky, and **rain soaks the washing again**, so bring it in. Under a roof it dries like an airer. What you hang up shows on the line, up to 8 pieces.
- **Clothes airer.** A folding gullwing airer for indoors: wet clothes dry on it in about 3 hours whatever the weather, twice as fast next to a warm radiator. Up to 6 pieces show on it.
- **Clothes pegs.** Made by hand from wood and a spring, 10 at a time. A washing line needs 10.
- Look at a line or airer to see how many pieces are hung up and how long they'll take to dry.
- **Sockets go in sequence, up to 10 on a run.** A socket still links on to only one more socket, so a new socket has to go on the end of the run. A run now takes at most 10 sockets; start another circuit from the consumer unit for more. Look at a socket to see where it is on its run (`Socket 3 of 7 on this run`).
- New settings: `LineDryHours`, `AirerDryHours`, `MaxSocketsPerRun`.

## 1.0.30

- **Consumer unit (UK) and breaker panel (US).** Wire one from the power, then wire one circuit out of it per way: 6 ways in the white UK consumer unit with its red main switch, 8 in the grey US panel with its main breaker. A circuit stays dead until you fit a fuse or a breaker for it.
- **Fuses that blow.** 5A, 15A, 30A and 45A fuses (5W per amp). Put more on a circuit than its fuse carries and after 2 seconds it blows with a bang: the circuit goes dead and the fuse is useless (a **Blown Fuse**) until you take it out and fit a new one. Fuses are cheap and made by hand.
- **Breakers that trip.** The same ratings as breakers. Overload trips the breaker instead of burning it out: reset it at the panel. A breaker also switches its circuit off and on. Breakers are made at a workbench and unlock with the fridge and boiler.
- **Name your circuits.** Hold E on the panel, pick a circuit with **Next circuit**, then **Name circuit** and type `Kitchen sockets`, `Lights`, `Cooker` and so on. The panel lists every circuit with its name, fuse, load and whether it's blown, tripped or off, and every socket, switch and TV on a circuit shows `Circuit: <name>` when you look at it.
- **Socket chains.** Each socket can now wire on to one more socket as well as its plugs. The onward link doesn't use up a plug and has no lead, so a whole kitchen of sockets can share one circuit and one fuse. Switching a UK socket off only kills its own plugs; sockets further along the chain stay live.
- Switches and lights wired straight from a UK double socket now go off with that outlet's switch too.
- New settings: `WattsPerAmp`, `OverloadSeconds`.

## 1.0.29

- **TVs that play your videos.** A 43 inch flat-screen TV on a stand, and one for the wall. Every video file in the TV folder is a channel (`.mp4` works best), in file name order, and the channel name is the file name. Drop files into `%AppData%\7DaysToDie\HSWorkingStuff\TV` (made for you) or the mod's `TV` folder and they show up in game within a few seconds. Hold E for **On / Off**, **Next channel** and **Previous channel**. The sound comes from the set and fades with distance.
- **TVs need a socket.** Wire a TV from a socket; the wire tool refuses anything else. It draws 15W only while switched on, and goes dark when the socket or power is off.
- Every TV on the same channel shows the same moment, like broadcast TV. Each player's PC plays the video itself, so in multiplayer everyone needs the same files to see the same channels; the dedicated server needs none.
- **Plug leads meet the back of the appliance.** The black lead from a socket now ends on the side of the appliance facing the socket (its back, or its top when the socket is higher), instead of inside it.
- New settings: `TVVolume`, `TVHearRange`, `TVViewRange`.

## 1.0.28

- **Frozen food.** Food left in a powered chest freezer for 2 game hours freezes solid. Frozen food can't be eaten and recipes won't use it, but it doesn't spoil while frozen. Take it out and it thaws in 2 hours at room temperature, or 8 hours in a powered fridge. The item tooltip says it's frozen and how long it has left to thaw.
- **Microwave Defrost.** Hold E on a powered microwave and pick **Defrost** to thaw all the frozen food you carry at once.
- New settings: `EnableFreezing`, `FreezeHours`, `ThawHours`, `FridgeThawHours`.

## 1.0.27

- **Kettle.** A white electric kettle (10W while boiling). Boils a jar of water straight from murky, and makes mugs of tea.
- **Toaster.** A two-slot toaster (5W while toasting). Turns a slice of bread into toast.
- **Wheat.** A new crop. Wheat seed grows in three stages, from short green shoots to tall golden ears, and is harvested like corn (Living Off The Land boosts it). Craft 5 wheat into a seed. Seeds turn up in seed loot and at the traders.
- **Bread.** 3 wheat grinds into flour, 2 flour and a jar of water make dough, and dough bakes into a loaf on the campfire (cooking pot). Cut a loaf into 8 slices, and toast them.
- **Mugs and tea.** Fire 4 clay into 2 mugs on the campfire. **PJ Tops** and **Yorkie Brew** tea bags are found in kitchens, coffee machines and the traders. A mug, a tea bag and a jar of water in the kettle make a mug of tea. The coffee maker also brews coffee in a mug. Drinking always gives the mug back; the jar of water is used up.
- New icons are brighter.

## 1.0.26

- **Buckets empty down the sink.** Empty a full bucket of water while looking at a sink, shower or bath and it goes down the drain: no water is placed and you get the empty bucket back. Anywhere else it pours out like vanilla. Water coolers and washing machines are not drains.

## 1.0.25

- **US range outlet.** The US has no cooker switch: an electric range plugs into its own 240V outlet on the wall. New US Range Outlet (NEMA 14-50 style), always live, takes one plug and shows the big black plug and lead when a cooker is wired from it.
- **Radiator gauge reads the room when cold.** A cold radiator now settles to the room temperature where it hangs (what the wall thermometer shows), instead of a fixed 15°C / 59°F. Every temperature the mod shows follows your °C/°F game option.

## 1.0.24

- **Plugs show in the socket.** Wire an appliance from a socket and a plug appears in the outlet, with a black lead running to the appliance (vanilla wires only show with the wire tool out). A double socket shows one plug per appliance.
- **Each outlet of a UK double socket has its own switch.** Look at it for **Switch the left socket** / **Switch the right socket**; the first appliance you wire is the left plug, the second the right. A UK double socket placed before 1.0.24 starts with both outlets off: flick each one on once.
- **Switches flick.** The rockers on UK sockets, the UK light switch and the cooker switch now show up or down with the switch, the red flash on a UK socket shows when it is on, and the cooker switch neon lights only when it is on and powered. Vanilla no longer tints the whole plate green or red.
- **US light switch.** A US toggle switch: lever up for on, down for off.
- **Wash up at a sink.** Kitchen and bathroom sinks with a working tap have **Wash up**: 1 jar of water (hot if there is any), about 5 m off your smell. Once an hour; a shower or bath is the proper clean.
- **Showers and baths.** A shower now takes off a random 90-100% of your body smell and a bath all of it, and both still hide everything you carry for 10 or 20 minutes.
- **Wash in lakes and rivers.** Crouch at the water's edge and press the drink key to **Wash** (a splash, about 5 m off, once an hour). Standing in water up to your waist or more washes you slowly: 50-70% of your body smell comes off over half an hour (WaterWashHours). Get out early and you keep what came off. Standing up still drinks like vanilla.
- **Fridge-freezer icon** is no longer cut off top and bottom.
- Socket leads start from the plug instead of the bottom of the block.

## 1.0.23

- **Rooms follow the weather.** Indoors the temperature now trails the weather outside by a few game hours, and a house only takes 15°F / 8°C off it instead of vanilla's instant 30°F / 17°C. Cold snaps, nights and rainy days make rooms cold; summer makes them hot. Radiators heat a room to 68°F / 20°C and air conditioners cool it to 72°F / 22°C. Snow biomes need the heating on.
- **Wall thermometer.** Shows the room temperature, the temperature outside, and whether the heating or air con is working. Follows your °F/°C game option.
- **You get smelly.** After a day without a shower or bath you start to smell, worse over three days, and zombies can track it. Washing clears it.
- **Dirty and wet clothes.** Worn clothes and armour get dirty and add to your smell (hover them to see). Wash them in the new **washing machine** (power + cold water pipe, 4 jars). They come out wet, which makes you colder, until they dry on you or go in the **tumble dryer**.
- **UK white goods.** White UK washing machine, tumble dryer and tall fridge-freezer to match the boiler and radiators. The vanilla-model washer and dryer are craftable too.
- **Sockets and switches.** UK switched sockets (single and double), US outlets (single and duplex, always live), a UK light switch and a UK 45A cooker switch. Wire power in and wire the appliance from it; a single takes one plug and a double two, and the wire tool tells you when a socket is full.
## 1.0.22

- **Chest freezer is 2 blocks wide** and **opens without power**, like the fridge. Before, the freezer model could not be targeted at all, so it never opened. A 1-wide freezer you already placed: pick it up and place it again.
- **Home air conditioners.** The air conditioner is now a white wall split unit instead of the industrial vanilla box. New **ceiling cassette air conditioner** (30W) for the ceiling. Both unlock at Electrician 45 + Advanced Engineering 2.
- **Sink uses what's in your hand.** Empty jars in hand: **Fill jars in hand** (only those jars, never jars from your bag). Empty bucket in hand: **Fill bucket**. Anything else or nothing: **Drink**. **Fill all on you** still fills every empty jar and bucket you carry. A jar takes 1 jar of water and a bucket 4 (BucketUnits); when the tank runs short only part of a stack is filled. Same on vanilla sinks and the water cooler.
- **Sink animations.** Filling swings your arm with the vanilla water-fill sound, drinking plays the drink sound, and the shower and bath splash.
## 1.0.21

- **Everything has to be learned.** Every recipe in the mod now needs a magazine level **and** a perk rank, nothing is craftable from the start. Cookers, fridge, freezer, boiler and air con: Electrician magazines + Advanced Engineering. Plumbing, radiators, shower and bath: Workstation magazines + Advanced Engineering. Food storage and grills: Home Cooking magazines + Master Chef. Compost: Seed magazines + Living Off The Land. See the README for the levels. Blocks you already placed keep working.
- **Chest freezer.** New chest freezer model. Works like the fridge, but while it is powered (10W) food in it does not spoil at all.
- **Plumbed water cooler.** The vanilla water cooler, piped to a tank. Cold tap only, with a built-in filter so it always gives clean water.
- **Shower and bath.** Pipe them to a powered combi boiler. Wash uses 2 or 4 jars of hot water and takes away all your smell for 10 or 20 minutes.
- **Air conditioner.** Powered (20W), keeps you cool within about 6 blocks, like a radiator does for the cold.
- **Gas and charcoal grills.** Barbecue workstations with the vanilla grill menu and no grill tool. Burn wood or coal; a gas can lasts a minute in the gas grill.
- **Sink pipe stays under the worktop.** A sink on a cabinet no longer shows a pipe stub sticking up behind the tap; the pipe ends inside the cabinet.

## 1.0.20

- **Coffee maker.** New powered Coffee Maker and Commercial Coffee Maker (the vanilla models), unlocked with the microwave at Electrician 25. Put in a jar of water (murky is fine, it heats it) and coffee beans for a jar of coffee, or add 5 gunpowder for black strap coffee. No pot, no fire, no boiling first. Uses the same coffee unlocks as the campfire. 5W only while brewing or open; look at it for the time left.
- **Rain catch shift-click.** With the rain catch window open, shift-click (or the controller quick move) a dew gatherer, tarp or filter in your bag to drop it straight into its slot, like the vanilla dew collector.
- **Filtered water stays clean.** A rain catch with a filter now pours away any murky water in the tanks it feeds the moment it sends clean water in (murky jars or buckets poured in by hand, or anything left over), instead of mixing and turning the clean water murky. Its own murky water goes too. An empty tank or rain catch now says "empty" instead of keeping the old "murky" label.

## 1.0.19

- **Filter drains murky water.** Mixing water keeps the worse quality, so murky water left behind would spoil everything a filter collects after it. Putting a dew filter in a rain catch (window or radial) now pours away the murky water in that rain catch and in any murky tanks it fills, and tells you how many jars went. Clean water already in a tank is kept.

## 1.0.18

- **Rain catch opens.** Press E (or Open in the hold-E menu) for a rain catch window like the vanilla dew collector: three slots for the dew gatherer, tarp and filter, plus how much water it holds, its quality and whether it is piped to a tank. Drag tools in and out; anything else, or a second copy of a tool, goes back to your bag. The Install and Remove dew tools commands still work.
- **Sink pipe comes up from below.** A sink standing on a cabinet or a floor block is now plumbed from underneath: the pipe runs down the wall to the cabinet, inside it, and up into the sink, instead of joining the sink from the side at tap height. Take up the old sink run with the Pipe Tool and lay it again to get the new route.

## 1.0.17

- **Radiators need water.** The heating is now a sealed loop of hot water. Each radiator holds 1 jar. While central heating is on, the boiler fills dry radiators from a tank piped to it (or a lake intake), nearest first. Once filled, the loop keeps its water like a real sealed system, so the radiators stay warm even if the tank later runs dry. Every extra radiator needs another jar; only filled radiators heat. The boiler shows how many radiators are filled, and a cold radiator says how many more jars the tank needs. Existing heating starts empty and fills from the tank on the next load.

## 1.0.16

- **Radiator valve and temperature gauge.** Hold E on a radiator: **Turn up** / **Turn down** sets its valve to off or 1 to 5 (existing and new radiators start at 3). Higher reaches further and draws more power from the boiler; off takes the radiator out of the heating. Looking at it shows the radiator's own temperature on a gauge that climbs while it heats (about 40°C / 104°F at valve 1 up to 80°C / 176°F at valve 5) and cools back down when it stops. The gauge follows your °C or °F game option. The setting is saved with the radiator and shared with everyone on the server.
- **Sink says when the tank is empty.** A sink piped to an empty tank used to say "no tank piped in" and "boiler has no water behind it". It now says the tank is empty (cold), or the tank behind the boiler is empty (hot).

## 1.0.15

- **Fixed (3.3): wires, lights, switches, the cooker, boiler and rain catch losing their settings or connections on every load.** The fridge saved its food in the middle of its save record but read it back from the end. Everything saved after a fridge in the same area of the map was then thrown away on load (the log showed `Dropping TE with unknown/outdated type: None`), and their wires went with it. The fridge now reads its food back from the same place it wrote it. Saves made with the bug load correctly, food included. Anything already lost before this update has to be re-wired or re-placed once.
- Laid pipe was saving correctly (all 20 pipes in the test save were kept). Plumbing that seemed to "come unplumbed" was the boiler, pump and rain catch losing their data in the same fridge bug.

## 1.0.14

- **Pipe joins fixtures at the bottom.** Tanks, sinks, boilers and rain catches are plumbed at their lowest layer, from below (preferred, like the boiler's copper pipes) or from the side, never from on top. Sink pipe no longer comes in a block too high.
- **Pipe ends go into the fixture.** Where a pipe meets a tank, sink, boiler or radiator it carries on into the model instead of stopping short at the block edge. Existing laid pipe shows this too.
- **Radiators say what is wrong.** Instead of always "pipe to a combi boiler", a cold radiator now says whether it is not piped to a boiler at all, piped but central heating is off (turn it on at the boiler), the boiler has no power, or it is warming up.

## 1.0.13

- **Fixed: pipe would not go through concrete, wood and other building blocks.** Building blocks are shaped blocks, and the game only flags a shape as a solid cube if its model says so; the pipe tool now treats any block with all six sides full as solid. Pipe can run through the block a tank sits on, through walls and floors, and is hidden inside them.
- When New Connection finds no route, the log now says what was next to each end (`Pipe tool: no route from ...`), so a blocked route can be tracked down.

## 1.0.12

- **Half blocks and plates count when they face the right way.** The pipe tool checks each side of a block, not just whether it is a full cube:
  - A half block or plate that fills the **top** of its cell is a floor: pipe runs along it, radiators can stand on it, and radiator pipe runs through it under the floor.
  - A half block upright with its **flat full side** toward the pipe is a wall the pipe can run along (and the boiler's drop can run down).
  - A half block in the bottom of its cell is not a floor (anything on it floats half a block up), so it does not count.
- Doors, tables and other model blocks still never count.

## 1.0.11

- **Pipe never crosses open air.** Every bit of a run either sits against a floor, wall or ceiling, or is hidden inside a full block (wall, floor, terrain). It prefers the surface and only cuts through a wall or floor where that saves a long way round. If there is no such route you get "No route along walls or floors" instead of a pipe across the room.
- The drop from the boiler to the floor for radiators runs down the wall.
- Pipe no longer goes through doors, tables, plates or other part blocks, above or under the floor; only full blocks and terrain hide it.
- The HUD counts pipe hidden in floors or walls.

## 1.0.10

- **Pipe tool works like the lift and escalator tools.** Hold E for:
  - **New Water System**: aim at a rain catch, tank, boiler, sink, radiator or intake to start a system there. If it is already piped to things, you are told and that system is used.
  - **Use Water System Here**: aim at anything in a system (or a laid pipe) and new connections come from there. Use it on the boiler for hot runs, on the tank for cold.
  - **New Connection**: aim at the thing to connect and it plans the route from the system to it. The route is shown, then **Lay pipes**.
  - **Lay pipes** and **Take up pipes** as before.
- The system stays picked after laying, so you just aim at the next sink or radiator and pick New Connection again. No more Clear, no more one-thing-to-one-thing.
- New connections tee into the system's existing pipe where that is shortest, instead of always running back to the tank or boiler.
- Connecting something already piped to the system says so instead of laying a second run.
- Left click is Use Water System Here, right click is New Connection.
- The HUD shows the picked water system, how many things are in it, and what you are connecting.

## 1.0.9

- **Pipe tool: New connection.** The menu has **New connection**: it clears the last route and starts the next one from whatever you are looking at. **Clear** is gone; New connection does the same job.
- **Branch off laid pipe.** Start or end on a pipe you already laid and the new run tees into it, so you are no longer limited to one thing piped to one other thing.
- **Pipes hug walls and floors.** Routes prefer cells beside a floor or wall instead of cutting across a room, and the laid pipe sits against that floor or wall instead of floating in the middle of the block.
- **Radiators are fed under the floor.** Boiler to radiator is now a drop from the boiler to the floor, a hidden run through the floor, and straight up into the radiator. Later radiators branch off the pipe already under the floor.
- **The route preview is real pipe** (green, or red when you are short), not a flat line.
- **Pipe only joins what it was laid into.** A run passing under or beside a sink, tank or radiator no longer connects to it.
- **Fixed: Take up pipes could reach through walls** and take up a pipe in the next room.
- The log now says when a run is laid (`Pipe tool laid N pipes ...`) and how many laid pipes loaded with the save.

## 1.0.8

- **Fixed: error when looking at or clicking a microwave or electric cooker.** On 3.3, a cooker that had lost its cooking data threw `NullReferenceException` in `BlockWorkstation.GetBlockActivationCommands` every time you opened its menu. The cooker now rebuilds that data on load, or when you look at it, and the menu never errors. Food that was queued in a broken cooker is not recovered.

## 1.0.7

- **The boiler and Panel Radiator sit at the bottom of the block.** They were floating half a block up, poking out of the top of the cell.
- **The boiler and radiators can be broken and dug out.** Hits (tools, guns, admin digger) just "tinged" off them. Ones already placed in a save are fixed after the restart.

## 1.0.6

- **Laid pipe no longer gets in the way.** The pipe tool now lays pipe that is not a block. You still see it wherever the cell is open, but you can build walls, floors and furniture straight over it and the water keeps flowing. A full block or terrain in the cell hides that bit of pipe. **E > Take up pipes** on the pipe tool removes a run and gives the pipes back. Pipe blocks placed by hand still work.
- **Power on demand.** Appliances only draw from the generator while they work. Cookers draw while food is queued or the window is open. The microwave draws only while cooking with the door shut. The fridge already drew only while switched on. Idle appliances still show as powered when the circuit is live.
- **The water heater is now a wall-hung combi boiler** with its own model. Place it against a wall. It fires (15W) only for 30 seconds after a hot tap runs; idle it draws nothing. Heaters already placed in a save turn into boilers.
- **New: central heating.** Pipe radiators to the boiler and pick **Central heating on**. It draws 15W plus 5W per radiator, and standing within about 5 blocks of a warm radiator keeps you warm like a campfire.
- **New: Panel Radiator** (custom UK model) and **Cast Iron Radiator**, built at a workbench.
- New settings: `HotWaterSeconds`, `RadiatorWatts`, `RadiatorRadius`.
## 1.0.5

- **Pipe tool clicks work on 3.3.** Left or right click with the pipe tool threw `MissingFieldException: ItemInventoryData.holdingEntity` on 3.3 and did nothing.

## 1.0.4

- **Microwave-safe menu.** Chili dog, chicken nuggets, and black strap coffee are gone. It now cooks boiled water, coffee, goldenrod tea, red tea, honey tea, baked potato, corn on the cob, vegetable stew, sham chowder, spaghetti, and gumbo stew.
- **Cookers show their power.** The electric cooker and microwave windows show **Powered** or **Unpowered** where the campfire fuel slot was.
- **Rain catch no longer needs jars.** It collects murky water under open sky with nothing installed. Add the dew collector tools from its menu: the filter makes clean water, the gatherer and tarp each double the rate. Pipe it to a tank and it fills the tank.
- **Tanks are storage only.** A tank no longer collects water by itself. Fill it from a piped rain catch, or pour jars and buckets in.
- **Sinks have cold and hot taps.** Cold comes from a tank, hot comes through a powered water heater (always boiled). The sink menu shows both. A tap you can't use is greyed out with the reason, such as "Hot: needs heater" or "Hot: heater has no power". Water keeps the quality of its source, so murky in means murky out.
- **New: Pipe tool.** Select a start and an end like the lift and escalator tools. A HUD and a line preview show the route and how many pipes it needs. If you carry enough, it lays real pipe blocks for you.
- Tanks at negative Z coordinates could share water with the wrong tank. Fixed.
- Tanks and rain catches from older saves convert on load. A rain catch keeps its installed tools. Anything else that was inside the old collector slots drops on the ground next to it.

## 1.0.3

- **New: Microwave** (modern and retro look). Cheap to build at a workbench: 4 forged iron, 3 electrical parts, 4 polymers. Unlocks at Electrician crafting skill 25, alongside switches and lights. Wire it up, 5W.
- **Microwave menu only.** Boiled water, coffee, black strap coffee, red tea, goldenrod tea, honey tea, baked potato, corn on the cob, chili dog, and chicken nuggets. No pot or grill needed. Each recipe needs the same unlock it needs at a campfire, so the microwave never skips food progression.
- **It acts like a microwave.** Queue your food and close the door to start it. Opening it pauses the cooking, and closing it carries on from where it stopped.
- **Look at it to see the timer.** The prompt shows what's cooking and the time left ("Cooking Baked Potato: 0:42 left"). It also tells you when it's paused (door open or no power) and when the food is ready. You can still open it at any time.
- **It sounds like a microwave.** It hums while it cooks, goes quiet when the door opens or the power goes, and dings when the queue is done. Nearby players hear it too.
- **Working Fridge and Water Heater can be crafted again.** Their recipes were locked and nothing in the game unlocked them. They now unlock at Electrician crafting skill 45.

## 1.0.2

- **Electric cookers no longer cook without power.** With the cooker window open, the queue kept crafting even when the cooker was unwired or switched off. It now pauses until the cooker has power, and carries on when the power comes back.
- **Fridges open without power on 3.3.** A fridge that had never been wired could not be opened, stocked, or emptied. It now works as a plain box until you power it.
- **Switch prompts make sense.** Switches said "Switch light on" no matter what they were wired to. They now say what they control, for example "Turn Working Fridge On" or "Turn Electric Cooker Off". With two different devices both are named, and with more they show a count ("Turn 3 Devices On"). An unwired switch says "Switch On" / "Switch Off". This applies to every switch, vanilla included.

## 1.0.1

Fixes for 7 Days To Die 3.3. Nothing changes on 3.2.

- **Stoves, sinks, and tanks talk to the server again on 3.3.** Electric cooker power, tank levels, and sink fill/drink/pour messages were failing with `KeyNotFoundException: NetPackageHSWorkingStuff`.
- **Wire tool on an electric cooker no longer errors on 3.3.** Holding the wire tool at a cooker showed the watt label on 3.2 but threw `MissingFieldException` on 3.3.
- **Food spoil works on 3.3.** The spoil timer, the "Spoils in X days" tooltip, the morning warning, drying rack, and compost box were failing with `MissingFieldException: ItemStack.itemValue`.
- **Fridges load on 3.3.** On 3.3 a fridge stopped world-load for the containers around it and logged `VTable setup of type TileEntityHSFridge failed`. Fridges now load, save, and keep their food on 3.3. Fridges from a 3.2 save keep their contents when the world opens on 3.3.
- If the spoil tick ever errors, the rest of the mod keeps running. Join-hold no longer stops with it.

## 1.0.0

First release.

- **Food spoils** in about 7 days (`SpoilDays`). Tooltips show the days left, and there is a morning warning for the food closest to going off. Cooked and crafted food starts fresh. Stacks keep the older timer when merged.
- **Working Fridge** — 24 slots, food lasts 4x longer while powered. Store perishables / take spoiled buttons.
- **Working Cooler** — 12 slots, food lasts 2x longer, no power.
- **Drying rack** turns raw meat into dried meat. **Compost box** turns spoiled food into nitrate.
- **Plumbing** — pipes, intake, tank, sink, rain catch, and powered water heater. Fill all, pour all, and drink from the tap. Vanilla kitchen sinks and metal pipes count. Tanks take the dew collector mods (filter, gatherer, tarp).
- **Electric cookers** (old, modern, wall) — campfire recipes and tools, no wood. Wire them to power, 10W.
- Picking up a fridge keeps its food, and picking up a tank keeps its water.
- **Connect to IP** button on the main menu.
- **Hide News** — removes the News button, corner news, and news splash.
- **Join hold** — players joining a dedicated server that is still starting see "Server is Starting up please wait" instead of being kicked. Esc cancels.
- Every feature has an on/off switch in `HSWorkingStuffSettings.json`.
