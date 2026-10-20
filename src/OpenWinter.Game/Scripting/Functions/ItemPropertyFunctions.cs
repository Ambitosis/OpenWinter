using System.Runtime.CompilerServices;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>
/// Item property constructors, getters, add/remove and iteration. Constructors fill
/// the fields the toolset stores in PropertiesList: Type = ITEM_PROPERTY_* (itempropdef.2da
/// row), SubType, CostTable/CostTableValue and Param1/Param1Value (see <see cref="ItemPropertyRules.Make"/>).
/// </summary>
static class ItemPropertyFunctions
{
    static EffectConstants K(Call c) => EffectConstants.Of(c.Host.Definitions);

    /// <summary>ITEM_PROPERTY_<paramref name="name"/> with subtype, cost table value and param1 value (-1 for none).</summary>
    static ItemProperty P(Call c, string name, int sub = -1, int cost = -1, int param1 = -1)
    {
        var ip = ItemPropertyRules.Make(c.World, K(c)["ITEM_PROPERTY_" + name], sub, cost, param1);
        ip.Creator = c.SelfId;
        return ip;
    }

    // ------------------------------------------------------------------ constructors

    [NwFunc("ItemPropertyAbilityBonus")] static object? AbilityBonus(Call c) => P(c, "ABILITY_BONUS", c.I(0), c.I(1));
    [NwFunc("ItemPropertyACBonus")] static object? ACBonus(Call c) => P(c, "AC_BONUS", -1, c.I(0));
    [NwFunc("ItemPropertyACBonusVsAlign")] static object? ACBonusVsAlign(Call c) => P(c, "AC_BONUS_VS_ALIGNMENT_GROUP", c.I(0), c.I(1));
    [NwFunc("ItemPropertyACBonusVsDmgType")] static object? ACBonusVsDmg(Call c) => P(c, "AC_BONUS_VS_DAMAGE_TYPE", c.I(0), c.I(1));
    [NwFunc("ItemPropertyACBonusVsRace")] static object? ACBonusVsRace(Call c) => P(c, "AC_BONUS_VS_RACIAL_GROUP", c.I(0), c.I(1));
    [NwFunc("ItemPropertyACBonusVsSAlign")] static object? ACBonusVsSAlign(Call c) => P(c, "AC_BONUS_VS_SPECIFIC_ALIGNMENT", c.I(0), c.I(1));
    [NwFunc("ItemPropertyEnhancementBonus")] static object? Enhancement(Call c) => P(c, "ENHANCEMENT_BONUS", -1, c.I(0));
    [NwFunc("ItemPropertyEnhancementBonusVsAlign")] static object? EnhVsAlign(Call c) => P(c, "ENHANCEMENT_BONUS_VS_ALIGNMENT_GROUP", c.I(0), c.I(1));
    [NwFunc("ItemPropertyEnhancementBonusVsRace")] static object? EnhVsRace(Call c) => P(c, "ENHANCEMENT_BONUS_VS_RACIAL_GROUP", c.I(0), c.I(1));
    [NwFunc("ItemPropertyEnhancementBonusVsSAlign")] static object? EnhVsSAlign(Call c) => P(c, "ENHANCEMENT_BONUS_VS_SPECIFIC_ALIGNEMENT", c.I(0), c.I(1));
    [NwFunc("ItemPropertyEnhancementPenalty")] static object? EnhPenalty(Call c) => P(c, "DECREASED_ENHANCEMENT_MODIFIER", -1, c.I(0));
    [NwFunc("ItemPropertyWeightReduction")] static object? WeightReduction(Call c) => P(c, "BASE_ITEM_WEIGHT_REDUCTION", -1, c.I(0));
    [NwFunc("ItemPropertyBonusFeat")] static object? BonusFeat(Call c) => P(c, "BONUS_FEAT", c.I(0));
    [NwFunc("ItemPropertyBonusLevelSpell")] static object? BonusLevelSpell(Call c) => P(c, "BONUS_SPELL_SLOT_OF_LEVEL_N", c.I(0), c.I(1));
    [NwFunc("ItemPropertyCastSpell")] static object? CastSpell(Call c) => P(c, "CAST_SPELL", c.I(0), c.I(1));
    [NwFunc("ItemPropertyDamageBonus")] static object? DamageBonus(Call c) => P(c, "DAMAGE_BONUS", c.I(0), c.I(1));
    [NwFunc("ItemPropertyDamageBonusVsAlign")] static object? DamageVsAlign(Call c) => P(c, "DAMAGE_BONUS_VS_ALIGNMENT_GROUP", c.I(0), c.I(2), c.I(1));
    [NwFunc("ItemPropertyDamageBonusVsRace")] static object? DamageVsRace(Call c) => P(c, "DAMAGE_BONUS_VS_RACIAL_GROUP", c.I(0), c.I(2), c.I(1));
    [NwFunc("ItemPropertyDamageBonusVsSAlign")] static object? DamageVsSAlign(Call c) => P(c, "DAMAGE_BONUS_VS_SPECIFIC_ALIGNMENT", c.I(0), c.I(2), c.I(1));
    [NwFunc("ItemPropertyDamageImmunity")] static object? DamageImmunity(Call c) => P(c, "IMMUNITY_DAMAGE_TYPE", c.I(0), c.I(1));
    [NwFunc("ItemPropertyDamagePenalty")] static object? DamagePenalty(Call c) => P(c, "DECREASED_DAMAGE", -1, c.I(0));
    [NwFunc("ItemPropertyDamageReduction")] static object? DamageReduction(Call c) => P(c, "DAMAGE_REDUCTION", c.I(0), c.I(1));
    [NwFunc("ItemPropertyDamageResistance")] static object? DamageResistance(Call c) => P(c, "DAMAGE_RESISTANCE", c.I(0), c.I(1));
    [NwFunc("ItemPropertyDamageVulnerability")] static object? DamageVulnerability(Call c) => P(c, "DAMAGE_VULNERABILITY", c.I(0), c.I(1));
    [NwFunc("ItemPropertyDarkvision")] static object? Darkvision(Call c) => P(c, "DARKVISION");
    [NwFunc("ItemPropertyDecreaseAbility")] static object? DecreaseAbility(Call c) => P(c, "DECREASED_ABILITY_SCORE", c.I(0), c.I(1));
    [NwFunc("ItemPropertyDecreaseAC")] static object? DecreaseAC(Call c) => P(c, "DECREASED_AC", c.I(0), c.I(1));
    [NwFunc("ItemPropertyDecreaseSkill")] static object? DecreaseSkill(Call c) => P(c, "DECREASED_SKILL_MODIFIER", c.I(0), c.I(1));
    [NwFunc("ItemPropertyContainerReducedWeight")] static object? ContainerWeight(Call c) => P(c, "ENHANCED_CONTAINER_REDUCED_WEIGHT", -1, c.I(0));
    [NwFunc("ItemPropertyExtraMeleeDamageType")] static object? ExtraMelee(Call c) => P(c, "EXTRA_MELEE_DAMAGE_TYPE", c.I(0));
    [NwFunc("ItemPropertyExtraRangeDamageType")] static object? ExtraRanged(Call c) => P(c, "EXTRA_RANGED_DAMAGE_TYPE", c.I(0));
    [NwFunc("ItemPropertyHaste")] static object? Haste(Call c) => P(c, "HASTE");
    [NwFunc("ItemPropertyHolyAvenger")] static object? HolyAvenger(Call c) => P(c, "HOLY_AVENGER");
    [NwFunc("ItemPropertyImmunityMisc")] static object? ImmunityMisc(Call c) => P(c, "IMMUNITY_MISCELLANEOUS", c.I(0));
    [NwFunc("ItemPropertyImprovedEvasion")] static object? ImprovedEvasion(Call c) => P(c, "IMPROVED_EVASION");
    [NwFunc("ItemPropertyBonusSpellResistance")] static object? SpellResistance(Call c) => P(c, "SPELL_RESISTANCE", -1, c.I(0));
    [NwFunc("ItemPropertyBonusSavingThrowVsX")] static object? SaveVsX(Call c) => P(c, "SAVING_THROW_BONUS", c.I(0), c.I(1));
    [NwFunc("ItemPropertyBonusSavingThrow")] static object? Save(Call c) => P(c, "SAVING_THROW_BONUS_SPECIFIC", c.I(0), c.I(1));
    [NwFunc("ItemPropertyKeen")] static object? Keen(Call c) => P(c, "KEEN");
    [NwFunc("ItemPropertyLight")] static object? Light(Call c) => P(c, "LIGHT", -1, c.I(0), c.I(1));
    [NwFunc("ItemPropertyMaxRangeStrengthMod")] static object? Mighty(Call c) => P(c, "MIGHTY", -1, c.I(0));
    [NwFunc("ItemPropertyNoDamage")] static object? NoDamage(Call c) => P(c, "NO_DAMAGE");
    [NwFunc("ItemPropertyOnHitProps")] static object? OnHit(Call c) => P(c, "ON_HIT_PROPERTIES", c.I(0), c.I(1), c.I(2));
    [NwFunc("ItemPropertyReducedSavingThrowVsX")] static object? ReducedSaveVsX(Call c) => P(c, "DECREASED_SAVING_THROWS", c.I(0), c.I(1));
    [NwFunc("ItemPropertyReducedSavingThrow")] static object? ReducedSave(Call c) => P(c, "DECREASED_SAVING_THROWS_SPECIFIC", c.I(0), c.I(1));
    [NwFunc("ItemPropertyRegeneration")] static object? Regeneration(Call c) => P(c, "REGENERATION", -1, c.I(0));
    [NwFunc("ItemPropertySkillBonus")] static object? SkillBonus(Call c) => P(c, "SKILL_BONUS", c.I(0), c.I(1));
    [NwFunc("ItemPropertySpellImmunitySpecific")] static object? SpellImmunitySpecific(Call c) => P(c, "IMMUNITY_SPECIFIC_SPELL", -1, c.I(0));
    [NwFunc("ItemPropertySpellImmunitySchool")] static object? SpellImmunitySchool(Call c) => P(c, "IMMUNITY_SPELL_SCHOOL", c.I(0));
    [NwFunc("ItemPropertyThievesTools")] static object? ThievesTools(Call c) => P(c, "THIEVES_TOOLS", -1, c.I(0));
    [NwFunc("ItemPropertyAttackBonus")] static object? AttackBonus(Call c) => P(c, "ATTACK_BONUS", -1, c.I(0));
    [NwFunc("ItemPropertyAttackBonusVsAlign")] static object? AttackVsAlign(Call c) => P(c, "ATTACK_BONUS_VS_ALIGNMENT_GROUP", c.I(0), c.I(1));
    [NwFunc("ItemPropertyAttackBonusVsRace")] static object? AttackVsRace(Call c) => P(c, "ATTACK_BONUS_VS_RACIAL_GROUP", c.I(0), c.I(1));
    [NwFunc("ItemPropertyAttackBonusVsSAlign")] static object? AttackVsSAlign(Call c) => P(c, "ATTACK_BONUS_VS_SPECIFIC_ALIGNMENT", c.I(0), c.I(1));
    [NwFunc("ItemPropertyAttackPenalty")] static object? AttackPenalty(Call c) => P(c, "DECREASED_ATTACK_MODIFIER", -1, c.I(0));
    [NwFunc("ItemPropertyUnlimitedAmmo")] static object? UnlimitedAmmo(Call c) => P(c, "UNLIMITED_AMMUNITION", 0, c.I(0));
    [NwFunc("ItemPropertyLimitUseByAlign")] static object? LimitAlign(Call c) => P(c, "USE_LIMITATION_ALIGNMENT_GROUP", c.I(0));
    [NwFunc("ItemPropertyLimitUseByClass")] static object? LimitClass(Call c) => P(c, "USE_LIMITATION_CLASS", c.I(0));
    [NwFunc("ItemPropertyLimitUseByRace")] static object? LimitRace(Call c) => P(c, "USE_LIMITATION_RACIAL_TYPE", c.I(0));
    [NwFunc("ItemPropertyLimitUseBySAlign")] static object? LimitSAlign(Call c) => P(c, "USE_LIMITATION_SPECIFIC_ALIGNMENT", c.I(0));
    [NwFunc("ItemPropertyVampiricRegeneration")] static object? VampiricRegen(Call c) => P(c, "REGENERATION_VAMPIRIC", -1, c.I(0));
    // itempropdef.2da: the subtype is the strength (IPRP_TRAPS), the cost table value the type (iprp_trapcost), as in trap kit blueprints.
    [NwFunc("ItemPropertyTrap")] static object? Trap(Call c) => P(c, "TRAP", c.I(0), c.I(1));
    [NwFunc("ItemPropertyTrueSeeing")] static object? TrueSeeing(Call c) => P(c, "TRUE_SEEING");
    // The original stores the property itself as the param (not nSpecial) when its row has a param table.
    [NwFunc("ItemPropertyOnMonsterHitProperties")]
    static object? OnMonsterHit(Call c) =>
        P(c, "ON_MONSTER_HIT", c.I(0), -1, c.World.Resources.TwoDA("iprp_monsterhit").IsEmpty(c.I(0), "Param1ResRef") ? -1 : c.I(0));
    [NwFunc("ItemPropertyTurnResistance")] static object? TurnResistance(Call c) => P(c, "TURN_RESISTANCE", -1, c.I(0));
    [NwFunc("ItemPropertyMassiveCritical")] static object? MassiveCritical(Call c) => P(c, "MASSIVE_CRITICALS", -1, c.I(0));
    [NwFunc("ItemPropertyFreeAction")] static object? FreeAction(Call c) => P(c, "FREEDOM_OF_MOVEMENT");
    [NwFunc("ItemPropertyMonsterDamage")] static object? MonsterDamage(Call c) => P(c, "MONSTER_DAMAGE", -1, c.I(0));
    [NwFunc("ItemPropertyImmunityToSpellLevel")] static object? ImmunitySpellLevel(Call c) => P(c, "IMMUNITY_SPELLS_BY_LEVEL", -1, c.I(0));
    // Walk type 0 (the default) is the zombie walk, row 1.
    [NwFunc("ItemPropertySpecialWalk")] static object? SpecialWalk(Call c) => P(c, "SPECIAL_WALK", c.I(0) == 0 ? 1 : c.I(0));
    [NwFunc("ItemPropertyHealersKit")] static object? HealersKit(Call c) => P(c, "HEALERS_KIT", -1, c.I(0));
    [NwFunc("ItemPropertyWeightIncrease")] static object? WeightIncrease(Call c) => P(c, "WEIGHT_INCREASE", -1, -1, c.I(0));
    // The level is 1-based; iprp_spellcstr rows start at level 1.
    [NwFunc("ItemPropertyOnHitCastSpell")] static object? OnHitCastSpell(Call c) => P(c, "ONHITCASTSPELL", c.I(0), c.I(1) - 1);
    [NwFunc("ItemPropertyVisualEffect")] static object? VisualEffect(Call c) => P(c, "VISUALEFFECT", c.I(0));
    [NwFunc("ItemPropertyArcaneSpellFailure")] static object? ArcaneSpellFailure(Call c) => P(c, "ARCANE_SPELL_FAILURE", -1, c.I(0));
    [NwFunc("ItemPropertyMaterial")] static object? Material(Call c) => P(c, "MATERIAL", -1, c.I(0));
    [NwFunc("ItemPropertyQuality")] static object? Quality(Call c) => P(c, "QUALITY", -1, c.I(0));
    [NwFunc("ItemPropertyAdditional")] static object? Additional(Call c) => P(c, "ADDITIONAL", -1, c.I(0));
    [NwFunc("ItemPropertyCustom")]
    static object? Custom(Call c)
    {
        var ip = ItemPropertyRules.Make(c.World, c.I(0), c.I(1, -1), c.I(2, -1), c.I(3, -1));
        if (ip.Valid) ip.Creator = c.SelfId;
        return ip;
    }

