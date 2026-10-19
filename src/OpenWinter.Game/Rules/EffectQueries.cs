using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Rules;

/// <summary>
/// Read-only questions rules code asks about the effects on an object.
///
/// Effects follow the shared data contract: <see cref="Effect.Type"/> is the
/// EFFECT_TYPE_* constant and the constructor's arguments are stored in order
/// (ints in <c>Ints[0..]</c>, floats in <c>Floats</c>, strings, objects, vectors).
/// For example EffectACIncrease(nValue, nModifyType, nDamageType) stores
/// Ints = [value, modifyType, damageType]; EffectAbilityIncrease(nAbility, nModifyBy)
/// stores Ints = [ability, amount]. Versus filters (VersusAlignmentEffect,
/// VersusRacialTypeEffect, VersusTrapEffect) live in Effect.Versus* fields; use
/// <see cref="AppliesVersus"/> to honour them.
///
/// Usage:
/// <code>
///   var k = EffectConstants.Of(world);
///   int acBonus = EffectQueries.Sum(creature, k["EFFECT_TYPE_AC_INCREASE"], e => e.Ints[1] == acDodge, valueIndex: 0);
///   bool hasted = EffectQueries.Has(creature, k["EFFECT_TYPE_HASTE"]);
///   foreach (var e in EffectQueries.Active(creature, k["EFFECT_TYPE_CONCEALMENT"], versus: attacker)) ...
///   bool immune = EffectImmunity.IsImmune(world, creature, k["IMMUNITY_TYPE_POISON"], versus: attacker);
/// </code>
/// "Active" means currently applied (temporary or permanent) and not expired.
/// </summary>
public static class EffectQueries
{
    /// <summary>Effects of a type on an object. With <paramref name="versus"/>, only those that apply against it; without, only unrestricted ones.</summary>
    public static IEnumerable<Effect> Active(GameObject o, int effectType, GameObject? versus = null, bool ignoreVersus = false)
    {
        foreach (var e in o.Effects)
            if (e.Type == effectType && (ignoreVersus || AppliesVersus(e, versus))) yield return e;
    }

    /// <summary>Effects matching a predicate.</summary>
    public static IEnumerable<Effect> Active(GameObject o, Func<Effect, bool> filter) => o.Effects.Where(filter);

    public static bool Has(GameObject o, int effectType) => o.Effects.Any(e => e.Type == effectType);

    /// <summary>Sum of <c>Ints[valueIndex]</c> over matching effects (e.g. total AC or attack bonus).</summary>
    public static int Sum(GameObject o, int effectType, Func<Effect, bool>? filter = null, int valueIndex = 0, GameObject? versus = null)
    {
        int total = 0;
        foreach (var e in Active(o, effectType, versus))
            if (filter == null || filter(e)) total += e.Ints[valueIndex];
        return total;
    }

    /// <summary>Largest <c>Ints[valueIndex]</c> over matching effects (0 if none) — for bonuses of one kind that don't stack.</summary>
    public static int Max(GameObject o, int effectType, Func<Effect, bool>? filter = null, int valueIndex = 0, GameObject? versus = null)
    {
        int best = 0;
        foreach (var e in Active(o, effectType, versus))
            if ((filter == null || filter(e)) && e.Ints[valueIndex] > best) best = e.Ints[valueIndex];
        return best;
    }

    /// <summary>Whether any effect on the object came from a spell (GetHasSpellEffect).</summary>
    public static bool HasSpell(GameObject o, int spellId) => o.Effects.Any(e => !e.Hidden && e.SpellId == spellId);

    /// <summary>
    /// Whether an effect with versus filters applies against <paramref name="versus"/>.
    /// Unfiltered effects always apply; filtered ones never apply against nobody.
    /// </summary>
    public static bool AppliesVersus(Effect e, GameObject? versus, bool versusTrap = false)
    {
        if (e.VersusTraps) return versusTrap;
        bool race = e.VersusRace >= 0, align = e.VersusLawChaos != 0 || e.VersusGoodEvil != 0;
        if (!race && !align) return true;
        if (versus is not Creature c) return false;
        if (race && c.Race != e.VersusRace) return false;
        return Axis(e.VersusLawChaos, c.LawfulChaotic) && Axis(e.VersusGoodEvil, c.GoodEvil);
    }

