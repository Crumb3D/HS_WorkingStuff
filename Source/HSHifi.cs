using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

// Hi-fi: plugs into a socket, and speakers are wired out of it with the wire tool. The music only comes out of
// the speakers, so with none wired it plays to itself. Hi-fi meta2and1: bit 7 = on, bit 6 = playing (clear =
// paused), bits 0-5 = track. Speaker meta = volume + 1 (0 = the default).
public class BlockHSHifi : BlockPowered
{
    readonly BlockActivationCommand[] hifiCmds = new BlockActivationCommand[]
    {
        new BlockActivationCommand("hshifipower", "electric_switch", true),
        new BlockActivationCommand("hshifiplay", "twitch_play", false),
        new BlockActivationCommand("hshifinext", "arrow_right", false),
        new BlockActivationCommand("hshifiprev", "arrow_left", false),
        new BlockActivationCommand("hshifiup", "add", false),
        new BlockActivationCommand("hshifidown", "subtract", false),
        new BlockActivationCommand("take", "hand", false)
    };

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
        if (GameManager.IsDedicatedServer || _ebcd == null || _ebcd.transform == null)
            return;
        var player = _ebcd.transform.GetComponent<HSHifiPlayer>();
        if (player == null)
            player = _ebcd.transform.gameObject.AddComponent<HSHifiPlayer>();
        player.Pos = _blockPos;
        if (!_blockValue.ischild && _ebcd != null)
            HSShare.Attach(_ebcd.transform, _blockPos);
    }

    public override bool CanPlaceBlockAt(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, bool _bOmitCollideCheck = false)
    {
        if (HSShare.OnSocket(_world, _blockPos))
            return base.CanPlaceBlockAt(_world, _blockPos, _blockValue, true);
        return base.CanPlaceBlockAt(_world, _blockPos, _blockValue, _bOmitCollideCheck);
    }

    public override void OnBlockAdded(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue, PlatformUserIdentifierAbs _addedByPlayer)
    {
        base.OnBlockAdded(world, _chunk, _blockPos, _blockValue, _addedByPlayer);
        if (!_blockValue.ischild)
        {
            HSHifi.Track(_blockPos);
            HSShare.Claim(world, _blockPos);
        }
    }

    public override void OnBlockLoaded(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue)
    {
        base.OnBlockLoaded(_world, _blockPos, _blockValue);
        if (!_blockValue.ischild)
            HSHifi.Track(_blockPos);
    }

    public override void OnBlockRemoved(WorldBase world, Chunk _chunk, Vector3i _blockPos, BlockValue _blockValue)
    {
        if (!_blockValue.ischild)
            HSShare.QueueRestore(_blockPos);
        HSHifi.Untrack(_blockPos);
        base.OnBlockRemoved(world, _chunk, _blockPos, _blockValue);
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        bool on = HSHifi.IsOn(_blockValue);
        bool playing = on && HSHifi.Playing(_blockValue);
        hifiCmds[1].text = playing ? "hshifipause" : "hshifiplay";
        hifiCmds[1].icon = playing ? "twitch_pause" : "twitch_play";
        for (int i = 1; i <= 5; i++)
            hifiCmds[i].enabled = on;
        bool mine = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        hifiCmds[6].enabled = mine && TakeDelay > 0f;
        return hifiCmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (!_commandName.StartsWith("hshifi"))
            return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
        if (_commandName == "hshifiup" || _commandName == "hshifidown")
        {
            HSHifi.SetAllVolume(_world, _blockPos, _player, _commandName == "hshifiup" ? 1 : -1);
            return true;
        }
        bool on = HSHifi.IsOn(_blockValue);
        bool playing = HSHifi.Playing(_blockValue);
        int track = HSHifi.Track(_blockValue);
        int count = HSHifi.Tracks().Count;
        if (_commandName == "hshifipower")
        {
            on = !on;
            playing = on;
        }
        else if (_commandName == "hshifiplay" || _commandName == "hshifipause")
            playing = _commandName == "hshifiplay";
        else
        {
            if (count == 0)
            {
                GameManager.ShowTooltip(_player, HSHifi.NoMusicText());
                return true;
            }
            track = ((track % count) + (_commandName == "hshifinext" ? 1 : count - 1)) % count;
            playing = true;
        }
        _world.SetBlockRPC(_blockPos, HSHifi.With(_blockValue, on, playing, track));
        Audio.Manager.BroadcastPlay(_blockPos.ToVector3(), _commandName == "hshifipower" && !on ? "switch_down" : "switch_up");
        return true;
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        string circuit = HSCircuits.CircuitLine(_world, _blockPos);
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + HSHifi.Status(_world, _blockPos, _blockValue)
            + (string.IsNullOrEmpty(circuit) ? "" : "\n" + circuit);
    }
}

