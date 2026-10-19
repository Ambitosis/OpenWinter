using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Creature statistics: hit points, abilities, armour class, attack, saves, alignment, classes, XP and limits.</summary>
static class CreatureStatFunctions
{
    static Creature? Cr(Call c, int i) => c.Obj<Creature>(i);

    // ---------------------------------------------------------------- hit points

    [NwFunc("GetCurrentHitPoints")] static object? CurrentHP(Call c) => c.Obj(0) switch
    {
        Creature cr => CreatureRules.CurrentHitPoints(c.World, cr) + CreatureRules.TemporaryHitPoints(cr),
        Door d => d.CurrentHitPoints,
        Placeable p => p.CurrentHitPoints,
        Sound => 1, // measured (audio_scripts)
        _ => 0,
    };

    [NwFunc("GetMaxHitPoints")] static object? MaxHP(Call c)
    {
        switch (c.Obj(0))
        {
            case Creature cr:
                if (!c.World.System<EffectSystem>().HitPointsPending(cr)) CreatureRules.SyncHitPoints(c.World, cr);
                return cr.MaxHitPoints;
            case Door d: return d.HitPoints;
            case Placeable p: return p.HitPoints;
            case Sound: return 1;
            default: return 0;
        }
    }

    [NwFunc("SetCurrentHitPoints")] static object? SetCurrentHP(Call c)
    {
        int hp = c.I(1);
        if (c.Obj(0) is Creature init) CreatureRules.EnsureInit(c.World, init);
        switch (c.Obj(0))
        {
            case Creature cr when !cr.IsDead:
                if (cr.CurrentHitPoints <= 0)
                {
                    if (cr.IsPC) cr.CurrentHitPoints = Math.Clamp(hp, -9, 0); // dying player
                }
                else cr.CurrentHitPoints = Math.Clamp(hp, 1, CreatureRules.MaxHitPoints(c.World, cr));
                c.World.MarkChanged(cr);
                break;
            case Door d when d.CurrentHitPoints > 0:
                d.CurrentHitPoints = Math.Clamp(hp, 1, Math.Max(1, d.HitPoints));
                break;
            case Placeable p when p.CurrentHitPoints > 0:
                p.CurrentHitPoints = Math.Clamp(hp, 1, Math.Max(1, p.HitPoints));
                break;
        }
        return null;
    }

    // A door reads dead by its hit points: killed by damage yes, by EffectDeath no; an object that is gone reads dead
    // (nwserver, door_bashed: ED_0, EK_0, and UK after it was removed).
    [NwFunc("GetIsDead")] static object? IsDead(Call c) => c.Obj(0) switch
    {
        null => 1,
        Door d => d.CurrentHitPoints <= 0 ? 1 : 0,
        _ => Cr(c, 0) is { } cr && (cr.IsDead || cr.CurrentHitPoints <= 0) ? 1 : 0,
    };

    [NwFunc("GetLastDamager")] static object? LastDamager(Call c) => c.Obj(0)?.LastDamager ?? GameObject.InvalidId;

    [NwFunc("GetDamageDealtByType")] static object? DamageByType(Call c)
    {
        if (c.Self is not { } self) return -1;
        int mask = c.I(0), total = 0;
        bool any = false;
        foreach (var (type, amount) in self.LastDamageByType)
            if ((type & mask) != 0 || type == mask) { total += amount; any = true; }
        return any ? total : -1;
    }

    [NwFunc("GetTotalDamageDealt")] static object? TotalDamage(Call c) => c.Self?.LastDamageByType.Values.Sum() ?? 0;

    // ---------------------------------------------------------------- abilities

    [NwFunc("GetAbilityScore")] static object? AbilityScore(Call c) =>
        Cr(c, 0) is { } cr ? CreatureRules.AbilityScore(c.World, cr, c.I(1), c.I(2) != 0) : 0;

    [NwFunc("GetAbilityModifier")] static object? AbilityModifier(Call c) =>
        Cr(c, 1) is { } cr && (uint)c.I(0) <= 5 ? CreatureRules.AbilityModifier(c.World, cr, c.I(0)) : 0;

    // ---------------------------------------------------------------- armour class and attack

