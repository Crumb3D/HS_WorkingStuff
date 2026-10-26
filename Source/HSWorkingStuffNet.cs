using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Audio;
using HarmonyLib;
using UnityEngine;

public static class HSWorkingStuffNet
{
    public const byte SinkFill = 1;
    public const byte SinkFillAll = 2;
    public const byte SinkDrink = 3;
    public const byte TankPour = 4;
    public const byte TankPourAll = 5;
    public const byte TankFill = 6;
    public const byte TankFillAll = 7;
    public const byte TankSync = 8;
    public const byte StovePower = 9;
    public const byte RainInstall = 10;
    public const byte RainRemove = 11;
    public const byte SinkFillHot = 12;
    public const byte SinkFillAllHot = 13;
    public const byte SinkDrinkHot = 14;
    public const byte BoilerHeating = 15;
    public const byte BoilerSync = 16;
    public const byte RadiatorSync = 17;
    public const byte PipeLay = 18;
    public const byte PipeRemove = 19;
    public const byte PipeCell = 20;
    public const byte PipeHello = 21;
    public const byte Wash = 22;
    public const byte Laundry = 23;
    public const byte SinkWash = 24;
    public const byte CircuitSync = 25;
    public const byte CircuitFit = 26;
    public const byte CircuitRemove = 27;
    public const byte CircuitSwitch = 28;
    public const byte CircuitName = 29;
    public const byte CircuitAsk = 30;
    public const byte Toilet = 31;
    public const byte Salvage = 32;
    public const byte ShareSync = 33;
    public const byte LightLink = 34;
    public const byte PipeRemoveStretch = 35;

    public static bool IsAuthority
    {
        get
        {
            try
            {
                if (GameManager.IsDedicatedServer)
                    return true;
                var cm = ConnectionManager.Instance;
                if (cm != null)
                    return cm.IsServer;
            }
            catch { }
            return true;
        }
    }

    static Type pkgType;

    static Type PackageType32()
    {
        return typeof(NetPackageHSWorkingStuff);
    }