// Passive speaker: wired from a hi-fi. Its own volume overrides the hi-fi's until the hi-fi volume is turned again.
public class BlockHSHifiSpeaker : BlockPowered
{
    readonly BlockActivationCommand[] speakerCmds = new BlockActivationCommand[]
    {
        new BlockActivationCommand("hsspkup", "add", true),
        new BlockActivationCommand("hsspkdown", "subtract", true),
        new BlockActivationCommand("take", "hand", false)
    };

    public override void OnBlockEntityTransformAfterActivated(WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, BlockEntityData _ebcd)
    {
        base.OnBlockEntityTransformAfterActivated(_world, _blockPos, _blockValue, _ebcd);
        HSModelHit.Wire(_ebcd);
    }

    public override bool HasBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return true;
    }

    public override BlockActivationCommand[] GetBlockActivationCommands(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        bool mine = _world.IsMyLandProtectedBlock(_blockPos, _world.GetGameManager().GetPersistentLocalPlayer());
        speakerCmds[2].enabled = mine && TakeDelay > 0f;
        return speakerCmds;
    }

    public override bool OnBlockActivated(string _commandName, WorldBase _world, Vector3i _blockPos, BlockValue _blockValue, EntityPlayerLocal _player)
    {
        if (_commandName != "hsspkup" && _commandName != "hsspkdown")
            return base.OnBlockActivated(_commandName, _world, _blockPos, _blockValue, _player);
        int v = HSHifi.Volume(_blockValue) + (_commandName == "hsspkup" ? 1 : -1);
        if (v < 0 || v > HSHifi.MaxVolume)
        {
            GameManager.ShowTooltip(_player, HSHifi.VolumeLimitText(v < 0));
            return true;
        }
        _world.SetBlockRPC(_blockPos, HSHifi.WithVolume(_blockValue, v));
        Audio.Manager.BroadcastPlay(_blockPos.ToVector3(), "switch_up");
        GameManager.ShowTooltip(_player, HSLoc.Format("hsSpeakerVolumeSet", "This speaker: volume {0} of {1}", v, HSHifi.MaxVolume));
        return true;
    }

    public override string GetActivationText(WorldBase _world, BlockValue _blockValue, Vector3i _blockPos, EntityAlive _entityFocusing)
    {
        return _blockValue.Block.GetLocalizedBlockName() + "\n" + HSHifi.SpeakerStatus(_world, _blockPos, _blockValue);
    }
}

public static class HSHifi
{
    public const int MaxSpeakers = 5;
    public const int MaxVolume = 10;
    public const int DefaultVolume = 6;
    static readonly string[] Extensions = { ".mp3", ".ogg", ".wav" };
    static List<string> tracks = new List<string>();
    static float nextScan = -1f;
    static readonly HashSet<long> hifis = new HashSet<long>();
    public static readonly HashSet<string> Failed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static void Track(Vector3i pos)
    {
        hifis.Add(HSWater.Key(pos));
    }

    public static void Untrack(Vector3i pos)
    {
        hifis.Remove(HSWater.Key(pos));
    }

    public static void VisitPlaying(World world, System.Action<Vector3i> visit)
    {
        if (world == null || visit == null || hifis.Count == 0)
            return;
        foreach (long k in hifis)
        {
            Vector3i p = HSWater.FromKey(k);
            if (world.GetChunkFromWorldPos(p) == null)
                continue;
            BlockValue bv = world.GetBlock(p);
            if (bv.Block is BlockHSHifi && IsOn(bv) && Playing(bv) && HSTV.Live(world, p))
                visit(p);
        }
    }

