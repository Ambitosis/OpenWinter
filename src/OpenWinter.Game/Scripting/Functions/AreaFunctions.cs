using System.Numerics;
using System.Text.Json.Nodes;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>
/// Areas (properties, weather, fog, sky, lighting, tiles, exploration, instancing),
/// transitions, encounters, visual transforms and other client-side overrides.
/// </summary>
static class AreaFunctions
{
    static float[] Vec(Vector3 v) => SoundService.Vec(v);

    /// <summary>"oArea" arguments: an area, the area of an object, or the caller's area when invalid.</summary>
    static Area? AreaArg(Call c, int i)
    {
        var target = c.Obj(i);
        if (target == null && c.O(i) != GameObject.InvalidId) return null; // a stale object, not OBJECT_INVALID
        return AreaSettings.Resolve(target, c.Self);
    }

    static AreaEnvironment Env(Call c, Area a) => AreaSettings.Of(c.World, a);

    // ---- area properties ----

    [NwFunc("GetIsAreaInterior")] static object? IsInterior(Call c) => AreaArg(c, 0) is { } a && (a.IsInterior || a.IsUnderground) ? 1 : 0;
    [NwFunc("GetIsAreaNatural")] static object? IsNatural(Call c) => c.Obj(0) is Area a ? (a.IsNatural ? 1 : 0) : -1;
    [NwFunc("GetIsAreaAboveGround")] static object? IsAboveGround(Call c) => c.Obj(0) is Area a ? (a.IsUnderground ? 0 : 1) : -1;
    [NwFunc("GetTilesetResRef")] static object? Tileset(Call c) => c.Obj(0) is Area a ? a.Tileset : "";
    [NwFunc("GetAreaSize")] static object? AreaSize(Call c) => AreaArg(c, 1) is { } a ? c.I(0) switch { 0 => a.Height, 1 => a.Width, _ => 0 } : 0;
    [NwFunc("GetAreaNoRestFlag")] static object? NoRest(Call c) => AreaArg(c, 0) is { } a && Env(c, a).NoRest ? 1 : 0;
    [NwFunc("SetAreaNoRestFlag")]
    static object? SetNoRest(Call c)
    {
        if (AreaArg(c, 1) is { } a) Env(c, a).NoRest = c.I(0) != 0;
        return null;
    }

    [NwFunc("GetStartingLocation")]
    static object? StartingLocation(Call c)
    {
        var m = c.World.Module;
        if (c.World.FindArea(m.EntryArea) is not { } area) return Location.Invalid;
        float facing = MathF.Atan2(m.EntryDirection.Y, m.EntryDirection.X) * 180f / MathF.PI;
        return new Location(area.Id, m.EntryPosition, facing < 0 ? facing + 360f : facing);
    }

    [NwFunc("GetSurfaceMaterial")]
    static object? SurfaceMaterial(Call c)
    {
        var loc = c.L(0);
        return c.World.Get(loc.Area) is Area a ? a.Walkmesh.SurfaceAt(loc.Position) : 0;
    }

    [NwFunc("GetGroundHeight")]
    static object? GroundHeight(Call c)
    {
        var loc = c.L(0);
        if (c.World.Get(loc.Area) is not Area a) return -6f;
        var p = loc.Position;
        return a.Walkmesh.GroundHeight(p.X, p.Y, p.Z, 1000f) ?? a.Walkmesh.HighestGround(p.X, p.Y) ?? -6f;
    }

    // ---- area iteration and instancing ----

    static readonly Dictionary<ScriptHost, int> _areaCursor = new();

    [NwFunc("GetFirstArea")]
    static object? FirstArea(Call c)
    {
        _areaCursor[c.Host] = 0;
        return c.World.Areas.Count > 0 ? c.World.Areas[0].Id : GameObject.InvalidId;
    }
    [NwFunc("GetNextArea")]
    static object? NextArea(Call c)
    {
        if (!_areaCursor.TryGetValue(c.Host, out int i) || i >= c.World.Areas.Count) return GameObject.InvalidId;
        _areaCursor[c.Host] = ++i;
        return i < c.World.Areas.Count ? c.World.Areas[i].Id : GameObject.InvalidId;
    }

