using System.Collections;
using System.Numerics;
using System.Reflection;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.WorldObjects;

/// <summary>CreateArea / CopyArea / DestroyArea: run-time area instances with generated "nw_N" resrefs.</summary>
public static class AreaInstancing
{
    /// <summary>The lowest free "nw_N" resref.</summary>
    static string NextResRef(World world)
    {
        for (int n = 0; ; n++)
        {
            string r = "nw_" + n;
            if (!world.Areas.Any(a => a.ResRef.Equals(r, StringComparison.OrdinalIgnoreCase))) return r;
        }
    }

    static AreaLoader Loader(World world) => world.Loader ?? new AreaLoader(world);

    /// <summary>Instances a module area from its ARE/GIT. Spawn scripts run after the current script.</summary>
    public static Area? Create(World world, string sourceResRef, string newTag, string newName)
    {
        // Only module areas (with .are data) can be instanced; run-time "nw_" copies can't.
        if (string.IsNullOrEmpty(sourceResRef) || !world.Resources.Contains(sourceResRef, ResType.Are)) return null;
        Area area;
        try
        {
            var are = world.Resources.ReadGff(sourceResRef, ResType.Are) ?? throw new FileNotFoundException(sourceResRef);
            var git = world.Resources.ReadGff(sourceResRef, ResType.Git) ?? new GffStruct();
            world.System<TileOverrides>().Apply(sourceResRef, are);
            area = Loader(world).Load(sourceResRef, are, git);
        }
        catch (Exception e) when (e is FileNotFoundException or InvalidDataException) { return null; }
        area.SourceResRef = sourceResRef;
        area.ResRef = NextResRef(world);
        Finish(world, area, newTag, newName);
        // Placed creatures bring their items like at module load (OnAcquireItem), then spawn.
        foreach (var c in area.Objects.OfType<Creature>().ToList()) world.Inventory.FireSpawnAcquires(c);
        world.After(0, () =>
        {
            foreach (var c in area.Objects.OfType<Creature>().ToList())
                world.RunEvent(c, "ScriptSpawn", new EventContext());
        });
        return area;
    }

    /// <summary>
    /// Makes an area built from data (JsonToObject of the combined area format) a run-time instance with the next
    /// "nw_N" resref. Its objects come with their saved state; no spawn scripts run.
    /// </summary>
    public static Area Adopt(World world, Area area)
    {
        if (area.SourceResRef.Length == 0) area.SourceResRef = area.ResRef;
        area.ResRef = NextResRef(world);
        Finish(world, area, "", "");
        return area;
    }

    static void Finish(World world, Area area, string newTag, string newName)
    {
        if (newTag.Length > 0) world.SetTag(area, newTag);
        if (newName.Length > 0) area.Name = new LocString(newName);
        world.Areas.Add(area);
        DoorService.SyncWalkmesh(area);
    }

    /// <summary>Copies an area with its current state and everything in it except players.</summary>
    public static Area? Copy(World world, Area source, string newTag, string newName)
    {
        var area = new Area
        {
            ResRef = NextResRef(world),
            SourceResRef = source.SourceResRef.Length > 0 ? source.SourceResRef : source.ResRef,
            Are = source.Are,
            Tag = source.Tag,
            Name = source.Name.Clone(),
            Description = source.Description,
            Tileset = source.Tileset,
            Width = source.Width,
            Height = source.Height,
            Tiles = (Net.TileInfo[])source.Tiles.Clone(),
            TileHeightStep = source.TileHeightStep,
            Environment = source.Environment?.Clone(),
        };
        foreach (var kv in source.Scripts) area.Scripts[kv.Key] = kv.Value;
        CopyLocals(source.Locals, area.Locals);
        world.Register(area);

        foreach (var o in source.Objects.ToList())
        {
            if (o.IsDestroyed || o is Creature { IsPC: true }) continue;
            var copy = ObjectCloner.Clone(world, o);
            copy.Area = area;
            area.Objects.Add(copy);
            if (copy is Door d) d.WalkmeshOwner = (int)d.Id;
        }
        WalkmeshBuilder.Rebuild(world, area);
        Finish(world, area, newTag, newName);
        world.After(0, () =>
        {
            foreach (var c in area.Objects.OfType<Creature>().ToList())
                world.RunEvent(c, "ScriptSpawn", new EventContext());
        });
        return area;
    }