    // ------------------------------------------------------------------ getters and modifiers

    [NwFunc("GetIsItemPropertyValid")] static object? IsValid(Call c) => c.IP(0).Valid ? 1 : 0;
    [NwFunc("GetItemPropertyType")] static object? Type(Call c) => c.IP(0).Valid ? c.IP(0).Type : -1;
    [NwFunc("GetItemPropertyDurationType")] static object? DurationType(Call c) => c.IP(0).Valid ? c.IP(0).DurationType : -1;
    [NwFunc("GetItemPropertySubType")] static object? SubType(Call c) => c.IP(0).Valid ? c.IP(0).SubType : -1;
    [NwFunc("GetItemPropertyCostTable")] static object? CostTable(Call c) => c.IP(0).Valid ? c.IP(0).CostTable : -1;
    [NwFunc("GetItemPropertyCostTableValue")] static object? CostTableValue(Call c) => c.IP(0).Valid ? c.IP(0).CostTableValue : -1;
    [NwFunc("GetItemPropertyParam1")] static object? Param1(Call c) => c.IP(0).Valid ? c.IP(0).Param1 : -1;
    [NwFunc("GetItemPropertyParam1Value")] static object? Param1Value(Call c) => c.IP(0).Valid ? c.IP(0).Param1Value : -1;
    [NwFunc("GetItemPropertyTag")] static object? Tag(Call c) => c.IP(0).Tag;
    [NwFunc("TagItemProperty")]
    static object? TagProperty(Call c)
    {
        var ip = c.IP(0).Clone();
        ip.Tag = c.S(1);
        return ip;
    }
    [NwFunc("GetItemPropertyDuration")]
    static object? Duration(Call c) =>
        c.IP(0).Valid && c.IP(0).DurationType == K(c).Temporary ? (int)c.IP(0).Duration : 0;
    [NwFunc("GetItemPropertyDurationRemaining")]
    static object? DurationRemaining(Call c)
    {
        var ip = c.IP(0);
        if (!ip.Valid || ip.DurationType != K(c).Temporary) return 0;
        return ip.ExpiresAt == 0 ? (int)ip.Duration : (int)Math.Max(0, ip.ExpiresAt - c.World.Time);
    }
    [NwFunc("GetItemPropertyUsesPerDayRemaining")]
    static object? UsesPerDayRemaining(Call c)
    {
        if (c.Obj<Item>(0) is not { } item) return 0;
        var want = c.IP(1);
        var ip = item.Properties.FirstOrDefault(p => p.Id == want.Id);
        if (ip == null || ItemPropertyRules.MaxUsesPerDay(c.World, ip) == 0) return 0;
        return ip.UsesPerDay;
    }