    /// <summary>Alignment axis test: 0 all, 1 neutral, 2 high (lawful/good), 3 low (chaotic/evil).</summary>
    static bool Axis(int want, int value) => want switch
    {
        1 => value > 30 && value < 70,
        2 => value >= 70,
        3 => value <= 30,
        _ => true,
    };
}

/// <summary>
/// Immunities from effects (EffectImmunity, IMMUNITY_TYPE_*) and equipped item properties
/// (Immunity: Miscellaneous, Freedom of Movement, Mind Blank), combined with
/// the creature rules' racial and class feat immunities.
/// </summary>
public static class EffectImmunity
{
    public static bool IsImmune(World world, GameObject target, int immunityType, GameObject? versus = null)
    {
        // Creature queries include racial/class feat immunities as well as effects
        // and equipment. Applying an effect must honor those same immunities.
        if (target is Creature creature && CreatureRules.IsImmune(world, creature, immunityType, versus)) return true;
        var k = EffectConstants.Of(world);
        foreach (var e in target.Effects)
            if (e.Type == k.Immunity && e.Ints[0] == immunityType && EffectQueries.AppliesVersus(e, versus)) return true;
        if (target is Creature c)
            foreach (var item in c.Equipment.Values)
                foreach (var ip in item.Properties)
                    if (ItemPropertyRules.GrantsImmunity(k, ip, immunityType)) return true;
        return false;
    }

    /// <summary>Whether a target resists an effect being applied because of an immunity (honours IgnoreEffectImmunity).</summary>
    public static bool Blocks(World world, GameObject target, Effect e) => BlockedBy(world, target, e) != NotBlocked;

    public const int NotBlocked = -1, BlockedByPlot = -2;

    /// <summary>What keeps an effect off: <see cref="NotBlocked"/>, <see cref="BlockedByPlot"/> (plot or immortal) or the IMMUNITY_TYPE_*.</summary>
    public static int BlockedBy(World world, GameObject target, Effect e)
    {
        var k = EffectConstants.Of(world);
        // Measured on nwserver (parity suite condition_actions, block.*): plot and immortal creatures refuse the
        // disabling mind conditions even with IgnoreEffectImmunity; plot creatures also refuse every other harmful
        // condition and penalty, unless the effect ignores immunity.
        if (target is Creature { Plot: true } or Creature { IsImmortal: true } && IsSubdual(k, e.Type)) return BlockedByPlot;
        if (e.IgnoreImmunity) return NotBlocked;
        if (target is Creature { Plot: true } && IsHarmful(k, e.Type)) return BlockedByPlot;
        var types = ImmunitiesFor(k, e.Type);
        if (types.Length == 0) return NotBlocked;
        var versus = world.Get(e.Creator);
        foreach (int t in types)
            if (IsImmune(world, target, t, versus)) return t;
        return NotBlocked;
    }

    /// <summary>
    /// The feedback a player gets when an immunity keeps an effect off ("Aluvian Darkstar : Immune to Sleep.",
    /// parity/visual scene conditions): dialog.tlk 62453-62474, or null for an immunity without one.
    /// </summary>
    public static string? ImmuneMessage(World world, EffectConstants k, int immunity, string name)
    {
        string? key = null;
        foreach (var (n, strref) in ImmuneLines)
            if (immunity == k["IMMUNITY_TYPE_" + n]) { key = world.Resources.TlkString(strref); break; }
        return string.IsNullOrEmpty(key) ? null : key.Replace("<CUSTOM0>", name);
    }

    static readonly (string Immunity, uint StrRef)[] ImmuneLines =
    {
        ("DISEASE", 62453), ("CRITICAL_HIT", 62454), ("DEATH", 62455), ("FEAR", 62456), ("KNOCKDOWN", 62457), ("PARALYSIS", 62458),
        ("NEGATIVE_LEVEL", 62459), ("MIND_SPELLS", 62460), ("POISON", 62461), ("SNEAK_ATTACK", 62462), ("SLEEP", 62463), ("DAZED", 62464),
        ("CONFUSED", 62465), ("STUN", 62466), ("BLINDNESS", 62467), ("DEAFNESS", 62468), ("CURSED", 62469), ("CHARM", 62470),
        ("DOMINATE", 62471), ("ENTANGLE", 62472), ("SILENCE", 62473), ("SLOW", 62474),
    };

