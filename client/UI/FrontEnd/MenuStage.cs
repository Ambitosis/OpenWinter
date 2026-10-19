using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.FrontEnd;

/// <summary>
/// The main menu's 3D backdrop: one area of a module (tiles, placeables and doors from its ARE / GIT,
/// read client-side, no server) seen from a camera slowly circling over it at dusk.
/// </summary>
public partial class MenuStage : Node3D
{
    /// <summary>Raised on the main thread once the area is built and showing (false: nothing could be built).</summary>
    public event Action<bool>? Ready_;
    public string AreaName { get; private set; } = "";

    Camera3D _camera = null!;
    Vector3 _center;
    float _radius = 30, _height = 14, _angle;
    float _speed = 0.022f;

    sealed record Plan(ResMan Rm, LoadAreaMessage Area, List<SpawnMessage> Objects);

    /// <summary>Starts building <paramref name="area"/> of <paramref name="modulePath"/> (null area: a scenic one is picked), by moonlight when <paramref name="night"/>.</summary>
    public void Load(GamePaths paths, string modulePath, string? area, bool night = false)
    {
        _ = Task.Run(() =>
        {
            try { return Prepare(paths, modulePath, area, night); }
            catch (Exception e) when (e is IOException or InvalidDataException or EndOfStreamException or UnauthorizedAccessException or ArgumentException)
            {
                GD.PrintErr("[menu] backdrop: " + e.Message);
                return null;
            }
        }).ContinueWith(t => Callable.From(() => Build(t.IsCompletedSuccessfully ? t.Result : null)).CallDeferred());
    }

    static Plan? Prepare(GamePaths paths, string modulePath, string? areaResRef, bool night)
    {
        var rm = ResMan.ForModule(paths, modulePath);
        var ifo = rm.ReadGff("module", ResType.Ifo);
        if (ifo == null) return null;
        var areas = ifo.GetList("Mod_Area_list").Select(a => a.GetString("Area_Name")).Where(a => a.Length > 0).ToList();
        string? pick = areaResRef;
        if (pick == null)
        {
            // Prefer a natural exterior with some size and placeables.
            var scored = new List<(string ResRef, int Score)>();
            foreach (var a in areas.Take(60))
            {
                var are = rm.ReadGff(a, ResType.Are);
                if (are == null) continue;
                int flags = (int)are.GetInt("Flags");
                int w = (int)are.GetInt("Width"), h = (int)are.GetInt("Height");
                int score = Math.Min(w * h, 64) + ((flags & 1) == 0 ? 40 : 0) + ((flags & 4) != 0 ? 20 : 0) - ((flags & 2) != 0 ? 30 : 0);
                scored.Add((a, score));
            }
            pick = scored.OrderByDescending(s => s.Score).FirstOrDefault().ResRef ?? ifo.GetString("Mod_Entry_Area");
        }
        var areGff = rm.ReadGff(pick, ResType.Are);
        if (areGff == null) return null;
        var git = rm.ReadGff(pick, ResType.Git) ?? new GffStruct();
        var msg = Describe(pick, areGff, git, rm.Talk, night);
        var objects = new List<SpawnMessage>();
        uint id = 1;
        foreach (var p in git.GetList("Placeable List"))
            objects.Add(new SpawnMessage
            {
                ObjectId = id++, Kind = ObjectKind.Placeable, Position = Pos(p), Facing = p.GetFloat("Bearing"),
                Appearance = new Appearance { Type = (int)p.GetInt("Appearance") },
                // AnimationState: 1 open, 3 destroyed, 4 activated (as the server reads it).
                Flags = (p.GetInt("AnimationState") switch { 1 => ObjectFlags.Open, 3 => ObjectFlags.Dead, 4 => ObjectFlags.Activated, _ => 0 }),
            });
        foreach (var d in git.GetList("Door List"))
            objects.Add(new SpawnMessage
            {
                ObjectId = id++, Kind = ObjectKind.Door, Position = Pos(d), Facing = d.GetFloat("Bearing"),
                Appearance = new Appearance { Type = (int)d.GetInt("Appearance"), GenericDoorType = (int)d.GetInt("GenericType") },
            });
        return new Plan(rm, msg, objects);
    }

    static System.Numerics.Vector3 Pos(GffStruct s) =>
        new(s.GetFloat("X", s.GetFloat("XPosition")), s.GetFloat("Y", s.GetFloat("YPosition")), s.GetFloat("Z", s.GetFloat("ZPosition")));

