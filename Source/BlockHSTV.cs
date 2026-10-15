using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

// TV: block meta2and1 bit 7 = on, bits 0-6 = channel (meta and meta2 hold only 4 bits each). Channels are the video files in the TV folders,
// sorted by name, so every client needs the same files to see the same channel.
public class BlockHSTV : BlockPowered
{
    static readonly BlockActivationCommand[] tvCmds = new BlockActivationCommand[4]
    {
        new BlockActivationCommand("hstvpower", "electric_switch", true),
        new BlockActivationCommand("hstvnext", "frames", true),
        new BlockActivationCommand("hstvprev", "frames", true),
        new BlockActivationCommand("take", "hand", false)
    };

    public override void OnBlockAdded(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue, PlatformUserIdentifierAbs _addedByPlayer)
    {
        base.OnBlockAdded(world, _chunk, _blockPos, _blockValue, _addedByPlayer);
        if (!_blockValue.ischild)
            HSTV.Track(_blockPos);
    }

    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockLoaded(_world, _blockPos, _blockValue);
        if (!_blockValue.ischild)
            HSTV.Track(_blockPos);
    }

    public override void OnBlockRemoved(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        HSTV.Untrack(_blockPos);
        base.OnBlockRemoved(world, _chunk, _blockPos, _blockValue);
    }

    public override void OnBlockValueChanged(WorldBase _world, Chunk _chunk, Vector3i _blockPos, BlockValue _oldBlockValue, BlockValue _newBlockValue)
    {
        base.OnBlockValueChanged(_world, _chunk, _blockPos, _oldBlockValue, _newBlockValue);
        if (!_newBlockValue.ischild)
            HSTV.Demand(_world, _blockPos, _newBlockValue);
    }

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
        if (GameManager.IsDedicatedServer || _ebcd == null || _ebcd.transform == null)
            return;
        var screen = _ebcd.transform.GetComponent<HSTVScreen>();
        if (screen == null)
            screen = _ebcd.transform.gameObject.AddComponent<HSTVScreen>();
        screen.Pos = ParentPos(_blockPos, _blockValue);
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        Vector3i pos = ParentPos(_blockPos, _blockValue);
        bool mine = _world.IsMyLandProtectedBlock(pos, _world.GetGameManager().GetPersistentLocalPlayer());
        bool on = HSTV.IsOn(_world.GetBlock(pos));
        tvCmds[1].enabled = on;
        tvCmds[2].enabled = on;
        tvCmds[3].enabled = mine && TakeDelay > 0f;
        return tvCmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        Vector3i pos = ParentPos(_blockPos, _blockValue);
        _blockValue = _world.GetBlock(pos);
        if (_commandName != "hstvpower" && _commandName != "hstvnext" && _commandName != "hstvprev")
            return base.OnBlockActivated(_commandName, _world, pos, _blockValue, _player);
        bool on = HSTV.IsOn(_blockValue);
        int ch = HSTV.Channel(_blockValue);
        int count = HSTV.Channels().Count;
        if (_commandName == "hstvpower")
            on = !on;
        else if (count > 0)
            ch = ((ch % count) + (_commandName == "hstvnext" ? 1 : count - 1)) % count;
        _blockValue.meta2and1 = (byte)((on ? 0x80 : 0) | (ch & 0x7F));
        _world.SetBlockRPC(pos, _blockValue);
        Audio.Manager.BroadcastPlay(pos.ToVector3(), _commandName == "hstvpower" && !on ? "switch_down" : "switch_up");
        return true;
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        Vector3i pos = ParentPos(_blockPos, _blockValue);
        _blockValue = _world.GetBlock(pos);
        string circuit = HSCircuits.CircuitLine(_world, pos);
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + HSTV.Status(_world, pos, _blockValue)
            + (string.IsNullOrEmpty(circuit) ? "" : "\n" + circuit);
    }

    public static Vector3i ParentPos(Vector3i pos, BlockValue bv)
    {
        if (bv.ischild && bv.Block != null)
            return bv.Block.multiBlockPos.GetParentPos(pos, bv);
        return pos;
    }
}

