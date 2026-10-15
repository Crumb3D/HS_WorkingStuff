using System.Collections.Generic;
using UnityEngine;

// Washing line (two posts, a line and pegs) and clothes airer: storage you hang wet clothes and armour in, and they dry
// with no power. A line under open sky dries fastest in daylight, slower at night, and rain soaks everything on it again.
// A covered line dries like an airer; an airer dries twice as fast in a room a radiator is warming.
// Block meta2 = how many pieces of clothing are hung up (the model shows that many).
public class BlockHSDryer : BlockCompositeTileEntity
{
    public bool Outdoor;
    public int Shown = 8;

    public override void Init()
    {
        base.Init();
        Properties.ParseBool("HSOutdoor", ref Outdoor);
        Properties.ParseInt("HSShown", ref Shown);
        Shown = Mathf.Clamp(Shown, 0, 15);
    }

    public override void OnBlockAdded(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue, PlatformUserIdentifierAbs _addedByPlayer)
    {
        base.OnBlockAdded(_world, _chunk, _blockPos, _blockValue, _addedByPlayer);
        if (!_blockValue.ischild)
            HSDrying.Track(_blockPos);
    }

    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockLoaded(_world, _blockPos, _blockValue);
        if (!_blockValue.ischild)
            HSDrying.Track(_blockPos);
    }

    public override void OnBlockRemoved(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        if (!_blockValue.ischild)
            HSDrying.Untrack(_blockPos);
        base.OnBlockRemoved(_world, _chunk, _blockPos, _blockValue);
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _bed)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _bed);
        HSModelHit.Wire(_bed);
        if (GameManager.IsDedicatedServer || _bed == null || _bed.transform == null)
            return;
        var view = _bed.transform.GetComponent<HSDryerView>();
        if (view == null)
            view = _bed.transform.gameObject.AddComponent<HSDryerView>();
        view.Pos = _blockPos;
        view.Shown = Shown;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_blockValue.ischild)
        {
            Vector3i parent = _blockValue.Block.multiBlockPos.GetParentPos(_blockPos, _blockValue);
            return OnBlockActivated(_commandName, _world, parent, _world.GetBlock(parent), _player);
        }
        if (_commandName == "Search")
        {
            XUiC_HSDryingWindowGroup.Open(_player, _world.GetTileEntity(_blockPos), _blockValue.Block.GetLocalizedBlockName());
            return true;
        }
        return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        string text = base.GetActivationText(_world, _blockValue, _blockPos, _entityFocusing);
        Vector3i pos = HSWater.Parent(_blockPos, _blockValue);
        World world = _world as World;
        if (world == null)
            return text;
        string status = HSDrying.Status(world, pos, this, world.GetBlock(pos));
        return string.IsNullOrEmpty(text) ? status : text + "\n" + status;
    }
}

public static class HSDrying
{
    public enum Weather { Sun, Night, Rain, Covered, Indoors, Warm }

    const float TickSeconds = 2f;
    const float RainSoakHours = 1f;
    static readonly HashSet<Vector3i> tracked = new HashSet<Vector3i>();
    static float nextTick;
    static ulong lastTime;

    public static int WetSlots(ItemStack[] stacks)
    {
        int n = stacks != null ? stacks.Length : 0;
        return n / 2;
    }

    public static void Track(Vector3i pos)
    {
        tracked.Add(pos);
    }

    public static void Untrack(Vector3i pos)
    {
        tracked.Remove(pos);
    }

    public static void Clear()
    {
        tracked.Clear();
        nextTick = 0f;
        lastTime = 0UL;
    }

    static float Rain()
    {
        try
        {
            return WeatherManager.Instance != null ? WeatherManager.Instance.GetCurrentRainfallPercent() : 0f;
        }
        catch
        {
            return 0f;
        }
    }

    public static Weather Conditions(World world, Vector3i pos, BlockHSDryer block)
    {
        if (!block.Outdoor)
            return HSBoiler.WarmAt(world, new Vector3(pos.x + 0.5f, pos.y + 0.5f, pos.z + 0.5f)) ? Weather.Warm : Weather.Indoors;
        if (!HSWater.OpenToSky(world, pos, block))
            return Weather.Covered;
        if (Rain() >= 0.15f)
            return Weather.Rain;
        return world.IsDaytime() ? Weather.Sun : Weather.Night;
    }

