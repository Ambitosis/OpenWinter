using OpenWinter.Game.Nwnx.Plugins;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Combat;

/// <summary>The NWNX_Damage attack event: each attack, hit or miss, as it is swung.</summary>
public sealed partial class CombatSystem
{
    /// <summary>
    /// A planned attack's number in its round and whether it counts as an extra attack. As on nwserver (parity suite
    /// nwnx_damage): main-hand attacks come first, then extra attacks, then off-hand ones, and attacks of opportunity
    /// swung before take the first numbers; a main-hand attack beyond the creature's normal count (one an attack of
    /// opportunity pushed along) is an extra attack.
    /// </summary>
    static (int Number, bool Extra) AttackNumber(List<PlannedAttack> plan, PlannedAttack planned, int opportunities)
    {
        int normal = plan.Count(p => !p.Extra && !p.OffHand && p.OpportunityTarget == GameObject.InvalidId);
        int mains = plan.Count(p => !p.Extra && !p.OffHand) + opportunities, extras = plan.Count(p => p.Extra && !p.OffHand);
        int Position(Func<PlannedAttack, bool> group) => plan.Where(group).ToList().IndexOf(planned) + 1;
        if (planned.OffHand) return (mains + extras + Position(p => p.OffHand), false);
        if (planned.Extra) return (mains + Position(p => p.Extra && !p.OffHand), true);
        int n = Position(p => !p.Extra && !p.OffHand) + opportunities;
        return (n, n > normal);
    }

    /// <summary>NWNX_Damage weapon attack type: 1 main hand, 2 off hand, 3-5 creature weapons, 6 extra, 7 unarmed, 8 unarmed extra.</summary>
    static int WeaponAttackType(AttackContext a)
    {
        if (a.Weapon == null) return a.ExtraAttack ? 8 : 7;
        if (a.CreatureWeapon)
        {
            foreach (var (slot, item) in a.Attacker.Equipment)
                if (item == a.Weapon)
                    return slot switch { EquipSlot.CreatureLeft => 3, EquipSlot.CreatureRight => 4, EquipSlot.CreatureBite => 5, _ => 3 };
            return 3;
        }
        if (a.OffHand) return 2;
        return a.ExtraAttack ? 6 : 1;
    }

    /// <summary>NWNX_Damage attack type: 65002 attack of opportunity, 65003 riposte, or the feat of a special attack.</summary>
    static int SpecialAttackType(AttackContext a) => a.Kind switch
    {
        AttackKind.Opportunity => 65002,
        AttackKind.Riposte => 65003,
        AttackKind.Cleave => CombatK.FeatCleave,
        AttackKind.GreatCleave => CombatK.FeatGreatCleave,
        AttackKind.CircleKick => CombatK.FeatCircleKick,
        AttackKind.Whirlwind => CombatK.FeatWhirlwindAttack,
        _ => a.SpecialFeat >= 0 ? a.SpecialFeat : 0,
    };

    int AttackResult(AttackOutcome o, bool hit)
    {
        if (hit)
        {
            // A natural 20 that the attack bonus wouldn't have made is an ordinary hit (nwnx_damage nat20).
            if (o.Critical && o.Devastating) return NwnxAttackEventData.DevastatingCritical;
            return o.Critical ? NwnxAttackEventData.CriticalHit : NwnxAttackEventData.Hit;
        }
        if (o.Parried || o.Deflected) return NwnxAttackEventData.Parried;
        if (o.Concealed) return NwnxAttackEventData.Concealed;
        return NwnxAttackEventData.Miss;
    }

    /// <summary>
    /// Runs the NWNX_Damage attack event for an attack just swung. The script sees the damage as it will get past
    /// the target's defences, physical damage as base weapon damage (nwnx_damage: weapon fire damage on a fire-immune
    /// target reads 0, a hit soaked by damage reduction 0). What it changes stands: a hit made a miss deals nothing,
    /// a miss made a hit deals the damage it set, and damage it set is dealt as set.
    /// </summary>
    void AttackEvent(AttackOutcome o, ref bool hit)
    {
        var a = o.Context;
        var att = a.Attacker;
        if (!DamagePlugin.HasAttackEvent(_world, att.Id)) return;
        var t = a.Target;
        var data = new NwnxAttackEventData
        {
            Target = t.Id, AttackNumber = a.AttackNumber, WeaponAttackType = WeaponAttackType(a), AttackType = SpecialAttackType(a),
            ToHitRoll = o.Roll, ToHitModifier = o.AttackBonus, Ranged = a.Ranged, AttackResult = AttackResult(o, hit),
        };
        if (Creature is { } r && r.SneakAttackApplies(a, out int sneak, out int death))
            data.SneakAttack = (sneak > 0 ? 1 : 0) | (death > 0 ? 2 : 0);
        if (hit && o.Damage is { } d)
        {
            var dealt = HitPoints.Defend(_world, t, att.Id, d.ByType, d.Power, ignoreReductions: false, a.Ranged, d.Unresisted, null, consume: false);
            var reported = new Dictionary<int, int>();
            int total = 0;
            foreach (var (type, raw) in d.ByType)
            {
                if (raw <= 0) continue;
                int key = (type & CombatK.PhysicalDamage) != 0 ? CombatK.DamageBaseWeapon : type;
                int amount = dealt.GetValueOrDefault(type);
                reported[key] = reported.GetValueOrDefault(key) + amount;
                total += amount;
            }
            data.Damage = NwnxDamageData.From(reported);
            data.KillingBlow = total > 0 && !t.Plot && t is not Creature { IsImmortal: true }
                && HitPoints.Current(t) - total <= (t is Creature { IsPC: true } ? HitPoints.PlayerDeathThreshold : 0);
        }
        var before = data.Damage.Copy();
        int result = data.AttackResult, sneakBefore = data.SneakAttack;
        DamagePlugin.RunAttackEvent(_world, att.Id, data);

        bool nowHit = NwnxAttackEventData.IsHit(data.AttackResult);
        if (hit && !nowHit)
        {
            hit = o.Hit = false;
            o.Critical = o.Devastating = false;
            o.Damage = null;
            o.Feedback.Remove("Critical Hit");
        }
        else if (nowHit && (!hit || !data.Damage.SameAs(before) || data.AttackResult != result))
        {
            hit = o.Hit = true;
            bool critical = data.AttackResult is NwnxAttackEventData.CriticalHit or NwnxAttackEventData.DevastatingCritical;
            if (critical && !o.Critical) o.Feedback.Add("Critical Hit");
            else if (!critical && o.Critical) o.Feedback.Remove("Critical Hit");
            o.Critical = critical;
            o.Devastating = data.AttackResult == NwnxAttackEventData.DevastatingCritical;
            if (!data.Damage.SameAs(before) || o.Damage == null)
            {
                var set = new DamageRoll { Defended = true, Power = o.Damage?.Power ?? 0 };
                foreach (var (type, amount) in data.Damage.ToDictionary()) set.ByType[type] = amount;
                o.Damage = set;
            }
        }
        // The sneak attack value only changes what the attack is announced as.
        if (data.SneakAttack != sneakBefore)
        {
            o.Feedback.Remove("Sneak Attack");
            o.Feedback.Remove("Death Attack");
            if (data.SneakAttack != 0) o.Feedback.Insert(0, (data.SneakAttack & 2) != 0 ? "Death Attack" : "Sneak Attack");
            o.SneakSituation = data.SneakAttack != 0;
        }
    }
}
