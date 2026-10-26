using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.SaveGames;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Area. Getters and setters read and write the area's own settings, so the engine's functions and rules see
/// them too. Measured on nwserver with NWNX:EE (parity suites nwnx_area_props, nwnx_area_tiles, nwnx_area_objects):
/// <list type="bullet">
/// <item>Setters ignore values out of range (PvP 0-3, wind power 0-2, weather 0-100, shadow opacity 0-100, a fog
/// clip distance below 0, day/night types 0-2, negative colours; larger colours lose their high bits); getters on anything but an area answer 0 (colours
/// and flags -1).</item>
/// <item>A failed argument check (a tile loop outside 1-3, negative tile coordinates or index, a depth below 1, a
/// rotation outside 1-3, a file name over 16 characters) returns nothing, which scripts read as 0 or "".</item>
/// <item>Tile coordinates are metres. Sun and moon colours are set as 0xRRGGBB and read back as the area stores them
/// (0xBBGGRR). GetAreaWind reports nothing until SetAreaWind, which also makes the wind power 3.</item>
/// <item>Changes to tiles, flags, fog, shadows, colours and the day/night cycle reach clients when they next load the
/// area.</item>
/// </list>
/// </summary>
static class AreaPlugin
{
    const string P = "Area";

    static AreaEnvironment Env(NwnxCall n, Area a) => AreaSettings.Of(n.World, a);
    static Area? PopArea(NwnxCall n) => n.PopObject<Area>();
    static void PushVector(NwnxCall n, Vector3 v) => n.Push(new NwnxValue(NwnxType.Vector, v));

    [NwnxFunc(P, "GetNumberOfPlayersInArea")]
    static void Players(NwnxCall n)
    {
        var a = PopArea(n);
        n.Push(a == null ? 0 : n.World.ConnectedPCs.Count(pc => pc.Area == a));
    }

    static uint Live(NwnxCall n, uint id) => n.World.Get(id) != null ? id : GameObject.InvalidId;
    [NwnxFunc(P, "GetLastEntered")] static void LastEntered(NwnxCall n) => n.PushObject(PopArea(n) is { } a ? Live(n, a.LastEntered) : GameObject.InvalidId);
    [NwnxFunc(P, "GetLastLeft")] static void LastLeft(NwnxCall n) => n.PushObject(PopArea(n) is { } a ? Live(n, a.LastLeft) : GameObject.InvalidId);

    // ---- settings ----

    [NwnxFunc(P, "GetPVPSetting")] static void GetPvp(NwnxCall n) => n.Push(PopArea(n) is { } a ? (int)a.Are.GetInt("PlayerVsPlayer", 3) : 0);
    [NwnxFunc(P, "SetPVPSetting")]
    static void SetPvp(NwnxCall n)
    {
        var a = PopArea(n); int v = n.PopInt();
        if (a != null && v is >= 0 and <= 3) a.Are.SetByte("PlayerVsPlayer", v);
    }

    [NwnxFunc(P, "GetAreaSpotModifier")] static void GetSpot(NwnxCall n) => n.Push(PopArea(n) is { } a ? (int)a.Are.GetInt("ModSpotCheck") : 0);
    [NwnxFunc(P, "SetAreaSpotModifier")]
    static void SetSpot(NwnxCall n)
    {
        var a = PopArea(n); int v = n.PopInt();
        a?.Are.SetInt("ModSpotCheck", v);
    }
    [NwnxFunc(P, "GetAreaListenModifier")] static void GetListen(NwnxCall n) => n.Push(PopArea(n) is { } a ? (int)a.Are.GetInt("ModListenCheck") : 0);
    [NwnxFunc(P, "SetAreaListenModifier")]
    static void SetListen(NwnxCall n)
    {
        var a = PopArea(n); int v = n.PopInt();
        a?.Are.SetInt("ModListenCheck", v);
    }

    [NwnxFunc(P, "GetNoRestingAllowed")] static void GetNoRest(NwnxCall n) => n.Push(PopArea(n) is { } a && Env(n, a).NoRest);
    [NwnxFunc(P, "SetNoRestingAllowed")]
    static void SetNoRest(NwnxCall n)
    {
        var a = PopArea(n); bool v = n.PopInt() != 0;
        if (a == null) return;
        Env(n, a).NoRest = v;
        a.Are.SetByte("NoRest", v ? 1 : 0);
    }