    static void CopyLocals(LocalVariables from, LocalVariables to)
    {
        foreach (var kv in from.Ints) to.Ints[kv.Key] = kv.Value;
        foreach (var kv in from.Floats) to.Floats[kv.Key] = kv.Value;
        foreach (var kv in from.Strings) to.Strings[kv.Key] = kv.Value;
        foreach (var kv in from.Objects) to.Objects[kv.Key] = kv.Value;
        foreach (var kv in from.Locations) to.Locations[kv.Key] = kv.Value;
        foreach (var kv in from.Jsons) to.Jsons[kv.Key] = kv.Value;
    }

    /// <summary>DestroyArea return codes: 1 destroyed, 0 not an area, -1 module entry area, -2 players inside.</summary>
    /// <summary>DestroyArea's result without destroying anything: 1 when it may go, otherwise the error code.</summary>
    public static int CanDestroy(World world, GameObject? o)
    {
        if (o is not Area area || area.IsDestroyed) return 0;
        if (area.SourceResRef.Length == 0 && area.ResRef.Equals(world.Module.EntryArea, StringComparison.OrdinalIgnoreCase)) return -1;
        if (area.Objects.Any(x => x is Creature { IsPC: true })) return -2;
        return 1;
    }

    public static int Destroy(World world, GameObject? o)
    {
        int result = CanDestroy(world, o);
        if (result != 1 || o is not Area area) return result;
        foreach (var obj in area.Objects.ToList()) world.Destroy(obj);
        world.Areas.Remove(area);
        world.Destroy(area);
        return 1;
    }
}

/// <summary>Builds an area's walkmesh from its tiles, placeable PWKs and door DWKs (SetTile, CopyArea).</summary>
public static class WalkmeshBuilder
{
    public static void Rebuild(World world, Area area)
    {
        var rm = world.Resources;
        var surfaces = rm.TwoDA("surfacemat");
        var wm = area.Walkmesh;
        wm.Clear();
        var set = Ini.Parse(rm.Read(area.Tileset, ResType.Set) ?? Array.Empty<byte>());
        for (int i = 0; i < area.Tiles.Length; i++)
        {
            var model = world.Models.Get(set.Get($"TILE{area.Tiles[i].Id}", "Model"));
            if (model != null) Add(wm, surfaces, model, AreaLoader.TileTransform(area, i, area.Tiles[i]), onlyWalkmeshNodes: true, owner: 0);
        }
        var placeables = rm.TwoDA("placeables");
        foreach (var o in area.Objects)
        {
            switch (o)
            {
                case Placeable p when world.Models.GetWalkmesh(placeables.Get(p.Appearance, "ModelName"), ResType.Pwk) is { } pwk:
                    wm.SetPlaceableOwner((int)p.Id);
                    Add(wm, surfaces, pwk, Placement(p.Position, p.Facing), onlyWalkmeshNodes: false, owner: (int)p.Id);
                    break;
                case Door d when world.Models.GetWalkmesh(DoorModel(world, d), ResType.Dwk) is { } dwk:
                    AreaLoader.AddDoorWalkmesh(wm, d, dwk, Placement(d.Position, d.Facing),
                        (w, m, xf, only, owner, filter) => Add(w, surfaces, m, xf, only, owner, filter));
                    break;
            }
        }
        DoorService.SyncWalkmesh(area);
    }

    static string DoorModel(World world, Door d) => world.Loader?.DoorModel(d) ?? (d.Appearance > 0
        ? world.Resources.TwoDA("doortypes").Get(d.Appearance, "Model")
        : world.Resources.TwoDA("genericdoors").Get(d.GenericType, "ModelName"));

    static Matrix4x4 Placement(Vector3 position, float bearing) => Matrix4x4.CreateRotationZ(bearing) * Matrix4x4.CreateTranslation(position);

    static void Add(Walkmesh wm, TwoDA surfaces, ModelData model, Matrix4x4 xf, bool onlyWalkmeshNodes, int owner, Func<ModelPart, bool>? filter = null)
    {
        var globals = model.RestGlobals();
        for (int i = 0; i < model.Parts.Count; i++)
        {
            var part = model.Parts[i];
            var mesh = part.Mesh;
            if (mesh == null || (onlyWalkmeshNodes && !mesh.IsWalkmesh)) continue;
            if (filter != null && !filter(part)) continue;
            var m = globals[i] * xf;
            for (int f = 0; f + 2 < mesh.Indices.Length; f += 3)
            {
                int surface = f / 3 < mesh.FaceSurfaces.Length ? mesh.FaceSurfaces[f / 3] : 0;
                wm.Add(Vector3.Transform(mesh.Vertices[mesh.Indices[f]], m), Vector3.Transform(mesh.Vertices[mesh.Indices[f + 1]], m),
                    Vector3.Transform(mesh.Vertices[mesh.Indices[f + 2]], m), surface, surfaces.Get(surface, "Walk") == "1", owner);
            }
        }
    }
}