    public static void RegisterPackage()
    {
        try
        {
            Type t = HSGameVersion.Is33
                ? HSGameApi.NetPackageType33("NetPackageHSWorkingStuff", typeof(NetPackageHSWorkingStuffCore))
                : PackageType32();
            pkgType = t;
            var f = typeof(NetPackageManager).GetField("knownPackageTypes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (f == null)
                return;
            var dict = f.GetValue(null) as IDictionary;
            if (dict == null)
                return;
            var args = f.FieldType.GetGenericArguments();
            // The game's own scan already registers the compiled class under this name.
            // On 3.3 we send the emitted type, so the name must point at it or
            // GetPackageId throws KeyNotFoundException.
            if (args != null && args.Length >= 1 && args[0] == typeof(Type))
            {
                if (!dict.Contains(t))
                    dict[t] = t.Name;
            }
            else
                dict[t.Name] = t;
        }
        catch (Exception e)
        {
            Log.Error("[HSWorkingStuff] Net package register failed. " + e);
        }
    }

    static NetPackageHSWorkingStuffCore Pkg()
    {
        if (pkgType == null)
            RegisterPackage();
        return (NetPackageHSWorkingStuffCore)HSGameApi.GetNetPackage(pkgType);
    }

    static void ToServer(NetPackage pkg)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null)
            return;
        cm.SendToClientsOrServer(pkg);
    }

    static void ToClients(NetPackage pkg)
    {
        var cm = ConnectionManager.Instance;
        if (cm == null || !cm.IsServer)
            return;
        cm.SendPackage(pkg);
    }

    public static void SendWater(byte kind, Vector3i pos, EntityPlayer player)
    {
        SendWater(kind, pos, player, 0);
    }

    public static void SendWater(byte kind, Vector3i pos, EntityPlayer player, int arg)
    {
        if (player == null)
            return;
        if (IsAuthority)
        {
            Handle(kind, pos, player, arg);
            return;
        }
        ToServer(Pkg().OfWater(kind, player.entityId, pos, arg));
    }

    public static void SendCircuit(byte kind, Vector3i panel, EntityPlayer player, int arg, string text)
    {
        if (player == null)
            return;
        if (IsAuthority)
        {
            HSCircuits.Handle(kind, panel, player, arg, text);
            return;
        }
        ToServer(Pkg().OfText(kind, player.entityId, panel, arg, text));
    }

    public static void BroadcastCircuit(Vector3i panel, int packed, string text)
    {
        if (!IsAuthority)
            return;
        ToClients(Pkg().OfText(CircuitSync, -1, panel, packed, text));
    }

    public static void SendLight(Vector3i sw, Vector3i light, EntityPlayer player)
    {
        string text = HSLights.Text(light);
        if (IsAuthority)
        {
            HSLights.Link(sw, text);
            return;
        }
        if (player == null)
            return;
        ToServer(Pkg().OfText(LightLink, player.entityId, sw, 0, text));
    }

    // Take up only the pipe stretch between two ends (fixtures or laid pipe cells).
    public static void SendPipeStretch(Vector3i a, bool aPipe, Vector3i b, bool bPipe, EntityPlayer player)
    {
        if (player == null)
            return;
        string text = b.x + "," + b.y + "," + b.z + "," + (bPipe ? "1" : "0");
        int arg = aPipe ? 1 : 0;
        if (IsAuthority)
        {
            HSPipeTool.HandleStretchRemove(a, aPipe, b, bPipe, player);
            return;
        }
        ToServer(Pkg().OfText(PipeRemoveStretch, player.entityId, a, arg, text));
    }

    public static void BroadcastShare(Vector3i pos, string text)
    {
        if (!IsAuthority)
            return;
        ToClients(Pkg().OfText(ShareSync, -1, pos, 0, text));
    }

    public static void SendShareTo(int entityId, Vector3i pos, string text)
    {
        if (!IsAuthority)
            return;
        var cm = ConnectionManager.Instance;
        if (cm == null || !cm.IsServer)
            return;
        cm.SendPackage(Pkg().OfText(ShareSync, -1, pos, 0, text), true, entityId);
    }

    public static void SendCircuitTo(int entityId, Vector3i panel, int packed, string text)
    {
        if (!IsAuthority)
            return;
        var cm = ConnectionManager.Instance;
        if (cm == null || !cm.IsServer)
            return;
        cm.SendPackage(Pkg().OfText(CircuitSync, -1, panel, packed, text), true, entityId);
    }

    public static void BroadcastTank(Vector3i pos)
    {
        if (!IsAuthority)
            return;
        var t = HSWater.GetTank(pos);
        ToClients(Pkg().OfTank(pos, t.Units, (t.Quality & 0xF) | (t.Mods << 4)));
    }

    // After a draw or pour, sync every tank in the bank (cascade moves water between them).
    public static void BroadcastTankBank(World world, Vector3i tankPos)
    {
        if (!IsAuthority)
            return;
        List<Vector3i> bank = HSWater.TankBank(world, tankPos);
        for (int i = 0; i < bank.Count; i++)
            BroadcastTank(bank[i]);
        if (bank.Count == 0)
            BroadcastTank(tankPos);
    }

    public static void Tell(EntityPlayer player, string text)
    {
        var local = player as EntityPlayerLocal;
        if (local != null)
            GameManager.ShowTooltip(local, text);
        else
            GameManager.ShowTooltipMP(player, text);
    }

    public static void Handle(byte kind, Vector3i pos, EntityPlayer player)
    {
        Handle(kind, pos, player, 0);
    }

    public static void Handle(byte kind, Vector3i pos, EntityPlayer player, int arg)
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null || player == null)
            return;
        if (kind == Laundry)
        {
            HandleLaundry(world, pos, player);
            return;
        }
        if (kind == Toilet)
        {
            HSNeeds.Handle(world, pos, player, arg);
            return;
        }
        if (kind == Salvage)
        {
            HSSalvage.Convert(world, pos, player);
            return;
        }
        if (!HSWorkingStuffSettings.EnablePlumbing)
            return;
        bool ok = false;
        bool sinkHot = kind == SinkFillHot || kind == SinkFillAllHot || kind == SinkDrinkHot;
        if (kind == SinkFill || kind == SinkFillAll || kind == SinkDrink || sinkHot)
        {
            HSWater.Taps taps = HSWater.FindTaps(world, pos);
            HSWater.Source src = sinkHot ? taps.Hot : taps.Cold;
            if (!src.Ok)
            {
                Tell(player, sinkHot ? HSWater.HotReason(taps) : HSWater.ColdReason(taps));
                return;
            }
            if (sinkHot)
                HSBoiler.HotDraw(taps.HeaterPos);
            else if (world.GetBlock(pos).Block is BlockHSWaterCooler)
                src.Quality = HSWater.QualityBoiled;
            if (kind == SinkDrink || kind == SinkDrinkHot)
            {
                HSWater.Drink(player, src);
                ok = true;
            }
            else
                ok = HSWater.FillPlayerContainers(player, src, kind == SinkFillAll || kind == SinkFillAllHot);
            if (ok && !src.Infinite)
                BroadcastTankBank(world, src.TankPos);
        }
        else if (kind == SinkWash)
        {
            HSWater.Taps taps = HSWater.FindTaps(world, pos);
            HSWater.Source src = taps.Hot;
            bool hot = src.Ok && HSWater.TakeFromSource(ref src, 1);
            if (!hot)
            {
                src = taps.Cold;
                if (!src.Ok || !HSWater.TakeFromSource(ref src, 1))
                {
                    Tell(player, HSWater.ColdReason(taps));
                    return;
                }
            }
            if (hot)
                HSBoiler.HotDraw(taps.HeaterPos);
            if (!src.Infinite)
                BroadcastTankBank(world, src.TankPos);
            if (player.Buffs != null)
                player.Buffs.AddBuff(hot ? HSLaundry.SinkWashBuff : HSLaundry.SinkWashColdBuff);
            return;
        }
        else if (kind == Wash)
        {
            var shower = world.GetBlock(pos).Block as BlockHSShower;
            if (shower == null)
                return;
            HSWater.Taps taps = HSWater.FindTaps(world, pos);
            int jars = shower.Jars;
            HSWater.Source src = taps.Hot;
            bool hot = src.Ok && HSWater.TakeFromSource(ref src, jars);
            if (!hot)
            {
                src = taps.Cold;
                if (!src.Ok || !HSWater.TakeFromSource(ref src, jars))
                {
                    Tell(player, HSLoc.Format("hsWashShort", "Not enough water: a wash needs {0} jars. Hot water cleans more", jars));
                    return;
                }
            }
            if (hot)
                HSBoiler.HotDraw(taps.HeaterPos);
            if (!src.Infinite)
                BroadcastTankBank(world, src.TankPos);
            if (hot)
                HSWash.Apply(player, shower.IsBath);
            else
                HSWash.ApplyCold(player, shower.IsBath);
            Tell(player, hot
                ? (shower.IsBath
                    ? HSLoc.Get("hsWashBath", "Bathed: all of your body smell is gone, and nothing smells on you for 20 minutes")
                    : HSLoc.Get("hsWashShower", "Showered: nearly all of your body smell is gone, and nothing smells on you for 10 minutes"))
                : (shower.IsBath
                    ? HSLoc.Get("hsWashColdBath", "Cold bath: about half your body smell is gone, and nothing smells on you for 10 minutes")
                    : HSLoc.Get("hsWashColdShower", "Cold shower: about half your body smell is gone, and nothing smells on you for 5 minutes")));
            return;
        }
        else if (kind == RainInstall || kind == RainRemove)
        {
            var t = HSWater.GetTank(pos);
            int mods = kind == RainInstall ? (t.Mods | arg) : (t.Mods & ~arg);
            HSWater.SetMods(pos, mods & 7);
            BroadcastTank(pos);
            if (kind == RainInstall && (arg & HSWater.ModFilter) != 0)
            {
                var changed = new System.Collections.Generic.List<Vector3i>();
                int drained = HSWater.DrainMurky(world, pos, changed);
                for (int i = 0; i < changed.Count; i++)
                    BroadcastTank(changed[i]);
                if (drained > 0)
                    Tell(player, HSLoc.Format("hsRainDrained", "Filter in: drained {0} jars of murky water so the clean water stays clean", drained));
            }
            return;
        }
        else if (kind == TankPour || kind == TankPourAll)
        {
            int poured = HSWater.PourIntoTank(player, pos, kind == TankPourAll);
            ok = poured > 0;
            if (ok)
            {
                BroadcastTankBank(world, pos);
                int pool = HSWater.PoolUnits(world, pos);
                int bank = HSWater.TankBank(world, pos).Count;
                int cap = bank > 1 ? bank * HSWorkingStuffSettings.TankCapacity : HSWorkingStuffSettings.TankCapacity;
                Tell(player, HSLoc.Format("hsTankPoured", "Poured {0} in. The tank holds {1} of {2}",
                    poured, pool, cap));
            }
        }
        else if (kind == TankFill || kind == TankFillAll)
        {
            int pool = HSWater.PoolUnits(world, pos);
            int q = HSWater.GetTank(pos).Quality;
            if (HSWater.GetTank(pos).Units <= 0 && pool > 0)
            {
                List<Vector3i> bank = HSWater.TankBank(world, pos);
                for (int i = 0; i < bank.Count; i++)
                {
                    var bt = HSWater.GetTank(bank[i]);
                    if (bt.Units > 0)
                    {
                        q = bt.Quality;
                        break;
                    }
                }
            }
            var src = new HSWater.Source
            {
                Ok = pool > 0,
                Infinite = false,
                TankPos = pos,
                Quality = q
            };
            ok = HSWater.FillPlayerContainers(player, src, kind == TankFillAll);
            if (ok)
                BroadcastTankBank(world, pos);
        }
        else if (kind == BoilerHeating)
        {
            BlockValue bv = world.GetBlock(pos);
            if (bv.Block == null || !HSBoiler.IsBoiler(bv.Block))
                return;
            HSBoiler.SetHeating(pos, arg != 0);
            Tell(player, arg != 0
                ? HSLoc.Get("hsBoilerToldOn", "Central heating on")
                : HSLoc.Get("hsBoilerToldOff", "Central heating off"));
            return;
        }
        else if (kind == PipeLay)
        {
            HSPipeNet.Lay(pos, arg);
            return;
        }
        else if (kind == PipeRemove)
        {
            int[] shapes = new int[3];
            int n = HSPipeNet.RemoveRun(pos, shapes);
            if (n <= 0)
                return;
            for (int s = 0; s < shapes.Length && s < HSPipeTool.PipeItems.Length; s++)
            {
                if (shapes[s] <= 0)
                    continue;
                ItemValue iv = ItemClass.GetItem(HSPipeTool.PipeItems[s]);
                if (iv != null && !iv.IsEmpty())
                    HSWater.GiveOrDrop(player, new ItemStack(iv, shapes[s]));
            }
            Tell(player, HSLoc.Format("hsPipeTook", "Took up {0} straight, {1} corners and {2} joints", shapes[0], shapes[1], shapes[2]));
            HSInv.NotifyPlayer(player);
            return;
        }
        else if (kind == PipeHello)
        {
            HSPipeNet.SendAll(player.entityId);
            return;
        }
        else if (kind == TankSync)
            return;
        if (ok)
            HSInv.NotifyPlayer(player);
    }

    // Washer: power and a cold tap, uses WasherJars, then hums for WasherSeconds. Dryer: power only, same wait.
    static void HandleLaundry(World world, Vector3i pos, EntityPlayer player)
    {
        BlockValue bv = world.GetBlock(pos);
        var block = bv.Block as BlockHSLaundry;
        if (block == null)
            return;
        pos = HSWater.Parent(pos, bv);
        if (block.IsDryer)
            return;
        HSLaundry.TakeWashWater(world, pos, player);
    }

    public static void HandleSync(Vector3i pos, int units, int packed)
    {
        HSWater.SetTank(pos, new HSWater.Tank { Units = units, Quality = packed & 0xF, Mods = (packed >> 4) & 7 });
    }

    public static void BroadcastStovePower(Vector3i pos, bool on)
    {
        if (!IsAuthority)
            return;
        ToClients(Pkg().OfStove(pos, on));
    }

    public static void HandleStovePower(Vector3i pos, bool on)
    {
        HSStovePower.RememberPower(pos, on);
    }

    public static void BroadcastBoiler(Vector3i pos, HSBoiler.Info info)
    {
        if (!IsAuthority)
            return;
        int flags = (info.Heating ? 1 : 0) | (info.Firing ? 2 : 0);
        int counts = (info.Radiators & 0xFFFF) | (Math.Min(info.Filled, 0x7FFF) << 16);
        ToClients(Pkg().OfSync(BoilerSync, pos, flags, counts));
    }

    public static void BroadcastRadiator(Vector3i pos, bool warm)
    {
        if (!IsAuthority)
            return;
        ToClients(Pkg().OfSync(RadiatorSync, pos, warm ? 1 : 0, 0));
    }

    public static void BroadcastPipe(Vector3i pos, int mask)
    {
        if (!IsAuthority)
            return;
        ToClients(Pkg().OfSync(PipeCell, pos, mask, 0));
    }

    public static void SendPipeTo(int entityId, Vector3i pos, int mask)
    {
        if (!IsAuthority)
            return;
        var cm = ConnectionManager.Instance;
        if (cm == null || !cm.IsServer)
            return;
        cm.SendPackage(Pkg().OfSync(PipeCell, pos, mask, 0), true, entityId);
    }

    public static void HandleBoilerSync(Vector3i pos, int flags, int counts)
    {
        HSBoiler.Remember(pos, new HSBoiler.Info
        {
            Heating = (flags & 1) != 0,
            Firing = (flags & 2) != 0,
            Radiators = counts & 0xFFFF,
            Filled = (counts >> 16) & 0x7FFF
        });
    }
}

