using System.Numerics;

namespace OpenWinter.Content;

/// <summary>
/// A particle emitter node. Field names follow the emitter properties of the content this engine imports; see
/// docs/EMITTERS.md for what each does, how often the base game uses it and what the renderer does with it.
/// </summary>
public sealed class EmitterData
{
    public string Texture = "";
    /// <summary>Fountain (a continuous stream), Single (one particle that stays), Explosion (bursts), Lightning (a bolt to the reference node).</summary>
    public string Update = "Fountain";
    /// <summary>Normal, Linked, Billboard_to_Local_Z, Billboard_to_World_Z, Aligned_to_World_Z, Aligned_to_Particle_Dir, Motion_Blur.</summary>
    public string Render = "Normal";
    /// <summary>Normal (alpha), Lighten (additive), Punch-Through (cut-out).</summary>
    public string Blend = "Normal";
    /// <summary>A model whose mesh is drawn as each particle instead of a textured quad (debris).</summary>
    public string Chunk = "";
    /// <summary>0 normal, 1 trail (particles laid along the emitter's path as it moves).</summary>
    public int SpawnType;
    public int RenderOrder;
    public bool TwoSided, Loop;
    // Header flags.
    public bool P2P, P2PBezier = true, AffectedByWind, Tinted, Bounce, Random, Inherit, InheritVel, InheritLocal, Splat, InheritPart;
    public float DeadSpace, BlastRadius, BlastLength;

    public float BirthRate = 10f;
    public float LifeExpectancy = 1f;
    public float Velocity = 1f;
    public float RandomVelocity;
    public float Spread;
    public float Mass;
    public float SizeStart = 0.5f;
    public float SizeEnd = 0.5f;
    /// <summary>Particle height when not square (both 0: square).</summary>
    public float SizeStartY, SizeEndY;
    public Vector3 ColorStart = Vector3.One;
    public Vector3 ColorEnd = Vector3.One;
    public float AlphaStart = 1f;
    public float AlphaEnd = 1f;
    /// <summary>Optional middle point of the colour, alpha and size ramps, at PercentMid of the life.</summary>
    public bool HasMid;
    public Vector3 ColorMid = Vector3.One;
    public float AlphaMid = 1f, SizeMid, SizeMidY, PercentStart, PercentMid = 0.5f, PercentEnd = 1f;
    /// <summary>Emitter area in centimetres (full width and depth).</summary>
    public float XSize = 0f;
    public float YSize = 0f;
    public int GridX = 1;
    public int GridY = 1;
    public float FrameStart;
    public float FrameEnd;
    public float Fps;
    public float ParticleRot, Drag, Grav, BounceCo, CombineTime, Threshold, BlurLength = 10f, Detonate;
    public float LightningDelay, LightningRadius, LightningScale, LightningSubDiv, LightningZigZag;
    public float P2PBezier2, P2PBezier3;

    /// <summary>A copy with another birthrate (an animation's keyed value).</summary>
    public EmitterData WithBirthRate(float rate)
    {
        var copy = Clone();
        copy.BirthRate = rate;
        return copy;
    }

    public EmitterData Clone() => (EmitterData)MemberwiseClone();

    public bool Is(string update) => Update.Equals(update, StringComparison.OrdinalIgnoreCase);

    /// <summary>A controller's value (colours: <paramref name="channel"/> 0-2).</summary>
    public float Get(EmitterParam p, int channel = 0) => p switch
    {
        EmitterParam.BirthRate => BirthRate, EmitterParam.LifeExp => LifeExpectancy, EmitterParam.Velocity => Velocity,
        EmitterParam.RandVel => RandomVelocity, EmitterParam.Spread => Spread, EmitterParam.Mass => Mass,
        EmitterParam.SizeStart => SizeStart, EmitterParam.SizeEnd => SizeEnd, EmitterParam.SizeStartY => SizeStartY,
        EmitterParam.SizeEndY => SizeEndY, EmitterParam.SizeMid => SizeMid, EmitterParam.SizeMidY => SizeMidY,
        EmitterParam.AlphaStart => AlphaStart, EmitterParam.AlphaEnd => AlphaEnd, EmitterParam.AlphaMid => AlphaMid,
        EmitterParam.ColorStart => Channel(ColorStart, channel), EmitterParam.ColorEnd => Channel(ColorEnd, channel),
        EmitterParam.ColorMid => Channel(ColorMid, channel),
        EmitterParam.PercentStart => PercentStart, EmitterParam.PercentMid => PercentMid, EmitterParam.PercentEnd => PercentEnd,
        EmitterParam.Fps => Fps, EmitterParam.FrameStart => FrameStart, EmitterParam.FrameEnd => FrameEnd,
        EmitterParam.XSize => XSize, EmitterParam.YSize => YSize, EmitterParam.ParticleRot => ParticleRot,
        EmitterParam.Drag => Drag, EmitterParam.Grav => Grav, EmitterParam.BounceCo => BounceCo,
        EmitterParam.CombineTime => CombineTime, EmitterParam.Threshold => Threshold, EmitterParam.BlurLength => BlurLength,
        EmitterParam.LightningDelay => LightningDelay, EmitterParam.LightningRadius => LightningRadius,
        EmitterParam.LightningScale => LightningScale, EmitterParam.LightningSubDiv => LightningSubDiv,
        EmitterParam.LightningZigZag => LightningZigZag,
        EmitterParam.P2PBezier2 => P2PBezier2, EmitterParam.P2PBezier3 => P2PBezier3, EmitterParam.Detonate => Detonate,
        _ => 0f,
    };

