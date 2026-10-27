using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Rendering;
using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>
/// Script-driven changes to a built area: tiles replaced or re-lit (SetTile, SetTileJson,
/// SetTileMainLightColor/SourceLightColor), light directions (SetAreaLightDirection),
/// forced day or night (DayToNight/NightToDay) and sky removal. Driven by
/// <see cref="WorldFx.WorldFxPlayer"/>.
/// </summary>
public partial class AreaView
{
    ClientContent? _tileContent;
    Ini? _tileSet;
    float _tileStep = 5f;
    Node3D? _tilesRoot;
    Node3D?[] _tileNodes = Array.Empty<Node3D?>();
    TileExploration?[] _tileDiscovery = Array.Empty<TileExploration?>();
    List<(string Name, OmniLight3D Light)>[] _tileLights = Array.Empty<List<(string, OmniLight3D)>>();
    List<Vector3>[] _tileWalk = Array.Empty<List<Vector3>>();
    /// <summary>Per tile: its source-light nodes (1 or 2, where) and the flames placed on them.</summary>
    List<(int Which, Transform3D At)>[] _tileSourceNodes = Array.Empty<List<(int, Transform3D)>>();
    Node3D?[] _tileSourceRoots = Array.Empty<Node3D?>();
    List<Node3D>[] _tileSources = Array.Empty<List<Node3D>>();

    /// <summary>The environment and sun/moon light the area drives (weather flashes add to them).</summary>
    public Godot.Environment? Environment => _env;
    public DirectionalLight3D? SunLight => _sun;

    /// <summary>The tileset description (.set) the tiles are built from.</summary>
    public Ini? TileSet => _tileSet;
    public float TileStep => _tileStep;

    void BuildTiles(ClientContent content, Ini set, float step, Node3D tiles)
    {
        _tileContent = content;
        _tileSet = set;
        _tileStep = step;
        _tilesRoot = tiles;
        int n = Info.Tiles.Length;
        _tileNodes = new Node3D?[n];
        _tileDiscovery = new TileExploration?[n];
        _tileLights = new List<(string, OmniLight3D)>[n];
        _tileWalk = new List<Vector3>[n];
        _tileSourceNodes = new List<(int, Transform3D)>[n];
        _tileSourceRoots = new Node3D?[n];
        _tileSources = new List<Node3D>[n];
        ResetTileFades(n);
        using (Gameplay.AreaLoadTiming.Phase("tiles"))
            for (int i = 0; i < n; i++) BuildTile(i);
        using (Gameplay.AreaLoadTiming.Phase("border")) BuildBorder();
        RebuildWalkTriangles();
    }

    /// <summary>Where tile <paramref name="i"/> sits: its grid square, orientation and height.</summary>
    public Transform3D TileTransform(int i)
    {
        var t = Info.Tiles[i];
        return new Transform3D(new Basis(Vector3.Back, MathF.PI / 2f * t.Orientation),
            new Vector3((i % Info.Width + 0.5f) * 10f, (i / Info.Width + 0.5f) * 10f, t.Height * _tileStep));
    }

    /// <summary>The model of tile <paramref name="i"/>, from the tileset.</summary>
    public ModelData? TileModel(int i) =>
        _tileContent == null || _tileSet == null ? null : _tileContent.Models.Get(_tileSet.Get($"TILE{Info.Tiles[i].Id}", "Model"));

