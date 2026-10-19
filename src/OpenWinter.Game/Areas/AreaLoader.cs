using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;
using OpenWinter.Game.Objects;
using OpenWinter.Net;

namespace OpenWinter.Game.Areas;

/// <summary>Builds an Area (tiles, walkmesh, instances) from ARE + GIT resources.</summary>
public sealed class AreaLoader
{
    readonly World _world;
    readonly ResMan _rm;
    readonly ModelCache _models;
    readonly TwoDA _surfaces;

    public AreaLoader(World world)
    {
        _world = world;
        _rm = world.Resources;
        _models = world.Models;
        _surfaces = _rm.TwoDA("surfacemat");
    }

    bool Walkable(int surface) => _surfaces.Get(surface, "Walk") == "1";

    public Area Load(string resref)
    {
        var are = _rm.ReadGff(resref, ResType.Are) ?? throw new FileNotFoundException($"Area {resref}.are not found");
        var git = _rm.ReadGff(resref, ResType.Git) ?? new GffStruct();
        return Load(resref, are, git);
    }

    /// <summary>An area from its ARE and GIT structs (a module area, the combined area format, a saved game).</summary>
    public Area Load(string resref, GffStruct are, GffStruct git)
    {
        var area = new Area
        {
            ResRef = resref,
            Are = are,
            Tag = are.GetString("Tag"),
            Name = are.GetLocString("Name"),
            Tileset = are.GetString("Tileset"),
            Width = (int)are.GetInt("Width"),
            Height = (int)are.GetInt("Height"),
        };
        foreach (var evt in new[] { "OnEnter", "OnExit", "OnHeartbeat", "OnUserDefined" })
            area.Scripts[evt] = are.GetString(evt);
        var props = git.GetStruct("AreaProperties") ?? new GffStruct();
        area.Properties = props;
        area.MusicDay = (int)props.GetInt("MusicDay");
        area.MusicNight = (int)props.GetInt("MusicNight");
        area.MusicBattle = (int)props.GetInt("MusicBattle");
        area.MusicDelay = (int)props.GetInt("MusicDelay");
        area.AmbientDay = (int)props.GetInt("AmbientSndDay");
        area.AmbientNight = (int)props.GetInt("AmbientSndNight");
        area.AmbientDayVolume = (int)props.GetInt("AmbientSndDayVol", 64);
        area.AmbientNightVolume = (int)props.GetInt("AmbientSndNitVol", 64);
        area.Locals.LoadFrom(git.GetStruct("AreaProperties") ?? are);
        area.Locals.LoadFrom(are);
        // Saved games and the combined area format keep the area's variables in the GIT.
        if (git.Has("VarTable")) area.Locals.LoadFrom(git);
        _world.Register(area);

        LoadTiles(area);
        foreach (var s in git.GetList("Placeable List")) AddPlaceable(area, s);
        foreach (var s in git.GetList("Door List")) AddDoor(area, s);
        foreach (var s in git.GetList("WaypointList")) AddWaypoint(area, s);
        foreach (var s in git.GetList("TriggerList")) AddTrigger(area, s);
        foreach (var s in git.GetList("SoundList")) AddSound(area, s);
        foreach (var s in git.GetList("Encounter List")) AddEncounter(area, s);
        foreach (var s in git.GetList("StoreList")) AddStore(area, s);
        foreach (var s in git.GetList("Creature List")) AddCreature(area, s);
        foreach (var s in git.GetList("List")) AddItem(area, s);
        return area;
    }

    void LoadTiles(Area area)
    {
        var set = Ini.Parse(_rm.Read(area.Tileset, ResType.Set) ?? Array.Empty<byte>());
        area.TileHeightStep = set.GetFloat("GENERAL", "Transition", 5f);
        var list = area.Are.GetList("Tile_List");
        area.Tiles = new TileInfo[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var t = list[i];
            var info = new TileInfo
            {
                Id = (int)t.GetInt("Tile_ID"),
                Orientation = (byte)t.GetInt("Tile_Orientation"),
                Height = (sbyte)t.GetInt("Tile_Height"),
                MainLight1 = (byte)t.GetInt("Tile_MainLight1"),
                MainLight2 = (byte)t.GetInt("Tile_MainLight2"),
                SourceLight1 = (byte)t.GetInt("Tile_SrcLight1"),
                SourceLight2 = (byte)t.GetInt("Tile_SrcLight2"),
                AnimLoops = (byte)(t.GetInt("Tile_AnimLoop1") | (t.GetInt("Tile_AnimLoop2") << 1) | (t.GetInt("Tile_AnimLoop3") << 2)),
            };
            area.Tiles[i] = info;
            var model = _models.Get(set.Get($"TILE{info.Id}", "Model"));
            if (model == null) continue;
            var xf = TileTransform(area, i, info);
            AddWalkmesh(area.Walkmesh, model, xf, onlyWalkmeshNodes: true, owner: 0);
        }
    }