    [NwnxFunc(P, "GetWindPower")] static void GetWindPower(NwnxCall n) => n.Push(PopArea(n) is { } a ? Env(n, a).WindPower : 0);
    [NwnxFunc(P, "SetWindPower")]
    static void SetWindPower(NwnxCall n)
    {
        var a = PopArea(n); int v = n.PopInt();
        if (a == null || v is < 0 or > 2) return;
        Env(n, a).WindPower = v;
        a.Are.SetInt("WindPower", v);
    }

    [NwnxFunc(P, "GetAreaWind")]
    static void GetWind(NwnxCall n)
    {
        var env = PopArea(n) is { } a ? Env(n, a) : null;
        bool set = env is { WindScripted: true };
        PushVector(n, set ? env!.WindDirection : Vector3.Zero);
        n.Push(set ? env!.WindMagnitude : 0f);
        n.Push(set ? env!.WindYaw : 0f);
        n.Push(set ? env!.WindPitch : 0f);
    }

    static readonly string[] WeatherFields = { "ChanceRain", "ChanceSnow", "ChanceLightning" };

    [NwnxFunc(P, "GetWeatherChance")]
    static void GetWeather(NwnxCall n)
    {
        var a = PopArea(n); int type = n.PopInt();
        if (a == null) { n.Push(0); return; }
        var env = Env(n, a);
        n.Push(type switch { 0 => env.ChanceRain, 1 => env.ChanceSnow, 2 => env.ChanceLightning, _ => 0 });
    }
    [NwnxFunc(P, "SetWeatherChance")]
    static void SetWeather(NwnxCall n)
    {
        var a = PopArea(n); int type = n.PopInt(), chance = n.PopInt();
        if (a == null || type is < 0 or > 2 || chance is < 0 or > 100) return;
        var env = Env(n, a);
        switch (type)
        {
            case 0: env.ChanceRain = chance; break;
            case 1: env.ChanceSnow = chance; break;
            default: env.ChanceLightning = chance; break;
        }
        a.Are.SetInt(WeatherFields[type], chance);
    }

    [NwnxFunc(P, "GetFogClipDistance")] static void GetFogClip(NwnxCall n) => n.Push(PopArea(n) is { } a ? a.Are.GetFloat("FogClipDist", 45f) : 0f);
    [NwnxFunc(P, "SetFogClipDistance")]
    static void SetFogClip(NwnxCall n)
    {
        var a = PopArea(n); float v = n.PopFloat();
        if (a != null && v >= 0) a.Are.SetFloat("FogClipDist", v);
    }

    [NwnxFunc(P, "GetShadowOpacity")] static void GetShadow(NwnxCall n) => n.Push(PopArea(n) is { } a ? (int)a.Are.GetInt("ShadowOpacity", 60) : 0);
    [NwnxFunc(P, "SetShadowOpacity")]
    static void SetShadow(NwnxCall n)
    {
        var a = PopArea(n); int v = n.PopInt();
        if (a != null && v is >= 0 and <= 100) a.Are.SetByte("ShadowOpacity", v);
    }

    /// <summary>NWNX_AREA_DAYNIGHTCYCLE_*: 0 the area follows the clock, 1 always bright, 2 always dark (ARE DayNightCycle, IsNight).</summary>
    [NwnxFunc(P, "GetDayNightCycle")]
    static void GetDayNight(NwnxCall n)
    {
        var a = PopArea(n);
        n.Push(a == null ? 0 : a.Are.GetInt("DayNightCycle") != 0 ? 0 : a.Are.GetInt("IsNight") != 0 ? 2 : 1);
    }
    [NwnxFunc(P, "SetDayNightCycle")]
    static void SetDayNight(NwnxCall n)
    {
        var a = PopArea(n); int type = n.PopInt();
        if (a == null || type is < 0 or > 2) return;
        a.Are.SetByte("DayNightCycle", type == 0 ? 1 : 0);
        a.Are.SetByte("IsNight", type == 2 ? 1 : 0);
    }

    static readonly string[] ColorFields = { "MoonAmbientColor", "MoonDiffuseColor", "SunAmbientColor", "SunDiffuseColor" };