/// <summary>
/// Copies a game object with its run-time state (CopyArea): a member-wise copy whose
/// collections and state objects are duplicated, carried items copied as new objects,
/// and the action queue left empty. Templates (GFF structs) are shared, read-only.
/// </summary>
public static class ObjectCloner
{
    static readonly MethodInfo MemberwiseCloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    public static GameObject Clone(World world, GameObject source)
    {
        var copy = (GameObject)MemberwiseCloneMethod.Invoke(source, null)!;
        copy.Id = 0;
        copy.IsDestroyed = false;
        copy.Area = null;
        DeepCopyFields(world, copy, copy.GetType());
        copy.Actions.Clear();
        copy.NextHeartbeat = -1;
        switch (copy)
        {
            case Creature c: c.Player = null; c.SittingOn = GameObject.InvalidId; break;
            case Trigger t: t.Inside.Clear(); break;
            case Encounter e: e.State.Inside.Clear(); e.State.Spawned.Clear(); break;
            case Placeable p: p.Sitter = GameObject.InvalidId; break;
        }
        world.Register(copy);
        // Items now belong to the copy.
        switch (copy)
        {
            case Creature cr: foreach (var i in cr.Equipment.Values) i.Possessor = cr.Id; break;
        }
        return copy;
    }

    static readonly Dictionary<Type, FieldInfo[]> _fields = new();

    static FieldInfo[] Fields(Type t)
    {
        if (_fields.TryGetValue(t, out var f)) return f;
        var list = new List<FieldInfo>();
        for (var type = t; type != null && type != typeof(object); type = type.BaseType)
            list.AddRange(type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly));
        return _fields[t] = list.ToArray();
    }

    static void DeepCopyFields(World world, object target, Type type)
    {
        foreach (var f in Fields(type))
        {
            var v = f.GetValue(target);
            if (v == null) continue;
            var copied = CopyValue(world, v);
            if (!ReferenceEquals(copied, v)) f.SetValue(target, copied);
        }
    }

    static object? CopyValue(World world, object v)
    {
        switch (v)
        {
            case string or GffStruct or Delegate or ScriptSituation: return v;
            case List<GameAction>: return new List<GameAction>(); // the copy starts with an empty action queue
            case GameObject go: return go; // references to other objects stay as they are
            case LocString l: return l.Clone();
            case Effect e: return e.Clone();
            case ItemProperty ip: return ip.Clone();
            case Array a:
            {
                var copy = (Array)a.Clone();
                if (a.GetType().GetElementType() is { IsValueType: false } && a.GetType().GetElementType() != typeof(string))
                    for (int i = 0; i < copy.Length; i++)
                        if (copy.GetValue(i) is { } x) copy.SetValue(CopyValue(world, x), i);
                return copy;
            }
        }
        var t = v.GetType();
        if (t.IsValueType) return v;
        if (v is IList list && t.IsGenericType)
        {
            var copy = (IList)Activator.CreateInstance(t)!;
            foreach (var x in list) copy.Add(x is Item item ? Clone(world, item) : x == null ? null : CopyValue(world, x));
            return copy;
        }
        if (v is IDictionary dict && t.IsGenericType)
        {
            IDictionary copy;
            // Keep the comparer (e.g. case-insensitive script names).
            var comparerProp = t.GetProperty("Comparer");
            var comparer = comparerProp?.GetValue(v);
            copy = (IDictionary)(comparer != null && t.GetConstructor(new[] { comparerProp!.PropertyType }) is { } ctor
                ? ctor.Invoke(new[] { comparer }) : Activator.CreateInstance(t)!);
            foreach (DictionaryEntry kv in dict) copy[kv.Key] = kv.Value is Item item ? Clone(world, item) : kv.Value == null ? null : CopyValue(world, kv.Value);
            return copy;
        }
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(HashSet<>))
            return Activator.CreateInstance(t, v);
        // Plain state objects defined by the engine (LocalVariables, TrapData, ...): copy member-wise, recursively.
        if (t.Assembly == typeof(World).Assembly && !typeof(GameObject).IsAssignableFrom(t) && t.IsClass
            && t != typeof(PlayerSession) && t != typeof(World))
        {
            var copy = MemberwiseCloneMethod.Invoke(v, null)!;
            DeepCopyFields(world, copy, t);
            return copy;
        }
        return v;
    }
}