    [NwFunc("CreateArea")] static object? CreateArea(Call c) => AreaInstancing.Create(c.World, c.S(0), ObjectFunctions.CleanTag(c.S(1)), c.S(2))?.Id ?? GameObject.InvalidId;
    [NwFunc("CopyArea")]
    static object? CopyArea(Call c) =>
        c.Obj(0) is Area a ? AreaInstancing.Copy(c.World, a, ObjectFunctions.CleanTag(c.S(1)), c.S(2))?.Id ?? GameObject.InvalidId : GameObject.InvalidId;
    /// <summary>The result comes at once; the area goes when the running script has finished (like DestroyObject).</summary>
    [NwFunc("DestroyArea")]
    static object? DestroyArea(Call c)
    {
        int result = AreaInstancing.CanDestroy(c.World, c.Obj(0));
        if (result == 1 && c.Obj(0) is Area a) c.World.After(0, () => AreaInstancing.Destroy(c.World, a));
        return result;
    }

    // ---- transitions ----

    [NwFunc("GetTransitionTarget")]
    static object? TransitionTarget(Call c) =>
        c.Obj(0) is { } o ? TransitionService.ResolveTarget(c.World, o)?.Id ?? GameObject.InvalidId : GameObject.InvalidId;

    [NwFunc("SetTransitionTarget")]
    static object? SetTransitionTarget(Call c)
    {
        if (c.Obj(0) is not { } o || o is Area) return null;
        var target = c.Obj(1);
        o.TransitionTargetId = target is { Area: not null } ? target.Id : GameObject.InvalidId;
        o.TransitionResolved = true;
        c.World.MarkChanged(o);
        return null;
    }

    [NwFunc("SetAreaTransitionBMP")]
    static object? TransitionBmp(Call c)
    {
        if (c.Self is Creature { IsPC: true } pc)
            c.World.Output?.Notify("area-transition-bmp", pc, new { predefined = c.I(0), custom = c.S(1) }, pc);
        return null;
    }

    // ---- encounters ----

    static Encounter? Enc(Call c, int i) => c.ObjOrSelf(i) as Encounter;

    [NwFunc("GetEncounterActive")] static object? EncActive(Call c) => Enc(c, 0)?.ActiveValue ?? 0;
    [NwFunc("SetEncounterActive")] static object? SetEncActive(Call c) { if (Enc(c, 1) is { } e) e.ActiveValue = c.I(0); return null; }
    [NwFunc("GetEncounterSpawnsMax")] static object? EncMax(Call c) => Enc(c, 0)?.State.SpawnsMax ?? 0;
    [NwFunc("SetEncounterSpawnsMax")] static object? SetEncMax(Call c) { if (Enc(c, 1) is { } e) e.State.SpawnsMax = c.I(0); return null; }
    [NwFunc("GetEncounterSpawnsCurrent")] static object? EncCurrent(Call c) => Enc(c, 0)?.State.SpawnsCurrent ?? 0;
    [NwFunc("SetEncounterSpawnsCurrent")] static object? SetEncCurrent(Call c) { if (Enc(c, 1) is { } e) e.State.SpawnsCurrent = c.I(0); return null; }
    // Measured (encounter_events): Get returns the encdifficulty.2da VALUE of the encounter's row (Hard reads 5), and Set
    // takes the ENCOUNTER_DIFFICULTY_* row, ignoring values outside 0..4.
    [NwFunc("GetEncounterDifficulty")]
    static object? EncDifficulty(Call c) =>
        Enc(c, 0) is { } e ? EncounterSystem.DifficultyValue(c.World, e.State.DifficultyIndex) : 0;
    [NwFunc("SetEncounterDifficulty")] static object? SetEncDifficulty(Call c) { if (Enc(c, 1) is { } e && c.I(0) is >= 0 and <= 4) e.State.DifficultyIndex = c.I(0); return null; }
    [NwFunc("GetIsEncounterCreature")]
    static object? IsEncCreature(Call c) =>
        c.ObjOrSelf(0) is Creature { SpawnedByEncounter: var id } && id != GameObject.InvalidId ? 1 : 0;

    [NwFunc("GetIsInSubArea")]
    static object? InSubArea(Call c)
    {
        uint creature = c.O(0);
        return c.ObjOrSelf(1) switch
        {
            Trigger t => t.Inside.Contains(creature) ? 1 : 0,
            Encounter e => e.State.Inside.Contains(creature) ? 1 : 0,
            // The creatures an area of effect holds (parity suite aoe_subarea): TRUE already in its OnEnter,
            // FALSE already in its OnExit; never for anything but a creature.
            AreaOfEffect a => c.Obj(0) is Creature && a.Inside.Contains(creature) ? 1 : 0,
            _ => 0,
        };
    }