    [NwnxFunc(P, "GetSunMoonColors")]
    static void GetColors(NwnxCall n)
    {
        var a = PopArea(n); int type = n.PopInt();
        n.Push(a == null ? -1 : type is >= 0 and <= 3 ? (int)(uint)a.Are.GetInt(ColorFields[type]) : 0);
    }
    [NwnxFunc(P, "SetSunMoonColors")]
    static void SetColors(NwnxCall n)
    {
        var a = PopArea(n); int type = n.PopInt(), color = n.PopInt();
        if (a == null || type is < 0 or > 3 || color < 0) return;
        uint rgb = (uint)color & 0xFFFFFF; // masked, not clamped: 0x1000001 reads 0x010000 (nwnx_area_props)
        a.Are.SetDWord(ColorFields[type], ((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF));
    }

    [NwnxFunc(P, "GetAreaFlags")] static void GetFlags(NwnxCall n) => n.Push(PopArea(n) is { } a ? (int)(uint)a.Are.GetInt("Flags") : -1);
    [NwnxFunc(P, "SetAreaFlags")]
    static void SetFlags(NwnxCall n)
    {
        var a = PopArea(n); int v = n.PopInt();
        a?.Are.SetDWord("Flags", unchecked((uint)v));
    }

    [NwnxFunc(P, "GetMusicIsPlaying")]
    static void MusicPlaying(NwnxCall n)
    {
        var a = PopArea(n); bool battle = n.PopInt() != 0;
        n.Push(a != null && (battle ? Env(n, a).BattlePlaying : Env(n, a).MusicPlaying));
    }

    [NwnxFunc(P, "GetAmbientSoundDay")] static void AmbientDay(NwnxCall n) => n.Push(PopArea(n) is { } a ? Env(n, a).AmbientDay : 0);
    [NwnxFunc(P, "GetAmbientSoundNight")] static void AmbientNight(NwnxCall n) => n.Push(PopArea(n) is { } a ? Env(n, a).AmbientNight : 0);
    [NwnxFunc(P, "GetAmbientSoundDayVolume")] static void AmbientDayVolume(NwnxCall n) => n.Push(PopArea(n) is { } a ? Env(n, a).AmbientDayVolume : 0);
    [NwnxFunc(P, "GetAmbientSoundNightVolume")] static void AmbientNightVolume(NwnxCall n) => n.Push(PopArea(n) is { } a ? Env(n, a).AmbientNightVolume : 0);

    // ---- tiles ----

    /// <summary>The tile index under (x, y) metres, or -1 outside the area.</summary>
    static int TileAt(Area a, float x, float y)
    {
        int tx = (int)(x / Area.TileSize), ty = (int)(y / Area.TileSize);
        if (tx >= a.Width || ty >= a.Height) return -1;
        int i = ty * a.Width + tx;
        return i < a.Tiles.Length ? i : -1;
    }

    static void PushTileInfo(NwnxCall n, Area a, int i)
    {
        if (i < 0) { for (int k = 0; k < 5; k++) n.Push(-1); return; }
        var t = a.Tiles[i];
        n.Push(t.Id);
        n.Push((int)t.Height);
        n.Push((int)t.Orientation);
        n.Push(i % a.Width);
        n.Push(i / a.Width);
    }

    [NwnxFunc(P, "GetTileInfo")]
    static void TileInfo(NwnxCall n)
    {
        var a = PopArea(n); float x = n.PopFloat(), y = n.PopFloat();
        if (a == null) { for (int k = 0; k < 5; k++) n.Push(-1); return; }
        if (x < 0 || y < 0) return;
        PushTileInfo(n, a, TileAt(a, x, y));
    }

    [NwnxFunc(P, "GetTileInfoByTileIndex")]
    static void TileInfoByIndex(NwnxCall n)
    {
        var a = PopArea(n); int i = n.PopInt();
        if (a == null) { for (int k = 0; k < 5; k++) n.Push(-1); return; }
        if (i < 0) return;
        PushTileInfo(n, a, i < a.Tiles.Length && i < a.Width * a.Height ? i : -1);
    }

    [NwnxFunc(P, "GetTileModelResRef")]
    static void TileModel(NwnxCall n)
    {
        var a = PopArea(n); float x = n.PopFloat(), y = n.PopFloat();
        if (a == null) { n.Push(""); return; }
        if (x < 0 || y < 0) return;
        int i = TileAt(a, x, y);
        n.Push(i < 0 ? "" : TilesetPlugin.SetFile(n.World, a.Tileset)?.Get($"TILE{a.Tiles[i].Id}", "Model") ?? "");
    }

    [NwnxFunc(P, "GetTileAnimationLoop")]
    static void GetAnimLoop(NwnxCall n)
    {
        var a = PopArea(n); float x = n.PopFloat(), y = n.PopFloat(); int loop = n.PopInt();
        if (a == null) { n.Push(-1); return; }
        if (loop is < 1 or > 3 || x < 0 || y < 0) return;
        int i = TileAt(a, x, y);
        n.Push(i < 0 ? -1 : (a.Tiles[i].AnimLoops >> (loop - 1)) & 1);
    }

    /// <summary>Takes effect when clients next load the area (as in the original).</summary>
    [NwnxFunc(P, "SetTileAnimationLoop")]
    static void SetAnimLoop(NwnxCall n)
    {
        var a = PopArea(n); float x = n.PopFloat(), y = n.PopFloat(); int loop = n.PopInt(); bool on = n.PopInt() != 0;
        if (a == null || loop is < 1 or > 3 || x < 0 || y < 0) return;
        int i = TileAt(a, x, y);
        if (i < 0) return;
        int bit = 1 << (loop - 1);
        a.Tiles[i].AnimLoops = (byte)(on ? a.Tiles[i].AnimLoops | bit : a.Tiles[i].AnimLoops & ~bit);
    }

    /// <summary>
    /// Whether a creature of personal space <c>fPerSpace</c> could walk straight between two points: 1 clear, -1 blocked
    /// by terrain (tile ground missing or too steep a step), -2 by a placeable or a closed door (doors unless
    /// ignored), -3 by a creature (not one standing at the start); the first obstruction along the line decides. Implemented from the header: the
    /// call crashed nwserver on the generated parity area, so it is unmeasured. The height is not used.
    /// </summary>
    [NwnxFunc(P, "TestDirectLine")]
    static void TestDirectLine(NwnxCall n)
    {
        var area = PopArea(n);
        float sx = n.PopFloat(), sy = n.PopFloat(), ex = n.PopFloat(), ey = n.PopFloat(), space = n.PopFloat();
        n.PopFloat();
        bool ignoreDoors = n.PopInt() != 0;
        if (area == null) { n.Push(0); return; }
        var from = new Vector2(sx, sy);
        var dir = new Vector2(ex, ey) - from;
        float length = dir.Length();
        // The first creature in the way: the distance along the line where its space meets the walker's.
        float creatureAt = float.MaxValue;
        foreach (var c in area.Objects.OfType<Creature>())
        {
            if (c.IsDestroyed || c.IsDead) continue;
            var p = new Vector2(c.Position.X, c.Position.Y);
            float r = PersonalSpace.Radius(n.World, c);
            if (Vector2.Distance(p, from) < r) continue; // the creature standing at the start: the walker itself
            float t = length > 0 ? Math.Clamp(Vector2.Dot(p - from, dir) / (length * length), 0, 1) : 0;
            if (Vector2.Distance(p, from + dir * t) < space + r) creatureAt = MathF.Min(creatureAt, t * length);
        }
        var wm = area.Walkmesh;
        float? z = wm.GroundHeight(sx, sy, 1000f, 2000f, tilesOnly: true);
        int steps = Math.Max(1, (int)(length / 0.25f));
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            if (t * length > creatureAt) break;
            float x = sx + dir.X * t, y = sy + dir.Y * t;
            var ground = z is { } nz ? wm.GroundHeight(x, y, nz, 1f, tilesOnly: true) : null;
            if (ground == null) { n.Push(-1); return; }
            z = ground;
            if (wm.GroundHeight(x, y, ground.Value, 1f, ignoreClosedDoors: ignoreDoors) == null) { n.Push(-2); return; }
        }
        n.Push(creatureAt < float.MaxValue ? -3 : 1);
    }

