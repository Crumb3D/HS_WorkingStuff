using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// A plumbed toilet (pee and poo) and a wall urinal (pee only). meta2 holds the bowl and the cistern:
// bit 0 dirty, bits 1-3 cistern jars. A urinal also uses bit 3 as "one pee already banked" and only 2 cistern bits,
// so two pees share one jar. Poo is refused while the bowl is dirty. A tree takes a pee and nothing else.
public class BlockHSToilet : Block
{
    public bool Urinal;

    public override void Init()
    {
        base.Init();
        Properties.ParseBool("HSUrinal", ref Urinal);
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        string name = _blockValue.Block.GetLocalizedBlockName();
        World world = _world as World;
        if (world == null)
            return name;
        string status = HSNeeds.Status(world, _blockPos, _blockValue, Urinal, _entityFocusing as EntityPlayer);
        return string.IsNullOrEmpty(status) ? name : name + "\n" + status;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return HSNeeds.Commands(_world as World, _blockPos, _blockValue, Urinal, _entityFocusing as EntityPlayerLocal);
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        return HSNeeds.Run(_commandName, _world as World, _blockPos, _blockValue, Urinal, _player);
    }
}

public static class HSNeeds
{
    public const string BladderCVar = "hsBladder";
    public const string BowelCVar = "hsBowels";
    public const string PeeBuff = "buffHSNeedPee";
    public const string PooBuff = "buffHSNeedPoo";
    public const int MinUse = 10;

    const int PeeToilet = 1;
    const int PeeUrinal = 2;
    const int Poo = 3;
    const int Flush = 4;
    const int Fill = 5;

    static readonly HashSet<string> off = new HashSet<string> { "hsneedpee", "hsneedpoo", "hsbowlfull", "hsflushnone" };
    static readonly FastTags<TagGroup.Global> TreeTag = FastTags<TagGroup.Global>.Parse("TreeTrunk");

    static ulong lastTick;
    static float nextTick;
    static bool urgedPee, urgedPoo;
    static float nextScan, cachedSmell;

    public static bool IsOff(string command)
    {
        return command != null && off.Contains(command);
    }

    public static void Clear()
    {
        lastTick = 0UL;
        nextTick = 0f;
        urgedPee = urgedPoo = false;
        nextScan = 0f;
        cachedSmell = 0f;
    }

    public static bool Dirty(BlockValue bv)
    {
        return (bv.meta2 & 1) != 0;
    }

    public static int Cistern(BlockValue bv)
    {
        return (bv.meta2 >> 1) & 7;
    }

    static bool Half(BlockValue bv)
    {
        return (bv.meta2 & 8) != 0;
    }

    static int Cap(bool urinal)
    {
        int cap = Mathf.Clamp(HSWorkingStuffSettings.CisternJars, 1, urinal ? 3 : 7);
        return cap;
    }

    static byte Pack(bool urinal, bool dirty, int cistern, bool half)
    {
        cistern = Mathf.Clamp(cistern, 0, urinal ? 3 : 7);
        int bits = (dirty ? 1 : 0) | (cistern << 1);
        if (urinal && half)
            bits |= 8;
        return (byte)bits;
    }

    static float Get(EntityPlayer p, string cvar)
    {
        if (p == null || p.Buffs == null)
            return 0f;
        return Mathf.Clamp(p.Buffs.GetCustomVar(cvar), 0f, 100f);
    }

    public static bool NeedsPee(EntityPlayer p)
    {
        return HSWorkingStuffSettings.EnableNeeds && Get(p, BladderCVar) >= MinUse;
    }

    public static bool NeedsPoo(EntityPlayer p)
    {
        return HSWorkingStuffSettings.EnableNeeds && Get(p, BowelCVar) >= MinUse;
    }

    public static bool IsTree(Block block)
    {
        return block != null && block.HasAnyFastTags(TreeTag);
    }

    public static void FromDrink(EntityPlayerLocal p)
    {
        Add(p, BladderCVar, HSWorkingStuffSettings.DrinkBladder, true);
    }

