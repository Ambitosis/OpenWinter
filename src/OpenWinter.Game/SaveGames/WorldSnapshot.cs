using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.SaveGames;

/// <summary>
/// The complete simulation state of a world as one JSON document: world fields, every
/// system, every game object (with its id), the script scheduler (DelayCommand/AssignCommand
/// closures), engine timers and persistence data that lives outside the objects. See
/// docs/SAVEGAME.md. Take it between world steps; restore into a world freshly loaded from
/// the same module with <see cref="Apply"/>.
/// </summary>
public static class WorldSnapshot
{
    public const int FormatVersion = 1;

    /// <summary>Captures the world. <paramref name="warnings"/> lists anything that could not be saved.</summary>
    public static JsonObject Capture(World world, out IReadOnlyCollection<string> warnings)
    {
        var w = new SnapshotWriter(world);
        // Every registered object gets a key first (by id), so references from anywhere resolve.
        foreach (var o in world.AllObjects.OrderBy(o => o.Id)) w.KeyOf(o);

        var worldFields = new JsonObject();
        foreach (var (key, f) in SnapshotTypes.Fields(typeof(World)))
        {
            if (World.SnapshotExcludedFields.Contains(key) || typeof(Delegate).IsAssignableFrom(f.FieldType) || SnapshotTypes.IsSkipped(f.FieldType)) continue;
            var node = w.Write(f.GetValue(world), f.FieldType);
            if (!SnapshotWriter.IsOmitted(node)) worldFields[key] = node;
        }

        var systems = new JsonArray();
        foreach (var s in world.Systems)
            systems.Add(new JsonObject { ["type"] = s.GetType().FullName, ["fields"] = w.Fields(s, s.GetType(), closure: false) });

        var host = new JsonObject();
        if (world.Scripts is { } scripts)
        {
            host["seq"] = scripts.ScheduleSequence;
            host["params"] = w.Write(scripts.PendingParams, scripts.PendingParams.GetType());
            var list = new JsonArray();
            foreach (var (s, self, at, seq) in scripts.PendingScheduled)
                list.Add(new JsonObject { ["at"] = at, ["seq"] = seq, ["self"] = self, ["s"] = w.Write(s, typeof(ScriptSituation)) });
            host["scheduled"] = list;
        }

        var timers = new JsonArray();
        foreach (var (action, at, seq) in world.PendingTimers)
        {
            var node = w.Write(action, typeof(Action), allowDelegates: true);
            if (SnapshotWriter.IsOmitted(node) || node == null) continue;
            timers.Add(new JsonObject { ["at"] = at, ["seq"] = seq, ["do"] = node });
        }

        var objects = new JsonArray();
        var extra = new JsonObject();
        var charDbs = new JsonObject();
        while (w.TryNextPending(out var o))
        {
            var entry = new JsonObject
            {
                ["k"] = w.KeyOf(o),
                ["id"] = o.Id,
                ["t"] = SnapshotTypes.TypeName(o.GetType()),
                ["reg"] = world.IsRegistered(o),
            };
            if (o is Area a) { entry["resref"] = a.ResRef; entry["src"] = a.SourceResRef; }
            entry["f"] = w.Fields(o, o.GetType(), closure: false);
            objects.Add(entry);
            if (o is Creature c && PersistenceSystem.CharacterDbStruct(c) is { } db)
                charDbs[w.KeyOf(o)] = Convert.ToBase64String(Gff.Write(db, "GFF "));
        }
        w.FinishReferences();

        if (charDbs.Count > 0) extra["characterDbs"] = charDbs;
        if (world.Module.Database is { IsOpen: true } moduleDb) extra["moduleDb"] = Convert.ToBase64String(moduleDb.Serialize());
        var temp = new JsonArray();
        foreach (var src in world.Resources.Sources.OfType<MemorySource>().Where(s => s.Description == "TEMP:"))
            foreach (var key in src.Keys)
                if (src.Read(key) is { } data)
                    temp.Add(new JsonObject { ["resref"] = key.ResRef, ["type"] = key.Type, ["data"] = Convert.ToBase64String(data) });
        if (temp.Count > 0) extra["tempTemplates"] = temp;

        var players = new JsonArray();
        foreach (var pc in Players.FamiliarPossession.Players(world))
            players.Add(new JsonObject { ["obj"] = w.KeyOf(pc), ["player"] = (pc.Player ?? pc.FamiliarController)?.PlayerName ?? "", ["vaultFile"] = pc.VaultFile });

        warnings = w.Warnings;
        return new JsonObject
        {
            ["format"] = FormatVersion,
            ["engine"] = typeof(World).Assembly.ManifestModule.ModuleVersionId.ToString(),
            ["time"] = world.Time,
            ["nextId"] = world.NextObjectId,
            ["timerSeq"] = world.TimerSequence,
            ["players"] = players,
            ["world"] = worldFields,
            ["systems"] = systems,
            ["host"] = host,
            ["timers"] = timers,
            ["objects"] = objects,
            ["extra"] = extra,
        };
    }