    [NwnxFunc(P, "GetPathExists")]
    static void PathExists(NwnxCall n)
    {
        var a = PopArea(n); var start = n.PopVector(); var end = n.PopVector(); int depth = n.PopInt();
        if (a == null) { n.Push(0); return; }
        if (depth <= 0) return;
        n.Push(TilePathCheck.Exists(a, start, end, depth));
    }

    [NwnxFunc(P, "RotateArea")]
    static void Rotate(NwnxCall n)
    {
        var a = PopArea(n); int turns = n.PopInt();
        if (a != null && turns is >= 1 and <= 3) AreaRotation.Rotate(n.World, a, turns);
    }

    // ---- objects ----

    static List<Vector3> Square(float x, float y, float size) =>
        [new(x - size, y + size, 0), new(x - size, y - size, 0), new(x + size, y - size, 0), new(x + size, y + size, 0)];

    static Trigger PlaceTrigger(NwnxCall n, Area a, Vector3 position, float size, string tag, int type)
    {
        var t = new Trigger { Position = position, TriggerType = type, Tag = tag, Geometry = Square(position.X, position.Y, size) };
        n.World.AddToArea(t, a, position, 0f);
        return t;
    }

    /// <summary>A square transition trigger (sides of twice <c>size</c>) leading to a door or waypoint; "AreaTransition" by default.</summary>
    [NwnxFunc(P, "CreateTransition")]
    static void CreateTransition(NwnxCall n)
    {
        var a = PopArea(n); var target = n.PopObject();
        float x = n.PopFloat(), y = n.PopFloat(), z = n.PopFloat(), size = n.PopFloat(); var tag = n.PopString();
        if (a == null || target is not (Door or Waypoint)) { n.PushObject(GameObject.InvalidId); return; }
        var t = PlaceTrigger(n, a, new Vector3(x, y, z), size, tag.Length > 0 ? tag : "AreaTransition", Trigger.TypeTransition);
        t.LinkedTo = target.Tag;
        t.LinkedToFlags = (byte)(target is Door ? 1 : 2);
        t.TransitionTargetId = target.Id;
        t.TransitionResolved = true;
        n.PushObject(t.Id);
    }

