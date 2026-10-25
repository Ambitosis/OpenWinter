using OpenWinter.Content;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>
/// Tile fading: the upper walls, arches, roofs and tree tops a tile marks as fading can be cut down (with black caps
/// over the cut and plugs in the neighbours, see <see cref="TileFade"/>), easing in and out. A tile's caps and its
/// neighbours' plugs follow its most faded part. The original fades none of them in play: not at any camera height,
/// distance or side, with keyholing on or off and with every Hide Second Story Tiles mode (parity/visual fadecam);
/// the keyhole is what shows a hidden player. So nothing fades unless <see cref="FadeOverride"/> asks.
/// </summary>
public partial class AreaView
{
    /// <summary>Seconds a part takes to fade fully out or back in.</summary>
    const float FadeSeconds = 0.3f;
    /// <summary>Physics layer holding only fading geometry, for measuring it against the original (the fade probe).</summary>
    public const uint FadeLayer = 1 << 20;

    sealed class FadeNode
    {
        public required int Tile;
        public required MeshInstance3D Mesh;
        public float Amount;
    }

    List<(int Role, int Edge, MeshInstance3D Mesh)>[] _tileFades = Array.Empty<List<(int, int, MeshInstance3D)>>();
    List<FadeNode>[] _fadeNodes = Array.Empty<List<FadeNode>>();
    float[] _fadeAmount = Array.Empty<float>();
    readonly Dictionary<ulong, FadeNode> _fadeBodies = new();
    /// <summary>Parts not yet whole again.</summary>
    readonly HashSet<FadeNode> _activeFades = new();
    readonly HashSet<int> _touchedTiles = new();
    readonly List<FadeNode> _fadeStep = new();

    void ResetTileFades(int count)
    {
        _tileFades = new List<(int, int, MeshInstance3D)>[count];
        _fadeNodes = new List<FadeNode>[count];
        for (int i = 0; i < count; i++) { _tileFades[i] = new(); _fadeNodes[i] = new(); }
        _fadeAmount = new float[count];
        _fadeBodies.Clear();
        _activeFades.Clear();
    }

    void SetTileFades(int tile, List<(int Role, int Edge, MeshInstance3D Mesh)> fades, ModelData? model = null, IReadOnlyDictionary<string, Vector3[]>? faces = null)
    {
        if (tile < 0 || tile >= _tileFades.Length) return;
        foreach (var old in _fadeNodes[tile]) _activeFades.Remove(old);
        _tileFades[tile] = fades;
        _fadeNodes[tile] = new();
        foreach (var (role, _, mesh) in fades)
        {
            if (role != TileFade.Fade || TrimeshShape(model, mesh, faces) is not { } shape) continue;
            var node = new FadeNode { Tile = tile, Mesh = mesh };
            var body = new StaticBody3D { CollisionLayer = FadeLayer, CollisionMask = 0 };
            body.AddChild(new CollisionShape3D { Shape = shape });
            mesh.AddChild(body);
            _fadeBodies[body.GetInstanceId()] = node;
            _fadeNodes[tile].Add(node);
        }
        _fadeAmount[tile] = 0;
        ApplyFadeAround(tile);
    }

    /// <summary>Holds every fading part at <see cref="FadeOverride"/> while it is set; afterwards they ease back to whole.</summary>
    public void UpdateFade(double delta)
    {
        if (_fadeBodies.Count == 0) return;
        if (FadeOverride is { } forced)
        {
            for (int t = 0; t < _fadeNodes.Length; t++)
            {
                foreach (var n in _fadeNodes[t]) n.Amount = forced;
                _fadeAmount[t] = _fadeNodes[t].Count > 0 ? forced : 0;
            }
            for (int t = 0; t < _fadeNodes.Length; t++) ApplyFade(t);
            _activeFades.Clear();
            foreach (var list in _fadeNodes) foreach (var n in list) if (forced > 0) _activeFades.Add(n);
            return;
        }
        // Parts left faded by an override ease back in.
        if (_activeFades.Count == 0) return;
        float step = (float)delta / FadeSeconds;
        _touchedTiles.Clear();
        _fadeStep.Clear();
        _fadeStep.AddRange(_activeFades);
        foreach (var node in _fadeStep)
        {
            float next = Math.Max(0f, node.Amount - step);
            if (next != node.Amount) { node.Amount = next; _touchedTiles.Add(node.Tile); }
            if (next == 0f) _activeFades.Remove(node);
        }
        foreach (int t in _touchedTiles)
        {
            float most = 0;
            foreach (var n in _fadeNodes[t]) most = Math.Max(most, n.Amount);
            _fadeAmount[t] = most;
            ApplyFadeAround(t);
        }
    }

    /// <summary>For measuring (the visual harness): every fading part held at this amount (0 whole, 1 faded); null for play, where nothing fades.</summary>
    public static float? FadeOverride;

    /// <summary>The tile whose fading part a physics body belongs to (-1 if it is not one).</summary>
    public int FadeTileOf(GodotObject collider) => _fadeBodies.TryGetValue(collider.GetInstanceId(), out var n) ? n.Tile : -1;

    /// <summary>How faded each tile is now (its most faded part).</summary>
    public IReadOnlyList<float> TileFadeAmounts => _fadeAmount;

    void ApplyFadeAround(int tile)
    {
        ApplyFade(tile);
        int x = tile % Info.Width, y = tile / Info.Width;
        for (int e = 0; e < 4; e++)
        {
            var (dx, dy) = TileFade.Offset(e);
            int nx = x + dx, ny = y + dy;
            if (nx >= 0 && ny >= 0 && nx < Info.Width && ny < Info.Height) ApplyFade(ny * Info.Width + nx);
        }
    }

    void ApplyFade(int tile)
    {
        if (tile < 0 || tile >= _tileFades.Length) return;
        float f = _fadeAmount[tile];
        foreach (var node in _fadeNodes[tile])
            if (IsInstanceValid(node.Mesh)) Show(node.Mesh, 1f - node.Amount);
        foreach (var (role, edge, mesh) in _tileFades[tile])
        {
            if (role == TileFade.Fade || !IsInstanceValid(mesh)) continue;
            Show(mesh, role == TileFade.Base ? f : (1f - f) * NeighbourFade(tile, edge));
        }
    }

    static void Show(MeshInstance3D mesh, float shown)
    {
        mesh.Visible = shown > 0.001f;
        mesh.Transparency = shown >= 0.999f ? 0f : 1f - shown;
    }
    /// <summary>How faded the neighbour across a tile's model-space edge is (any neighbour when the edge is unknown).</summary>
    float NeighbourFade(int tile, int edge)
    {
        int x = tile % Info.Width, y = tile / Info.Width;
        float Across(int worldEdge)
        {
            var (dx, dy) = TileFade.Offset(worldEdge);
            int nx = x + dx, ny = y + dy;
            return nx >= 0 && ny >= 0 && nx < Info.Width && ny < Info.Height ? _fadeAmount[ny * Info.Width + nx] : 0f;
        }
        if (edge < 0) return Math.Max(Math.Max(Across(0), Across(1)), Math.Max(Across(2), Across(3)));
        return Across(TileFade.RotateEdge(edge, Info.Tiles[tile].Orientation));
    }
}