    public static void FromFood(EntityPlayerLocal p)
    {
        Add(p, BowelCVar, HSWorkingStuffSettings.EatBowels, false);
    }

    static void Add(EntityPlayerLocal p, string cvar, float amount, bool pee)
    {
        if (!HSWorkingStuffSettings.EnableNeeds || p == null || p.Buffs == null || amount <= 0f)
            return;
        float v = Get(p, cvar) + amount;
        if (v >= 100f)
        {
            if (pee)
                AccidentPee(p);
            else
                AccidentPoo(p);
            return;
        }
        p.Buffs.SetCustomVar(cvar, v);
    }

    public static void ClientTick()
    {
        if (!HSWorkingStuffSettings.EnableNeeds)
            return;
        float t = Time.time;
        if (t < nextTick)
            return;
        nextTick = t + 2f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        EntityPlayerLocal p = world != null ? world.GetPrimaryPlayer() : null;
        if (p == null || p.Buffs == null || p.IsDead())
        {
            lastTick = 0UL;
            urgedPee = urgedPoo = false;
            return;
        }
        ulong now = world.worldTime;
        float hours = 0f;
        if (lastTick != 0UL && now > lastTick && now - lastTick < 2000UL)
            hours = (now - lastTick) / 1000f;
        lastTick = now;
        try
        {
            if (hours > 0f)
            {
                float bladder = Get(p, BladderCVar) + hours * (100f / Mathf.Max(0.25f, HSWorkingStuffSettings.BladderHours));
                float bowels = Get(p, BowelCVar) + hours * (100f / Mathf.Max(0.25f, HSWorkingStuffSettings.BowelHours));
                if (bladder >= 100f)
                    AccidentPee(p);
                else
                    p.Buffs.SetCustomVar(BladderCVar, bladder);
                if (bowels >= 100f)
                    AccidentPoo(p);
                else
                    p.Buffs.SetCustomVar(BowelCVar, bowels);
            }
            float urge = HSWorkingStuffSettings.UrgeAt;
            bool needPee = Get(p, BladderCVar) >= urge;
            bool needPoo = Get(p, BowelCVar) >= urge;
            if (needPee && !urgedPee)
                GameManager.ShowTooltip(p, HSLoc.Get("hsUrgePee", "You need a pee. A toilet, a urinal or a tree will do"));
            if (needPoo && !urgedPoo)
                GameManager.ShowTooltip(p, HSLoc.Get("hsUrgePoo", "You need the toilet. Only a toilet will do"));
            urgedPee = needPee;
            urgedPoo = needPoo;
            Toggle(p, PeeBuff, needPee);
            Toggle(p, PooBuff, needPoo);
        }
        catch { }
    }

    static void Toggle(EntityPlayerLocal p, string buff, bool on)
    {
        bool has = p.Buffs.HasBuff(buff);
        if (on && !has)
            p.Buffs.AddBuff(buff);
        else if (!on && has)
            p.Buffs.RemoveBuff(buff);
    }

    static void Relieve(EntityPlayerLocal p, string cvar, string buff)
    {
        if (p == null || p.Buffs == null)
            return;
        p.Buffs.SetCustomVar(cvar, 0f);
        if (p.Buffs.HasBuff(buff))
            p.Buffs.RemoveBuff(buff);
        if (cvar == BladderCVar)
            urgedPee = false;
        else
            urgedPoo = false;
    }

    static void AccidentPee(EntityPlayerLocal p)
    {
        Relieve(p, BladderCVar, PeeBuff);
        HSLaundry.SoilWorn(p, true);
        AddSmell(p, 8f);
        PlayOn(p, "hsStream");
        GameManager.ShowTooltip(p, HSLoc.Get("hsAccidentPee", "You wet yourself. Your clothes are soaked and you smell"));
    }

    static void AccidentPoo(EntityPlayerLocal p)
    {
        Relieve(p, BowelCVar, PooBuff);
        HSLaundry.SoilWorn(p, false);
        AddSmell(p, 24f);
        PlayOn(p, "hsSoilDry");
        GameManager.ShowTooltip(p, HSLoc.Get("hsAccidentPoo", "You didn't make it. Your clothes are filthy and you smell"));
    }