    // ------------------------------------------------------------------ items

    [NwFunc("GetItemHasItemProperty")]
    static object? HasProperty(Call c) =>
        c.Obj<Item>(0) is { } item && item.Properties.Any(p => p.Type == c.I(1)) ? 1 : 0;

    [NwFunc("AddItemProperty")]
    static object? Add(Call c)
    {
        // The base item's itemprops.2da column must allow the property.
        if (c.Obj<Item>(2) is not { } item || !c.World.Inventory.Rules.AllowsProperty(item.BaseItem, c.IP(1).Type)) return null;
        c.World.System<EffectSystem>().AddItemProperty(item, c.IP(1), c.I(0), c.F(3));
        item.Reprice();
        // A property with uses per day starts with all of them.
        if (item.Properties.LastOrDefault(p => p.Id == c.IP(1).Id) is { } added && InventorySystem.MaxUsesPerDay(added) is var max and > 0)
            added.UsesPerDay = max;
        return null;
    }

    [NwFunc("RemoveItemProperty")]
    static object? Remove(Call c)
    {
        // Only the item's own property goes (got from iterating it, or the variable that was added), not an equal one built separately.
        var want = c.IP(1);
        if (c.Obj<Item>(0) is not { } item || item.Properties.FirstOrDefault(p => p.Id == want.Id) is not { } ip) return null;
        int before = c.World.Inventory.Rules.GoldValue(item);
        if (!c.World.System<EffectSystem>().RemoveItemProperty(item, ip)) return null;
        bool sameTick = item.HeldAt == c.World.Time;
        int? held = item.HeldValue is { } h && sameTick ? h : before;
        var removed = sameTick && item.RemovedProperties != null ? item.RemovedProperties : new List<ItemProperty>();
        removed.Add(ip);
        item.Reprice();
        item.HeldValue = held;
        item.HeldAt = c.World.Time;
        item.RemovedProperties = removed;
        return null;
    }

    /// <summary>One cursor per (script thread, item); iterates a snapshot so removing while iterating is safe.</summary>
    static readonly ConditionalWeakTable<ScriptThread, Dictionary<uint, (List<ItemProperty> List, int Index)>> _cursors = new();

    [NwFunc("GetFirstItemProperty")]
    static object? First(Call c)
    {
        if (c.Obj<Item>(0) is not { } item) return new ItemProperty();
        var list = item.Properties.ToList();
        var map = _cursors.GetOrCreateValue(c.Thread);
        return Advance(item, map, list, 0);
    }

    [NwFunc("GetNextItemProperty")]
    static object? Next(Call c)
    {
        if (c.Obj<Item>(0) is not { } item || !_cursors.TryGetValue(c.Thread, out var map) || !map.TryGetValue(item.Id, out var cur))
            return new ItemProperty();
        return Advance(item, map, cur.List, cur.Index + 1);
    }

    static ItemProperty Advance(Item item, Dictionary<uint, (List<ItemProperty>, int)> map, List<ItemProperty> list, int i)
    {
        while (i < list.Count && !item.Properties.Contains(list[i])) i++;
        map[item.Id] = (list, i);
        return i < list.Count ? list[i] : new ItemProperty();
    }
}