    void BuildTile(int i)
    {
        _tileDiscovery[i] = null;
        _tileLights[i] = new();
        _tileWalk[i] = new();
        _tileSourceNodes[i] = new();
        _tileSources[i] ??= new();
        _tileSourceRoots[i] = null;
        var data = TileModel(i);
        if (data == null && _tileContent != null && _tileSet != null)
            _tileContent.WarnOnce($"tile {Info.Tiles[i].Id} of {Info.Tileset}: model '{_tileSet.Get($"TILE{Info.Tiles[i].Id}", "Model")}' not found; tile not drawn");
        if (data == null || _tileContent == null || _tilesRoot == null) return;
        var built = ModelBuilder.BuildStatic(data, _tileContent.Materials, shareMeshes: true);
        built.Root.Transform = TileTransform(i);
        built.Root.Name = $"Tile{i}_{built.Root.Name}";
        var globals = data.RestGlobals();
        for (int p = 0; p < data.Parts.Count; p++)
        {
            if (data.Parts[p].Mesh is not { IsWalkmesh: true } wm) continue;
            var xf = built.Root.Transform * Rendering.Convert.T(globals[p]);
            var surfaces = _tileContent.Resources.TwoDA("surfacemat");
            for (int f = 0; f + 2 < wm.Indices.Length; f += 3)
            {
                if (surfaces.Get(f / 3 < wm.FaceSurfaces.Length ? wm.FaceSurfaces[f / 3] : 0, "Walk") != "1") continue;
                for (int v = 0; v < 3; v++) _tileWalk[i].Add(xf * Rendering.Convert.V(wm.Vertices[wm.Indices[f + v]]));
            }
        }
        _tileLights[i] = built.Lights;
        _tileSourceNodes[i] = data.Parts.Select((part, p) => (part, p))
            .Where(x => x.part.Name.EndsWith("sl1", StringComparison.OrdinalIgnoreCase) || x.part.Name.EndsWith("sl2", StringComparison.OrdinalIgnoreCase))
            .Select(x => (x.part.Name.EndsWith("sl1", StringComparison.OrdinalIgnoreCase) ? 1 : 2, Rendering.Convert.T(globals[x.p]))).ToList();
        // Keep effects and lights independently hidden while unknown solid geometry still occludes.
        var details = new Node3D { Name = "Details" };
        foreach (var child in built.Root.GetChildren().Where(c => c is not MeshInstance3D).ToArray())
        {
            built.Root.RemoveChild(child);
            details.AddChild(child);
        }
        built.Root.AddChild(details);
        _tileSourceRoots[i] = details;
        ColorTileLights(i);
        _tilesRoot.AddChild(built.Root);
        SetTileFades(i, built.Fades, data, built.Faces);
        AddCameraCollision(built.Root, data, built.Faces);
        Rendering.Keyhole.Apply(built.Root);
        _tileNodes[i] = built.Root;
        _tileDiscovery[i] = Info.IsInterior ? new TileExploration(built.Root, details) : null;
        if (_explored != null) _tileDiscovery[i]!.SetExplored(_explored[i]);
    }

    void RebuildWalkTriangles()
    {
        WalkTriangles.Clear();
        foreach (var list in _tileWalk) if (list != null) WalkTriangles.AddRange(list);
    }

    /// <summary>Colours a tile's main and source lights from lightcolor.2da (row 0 and black rows are off).</summary>
    void ColorTileLights(int i)
    {
        if (_tileContent == null) return;
        var t = Info.Tiles[i];
        var lightColors = _tileContent.Resources.TwoDA("lightcolor");
        foreach (var (name, light) in _tileLights[i])
        {
            int index = name.EndsWith("ml1", StringComparison.OrdinalIgnoreCase) ? t.MainLight1
                : name.EndsWith("ml2", StringComparison.OrdinalIgnoreCase) ? t.MainLight2
                : name.EndsWith("sl1", StringComparison.OrdinalIgnoreCase) ? t.SourceLight1
                : name.EndsWith("sl2", StringComparison.OrdinalIgnoreCase) ? t.SourceLight2 : -1;
            if (index < 0) continue;
            var c = new Color(lightColors.GetFloat(index, "RED"), lightColors.GetFloat(index, "GREEN"), lightColors.GetFloat(index, "BLUE"));
            // Source lights are the flames placed on the sl nodes (below), not lightcolor.2da colours.
            bool source = name.EndsWith("sl1", StringComparison.OrdinalIgnoreCase) || name.EndsWith("sl2", StringComparison.OrdinalIgnoreCase);
            if (source || index == 0 || c.R + c.G + c.B < 0.01f) light.Visible = false;
            else
            {
                light.Visible = true;
                light.LightColor = c;
            }
        }
        PlaceSourceLights(i);
    }

