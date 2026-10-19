using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Rendering;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Map;

/// <summary>A map note (a waypoint with a map note) in area metres.</summary>
public readonly record struct MapPin(string Note, Vector2 Position, string Id = "", bool Personal = false);

/// <summary>
/// What the player knows of the current area's map, shared by the map window and the HUD minimap:
/// the tiles' minimap images (tileset ImageMap2D, turned to each tile's orientation) composed
/// into one texture, a soft fog-of-war mask of the explored tiles, the map notes and the party.
/// One per session (<see cref="Of"/>); it follows area changes and the server's exploration notifies.
/// </summary>
public sealed class AreaMap
{
    /// <summary>Area metres per tile.</summary>
    public const float TileSize = 10f;
    /// <summary>Fog mask pixels per tile (the mask is blurred, so edges fade over about a third of a tile).</summary>
    const int FogPerTile = 8;

    static readonly Dictionary<ClientSession, AreaMap> BySession = new();

    public static AreaMap Of(ClientSession session)
    {
        if (!BySession.TryGetValue(session, out var map)) BySession[session] = map = new AreaMap(session);
        return map;
    }

    readonly ClientSession _session;
    bool[] _explored = [];
    readonly List<MapPin> _pins = new();
    readonly HashSet<uint> _party = new();
    bool _fogDirty, _pinRefreshQueued;

    public uint AreaId { get; private set; }
    public string Name { get; private set; } = "";
    public int Width { get; private set; }
    public int Height { get; private set; }
    /// <summary>All the area's tiles, north at the top, <see cref="PixelsPerTile"/> per tile; null before an area loads.</summary>
    public ImageTexture? Tiles { get; private set; }
    /// <summary>Explored mask matching <see cref="Tiles"/> (white = seen), blurred for soft edges.</summary>
    public ImageTexture? Fog { get; private set; }
    public int PixelsPerTile { get; private set; } = 32;
    public IReadOnlyList<MapPin> Pins => _pins;
    /// <summary>Object ids of the player's party and associates (from the HUD feed).</summary>
    public IReadOnlyCollection<uint> Party => _party;
    public int ExploredCount { get; private set; }
    /// <summary>The area's size in metres.</summary>
    public Vector2 Extent => new(Width * TileSize, Height * TileSize);
    /// <summary>Raised when the area, tiles or fog changed (not every frame).</summary>
    public event Action? Changed;

    AreaMap(ClientSession session)
    {
        _session = session;
        session.NotifyReceived += OnNotify;
        session.AreaLoaded += OnAreaLoaded;
        session.TreeExiting += () => BySession.Remove(session);
        if (session.Area != null) OnAreaLoaded();
    }

    /// <summary>Asks the server for the explored tiles and map notes again (the map window does on open).</summary>
    public void Refresh() => _session.Request("area-map");

    public void AddPin(uint area, Vector2 at, string note) => _session.Request("map-pin-add", new { area, x = at.X, y = at.Y, note });
    public void EditPin(uint area, string id, string note) => _session.Request("map-pin-edit", new { area, id, note });
    public void RemovePin(uint area, string id) => _session.Request("map-pin-remove", new { area, id });

