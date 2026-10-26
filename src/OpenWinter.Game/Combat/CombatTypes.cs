using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Combat;

/// <summary>Dice for combat. Inject a deterministic one in tests (<see cref="CombatSystem.Dice"/>).</summary>
public interface IDice
{
    /// <summary>A roll of one die: 1..<paramref name="sides"/>.</summary>
    int Roll(int sides);
}

public sealed class RandomDice : IDice
{
    readonly Random _rng;
    public RandomDice(Random? rng = null) { _rng = rng ?? Random.Shared; }
    public int Roll(int sides) => sides <= 1 ? 1 : _rng.Next(1, sides + 1);
}

/// <summary>
/// Dice that return queued values first (clamped to the die), then fall back to a seeded generator.
/// Tests queue the exact d20/d100/damage rolls they want.
/// </summary>
public sealed class ScriptedDice : IDice
{
    readonly Queue<int> _queued = new();
    readonly Random _rng;
    public ScriptedDice(int seed = 1, params int[] rolls) { _rng = new Random(seed); foreach (var r in rolls) _queued.Enqueue(r); }
    public ScriptedDice Then(params int[] rolls) { foreach (var r in rolls) _queued.Enqueue(r); return this; }
    public int Remaining => _queued.Count;
    public int Roll(int sides) => _queued.Count > 0 ? Math.Clamp(_queued.Dequeue(), 1, Math.Max(1, sides)) : sides <= 1 ? 1 : _rng.Next(1, sides + 1);
}

public static class DiceExtensions
{
    /// <summary>Sum of <paramref name="count"/> dice.</summary>
    public static int Roll(this IDice d, int count, int sides)
    {
        int total = 0;
        for (int i = 0; i < count; i++) total += sides <= 0 ? 0 : d.Roll(sides);
        return total;
    }
}

/// <summary>One attack of a combat round, planned at the start of the round.</summary>
public sealed class PlannedAttack
{
    /// <summary>The weapon (null = unarmed). Creature weapons and double weapons appear here too.</summary>
    public Item? Weapon;
    public bool OffHand;
    /// <summary>Iterative attack penalty (positive number subtracted from the attack bonus).</summary>
    public int Penalty;
    /// <summary>An extra attack (haste, rapid shot, flurry, EffectModifyAttacks).</summary>
    public bool Extra;
    /// <summary>An attack of opportunity at this creature, made as the round's first swing (<see cref="CombatSystem"/>).</summary>
    public uint OpportunityTarget = GameObject.InvalidId;
    public override string ToString() => $"{(OffHand ? "off" : "main")} {(Weapon == null ? "unarmed" : Weapon.BaseItem.ToString())} -{Penalty}{(Extra ? " extra" : "")}";
}

/// <summary>Kinds of attack outside the normal round (for feedback and rules that care).</summary>
public enum AttackKind { Normal, Opportunity, Cleave, GreatCleave, CircleKick, Riposte, Whirlwind, Touch }

/// <summary>Everything the rules need to know about one attack. Situation flags are filled by <see cref="ICombatRules.Prepare"/>.</summary>
public sealed class AttackContext
{
    public required World World;
    public required Creature Attacker;
    public required GameObject Target;
    public required IDice Dice;
    /// <summary>The weapon (null = unarmed strike).</summary>
    public Item? Weapon;
    /// <summary>Ammunition fired by a launcher (an equipped stack, or the blueprint behind Unlimited Ammunition).</summary>
    public Item? Ammo;
    public bool OffHand;
    public bool Ranged;
    /// <summary>Thrown weapon (darts, shuriken, throwing axes): ranged, consumes itself, adds Strength to damage.</summary>
    public bool Thrown;
    public bool CreatureWeapon;
    public AttackKind Kind = AttackKind.Normal;
    /// <summary>Iterative penalty (positive) for this attack in the round.</summary>
    public int Penalty;
    /// <summary>Additional modifier from the caller (scripts, special cases).</summary>
    public int Modifier;
    /// <summary>SPECIAL_ATTACK_* value of a feat attack, 0 for none.</summary>
    public int SpecialAttack;
    /// <summary>FEAT_* used for a special attack (smite, stunning fist, quivering palm...), or -1.</summary>
    public int SpecialFeat = -1;
    /// <summary>Touch attack: armour, shield and natural armour don't count.</summary>
    public bool Touch;
    /// <summary>Swing animation to send instead of the weapon's ("" = none).</summary>
    public string? Animation;
    /// <summary>Send combat log lines to the players involved (DoWhirlwindAttack's bDisplayFeedback).</summary>
    public bool ShowFeedback = true;
    /// <summary>1-based number of the attack in its round, main-hand attacks first, then extra and off-hand ones.</summary>
    public int AttackNumber = 1;
    /// <summary>An attack beyond the creature's normal ones this round (haste and other extra attacks).</summary>
    public bool ExtraAttack;

