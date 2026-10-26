using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Feats, skills, spells (known, memorised, spell-like abilities) and talents.</summary>
static class CreatureAbilityFunctions
{
    static Creature? Cr(Call c, int i) => c.Obj<Creature>(i);

    // ---------------------------------------------------------------- feats

    [NwFunc("GetHasFeat")]
    static object? HasFeat(Call c) =>
        Cr(c, 1) is { } cr && CreatureRules.HasFeat(c.World, cr, c.I(0), c.I(2) != 0) ? 1 : 0;

    [NwFunc("GetFeatRemainingUses")]
    static object? FeatUses(Call c) =>
        Cr(c, 1) is { } cr ? CreatureRules.RemainingFeatUses(c.World, cr, c.I(0)) : 0;

    [NwFunc("DecrementRemainingFeatUses")]
    static object? DecFeat(Call c)
    {
        if (Cr(c, 0) is { } cr) CreatureRules.AdjustFeatUses(c.World, cr, c.I(1), -1);
        return null;
    }

    [NwFunc("IncrementRemainingFeatUses")]
    static object? IncFeat(Call c)
    {
        if (Cr(c, 0) is { } cr) CreatureRules.AdjustFeatUses(c.World, cr, c.I(1), +1);
        return null;
    }

    [NwFunc("GetHasFeatEffect")]
    static object? HasFeatEffect(Call c)
    {
        if (c.Obj(1) is not { } o) return 0;
        int spell = c.World.Resources.TwoDA("feat").GetInt(c.I(0), "SPELLID", -1);
        return spell >= 0 && CreatureRules.Effects(o).Any(e => e.SpellId == spell) ? 1 : 0;
    }

    [NwFunc("ActionUseFeat")]
    static object? UseFeat(Call c)
    {
        // int nFeat, object oTarget, int nSubFeat, location lTarget
        if (c.Self is Creature self && self.Commandable)
            UseFeatAction.Order(c.World, self, c.I(0), c.I(2), c.O(1), c.L(3));
        return null;
    }

    // ---------------------------------------------------------------- skills

    /// <summary>Only skills one of the creature's classes can take count, even with ranks (a fighter has no Perform), as in the original.</summary>
    [NwFunc("GetHasSkill")]
    static object? HasSkill(Call c) =>
        Cr(c, 1) is { } cr && CreatureRules.HasSkill(c.World, cr, c.I(0))
        && cr.Classes.Any(k => k.Level > 0 && Progression.ProgressionRules.CanBuySkill(c.World, k.Class, c.I(0))) ? 1 : 0;

    // A skill the creature can't use (untrained-only with no ranks) reads 0 in the original, not -1.
    [NwFunc("GetSkillRank")]
    static object? SkillRank(Call c) => Cr(c, 1) is { } cr
        ? CreatureRules.HasSkill(c.World, cr, c.I(0)) || c.I(2) != 0 ? CreatureRules.SkillRank(c.World, cr, c.I(0), c.I(2) != 0) : 0
        : -1;

    [NwFunc("GetIsSkillSuccessful")]
    static object? SkillCheck(Call c) =>
        Cr(c, 0) is { } cr && CreatureRules.SkillCheck(c.World, cr, c.I(1), c.I(2)) ? 1 : 0;

    // ---------------------------------------------------------------- spells

    [NwFunc("GetHasSpell")] static object? HasSpell(Call c) => Cr(c, 1) is { } cr ? CreatureRules.SpellCount(c.World, cr, c.I(0)) : 0;

    [NwFunc("DecrementRemainingSpellUses")]
    static object? DecSpell(Call c)
    {
        if (Cr(c, 0) is { } cr) CreatureRules.SpendSpell(c.World, cr, c.I(1));
        return null;
    }

    [NwFunc("GetSpellLevelByClass")] static object? SpellLevel(Call c) => CreatureRules.SpellLevelForClass(c.World, c.I(0), c.I(1));

    [NwFunc("GetSpellUsesLeft")]
    static object? SpellUsesLeft(Call c) =>
        Cr(c, 0) is { } cr ? CreatureRules.SpellUsesLeft(c.World, cr, c.I(1), c.I(2), c.I(3), c.I(4)) : 0;

    [NwFunc("ReadySpellLevel")]
    static object? ReadyLevel(Call c)
    {
        if (Cr(c, 0) is { } cr) CreatureRules.ReadySpellLevel(c.World, cr, c.I(1), c.I(2, NwRules.ClassInvalid), c.I(3, 255));
        return null;
    }

    static CreatureClass? Class(Call c, Creature cr, int cls) => cr.Classes.FirstOrDefault(x => x.Class == cls);

