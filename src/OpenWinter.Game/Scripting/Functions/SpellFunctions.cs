using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Spells;
using OpenWinter.Game.Skills;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Spell casting actions, the spell context getters, spell resistance/immunity/absorption, resting and talents.</summary>
static class SpellFunctions
{
    static SpellSystem Spells(Call c) => c.World.System<SpellSystem>();

    static void Enqueue(Call c, GameAction a)
    {
        if (c.Self is { Commandable: true } o) o.Actions.Add(a);
    }

    /// <summary>The spell context of OBJECT_SELF: an area of effect's spell inside its scripts, else the caller's current/last spell.</summary>
    static SpellCastInfo? Info(Call c) => c.Self switch
    {
        AreaOfEffect aoe => aoe.LastSpell,
        { } o => o.LastSpell,
        _ => null,
    };

    // ------------------------------------------------------------------ spell context

    [NwFunc("GetSpellId")]
    static object? SpellId(Call c)
    {
        if (c.Event.Values.ContainsKey("SpellId") && c.Self is not AreaOfEffect) return c.Event.Int("SpellId");
        if (c.Self is AreaOfEffect aoe) return aoe.SpellId;
        return c.Self?.LastSpell?.SpellId ?? -1;
    }

    [NwFunc("GetSpellTargetObject")]
    static object? SpellTarget(Call c)
    {
        uint t = Info(c)?.Target ?? GameObject.InvalidId;
        return c.World.Get(t) != null ? t : GameObject.InvalidId;
    }

    [NwFunc("GetSpellTargetLocation")]
    static object? SpellTargetLocation(Call c)
    {
        var info = Info(c);
        if (info == null) return Location.Invalid;
        // A spell cast at an object aims where the object is now.
        if (c.World.Get(info.Target) is { Area: not null } t) return t.Location;
        return info.TargetLocation;
    }

    [NwFunc("GetLastSpellCastClass")] static object? CastClass(Call c) => Info(c)?.ClassType ?? NwRules.ClassInvalid;
    [NwFunc("GetLastSpellLevel")] static object? SpellLevel(Call c) => Math.Max(0, Info(c)?.SpellLevel ?? 0);
    [NwFunc("GetSpellCastSpontaneously")] static object? Spontaneous(Call c) => Info(c)?.Spontaneous == true ? 1 : 0;

    // ------------------------------------------------------------------ casting actions

    [NwFunc("ActionCastSpellAtObject")]
    static object? CastAtObject(Call c)
    {
        // nSpell, oTarget, nMetaMagic, bCheat, nDomainLevel, nProjectilePathType, bInstantSpell, nClass, bSpontaneousCast
        if (c.Obj(1) == null) return null;
        // A path type above PROJECTILE_PATH_TYPE_ACCELERATING (4) makes the command do nothing (projectile_paths).
        if (c.I(5) > SpellTable.MaxScriptProjectilePath) return null;
        Enqueue(c, new CastSpellAction
        {
            Spell = c.I(0), Target = c.O(1), MetaMagic = c.I(2, SpellSlots.MetaAny), Cheat = c.I(3) != 0, DomainLevel = c.I(4),
            ProjectilePathType = c.I(5), Instant = c.I(6) != 0, Class = c.I(7, -1), Spontaneous = c.I(8) != 0,
        });
        return null;
    }

    [NwFunc("ActionCastSpellAtLocation")]
    static object? CastAtLocation(Call c)
    {
        // nSpell, lTargetLocation, nMetaMagic, bCheat, nProjectilePathType, bInstantSpell, nClass, bSpontaneousCast, nDomainLevel
        var loc = c.L(1);
        if (c.World.Get(loc.Area) == null) return null;
        if (c.I(4) > SpellTable.MaxScriptProjectilePath) return null;
        Enqueue(c, new CastSpellAction
        {
            Spell = c.I(0), TargetLocation = loc, MetaMagic = c.I(2, SpellSlots.MetaAny), Cheat = c.I(3) != 0,
            ProjectilePathType = c.I(4), Instant = c.I(5) != 0, Class = c.I(6, -1), Spontaneous = c.I(7) != 0, DomainLevel = c.I(8),
        });
        return null;
    }

    [NwFunc("ActionCastFakeSpellAtObject")]
    static object? FakeAtObject(Call c)
    {
        if (c.Obj(1) == null) return null;
        Enqueue(c, new CastSpellAction { Spell = c.I(0), Target = c.O(1), ProjectilePathType = c.I(2), Fake = true, Cheat = true });
        return null;
    }

    [NwFunc("ActionCastFakeSpellAtLocation")]
    static object? FakeAtLocation(Call c)
    {
        var loc = c.L(1);
        if (c.World.Get(loc.Area) == null) return null;
        Enqueue(c, new CastSpellAction { Spell = c.I(0), TargetLocation = loc, ProjectilePathType = c.I(2), Fake = true, Cheat = true });
        return null;
    }

    [NwFunc("ActionCounterSpell")]
    static object? CounterSpell(Call c)
    {
        if (c.Self is Creature && c.Obj(0) is { } t) Enqueue(c, new CounterSpellAction { Target = t.Id });
        return null;
    }

    [NwFunc("ActionUseTalentOnObject")]
    static object? TalentOnObject(Call c)
    {
        if (c.Obj(1) is { } t) UseTalent(c, c.T(0), t.Id, Location.Invalid);
        return null;
    }

    [NwFunc("ActionUseTalentAtLocation")]
    static object? TalentAtLocation(Call c)
    {
        var loc = c.L(1);
        if (c.World.Get(loc.Area) != null) UseTalent(c, c.T(0), GameObject.InvalidId, loc);
        return null;
    }