public abstract class NetPackageHSWorkingStuffCore : NetPackage
{
    protected byte kind;
    protected int entityId;
    protected int x;
    protected int y;
    protected int z;
    protected int units;
    protected int quality;
    protected string text;

    public override NetPackageDirection PackageDirection
    {
        get { return NetPackageDirection.Both; }
    }

    public NetPackageHSWorkingStuffCore OfWater(byte k, int eid, Vector3i pos, int arg)
    {
        kind = k;
        entityId = eid;
        x = pos.x;
        y = pos.y;
        z = pos.z;
        units = arg;
        text = null;
        return this;
    }

    public NetPackageHSWorkingStuffCore OfTank(Vector3i pos, int u, int q)
    {
        kind = HSWorkingStuffNet.TankSync;
        x = pos.x;
        y = pos.y;
        z = pos.z;
        units = u;
        quality = q;
        text = null;
        return this;
    }

    public NetPackageHSWorkingStuffCore OfStove(Vector3i pos, bool on)
    {
        kind = HSWorkingStuffNet.StovePower;
        x = pos.x;
        y = pos.y;
        z = pos.z;
        units = on ? 1 : 0;
        text = null;
        return this;
    }

    public NetPackageHSWorkingStuffCore OfSync(byte k, Vector3i pos, int u, int q)
    {
        kind = k;
        x = pos.x;
        y = pos.y;
        z = pos.z;
        units = u;
        quality = q;
        text = null;
        return this;
    }