    [NwFunc("GetKnownSpellCount")]
    static object? KnownCount(Call c)
    {
        int lvl = c.I(2);
        return Cr(c, 0) is { } cr && Class(c, cr, c.I(1)) is { } cc && lvl >= 0 && lvl < 10 ? cc.KnownSpells[lvl].Count : 0;
    }

    [NwFunc("GetKnownSpellId")]
    static object? KnownId(Call c)
    {
        int lvl = c.I(2), idx = c.I(3);
        if (Cr(c, 0) is not { } cr || Class(c, cr, c.I(1)) is not { } cc || lvl < 0 || lvl > 9) return -1;
        return idx >= 0 && idx < cc.KnownSpells[lvl].Count ? cc.KnownSpells[lvl][idx] : -1;
    }

    [NwFunc("GetIsInKnownSpellList")]
    static object? InKnownList(Call c) =>
        Cr(c, 0) is { } cr && Class(c, cr, c.I(1)) is { } cc && cc.KnownSpells.Any(l => l.Contains(c.I(2))) ? 1 : 0;

    [NwFunc("GetMemorizedSpellCountByLevel")]
    static object? MemCount(Call c) =>
        Cr(c, 0) is { } cr ? CreatureRules.MemorizedSlotCount(c.World, cr, c.I(1), c.I(2)) : 0;

    /// <summary>A memorised slot (creature, class, level, index args), or null when the slot doesn't exist.</summary>
    static (Creature Creature, int ClassIndex, int Level, int Index, (int Spell, int Meta, bool Ready) Slot)? Slot(Call c)
    {
        if (Cr(c, 0) is not { } cr) return null;
        int cls = c.I(1), lvl = c.I(2), idx = c.I(3);
        int ci = CreatureRules.ClassIndex(cr, cls);
        if (ci < 0 || lvl < 0 || lvl > 9 || idx < 0 || idx >= CreatureRules.MemorizedSlotCount(c.World, cr, cls, lvl)) return null;
        var list = cr.Classes[ci].Memorized[lvl];
        return (cr, ci, lvl, idx, idx < list.Count ? list[idx] : (-1, 0, false));
    }

    [NwFunc("GetMemorizedSpellId")] static object? MemId(Call c) => Slot(c) is { } s && s.Slot.Spell >= 0 ? s.Slot.Spell : -1;
    [NwFunc("GetMemorizedSpellReady")] static object? MemReady(Call c) => Slot(c) is { } s && s.Slot.Spell >= 0 ? (s.Slot.Ready ? 1 : 0) : -1;
    [NwFunc("GetMemorizedSpellMetaMagic")] static object? MemMeta(Call c) => Slot(c) is { } s && s.Slot.Spell >= 0 ? s.Slot.Meta : -1;
    [NwFunc("GetMemorizedSpellIsDomainSpell")]
    static object? MemDomain(Call c) =>
        Slot(c) is { } s && s.Slot.Spell >= 0 ? (s.Creature.DomainSlots.Contains((s.ClassIndex, s.Level, s.Index)) ? 1 : 0) : -1;

    [NwFunc("SetMemorizedSpell")]
    static object? SetMem(Call c)
    {
        // object oCreature, int nClassType, int nSpellLevel, int nIndex, int nSpellId, int bReady, int nMetaMagic, int bIsDomainSpell
        if (Cr(c, 0) is not { } cr || !CreatureRules.EnsureSlot(c.World, cr, c.I(1), c.I(2), c.I(3))) return null;
        int ci = CreatureRules.ClassIndex(cr, c.I(1));
        cr.Classes[ci].Memorized[c.I(2)][c.I(3)] = (c.I(4), c.I(6), c.I(5) != 0);
        var key = (ci, c.I(2), c.I(3));
        if (c.I(7) != 0) cr.DomainSlots.Add(key); else cr.DomainSlots.Remove(key);
        return null;
    }

    [NwFunc("SetMemorizedSpellReady")]
    static object? SetMemReady(Call c)
    {
        if (Slot(c) is not { } s || s.Slot.Spell < 0) return null;
        var list = s.Creature.Classes[s.ClassIndex].Memorized[s.Level];
        list[s.Index] = (s.Slot.Spell, s.Slot.Meta, c.I(4) != 0);
        return null;
    }

    [NwFunc("ClearMemorizedSpell")]
    static object? ClearMem(Call c)
    {
        if (Slot(c) is not { } s) return null;
        var list = s.Creature.Classes[s.ClassIndex].Memorized[s.Level];
        if (s.Index < list.Count) list[s.Index] = (-1, 0, false);
        s.Creature.DomainSlots.Remove((s.ClassIndex, s.Level, s.Index));
        return null;
    }