    // ---- map ----

    [NwFunc("SetMapPinEnabled")]
    static object? MapPin(Call c)
    {
        if (c.Obj(0) is Waypoint w)
        {
            w.MapNoteEnabled = c.I(1) != 0;
            c.World.Output?.Notify("map-pin", w, new { enabled = w.MapNoteEnabled, note = w.MapNote.Resolve(c.World.Resources.Talk), position = Vec(w.Position) });
        }
        return null;
    }

    static bool[]? Explored(Call c, Area a, Creature pc, bool create)
    {
        var env = Env(c, a);
        if (env.Explored.TryGetValue(pc.Id, out var tiles)) return tiles;
        if (!create) return null;
        return env.Explored[pc.Id] = new bool[Math.Max(0, a.Width * a.Height)];
    }

    [NwFunc("ExploreAreaForPlayer")]
    static object? ExploreArea(Call c)
    {
        if (c.Obj(0) is not Area a || c.Obj<Creature>(1) is not { IsPC: true } pc) return null;
        bool explored = c.I(2, 1) != 0;
        Array.Fill(Explored(c, a, pc, true)!, explored);
        c.World.Output?.Notify("map-explored", a, new MapExploredNotice(a.Id, explored), pc);
        // Native re-explores the visible rooms at once while the player explores (minimapoff scene).
        if (!explored && pc.Area == a) c.World.System<WorldObjects.MapExploration>().RevealNow(pc);
        return null;
    }

    [NwFunc("SetTileExplored")]
    static object? SetTileExplored(Call c)
    {
        if (c.Obj<Creature>(0) is not { IsPC: true } pc || c.Obj(1) is not Area a) return -1;
        int x = c.I(2), y = c.I(3);
        if (x < 0 || y < 0 || x >= a.Width || y >= a.Height) return -1;
        var tiles = Explored(c, a, pc, true)!;
        int i = y * a.Width + x;
        bool before = tiles[i];
        tiles[i] = c.I(4) != 0;
        if (before != tiles[i]) c.World.Output?.Notify("tile-explored", a, new TileExploredNotice(a.Id, x, y, tiles[i]), pc);
        return before ? 1 : 0;
    }

    [NwFunc("GetTileExplored")]
    static object? GetTileExplored(Call c)
    {
        if (c.Obj<Creature>(0) is not { IsPC: true } pc || c.Obj(1) is not Area a) return -1;
        int x = c.I(2), y = c.I(3);
        if (x < 0 || y < 0 || x >= a.Width || y >= a.Height) return -1;
        return Explored(c, a, pc, false) is { } tiles && tiles[y * a.Width + x] ? 1 : 0;
    }

    // ---- weather, fog, sky, light ----

    [NwFunc("SetWeather")]
    static object? SetWeather(Call c)
    {
        int weather = c.I(1);
        IEnumerable<Area> areas = c.Obj(0) switch
        {
            Module => c.World.Areas.Where(a => !a.IsInterior),
            Area a when !a.IsInterior => new[] { a },
            _ => Array.Empty<Area>(),
        };
        foreach (var a in areas.ToList())
        {
            var env = Env(c, a);
            env.WeatherOverride = weather is >= 0 and <= 2 ? weather : -1;
            c.World.Output?.Notify("weather", a, WeatherSystem.Data(a, env));
        }
        return null;
    }

    [NwFunc("GetWeather")]
    static object? GetWeather(Call c)
    {
        if (c.Obj(0) is not Area a) return -1;
        var env = Env(c, a);
        if (env.NextWeatherRoll == 0)
        {
            WeatherSystem.Roll(env);
            env.NextWeatherRoll = c.World.Time + WeatherSystem.RollInterval;
        }
        return WeatherSystem.Current(a, env);
    }

    [NwFunc("SetFogAmount")]
    static object? SetFog(Call c)
    {
        if (AreaArg(c, 2) is not { } a) return null;
        var env = Env(c, a);
        int type = c.I(0), amount = Math.Clamp(c.I(1), 0, 100);
        if (type is 0 or 1) env.SunFog = amount;
        if (type is 0 or 2) env.MoonFog = amount;
        c.World.Output?.Notify("area-fog", a, new { sun = env.SunFog, moon = env.MoonFog });
        return null;
    }