    // ---- situation (rules fill these in Prepare)
    /// <summary>The target can't use its Dexterity bonus (flat-footed, helpless, can't see the attacker).</summary>
    public bool TargetDeniedDex;
    /// <summary>The target isn't attacking anyone: sneak attacks work even when Uncanny Dodge keeps its Dexterity.</summary>
    public bool FlatFooted;
    /// <summary>The target is attacking someone else.</summary>
    public bool Flanked;
    /// <summary>The target can't see the attacker.</summary>
    public bool AttackerUnseen;
    /// <summary>The attacker can't see the target.</summary>
    public bool TargetUnseen;
    public float Distance;

    public bool Unarmed => Weapon == null;
    public Creature? TargetCreature => Target as Creature;
}

/// <summary>Damage of one hit by DAMAGE_TYPE_* bit, plus the damage power (DAMAGE_POWER_*) for damage reduction.</summary>
public sealed class DamageRoll
{
    public readonly Dictionary<int, int> ByType = new();
    /// <summary>The part of <see cref="ByType"/> that immunity and resistance don't touch (damage effects of other than physical types).</summary>
    public readonly Dictionary<int, int> Unresisted = new();
    public int Power;
    /// <summary>The amounts are final: they already got past the target's defences (a script set them).</summary>
    public bool Defended;
    /// <summary>Sneak attack and death attack damage included in <see cref="ByType"/>.</summary>
    public int Sneak;
    public bool DeathAttack;
    public int Total => ByType.Values.Sum();
    public void Add(int type, int amount)
    {
        if (amount == 0 || type == 0) return;
        ByType[type] = ByType.GetValueOrDefault(type) + amount;
    }
}

/// <summary>The result of one resolved attack. Sent to clients in the <c>attack</c> notify.</summary>
public sealed class AttackOutcome
{
    public required AttackContext Context;
    public int Roll, AttackBonus, ArmorClass;
    public bool Hit, Critical, Threat;
    /// <summary>A critical hit that brings a devastating critical (its Fortitude save comes when the blow lands).</summary>
    public bool Devastating;
    /// <summary>A confirmed threat, or a sneak attack, on a target immune to it (the log says so).</summary>
    public bool CriticalImmune, SneakImmune;
    public int ConfirmRoll;
    /// <summary>Concealment/miss chance percentage that applied (0 for none), and whether it made the attack miss.</summary>
    public int Concealment;
    public bool Concealed;
    public bool Parried, Deflected, EpicDodged;
    /// <summary>The attack came as a sneak or death attack (announced even when it missed).</summary>
    public bool SneakSituation;
    public DamageRoll? Damage;
    /// <summary>Hit points the target actually lost.</summary>
    public int Dealt;
    /// <summary>What got through the target's defences and what they took (for the combat log).</summary>
    public Rules.DamageBreakdown? Breakdown;
    public bool Killed;
    /// <summary>A coup de grace: the target was asleep or held, and the attack killed it outright.</summary>
    public bool CoupDeGrace;
    public bool OutOfAmmo;
    /// <summary>Combat feedback strings ("Attack Of Opportunity", "Sneak Attack", "Critical Hit", ...).</summary>
    public readonly List<string> Feedback = new();
    public string Animation = "";
    /// <summary>Seconds this swing takes (the round divided by the planned attacks).</summary>
    public float Swing = 1;
    /// <summary>The blow lands after the swing (a round attack): until <see cref="Landed"/>, no damage has been dealt.</summary>
    public bool Pending;
    /// <summary>The blow has landed: damage, effects and feedback are done.</summary>
    public bool Landed;
}