    /// <summary>The LoadArea message the server would send for this area (lighting fixed, no day / night cycle).</summary>
    static LoadAreaMessage Describe(string resref, GffStruct are, GffStruct git, TalkTable talk, bool night)
    {
        var list = are.GetList("Tile_List");
        var tiles = new TileInfo[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            var t = list[i];
            tiles[i] = new TileInfo
            {
                Id = (int)t.GetInt("Tile_ID"), Orientation = (byte)t.GetInt("Tile_Orientation"), Height = (sbyte)t.GetInt("Tile_Height"),
                MainLight1 = (byte)t.GetInt("Tile_MainLight1"), MainLight2 = (byte)t.GetInt("Tile_MainLight2"),
                SourceLight1 = (byte)t.GetInt("Tile_SrcLight1"), SourceLight2 = (byte)t.GetInt("Tile_SrcLight2"),
                AnimLoops = (byte)(t.GetInt("Tile_AnimLoop1") | (t.GetInt("Tile_AnimLoop2") << 1) | (t.GetInt("Tile_AnimLoop3") << 2)),
            };
        }
        int flags = (int)are.GetInt("Flags");
        var props = git.GetStruct("AreaProperties") ?? new GffStruct();
        return new LoadAreaMessage
        {
            ResRef = resref, Name = are.GetLocString("Name").Resolve(talk), Tileset = are.GetString("Tileset"),
            Width = (int)are.GetInt("Width"), Height = (int)are.GetInt("Height"), Tiles = tiles,
            IsNight = are.GetInt("IsNight") != 0 || night, IsInterior = (flags & 1) != 0, IsUnderground = (flags & 2) != 0, IsNatural = (flags & 4) != 0,
            SunAmbient = (uint)are.GetInt("SunAmbientColor"), SunDiffuse = (uint)are.GetInt("SunDiffuseColor"), SunFogColor = (uint)are.GetInt("SunFogColor"),
            MoonAmbient = (uint)are.GetInt("MoonAmbientColor"), MoonDiffuse = (uint)are.GetInt("MoonDiffuseColor"), MoonFogColor = (uint)are.GetInt("MoonFogColor"),
            SunFogAmount = (byte)are.GetInt("SunFogAmount"), MoonFogAmount = (byte)are.GetInt("MoonFogAmount"),
            SunShadows = are.GetInt("SunShadows") != 0, MoonShadows = are.GetInt("MoonShadows") != 0,
            FogClipDistance = are.GetFloat("FogClipDist", 45f), SkyBox = are.GetInt("SkyBox").ToString(),
            MusicDay = (int)props.GetInt("MusicDay"), MusicNight = (int)props.GetInt("MusicNight"),
            DayNightCycle = false,
        };
    }

    void Build(Plan? plan)
    {
        if (!IsInsideTree() || plan == null) { Ready_?.Invoke(false); return; }
        try
        {
            var content = new ClientContent(plan.Rm);
            var space = new Node3D { Name = "AuroraSpace", RotationDegrees = new Vector3(-90, 0, 0) };
            AddChild(space);
            var area = AreaView.Build(plan.Area, content, out var env, out var sun);
            space.AddChild(area);
            // A filmic grade for the title screen: a little darker, richer contrast, softer colour.
            env.TonemapMode = Godot.Environment.ToneMapper.Aces;
            env.AdjustmentEnabled = true;
            bool night = plan.Area.IsNight;
            env.AdjustmentBrightness = night ? 1.35f : 0.92f;
            env.AdjustmentContrast = 1.12f;
            env.AdjustmentSaturation = night ? 0.62f : 0.82f;
            env.GlowEnabled = true;
            env.GlowIntensity = 0.7f;
            env.GlowBloom = 0.06f;
            AddChild(new WorldEnvironment { Environment = env });
            AddChild(sun);
            area.Bind(env, sun, content);
            foreach (var spawn in plan.Objects)
                if (StaticObjectView.Create(spawn, content) is { } view) space.AddChild(view);
            AreaName = plan.Area.Name;
            float w = plan.Area.Width * 10f, h = plan.Area.Height * 10f;
            float ground = plan.Area.Tiles.Length > 0 ? (float)plan.Area.Tiles.Average(t => t.Height) * 5f : 0;
            _center = new Vector3(w / 2, ground + 1.5f, -h / 2);
            // The fog counts from the player in play; here, from the middle the camera circles.
            area.FogCentre = _center;
            _radius = Math.Clamp(Math.Min(w, h) * 0.42f, 12f, 48f);
            // Fly above the rooftops and treetops: most of the scene stays below the camera.
            var tops = space.FindChildren("*", "MeshInstance3D", true, false).OfType<MeshInstance3D>()
                .Select(mi => (mi.GlobalTransform * mi.GetAabb()).End.Y).Where(y => !float.IsNaN(y)).OrderBy(y => y).ToList();
            float high = tops.Count > 0 ? tops[(int)(tops.Count * 0.995f)] : ground + 10;
            _height = Math.Clamp(high + 4f, ground + 10f, ground + 42f);
            _radius = Math.Max(_radius, (_height - ground) * 1.2f);
            _angle = 0.6f;
            _camera = new Camera3D { Current = true, Fov = 48 };
            AddChild(_camera);
            Place();
            Ready_?.Invoke(true);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or EndOfStreamException or ArgumentException or NullReferenceException or IndexOutOfRangeException)
        {
            GD.PrintErr("[menu] backdrop build: " + e.Message);
            Ready_?.Invoke(false);
        }
    }

    public override void _Process(double delta)
    {
        if (_camera == null) return;
        _angle += _speed * (float)delta;
        Place();
    }

    void Place()
    {
        var focus = _center + new Vector3(0, 1.5f, 0);
        var pos = _center + new Vector3(MathF.Cos(_angle) * _radius, _height - _center.Y, MathF.Sin(_angle) * _radius);
        _camera.GlobalPosition = pos;
        _camera.LookAt(focus);
    }
}