    [NwFunc("GetFogAmount")]
    static object? GetFog(Call c)
    {
        if (AreaArg(c, 1) is not { } a) return 0;
        return c.I(0) switch { 1 => Env(c, a).SunFog, 2 => Env(c, a).MoonFog, _ => -1 };
    }

    [NwFunc("SetSkyBox")]
    static object? SetSkyBox(Call c)
    {
        if (AreaArg(c, 1) is not { } a) return null;
        Env(c, a).SkyBox = c.I(0);
        c.World.Output?.Notify("area-skybox", a, new { skybox = c.I(0) });
        return null;
    }
    [NwFunc("GetSkyBox")] static object? GetSkyBox(Call c) => AreaArg(c, 0) is { } a ? Env(c, a).SkyBox : 0;

    [NwFunc("GetAreaLightDirection")]
    static object? LightDirection(Call c)
    {
        if (AreaArg(c, 1) is not { } a) return Vector3.Zero;
        var env = Env(c, a);
        return c.I(0) switch
        {
            1 => env.SunDirectionSet ? env.SunDirection : DefaultLightDirection,
            0 => env.MoonDirectionSet ? env.MoonDirection : DefaultLightDirection,
            _ => new Vector3(-1e7f),
        };
    }

    /// <summary>The original's light direction when an area sets none.</summary>
    static readonly Vector3 DefaultLightDirection = new(0.433224291f, 0.487377316f, 0.758142531f);
    [NwFunc("SetAreaLightDirection")]
    static object? SetLightDirection(Call c)
    {
        if (AreaArg(c, 2) is not { } a) return null;
        var env = Env(c, a);
        var dir = CoreFunctions.NormalizeVector(c.V(1)); // stored normalised, as the original reports it back
        if (c.I(0) == 1) { env.SunDirection = dir; env.SunDirectionSet = true; }
        else { env.MoonDirection = dir; env.MoonDirectionSet = true; }
        c.World.Output?.Notify("area-light-direction", a, new { type = c.I(0), direction = Vec(c.V(1)), fade = c.F(3) });
        return null;
    }

    [NwFunc("SetAreaWind")]
    static object? SetWind(Call c)
    {
        if (c.Obj(0) is not Area a) return null;
        var env = Env(c, a);
        env.WindDirection = c.V(1);
        env.WindMagnitude = c.F(2);
        env.WindYaw = c.F(3);
        env.WindPitch = c.F(4);
        c.World.Output?.Notify("area-wind", a, new { direction = Vec(env.WindDirection), magnitude = env.WindMagnitude, yaw = env.WindYaw, pitch = env.WindPitch });
        return null;
    }

    [NwFunc("RecomputeStaticLighting")]
    static object? RecomputeLighting(Call c)
    {
        if (c.Obj(0) is not Area a) return null;
        Env(c, a).LightingDirty = false;
        var lights = a.Objects.OfType<Placeable>().Select(p => new { id = p.Id, on = p.Illuminated }).ToList();
        c.World.Output?.Notify("area-recompute-lighting", a, new { placeables = lights });
        return null;
    }

    [NwFunc("DayToNight")] static object? DayToNight(Call c) => DayNight(c, true);
    [NwFunc("NightToDay")] static object? NightToDay(Call c) => DayNight(c, false);
    static object? DayNight(Call c, bool night)
    {
        if (c.Obj<Creature>(0) is { IsPC: true } pc) c.World.Output?.Notify("day-night", pc, new { night, transition = c.F(1) }, pc);
        return null;
    }

    // ---- tiles ----

    static (Area Area, int Index)? TileAt(Call c, Location loc)
    {
        if (c.World.Get(loc.Area) is not Area a) return null;
        int x = (int)MathF.Floor(loc.Position.X / Area.TileSize), y = (int)MathF.Floor(loc.Position.Y / Area.TileSize);
        if (x < 0 || y < 0 || x >= a.Width || y >= a.Height) return null;
        int i = y * a.Width + x;
        return i < a.Tiles.Length ? (a, i) : null;
    }

    [NwFunc("GetTileID")] static object? TileId(Call c) => TileAt(c, c.L(0)) is var (a, i) ? a.Tiles[i].Id : -1;
    [NwFunc("GetTileOrientation")] static object? TileOrientation(Call c) => TileAt(c, c.L(0)) is var (a, i) ? a.Tiles[i].Orientation : -1;
    [NwFunc("GetTileHeight")] static object? TileHeight(Call c) => TileAt(c, c.L(0)) is var (a, i) ? a.Tiles[i].Height : -1;