    static void AddSmell(EntityPlayerLocal p, float hours)
    {
        if (p == null || p.Buffs == null || !HSWorkingStuffSettings.EnableBodySmell)
            return;
        p.Buffs.SetCustomVar(HSLaundry.BodyCVar, p.Buffs.GetCustomVar(HSLaundry.BodyCVar) + hours);
    }

    public static float DirtyNearby(EntityPlayer p)
    {
        if (!HSWorkingStuffSettings.EnableNeeds || p == null)
            return 0f;
        if (Time.time < nextScan)
            return cachedSmell;
        nextScan = Time.time + 1f;
        cachedSmell = 0f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return 0f;
        Vector3 at = p.position;
        int x0 = Mathf.FloorToInt(at.x);
        int y0 = Mathf.FloorToInt(at.y);
        int z0 = Mathf.FloorToInt(at.z);
        for (int y = y0 - 1; y <= y0 + 2 && cachedSmell <= 0f; y++)
            for (int x = x0 - 4; x <= x0 + 4 && cachedSmell <= 0f; x++)
                for (int z = z0 - 4; z <= z0 + 4; z++)
                {
                    BlockValue bv = world.GetBlock(x, y, z);
                    if (!Dirty(bv) || bv.Block == null)
                        continue;
                    string n = bv.Block.GetBlockName();
                    if (n == "hsToilet" || n == "hsUrinal")
                    {
                        cachedSmell = HSWorkingStuffSettings.DirtyToiletSmell;
                        break;
                    }
                }
        return cachedSmell;
    }

    public static string Status(World world, Vector3i pos, BlockValue bv, bool urinal, EntityPlayer who)
    {
        var lines = new List<string>();
        if (HSWorkingStuffSettings.EnableNeeds && who != null)
        {
            lines.Add(HSLoc.Format("hsBladderLine", "Bladder {0}%", Mathf.RoundToInt(Get(who, BladderCVar))));
            lines.Add(HSLoc.Format("hsBowelLine", "Bowels {0}%", Mathf.RoundToInt(Get(who, BowelCVar))));
        }
        if (Dirty(bv))
            lines.Add(HSLoc.Get(urinal ? "hsUrinalDirty" : "hsBowlDirty", urinal
                ? "The urinal hasn't flushed. It smells until it does"
                : "The bowl is dirty and it smells. Flush it before you can go again"));
        int cistern = Cistern(bv) & (urinal ? 3 : 7);
        if (cistern > 0)
            lines.Add(HSLoc.Format("hsCisternJars", "Cistern: {0} jars", cistern));
        if (HasPipe(world, pos, FlushNeed()))
            lines.Add(HSLoc.Get("hsToiletPiped", "Piped: a flush takes water from the tank"));
        else if (cistern <= 0)
            lines.Add(HSLoc.Get("hsToiletDry", "No water. Pipe it, or pour a jar or bucket into the cistern"));
        return string.Join("\n", lines.ToArray());
    }

    public static BlockActivationCommand[] Commands(World world, Vector3i pos, BlockValue bv, bool urinal, EntityPlayerLocal who)
    {
        var list = new List<BlockActivationCommand>();
        if (HSWorkingStuffSettings.EnableNeeds)
        {
            list.Add(new BlockActivationCommand(NeedsPee(who) ? "hspee" : "hsneedpee", "water", true));
            if (!urinal)
            {
                if (Dirty(bv))
                    list.Add(new BlockActivationCommand("hsbowlfull", "water", true));
                else
                    list.Add(new BlockActivationCommand(NeedsPoo(who) ? "hspoo" : "hsneedpoo", "water", true));
            }
        }
        int cistern = Cistern(bv) & (urinal ? 3 : 7);
        bool canFlush = Dirty(bv) || cistern > 0 || HasPipe(world, pos, FlushNeed());
        list.Add(new BlockActivationCommand(canFlush ? "hsflush" : "hsflushnone", "water", true));
        int held = HSWater.HoldingKind(who);
        if (held == 3 || held == 4)
            list.Add(new BlockActivationCommand("hsfill", "water", true));
        return list.ToArray();
    }

