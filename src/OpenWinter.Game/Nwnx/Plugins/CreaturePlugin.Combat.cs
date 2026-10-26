using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Creature's combat functions (parity suite nwnx_creature_attack).</summary>
static partial class CreaturePlugin
{
    static CombatSystem Combat(NwnxCall n) => n.World.System<CombatSystem>();
    static CreatureCombatRules? CombatRules(NwnxCall n) => Combat(n).Rules as CreatureCombatRules;

    /// <summary>The base attack bonus, replacing the class tables' (attacks per round follow); 0 goes back to them.</summary>
    [NwnxFunc("Creature", "SetBaseAttackBonus")]
    static void SetBab(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int bab = n.PopInt();
        if (c != null) O(c).BaseAttackBonus = Math.Clamp(bab, 0, 254);
    }

    [NwnxFunc("Creature", "GetAttacksPerRound")]
    static void AttacksPerRound(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); bool baseApr = n.PopInt() != 0;
        n.Push(c != null ? CreatureRules.AttacksPerRound(n.World, c, checkOverride: !baseApr) : -1);
    }

    [NwnxFunc("Creature", "GetAttackBonus")]
    static void AttackBonus(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        bool melee = n.PopInt() != 0, touch = n.PopInt() != 0, offHand = n.PopInt() != 0, includeBase = n.PopInt() != 0;
        n.Push(c != null && CombatRules(n) is { } r ? r.OwnAttackBonus(c, melee, touch, offHand, includeBase) : -1);
    }

    [NwnxFunc("Creature", "GetTotalEffectBonus")]
    static void TotalEffectBonus(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int type = n.PopInt(); var target = n.PopObject();
        bool elemental = n.PopInt() != 0, forceMax = n.PopInt() != 0;
        int save = n.PopInt(), saveType = n.PopInt(), skill = n.PopInt(), ability = n.PopInt();
        bool offHand = n.PopInt() != 0;
        n.Push(c != null && CombatRules(n) is { } r ? r.TotalEffectBonus(c, type, target, elemental, forceMax, save, saveType, skill, ability, offHand) : -1);
    }

    [NwnxFunc("Creature", "GetArmorClassVersus")]
    static void ArmorClassVersus(NwnxCall n)
    {
        var t = n.PopObject<Creature>(); var versus = n.PopObject<Creature>(); bool touch = n.PopInt() != 0;
        if (t == null || CombatRules(n) is not { } r) { n.Push(-255); return; }
        n.Push(r.ArmorClassVersus(t, versus, touch, Combat(n).IsFlatFooted(t)));
    }

    [NwnxFunc("Creature", "GetFlatFooted")]
    static void FlatFooted(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(c != null && Combat(n).IsFlatFooted(c));
    }

    [NwnxFunc("Creature", "GetIsFlanking")]
    static void IsFlanking(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var t = n.PopObject<Creature>();
        n.Push(c != null && t != null && c != t && Combat(n).IsFlanking(c, t));
    }

    /// <summary>Armour's skill penalty, as a negative number (nwserver: chain mail -5).</summary>
    [NwnxFunc("Creature", "GetArmorCheckPenalty")]
    static void ArmorCheckPenalty(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        if (c == null || c.PolymorphForm != null || !c.Equipment.TryGetValue(EquipSlot.Chest, out var armour)) { n.Push(0); return; }
        n.Push(-Math.Abs(n.World.Resources.TwoDA("armor").GetInt(CreatureRules.ArmourItemAC(n.World, armour), "ACCHECK")));
    }

    /// <summary>A shield's skill penalty, as a negative number (nwserver: a tower shield -10).</summary>
    [NwnxFunc("Creature", "GetShieldCheckPenalty")]
    static void ShieldCheckPenalty(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        if (c == null || c.PolymorphForm != null || !c.Equipment.TryGetValue(EquipSlot.LeftHand, out var shield)) { n.Push(0); return; }
        n.Push(-Math.Abs(n.World.Resources.TwoDA("baseitems").GetInt(shield.BaseItem, "ArmorCheckPen")));
    }

    [NwnxFunc("Creature", "GetMaxAttackRange")]
    static void MaxAttackRange(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var t = n.PopObject();
        n.Push(c != null && t != null ? Combat(n).MaxAttackRange(c, t) : 0f);
    }

    [NwnxFunc("Creature", "GetMaximumBonusAttacks")]
    static void MaximumBonusAttacks(NwnxCall n)
    {
        var c = n.PopObject<Creature>();
        n.Push(c == null ? 0 : c.Overrides?.MaxBonusAttacks ?? 5);
    }

    [NwnxFunc("Creature", "SetMaximumBonusAttacks")]
    static void SetMaximumBonusAttacks(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int max = n.PopInt(); n.PopInt();
        if (c != null) O(c).MaxBonusAttacks = max < 0 ? null : max;
    }

    // ---- Critical hits ---------------------------------------------------------

    /// <summary>Hands 1 (main) and 2 (off); anything else stands for all attacks.</summary>
    static int Hand(int hand) => hand is 1 or 2 ? hand : 0;

    static void SetCrit(NwnxCall n, Func<CreatureOverrides, Dictionary<(int, int), int>> table, bool clearOnNegative)
    {
        var c = n.PopObject<Creature>(); int value = n.PopInt(); int hand = Hand(n.PopInt()); n.PopInt(); int item = n.PopInt();
        if (c == null) return;
        var d = table(O(c));
        if (clearOnNegative && value < 0) d.Remove((hand, item));
        else d[(hand, item)] = value;
    }

    static void GetCrit(NwnxCall n, Func<CreatureOverrides, Dictionary<(int, int), int>> table, int none)
    {
        var c = n.PopObject<Creature>(); int hand = Hand(n.PopInt()); int item = n.PopInt();
        n.Push(c?.Overrides is { } o && table(o).TryGetValue((hand, item), out int v) ? v : c == null ? -1 : none);
    }

    [NwnxFunc("Creature", "SetCriticalMultiplierModifier")] static void SetCritMultMod(NwnxCall n) => SetCrit(n, o => o.CritMultiplierModifier, false);
    [NwnxFunc("Creature", "GetCriticalMultiplierModifier")] static void GetCritMultMod(NwnxCall n) => GetCrit(n, o => o.CritMultiplierModifier, 0);
    [NwnxFunc("Creature", "SetCriticalMultiplierOverride")] static void SetCritMultOver(NwnxCall n) => SetCrit(n, o => o.CritMultiplierOverride, true);
    [NwnxFunc("Creature", "GetCriticalMultiplierOverride")] static void GetCritMultOver(NwnxCall n) => GetCrit(n, o => o.CritMultiplierOverride, -1);
    [NwnxFunc("Creature", "SetCriticalRangeModifier")] static void SetCritRangeMod(NwnxCall n) => SetCrit(n, o => o.CritRangeModifier, false);
    [NwnxFunc("Creature", "GetCriticalRangeModifier")] static void GetCritRangeMod(NwnxCall n) => GetCrit(n, o => o.CritRangeModifier, 0);
    [NwnxFunc("Creature", "SetCriticalRangeOverride")] static void SetCritRangeOver(NwnxCall n) => SetCrit(n, o => o.CritRangeOverride, true);
    [NwnxFunc("Creature", "GetCriticalRangeOverride")] static void GetCritRangeOver(NwnxCall n) => GetCrit(n, o => o.CritRangeOverride, -1);

    // ---- Rolls, initiative, special attacks ------------------------------------

    [NwnxFunc("Creature", "SetInitiativeModifier")]
    static void SetInitiativeModifier(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int mod = n.PopInt(); n.PopInt();
        if (c != null) O(c).InitiativeModifier = mod;
    }

    [NwnxFunc("Creature", "GetInitiativeModifier")]
    static void GetInitiativeModifier(NwnxCall n) => n.Push(n.PopObject<Creature>()?.Overrides?.InitiativeModifier ?? 0);

    /// <summary>A modifier for one natural attack roll, for one attacker or (OBJECT_INVALID) every creature.</summary>
    [NwnxFunc("Creature", "SetAttackRollOverride")]
    static void SetAttackRollOverride(NwnxCall n)
    {
        uint id = n.PopObjectId(); int roll = n.PopInt(); int mod = n.PopInt();
        var table = n.World.Get(id) is Creature c ? O(c).AttackRollOverride
            : id == GameObject.InvalidId ? n.World.System<CreatureOverrideSystem>().AttackRollOverride : null;
        if (table != null && roll is >= 1 and <= 20) table[roll] = mod;
    }

    /// <summary>Parry mode parries every attack, for one creature or (OBJECT_INVALID) every creature.</summary>
    [NwnxFunc("Creature", "SetParryAllAttacks")]
    static void SetParryAllAttacks(NwnxCall n)
    {
        uint id = n.PopObjectId(); bool parry = n.PopInt() != 0;
        if (n.World.Get(id) is Creature c) O(c).ParryAllAttacks = parry;
        else if (id == GameObject.InvalidId) n.World.System<CreatureOverrideSystem>().ParryAllAttacks = parry;
    }

    [NwnxFunc("Creature", "DoCleaveAttack")]
    static void DoCleaveAttack(NwnxCall n)
    {
        if (n.PopObject<Creature>() is { } c) Combat(n).CleaveAttack(c);
    }

    [NwnxFunc("Creature", "BroadcastAttackOfOpportunity")]
    static void BroadcastAttackOfOpportunity(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); var single = n.PopObject<Creature>(); n.PopInt();
        if (c != null) Combat(n).BroadcastAttackOfOpportunity(c, single);
    }

    /// <summary>The elemental projectile of the creature's ranged attacks (-1: the weapon's own); shown by clients.</summary>
    [NwnxFunc("Creature", "OverrideRangedProjectileVFX")]
    static void OverrideRangedProjectileVfx(NwnxCall n)
    {
        var c = n.PopObject<Creature>(); int vfx = n.PopInt(); n.PopInt();
        if (c == null) return;
        O(c).RangedProjectileVfx = vfx < 0 ? null : vfx;
        n.World.Output?.Notify("creature-override", c, new { rangedProjectileVfx = vfx });
    }
}