    static object TileData(Area a, int i) => new
    {
        index = i, id = a.Tiles[i].Id, orientation = (int)a.Tiles[i].Orientation, height = (int)a.Tiles[i].Height,
        animLoop1 = (a.Tiles[i].AnimLoops & 1) != 0, animLoop2 = (a.Tiles[i].AnimLoops & 2) != 0, animLoop3 = (a.Tiles[i].AnimLoops & 4) != 0,
    };

    static void TilesChanged(Call c, Area a, List<int> indices, int flags, bool geometry)
    {
        if (geometry) WalkmeshBuilder.Rebuild(c.World, a);
        c.World.Output?.Notify("area-tiles", a, new { tileset = a.Tileset, flags, tiles = indices.Select(i => TileData(a, i)).ToList() });
    }

    [NwFunc("SetTile")]
    static object? SetTile(Call c)
    {
        if (TileAt(c, c.L(0)) is not var (a, i)) return null;
        ref var t = ref a.Tiles[i];
        t.Id = c.I(1);
        t.Orientation = (byte)(c.I(2) & 3);
        t.Height = (sbyte)c.I(3);
        TilesChanged(c, a, new List<int> { i }, c.I(4, 4), geometry: true);
        return null;
    }

    [NwFunc("SetTileAnimationLoops")]
    static object? SetTileAnimLoops(Call c)
    {
        if (TileAt(c, c.L(0)) is not var (a, i)) return null;
        a.Tiles[i].AnimLoops = (byte)((c.I(1) != 0 ? 1 : 0) | (c.I(2) != 0 ? 2 : 0) | (c.I(3) != 0 ? 4 : 0));
        TilesChanged(c, a, new List<int> { i }, 0, geometry: false);
        return null;
    }

    [NwFunc("SetTileJson")]
    static object? SetTileJson(Call c)
    {
        if (c.Obj(0) is not Area a || c.J(1).Node is not JsonArray list) return null;
        if (c.S(3).Length > 0) a.Tileset = c.S(3);
        var changed = new List<int>();
        foreach (var node in list)
        {
            if (node is not JsonObject o || Int(o, "index") is not { } i || i < 0 || i >= a.Tiles.Length) continue;
            ref var t = ref a.Tiles[i];
            t.Id = Int(o, "tileid") ?? 0;
            t.Orientation = (byte)((Int(o, "orientation") ?? 0) & 3);
            t.Height = (sbyte)(Int(o, "height") ?? 0);
            int loops = t.AnimLoops;
            for (int bit = 0; bit < 3; bit++)
                if (Int(o, "animloop" + (bit + 1)) is { } v) loops = v != 0 ? loops | (1 << bit) : loops & ~(1 << bit);
            t.AnimLoops = (byte)loops;
            changed.Add(i);
        }
        if (changed.Count > 0 || c.S(3).Length > 0) TilesChanged(c, a, changed, c.I(2, 4), geometry: true);
        return null;

        static int? Int(JsonObject o, string key) =>
            o[key] is System.Text.Json.Nodes.JsonValue v && v.TryGetValue<long>(out var l) ? (int)l
            : o[key] is System.Text.Json.Nodes.JsonValue d && d.TryGetValue<double>(out var f) ? (int)f : null;
    }

    static object? AreaReload(Call c, string what)
    {
        if (c.Obj(0) is Area a) c.World.Output?.Notify("area-reload", a, new { what });
        return null;
    }
    [NwFunc("ReloadAreaGrass")] static object? ReloadGrass(Call c) => AreaReload(c, "grass");
    [NwFunc("ReloadAreaBorder")] static object? ReloadBorder(Call c) => AreaReload(c, "border");

