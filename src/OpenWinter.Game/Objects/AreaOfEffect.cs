using System.Numerics;

namespace OpenWinter.Game.Objects;

/// <summary>
/// A persistent area of effect (EffectAreaOfEffect): a circle or rectangle from
/// vfx_persistent.2da that runs OnObjEnter / OnObjExit / OnHeartbeat scripts for
/// the creatures inside it. Created and ticked by <see cref="Rules.EffectSystem"/>.
/// Event script fields: "OnObjEnter", "OnObjExit", "OnHeartbeat", "OnUserDefined".
/// </summary>
public sealed partial class AreaOfEffect : GameObject
{
    public override ObjectType Type => ObjectType.AreaOfEffect;

    /// <summary>vfx_persistent.2da row (AOE_PER_* / VFX_PER_* constant).</summary>
    public int AoeId;
    /// <summary>'C' circle or 'R' rectangle.</summary>
    public char Shape = 'C';
    public float Radius, Width, Length;
    /// <summary>The object that applied the effect (GetAreaOfEffectCreator).</summary>
    public uint Creator = InvalidId;
    /// <summary>Object the AoE follows when applied with ApplyEffectToObject (InvalidId when placed at a location).</summary>
    public uint AttachedTo = InvalidId;
    /// <summary>The effect that created it (UniqueId of the applied effect on <see cref="AttachedTo"/>, 0 for location effects).</summary>
    public long EffectId;
    public int SpellId = -1;
    public int CasterLevel;
    /// <summary>Game time it disappears (temporary effects); 0 = permanent.</summary>
    public double ExpiresAt;
    /// <summary>Objects currently inside, in entry order (GetFirst/NextInPersistentObject).</summary>
    public readonly List<uint> Inside = new();
    /// <summary>Object types the AoE tracks (OBJECT_TYPE_* bits). Creatures by default.</summary>
    public ObjectType Tracks = ObjectType.Creature;

    /// <summary>Where each object in the area stood at the last membership update (only movers are tested again).</summary>
    internal readonly Dictionary<uint, Vector3> LastSeen = new();
    /// <summary>Where the area of effect stood at the last membership update.</summary>
    internal Vector3? LastPosition;

    /// <summary>
    /// Whether a point is inside the shape: a circle is a sphere (3-D distance, measured by parity suite
    /// aoe_subarea: a fog laid 4 m up took in creatures 4.47 m away and not 5.05 m away, and walkers entered 5 m out
    /// in a straight line); a rectangle is tested flat.
    /// </summary>
    public bool Contains(Vector3 p)
    {
        var d = new Vector2(p.X - Position.X, p.Y - Position.Y);
        if (Shape != 'R') return Vector3.DistanceSquared(p, Position) <= Radius * Radius;
        // Rectangle: Length runs along the facing, Width across it.
        float c = MathF.Cos(Facing), s = MathF.Sin(Facing);
        float along = d.X * c + d.Y * s, across = -d.X * s + d.Y * c;
        return MathF.Abs(along) <= Length / 2 && MathF.Abs(across) <= Width / 2;
    }
}
