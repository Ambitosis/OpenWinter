using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Combat;

/// <summary>Cleave left to the module (NWNX_Risenholm_SetScriptedCleave and DoCleaveAttack).</summary>
public sealed partial class CombatSystem
{
    /// <summary>A killing blow earns no Cleave by itself; the module queues one with <see cref="QueueCleave"/>.</summary>
    public bool ScriptedCleave;

    /// <summary>
    /// A Cleave (Great Cleave without limit, Cleave once a round) by a creature that has just killed: one attack on
    /// another enemy in reach, never the one it killed. False when none is due.
    /// </summary>
    public bool QueueCleave(Creature attacker, GameObject? killed)
    {
        if (attacker.IsDead || Creature is not { } rules) return false;
        bool great = HasFeat(attacker, CombatK.FeatGreatCleave);
        if (!great && !HasFeat(attacker, CombatK.FeatCleave)) return false;
        var s = State(attacker);
        if (!great && s.Cleaves >= RI("NUM_CLEAVE_ATTACKS", 1)) return false;
        var weapon = rules.Weapons(attacker).Main;
        if (weapon != null && _world.Inventory.Rules.IsRangedWeapon(weapon.BaseItem)) return false;
        var next = NearestEnemy(attacker, killed, (c, t) => MeleeReach(c, t));
        if (next == null || next == killed) return false;
        s.Cleaves++;
        var a = Rules.Context(attacker, next, weapon, false, Dice);
        a.Kind = great ? AttackKind.GreatCleave : AttackKind.Cleave;
        Resolve(a);
        return true;
    }
}