    // Hours to dry something soaking wet; 0 means it is getting wetter.
    public static float DryHours(Weather w)
    {
        float line = Mathf.Max(0.1f, HSWorkingStuffSettings.LineDryHours);
        float airer = Mathf.Max(0.1f, HSWorkingStuffSettings.AirerDryHours);
        switch (w)
        {
            case Weather.Sun: return line;
            case Weather.Night: return line * 2f;
            case Weather.Rain: return 0f;
            case Weather.Warm: return airer * 0.5f;
            default: return airer;
        }
    }

    public static string Status(World world, Vector3i pos, BlockHSDryer block, BlockValue bv)
    {
        if (!HSWorkingStuffSettings.EnableLaundry)
            return "";
        Weather w = Conditions(world, pos, block);
        string cond;
        switch (w)
        {
            case Weather.Sun:
                cond = HSLoc.Format("hsDrySun", "Good drying weather: wet clothes dry in {0} h", DryHours(w).ToString("0.#"));
                break;
            case Weather.Night:
                cond = HSLoc.Format("hsDryNight", "Night: drying slowly, about {0} h", DryHours(w).ToString("0.#"));
                break;
            case Weather.Rain:
                cond = HSLoc.Get("hsDryRain", "Raining: the washing is getting wet again. Bring it in!");
                break;
            case Weather.Covered:
                cond = HSLoc.Format("hsDryCovered", "Under cover: dries like an airer, about {0} h", DryHours(w).ToString("0.#"));
                break;
            case Weather.Warm:
                cond = HSLoc.Format("hsDryWarm", "Warm room: wet clothes dry in {0} h", DryHours(w).ToString("0.#"));
                break;
            default:
                cond = HSLoc.Format("hsDryIndoors", "Wet clothes dry in {0} h (a warm radiator nearby halves that)", DryHours(w).ToString("0.#"));
                break;
        }
        int n = bv.meta2;
        string hung = n > 0
            ? HSLoc.Format("hsDryHung", "{0} pieces of clothing hung up", n)
            : HSLoc.Get("hsDryEmpty", "Nothing hung up: put wet clothes or armour in to dry them");
        return hung + "\n" + cond;
    }

    // Wetness runs off the hour the item was washed (HSLaundry.MetaWet) and falls by itself over AirDryHours,
    // so this only moves that hour to make the line or airer faster, or rain make it wetter.
    static bool Apply(ItemValue iv, float nowH, float hours, float dryHours)
    {
        float air = Mathf.Max(0.1f, HSWorkingStuffSettings.AirDryHours);
        float wet = HSLaundry.Wetness(iv, nowH);
        float next;
        if (dryHours <= 0f)
        {
            if (wet >= 1f)
                return false;
            next = Mathf.Min(1f, wet + hours / RainSoakHours + hours / air);
        }
        else
        {
            if (wet <= 0f || dryHours >= air)
                return false;
            next = wet - hours * (1f / dryHours - 1f / air);
        }
        if (next <= 0f)
            iv.SetMetadata(HSLaundry.MetaWet, -1f);
        else
            iv.SetMetadata(HSLaundry.MetaWet, nowH - (1f - next) * air);
        return true;
    }