    /// <summary>Conditions a plot or immortal creature never takes, IgnoreEffectImmunity or not.</summary>
    static readonly string[] Subdual = { "SLEEP", "STUNNED", "PARALYZE", "DAZED", "CONFUSED", "FRIGHTENED", "CHARMED", "DOMINATED", "PETRIFY", "TURNED" };

    /// <summary>Other harmful effects a plot creature refuses (cutscene immobilisation and every bonus still land).</summary>
    static readonly string[] Harmful =
    {
        "KNOCKDOWN", "ENTANGLE", "CUTSCENE_PARALYZE", "CUTSCENE_DOMINATED", "SLOW", "BLINDNESS", "DEAF", "SILENCE", "CURSE",
        "NEGATIVELEVEL", "POISON", "DISEASE", "ABILITY_DECREASE", "AC_DECREASE", "ATTACK_DECREASE", "SKILL_DECREASE",
        "SAVING_THROW_DECREASE", "MOVEMENT_SPEED_DECREASE", "DAMAGE_IMMUNITY_DECREASE", "SPELL_RESISTANCE_DECREASE", "DAMAGE_DECREASE",
    };

    public static bool IsSubdual(EffectConstants k, int type) => In(k, type, Subdual);
    public static bool IsHarmful(EffectConstants k, int type) => In(k, type, Harmful);

    static bool In(EffectConstants k, int type, string[] names)
    {
        foreach (var n in names) if (type == k["EFFECT_TYPE_" + n]) return true;
        return false;
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<EffectConstants, Dictionary<int, int[]>> _maps = new();

    /// <summary>IMMUNITY_TYPE_* values that stop an effect type from landing.</summary>
    public static int[] ImmunitiesFor(EffectConstants k, int effectType)
    {
        var map = _maps.GetValue(k, Build);
        return map.TryGetValue(effectType, out var list) ? list : Array.Empty<int>();
    }

    static Dictionary<int, int[]> Build(EffectConstants k)
    {
        int I(string n) => k["IMMUNITY_TYPE_" + n];
        int mind = I("MIND_SPELLS");
        var pairs = new (string Effect, int[] Immunities)[]
        {
            ("POISON", new[] { I("POISON") }),
            ("DISEASE", new[] { I("DISEASE") }),
            ("FRIGHTENED", new[] { I("FEAR"), mind }),
            // Mind immunity keeps paralysis and stunning off too.
            ("PARALYZE", new[] { I("PARALYSIS"), mind }),
            ("BLINDNESS", new[] { I("BLINDNESS") }),
            ("DEAF", new[] { I("DEAFNESS") }),
            ("SLOW", new[] { I("SLOW") }),
            ("ENTANGLE", new[] { I("ENTANGLE") }),
            ("SILENCE", new[] { I("SILENCE") }),
            ("STUNNED", new[] { I("STUN"), mind }),
            ("SLEEP", new[] { I("SLEEP"), mind }),
            ("CHARMED", new[] { I("CHARM"), mind }),
            ("DOMINATED", new[] { I("DOMINATE"), mind }),
            ("CONFUSED", new[] { I("CONFUSED"), mind }),
            ("DAZED", new[] { I("DAZED"), mind }),
            ("CURSE", new[] { I("CURSED") }),
            ("ABILITY_DECREASE", new[] { I("ABILITY_DECREASE") }),
            ("ATTACK_DECREASE", new[] { I("ATTACK_DECREASE") }),
            ("DAMAGE_DECREASE", new[] { I("DAMAGE_DECREASE") }),
            ("DAMAGE_IMMUNITY_DECREASE", new[] { I("DAMAGE_IMMUNITY_DECREASE") }),
            ("AC_DECREASE", new[] { I("AC_DECREASE") }),
            ("MOVEMENT_SPEED_DECREASE", new[] { I("MOVEMENT_SPEED_DECREASE") }),
            ("SAVING_THROW_DECREASE", new[] { I("SAVING_THROW_DECREASE") }),
            ("SPELL_RESISTANCE_DECREASE", new[] { I("SPELL_RESISTANCE_DECREASE") }),
            ("SKILL_DECREASE", new[] { I("SKILL_DECREASE") }),
            ("KNOCKDOWN", new[] { I("KNOCKDOWN") }),
            ("NEGATIVELEVEL", new[] { I("NEGATIVE_LEVEL") }),
        };
        var map = new Dictionary<int, int[]>();
        foreach (var (name, imm) in pairs) map[k["EFFECT_TYPE_" + name]] = imm;
        return map;
    }
}
