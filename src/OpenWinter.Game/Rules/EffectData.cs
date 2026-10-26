using System.Runtime.CompilerServices;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting;

/// <summary>Effect state beyond the shared data contract (links, versus filters, application bookkeeping).</summary>
public sealed partial class Effect
{
    /// <summary>Shared by every effect applied together from one link (GetEffectLinkId). Empty until linked or applied.</summary>
    public string LinkId = "";
    /// <summary>VersusRacialTypeEffect: RACIAL_TYPE_* the effect applies against, or -1 for all.</summary>
    public int VersusRace = -1;
    /// <summary>VersusAlignmentEffect: ALIGNMENT_LAWFUL/CHAOTIC/NEUTRAL (0 = all).</summary>
    public int VersusLawChaos;
    /// <summary>VersusAlignmentEffect: ALIGNMENT_GOOD/EVIL/NEUTRAL (0 = all).</summary>
    public int VersusGoodEvil;
    /// <summary>VersusTrapEffect: the effect only applies against traps.</summary>
    public bool VersusTraps;
    /// <summary>The object the effect is applied to (InvalidId while it is only a value in a script).</summary>
    public uint Target = GameObject.InvalidId;
    /// <summary>Game time it was applied.</summary>
    public double AppliedAt;
    /// <summary>Next game time an interval effect (regenerate, run-script, dying) ticks.</summary>
    public double NextTick;
    /// <summary>Area-of-effect object created by an EffectAreaOfEffect applied to an object.</summary>
    public uint AreaObject = GameObject.InvalidId;
    /// <summary>The particular creature owned by this summon effect, including after detachment.</summary>
    public uint SummonedObject = GameObject.InvalidId;
    /// <summary>
    /// An EffectHitPointChangeWhenDying applied as DURATION_TYPE_INSTANT, which the original turns into a temporary
    /// effect ticking every |6 / change| seconds (visual scene bleedinst).
    /// </summary>
    public bool FromInstant;
    /// <summary>
    /// Identity given when the script made the effect (copies and modified copies keep it; a link gives all its
    /// parts its own). RemoveEffect removes every applied effect with the same identity.
    /// </summary>
    public long ScriptId;
    /// <summary>
    /// A lasting effect applied as DURATION_TYPE_INSTANT: like the original it changes the creature for good but
    /// is not in the effect list, so scripts can't see or remove it and rest and death don't clear it.
    /// </summary>
    public bool Hidden;
    /// <summary>Curse ability penalties suppressed by immunity when they land (one bit per ability).</summary>
    public int CurseBlockedAbilities;
    /// <summary>An innate effect a creature has from its feats or race (NWNX_Feat, NWNX_Race); always <see cref="Hidden"/>.</summary>
    public bool Innate;

    public bool IsValid => Type > 0;

    /// <summary>The effects a link applies (the effect itself when it is not a link).</summary>
    public IReadOnlyList<Effect> Leaves => Linked.Count > 0 ? Linked : new[] { this };

    /// <summary>A deep copy. Links are copied too unless <paramref name="withLinks"/> is false.</summary>
    public Effect Copy(bool withLinks = true)
    {
        var e = new Effect
        {
            Type = Type, SubType = SubType, DurationType = DurationType, Duration = Duration, Creator = Creator,
            SpellId = SpellId, CasterLevel = CasterLevel, Tag = Tag,
            Ints = (int[])Ints.Clone(), Floats = (float[])Floats.Clone(), Strings = (string[])Strings.Clone(),
            Objects = (uint[])Objects.Clone(), Vectors = (System.Numerics.Vector3[])Vectors.Clone(),
            IgnoreImmunity = IgnoreImmunity, ExpiresAt = ExpiresAt, UniqueId = UniqueId,
            LinkId = LinkId, VersusRace = VersusRace, VersusLawChaos = VersusLawChaos, VersusGoodEvil = VersusGoodEvil,
            VersusTraps = VersusTraps, Target = Target, AppliedAt = AppliedAt, NextTick = NextTick,
            AreaObject = AreaObject, SummonedObject = SummonedObject, FromInstant = FromInstant, ScriptId = ScriptId, Hidden = Hidden,
            CurseBlockedAbilities = CurseBlockedAbilities, Innate = Innate,
        };
        if (withLinks) foreach (var leaf in Linked) e.Linked.Add(leaf.Copy(false));
        return e;
    }