    /// <summary>A square generic trigger (sides of twice <c>size</c>) with no scripts; "GenericTrigger" by default.</summary>
    [NwnxFunc(P, "CreateGenericTrigger")]
    static void CreateGenericTrigger(NwnxCall n)
    {
        var a = PopArea(n); float x = n.PopFloat(), y = n.PopFloat(), z = n.PopFloat(); var tag = n.PopString(); float size = n.PopFloat();
        if (a == null) { n.PushObject(GameObject.InvalidId); return; }
        n.PushObject(PlaceTrigger(n, a, new Vector3(x, y, z), size, tag.Length > 0 ? tag : "GenericTrigger", Trigger.TypeGeneric).Id);
    }

    /// <summary>A sound object from a UTS blueprint. Like the original's, it reports no resref.</summary>
    [NwnxFunc(P, "CreateSoundObject")]
    static void CreateSound(NwnxCall n)
    {
        var a = PopArea(n); var position = n.PopVector(); var resref = n.PopString();
        var uts = resref.Length > 0 ? n.World.Resources.ReadGff(resref, ResType.Uts) : null;
        if (a == null || uts == null || n.World.Loader == null) { n.PushObject(GameObject.InvalidId); return; }
        var s = n.World.Loader.CreateSound(a, uts, position);
        s.ResRef = "";
        n.World.AddToArea(s, a, position, 0f);
        n.PushObject(s.Id);
    }

    [NwnxFunc(P, "SetDefaultObjectUiDiscoveryMask")]
    static void DiscoveryMask(NwnxCall n)
    {
        uint area = n.PopObjectId(); int types = n.PopInt(), mask = n.PopInt(); bool force = n.PopInt() != 0;
        if (area != GameObject.InvalidId && n.World.Get(area) is not Area) return;
        n.World.System<UiDiscoveryDefaults>().Set(area, types, mask, force);
    }

    // ---- export ----

    const string KeyExcluded = "area:exportExcluded";

    static HashSet<uint> Excluded(NwnxCall n)
    {
        var state = n.State(GameObject.InvalidId);
        if (state.TryGetValue(KeyExcluded, out var v) && v.Value is HashSet<uint> set) return set;
        set = new HashSet<uint>();
        state[KeyExcluded] = new NwnxValue(NwnxType.Object, set);
        return set;
    }

    [NwnxFunc(P, "AddObjectToExclusionList")] static void Exclude(NwnxCall n) => Excluded(n).Add(n.PopObjectId());
    [NwnxFunc(P, "RemoveObjectFromExclusionList")] static void Include(NwnxCall n) => Excluded(n).Remove(n.PopObjectId());