    public static Matrix4x4 TileTransform(Area area, int index, TileInfo t) =>
        Matrix4x4.CreateRotationZ(MathF.PI / 2f * t.Orientation) *
        Matrix4x4.CreateTranslation(((index % area.Width) + 0.5f) * Area.TileSize, ((index / area.Width) + 0.5f) * Area.TileSize, t.Height * area.TileHeightStep);

    void AddWalkmesh(Walkmesh wm, ModelData model, Matrix4x4 xf, bool onlyWalkmeshNodes, int owner, Func<ModelPart, bool>? filter = null)
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
                    Vector3.Transform(mesh.Vertices[mesh.Indices[f + 2]], m), surface, Walkable(surface), owner);
            }
        }
    }

    static Matrix4x4 Placement(Vector3 position, float bearing) => Matrix4x4.CreateRotationZ(bearing) * Matrix4x4.CreateTranslation(position);

    static void ReadCommon(GameObject o, GffStruct s, string nameField = "LocName")
    {
        o.Tag = s.GetString("Tag");
        o.ResRef = s.GetString("TemplateResRef");
        o.Name = s.GetLocString(nameField);
        o.Description = s.GetLocString("Description").FirstString;
        o.Plot = s.GetInt("Plot") != 0;
        o.Locals.LoadFrom(s);
        WorldObjectFields.Load(o, s);
    }

    static void ReadScripts(GameObject o, GffStruct s, params string[] fields)
    {
        foreach (var f in fields) o.Scripts[f] = s.GetString(f);
    }

    /// <summary>Called for every object read from a GIT, with its struct (loading the original's save games maps their object ids).</summary>
    public Action<GameObject, GffStruct>? Loaded;

    void Place(Area area, GameObject o, GffStruct? s = null)
    {
        _world.Register(o);
        if (s != null) Loaded?.Invoke(o, s);
        o.Area = area;
        area.Objects.Add(o);
        if (o is Trigger) area.TriggerCache = null;
        if (o is Door) area.DoorCache = null;
    }

    public Placeable CreatePlaceable(Area area, GffStruct s, Vector3 position, float bearing)
    {
        var p = new Placeable
        {
            Appearance = (int)s.GetInt("Appearance"),
            Static = s.GetInt("Static") != 0,
            Useable = s.GetInt("Useable") != 0,
            HasInventory = s.GetInt("HasInventory") != 0,
            Locked = s.GetInt("Locked") != 0,
            HitPoints = (int)s.GetInt("HP"),
            CurrentHitPoints = (int)s.GetInt("CurrentHP"),
            Template = s,
            Position = position,
            Facing = bearing,
        };
        ReadCommon(p, s);
        ReadScripts(p, s, "OnClosed", "OnDamaged", "OnDeath", "OnDisarm", "OnHeartbeat", "OnInvDisturbed", "OnLock",
            "OnMeleeAttacked", "OnOpen", "OnSpellCastAt", "OnTrapTriggered", "OnUnlock", "OnUsed", "OnUserDefined", "OnClick", "OnDialog");
        Place(area, p, s);
        var model = _rm.TwoDA("placeables").Get(p.Appearance, "ModelName");
        if (_models.GetWalkmesh(model, ResType.Pwk) is { } pwk)
        {
            area.Walkmesh.SetPlaceableOwner((int)p.Id);
            AddWalkmesh(area.Walkmesh, pwk, Placement(position, bearing), onlyWalkmeshNodes: false, owner: (int)p.Id);
        }
        return p;
    }

    void AddPlaceable(Area area, GffStruct s) =>
        CreatePlaceable(area, s, new Vector3(s.GetFloat("X"), s.GetFloat("Y"), s.GetFloat("Z")), s.GetFloat("Bearing"));

    void AddDoor(Area area, GffStruct s) =>
        CreateDoor(area, s, new Vector3(s.GetFloat("X"), s.GetFloat("Y"), s.GetFloat("Z")), s.GetFloat("Bearing"));

    /// <summary>A door from a GIT or UTD struct at a position. Its walkmesh is added to the area's.</summary>
    public Door CreateDoor(Area area, GffStruct s, Vector3 position, float bearing)
    {
        var d = new Door
        {
            Appearance = (int)s.GetInt("Appearance"),
            GenericType = (int)s.GetInt("GenericType_New", s.GetInt("GenericType")), // EE writes GenericType_New
            Locked = s.GetInt("Locked") != 0,
            LinkedTo = s.GetString("LinkedTo"),
            LinkedToFlags = (byte)s.GetInt("LinkedToFlags"),
            HitPoints = (int)s.GetInt("HP"),
            CurrentHitPoints = (int)s.GetInt("CurrentHP"),
            Template = s,
            Position = position,
            Facing = bearing,
        };
        ReadCommon(d, s);
        ReadScripts(d, s, "OnClick", "OnClosed", "OnDamaged", "OnDeath", "OnDisarm", "OnFailToOpen", "OnHeartbeat", "OnLock",
            "OnMeleeAttacked", "OnOpen", "OnSpellCastAt", "OnTrapTriggered", "OnUnlock", "OnUserDefined", "OnDialog");
        Place(area, d, s);
        d.WalkmeshOwner = (int)d.Id;
        string model = DoorModel(d);
        if (_models.GetWalkmesh(model, ResType.Dwk) is { } dwk) AddDoorWalkmesh(area.Walkmesh, d, dwk, Placement(d.Position, d.Facing), AddWalkmesh);
        return d;
    }

    /// <summary>
    /// A door's walkmesh: the closed slab, and the leaf swung open to either side (wg_open1, wg_open2), each under its
    /// own owner so that opening and closing switch them (DoorService).
    /// </summary>
    public static void AddDoorWalkmesh(Walkmesh wm, Door d, ModelData dwk, Matrix4x4 placement,
        Action<Walkmesh, ModelData, Matrix4x4, bool, int, Func<ModelPart, bool>?> add)
    {
        wm.SetClosedDoorOwner(d.WalkmeshOwner);
        add(wm, dwk, placement, false, d.WalkmeshOwner, part => part.Name.Contains("closed", StringComparison.OrdinalIgnoreCase));
        for (int side = 1; side <= 2; side++)
        {
            string name = "wg_open" + side;
            add(wm, dwk, placement, false, d.LeafOwner(side), part => part.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            wm.SetOwnerEnabled(d.LeafOwner(side), false);
        }
    }

    public string DoorModel(Door d) => d.Appearance > 0
        ? _rm.TwoDA("doortypes").Get(d.Appearance, "Model")
        : _rm.TwoDA("genericdoors").Get(d.GenericType, "ModelName");

    void AddWaypoint(Area area, GffStruct s) => CreateWaypoint(area, s, Positioned(s), Oriented(s));

    static Vector3 Positioned(GffStruct s) => new(s.GetFloat("XPosition"), s.GetFloat("YPosition"), s.GetFloat("ZPosition"));
    static float Oriented(GffStruct s) => MathF.Atan2(s.GetFloat("YOrientation"), s.GetFloat("XOrientation"));

    public Waypoint CreateWaypoint(Area area, GffStruct s, Vector3 position, float facing)
    {
        var w = new Waypoint
        {
            Position = position,
            Facing = facing,
            HasMapNote = s.GetInt("HasMapNote") != 0,
            MapNote = s.GetLocString("MapNote"),
        };
        ReadCommon(w, s, "LocalizedName");
        Place(area, w, s);
        return w;
    }

    void AddTrigger(Area area, GffStruct s) => CreateTrigger(area, s, Positioned(s));

    /// <summary>A trigger from a GIT or UTT struct; its geometry points are relative to <paramref name="origin"/>.</summary>
    public Trigger CreateTrigger(Area area, GffStruct s, Vector3 origin)
    {
        var t = new Trigger
        {
            Position = origin,
            TriggerType = (int)s.GetInt("Type"),
            LinkedTo = s.GetString("LinkedTo"),
            LinkedToFlags = (byte)s.GetInt("LinkedToFlags"),
            Cursor = (int)s.GetInt("Cursor"),
        };
        foreach (var p in s.GetList("Geometry"))
            t.Geometry.Add(origin + new Vector3(p.GetFloat("PointX"), p.GetFloat("PointY"), p.GetFloat("PointZ")));
        ReadCommon(t, s, "LocalizedName");
        ReadScripts(t, s, "OnClick", "OnDisarm", "OnTrapTriggered", "ScriptHeartbeat", "ScriptOnEnter", "ScriptOnExit", "ScriptUserDefine");
        Place(area, t, s);
        return t;
    }

    void AddSound(Area area, GffStruct s) => CreateSound(area, s, Positioned(s));

    public Sound CreateSound(Area area, GffStruct s, Vector3 position)
    {
        var o = new Sound
        {
            Template = s,
            Position = position,
            Active = s.GetInt("Active") != 0,
            Continuous = s.GetInt("Continuous") != 0,
            Looping = s.GetInt("Looping") != 0,
            Positional = s.GetInt("Positional") != 0,
            MaxDistance = s.GetFloat("MaxDistance"),
            MinDistance = s.GetFloat("MinDistance"),
            Volume = (int)s.GetInt("Volume", 127),
        };
        foreach (var snd in s.GetList("Sounds")) o.Sounds.Add(snd.GetString("Sound"));
        ReadCommon(o, s, "LocName");
        Place(area, o, s);
        return o;
    }

    void AddEncounter(Area area, GffStruct s) => CreateEncounter(area, s, Positioned(s));

    /// <summary>An encounter from a GIT or UTE struct; its geometry points are relative to <paramref name="origin"/>.</summary>
    public Encounter CreateEncounter(Area area, GffStruct s, Vector3 origin)
    {
        var e = new Encounter { Position = origin, Active = s.GetInt("Active") != 0, Template = s };
        foreach (var p in s.GetList("Geometry"))
            e.Geometry.Add(origin + new Vector3(p.GetFloat("X"), p.GetFloat("Y"), p.GetFloat("Z")));
        ReadCommon(e, s, "LocalizedName");
        ReadScripts(e, s, "OnEntered", "OnExhausted", "OnExit", "OnHeartbeat", "OnUserDefined");
        Place(area, e, s);
        return e;
    }

    void AddStore(Area area, GffStruct s) => CreateStore(area, s, Positioned(s), Oriented(s));

    /// <summary>A store from a GIT or UTM struct; its stock is read from the struct when first needed.</summary>
    public Store CreateStore(Area area, GffStruct s, Vector3 position, float facing)
    {
        var o = new Store
        {
            Position = position,
            Facing = facing,
            Template = s,
        };
        ReadCommon(o, s, "LocName");
        ReadScripts(o, s, "OnOpenStore", "OnStoreClosed");
        Place(area, o, s);
        return o;
    }

    void AddItem(Area area, GffStruct s)
    {
        var i = new Item
        {
            Position = new Vector3(s.GetFloat("XPosition"), s.GetFloat("YPosition"), s.GetFloat("ZPosition")),
            Facing = MathF.Atan2(s.GetFloat("YOrientation"), s.GetFloat("XOrientation")),
            BaseItem = (int)s.GetInt("BaseItem"),
            StackSize = (int)Math.Max(1, s.GetInt("StackSize")),
            Template = s,
        };
        ReadCommon(i, s, "LocalizedName");
        Place(area, i, s);
    }

    void AddCreature(Area area, GffStruct s)
    {
        var c = _world.Creatures.FromTemplate(s, validateStartingEquipment: true);
        // Like any creature arriving, module creatures on a taken spot move to the nearest free one.
        c.Position = PersonalSpace.FindSpot(_world, area, c, new Vector3(s.GetFloat("XPosition"), s.GetFloat("YPosition"), s.GetFloat("ZPosition")));
        c.Facing = MathF.Atan2(s.GetFloat("YOrientation"), s.GetFloat("XOrientation"));
        Place(area, c, s);
        area.CreatureGrid.Update(c);
    }
}