    [NwFunc("GetAC")] static object? AC(Call c) => c.Obj(0) switch
    {
        // The original leaves the size modifier out of GetAC (combat rolls still apply it); parity lab, basics suite.
        Creature cr => CreatureRules.ArmorClass(c.World, cr) is var ac ? ac.Total - ac.Size : 0,
        Item or Door or Placeable => 0,
        _ => -1,
    };

    [NwFunc("GetBaseAttackBonus")] static object? BAB(Call c) => Cr(c, 0) is { } cr ? CreatureRules.BaseAttackBonus(c.World, cr) : 0;

    [NwFunc("GetAttacksPerRound")] static object? Attacks(Call c) => Cr(c, 0) is { } cr ? CreatureRules.AttacksPerRound(c.World, cr, c.I(1) != 0) : 0;

    [NwFunc("SetBaseAttackBonus")] static object? SetAttacks(Call c)
    {
        if (Cr(c, 1) is { } cr) { CreatureRules.EnsureInit(c.World, cr); cr.BaseAttackOverride = Math.Clamp(c.I(0), 1, 6); }
        return null;
    }

    [NwFunc("RestoreBaseAttackBonus")] static object? RestoreAttacks(Call c)
    {
        if (Cr(c, 0) is { } cr) { CreatureRules.EnsureInit(c.World, cr); cr.BaseAttackOverride = -1; }
        return null;
    }

    [NwFunc("GetSpellResistance")] static object? SR(Call c) => Cr(c, 0) is { } cr ? CreatureRules.SpellResistance(c.World, cr) : 0;
    [NwFunc("GetArcaneSpellFailure")] static object? ASF(Call c) => Cr(c, 0) is { } cr ? CreatureRules.ArcaneSpellFailure(c.World, cr) : 0;

    [NwFunc("GetTurnResistanceHD")] static object? TurnResistance(Call c)
    {
        if (Cr(c, 0) is not { } cr) return 0;
        int tr = 0;
        foreach (var e in CreatureRules.Effects(cr))
        {
            if (e.Type == 78) tr += e.Ints[0];      // EFFECT_TYPE_TURN_RESISTANCE_INCREASE
            else if (e.Type == 77) tr -= e.Ints[0]; // EFFECT_TYPE_TURN_RESISTANCE_DECREASE
        }
        foreach (var (_, _, p) in CreatureRules.EquippedProperties(cr))
            if (p.Type == 73) tr += CreatureRules.PropertyValue(c.World, p); // ITEM_PROPERTY_TURN_RESISTANCE
        return tr;
    }

    [NwFunc("GetWeight")] static object? Weight(Call c) => c.Obj(0) switch
    {
        Item i => CreatureRules.ItemWeight(c.World, i),
        // The original only keeps a carried weight for player characters.
        Creature cr => cr.IsPC ? CreatureRules.CarriedWeight(c.World, cr) : 0,
        _ => 0,
    };

    // ---------------------------------------------------------------- saving throws

    [NwFunc("GetFortitudeSavingThrow")] static object? FortSave(Call c) => c.Obj(0) is { } o ? CreatureRules.SavingThrow(c.World, o, NwRules.SaveFort) : 0;
    [NwFunc("GetReflexSavingThrow")] static object? RefSave(Call c) => c.Obj(0) is { } o ? CreatureRules.SavingThrow(c.World, o, NwRules.SaveReflex) : 0;
    [NwFunc("GetWillSavingThrow")] static object? WillSave(Call c) => c.Obj(0) is { } o ? CreatureRules.SavingThrow(c.World, o, NwRules.SaveWill) : 0;

    [NwFunc("SetFortitudeSavingThrow")] static object? SetFort(Call c) { if (c.Obj(0) is { } o) CreatureRules.SetStaticSave(o, NwRules.SaveFort, c.I(1)); return null; }
    [NwFunc("SetReflexSavingThrow")] static object? SetRef(Call c) { if (c.Obj(0) is { } o) CreatureRules.SetStaticSave(o, NwRules.SaveReflex, c.I(1)); return null; }
    [NwFunc("SetWillSavingThrow")] static object? SetWill(Call c) { if (c.Obj(0) is { } o) CreatureRules.SetStaticSave(o, NwRules.SaveWill, c.I(1)); return null; }