    public static void ServerTick()
    {
        if (!HSWorkingStuffNet.IsAuthority || tracked.Count == 0)
            return;
        float t = Time.time;
        if (t < nextTick)
            return;
        nextTick = t + TickSeconds;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        ulong now = world.worldTime;
        float hours = lastTime != 0UL && now > lastTime && now - lastTime < 1000UL ? (now - lastTime) / 1000f : 0f;
        lastTime = now;
        float nowH = HSLaundry.NowHours(world);
        List<Vector3i> gone = null;
        foreach (Vector3i pos in tracked)
        {
            if (world.GetChunkFromWorldPos(pos) == null)
                continue;
            BlockValue bv = world.GetBlock(pos);
            BlockHSDryer block = bv.Block as BlockHSDryer;
            if (block == null || bv.ischild)
            {
                if (gone == null)
                    gone = new List<Vector3i>();
                gone.Add(pos);
                continue;
            }
            TileEntity te = world.GetTileEntity(pos);
            if (te == null)
                continue;
            ItemStack[] stacks = HSInv.GetStorageStacks(te);
            if (stacks == null)
                continue;
            float dryHours = DryHours(Conditions(world, pos, block));
            bool changed = false;
            int half = WetSlots(stacks);
            for (int i = 0; i < half; i++)
            {
                ItemValue iv = HSItem.Val(stacks[i]);
                if (stacks[i] == null || stacks[i].IsEmpty() || !HSLaundry.IsClothing(iv))
                    continue;
                if (HSWorkingStuffSettings.EnableLaundry && hours > 0f && Apply(iv, nowH, hours, dryHours))
                    changed = true;
            }
            if (HSWorkingStuffSettings.EnableLaundry && SortSides(stacks, nowH, half))
                changed = true;
            if (changed)
            {
                for (int i = 0; i < stacks.Length; i++)
                    HSInv.SetStorageSlot(te, i, stacks[i] != null ? stacks[i] : ItemStack.Empty.Clone());
            }
            int clothes = 0;
            for (int i = 0; i < half; i++)
            {
                if (stacks[i] != null && !stacks[i].IsEmpty() && HSLaundry.IsClothing(HSItem.Val(stacks[i])))
                    clothes++;
            }
            int show = Mathf.Min(clothes, block.Shown);
            if (bv.meta2 != show)
            {
                bv.meta2 = (byte)show;
                world.SetBlockRPC(pos, bv);
            }
        }
        if (gone != null)
            for (int i = 0; i < gone.Count; i++)
                tracked.Remove(gone[i]);
    }

    // Wet stays on the left; when it is dry it moves to the right. Wet that was on the
    // right (an old save, or someone dragging) comes back left if there is a peg free.
    static bool SortSides(ItemStack[] stacks, float nowH, int half)
    {
        if (stacks == null || half <= 0)
            return false;
        bool changed = false;
        for (int i = 0; i < half; i++)
        {
            ItemValue iv = HSItem.Val(stacks[i]);
            if (stacks[i] == null || stacks[i].IsEmpty() || !HSLaundry.IsClothing(iv))
                continue;
            if (HSLaundry.Wetness(iv, nowH) > 0f)
                continue;
            int dest = FirstEmpty(stacks, half, stacks.Length);
            if (dest < 0)
                continue;
            stacks[dest] = stacks[i];
            stacks[i] = ItemStack.Empty.Clone();
            changed = true;
        }
        for (int i = half; i < stacks.Length; i++)
        {
            ItemValue iv = HSItem.Val(stacks[i]);
            if (stacks[i] == null || stacks[i].IsEmpty() || !HSLaundry.IsClothing(iv))
                continue;
            if (HSLaundry.Wetness(iv, nowH) <= 0f)
                continue;
            int dest = FirstEmpty(stacks, 0, half);
            if (dest < 0)
                continue;
            stacks[dest] = stacks[i];
            stacks[i] = ItemStack.Empty.Clone();
            changed = true;
        }
        return changed;
    }

    static int FirstEmpty(ItemStack[] stacks, int from, int to)
    {
        for (int i = from; i < to && i < stacks.Length; i++)
        {
            if (stacks[i] == null || stacks[i].IsEmpty())
                return i;
        }
        return -1;
    }
}

// The model carries "<Model>_Cloth0".."_Cloth<n>": the first meta2 of them show.
public class HSDryerView : MonoBehaviour
{
    public Vector3i Pos;
    public int Shown = 8;
    readonly List<GameObject> cloths = new List<GameObject>();
    float next;

    void Start()
    {
        var byIndex = new SortedDictionary<int, GameObject>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            int at = t.name.LastIndexOf("_Cloth");
            int i;
            if (at >= 0 && int.TryParse(t.name.Substring(at + 6), out i))
                byIndex[i] = t.gameObject;
        }
        cloths.AddRange(byIndex.Values);
    }

    void Update()
    {
        if (Time.time < next)
            return;
        next = Time.time + 0.5f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        int n = world.GetBlock(Pos).meta2;
        for (int i = 0; i < cloths.Count; i++)
        {
            bool on = i < n && i < Shown;
            if (cloths[i].activeSelf != on)
                cloths[i].SetActive(on);
        }
    }
}