    public void Set(EmitterParam p, float v, int channel = 0)
    {
        switch (p)
        {
            case EmitterParam.BirthRate: BirthRate = v; break;
            case EmitterParam.LifeExp: LifeExpectancy = v; break;
            case EmitterParam.Velocity: Velocity = v; break;
            case EmitterParam.RandVel: RandomVelocity = v; break;
            case EmitterParam.Spread: Spread = v; break;
            case EmitterParam.Mass: Mass = v; break;
            case EmitterParam.SizeStart: SizeStart = v; break;
            case EmitterParam.SizeEnd: SizeEnd = v; break;
            case EmitterParam.SizeStartY: SizeStartY = v; break;
            case EmitterParam.SizeEndY: SizeEndY = v; break;
            case EmitterParam.SizeMid: SizeMid = v; HasMid = true; break;
            case EmitterParam.SizeMidY: SizeMidY = v; HasMid = true; break;
            case EmitterParam.AlphaStart: AlphaStart = v; break;
            case EmitterParam.AlphaEnd: AlphaEnd = v; break;
            case EmitterParam.AlphaMid: AlphaMid = v; HasMid = true; break;
            case EmitterParam.ColorStart: ColorStart = WithChannel(ColorStart, channel, v); break;
            case EmitterParam.ColorEnd: ColorEnd = WithChannel(ColorEnd, channel, v); break;
            case EmitterParam.ColorMid: ColorMid = WithChannel(ColorMid, channel, v); HasMid = true; break;
            case EmitterParam.PercentStart: PercentStart = v; break;
            case EmitterParam.PercentMid: PercentMid = v; HasMid = true; break;
            case EmitterParam.PercentEnd: PercentEnd = v; break;
            case EmitterParam.Fps: Fps = v; break;
            case EmitterParam.FrameStart: FrameStart = v; break;
            case EmitterParam.FrameEnd: FrameEnd = v; break;
            case EmitterParam.XSize: XSize = v; break;
            case EmitterParam.YSize: YSize = v; break;
            case EmitterParam.ParticleRot: ParticleRot = v; break;
            case EmitterParam.Drag: Drag = v; break;
            case EmitterParam.Grav: Grav = v; break;
            case EmitterParam.BounceCo: BounceCo = v; break;
            case EmitterParam.CombineTime: CombineTime = v; break;
            case EmitterParam.Threshold: Threshold = v; break;
            case EmitterParam.BlurLength: BlurLength = v; break;
            case EmitterParam.LightningDelay: LightningDelay = v; break;
            case EmitterParam.LightningRadius: LightningRadius = v; break;
            case EmitterParam.LightningScale: LightningScale = v; break;
            case EmitterParam.LightningSubDiv: LightningSubDiv = v; break;
            case EmitterParam.LightningZigZag: LightningZigZag = v; break;
            case EmitterParam.P2PBezier2: P2PBezier2 = v; break;
            case EmitterParam.P2PBezier3: P2PBezier3 = v; break;
            case EmitterParam.Detonate: Detonate = v; break;
        }
    }

    public static bool IsColor(EmitterParam p) => p is EmitterParam.ColorStart or EmitterParam.ColorEnd or EmitterParam.ColorMid;

    static float Channel(Vector3 c, int i) => i switch { 0 => c.X, 1 => c.Y, _ => c.Z };
    static Vector3 WithChannel(Vector3 c, int i, float v) => i switch { 0 => c with { X = v }, 1 => c with { Y = v }, _ => c with { Z = v } };
}

/// <summary>Emitter controllers that geometry sets and animations may key.</summary>
public enum EmitterParam
{
    BirthRate, LifeExp, Velocity, RandVel, Spread, Mass,
    SizeStart, SizeEnd, SizeStartY, SizeEndY, SizeMid, SizeMidY,
    AlphaStart, AlphaEnd, AlphaMid, ColorStart, ColorEnd, ColorMid, PercentStart, PercentMid, PercentEnd,
    Fps, FrameStart, FrameEnd, XSize, YSize, ParticleRot, Drag, Grav, BounceCo, CombineTime, Threshold, BlurLength,
    LightningDelay, LightningRadius, LightningScale, LightningSubDiv, LightningZigZag, P2PBezier2, P2PBezier3, Detonate,
}

/// <summary>Keys of one animated value: times and rows of <see cref="Width"/> values.</summary>
public sealed class KeyTrack
{
    public float[] Times = Array.Empty<float>();
    public float[] Values = Array.Empty<float>();
    public int Width = 1;

    /// <summary>Linear interpolation at <paramref name="t"/>, held at the first and last keys.</summary>
    public float Sample(float t, int column = 0)
    {
        if (Times.Length == 0) return 0f;
        if (t <= Times[0]) return Values[column];
        for (int k = 1; k < Times.Length; k++)
        {
            if (t > Times[k]) continue;
            float span = Times[k] - Times[k - 1];
            float f = span > 1e-6f ? (t - Times[k - 1]) / span : 1f;
            return Values[(k - 1) * Width + column] + (Values[k * Width + column] - Values[(k - 1) * Width + column]) * f;
        }
        return Values[(Times.Length - 1) * Width + column];
    }

    /// <summary>The largest value of a column.</summary>
    public float Max(int column = 0)
    {
        float m = float.MinValue;
        for (int k = 0; k < Times.Length; k++) m = Math.Max(m, Values[k * Width + column]);
        return Times.Length == 0 ? 0 : m;
    }
}