    [NwFunc("ClearMemorizedSpellBySpellId")]
    static object? ClearMemById(Call c)
    {
        if (Cr(c, 0) is not { } cr) return null;
        int ci = CreatureRules.ClassIndex(cr, c.I(1));
        if (ci < 0) return null;
        for (int lvl = 0; lvl < 10; lvl++)
        {
            var list = cr.Classes[ci].Memorized[lvl];
            for (int i = 0; i < list.Count; i++)
                if (list[i].Spell == c.I(2)) { list[i] = (-1, 0, false); cr.DomainSlots.Remove((ci, lvl, i)); }
        }
        return null;
    }

    // ---------------------------------------------------------------- spell-like abilities

    static SpellAbility? Ability(Call c, int creatureArg, int indexArg)
    {
        if (Cr(c, creatureArg) is not { } cr) return null;
        CreatureRules.EnsureInit(c.World, cr);
        int i = c.I(indexArg);
        return i >= 0 && i < cr.SpellAbilities.Count ? cr.SpellAbilities[i] : null;
    }

    [NwFunc("GetSpellAbilityCount")]
    static object? AbilityCount(Call c)
    {
        if (Cr(c, 0) is not { } cr) return 0;
        CreatureRules.EnsureInit(c.World, cr);
        return cr.SpellAbilities.Count;
    }

    [NwFunc("GetSpellAbilitySpell")] static object? AbilitySpell(Call c) => Ability(c, 0, 1)?.Spell ?? -1;
    [NwFunc("GetSpellAbilityCasterLevel")] static object? AbilityCL(Call c) => Ability(c, 0, 1)?.CasterLevel ?? -1;
    [NwFunc("GetSpellAbilityReady")] static object? AbilityReady(Call c) => Ability(c, 0, 1) is { } a ? (a.Ready ? 1 : 0) : -1;
    [NwFunc("SetSpellAbilityReady")]
    static object? SetAbilityReady(Call c)
    {
        if (Ability(c, 0, 1) is { } a) a.Ready = c.I(2) != 0;
        return null;
    }

    // ---------------------------------------------------------------- rest

    [NwFunc("ForceRest")]
    static object? ForceRest(Call c)
    {
        if (Cr(c, 0) is not { } cr || cr.IsDead) return null;
        CreatureRules.Rest(c.World, cr);
        return null;
    }

    // ---------------------------------------------------------------- talents

    [NwFunc("TalentSpell")] static object? TalentSpell(Call c) => new Talent { Type = NwRules.TalentSpell, Id = c.I(0) };
    [NwFunc("TalentFeat")] static object? TalentFeat(Call c) => new Talent { Type = NwRules.TalentFeat, Id = c.I(0) };
    [NwFunc("TalentSkill")] static object? TalentSkill(Call c) => new Talent { Type = NwRules.TalentSkill, Id = c.I(0) };
    [NwFunc("GetIsTalentValid")] static object? TalentValid(Call c) => c.T(0).Valid ? 1 : 0;
    [NwFunc("GetTypeFromTalent")] static object? TalentType(Call c) => c.T(0).Valid ? c.T(0).Type : -1;
    [NwFunc("GetIdFromTalent")] static object? TalentId(Call c) => c.T(0).Valid ? c.T(0).Id : -1;

    [NwFunc("GetCreatureHasTalent")]
    static object? HasTalent(Call c) =>
        Cr(c, 1) is { } cr && CreatureRules.HasTalent(c.World, cr, c.T(0)) ? 1 : 0;

    [NwFunc("GetCreatureTalentRandom")]
    static object? RandomTalent(Call c) =>
        Cr(c, 1) is { } cr ? CreatureRules.RandomTalent(c.World, cr, c.I(0)) : new Talent { Type = -1 };

    [NwFunc("GetCreatureTalentBest")]
    static object? BestTalent(Call c) =>
        Cr(c, 2) is { } cr ? CreatureRules.BestTalent(c.World, cr, c.I(0), c.I(1)) : new Talent { Type = -1 };
}

/// <summary>
/// ActionUseFeat: spends a use of the feat and, for feats that cast a spell (feat.2da SPELLID),
/// casts that spell at the feat's target like a spell-like ability (<see cref="Spells.CastSpellAction"/>).
/// </summary>
public sealed class UseFeatAction : GameAction
{
    public int Feat, SubFeat;
    public uint Target = GameObject.InvalidId;
    public Location TargetLocation = Location.Invalid;
    // ACTION_CASTSPELL, or ACTION_ATTACKOBJECT once a combat feat has turned into an attack.
    public override int ActionType => _attack != null ? 3 : 4;

    Spells.CastSpellAction? _cast;
    Combat.AttackAction? _attack;

    /// <summary>The attack a combat feat turned into, if it did.</summary>
    public Combat.AttackAction? Attack => _attack;

    /// <summary>A melee feat (TALENT_CATEGORY_HARMFUL_MELEE) or one combat uses as the next attack.</summary>
    public static bool IsMeleeFeat(World world, int feat) =>
        Combat.CombatSystem.IsAttackFeat(feat) || world.Resources.TwoDA("feat").GetInt(feat, "CATEGORY", -1) == 22;