    public static bool Run(string command, World world, Vector3i pos, BlockValue bv, bool urinal, EntityPlayerLocal player)
    {
        if (IsOff(command))
        {
            Tell(player, OffText(command, urinal));
            return true;
        }
        int arg;
        switch (command)
        {
            case "hspee":
                if (!NeedsPee(player))
                {
                    Tell(player, HSLoc.Get("hsNeedNone", "You don't need to go"));
                    return true;
                }
                PlayAt(pos, "hsStream");
                bool flushNow = !urinal || HSWorkingStuffSettings.UrinalUsesPerJar <= 1 || Half(bv);
                if (flushNow && HasFlushWater(world, pos, bv, FlushNeed()))
                    PlayAt(pos, "hsFlush");
                Relieve(player, BladderCVar, PeeBuff);
                Tell(player, HSLoc.Get(urinal ? "hsUrinalUsed" : "hsPeed", urinal ? "Used the urinal" : "That's better"));
                arg = urinal ? PeeUrinal : PeeToilet;
                break;
            case "hspoo":
                if (urinal)
                    return false;
                if (Dirty(bv))
                {
                    Tell(player, HSLoc.Get("hsBowlFull", "The bowl is dirty. Flush it before you can go again"));
                    return true;
                }
                if (!NeedsPoo(player))
                {
                    Tell(player, HSLoc.Get("hsNeedNone", "You don't need to go"));
                    return true;
                }
                PlayAt(pos, "hsSoil");
                if (HasFlushWater(world, pos, bv, FlushNeed()))
                    PlayAt(pos, "hsFlush");
                else
                    Tell(player, HSLoc.Get("hsNoFlush", "No water to flush. The bowl stays dirty until you flush it"));
                Relieve(player, BowelCVar, PooBuff);
                arg = Poo;
                break;
            case "hsflush":
                if (!HasFlushWater(world, pos, bv, FlushNeed()))
                {
                    Tell(player, HSLoc.Get("hsNoFlush", "No water to flush. The bowl stays dirty until you flush it"));
                    return true;
                }
                PlayAt(pos, "hsFlush");
                arg = Flush;
                break;
            case "hsfill":
                PlayAt(pos, "bucketfill_water");
                arg = Fill;
                break;
            default:
                return false;
        }
        HSWorkingStuffNet.SendWater(HSWorkingStuffNet.Toilet, pos, player, arg);
        return true;
    }

    static string OffText(string command, bool urinal)
    {
        switch (command)
        {
            case "hsneedpee": return HSLoc.Get("hsNeedNone", "You don't need to go");
            case "hsneedpoo": return HSLoc.Get("hsNeedNonePoo", "You don't need the toilet");
            case "hsbowlfull": return HSLoc.Get("hsBowlFull", "The bowl is dirty. Flush it before you can go again");
            default: return HSLoc.Get("hsFlushNone", urinal ? "Nothing to flush" : "Nothing to flush");
        }
    }

    public static void TreePee(EntityPlayerLocal player, Vector3i pos)
    {
        if (!NeedsPee(player))
        {
            Tell(player, HSLoc.Get("hsNeedNone", "You don't need to go"));
            return;
        }
        PlayAt(pos, "hsStream");
        Relieve(player, BladderCVar, PeeBuff);
        Tell(player, HSLoc.Get("hsTreePee", "Went against the tree"));
    }