    [NwFunc("SetAreaGrassOverride")]
    static object? GrassOverride(Call c)
    {
        if (c.Obj(0) is not Area a || c.S(2).Length == 0) return null;
        var data = new
        {
            material = c.I(1), texture = c.S(2), density = c.F(3), height = c.F(4),
            ambient = Vec(Vector3.Clamp(c.V(5), Vector3.Zero, Vector3.One)), diffuse = Vec(Vector3.Clamp(c.V(6), Vector3.Zero, Vector3.One)),
        };
        Env(c, a).GrassOverrides[c.I(1)] = data;
        c.World.Output?.Notify("area-grass", a, data);
        return null;
    }
    [NwFunc("RemoveAreaGrassOverride")]
    static object? RemoveGrassOverride(Call c)
    {
        if (c.Obj(0) is Area a && Env(c, a).GrassOverrides.Remove(c.I(1)))
            c.World.Output?.Notify("area-grass", a, new { material = c.I(1), removed = true });
        return null;
    }
    [NwFunc("SetAreaDefaultGrassDisabled")]
    static object? DefaultGrassDisabled(Call c)
    {
        if (c.Obj(0) is not Area a) return null;
        Env(c, a).DefaultGrassDisabled = c.I(1) != 0;
        c.World.Output?.Notify("area-grass", a, new { defaultDisabled = c.I(1) != 0 });
        return null;
    }
    [NwFunc("SetAreaTileBorderDisabled")]
    static object? TileBorderDisabled(Call c)
    {
        if (c.Obj(0) is not Area a) return null;
        // Takes effect when clients next load the area.
        Env(c, a).TileBorderDisabled = c.I(1) != 0;
        return null;
    }

    // Event data for the module's OnPlayerTileAction (set by whoever raises the event).
    [NwFunc("GetLastTileActionId")] static object? TileActionId(Call c) => c.Event.Int("TileActionId");
    [NwFunc("GetLastTileActionPosition")]
    static object? TileActionPosition(Call c) =>
        c.Event.Values.TryGetValue("TileActionPosition", out var v) && v is Vector3 p ? p : Vector3.Zero;
    [NwFunc("GetLastPlayerToDoTileAction")]
    static object? TileActionPlayer(Call c)
    {
        uint id = c.Event.Obj("TileActionPlayer");
        return c.World.Get(id) != null ? id : GameObject.InvalidId;
    }

    // ---- camera ----

    static object? CameraLock(Call c, string which)
    {
        if (c.Obj<Creature>(0) is { IsPC: true } pc)
            c.World.Output?.Notify("camera-lock", pc, new { which, locked = c.I(1, 1) != 0 }, pc);
        return null;
    }
    [NwFunc("LockCameraPitch")] static object? LockPitch(Call c) => CameraLock(c, "pitch");
    [NwFunc("LockCameraDistance")] static object? LockDistance(Call c) => CameraLock(c, "distance");
    [NwFunc("LockCameraDirection")] static object? LockDirection(Call c) => CameraLock(c, "direction");

    // ---- object visuals ----

    static bool CanTransform(GameObject? o) => o is Creature or Placeable or Item or Door;

    [NwFunc("GetObjectVisualTransform")]
    static object? GetVisualTransform(Call c)
    {
        var o = c.Obj(0);
        int transform = c.I(1);
        if (!CanTransform(o)) return 0f;
        if (o!.VisualTransforms?.TryGetValue((c.I(3), transform), out var vt) != true) return VisualTransform.Default(transform);
        return c.I(2) != 0 ? vt!.ValueAt(c.World.Time) : vt!.To;
    }

    [NwFunc("SetObjectVisualTransform")]
    static object? SetVisualTransform(Call c)
    {
        // object oObject, int nTransform, float fValue, int nLerpType, float fLerpDuration, int bPauseWithGame, int nScope, int nBehaviorFlags, int nRepeats
        var o = c.Obj(0);
        if (!CanTransform(o)) return 0f;
        int transform = c.I(1), scope = c.I(6);
        var key = (scope, transform);
        o!.VisualTransforms ??= new();
        float old = o.VisualTransforms.TryGetValue(key, out var prev) ? prev.ValueAt(c.World.Time) : VisualTransform.Default(transform);
        var vt = new VisualTransform
        {
            From = old, To = c.F(2), LerpType = c.I(3), LerpDuration = c.F(4), PauseWithGame = c.I(5, 1) != 0,
            StartTime = c.World.Time, Behavior = c.I(7), Repeats = c.I(8),
        };
        o.VisualTransforms[key] = vt;
        c.World.Output?.Notify("visual-transform", o, new
        {
            transform, scope, from = old, value = vt.To, lerp = vt.LerpType, duration = vt.LerpDuration,
            pauseWithGame = vt.PauseWithGame, behavior = vt.Behavior, repeats = vt.Repeats,
        });
        return old;
    }