    /// <summary>
    /// Tile source lights: a tile's "sl1"/"sl2" nodes carry the flame model fx_flame01 playing the animation
    /// named after the tile's source light value (1-15, 0 = none), which sets the flame and its light's colour
    /// (2 a pale yellow torch, 3 a bright yellow one...). These are the wall torches and braziers of interiors.
    /// </summary>
    void PlaceSourceLights(int i)
    {
        foreach (var old in _tileSources[i]) if (IsInstanceValid(old)) old.QueueFree();
        _tileSources[i].Clear();
        if (_tileContent == null || _tileSourceRoots[i] is not { } root || _tileSourceNodes[i].Count == 0) return;
        var flame = _tileContent.Models.Get("fx_flame01");
        if (flame == null) return;
        var t = Info.Tiles[i];
        var globals = flame.RestGlobals();
        foreach (var (which, at) in _tileSourceNodes[i])
        {
            int value = which == 1 ? t.SourceLight1 : t.SourceLight2;
            if (value <= 0 || !flame.Animations.TryGetValue(value.ToString(), out var anim)) continue;
            var holder = new Node3D { Name = $"SourceLight{which}", Transform = at };
            for (int p = 0; p < flame.Parts.Count; p++)
            {
                var part = flame.Parts[p];
                var track = anim.Tracks.GetValueOrDefault(part.Name);
                var xf = Rendering.Convert.T(globals[p]);
                if (part.Light is { } l)
                {
                    var c = track is { LightColors.Length: > 0 } ? track.LightColors[0] : l.Color;
                    holder.AddChild(new OmniLight3D
                    {
                        Name = part.Name, LightColor = new Color(c.X, c.Y, c.Z), OmniRange = Math.Max(0.5f, l.Radius),
                        LightEnergy = Math.Clamp(l.Multiplier, 0.2f, 3f) * ModelBuilder.LightScale, ShadowEnabled = false, OmniAttenuation = 1.2f, Transform = xf,
                    });
                }
                if (part.Emitter is { } e)
                {
                    var keyed = track is { EmitterKeys.Count: > 0 } ? ModelBuilder.Sampled(e, track) : e;
                    if (keyed.BirthRate > 0 && EmitterBuilder.Build(keyed, _tileContent.Materials.Textures, new Rendering.Emitters.EmitterOptions { Warm = true }) is { } em)
                    {
                        em.Name = part.Name;
                        em.Transform = xf;
                        holder.AddChild(em);
                    }
                }
            }
            root.AddChild(holder);
            _tileSources[i].Add(holder);
        }
    }

    /// <summary>SetTileMainLightColor / SetTileSourceLightColor: re-lights one tile.</summary>
    public void RelightTile(int x, int y, int main1, int main2, int source1, int source2)
    {
        if (x < 0 || y < 0 || x >= Info.Width || y >= Info.Height) return;
        int i = y * Info.Width + x;
        if (i >= Info.Tiles.Length) return;
        Info.Tiles[i].MainLight1 = (byte)main1;
        Info.Tiles[i].MainLight2 = (byte)main2;
        Info.Tiles[i].SourceLight1 = (byte)source1;
        Info.Tiles[i].SourceLight2 = (byte)source2;
        if (i < _tileLights.Length && _tileLights[i] != null) ColorTileLights(i);
    }

    /// <summary>
    /// SetTile / SetTileJson: rebuilds the given tiles (and every tile when the tileset changed).
    /// Returns the indices that were rebuilt.
    /// </summary>
    public List<int> ReplaceTiles(string tileset, IEnumerable<(int Index, int Id, int Orientation, int Height, byte AnimLoops)> tiles)
    {
        var rebuilt = new List<int>();
        if (_tileContent == null) return rebuilt;
        foreach (var t in tiles)
        {
            if (t.Index < 0 || t.Index >= Info.Tiles.Length) continue;
            Info.Tiles[t.Index].Id = t.Id;
            Info.Tiles[t.Index].Orientation = (byte)(t.Orientation & 3);
            Info.Tiles[t.Index].Height = (sbyte)t.Height;
            Info.Tiles[t.Index].AnimLoops = t.AnimLoops;
            rebuilt.Add(t.Index);
        }
        if (tileset.Length > 0 && !string.Equals(tileset, Info.Tileset, StringComparison.OrdinalIgnoreCase))
        {
            Info.Tileset = tileset;
            _tileSet = Ini.Parse(_tileContent.Resources.Read(tileset, ResType.Set) ?? Array.Empty<byte>());
            _tileStep = _tileSet.GetFloat("GENERAL", "Transition", 5f);
            rebuilt = Enumerable.Range(0, Info.Tiles.Length).ToList();
        }
        foreach (int i in rebuilt.Distinct())
        {
            if (_tileNodes[i] is { } old)
            {
                SetTileFades(i, new());
                old.QueueFree();
                _tileNodes[i] = null;
            }
            BuildTile(i);
        }
        BuildBorder();
        RebuildWalkTriangles();
        return rebuilt;
    }

    // ---- sky ----

    /// <summary>SetSkyBox(SKYBOX_NONE): removes the sky model.</summary>
    public void ClearSky()
    {
        _sky?.QueueFree();
        _sky = null;
        _skyModel = "";
    }

    // ---- forced day/night ----

    float? _daylightTarget;
    float _daylightFrom, _daylightFade, _daylightElapsed;
    float _lastDaylight = 1f;