    // The music comes out of the speakers. A hi-fi with none wired is silent, so it does not ring outside.
    public static void VisitSpeakers(World world, System.Action<Vector3i> visit)
    {
        if (world == null || visit == null || hifis.Count == 0)
            return;
        foreach (long k in hifis)
        {
            Vector3i p = HSWater.FromKey(k);
            if (world.GetChunkFromWorldPos(p) == null)
                continue;
            BlockValue bv = world.GetBlock(p);
            if (!(bv.Block is BlockHSHifi) || !IsOn(bv) || !Playing(bv) || !HSTV.Live(world, p))
                continue;
            List<Vector3i> speakers = Speakers(world, p);
            for (int i = 0; i < speakers.Count; i++)
                visit(speakers[i]);
        }
    }

    public static bool IsOn(BlockValue bv)
    {
        return (bv.meta2and1 & 0x80) != 0;
    }

    public static bool Playing(BlockValue bv)
    {
        return (bv.meta2and1 & 0x40) != 0;
    }

    public static int Track(BlockValue bv)
    {
        return bv.meta2and1 & 0x3F;
    }

    public static BlockValue With(BlockValue bv, bool on, bool playing, int track)
    {
        bv.meta2and1 = (byte)((on ? 0x80 : 0) | (playing ? 0x40 : 0) | (track & 0x3F));
        return bv;
    }

    public static int Volume(BlockValue bv)
    {
        return bv.meta == 0 ? DefaultVolume : Mathf.Clamp(bv.meta - 1, 0, MaxVolume);
    }

    public static BlockValue WithVolume(BlockValue bv, int volume)
    {
        bv.meta = (byte)(Mathf.Clamp(volume, 0, MaxVolume) + 1);
        return bv;
    }

    public static string ModFolder
    {
        get { return Path.Combine(HSWorkingStuffMod.ModPath ?? "", "Music"); }
    }

    public static string UserFolder
    {
        get
        {
            try
            {
                string user = GameIO.GetUserGameDataDir();
                return string.IsNullOrEmpty(user) ? null : Path.Combine(Path.Combine(user, "HSWorkingStuff"), "Music");
            }
            catch
            {
                return null;
            }
        }
    }

    // Tracks are the music files in both folders, sorted by name, so every player needs the same files.
    public static List<string> Tracks()
    {
        if (Time.time < nextScan)
            return tracks;
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
        if (found.Count > 64)
            found.RemoveRange(64, found.Count - 64);
        tracks = found;
        return tracks;
    }

    public static string File(int track)
    {
        var list = Tracks();
        return list.Count == 0 ? null : list[track % list.Count];
    }

    public static string NoMusicText()
    {
        return HSLoc.Format("hsHifiNoMusic", "No music: put .mp3, .ogg or .wav files in {0}", UserFolder ?? ModFolder);
    }

    public static string VolumeLimitText(bool low)
    {
        return low
            ? HSLoc.Get("hsHifiVolumeMin", "The volume is already right down")
            : HSLoc.Get("hsHifiVolumeMax", "The volume is already at full");
    }

    public static List<Vector3i> Speakers(WorldBase world, Vector3i hifi)
    {
        var list = new List<Vector3i>();
        var te = world != null ? world.GetTileEntity(hifi) as TileEntityPowered : null;
        if (te == null || te.wireDataList == null)
            return list;
        for (int i = 0; i < te.wireDataList.Count; i++)
            if (world.GetBlock(te.wireDataList[i]).Block is BlockHSHifiSpeaker)
                list.Add(te.wireDataList[i]);
        return list;
    }

    public static bool HifiOf(WorldBase world, Vector3i speaker, out Vector3i hifi)
    {
        var te = world != null ? world.GetTileEntity(speaker) as TileEntityPowered : null;
        return HSPlugs.Parent(te, out hifi) && world.GetBlock(hifi).Block is BlockHSHifi;
    }

