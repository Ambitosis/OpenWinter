using System.Numerics;

namespace OpenWinter.Content;

/// <summary>
/// Engine-side model description, independent of any file format. Format
/// readers fill it in; the client turns it into renderable objects and the
/// server uses it for walkmeshes and attachment points.
/// Coordinates are Z-up (the convention of the content this engine imports).
/// </summary>
public sealed class ModelData
{
    public string Name = "";
    public string Supermodel = "";
    public float AnimationScale = 1f;
    public ModelClassification Classification;
    /// <summary>Parents always precede children.</summary>
    public readonly List<ModelPart> Parts = new();
    /// <summary>Keyed by lowercase name.</summary>
    public readonly Dictionary<string, ModelAnimation> Animations = new(StringComparer.OrdinalIgnoreCase);

    public int FindPart(string name)
    {
        for (int i = 0; i < Parts.Count; i++)
            if (string.Equals(Parts[i].Name, name, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    /// <summary>Model-space rest transform of each part.</summary>
    public Matrix4x4[] RestGlobals()
    {
        var result = new Matrix4x4[Parts.Count];
        for (int i = 0; i < Parts.Count; i++)
        {
            var p = Parts[i];
            var local = Matrix4x4.CreateFromQuaternion(p.Rotation) * Matrix4x4.CreateTranslation(p.Position);
            result[i] = p.Parent < 0 ? local : local * result[p.Parent];
        }
        return result;
    }
}

public enum ModelClassification { Other, Effect, Tile, Character, Door, Item, Gui }

public sealed class ModelPart
{
    public string Name = "";
    public int Parent = -1;
    public Vector3 Position;
    public Quaternion Rotation = Quaternion.Identity;
    public MeshData? Mesh;
    public LightData? Light;
    public EmitterData? Emitter;
    /// <summary>A named submodel referenced by an emitter endpoint.</summary>
    public ModelReference? Reference;
    /// <summary>
    /// The mesh's "tilefade" value: 0 never fades; non-zero marks tile geometry
    /// (upper walls, roofs, wall caps) the client hides when it would block the view of the player.
    /// </summary>
    public int TileFadeMode;
    public bool TileFade => TileFadeMode != 0;
}

public sealed class ModelReference
{
    public string Model = "";
    public bool Reattachable;
}

public sealed class MeshData
{
    public Vector3[] Vertices = Array.Empty<Vector3>();
    public Vector3[] Normals = Array.Empty<Vector3>();
    public Vector2[] Uvs = Array.Empty<Vector2>();
    /// <summary>Triangle list, clockwise front faces when viewed in a Y-up right-handed renderer after the Z-up→Y-up rotation.</summary>
    public int[] Indices = Array.Empty<int>();
    /// <summary>Surface (material) id per triangle; meaningful for walkmeshes.</summary>
    public int[] FaceSurfaces = Array.Empty<int>();
    public string Texture = "";
    public bool Render = true;
    public int TransparencyHint;
    public Vector3 Diffuse = Vector3.One;
    public Vector3 Ambient = Vector3.One;
    /// <summary>Specular colour; Aurora world geometry is almost always black (no highlights).</summary>
    public Vector3 Specular;
    /// <summary>Authored mesh self-illumination colour, independent of area lighting.</summary>
    public Vector3 SelfIllumination;
    public float Alpha = 1f;
    public bool Beaming;
    public bool Shadow = true;
    public bool IsWalkmesh;
    public bool IsDangly;
    /// <summary>Four influences per vertex: part indices (or -1) and weights. Empty for rigid meshes.</summary>
    public int[] BoneParts = Array.Empty<int>();
    public float[] BoneWeights = Array.Empty<float>();

    public bool IsSkinned => BoneParts.Length == Vertices.Length * 4 && BoneParts.Length > 0;

    /// <summary>A shallow copy (vertex arrays shared) for per-instance changes like texture or bone remapping.</summary>
    public MeshData Clone() => (MeshData)MemberwiseClone();
}

public sealed class LightData
{
    public Vector3 Color = Vector3.One;
    public float Radius = 5f;
    public float Multiplier = 1f;
    public bool IsDynamic;
    public bool AffectsDynamic;
    public bool Shadow;
    public int Priority;
}

public sealed class ModelAnimation
{
    public string Name = "";
    public float Length;
    public float Transition = 0.25f;
    public string Root = "";
    public readonly List<(float Time, string Name)> Events = new();
    /// <summary>Keyed by lowercase part name.</summary>
    public readonly Dictionary<string, AnimationTrack> Tracks = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AnimationTrack
{
    public float[] PositionTimes = Array.Empty<float>();
    public Vector3[] Positions = Array.Empty<Vector3>();
    public float[] RotationTimes = Array.Empty<float>();
    public Quaternion[] Rotations = Array.Empty<Quaternion>();
    /// <summary>Uniform node scale, relative to its parent.</summary>
    public float[] ScaleTimes = Array.Empty<float>();
    public float[] Scales = Array.Empty<float>();
    /// <summary>Emitter birthrate keys (spell effects switch their emitters on and off through the animation).</summary>
    public float[] BirthRateTimes = Array.Empty<float>();
    public float[] BirthRates = Array.Empty<float>();
    /// <summary>Light colour keys (the source-light flame model's animations set its light's colour).</summary>
    public float[] LightColorTimes = Array.Empty<float>();
    public Vector3[] LightColors = Array.Empty<Vector3>();
    /// <summary>Light multiplier keys (a flash that fades).</summary>
    public float[] LightTimes = Array.Empty<float>();
    public float[] LightMultipliers = Array.Empty<float>();
    /// <summary>Light radius keys (fx_light_clr's animations give the light its reach, 5 to 20 m, as well as its colour).</summary>
    public float[] LightRadiusTimes = Array.Empty<float>();
    public float[] LightRadii = Array.Empty<float>();
    /// <summary>Every emitter controller the animation keys (birthrate included).</summary>
    public readonly Dictionary<EmitterParam, KeyTrack> EmitterKeys = new();
    /// <summary>Mesh opacity keys (effect meshes fade in and out).</summary>
    public float[] AlphaTimes = Array.Empty<float>();
    public float[] Alphas = Array.Empty<float>();
}