public static class HSTV
{
    static readonly string[] Extensions = { ".mp4", ".m4v", ".webm", ".mov" };
    static readonly HashSet<long> tvs = new HashSet<long>();
    static List<string> channels = new List<string>();
    static float nextScan = -1f;
    static float nextDemand;

    public static bool IsOn(BlockValue bv)
    {
        return (bv.meta2and1 & 0x80) != 0;
    }

    public static int Channel(BlockValue bv)
    {
        return bv.meta2and1 & 0x7F;
    }

    public static void VisitOn(World world, System.Action<Vector3i> visit)
    {
        if (world == null || visit == null || tvs.Count == 0)
            return;
        foreach (long k in tvs)
        {
            Vector3i p = HSWater.FromKey(k);
            if (world.GetChunkFromWorldPos(p) == null)
                continue;
            BlockValue bv = world.GetBlock(p);
            if (bv.Block is BlockHSTV && IsOn(bv) && Live(world, p))
                visit(p);
        }
    }

    public static void Track(Vector3i pos)
    {
        tvs.Add(HSWater.Key(pos));
    }

    public static void Untrack(Vector3i pos)
    {
        tvs.Remove(HSWater.Key(pos));
    }

    public static void Clear()
    {
        tvs.Clear();
    }

    public static string ModFolder
    {
        get { return Path.Combine(HSWorkingStuffMod.ModPath ?? "", "TV"); }
    }

    public static string UserFolder
    {
        get
        {
            try
            {
                string user = GameIO.GetUserGameDataDir();
                return string.IsNullOrEmpty(user) ? null : Path.Combine(Path.Combine(user, "HSWorkingStuff"), "TV");
            }
            catch
            {
                return null;
            }
        }
    }

    // Rescanned every few seconds, so files dropped into a folder show up as new channels in game.
    public static List<string> Channels()
    {
        if (Time.time < nextScan)
            return channels;
        nextScan = Time.time + 5f;
        try
        {
            string user = UserFolder;
            if (!GameManager.IsDedicatedServer && user != null && !Directory.Exists(user))
                Directory.CreateDirectory(user);
        }
        catch { }
        var found = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string dir in new[] { ModFolder, UserFolder })
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                continue;
            string[] files;
            try
            {
                files = Directory.GetFiles(dir);
            }
            catch
            {
                continue;
            }
            foreach (string f in files)
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (Array.IndexOf(Extensions, ext) >= 0 && names.Add(Path.GetFileName(f)))
                    found.Add(f);
            }
        }
        found.Sort((a, b) => string.Compare(Path.GetFileName(a), Path.GetFileName(b), StringComparison.OrdinalIgnoreCase));
        if (found.Count > 128)
            found.RemoveRange(128, found.Count - 128);
        channels = found;
        return channels;
    }

    public static string File(int channel)
    {
        var list = Channels();
        return list.Count == 0 ? null : list[channel % list.Count];
    }

    public static string ChannelName(int channel)
    {
        string f = File(channel);
        return f == null ? "" : Path.GetFileNameWithoutExtension(f);
    }

    public static bool PluggedIn(WorldBase world, Vector3i pos)
    {
        var te = world != null ? world.GetTileEntity(pos) as TileEntityPowered : null;
        Vector3i parent;
        if (!HSPlugs.Parent(te, out parent))
            return false;
        return HSPlugs.Limit(world.GetBlock(parent).Block) > 0;
    }

    public static bool Live(WorldBase world, Vector3i pos)
    {
        var te = world != null ? world.GetTileEntity(pos) as TileEntityPowered : null;
        return te != null && te.IsPowered && PluggedIn(world, pos);
    }

    public static string Status(WorldBase world, Vector3i pos, BlockValue bv)
    {
        if (!PluggedIn(world, pos))
            return HSLoc.Get("hsTVNoSocket", "Not plugged in: wire it from a socket");
        if (!Live(world, pos))
            return HSLoc.Get("hsTVNoPower", "No power at the socket");
        if (!IsOn(bv))
            return HSLoc.Get("hsTVOff", "Off");
        int count = Channels().Count;
        if (count == 0)
            return HSLoc.Format("hsTVNoVideos", "No channels: put .mp4 files in {0}", UserFolder ?? ModFolder);
        int ch = Channel(bv) % count;
        return HSLoc.Format("hsTVChannel", "Channel {0} of {1}: {2}", ch + 1, count, ChannelName(ch));
    }

    // On-demand power: a TV only draws while it is switched on.
    public static void Demand(WorldBase world, Vector3i pos, BlockValue bv)
    {
        if (!HSWorkingStuffNet.IsAuthority)
            return;
        ushort watts = IsOn(bv) ? HSPowerDemand.Watts(bv.Block, 0) : (ushort)0;
        HSPowerDemand.Set(pos, watts);
    }

    public static void ServerTick()
    {
        if (tvs.Count == 0 || !HSWorkingStuffNet.IsAuthority || Time.time < nextDemand)
            return;
        nextDemand = Time.time + 2f;
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        List<long> stale = null;
        foreach (long k in tvs)
        {
            Vector3i p = HSWater.FromKey(k);
            if (world.GetChunkFromWorldPos(p) == null)
                continue;
            BlockValue bv = world.GetBlock(p);
            if (!(bv.Block is BlockHSTV))
            {
                if (stale == null)
                    stale = new List<long>();
                stale.Add(k);
                continue;
            }
            Demand(world, p, bv);
        }
        if (stale != null)
            foreach (long k in stale)
                tvs.Remove(k);
    }

    // Every TV on a channel shows the same moment, like broadcast TV.
    public static double BroadcastTime(double length)
    {
        if (length <= 1.0)
            return 0.0;
        double now = (DateTime.UtcNow - new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        return now % length;
    }
}