    // The hi-fi volume knob sets every speaker wired to it to the same level.
    public static void SetAllVolume(WorldBase world, Vector3i hifi, EntityPlayerLocal player, int step)
    {
        List<Vector3i> speakers = Speakers(world, hifi);
        if (speakers.Count == 0)
        {
            GameManager.ShowTooltip(player, HSLoc.Get("hsHifiNoSpeakers", "No speakers: wire speakers from the hi-fi to hear it"));
            return;
        }
        int v = Volume(world.GetBlock(speakers[0])) + step;
        if (v < 0 || v > MaxVolume)
        {
            GameManager.ShowTooltip(player, VolumeLimitText(v < 0));
            return;
        }
        foreach (Vector3i s in speakers)
        {
            BlockValue bv = world.GetBlock(s);
            if (Volume(bv) != v)
                world.SetBlockRPC(s, WithVolume(bv, v));
        }
        Audio.Manager.BroadcastPlay(hifi.ToVector3(), "switch_up");
        GameManager.ShowTooltip(player, HSLoc.Format("hsHifiVolumeSet", "All {0} speakers: volume {1} of {2}", speakers.Count, v, MaxVolume));
    }

    static string TrackLine(BlockValue bv)
    {
        int count = Tracks().Count;
        if (count == 0)
            return NoMusicText();
        int t = Track(bv) % count;
        string file = File(t);
        string name = Path.GetFileNameWithoutExtension(file);
        if (Failed.Contains(file))
            return HSLoc.Format("hsHifiCantPlay", "Can't play track {0}: {1}", t + 1, name);
        return Playing(bv)
            ? HSLoc.Format("hsHifiPlaying", "Playing track {0} of {1}: {2}", t + 1, count, name)
            : HSLoc.Format("hsHifiPaused", "Paused on track {0} of {1}: {2}", t + 1, count, name);
    }

    public static string Status(WorldBase world, Vector3i pos, BlockValue bv)
    {
        if (!HSTV.PluggedIn(world, pos))
            return HSLoc.Get("hsHifiNoSocket", "Not plugged in: wire it from a socket");
        if (!HSTV.Live(world, pos))
            return HSLoc.Get("hsHifiNoPower", "No power at the socket");
        if (!IsOn(bv))
            return HSLoc.Get("hsHifiOff", "Off");
        string text = TrackLine(bv);
        List<Vector3i> speakers = Speakers(world, pos);
        if (speakers.Count == 0)
            return text + "\n" + HSLoc.Get("hsHifiNoSpeakers", "No speakers: wire speakers from the hi-fi to hear it");
        return text + "\n" + HSLoc.Format("hsHifiSpeakers", "Speakers: {0} wired, volume {1} of {2}",
            speakers.Count, Volume(world.GetBlock(speakers[0])), MaxVolume);
    }

    public static string SpeakerStatus(WorldBase world, Vector3i pos, BlockValue bv)
    {
        string volume = HSLoc.Format("hsSpeakerVolume", "Volume {0} of {1}", Volume(bv), MaxVolume);
        Vector3i hifi;
        if (!HifiOf(world, pos, out hifi))
            return HSLoc.Get("hsSpeakerNoHifi", "Not wired: wire it from a hi-fi with the wire tool") + "\n" + volume;
        BlockValue hv = world.GetBlock(hifi);
        if (!HSTV.Live(world, hifi) || !IsOn(hv))
            return HSLoc.Get("hsSpeakerHifiOff", "The hi-fi is off") + "\n" + volume;
        return TrackLine(hv) + "\n" + volume;
    }

    public static AudioType TypeOf(string file)
    {
        string ext = Path.GetExtension(file).ToLowerInvariant();
        if (ext == ".ogg")
            return AudioType.OGGVORBIS;
        if (ext == ".wav")
            return AudioType.WAV;
        return AudioType.MPEG;
    }
}

// Client only: loads the track and plays it out of every speaker wired to this hi-fi, all in step.
// Lights the display and volume ring while on, the standby light while off, and draws the speaker cables.
public class HSHifiPlayer : MonoBehaviour
{
    public Vector3i Pos;

    readonly Dictionary<long, AudioSource> outs = new Dictionary<long, AudioSource>();
    readonly List<GameObject> cables = new List<GameObject>();
    AudioClip clip;
    string clipFile;
    string loadingFile;
    int track = -1;
    float position;
    bool running;
    float resendAt;
    float next;
    float nextCables;
    string state;

