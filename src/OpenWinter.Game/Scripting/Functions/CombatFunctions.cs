using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Attacking and combat state queries.</summary>
static class CombatFunctions
{
    static CombatSystem Combat(Call c) => c.World.System<CombatSystem>();

    // void ActionAttack(object oAttackee, int bPassive=FALSE)
    [NwFunc("ActionAttack")]
    static object? ActionAttack(Call c)
    {
        if (c.Self is { Commandable: true } self && c.Obj(0) is { } target)
        {
            bool passive = c.I(1) != 0;
            if (AttackAction.Retarget(c.World, self, target.Id, passive)) return null;
            self.Actions.Add(new AttackAction { Target = target.Id, Passive = passive });
            // An order that becomes the current action puts the creature in combat at once: the original's
            // GetIsInCombat is already TRUE when the script that gave the order goes on (nwserver ai_queries), so a
            // second OnPerception in the same instant leaves the stock AI alone (ai_melee: one shout, not two).
            // An order queued behind other actions does too when its target is an enemy, though the attack hasn't
            // begun (nwserver with Prelude's faction table, cast_cadence_handoffs cases 0-3 and a probe: an attack
            // queued behind a cast or a 3 s ActionWait read in combat from 0.4 s on; queued on a friend, not).
            if (self is Creature cr && !cr.IsDead && target != self && target.Area == self.Area && !Rules.HitPoints.IsDead(target)
                && (self.Actions.Count == 1 || c.World.Factions.IsEnemyOf(cr, target)))
                Combat(c).EnterCombat(cr);
        }
        return null;
    }

    static uint Valid(Call c, uint id) => c.World.Get(id) != null ? id : GameObject.InvalidId;

    // Measured on nwserver (ai_queries): the target of the last swing, cleared with the attack order.
    [NwFunc("GetAttackTarget")]
    static object? AttackTarget(Call c) =>
        c.ObjOrSelf(0) is { } o ? Combat(c).SwingTargetOf(o) : GameObject.InvalidId;

    // The target of the current attack order, but not while the creature walks up to it (measured, ai_queries).
    [NwFunc("GetAttemptedAttackTarget")]
    static object? AttemptedAttack(Call c) =>
        c.Self is { } o ? Combat(c).AttemptedTarget(o) : GameObject.InvalidId;

    [NwFunc("GetAttemptedSpellTarget")]
    static object? AttemptedSpell(Call c) =>
        c.Self is { } o ? Valid(c, o.Locals.Objects.GetValueOrDefault("__attempted_spell_target", GameObject.InvalidId)) : GameObject.InvalidId;

    [NwFunc("GetLastAttacker")]
    static object? LastAttacker(Call c) =>
        c.ObjOrSelf(0) is { } o ? Valid(c, Combat(c).State(o).LastAttacker) : GameObject.InvalidId;

    // GetLastDamager lives in CreatureStatFunctions (recorded by HitPoints.Damage).

    [NwFunc("GetGoingToBeAttackedBy")]
    static object? GoingToBeAttackedBy(Call c)
    {
        var target = c.Obj(0);
        if (target == null) return GameObject.InvalidId;
        var attacker = c.World.AllObjects.OfType<Creature>().FirstOrDefault(cr => cr.Area == target.Area && Combat(c).IsInCombat(cr) && Combat(c).State(cr).Target == target.Id);
        return attacker?.Id ?? GameObject.InvalidId;
    }

    [NwFunc("GetLastKiller")] static object? LastKiller(Call c) => Valid(c, c.Event.Obj("Killer"));

    // The weapon of the creature's last attack (OBJECT_INVALID for unarmed strikes or before any attack).
    [NwFunc("GetLastWeaponUsed")]
    static object? LastWeapon(Call c) =>
        c.Obj<Creature>(0) is not { } cr ? GameObject.InvalidId
        : Combat(c).TryState(cr) is { LastAttack: not null } s ? Valid(c, s.LastWeapon)
        : cr.Equipment.GetValueOrDefault(EquipSlot.RightHand)?.Id ?? GameObject.InvalidId;

    // SPECIAL_ATTACK_* of the creature's last attack (feat attacks, flurry, rapid shot).
    [NwFunc("GetLastAttackType")]
    static object? LastAttackType(Call c) =>
        c.ObjOrSelf(0) is { } o && Combat(c).TryState(o) is { } s ? s.LastAttackType : 0;

    // COMBAT_MODE_* the creature was in for its last attack.
    [NwFunc("GetLastAttackMode")]
    static object? LastAttackMode(Call c) =>
        c.ObjOrSelf(0) is { } o && Combat(c).TryState(o) is { } s ? s.LastAttackMode : 0;

    // void DoWhirlwindAttack(int bDisplayFeedback=TRUE, int bImproved=FALSE): the whirlwind feat scripts call this.
    [NwFunc("DoWhirlwindAttack")]
    static object? Whirlwind(Call c)
    {
        if (c.Self is Creature self) Combat(c).Whirlwind(self, c.I(1) != 0, c.I(0) != 0);
        return null;
    }

    [NwFunc("SurrenderToEnemies")]
    static object? Surrender(Call c)
    {
        if (c.Self is not Creature self || self.Area == null) return null;
        // Everyone fighting the caller's faction stops and forgets the quarrel.
        foreach (var cr in self.Area.Objects.OfType<Creature>())
        {
            var s = Combat(c).State(cr);
            if (c.World.Get(s.Target) is Creature t && t.Faction == self.Faction) Combat(c).Stop(cr);
            if (cr.Faction == self.Faction) Combat(c).Stop(cr);
        }
        return null;
    }

    // int TouchAttackMelee(object oTarget, int bDisplayFeedback=TRUE, object oAttacker=OBJECT_SELF) / TouchAttackRanged:
    // 0 miss, 1 hit, 2 critical.
    [NwFunc("TouchAttackMelee")] static object? TouchMelee(Call c) => Touch(c, ranged: false);
    [NwFunc("TouchAttackRanged")] static object? TouchRanged(Call c) => Touch(c, ranged: true);

    static int Touch(Call c, bool ranged)
    {
        // oAttacker (EE) defaults to OBJECT_SELF.
        if (c.ObjOrSelf(2) is not Creature self || c.Obj(0) is not { } target) return 0;
        return Combat(c).TouchAttack(self, target, ranged, c.I(1, 1) != 0);
    }
}