    public bool IsExplored(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height && _explored.Length == Width * Height && _explored[y * Width + x];

    public bool IsExplored(Vector2 world) => IsExplored((int)MathF.Floor(world.X / TileSize), (int)MathF.Floor(world.Y / TileSize));

    void OnAreaLoaded()
    {
        if (_session.Area?.Info is not { } info) return;
        AreaId = info.AreaId;
        Name = Kit.NwnText.Strip(info.Name);
        Width = info.Width;
        Height = info.Height;
        _explored = new bool[Width * Height];
        ExploredCount = 0;
        _pins.Clear();
        Compose(info);
        RebuildFog();
        Changed?.Invoke();
        Refresh();
    }

    void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "tile-explored":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var d = doc.RootElement;
                if (d.GetProperty("area").GetUInt32() != AreaId) return;
                int x = d.GetProperty("x").GetInt32(), y = d.GetProperty("y").GetInt32(), i = y * Width + x;
                if (x < 0 || y < 0 || x >= Width || y >= Height || i >= _explored.Length) return;
                bool now = d.GetProperty("explored").GetBoolean();
                if (_explored[i] == now) return;
                _explored[i] = now;
                ExploredCount += now ? 1 : -1;
                // Tiles arrive a few at a time; rebuild the mask once per frame at most.
                if (!_fogDirty)
                {
                    _fogDirty = true;
                    Callable.From(() => { _fogDirty = false; RebuildFog(); Changed?.Invoke(); }).CallDeferred();
                }
                break;
            }
            case "map-explored":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var d = doc.RootElement;
                if (d.GetProperty("area").GetUInt32() != AreaId) return;
                bool explored = d.GetProperty("explored").GetBoolean();
                Array.Fill(_explored, explored);
                ExploredCount = explored ? _explored.Length : 0;
                RebuildFog();
                Changed?.Invoke();
                break;
            }
            case "map-pin":
                // A waypoint changed. Refresh the current area's authoritative pin
                // list, preserving private notes and excluding other areas' pins.
                // Area entry can deliver many waypoint overrides in one batch.
                if (!_pinRefreshQueued)
                {
                    _pinRefreshQueued = true;
                    Callable.From(() => { _pinRefreshQueued = false; Refresh(); }).CallDeferred();
                }
                break;
            case "area-map":
            {
                using var doc = JsonDocument.Parse(m.Json);
                var d = doc.RootElement;
                if (d.GetProperty("area").GetUInt32() != AreaId) return;
                var bits = d.GetProperty("explored").GetString() ?? "";
                ExploredCount = 0;
                for (int i = 0; i < _explored.Length; i++)
                {
                    _explored[i] = i < bits.Length && bits[i] == '1';
                    if (_explored[i]) ExploredCount++;
                }
                _pins.Clear();
                foreach (var p in d.GetProperty("pins").EnumerateArray())
                    _pins.Add(new MapPin(Kit.NwnText.Strip(p.GetProperty("note").GetString() ?? "").Trim(), new Vector2(p.GetProperty("x").GetSingle(), p.GetProperty("y").GetSingle()),
                        p.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "", p.TryGetProperty("personal", out var personal) && personal.GetBoolean()));
                RebuildFog();
                Changed?.Invoke();
                break;
            }
            case "hud":
            {
                using var doc = JsonDocument.Parse(m.Json);
                if (!doc.RootElement.TryGetProperty("party", out var party)) return;
                _party.Clear();
                foreach (var p in party.EnumerateArray())
                    if (p.TryGetProperty("id", out var id)) _party.Add(id.GetUInt32());
                break;
            }
        }
    }

    /// <summary>Builds the area texture from the tileset's per-tile minimap images.</summary>
    void Compose(LoadAreaMessage info)
    {
        Tiles = null;
        if (_session.Content is not { } content || Width <= 0 || Height <= 0) return;
        var set = Ini.Parse(content.Resources.Read(info.Tileset, ResType.Set) ?? []);
        var sources = new Dictionary<string, Image?>(StringComparer.OrdinalIgnoreCase);
        Image? Source(string name)
        {
            if (sources.TryGetValue(name, out var img)) return img;
            img = null;
            if (content.Textures.Get(name) is { Data: { } data } entry && TextureFactory.ToImage(data) is { } raw)
            {
                if (raw.IsCompressed()) raw.Decompress();
                raw.ClearMipmaps();
                raw.Convert(Image.Format.Rgba8);
                if (entry.FlipV) raw.FlipY();
                img = raw;
            }
            sources[name] = img;
            return img;
        }
        var names = new string[info.Tiles.Length];
        int largest = 16;
        for (int i = 0; i < info.Tiles.Length; i++)
        {
            names[i] = set.Get($"TILE{info.Tiles[i].Id}", "ImageMap2D").ToLowerInvariant();
            if (names[i].Length > 0 && Source(names[i]) is { } s) largest = Math.Max(largest, s.GetWidth());
        }
        // The tileset's images are small (16 px); scale them up smoothly once here rather than
        // blurring them on screen. Big areas keep the texture under 2048 pixels a side.
        PixelsPerTile = Math.Clamp(largest * 4, 16, Math.Max(16, 2048 / Math.Max(Width, Height)));
        int t = PixelsPerTile;
        var canvas = Image.CreateEmpty(Width * t, Height * t, false, Image.Format.Rgba8);
        canvas.Fill(new Color(0.16f, 0.14f, 0.11f));
        var turned = new Dictionary<(string, int), Image>();
        for (int i = 0; i < info.Tiles.Length && i < Width * Height; i++)
        {
            if (names[i].Length == 0 || Source(names[i]) is not { } src) continue;
            int o = info.Tiles[i].Orientation & 3;
            if (!turned.TryGetValue((names[i], o), out var img))
            {
                img = (Image)src.Duplicate();
                if (img.GetWidth() != t || img.GetHeight() != t) img.Resize(t, t, Image.Interpolation.Cubic);
                // Tiles turn counter-clockwise in the world, and so does their picture on a north-up map.
                for (int k = 0; k < o; k++) img.Rotate90(ClockDirection.Counterclockwise);
                turned[(names[i], o)] = img;
            }
            int x = i % Width, y = i / Width;
            canvas.BlitRect(img, new Rect2I(0, 0, t, t), new Vector2I(x * t, (Height - 1 - y) * t));
        }
        canvas.GenerateMipmaps();
        Tiles = ImageTexture.CreateFromImage(canvas);
    }

    /// <summary>Soft-edged explored mask: tile blocks, box-blurred both ways.</summary>
    void RebuildFog()
    {
        if (Width <= 0 || Height <= 0) { Fog = null; return; }
        int w = Width * FogPerTile, h = Height * FogPerTile;
        var a = new float[w * h];
        for (int ty = 0; ty < Height; ty++)
        for (int tx = 0; tx < Width; tx++)
        {
            if (!_explored[ty * Width + tx]) continue;
            int row0 = (Height - 1 - ty) * FogPerTile;
            for (int py = 0; py < FogPerTile; py++)
            for (int px = 0; px < FogPerTile; px++)
                a[(row0 + py) * w + tx * FogPerTile + px] = 1;
        }
        const int r = 3;
        var b = new float[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float sum = 0; int n = 0;
            for (int k = -r; k <= r; k++) { int xx = x + k; if (xx < 0 || xx >= w) continue; sum += a[y * w + xx]; n++; }
            b[y * w + x] = sum / n;
        }
        var bytes = new byte[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float sum = 0; int n = 0;
            for (int k = -r; k <= r; k++) { int yy = y + k; if (yy < 0 || yy >= h) continue; sum += b[yy * w + x]; n++; }
            bytes[y * w + x] = (byte)Math.Round(sum / n * 255);
        }
        var img = Image.CreateFromData(w, h, false, Image.Format.L8, bytes);
        if (Fog != null && Fog.GetWidth() == w && Fog.GetHeight() == h) Fog.Update(img);
        else Fog = ImageTexture.CreateFromImage(img);
    }
}