    public static void Handle(World world, Vector3i pos, EntityPlayer player, int arg)
    {
        if (world == null || player == null)
            return;
        BlockValue bv = world.GetBlock(pos);
        var block = bv.Block as BlockHSToilet;
        if (block == null)
            return;
        bool urinal = block.Urinal;
        bool dirty = Dirty(bv);
        int cistern = Cistern(bv) & (urinal ? 3 : 7);
        bool half = urinal && Half(bv);
        int need = FlushNeed();
        if (arg == Fill)
        {
            int room = Cap(urinal) - cistern;
            if (room <= 0)
            {
                Tell(player, HSLoc.Get("hsCisternFull", "The cistern is full"));
                return;
            }
            int got;
            if (!TakeHeld(player, out got))
            {
                Tell(player, HSLoc.Get("hsCisternHold", "Hold a jar or bucket of water to fill the cistern"));
                return;
            }
            cistern += Mathf.Min(room, got);
            Write(world, pos, bv, urinal, dirty, cistern, half);
            Tell(player, HSLoc.Format("hsCisternFilled", "Cistern filled: {0} jars", cistern));
            HSInv.NotifyPlayer(player);
            return;
        }
        if (arg == Flush)
        {
            if (!TryFlush(world, pos, ref cistern, need))
            {
                Tell(player, HSLoc.Get("hsNoFlush", "No water to flush. The bowl stays dirty until you flush it"));
                return;
            }
            Write(world, pos, bv, urinal, false, cistern, false);
            Tell(player, HSLoc.Get("hsFlushed", "Flushed"));
            return;
        }
        if (arg == Poo)
        {
            if (urinal || dirty)
            {
                Tell(player, urinal
                    ? HSLoc.Get("hsUrinalPoo", "A urinal is no good for that. Use a toilet")
                    : HSLoc.Get("hsBowlFull", "The bowl is dirty. Flush it before you can go again"));
                return;
            }
            dirty = true;
            if (TryFlush(world, pos, ref cistern, need))
                dirty = false;
            else
                Tell(player, HSLoc.Get("hsNoFlush", "No water to flush. The bowl stays dirty until you flush it"));
            Write(world, pos, bv, false, dirty, cistern, false);
            return;
        }
        if (arg == PeeToilet || arg == PeeUrinal)
        {
            if (urinal)
            {
                bool share = HSWorkingStuffSettings.UrinalUsesPerJar > 1;
                if (share && !half)
                    half = true;
                else if (TryFlush(world, pos, ref cistern, need))
                {
                    half = false;
                    dirty = false;
                }
                else
                    dirty = true;
            }
            else if (TryFlush(world, pos, ref cistern, need))
                dirty = false;
            Write(world, pos, bv, urinal, dirty, cistern, half);
        }
    }

    static int FlushNeed()
    {
        return Mathf.Max(1, HSWorkingStuffSettings.FlushJars);
    }

    static bool HasPipe(World world, Vector3i pos, int jars)
    {
        if (world == null || !HSWorkingStuffSettings.EnablePlumbing)
            return false;
        HSWater.Source src = HSWater.FindTaps(world, pos).Cold;
        if (!src.Ok)
            return false;
        if (src.Infinite)
            return true;
        return HSWater.GetTank(src.TankPos).Units >= jars;
    }

    static bool HasFlushWater(World world, Vector3i pos, BlockValue bv, int jars)
    {
        int cistern = Cistern(bv);
        var block = bv.Block as BlockHSToilet;
        if (block != null && block.Urinal)
            cistern &= 3;
        return cistern >= jars || HasPipe(world, pos, jars);
    }

    // Pipe water first, then the cistern. cistern is reduced when the cistern pays.
    static bool TryFlush(World world, Vector3i pos, ref int cistern, int jars)
    {
        if (HSWorkingStuffSettings.EnablePlumbing)
        {
            HSWater.Source src = HSWater.FindTaps(world, pos).Cold;
            if (src.Ok && HSWater.TakeFromSource(ref src, jars))
            {
                if (!src.Infinite)
                    HSWorkingStuffNet.BroadcastTank(src.TankPos);
                return true;
            }
        }
        if (cistern < jars)
            return false;
        cistern -= jars;
        return true;
    }

