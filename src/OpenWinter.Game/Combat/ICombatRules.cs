using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Combat;

/// <summary>
/// The rule calculations combat needs, one attack at a time. <see cref="CombatSystem"/> owns timing,
/// targeting, the attack/hit/critical rolls, parries, deflection, attacks of opportunity, cleave and
/// special attack side effects; the rules answer the numbers. The default implementation is
/// <see cref="CreatureCombatRules"/>; a module-specific rule set can replace <see cref="CombatSystem.Rules"/>.
/// </summary>
public interface ICombatRules
{
    /// <summary>The attacks a creature makes this round, in order (main hand iteratives, extra attacks, off hand).</summary>
    IReadOnlyList<PlannedAttack> PlanRound(Creature c);

    /// <summary>Builds the context for an attack with a weapon (null = unarmed) and fills in the situation.</summary>
    AttackContext Context(Creature attacker, GameObject target, Item? weapon, bool offHand, IDice dice);

    /// <summary>Fills the situation flags (flat-footed, flanked, who can see whom, distance).</summary>
    void Prepare(AttackContext a);

    int AttackBonus(AttackContext a);

    /// <summary>The target's armour class against this attack (versus effects, denied Dexterity, touch, dodge).</summary>
    int ArmorClass(AttackContext a);

    /// <summary>Miss chance (0-100) from the target's concealment and the attacker's miss chance; whichever is higher.</summary>
    int Concealment(AttackContext a);

    /// <summary>Lowest natural roll that threatens a critical (20 = only a natural 20) and the multiplier.</summary>
    (int Threat, int Multiplier) Critical(AttackContext a);

    /// <summary>The target ignores critical hits (and with them sneak attacks' sister rules, devastating critical).</summary>
    bool IsCriticalImmune(AttackContext a);

    /// <summary>Damage of a hit by type, with the damage power for damage reduction (sneak attack included when it applies).</summary>
    DamageRoll Damage(AttackContext a, bool critical);
}

/// <summary>
/// Replaceable pieces other packages plug into: line of sight for combat (skills/stealth), casting a
/// spell from an on-hit item property (spells). Defaults are set by the combat package.
/// </summary>
public static class CombatHooks
{
    /// <summary>Whether <c>observer</c> can see <c>target</c> for combat (invisibility, darkness, blindness, hiding).</summary>
    public static Func<World, Creature, GameObject, bool> CanSee = (w, o, t) => CombatVision.CanSee(w, o, t);

    /// <summary>
    /// Casts an on-hit spell (OnHitCastSpell item property): (world, caster, target, item, spellId, casterLevel).
    /// The default runs the spell's impact script with the item's spell context (GetSpellCastItem,
    /// GetSpellTargetObject); the spells package can route it through spell casting instead.
    /// </summary>
    public static Action<World, Creature, GameObject, Item, int, int> CastOnHitSpell = DefaultOnHitSpell;

    static void DefaultOnHitSpell(World world, Creature caster, GameObject target, Item item, int spell, int casterLevel)
    {
        var script = world.Resources.TwoDA("spells").Get(spell, "ImpactScript");
        if (script.Length == 0 || world.Scripts == null) return;
        caster.LastSpell = new SpellCastInfo { SpellId = spell, CasterLevel = casterLevel, CastItem = item.Id };
        world.Scripts.Run(script, caster.Id, new Scripting.EventContext()
            .Set("SpellCastItem", item.Id).Set("SpellId", spell).Set("SpellCaster", caster.Id)
            .Set("SpellTargetObject", target.Id).Set("SpellTarget", target.Id)
            .Set("SpellTargetLocation", target.Location).Set("CasterLevel", casterLevel));
    }
}