    static object? Roll(Call c, int save)
    {
        // object oCreature, int nDC, int nSaveType, object oSaveVersus
        var target = c.Obj(0);
        if (target is not (Creature or Door or Placeable)) return 0;
        return CreatureRules.RollSave(c.World, target, save, c.I(1), c.I(2), c.Obj(3));
    }

    [NwFunc("FortitudeSave")] static object? FortitudeSave(Call c) => Roll(c, NwRules.SaveFort);
    [NwFunc("ReflexSave")] static object? ReflexSave(Call c) => Roll(c, NwRules.SaveReflex);
    [NwFunc("WillSave")] static object? WillSaveRoll(Call c) => Roll(c, NwRules.SaveWill);

    [NwFunc("GetIsImmune")] static object? IsImmune(Call c) =>
        Cr(c, 0) is { } cr && CreatureRules.IsImmune(c.World, cr, c.I(1), c.Obj(2)) ? 1 : 0;

    // ---------------------------------------------------------------- spell context

    [NwFunc("GetCasterLevel")] static object? CasterLevel(Call c) => c.Obj(0)?.LastSpell?.CasterLevel ?? 0;
    [NwFunc("GetMetaMagicFeat")] static object? MetaMagic(Call c) => c.Self is { } s ? s.LastSpell?.MetaMagic ?? 0 : -1;
    [NwFunc("GetSpellFeatId")] static object? SpellFeat(Call c) => c.Self?.LastSpell?.FeatId ?? -1;
    [NwFunc("GetSpellSaveDC")] static object? SpellSaveDC(Call c) => c.Self is { } s ? CreatureRules.SpellSaveDC(c.World, s) : 0;

    // ---------------------------------------------------------------- alignment

    [NwFunc("GetAlignmentLawChaos")] static object? AlignLC(Call c) => Cr(c, 0) is { } cr ? CreatureRules.LawChaos(cr.LawfulChaotic) : -1;
    [NwFunc("GetAlignmentGoodEvil")] static object? AlignGE(Call c) => Cr(c, 0) is { } cr ? CreatureRules.GoodEvil(cr.GoodEvil) : -1;
    [NwFunc("GetLawChaosValue")] static object? LCValue(Call c) => Cr(c, 0)?.LawfulChaotic ?? -1;
    [NwFunc("GetGoodEvilValue")] static object? GEValue(Call c) => Cr(c, 0)?.GoodEvil ?? -1;

    [NwFunc("AdjustAlignment")] static object? AdjustAlignment(Call c)
    {
        if (Cr(c, 0) is { } cr) CreatureRules.AdjustAlignment(c.World, cr, c.I(1), c.I(2), c.I(3) != 0);
        return null;
    }

    // ---------------------------------------------------------------- classes, race, levels

    [NwFunc("GetHitDice")] static object? HitDice(Call c) => Cr(c, 0)?.TotalLevel ?? 0;

    [NwFunc("GetClassByPosition")] static object? ClassByPosition(Call c)
    {
        int pos = c.I(0);
        return Cr(c, 1) is { } cr && pos >= 1 && pos <= cr.Classes.Count ? cr.Classes[pos - 1].Class : NwRules.ClassInvalid;
    }

    [NwFunc("GetLevelByPosition")] static object? LevelByPosition(Call c)
    {
        int pos = c.I(0);
        return Cr(c, 1) is { } cr && pos >= 1 && pos <= cr.Classes.Count ? cr.Classes[pos - 1].Level : 0;
    }

    [NwFunc("GetLevelByClass")] static object? LevelByClass(Call c) => Cr(c, 1) is { } cr ? CreatureRules.ClassLevel(cr, c.I(0)) : 0;

    [NwFunc("GetRacialType")] static object? Race(Call c) => Cr(c, 0) is { } cr ? (int)cr.Race : NwRules.RacialTypeInvalid;

    [NwFunc("GetIsPlayableRacialType")] static object? Playable(Call c) =>
        Cr(c, 0) is { } cr && c.World.Resources.TwoDA("racialtypes").GetInt(cr.Race, "PlayerRace") != 0 ? 1 : 0;

    [NwFunc("GetCreatureSize")] static object? Size(Call c) => Cr(c, 0) is { } cr ? CreatureRules.CreatureSize(c.World, cr) : 0;
    [NwFunc("GetChallengeRating")] static object? CR(Call c) => Cr(c, 0)?.ChallengeRating ?? 0f;
    [NwFunc("GetMovementRate")] static object? MovementRate(Call c) => Cr(c, 0)?.MovementRateIndex ?? 0;