    public NetPackageHSWorkingStuffCore OfText(byte k, int eid, Vector3i pos, int u, string t)
    {
        kind = k;
        entityId = eid;
        x = pos.x;
        y = pos.y;
        z = pos.z;
        units = u;
        quality = 0;
        text = t;
        return this;
    }

    public override void read(PooledBinaryReader br)
    {
        kind = br.ReadByte();
        entityId = br.ReadInt32();
        x = br.ReadInt32();
        y = br.ReadInt32();
        z = br.ReadInt32();
        units = br.ReadInt32();
        quality = br.ReadInt32();
        text = br.ReadString();
    }

    public override void write(PooledBinaryWriter bw)
    {
        base.write(bw);
        bw.Write(kind);
        bw.Write(entityId);
        bw.Write(x);
        bw.Write(y);
        bw.Write(z);
        bw.Write(units);
        bw.Write(quality);
        bw.Write(text ?? "");
    }

    public override void ProcessPackage(World world, GameManager callbacks)
    {
        try
        {
            Vector3i pos = new Vector3i(x, y, z);
            if (kind == HSWorkingStuffNet.TankSync)
            {
                HSWorkingStuffNet.HandleSync(pos, units, quality);
                return;
            }
            if (kind == HSWorkingStuffNet.StovePower)
            {
                HSWorkingStuffNet.HandleStovePower(pos, units != 0);
                return;
            }
            if (kind == HSWorkingStuffNet.BoilerSync)
            {
                HSWorkingStuffNet.HandleBoilerSync(pos, units, quality);
                return;
            }
            if (kind == HSWorkingStuffNet.RadiatorSync)
            {
                HSBoiler.RememberRadiator(pos, units != 0);
                return;
            }
            if (kind == HSWorkingStuffNet.PipeCell)
            {
                if (!HSWorkingStuffNet.IsAuthority)
                    HSPipeNet.SetCell(pos, units);
                return;
            }
            if (kind == HSWorkingStuffNet.CircuitSync)
            {
                if (!HSWorkingStuffNet.IsAuthority)
                    HSCircuits.ApplySync(pos, units, text);
                return;
            }
            if (kind == HSWorkingStuffNet.ShareSync)
            {
                if (!HSWorkingStuffNet.IsAuthority)
                    HSShare.ApplySync(pos, text);
                return;
            }
            if (kind == HSWorkingStuffNet.LightLink)
            {
                if (HSWorkingStuffNet.IsAuthority)
                    HSLights.Link(pos, text);
                return;
            }
            if (kind == HSWorkingStuffNet.PipeRemoveStretch)
            {
                if (!HSWorkingStuffNet.IsAuthority)
                    return;
                EntityPlayer stretchPlayer = world != null ? world.GetEntity(entityId) as EntityPlayer : null;
                if (stretchPlayer == null && Sender != null && world != null)
                    stretchPlayer = world.GetEntity(Sender.entityId) as EntityPlayer;
                if (stretchPlayer == null || string.IsNullOrEmpty(text))
                    return;
                string[] f = text.Split(',');
                int bx, by, bz, bp;
                if (f.Length < 4 || !int.TryParse(f[0], out bx) || !int.TryParse(f[1], out by)
                    || !int.TryParse(f[2], out bz) || !int.TryParse(f[3], out bp))
                    return;
                HSPipeTool.HandleStretchRemove(pos, units != 0, new Vector3i(bx, by, bz), bp != 0, stretchPlayer);
                return;
            }
            if (world == null)
                return;
            EntityPlayer player = world.GetEntity(entityId) as EntityPlayer;
            if (player == null && Sender != null)
                player = world.GetEntity(Sender.entityId) as EntityPlayer;
            if (player == null)
                return;
            if (!HSWorkingStuffNet.IsAuthority)
                return;
            if (HSCircuits.IsRequest(kind))
                HSCircuits.Handle(kind, pos, player, units, text);
            else
                HSWorkingStuffNet.Handle(kind, pos, player, units);
        }
        catch (Exception e)
        {
            Log.Error("[HSWorkingStuff] Net package failed. " + e);
        }
    }
}

public sealed class NetPackageHSWorkingStuff : NetPackageHSWorkingStuffCore
{
    public override int GetLength()
    {
        return 30 + (text != null ? System.Text.Encoding.UTF8.GetByteCount(text) + 4 : 0);
    }
}

[HarmonyPatch(typeof(NetPackageManager), "SetupBaseMapping")]
public static class HSWorkingStuffNetRegister
{
    static void Postfix()
    {
        HSWorkingStuffNet.RegisterPackage();
    }
}