    static void UseTalent(Call c, Talent t, uint target, Location loc)
    {
        if (!t.Valid || c.Self is not Creature self) return;
        switch (t.Type)
        {
            case NwRules.TalentSpell when t.Item != GameObject.InvalidId && t.Property != null:
                // A potion talent drinks that potion.
                Enqueue(c, new Inventory.UseItemAction { ItemId = t.Item, Property = t.Property, Target = target, TargetLocation = loc });
                break;
            case NwRules.TalentSpell:
                // Talents list every prepared spell, domain slots included; a domain-only spell
                // needs its domain level to find that slot.
                Enqueue(c, new CastSpellAction
                {
                    Spell = t.Id, Target = target, TargetLocation = loc,
                    DomainLevel = SpellSlots.InferDomainLevel(c.World, self, t.Id, SpellSlots.MetaAny, -1)
                });
                break;
            case NwRules.TalentFeat:
                if (self.Commandable) UseFeatAction.Order(c.World, self, t.Id, 0, target, loc);
                break;
            case NwRules.TalentSkill:
                // Native object talents support these skill routes. Location talents and
                // OpenLock/Heal/Spot/Listen controls do not invoke the corresponding skill.
                if (target == GameObject.InvalidId || SkillFunctions.ApplyMode(c, t.Id)) break;
                if (t.Id == SkillIds.PickPocket)
                    Enqueue(c, new UseSkillAction { Skill = t.Id, Target = target, Range = UseSkillAction.RangeFor(t.Id) });
                break;
        }
    }

    [NwFunc("ActionRest")]
    static object? Rest(Call c)
    {
        if (c.Self is Creature) Enqueue(c, new RestAction { LineOfSightCheck = c.I(0) != 0 });
        return null;
    }

    [NwFunc("SetSpellTargetingData")]
    static object? SetTargeting(Call c)
    {
        if (c.Obj<Creature>(0) is { IsPC: true } pc) Spells(c).SetTargeting(pc, c.I(1), c.I(2), c.F(3), c.F(4), c.I(5));
        return null;
    }

    // ------------------------------------------------------------------ resistance

    [NwFunc("ResistSpell")]
    static object? ResistSpell(Call c)
    {
        if (c.Obj(0) is not { } caster || c.Obj(1) is not { } target) return 0;
        return SpellDefense.Resist(c.World, caster, target, SpellDefense.InfoFor(c.Self, caster));
    }

    /// <summary>The spell id argument, or the caster's current spell when -1.</summary>
    static int SpellArg(Call c, GameObject? caster, int index)
    {
        int spell = c.I(index, -1);
        if (spell >= 0 || caster == null) return spell;
        return SpellDefense.InfoFor(c.Self, caster)?.SpellId ?? -1;
    }

    [NwFunc("SpellResistanceCheck")]
    static object? ResistanceCheck(Call c)
    {
        // oTarget, oCaster, nSpellId, nCasterLevel, nSpellResistance, bFeedback
        if (c.Obj(0) is not { } target || c.Obj(1) is not { } caster) return 0;
        var info = SpellDefense.InfoFor(c.Self, caster);
        int level = c.I(3, -1);
        if (level < 0) level = info?.CasterLevel ?? (caster is Creature cr ? SpellSlots.CheatCasterLevel(c.World, cr, SpellArg(c, caster, 2)) : 0);
        return SpellDefense.ResistanceRoll(c.World, target, caster, level, c.I(4, -1), c.I(5, 1) != 0) ? 1 : 0;
    }

    [NwFunc("SpellImmunityCheck")]
    static object? ImmunityCheck(Call c)
    {
        // oTarget, oCaster, nSpellId, bFeedback
        if (c.Obj(0) is not { } target || c.Obj(1) is not { } caster) return 0;
        int spell = SpellArg(c, caster, 2);
        if (spell < 0) return 0;
        int level = SpellDefense.LevelOf(c.World, SpellDefense.InfoFor(c.Self, caster), spell);
        return SpellDefense.Immune(c.World, target, caster, spell, SpellTable.School(c.World, spell), level, c.I(3, 1) != 0) ? 1 : 0;
    }

    static object? Absorption(Call c, bool limited)
    {
        // oTarget, oCaster, nSpellId, nSpellSchool, nSpellLevel, [bRemoveLevels], bFeedback
        if (c.Obj(0) is not { } target || c.Obj(1) is not { } caster) return 0;
        int spell = SpellArg(c, caster, 2);
        int school = c.I(3, -1);
        if (school < 0) school = spell >= 0 ? SpellTable.School(c.World, spell) : SpellTable.SchoolGeneral;
        int level = c.I(4, -1);
        if (level < 0)
        {
            if (spell < 0) return 0;
            level = SpellDefense.LevelOf(c.World, SpellDefense.InfoFor(c.Self, caster), spell);
        }
        bool remove = limited && c.I(5, 1) != 0;
        bool feedback = limited ? c.I(6, 1) != 0 : c.I(5) != 0;
        return SpellDefense.Absorb(c.World, target, caster, spell, school, level, limited, remove, feedback) ? 1 : 0;
    }

    [NwFunc("SpellAbsorptionLimitedCheck")] static object? AbsorbLimited(Call c) => Absorption(c, limited: true);
    [NwFunc("SpellAbsorptionUnlimitedCheck")] static object? AbsorbUnlimited(Call c) => Absorption(c, limited: false);

    [NwFunc("GetReflexAdjustedDamage")]
    static object? ReflexAdjusted(Call c)
    {
        // nDamage, oTarget, nDC, nSaveType, oSaveVersus
        if (c.Obj(1) is not { } target) return c.I(0);
        return SpellDefense.ReflexAdjustedDamage(c.World, c.I(0), target, c.I(2), c.I(3), c.Obj(4));
    }
}