    [NwFunc("GetSpecialization")] static object? Specialization(Call c)
    {
        if (Cr(c, 0) is not { } cr) return -1;
        var cc = cr.Classes.FirstOrDefault(x => x.Class == c.I(1));
        if (cc == null) return -1;
        if (cc.School >= 0) return cc.School;
        return c.World.Resources.TwoDA("classes").GetInt(cc.Class, "PickSchool") != 0 ? 0 : -1;
    }

    [NwFunc("GetDomain")] static object? Domain(Call c)
    {
        if (Cr(c, 0) is not { } cr) return -1;
        var cc = cr.Classes.FirstOrDefault(x => x.Class == c.I(2));
        if (cc == null) return -1;
        // GetDomain reports native defaults even for a class with no domain
        // selection. This does not grant that class domain spell access.
        return c.I(1) switch { 1 => cc.Domain1 < 0 ? 0 : cc.Domain1, 2 => cc.Domain2 < 0 ? 1 : cc.Domain2, _ => -1 };
    }

    // ---------------------------------------------------------------- experience

    [NwFunc("GetXP")] static object? GetXP(Call c) => Cr(c, 0)?.Experience ?? 0;

    [NwFunc("SetXP")] static object? SetXP(Call c)
    {
        if (Cr(c, 0) is { } cr) CreatureRules.SetXP(c.World, cr, c.I(1));
        return null;
    }

    [NwFunc("GiveXPToCreature")] static object? GiveXP(Call c)
    {
        if (Cr(c, 0) is { } cr) CreatureRules.GiveXP(c.World, cr, c.I(1), multiclassPenalty: true);
        return null;
    }

    [NwFunc("GetModuleXPScale")] static object? XPScale(Call c) => c.World.XPScale;
    [NwFunc("SetModuleXPScale")] static object? SetXPScale(Call c)
    {
        c.World.XPScale = Math.Clamp(c.I(0), 0, 200); // out-of-range values clamp, as in the original
        return null;
    }

    // ---------------------------------------------------------------- bonus limits

    static int Limit(Call c) => Math.Clamp(c.I(0), 0, 255);

    [NwFunc("GetAbilityBonusLimit")] static object? AbilityBonusLimit(Call c) => c.World.Limits.AbilityBonus;
    [NwFunc("GetAbilityPenaltyLimit")] static object? AbilityPenaltyLimit(Call c) => c.World.Limits.AbilityPenalty;
    [NwFunc("GetAttackBonusLimit")] static object? AttackBonusLimit(Call c) => c.World.Limits.AttackBonus;
    [NwFunc("GetDamageBonusLimit")] static object? DamageBonusLimit(Call c) => c.World.Limits.DamageBonus;
    [NwFunc("GetSavingThrowBonusLimit")] static object? SaveBonusLimit(Call c) => c.World.Limits.SavingThrowBonus;
    [NwFunc("GetSkillBonusLimit")] static object? SkillBonusLimit(Call c) => c.World.Limits.SkillBonus;
    [NwFunc("SetAbilityBonusLimit")] static object? SetAbilityBonusLimit(Call c) { c.World.Limits.AbilityBonus = Limit(c); return null; }
    [NwFunc("SetAbilityPenaltyLimit")] static object? SetAbilityPenaltyLimit(Call c) { c.World.Limits.AbilityPenalty = Limit(c); return null; }
    [NwFunc("SetAttackBonusLimit")] static object? SetAttackBonusLimit(Call c) { c.World.Limits.AttackBonus = Limit(c); return null; }
    [NwFunc("SetDamageBonusLimit")] static object? SetDamageBonusLimit(Call c) { c.World.Limits.DamageBonus = Limit(c); return null; }
    [NwFunc("SetSavingThrowBonusLimit")] static object? SetSaveBonusLimit(Call c) { c.World.Limits.SavingThrowBonus = Limit(c); return null; }
    [NwFunc("SetSkillBonusLimit")] static object? SetSkillBonusLimit(Call c) { c.World.Limits.SkillBonus = Limit(c); return null; }
}
