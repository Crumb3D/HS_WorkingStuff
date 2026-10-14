using System;
using System.Collections;
using System.Reflection;
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

    public static void BroadcastTank(Vector3i pos)
    {
        if (!IsAuthority)
            return;
        var t = HSWater.GetTank(pos);
        ToClients(Pkg().OfTank(pos, t.Units, (t.Quality & 0xF) | (t.Mods << 4)));
    }

    static void Tell(EntityPlayer player, string text)
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
                BroadcastTank(src.TankPos);
        }
        else if (kind == SinkWash)
        {
            HSWater.Taps taps = HSWater.FindTaps(world, pos);
            bool hot = taps.Hot.Ok;
            HSWater.Source src = hot ? taps.Hot : taps.Cold;
            if (!src.Ok)
            {
                Tell(player, HSWater.ColdReason(taps));
                return;
            }
            if (!HSWater.TakeFromSource(ref src, 1))
            {
                Tell(player, HSWater.ColdReason(taps));
                return;
            }
            if (hot)
                HSBoiler.HotDraw(taps.HeaterPos);
            if (!src.Infinite)
                BroadcastTank(src.TankPos);
            if (player.Buffs != null)
                player.Buffs.AddBuff(HSLaundry.SinkWashBuff);
            return;
        }
        else if (kind == Wash)
        {
            var shower = world.GetBlock(pos).Block as BlockHSShower;
            if (shower == null)
                return;
            HSWater.Taps taps = HSWater.FindTaps(world, pos);
            HSWater.Source src = taps.Hot;
            if (!src.Ok)
            {
                Tell(player, HSWater.HotReason(taps));
                return;
            }
            int jars = shower.Jars;
            if (!HSWater.TakeFromSource(ref src, jars))
            {
                Tell(player, HSLoc.Format("hsWashShort", "Not enough hot water: a wash needs {0} jars", jars));
                return;
            }
            HSBoiler.HotDraw(taps.HeaterPos);
            if (!src.Infinite)
                BroadcastTank(src.TankPos);
            HSWash.Apply(player, shower.IsBath);
            Tell(player, shower.IsBath
                ? HSLoc.Get("hsWashBath", "Bathed: all of your body smell is gone, and nothing smells on you for 20 minutes")
                : HSLoc.Get("hsWashShower", "Showered: nearly all of your body smell is gone, and nothing smells on you for 10 minutes"));
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
            ok = HSWater.PourIntoTank(player, pos, kind == TankPourAll);
            if (ok)
                BroadcastTank(pos);
        }
        else if (kind == TankFill || kind == TankFillAll)
        {
            var t = HSWater.GetTank(pos);
            var src = new HSWater.Source
            {
                Ok = t.Units > 0,
                Infinite = false,
                TankPos = pos,
                Quality = t.Quality
            };
            ok = HSWater.FillPlayerContainers(player, src, kind == TankFillAll);
            if (ok)
                BroadcastTank(pos);
        }
        else if (kind == BoilerHeating)
        {
            BlockValue bv = world.GetBlock(pos);
            if (bv.Block == null || !HSBoiler.IsBoiler(bv.Block.GetBlockName()))
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
            int n = HSPipeNet.RemoveRun(pos);
            if (n <= 0)
                return;
            ItemValue iv = ItemClass.GetItem("hsPipeStraight");
            if (iv != null && !iv.IsEmpty())
                HSWater.GiveOrDrop(player, new ItemStack(iv, n));
            Tell(player, HSLoc.Format("hsPipeRemoved", "Took up {0} pipes", n));
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

    // Washer: power and a cold tap, uses WasherJars. Dryer: power only.
    static void HandleLaundry(World world, Vector3i pos, EntityPlayer player)
    {
        BlockValue bv = world.GetBlock(pos);
        var block = bv.Block as BlockHSLaundry;
        if (block == null)
            return;
        pos = HSWater.Parent(pos, bv);
        bool dryer = block.IsDryer;
        IHSFridgeInv inv = HSFridgeInv.Of(world.GetTileEntity(pos));
        if (inv == null || !inv.IsPowered)
        {
            Tell(player, HSLoc.Get("hsLaundryNoPower", "No power: wire it up first"));
            return;
        }
        inv.EnsureItems();
        float nowH = HSLaundry.NowHours(world);
        if (HSLaundry.Count(inv.Items, dryer, nowH) == 0)
        {
            Tell(player, dryer
                ? HSLoc.Get("hsDryerEmpty", "Nothing wet to dry")
                : HSLoc.Get("hsWasherEmpty", "Nothing to wash: put clothes or armour in first"));
            return;
        }
        if (!dryer && HSWorkingStuffSettings.EnablePlumbing)
        {
            HSWater.Taps taps = HSWater.FindTaps(world, pos);
            HSWater.Source src = taps.Cold;
            if (!src.Ok)
            {
                Tell(player, HSWater.ColdReason(taps));
                return;
            }
            int jars = HSWorkingStuffSettings.WasherJars;
            if (!HSWater.TakeFromSource(ref src, jars))
            {
                Tell(player, HSLoc.Format("hsWasherShort", "Not enough water: a wash needs {0} jars", jars));
                return;
            }
            if (!src.Infinite)
                BroadcastTank(src.TankPos);
        }
        int n = HSLaundry.Process(inv.Items, dryer, nowH);
        inv.SetModified();
        Tell(player, dryer
            ? HSLoc.Format("hsDried", "Dried {0} items", n)
            : HSLoc.Format("hsWashed", "Washed {0} items. They are wet: dry them in a dryer, or they dry on you in {1} hours", n, HSWorkingStuffSettings.AirDryHours.ToString("0")));
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
        return this;
    }

    public NetPackageHSWorkingStuffCore OfStove(Vector3i pos, bool on)
    {
        kind = HSWorkingStuffNet.StovePower;
        x = pos.x;
        y = pos.y;
        z = pos.z;
        units = on ? 1 : 0;
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
            if (world == null)
                return;
            EntityPlayer player = world.GetEntity(entityId) as EntityPlayer;
            if (player == null && Sender != null)
                player = world.GetEntity(Sender.entityId) as EntityPlayer;
            if (player == null)
                return;
            if (HSWorkingStuffNet.IsAuthority)
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
        return 29;
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