    static bool TakeHeld(EntityPlayer player, out int jars)
    {
        jars = 0;
        int kind = HSWater.HoldingKind(player);
        if (kind != 3 && kind != 4)
            return false;
        ItemStack data = HSInv.GetHeld(player);
        ItemValue iv = HSItem.Val(data);
        if (data == null || iv == null || iv.ItemClass == null)
            return false;
        jars = kind == 4 ? Mathf.Max(1, HSWorkingStuffSettings.BucketUnits) : 1;
        HSItem.SetVal(data, ItemClass.GetItem(kind == 4 ? "bucketEmpty" : "drinkJarEmpty"));
        return true;
    }

    static void Write(World world, Vector3i pos, BlockValue bv, bool urinal, bool dirty, int cistern, bool half)
    {
        bv.meta2 = Pack(urinal, dirty, cistern, half);
        world.SetBlockRPC(pos, bv);
    }

    public static void PlayAt(Vector3i pos, string sound)
    {
        try
        {
            Audio.Manager.BroadcastPlayByLocalPlayer(pos.ToVector3() + new Vector3(0.5f, 0.4f, 0.5f), sound);
        }
        catch { }
    }

    static void PlayOn(EntityPlayerLocal p, string sound)
    {
        try
        {
            if (p != null)
                p.PlayOneShot(sound);
        }
        catch { }
    }

    static void Tell(EntityPlayer player, string text)
    {
        if (player == null || string.IsNullOrEmpty(text))
            return;
        var local = player as EntityPlayerLocal;
        if (local != null)
            GameManager.ShowTooltip(local, text);
        else
            GameManager.ShowTooltipMP(player, text);
    }
}

// A tree trunk grows a Pee command while you need one. Poo is never offered.
[HarmonyPatch(typeof(Block), "HasBlockActivationCommands")]
public static class HSPatchTreePeeHas
{
    static void Postfix(Block __instance, EntityAlive _entityFocusing, ref bool __result)
    {
        if (__result || !HSNeeds.IsTree(__instance))
            return;
        if (HSNeeds.NeedsPee(_entityFocusing as EntityPlayer))
            __result = true;
    }
}

[HarmonyPatch(typeof(Block), "GetBlockActivationCommands")]
public static class HSPatchTreePeeCmds
{
    static void Postfix(Block __instance, EntityAlive _entityFocusing, ref BlockActivationCommand[] __result)
    {
        if (!HSNeeds.IsTree(__instance) || !HSNeeds.NeedsPee(_entityFocusing as EntityPlayer))
            return;
        int n = __result != null ? __result.Length : 0;
        var all = new BlockActivationCommand[n + 1];
        for (int i = 0; i < n; i++)
            all[i] = __result[i];
        all[n] = new BlockActivationCommand("hspee", "water", true);
        __result = all;
    }
}

[HarmonyPatch(typeof(Block), "OnBlockActivated", new[] { typeof(string), typeof(WorldBase), typeof(Vector3i), typeof(BlockValue), typeof(EntityPlayerLocal) })]
public static class HSPatchTreePeeRun
{
    static bool Prefix(Block __instance, string _commandName, Vector3i _blockPos, EntityPlayerLocal _player)
    {
        if (_commandName != "hspee" || !HSNeeds.IsTree(__instance))
            return true;
        HSNeeds.TreePee(_player, _blockPos);
        return false;
    }
}

// Drinking fills the bladder. Eating fills the bowels. A sink drink is counted where the tap runs.
[HarmonyPatch(typeof(ItemActionEat), "ExecuteInstantAction")]
public static class HSPatchEatNeeds
{
    static void Postfix(EntityAlive ent, ItemStack stack, bool __result)
    {
        if (!__result)
            return;
        var p = ent as EntityPlayerLocal;
        ItemValue iv = HSItem.Val(stack);
        if (p == null || iv == null || iv.ItemClass == null)
            return;
        ItemClass ic = iv.ItemClass;
        if (ic.HasAnyTags(FastTags<TagGroup.Global>.Parse("drinks")))
            HSNeeds.FromDrink(p);
        else if (ic.HasAnyTags(FastTags<TagGroup.Global>.Parse("food")))
            HSNeeds.FromFood(p);
    }
}