    /// <summary>Returns a copy with <paramref name="change"/> applied to it and every linked effect (MagicalEffect, TagEffect, ...).</summary>
    public Effect With(Action<Effect> change)
    {
        var e = Copy();
        change(e);
        foreach (var leaf in e.Linked) change(leaf);
        return e;
    }

    public override string ToString() => $"Effect type={Type} dur={DurationType} id={UniqueId}{(LinkId.Length > 0 ? " link=" + LinkId : "")}";
}

/// <summary>NWScript constants the effect rules need, read from nwscript.nss once per definitions set.</summary>
public sealed class EffectConstants
{
    static readonly ConditionalWeakTable<NssDefinitions, EffectConstants> _byDefs = new();
    static readonly ConditionalWeakTable<World, NssDefinitions> _fallback = new();

    readonly NssDefinitions _defs;
    readonly Dictionary<string, int> _cache = new(StringComparer.Ordinal);

    public static EffectConstants Of(NssDefinitions defs) => _byDefs.GetValue(defs, d => new EffectConstants(d));

    /// <summary>Constants for a world; loads nwscript.nss itself when scripting is off (tests, tools).</summary>
    public static EffectConstants Of(World world) =>
        Of(world.Scripts?.Definitions ?? _fallback.GetValue(world, w => ScriptHost.LoadDefinitions(w.Resources)));

    EffectConstants(NssDefinitions defs)
    {
        _defs = defs;
        int E(string n) => defs.Int("EFFECT_TYPE_" + n);
        Heal = E("HEAL"); Damage = E("DAMAGE"); Death = E("DEATH"); Resurrection = E("RESURRECTION");
        VisualEffect = E("VISUALEFFECT"); Beam = E("BEAM"); AreaOfEffect = E("AREA_OF_EFFECT");
        RunScript = E("RUNSCRIPT"); Regenerate = E("REGENERATE"); TemporaryHitpoints = E("TEMPORARY_HITPOINTS");
        Immunity = E("IMMUNITY"); DamageImmunityIncrease = E("DAMAGE_IMMUNITY_INCREASE");
        DamageImmunityDecrease = E("DAMAGE_IMMUNITY_DECREASE"); DamageResistance = E("DAMAGE_RESISTANCE");
        DamageReduction = E("DAMAGE_REDUCTION"); DispelAll = E("DISPELMAGICALL"); DispelBest = E("DISPELMAGICBEST");
        SummonCreature = E("SUMMON_CREATURE"); HitPointChangeWhenDying = E("HITPOINTCHANGEWHENDYING");
        Disappear = E("DISAPPEAR"); Appear = E("APPEAR"); DisappearAppear = E("DISAPPEARAPPEAR");
        Icon = E("ICON"); Knockdown = E("KNOCKDOWN");
        FirstHidden = E("APPEAR");
        Instant = defs.Int("DURATION_TYPE_INSTANT"); Temporary = defs.Int("DURATION_TYPE_TEMPORARY"); Permanent = defs.Int("DURATION_TYPE_PERMANENT");
        Magical = defs.Int("SUBTYPE_MAGICAL"); Supernatural = defs.Int("SUBTYPE_SUPERNATURAL");
        Extraordinary = defs.Int("SUBTYPE_EXTRAORDINARY"); Unyielding = defs.Int("SUBTYPE_UNYIELDING");
        DamageMagical = defs.Int("DAMAGE_TYPE_MAGICAL");
        RacialInvalid = defs.Int("RACIAL_TYPE_INVALID");
    }

    /// <summary>Any int constant by name (cached).</summary>
    public int this[string name]
    {
        get
        {
            if (!_cache.TryGetValue(name, out int v)) _cache[name] = v = _defs.Int(name);
            return v;
        }
    }

    public readonly int Heal, Damage, Death, Resurrection, VisualEffect, Beam, AreaOfEffect, RunScript, Regenerate,
        TemporaryHitpoints, Immunity, DamageImmunityIncrease, DamageImmunityDecrease, DamageResistance, DamageReduction,
        DispelAll, DispelBest, SummonCreature, HitPointChangeWhenDying, Disappear, Appear, DisappearAppear, Icon, Knockdown;
    /// <summary>EFFECT_TYPE_APPEAR: this and higher values are reported by GetEffectType only with bAllTypes.</summary>
    public readonly int FirstHidden;
    public readonly int Instant, Temporary, Permanent;
    public readonly int Magical, Supernatural, Extraordinary, Unyielding;
    public readonly int DamageMagical, RacialInvalid;
}