    void Update()
    {
        if (running && clip != null)
        {
            AudioSource master = Master();
            position = master != null ? master.time : position + Time.deltaTime;
        }
        if (Time.time < next)
            return;
        next = Time.time + 0.25f;
        try
        {
            Refresh();
        }
        catch (Exception e)
        {
            Log.Warning("[HSWorkingStuff] Hi-fi at " + Pos + ": " + e.Message);
            next = Time.time + 10f;
        }
    }

    AudioSource Master()
    {
        foreach (AudioSource s in outs.Values)
            if (s != null && s.isPlaying && s.clip == clip)
                return s;
        return null;
    }

    void Refresh()
    {
        World world = GameManager.Instance != null ? GameManager.Instance.World : null;
        if (world == null)
            return;
        BlockValue bv = world.GetBlock(Pos);
        bool live = HSTV.Live(world, Pos);
        bool on = live && HSHifi.IsOn(bv);
        ShowParts(on, live);
        List<Vector3i> speakers = HSHifi.Speakers(world, Pos);
        if (Time.time >= nextCables)
        {
            nextCables = Time.time + 1f;
            Cables(world, speakers);
        }
        int t = HSHifi.Track(bv);
        string file = on ? HSHifi.File(t) : null;
        string why = !on ? "off" : file == null ? "no music files in " + HSHifi.ModFolder + " or " + HSHifi.UserFolder
            : !Near(world) ? "too far away" : HSHifi.Failed.Contains(file) ? "can't play " + file
            : (HSHifi.Playing(bv) ? "playing " : "paused on ") + Path.GetFileName(file) + " to " + speakers.Count + " speakers";
        if (why != state)
        {
            state = why;
            Log.Out("[HSWorkingStuff] Hi-fi at " + Pos + ": " + why);
        }
        if (!on)
            position = 0f;
        if (t != track)
        {
            track = t;
            position = 0f;
        }
        if (file == null || !Near(world) || HSHifi.Failed.Contains(file))
        {
            Silence(false);
            return;
        }
        if (file != clipFile)
        {
            Silence(true);
            if (loadingFile != file)
            {
                loadingFile = file;
                StartCoroutine(Load(file));
            }
            return;
        }
        running = HSHifi.Playing(bv);
        if (running && position >= clip.length - 0.05f)
        {
            EndOfTrack(world, bv);
            return;
        }
        DropGone(speakers);
        float hear = Mathf.Max(4f, HSWorkingStuffSettings.HifiHearRange);
        foreach (Vector3i s in speakers)
        {
            AudioSource src = Out(world, s);
            if (src == null)
                continue;
            src.volume = Mathf.Clamp01(HSWorkingStuffSettings.HifiVolume) * HSHifi.Volume(world.GetBlock(s)) / HSHifi.MaxVolume;
            src.maxDistance = hear;
            if (src.clip != clip)
            {
                src.Stop();
                src.clip = clip;
            }
            if (!running)
            {
                if (src.isPlaying)
                    src.Pause();
                continue;
            }
            if (!src.isPlaying)
            {
                if (position < clip.length - 0.3f)
                {
                    src.time = Mathf.Max(0f, position);
                    src.Play();
                }
            }
            else if (Mathf.Abs(src.time - position) > 0.3f)
                src.time = Mathf.Clamp(position, 0f, clip.length - 0.1f);
        }
    }

    // The player that hears a track finish moves the hi-fi on to the next one, round to the first.
    void EndOfTrack(World world, BlockValue bv)
    {
        int count = HSHifi.Tracks().Count;
        int nt = count > 0 ? (HSHifi.Track(bv) % count + 1) % count : 0;
        if (nt == HSHifi.Track(bv))
        {
            position = 0f;
            foreach (AudioSource s in outs.Values)
                if (s != null)
                    s.Stop();
            return;
        }
        Silence(false);
        running = false;
        if (Time.time < resendAt)
            return;
        resendAt = Time.time + 3f;
        world.SetBlockRPC(Pos, HSHifi.With(bv, true, true, nt));
    }