    [NwFunc("ClearObjectVisualTransform")]
    static object? ClearVisualTransform(Call c)
    {
        var o = c.Obj(0);
        if (o?.VisualTransforms is not { Count: > 0 } map) return 0;
        int scope = c.I(1, -1);
        int removed = scope == -1 ? map.Count : map.Keys.Count(k => k.Scope == scope);
        if (scope == -1) map.Clear();
        else foreach (var k in map.Keys.Where(k => k.Scope == scope).ToList()) map.Remove(k);
        if (removed == 0) return 0;
        c.World.Output?.Notify("visual-transform-clear", o, new { scope });
        return 1;
    }

    [NwFunc("SetObjectVisibleDistance")]
    static object? SetVisibleDistance(Call c)
    {
        // TODO(server): GameServer could use this to limit which objects it sends; it currently sends whole areas.
        if (c.Obj(0) is { } o and not (Area or Module)) o.VisibleDistance = Math.Max(0f, c.F(1, 45f));
        return null;
    }
    [NwFunc("GetObjectVisibleDistance")] static object? GetVisibleDistance(Call c) => c.Obj(0) is { } o and not (Area or Module) ? o.VisibleDistance : -1f;

    [NwFunc("ReplaceObjectTexture")]
    static object? ReplaceTexture(Call c)
    {
        if (c.Obj(0) is not { } o) return null;
        o.TextureReplacements ??= new(StringComparer.OrdinalIgnoreCase);
        if (c.S(2).Length == 0) o.TextureReplacements.Remove(c.S(1));
        else o.TextureReplacements[c.S(1)] = c.S(2);
        c.World.Output?.Notify("object-texture", o, new { old = c.S(1), @new = c.S(2) });
        return null;
    }

    [NwFunc("ReplaceObjectAnimation")]
    static object? ReplaceAnimation(Call c)
    {
        if (c.Obj(0) is not { } o) return null;
        o.AnimationReplacements ??= new(StringComparer.OrdinalIgnoreCase);
        if (c.S(2).Length == 0) o.AnimationReplacements.Remove(c.S(1));
        else o.AnimationReplacements[c.S(1)] = c.S(2);
        c.World.Output?.Notify("object-animation", o, new { old = c.S(1), @new = c.S(2) });
        return null;
    }

    [NwFunc("SetTextureOverride")]
    static object? TextureOverride(Call c)
    {
        var data = new { old = c.S(0), @new = c.S(1) };
        if (c.Obj<Creature>(2) is { IsPC: true } pc) c.World.Output?.Notify("texture-override", pc, data, pc);
        else if (c.O(2) == GameObject.InvalidId) c.World.Output?.Notify("texture-override", null, data); // all players
        return null;
    }

    [NwFunc("SetMaterialShaderUniformInt")]
    static object? MaterialUniformInt(Call c)
    {
        if (c.Obj(0) is { } o) c.World.Output?.Notify("material-uniform", o, new { material = c.S(1), param = c.S(2), type = "int", value = new[] { (float)c.I(3) } });
        return null;
    }
    [NwFunc("SetMaterialShaderUniformVec4")]
    static object? MaterialUniformVec4(Call c)
    {
        if (c.Obj(0) is { } o) c.World.Output?.Notify("material-uniform", o, new { material = c.S(1), param = c.S(2), type = "vec4", value = new[] { c.F(3), c.F(4), c.F(5), c.F(6) } });
        return null;
    }
    [NwFunc("ResetMaterialShaderUniforms")]
    static object? ResetMaterialUniforms(Call c)
    {
        if (c.Obj(0) is { } o) c.World.Output?.Notify("material-uniform-reset", o, new { material = c.S(1), param = c.S(2) });
        return null;
    }

    static object? ShaderUniform(Call c, string type, float[] value)
    {
        if (c.Obj<Creature>(0) is { IsPC: true } pc) c.World.Output?.Notify("shader-uniform", pc, new { shader = c.I(1), type, value }, pc);
        return null;
    }
    [NwFunc("SetShaderUniformFloat")] static object? ShaderFloat(Call c) => ShaderUniform(c, "float", new[] { c.F(2) });
    [NwFunc("SetShaderUniformInt")] static object? ShaderInt(Call c) => ShaderUniform(c, "int", new[] { (float)c.I(2) });
    [NwFunc("SetShaderUniformVec")] static object? ShaderVec(Call c) => ShaderUniform(c, "vec4", new[] { c.F(2), c.F(3), c.F(4), c.F(5) });
}