    /// <summary>The folder exports go to: the user directory's nwnx folder. Any alias names it (no other alias is defined).</summary>
    static string ExportPath(NwnxCall n, string file)
    {
        var dir = Path.Combine(n.World.System<PersistenceSystem>().UserRoot, "nwnx");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, file);
    }

    static bool ValidFileName(string name) => name.Length is > 0 and <= 16 && GamePaths.IsPlainName(name);

    /// <summary>OBJECT_TYPE_* bits and the GIT lists they filter out.</summary>
    static readonly (int Type, string List)[] FilterLists =
    {
        (1, "Creature List"), (2, "List"), (4, "TriggerList"), (8, "Door List"), (16, "AreaEffectList"),
        (32, "WaypointList"), (64, "Placeable List"), (128, "StoreList"), (256, "Encounter List"),
    };

    /// <summary>
    /// Writes the area's objects as they are now to a GIT (players and excluded objects left out; filtered types'
    /// lists left out entirely), with the area's properties, its variables and UUID if asked, the top-level fields
    /// the original writes (parity suite nwnx_area_objects compares the files). The objects themselves are in
    /// OpenWinter's save format. Areas made from the file get them on the server's next start, as in the original.
    /// </summary>
    [NwnxFunc(P, "ExportGIT")]
    static void ExportGit(NwnxCall n)
    {
        var a = PopArea(n); var file = n.PopString(); bool vars = n.PopInt() != 0, uuid = n.PopInt() != 0; int filter = n.PopInt(); n.PopString();
        if (a == null) { n.Push(0); return; }
        if (file.Length == 0) file = a.ResRef;
        if (file.Length > 16) return;
        if (!ValidFileName(file)) { n.Push(0); return; }
        var excluded = Excluded(n);
        excluded.RemoveWhere(id => n.World.Get(id) == null);
        var git = AuroraSaveResources.Git(n.World, a, n.Registry.Log, objectIds: false, include: o => !excluded.Contains(o.Id));
        git.SetStruct("AreaProperties", AreaProperties(a, Env(n, a)));
        foreach (var (type, list) in FilterLists)
            if ((filter & type) != 0) git.Remove(list);
        if (vars) ObjectSerializer.WriteLocals(git, a.Locals);
        if (uuid && a.Uuid is { } id) git.SetString("UUID", id);
        n.Push(TryWrite(n, ExportPath(n, file.ToLowerInvariant() + ".git"), Gff.Write(git, "GIT ")));
    }

    /// <summary>Writes the area's ARE as it is now, named <c>sFileName</c> (its new resref), with an optional new name and tag.</summary>
    [NwnxFunc(P, "ExportARE")]
    static void ExportAre(NwnxCall n)
    {
        var a = PopArea(n); var file = n.PopString(); var name = n.PopString(); var tag = n.PopString(); n.PopString();
        if (a == null) { n.Push(0); return; }
        if (file.Length > 16) return;
        if (!ValidFileName(file)) { n.Push(0); return; }
        var are = Are(a, Env(n, a), file.ToLowerInvariant(), name.Length > 0 ? new LocString(name) : a.Name.Clone(), tag.Length > 0 ? tag : a.Tag);
        n.Push(TryWrite(n, ExportPath(n, file.ToLowerInvariant() + ".are"), Gff.Write(are, "ARE ")));
    }

    static bool TryWrite(NwnxCall n, string path, byte[] data)
    {
        try { File.WriteAllBytes(path, data); return true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            n.Registry.Log($"[nwnx] export to {path} failed: {e.Message}");
            return false;
        }
    }

    /// <summary>The exported ARE: the fields (and types) the original writes, from the area as it is now.</summary>
    static GffStruct Are(Area a, AreaEnvironment env, string resref, LocString name, string tag)
    {
        var src = a.Are;
        uint U(string f) => (uint)src.GetInt(f);
        var are = new GffStruct()
            .SetLocString("Name", name).SetString("Tag", tag).SetString("ResRef", resref)
            .SetInt("Width", a.Width).SetInt("Height", a.Height).SetResRef("Tileset", a.Tileset)
            .SetInt("ChanceLightning", env.ChanceLightning).SetInt("ChanceRain", env.ChanceRain).SetInt("ChanceSnow", env.ChanceSnow)
            .SetByte("DayNightCycle", (int)src.GetInt("DayNightCycle")).SetDWord("Flags", U("Flags")).SetFloat("FogClipDist", src.GetFloat("FogClipDist", 45f))
            .SetByte("IsNight", (int)src.GetInt("IsNight")).SetByte("LightingScheme", (int)src.GetInt("LightingScheme"))
            .SetWord("LoadScreenID", (int)src.GetInt("LoadScreenID")).SetInt("ModListenCheck", (int)src.GetInt("ModListenCheck"))
            .SetInt("ModSpotCheck", (int)src.GetInt("ModSpotCheck")).SetDWord("MoonAmbientColor", U("MoonAmbientColor"))
            .SetDWord("MoonDiffuseColor", U("MoonDiffuseColor")).SetByte("MoonFogAmount", env.MoonFog).SetDWord("MoonFogColor", U("MoonFogColor"))
            .SetByte("MoonShadows", (int)src.GetInt("MoonShadows")).SetByte("NoRest", env.NoRest ? 1 : 0)
            .SetResRef("OnHeartbeat", a.Script("OnHeartbeat")).SetResRef("OnUserDefined", a.Script("OnUserDefined"))
            .SetResRef("OnEnter", a.Script("OnEnter")).SetResRef("OnExit", a.Script("OnExit"))
            .SetByte("PlayerVsPlayer", (int)src.GetInt("PlayerVsPlayer", 3)).SetByte("ShadowOpacity", (int)src.GetInt("ShadowOpacity", 60))
            .SetByte("SkyBox", env.SkyBox).SetDWord("SunAmbientColor", U("SunAmbientColor")).SetDWord("SunDiffuseColor", U("SunDiffuseColor"))
            .SetByte("SunFogAmount", env.SunFog).SetDWord("SunFogColor", U("SunFogColor")).SetByte("SunShadows", (int)src.GetInt("SunShadows"))
            .SetInt("WindPower", env.WindPower);
        are.SetList("Tile_List", AuroraSaveResources.Are(a).GetList("Tile_List"));
        return are;
    }

    /// <summary>The exported GIT's AreaProperties: music and ambient sound, light, fog, sky, grass and wind as they are now.</summary>
    static GffStruct AreaProperties(Area a, AreaEnvironment env)
    {
        var are = a.Are;
        var sun = env.SunDirectionSet ? env.SunDirection : DefaultLightDirection;
        var moon = env.MoonDirectionSet ? env.MoonDirection : DefaultLightDirection;
        var wind = env.WindScripted ? env.WindDirection : Vector3.Zero;
        return new GffStruct(100)
            .SetInt("AmbientSndDay", env.AmbientDay).SetInt("AmbientSndNight", env.AmbientNight)
            .SetInt("AmbientSndDayVol", env.AmbientDayVolume).SetInt("AmbientSndNitVol", env.AmbientNightVolume)
            .SetInt("MusicBattle", env.MusicBattle).SetInt("MusicDay", env.MusicDay).SetInt("MusicNight", env.MusicNight).SetInt("MusicDelay", env.MusicDelay)
            .SetByte("SkyBox", env.SkyBox).SetDWord("SunAmbientColor", (uint)are.GetInt("SunAmbientColor")).SetDWord("SunDiffuseColor", (uint)are.GetInt("SunDiffuseColor"))
            .SetDWord("SunFogColor", (uint)are.GetInt("SunFogColor")).SetByte("SunFogAmount", env.SunFog)
            .SetDWord("MoonAmbientColor", (uint)are.GetInt("MoonAmbientColor")).SetDWord("MoonDiffuseColor", (uint)are.GetInt("MoonDiffuseColor"))
            .SetDWord("MoonFogColor", (uint)are.GetInt("MoonFogColor")).SetByte("MoonFogAmount", env.MoonFog)
            .SetFloat("SunDirectionX", sun.X).SetFloat("SunDirectionY", sun.Y).SetFloat("SunDirectionZ", sun.Z)
            .SetFloat("MoonDirectionX", moon.X).SetFloat("MoonDirectionY", moon.Y).SetFloat("MoonDirectionZ", moon.Z)
            .SetFloat("WindDirectionX", wind.X).SetFloat("WindDirectionY", wind.Y).SetFloat("WindDirectionZ", wind.Z)
            .SetFloat("WindMagnitude", env.WindScripted ? env.WindMagnitude : 0f).SetFloat("WindYaw", env.WindScripted ? env.WindYaw : 0f)
            .SetFloat("WindPitch", env.WindScripted ? env.WindPitch : 0f)
            .SetString("DisplayName", "").SetByte("GrassDefDisabled", env.DefaultGrassDisabled ? 1 : 0);
    }

    /// <summary>The light direction an area reports when no script has set one (as GetAreaLightDirection).</summary>
    static readonly Vector3 DefaultLightDirection = new(0.433224291f, 0.487377316f, 0.758142531f);
}