    bool Near(World world)
    {
        EntityPlayerLocal p = world.GetPrimaryPlayer();
        if (p == null)
            return false;
        float r = Mathf.Max(4f, HSWorkingStuffSettings.HifiHearRange) + 8f;
        return (p.GetPosition() - (Pos.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f))).sqrMagnitude <= r * r;
    }

    AudioSource Out(World world, Vector3i speaker)
    {
        long k = HSWater.Key(speaker);
        AudioSource src;
        if (outs.TryGetValue(k, out src) && src != null)
            return src;
        BlockEntityData ebcd = world.ChunkCache != null ? world.ChunkCache.GetBlockEntity(speaker) : null;
        if (ebcd == null || !ebcd.bHasTransform || ebcd.transform == null)
            return null;
        var go = new GameObject("HSHifiSound");
        go.transform.SetParent(ebcd.transform, false);
        go.transform.position = CabinetCentre(ebcd.transform);
        src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = 1f;
        src.spread = 60f;
        src.dopplerLevel = 0f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1.5f;
        src.maxDistance = Mathf.Max(4f, HSWorkingStuffSettings.HifiHearRange);
        src.priority = 90;
        outs[k] = src;
        return src;
    }

    // Standing and wall speakers sit at different heights in their block, so the sound starts from the cabinet itself.
    static Vector3 CabinetCentre(Transform model)
    {
        Bounds box = default(Bounds);
        bool any = false;
        foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
        {
            if (any)
                box.Encapsulate(r.bounds);
            else
            {
                box = r.bounds;
                any = true;
            }
        }
        return any ? box.center : model.position + Vector3.up * 0.15f;
    }

    void DropGone(List<Vector3i> speakers)
    {
        List<long> gone = null;
        foreach (var kv in outs)
        {
            bool keep = kv.Value != null;
            if (keep)
            {
                keep = false;
                foreach (Vector3i s in speakers)
                    if (HSWater.Key(s) == kv.Key)
                        keep = true;
            }
            if (keep)
                continue;
            if (gone == null)
                gone = new List<long>();
            gone.Add(kv.Key);
        }
        if (gone == null)
            return;
        foreach (long k in gone)
        {
            if (outs[k] != null)
                Destroy(outs[k].gameObject);
            outs.Remove(k);
        }
    }

    void Silence(bool drop)
    {
        running = false;
        foreach (AudioSource s in outs.Values)
        {
            if (s == null)
                continue;
            s.Stop();
            if (drop)
                s.clip = null;
        }
    }

    IEnumerator Load(string file)
    {
        var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(file), HSHifi.TypeOf(file));
        var dh = req.downloadHandler as DownloadHandlerAudioClip;
        if (dh != null)
        {
            dh.streamAudio = false;
            dh.compressed = false;
        }
        yield return req.SendWebRequest();
        try
        {
            if (loadingFile != file)
                yield break;
            loadingFile = null;
            AudioClip got = req.result == UnityWebRequest.Result.Success ? DownloadHandlerAudioClip.GetContent(req) : null;
            if (got == null || got.length <= 0.1f)
            {
                Log.Warning("[HSWorkingStuff] Hi-fi can't play " + file + ": " + (req.error ?? "no sound in the file"));
                HSHifi.Failed.Add(file);
                yield break;
            }
            got.name = "HSHifi_" + Path.GetFileNameWithoutExtension(file);
            Silence(true);
            if (clip != null)
                Destroy(clip);
            clip = got;
            clipFile = file;
            Log.Out("[HSWorkingStuff] Hi-fi at " + Pos + ": loaded " + Path.GetFileName(file) + " (" + got.length.ToString("0") + "s)");
        }
        finally
        {
            req.Dispose();
        }
    }

    void ShowParts(bool on, bool live)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            bool want;
            if (n.EndsWith("_Display") || n.EndsWith("_Ring"))
                want = on;
            else if (n.EndsWith("_Standby"))
                want = live && !on;
            else
                continue;
            if (t.gameObject.activeSelf != want)
                t.gameObject.SetActive(want);
        }
    }

    // Vanilla wires only show with the wire tool out; speaker cable is always there.
    // It is fitted to the walls: out of the set, up the wall, along the ceiling, and down to the speaker.
    void Cables(World world, List<Vector3i> speakers)
    {
        Material mat = null;
        foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
            foreach (Material m in r.sharedMaterials)
                if (mat == null && m != null && m.name.StartsWith("HS_Dark"))
                    mat = m;
        int n = 0;
        foreach (Vector3i s in speakers)
        {
            BlockEntityData ebcd = world.ChunkCache != null ? world.ChunkCache.GetBlockEntity(s) : null;
            if (mat == null || ebcd == null || !ebcd.bHasTransform || ebcd.transform == null)
                continue;
            Vector3 a = HSPlugs.LeadEnd(transform, ebcd.transform.position + Vector3.up * 0.05f);
            Vector3 b = HSPlugs.LeadEnd(ebcd.transform, a);
            List<Vector3> pts = CablePoints(world, Pos, s, a, b);
            if (n >= cables.Count)
            {
                var go = new GameObject("HSHifiCable" + n);
                go.transform.SetParent(transform, false);
                go.layer = gameObject.layer;
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = false;
                line.startWidth = line.endWidth = 0.006f;
                line.numCapVertices = 2;
                line.numCornerVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                cables.Add(go);
            }
            GameObject cable = cables[n++];
            cable.SetActive(true);
            var lr = cable.GetComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.positionCount = pts.Count;
            for (int k = 0; k < pts.Count; k++)
                lr.SetPosition(k, transform.InverseTransformPoint(pts[k]));
        }
        for (int i = n; i < cables.Count; i++)
            if (cables[i].activeSelf)
                cables[i].SetActive(false);
    }

    // The cable stays on a real wall. Glass does not count, so it goes around a window.
    static List<Vector3> CablePoints(World world, Vector3i from, Vector3i to, Vector3 a, Vector3 b)
    {
        int mode = RouteMode(world, to);
        List<Vector3i> cells = CableRoute(world, from, to);
        var pts = new List<Vector3>();
        pts.Add(a);
        if (cells != null)
        {
            for (int i = 0; i < cells.Count; i++)
            {
                Vector3i c = cells[i];
                if (c == from || c == to)
                    continue;
                pts.Add(Against(world, c, mode));
            }
        }
        if (cells == null)
        {
            pts.Add(a);
            return pts;
        }
        pts.Add(b);
        int k = 1;
        while (k < pts.Count - 1)
        {
            Vector3 ab = pts[k] - pts[k - 1];
            Vector3 bc = pts[k + 1] - pts[k];
            if (ab.sqrMagnitude < 1e-6f || Vector3.Cross(ab, bc).sqrMagnitude < 1e-5f)
                pts.RemoveAt(k);
            else
                k++;
        }
        return pts;
    }

    static List<Vector3i> CableRoute(World world, Vector3i from, Vector3i to)
    {
        const int reach = 16;
        int mode = RouteMode(world, to);
        Vector3i min = Vector3i.Min(from, to) - new Vector3i(reach, 8, reach);
        Vector3i max = Vector3i.Max(from, to) + new Vector3i(reach, 16, reach);
        if (min.y < 1)
            min.y = 1;
        if (max.y > 253)
            max.y = 253;
        var cost = new Dictionary<long, int>();
        var prev = new Dictionary<long, Vector3i>();
        var open = new List<Vector3i>();
        long fk = HSWater.Key(from);
        cost[fk] = 0;
        open.Add(from);
        Vector3i[] dirs = HSWater.Dirs;
        long tk = HSWater.Key(to);
        int found = int.MaxValue;
        int steps = 0;
        while (open.Count > 0 && steps++ < 8000)
        {
            int bestI = 0;
            int bestC = cost[HSWater.Key(open[0])];
            for (int i = 1; i < open.Count; i++)
            {
                int c = cost[HSWater.Key(open[i])];
                if (c < bestC)
                {
                    bestC = c;
                    bestI = i;
                }
            }
            Vector3i cur = open[bestI];
            open.RemoveAt(bestI);
            long ck = HSWater.Key(cur);
            if (bestC != cost[ck] || bestC >= found)
                continue;
            if (ck == tk)
            {
                found = bestC;
                continue;
            }
            Vector3i came = Vector3i.zero;
            Vector3i back;
            if (prev.TryGetValue(ck, out back))
                came = cur - back;
            for (int d = 0; d < dirs.Length; d++)
            {
                Vector3i n = cur + dirs[d];
                if (n.x < min.x || n.y < min.y || n.z < min.z || n.x > max.x || n.y > max.y || n.z > max.z)
                    continue;
                int step = CableStep(world, from, to, n, dirs[d], came, mode);
                if (step < 0)
                    continue;
                int nc = bestC + step;
                long nk = HSWater.Key(n);
                int old;
                if (cost.TryGetValue(nk, out old) && old <= nc)
                    continue;
                cost[nk] = nc;
                prev[nk] = cur;
                open.Add(n);
            }
        }
        if (!cost.ContainsKey(tk))
            return null;
        var path = new List<Vector3i>();
        Vector3i at = to;
        path.Add(at);
        int guard = 0;
        while (at != from && guard++ < 512)
        {
            if (!prev.TryGetValue(HSWater.Key(at), out at))
                return null;
            path.Add(at);
        }
        path.Reverse();
        return path;
    }

    // 0 = stand speaker (down, along the floor edge, up). 1 = wall speaker (up, along the ceiling edge, down).
    // 2 = a power lead (down the socket's wall to the bottom of the appliance, then along a wall).
    static int RouteMode(World world, Vector3i to)
    {
        Block block = world.GetBlock(to).Block;
        string n = block != null ? block.GetBlockName() : "";
        if (n == "hsHifiSpeakerWall")
            return 1;
        if (n == "hsHifiSpeaker")
            return 0;
        return 2;
    }

    public static List<Vector3> LeadPoints(World world, Vector3i from, Vector3i to, Vector3 a, Vector3 b)
    {
        return CablePoints(world, from, to, a, b);
    }

    static int CableStep(World world, Vector3i start, Vector3i end, Vector3i to, Vector3i dir, Vector3i came, int mode)
    {
        if (to != start && to != end)
        {
            BlockValue bv = world.GetBlock(to);
            if (!bv.isair)
                return -1;
            if (!OnWall(world, to))
                return -1;
        }
        bool ceil = OnCeil(world, to);
        bool floor = OnFloor(world, to);
        bool vert = dir.y != 0;
        int c;
        if (mode == 1)
        {
            if (ceil && !vert)
                c = 4;
            else if (vert)
                c = 7;
            else
                c = 16;
        }
        else if (mode == 0)
        {
            if (floor && !vert)
                c = 4;
            else if (vert)
                c = 7;
            else
                c = 16;
        }
        else
        {
            // Stay on the wall. A run along the floor is the long way across the room.
            c = 4;
        }
        if (came != Vector3i.zero && came != dir)
            c += 4;
        return c;
    }

    static bool Solid(World world, Vector3i p)
    {
        return HSPlugs.PlaceWall(world, p);
    }

    static bool OnWall(World world, Vector3i c)
    {
        return Solid(world, c + new Vector3i(1, 0, 0)) || Solid(world, c + new Vector3i(-1, 0, 0))
            || Solid(world, c + new Vector3i(0, 0, 1)) || Solid(world, c + new Vector3i(0, 0, -1));
    }

    static bool OnCeil(World world, Vector3i c)
    {
        return Solid(world, c + new Vector3i(0, 1, 0));
    }

    static bool OnFloor(World world, Vector3i c)
    {
        return Solid(world, c + new Vector3i(0, -1, 0));
    }

    static Vector3 Against(World world, Vector3i c, int mode)
    {
        Vector3 p = c.ToVector3() + new Vector3(0.5f, 0.5f, 0.5f) - Origin.position;
        const float d = 0.46f;
        Vector3 o = Vector3.zero;
        bool xp = Solid(world, c + new Vector3i(1, 0, 0));
        bool xn = Solid(world, c + new Vector3i(-1, 0, 0));
        bool zp = Solid(world, c + new Vector3i(0, 0, 1));
        bool zn = Solid(world, c + new Vector3i(0, 0, -1));
        if (xp && !xn) o.x = d;
        else if (xn && !xp) o.x = -d;
        if (zp && !zn) o.z = d;
        else if (zn && !zp) o.z = -d;
        if (mode == 0 && Solid(world, c + new Vector3i(0, -1, 0)))
            o.y = -d;
        else if (mode != 2 && Solid(world, c + new Vector3i(0, 1, 0)))
            o.y = d;
        return p + o;
    }

    void OnDestroy()
    {
        foreach (AudioSource s in outs.Values)
            if (s != null)
                Destroy(s.gameObject);
        outs.Clear();
        if (clip != null)
            Destroy(clip);
    }
}