    /// <summary>
    /// ActionUseFeat / ActionUseTalent* with a feat: a melee feat ordered while the creature is attacking changes that
    /// attack (nwserver ai_feat_use / ai_attack_queue), anything else is queued.
    /// </summary>
    public static void Order(World world, Creature self, int feat, int subFeat, uint target, Location location)
    {
        if (world.Get(target) != null && IsMeleeFeat(world, feat) && CreatureRules.HasFeat(world, self, feat)
            && Combat.AttackAction.Retarget(world, self, target))
        {
            if (Combat.CombatSystem.IsAttackFeat(feat) && UseFeatEvent(world, self, feat, subFeat, target, location))
            {
                CreatureRules.AdjustFeatUses(world, self, feat, -1);
                Combat.CombatSystem.TryUseCombatFeat(world, self, feat, subFeat, target);
            }
            return;
        }
        self.Actions.Add(new UseFeatAction { Feat = feat, Target = target, SubFeat = subFeat, TargetLocation = location });
    }

    /// <summary>
    /// NWNX_ON_USE_FEAT as a feat is used, before its use is spent (nwnx_events_creature); ACTION_RESULT is only in
    /// the _AFTER half. False when a subscriber skipped it.
    /// </summary>
    static bool UseFeatEvent(World world, Creature c, int feat, int subFeat, uint target, Location location)
    {
        var ev = Nwnx.NwnxEvent.Begin(world, "NWNX_ON_USE_FEAT", c);
        if (ev == null) return true;
        var at = world.Get(target) is { } t ? t.Position : location.Area != Location.Invalid.Area ? location.Position : System.Numerics.Vector3.Zero;
        ev.Add("FEAT_ID", feat).Add("SUBFEAT_ID", subFeat).AddObject("TARGET_OBJECT_ID", target).AddObject("AREA_OBJECT_ID", c.Area?.Id ?? GameObject.InvalidId)
            .Position("TARGET_POSITION_", at);
        bool skipped = ev.Before();
        ev.Add("ACTION_RESULT", !skipped).After();
        return !skipped;
    }

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (_cast != null) return _cast.Step(world, owner, dt);
        if (_attack != null) return _attack.Step(world, owner, dt);
        if (owner is not Creature c || !Spells.CastSpellAction.CanAct(world, c) || !CreatureRules.HasFeat(world, c, Feat)) return ActionStatus.Failed;
        if (!UseFeatEvent(world, c, Feat, SubFeat, Target, TargetLocation)) return ActionStatus.Failed;
        var feats = world.Resources.TwoDA("feat");
        int spell = SubFeat > 0 ? SubFeat : feats.GetInt(Feat, "SPELLID", -1);
        // Native turn_queue: Turn Undead spends its use at the end of preparation,
        // not when it enters the queue. Other feat families remain unmeasured.
        if (Feat != NwRules.FeatTurnUndead || spell < 0)
            CreatureRules.AdjustFeatUses(world, c, Feat, -1);
        // Attack feats (knockdown, disarm, called shot, smite, stunning fist, ...) become the next attack, and the action
        // goes on as an attack (nwserver ai_feat_use: GetCurrentAction is ACTION_ATTACKOBJECT and the fight continues).
        // The other melee feats (Power Attack, Expertise...) start an ordinary attack the same way.
        bool attackFeat = Combat.CombatSystem.TryUseCombatFeat(world, c, Feat, SubFeat, Target);
        if (attackFeat || spell < 0 && feats.GetInt(Feat, "CATEGORY", -1) == 22)
        {
            var t = world.Get(Target) ?? world.Get(world.System<Combat.CombatSystem>().TargetOf(c));
            if (t == null || t == c || t.Area != c.Area) return ActionStatus.Done;
            _attack = new Combat.AttackAction { Target = t.Id };
            return _attack.Step(world, owner, dt);
        }
        if (spell < 0) return ActionStatus.Done;
        // Feat spells are cast like spell-like abilities (no slot) at the feat's target.
        _cast = new Spells.CastSpellAction
        {
            Spell = spell, Target = Target, TargetLocation = TargetLocation, FeatId = Feat, CasterLevel = c.TotalLevel,
            Class = -1, Cheat = true,
        };
        return _cast.Step(world, owner, dt);
    }

    public override void Cancel(World world, GameObject owner)
    {
        _cast?.Cancel(world, owner);
        _attack?.Cancel(world, owner);
    }

    public override bool RetainsAfterQueueClear(World world, GameObject owner) =>
        Feat == NwRules.FeatTurnUndead && _cast?.RetainsAfterQueueClear(world, owner) == true;
}