// Client only: plays the channel onto the "<Model>_Screen" quad, with the sound coming from the set.
public class HSTVScreen : MonoBehaviour
{
    public Vector3i Pos;

    VideoPlayer player;
    AudioSource sound;
    RenderTexture frame;
    Renderer screen;
    Material offMat;
    Material onMat;
    string playing;
    string failed;
    string state;
    float next;
    float prepareBy;

    void Update()
    {
        if (Time.time < next)
            return;
        next = Time.time + 0.5f;
        try
        {
            Refresh();
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] TV at " + Pos + ": " + e.Message);
            next = Time.time + 10f;
        }
    }

    void Refresh()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        BlockValue bv = world.GetBlock(Pos);
        string file = null;
        string why;
        if (!HSTV.IsOn(bv))
            why = "switched off";
        else if (!HSTV.PluggedIn(world, Pos))
            why = "not wired from a socket";
        else if (!HSTV.Live(world, Pos))
            why = "no power at the socket";
        else if (!Near(world))
            why = "too far away";
        else if ((file = HSTV.File(HSTV.Channel(bv))) == null)
            why = "no video files in " + HSTV.ModFolder + " or " + HSTV.UserFolder;
        else if (file == failed)
            why = "can't play " + file;
        else
            why = "showing " + file;
        if (why != state)
        {
            state = why;
            Log.Out("[HSWorkingStuff] TV at " + Pos + ": " + why);
        }
        if (file == null || file == failed)
        {
            StopVideo();
            return;
        }
        if (file != playing)
            Play(file);
        else if (!player.isPrepared && Time.time > prepareBy)
        {
            Log.Warning("[HSWorkingStuff] TV at " + Pos + ": " + file + " did not load in 15 seconds");
            failed = file;
            StopVideo();
            return;
        }
        if (sound != null)
        {
            sound.volume = Mathf.Clamp01(HSWorkingStuffSettings.TVVolume);
            sound.maxDistance = Mathf.Max(2f, HSWorkingStuffSettings.TVHearRange);
        }
    }

    bool Near(World world)
    {
        EntityPlayerLocal p = world.GetPrimaryPlayer();
        if (p == null)
            return false;
        float r = Mathf.Max(5f, HSWorkingStuffSettings.TVViewRange);
        return (p.GetPosition() - (Pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f))).sqrMagnitude <= r * r;
    }

    bool EnsureParts()
    {
        if (player != null)
            return true;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.name.EndsWith("_Screen") || (r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("HS_Screen")))
            {
                screen = r;
                break;
            }
        }
        if (screen == null)
        {
            float best = -1f;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name.ToLowerInvariant();
                if (n.IndexOf("screen") >= 0 || n.IndexOf("display") >= 0 || n.IndexOf("glass") >= 0)
                {
                    screen = r;
                    break;
                }
                float size = r.bounds.size.sqrMagnitude;
                if (size > best)
                {
                    best = size;
                    screen = r;
                }
            }
        }
        if (screen == null)
        {
            Log.Warning("[HSWorkingStuff] TV at " + Pos + ": the model has no screen to show the picture on");
            return false;
        }
        offMat = screen.sharedMaterial;
        frame = new RenderTexture(854, 480, 0);
        frame.Create();
        onMat = new Material(offMat);
        onMat.SetColor("_Color", Color.black);
        onMat.SetTexture("_EmissionMap", frame);
        onMat.SetColor("_EmissionColor", Color.white);
        onMat.EnableKeyword("_EMISSION");

        sound = gameObject.AddComponent<AudioSource>();
        sound.playOnAwake = false;
        sound.spatialBlend = 1f;
        sound.rolloffMode = AudioRolloffMode.Linear;
        sound.minDistance = 1f;
        sound.maxDistance = Mathf.Max(2f, HSWorkingStuffSettings.TVHearRange);
        sound.dopplerLevel = 0f;

        player = gameObject.AddComponent<VideoPlayer>();
        player.playOnAwake = false;
        player.isLooping = true;
        player.skipOnDrop = true;
        player.waitForFirstFrame = true;
        player.source = VideoSource.Url;
        player.renderMode = VideoRenderMode.RenderTexture;
        player.targetTexture = frame;
        player.audioOutputMode = VideoAudioOutputMode.AudioSource;
        player.prepareCompleted += OnPrepared;
        player.errorReceived += OnError;
        return true;
    }

    void Play(string file)
    {
        if (!EnsureParts())
        {
            failed = file;
            return;
        }
        playing = file;
        prepareBy = Time.time + 15f;
        player.enabled = true;
        player.Stop();
        player.url = new Uri(file).AbsoluteUri;
        player.controlledAudioTrackCount = 1;
        player.EnableAudioTrack(0, true);
        player.SetTargetAudioSource(0, sound);
        player.Prepare();
    }

    void OnPrepared(VideoPlayer vp)
    {
        if (playing == null)
            return;
        Log.Out("[HSWorkingStuff] TV at " + Pos + ": loaded " + Path.GetFileName(playing) + " (" + vp.width + "x" + vp.height + ", " + vp.length.ToString("0") + "s)");
        vp.time = HSTV.BroadcastTime(vp.length);
        vp.Play();
        if (screen != null)
            screen.sharedMaterial = onMat;
    }

    void OnError(VideoPlayer vp, string message)
    {
        Log.Warning("[HSWorkingStuff] TV can't play " + playing + ": " + message);
        failed = playing;
        StopVideo();
    }

    void StopVideo()
    {
        if (playing == null)
            return;
        playing = null;
        if (player != null)
            player.Stop();
        if (screen != null)
            screen.sharedMaterial = offMat;
    }

    void OnDestroy()
    {
        if (player != null)
            player.Stop();
        if (frame != null)
            frame.Release();
        if (onMat != null)
            Destroy(onMat);
    }
}