    /// <summary>What a load restored: the saved player characters by player name (creature ids are as saved).</summary>
    public sealed class Restored
    {
        public readonly List<(Creature Creature, string Player)> Players = new();
        public readonly SortedSet<string> Warnings = new(StringComparer.Ordinal);
    }

    /// <summary>
    /// Replaces a freshly loaded world's state (module loaded without running its load or spawn
    /// events, scripts enabled) with a snapshot. No script events run.
    /// </summary>
    public static Restored Apply(World world, JsonObject snap)
    {
        if (snap["format"]?.GetValue<int>() is not { } format || format > FormatVersion)
            throw new InvalidDataException($"save format {snap["format"]} is not supported by this engine");
        var result = new Restored();

        // ---- phase 1: instances with their saved ids
        var objects = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        var registered = new List<GameObject>();
        var freshAreas = world.Areas.Where(a => a.SourceResRef.Length == 0)
            .GroupBy(a => a.ResRef, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var reused = new Dictionary<Area, (Net.TileInfo[] Tiles, HashSet<int> Owners)>();
        foreach (var e in (snap["objects"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            string key = e["k"]!.GetValue<string>(), typeName = e["t"]!.GetValue<string>();
            var type = SnapshotTypes.ResolveType(typeName);
            if (type == null || !typeof(GameObject).IsAssignableFrom(type)) { result.Warnings.Add($"object {key}: unknown type {typeName}"); continue; }
            GameObject o;
            if (type == typeof(Module)) o = world.Module;
            else if (type == typeof(Area) && e["src"]?.GetValue<string>() is "" or null && e["resref"]?.GetValue<string>() is { } resref
                     && freshAreas.Remove(resref, out var fresh))
            {
                o = fresh;
                reused[fresh] = ((Net.TileInfo[])fresh.Tiles.Clone(), fresh.ObjectsOf<Door>().Select(d => d.WalkmeshOwner).Where(x => x != 0).ToHashSet());
            }
            else o = (GameObject)(Activator.CreateInstance(type, nonPublic: true) ?? throw new InvalidDataException($"cannot create {type.Name}"));
            o.Id = e["id"]!.GetValue<uint>();
            objects[key] = o;
            if (e["reg"]?.GetValue<bool>() == true) registered.Add(o);
        }
        world.ReplaceObjects(registered);

        // ---- phase 2: state, in the order it was written (shared instances refer back)
        var reader = new SnapshotReader(world, objects);
        if (snap["world"] is JsonObject worldFields) reader.Populate(world, worldFields);
        foreach (var s in (snap["systems"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
        {
            var typeName = s["type"]!.GetValue<string>();
            var system = world.Systems.FirstOrDefault(x => x.GetType().FullName == typeName);
            if (system == null) { result.Warnings.Add($"system {typeName} no longer exists"); continue; }
            if (s["fields"] is JsonObject fields) reader.Populate(system, fields);
        }
        if (snap["host"] is JsonObject host && world.Scripts is { } scripts)
        {
            scripts.ScheduleSequence = host["seq"]?.GetValue<long>() ?? 0;
            reader.Read(host["params"], scripts.PendingParams.GetType(), scripts.PendingParams);
            var items = new List<(ScriptSituation, uint, double, long)>();
            foreach (var item in (host["scheduled"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            {
                var situation = reader.Read<ScriptSituation>(item["s"]);
                if (situation?.Program == null) { result.Warnings.Add("a delayed command's script is missing; dropped"); continue; }
                items.Add((situation, item["self"]!.GetValue<uint>(), item["at"]!.GetValue<double>(), item["seq"]!.GetValue<long>()));
            }
            scripts.ReplaceScheduled(items);
        }
        var timers = new List<(Action, double, long)>();
        foreach (var t in (snap["timers"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            if (reader.Read<Action>(t["do"]) is { } action) timers.Add((action, t["at"]!.GetValue<double>(), t["seq"]!.GetValue<long>()));
        foreach (var e in (snap["objects"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            if (objects.TryGetValue(e["k"]!.GetValue<string>(), out var o) && e["f"] is JsonObject fields) reader.Populate(o, fields);
        world.ReplaceTimers(timers);
        world.TimerSequence = snap["timerSeq"]?.GetValue<long>() ?? world.TimerSequence;
        uint maxId = registered.Count == 0 ? 0 : registered.Max(o => o.Id);
        world.NextObjectId = Math.Max(snap["nextId"]?.GetValue<uint>() ?? 0, maxId + 1);

        // ---- derived state
        foreach (var o in objects.Values) o.Actions.RemoveAll(a => a is DoCommandAction { Situation.Program: null });
        foreach (var area in world.Areas) RestoreWalkmesh(world, area, reused);
        RestorePersistence(world, snap["extra"] as JsonObject, objects, result);
        // Saved curse payloads survive, but native reconstructs their applied penalties
        // using immunity at reload rather than the immunity present when first cast.
        foreach (var creature in registered.OfType<Creature>())
            world.System<Rules.EffectSystem>().RestoreCursePenalties(creature);

        foreach (var p in (snap["players"] as JsonArray ?? new JsonArray()).OfType<JsonObject>())
            if (objects.TryGetValue(p["obj"]!.GetValue<string>(), out var o) && o is Creature c)
                result.Players.Add((c, p["player"]?.GetValue<string>() ?? ""));
        foreach (var warning in reader.Warnings) result.Warnings.Add(warning);
        return result;
    }

    /// <summary>
    /// Module areas keep the walkmesh they were loaded with unless the save changed their tiles
    /// (SetTile) or door walkmesh ids; instanced areas are built from their tiles and objects.
    /// </summary>
    static void RestoreWalkmesh(World world, Area area, Dictionary<Area, (Net.TileInfo[] Tiles, HashSet<int> Owners)> reused)
    {
        bool rebuild = true;
        if (reused.TryGetValue(area, out var fresh))
        {
            bool sameTiles = fresh.Tiles.Length == area.Tiles.Length && fresh.Tiles.AsSpan().SequenceEqual(area.Tiles);
            bool sameOwners = area.ObjectsOf<Door>().Where(d => d.WalkmeshOwner != 0).All(d => fresh.Owners.Contains(d.WalkmeshOwner));
            rebuild = !sameTiles || !sameOwners;
        }
        if (rebuild)
        {
            WalkmeshBuilder.Rebuild(world, area);
            area.ResetPathfinder();
        }
        DoorService.SyncWalkmesh(area);
    }

    static void RestorePersistence(World world, JsonObject? extra, Dictionary<string, GameObject> objects, Restored result)
    {
        if (extra == null) return;
        var persistence = world.System<PersistenceSystem>();
        if (extra["tempTemplates"] is JsonArray temp)
            foreach (var t in temp.OfType<JsonObject>())
                persistence.SetTemplate(t["resref"]!.GetValue<string>(), t["type"]!.GetValue<ushort>(), Convert.FromBase64String(t["data"]!.GetValue<string>()));
        if (extra["characterDbs"] is JsonObject chars)
            foreach (var (key, data) in chars)
                if (objects.TryGetValue(key, out var o) && o is Creature c && data != null)
                {
                    // Opened from the saved image the way a BIC's database is, then the template gets back
                    // the field it had (the open database is what the character exports from now on).
                    var saved = Gff.Read(Convert.FromBase64String(data.GetValue<string>()));
                    if (c.Template.Field("SQLite") is { } field)
                    {
                        var (type, value) = (field.Type, field.Value);
                        (field.Type, field.Value) = (GffType.Struct, saved);
                        persistence.CharacterDb(c);
                        (field.Type, field.Value) = (type, value);
                    }
                    else
                    {
                        c.Template.SetStruct("SQLite", saved);
                        persistence.CharacterDb(c);
                        c.Template.Remove("SQLite");
                    }
                }
        if (extra["moduleDb"]?.GetValue<string>() is { } moduleDb)
        {
            try { persistence.ModuleDb(world.Module).Load(Convert.FromBase64String(moduleDb)); }
            catch (InvalidDataException e) { result.Warnings.Add($"module database not restored: {e.Message}"); }
        }
    }
}