    /// <summary>Daylight the area is lit with this frame: 0 night .. 1 day.</summary>
    public float ShownDaylight => _lastDaylight;

    /// <summary>DayToNight / NightToDay: fades lighting to night (0) or day (1) over <paramref name="seconds"/> and holds it.</summary>
    public void ForceDaylight(float target, float seconds)
    {
        _daylightFrom = _daylightTarget.HasValue ? CurrentForced() : _lastDaylight;
        _daylightTarget = Math.Clamp(target, 0f, 1f);
        _daylightFade = Math.Max(0f, seconds);
        _daylightElapsed = 0;
    }

    float CurrentForced() => _daylightFade <= 0 ? _daylightTarget!.Value
        : Mathf.Lerp(_daylightFrom, _daylightTarget!.Value, Math.Clamp(_daylightElapsed / _daylightFade, 0f, 1f));

    /// <summary>The daylight the area shows: the clock's, or a script-forced one.</summary>
    float ScriptDaylight(float clock, double delta)
    {
        if (_daylightTarget == null) return _lastDaylight = clock;
        _daylightElapsed += (float)delta;
        return _lastDaylight = CurrentForced();
    }

    // ---- light directions ----

    Vector3? _sunDirection, _moonDirection;
    Vector3 _dirFrom, _dirTo;
    int _dirFading = -1;
    float _dirFade, _dirElapsed;

    /// <summary>SetAreaLightDirection: <paramref name="direction"/> points from the area towards the sun (type 1) or moon (type 0), in area coordinates.</summary>
    public void SetLightDirection(int type, Vector3 direction, float fade)
    {
        if (direction.LengthSquared() < 1e-6f) return;
        direction = direction.Normalized();
        var current = (type == 1 ? _sunDirection : _moonDirection) ?? CurrentLightOrigin();
        if (fade > 0)
        {
            _dirFrom = current;
            _dirTo = direction;
            _dirFading = type;
            _dirFade = fade;
            _dirElapsed = 0;
        }
        else if (_dirFading == type) _dirFading = -1;
        if (type == 1) _sunDirection = direction;
        else _moonDirection = direction;
    }

    /// <summary>Direction (area space) the current light comes from.</summary>
    Vector3 CurrentLightOrigin()
    {
        if (_sun == null) return Area(WorldFx.Logic.AreaLightDirection.Default);
        // The light shines along its -Z.
        var g = _sun.GlobalTransform.Basis.Z;
        return Area(WorldFx.Logic.AreaLightDirection.FromRenderer(new System.Numerics.Vector3(g.X, g.Y, g.Z))).Normalized();
    }

    static Vector3 Area(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);

    /// <summary>
    /// Points the sun or moon light: from a script-set direction (fading to it when asked), else from the original's
    /// fixed direction, which the hour doesn't move (scenes sundir and sunshadow).
    /// </summary>
    void AimLight(float daylight, double delta)
    {
        if (_sun == null) return;
        int type = daylight > 0.5f ? 1 : 0;
        Vector3? set = type == 1 ? _sunDirection : _moonDirection;
        var d = Area(WorldFx.Logic.AreaLightDirection.Current(type == 1, ToNumerics(_sunDirection), ToNumerics(_moonDirection)));
        if (set != null && _dirFading == type)
        {
            _dirElapsed += (float)delta;
            float t = Math.Clamp(_dirElapsed / Math.Max(0.001f, _dirFade), 0f, 1f);
            d = _dirFrom.Slerp(_dirTo, t);
            if (t >= 1) _dirFading = -1;
        }
        AimLight(_sun, d);
    }

    static System.Numerics.Vector3? ToNumerics(Vector3? v) => v is { } x ? new System.Numerics.Vector3(x.X, x.Y, x.Z) : null;

    /// <summary>Points <paramref name="light"/> so it shines from <paramref name="towardsLight"/> (area space).</summary>
    static void AimLight(DirectionalLight3D light, Vector3 towardsLight)
    {
        var r = WorldFx.Logic.AreaLightDirection.ToRenderer(new System.Numerics.Vector3(towardsLight.X, towardsLight.Y, towardsLight.Z));
        var origin = new Vector3(r.X, r.Y, r.Z).Normalized();
        var up = Math.Abs(origin.Dot(Vector3.Up)) > 0.99f ? Vector3.Forward : Vector3.Up;
        // The light travels opposite to where the sun is.
        light.Basis = Basis.LookingAt(-origin, up);
    }
}
