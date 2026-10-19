using System.Numerics;
using System.Runtime.CompilerServices;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>
/// Effect constructors, modifiers, getters, application and iteration.
/// Constructors store their arguments in order (ints → Ints, floats → Floats, strings,
/// objects, vectors) and Type = the EFFECT_TYPE_* constant; see <see cref="EffectQueries"/>.
/// </summary>
static class EffectFunctions
{
    static EffectConstants K(Call c) => EffectConstants.Of(c.Host.Definitions);
    static EffectSystem Sys(Call c) => c.World.System<EffectSystem>();

    /// <summary>A new effect of EFFECT_TYPE_<paramref name="type"/> created by OBJECT_SELF in the current spell context.</summary>
    static Effect New(Call c, string type, params int[] ints)
    {
        var k = K(c);
        var e = new Effect { Type = k["EFFECT_TYPE_" + type], SubType = k.Magical, Creator = c.SelfId };
        bool hasSpell = c.Event.Values.ContainsKey("SpellId");
        e.SpellId = hasSpell ? c.Event.Int("SpellId") : -1;
        e.CasterLevel = c.Event.Int("CasterLevel");
        // Effects made in an area-of-effect script belong to whoever cast the AoE.
        if (c.Self is AreaOfEffect aoe)
        {
            e.Creator = aoe.Creator;
            if (!hasSpell) e.SpellId = aoe.SpellId;
            if (e.CasterLevel == 0) e.CasterLevel = aoe.CasterLevel;
        }
        for (int i = 0; i < ints.Length && i < e.Ints.Length; i++) e.Ints[i] = ints[i];
        e.ScriptId = Sys(c).NextId();
        return e;
    }

    /// <summary>
    /// EFFECT_TYPE_INVALIDEFFECT for bad arguments. Most constructors give a blank effect (no subtype, no creator);
    /// heal, temporary hit points and skills make an invalid one that is magical and made by OBJECT_SELF.
    /// </summary>
    static Effect Invalid(Call c)
    {
        var e = New(c, "INVALIDEFFECT");
        e.Type = 0;
        return e;
    }

    /// <summary>The same effect with another subtype (some constructors don't make magical effects).</summary>
    static Effect Sub(Effect e, int subType) { e.SubType = subType; return e; }

    /// <summary>The state code the original stores in a state effect's first integer.</summary>
    static Effect State(Call c, string type, int code) => New(c, type, code);

    /// <summary>ABILITY_*, with anything out of range read as strength like the original.</summary>
    static int Ability(int a) => a is >= 0 and <= 5 ? a : 0;

    static Effect WithFloats(Effect e, params float[] f) { for (int i = 0; i < f.Length && i < e.Floats.Length; i++) e.Floats[i] = f[i]; return e; }
    static Effect WithStrings(Effect e, params string[] s) { for (int i = 0; i < s.Length && i < e.Strings.Length; i++) e.Strings[i] = s[i]; return e; }
    static Effect WithObjects(Effect e, params uint[] o) { for (int i = 0; i < o.Length && i < e.Objects.Length; i++) e.Objects[i] = o[i]; return e; }
    static Effect WithVectors(Effect e, params Vector3[] v) { for (int i = 0; i < v.Length && i < e.Vectors.Length; i++) e.Vectors[i] = v[i]; return e; }

    // ------------------------------------------------------------------ constructors

    [NwFunc("EffectHeal")] static object? Heal(Call c) => c.I(0) <= 0 ? Invalid(c) : New(c, "HEAL", c.I(0));
    [NwFunc("EffectDamage")] static object? Damage(Call c) => New(c, "DAMAGE", c.I(0), c.I(1), c.I(2));
    [NwFunc("EffectAbilityIncrease")] static object? AbilityInc(Call c) => New(c, "ABILITY_INCREASE", Ability(c.I(0)), c.I(1));
    [NwFunc("EffectAbilityDecrease")] static object? AbilityDec(Call c) => New(c, "ABILITY_DECREASE", Ability(c.I(0)), c.I(1));
    [NwFunc("EffectDamageResistance")] static object? DamageResist(Call c) => New(c, "DAMAGE_RESISTANCE", c.I(0), c.I(1), c.I(2), c.I(3));
    [NwFunc("EffectResurrection")] static object? Resurrection(Call c) => New(c, "RESURRECTION");
    [NwFunc("EffectSummonCreature")] static object? Summon(Call c) =>
        WithObjects(WithStrings(WithFloats(New(c, "SUMMON_CREATURE", c.I(1), c.I(3), c.I(4)), c.F(2)), c.S(0)), c.O(5));
    [NwFunc("EffectACIncrease")] static object? ACInc(Call c) => New(c, "AC_INCREASE", c.I(0), c.I(1), c.I(2));
    [NwFunc("EffectACDecrease")] static object? ACDec(Call c) => New(c, "AC_DECREASE", c.I(0), c.I(1), c.I(2));
    [NwFunc("EffectSavingThrowIncrease")] static object? SaveInc(Call c) => New(c, "SAVING_THROW_INCREASE", c.I(0), c.I(1), c.I(2));
    [NwFunc("EffectSavingThrowDecrease")] static object? SaveDec(Call c) => New(c, "SAVING_THROW_DECREASE", c.I(0), c.I(1), c.I(2));
    [NwFunc("EffectAttackIncrease")] static object? AttackInc(Call c) => New(c, "ATTACK_INCREASE", c.I(0), c.I(1));
    [NwFunc("EffectAttackDecrease")] static object? AttackDec(Call c) => New(c, "ATTACK_DECREASE", c.I(0), c.I(1));
    [NwFunc("EffectDamageReduction")] static object? DamageReduction(Call c) => New(c, "DAMAGE_REDUCTION", c.I(0), c.I(1), c.I(2), c.I(3));
    [NwFunc("EffectDamageIncrease")] static object? DamageInc(Call c) => New(c, "DAMAGE_INCREASE", c.I(0), c.I(1));
    [NwFunc("EffectDamageDecrease")] static object? DamageDec(Call c) => New(c, "DAMAGE_DECREASE", c.I(0), c.I(1));
    [NwFunc("EffectEntangle")] static object? Entangle(Call c) => New(c, "ENTANGLE");
    [NwFunc("EffectDeath")] static object? Death(Call c) => New(c, "DEATH", c.I(0), c.I(1));
    [NwFunc("EffectKnockdown")] static object? Knockdown(Call c) => New(c, "KNOCKDOWN");
    [NwFunc("EffectCurse")] static object? Curse(Call c) => New(c, "CURSE", c.I(0), c.I(1), c.I(2), c.I(3), c.I(4), c.I(5));
    [NwFunc("EffectParalyze")] static object? Paralyze(Call c) => State(c, "PARALYZE", 8);
    [NwFunc("EffectSpellImmunity")] static object? SpellImmunity(Call c) => c.I(0) < -1 ? Blank() : New(c, "SPELL_IMMUNITY", c.I(0));
    [NwFunc("EffectDeaf")] static object? Deaf(Call c) => New(c, "DEAF");
    [NwFunc("EffectSleep")] static object? Sleep(Call c) => State(c, "SLEEP", 9);
    [NwFunc("EffectCharmed")] static object? Charmed(Call c) => State(c, "CHARMED", 1);
    [NwFunc("EffectConfused")] static object? Confused(Call c) => State(c, "CONFUSED", 2);
    [NwFunc("EffectFrightened")] static object? Frightened(Call c) => State(c, "FRIGHTENED", 3);
    [NwFunc("EffectDominated")] static object? Dominated(Call c) => State(c, "DOMINATED", 7);
    [NwFunc("EffectDazed")] static object? Dazed(Call c) => State(c, "DAZED", 5);
    [NwFunc("EffectStunned")] static object? Stunned(Call c) => State(c, "STUNNED", 6);
    [NwFunc("EffectRegenerate")] static object? Regenerate(Call c) => WithFloats(New(c, "REGENERATE", c.I(0)), c.F(1));
    [NwFunc("EffectMovementSpeedIncrease")] static object? SpeedInc(Call c) => New(c, "MOVEMENT_SPEED_INCREASE", c.I(0));
    [NwFunc("EffectMovementSpeedDecrease")] static object? SpeedDec(Call c) => New(c, "MOVEMENT_SPEED_DECREASE", c.I(0));
    [NwFunc("EffectAreaOfEffect")] static object? AreaOfEffect(Call c) => WithStrings(New(c, "AREA_OF_EFFECT", c.I(0)), c.S(1), c.S(2), c.S(3));
    [NwFunc("EffectVisualEffect")] static object? VisualEffect(Call c) =>
        WithVectors(WithFloats(New(c, "VISUALEFFECT", c.I(0), c.I(1)), c.F(2, 1f)), c.V(3), c.V(4));
    [NwFunc("EffectBeam")] static object? Beam(Call c) =>
        c.I(0) < 0 ? Blank() : WithVectors(WithFloats(WithObjects(New(c, "BEAM", c.I(0), c.I(2), c.I(3)), c.O(1)), c.F(4, 1f)), c.V(5), c.V(6));
    [NwFunc("EffectSpellResistanceIncrease")] static object? SRInc(Call c) => New(c, "SPELL_RESISTANCE_INCREASE", c.I(0));
    [NwFunc("EffectSpellResistanceDecrease")] static object? SRDec(Call c) => New(c, "SPELL_RESISTANCE_DECREASE", c.I(0));
    [NwFunc("EffectPoison")] static object? Poison(Call c) => New(c, "POISON", c.I(0));
    [NwFunc("EffectDisease")] static object? Disease(Call c) => Sub(New(c, "DISEASE", c.I(0)), K(c).Supernatural);
    [NwFunc("EffectSilence")] static object? Silence(Call c) => New(c, "SILENCE");
    [NwFunc("EffectHaste")] static object? Haste(Call c) => New(c, "HASTE");
    [NwFunc("EffectSlow")] static object? Slow(Call c) => New(c, "SLOW");
    [NwFunc("EffectImmunity")] static object? Immunity(Call c) => New(c, "IMMUNITY", c.I(0));
    [NwFunc("EffectDamageImmunityIncrease")] static object? DmgImmInc(Call c) => New(c, "DAMAGE_IMMUNITY_INCREASE", c.I(0), c.I(1));
    [NwFunc("EffectDamageImmunityDecrease")] static object? DmgImmDec(Call c) => New(c, "DAMAGE_IMMUNITY_DECREASE", c.I(0), c.I(1));
    [NwFunc("EffectTemporaryHitpoints")] static object? TempHp(Call c) => c.I(0) < 0 ? Invalid(c) : New(c, "TEMPORARY_HITPOINTS", c.I(0));
    [NwFunc("EffectSkillIncrease")] static object? SkillInc(Call c) => ValidSkill(c, c.I(0)) ? New(c, "SKILL_INCREASE", c.I(0), c.I(1)) : Invalid(c);
    [NwFunc("EffectSkillDecrease")] static object? SkillDec(Call c) => ValidSkill(c, c.I(0)) ? New(c, "SKILL_DECREASE", c.I(0), c.I(1)) : Invalid(c);
    [NwFunc("EffectTurned")] static object? Turned(Call c) => State(c, "TURNED", 4);
    [NwFunc("EffectHitPointChangeWhenDying")] static object? HpChangeDying(Call c) => c.F(0) == 0 ? Blank() : Sub(WithFloats(New(c, "HITPOINTCHANGEWHENDYING"), c.F(0)), 0);
    [NwFunc("EffectInvisibility")] static object? Invisibility(Call c)
    {
        var k = K(c);
        int t = c.I(0);
        // Improved invisibility is an invisibility effect of the improved kind (GetEffectType says INVISIBILITY).
        if (t == k["INVISIBILITY_TYPE_NORMAL"] || t == k["INVISIBILITY_TYPE_DARKNESS"] || t == k["INVISIBILITY_TYPE_IMPROVED"]) return New(c, "INVISIBILITY", t);
        return Blank();
    }
    [NwFunc("EffectConcealment")] static object? Concealment(Call c) => c.I(0) is < 1 or > 100 ? Blank() : New(c, "CONCEALMENT", c.I(0), c.I(1));
    [NwFunc("EffectMissChance")] static object? MissChance(Call c) => c.I(0) is < 1 or > 100 ? Blank() : New(c, "MISS_CHANCE", c.I(0), c.I(1));
    [NwFunc("EffectDarkness")] static object? Darkness(Call c) => New(c, "DARKNESS");
    [NwFunc("EffectDispelMagicAll")] static object? DispelAll(Call c) => New(c, "DISPELMAGICALL", c.I(0));
    [NwFunc("EffectDispelMagicBest")] static object? DispelBest(Call c) => New(c, "DISPELMAGICBEST", c.I(0));
    [NwFunc("EffectUltravision")] static object? Ultravision(Call c) => New(c, "ULTRAVISION");
    [NwFunc("EffectNegativeLevel")] static object? NegativeLevel(Call c)
    {
        if (c.I(0) > 100) return Blank();
        // The original makes it temporary already (so GetIsEffectValid is TRUE before it is applied).
        var e = New(c, "NEGATIVELEVEL", c.I(0), c.I(1));
        e.DurationType = K(c).Temporary;
        return e;
    }
    [NwFunc("EffectPolymorph")] static object? Polymorph(Call c) => New(c, "POLYMORPH", c.I(0), c.I(1), c.I(2), c.I(3), c.I(4));
    [NwFunc("EffectSanctuary")] static object? Sanctuary(Call c) => c.I(0) <= 0 ? Blank() : New(c, "SANCTUARY", c.I(0));
    [NwFunc("EffectTrueSeeing")] static object? TrueSeeing(Call c) => New(c, "TRUESEEING");
    [NwFunc("EffectSeeInvisible")] static object? SeeInvisible(Call c) => New(c, "SEEINVISIBLE");
    [NwFunc("EffectTimeStop")] static object? TimeStop(Call c) => New(c, "TIMESTOP");
    [NwFunc("EffectBlindness")] static object? Blindness(Call c) => State(c, "BLINDNESS", 16);
    [NwFunc("EffectSpellLevelAbsorption")] static object? SpellLevelAbsorption(Call c) =>
        c.I(0) is < -1 or > 9 || c.I(2) < 0 ? Blank() : New(c, "SPELLLEVELABSORPTION", c.I(0), c.I(1), c.I(2));
    [NwFunc("EffectDisappearAppear")] static object? DisappearAppear(Call c)
    {
        var loc = c.L(0);
        return WithVectors(WithFloats(WithObjects(New(c, "DISAPPEARAPPEAR", c.I(1)), loc.Area), loc.FacingDegrees), loc.Position);
    }
    [NwFunc("EffectDisappear")] static object? Disappear(Call c) => Sub(New(c, "DISAPPEAR", c.I(0)), 0);
    [NwFunc("EffectAppear")] static object? Appear(Call c) => Sub(New(c, "APPEAR", c.I(0)), 0);
    [NwFunc("EffectModifyAttacks")] static object? ModifyAttacks(Call c) => c.I(0) > 5 ? Blank() : New(c, "MODIFY_ATTACKS", c.I(0));
    [NwFunc("EffectDamageShield")] static object? DamageShield(Call c) => New(c, "ELEMENTALSHIELD", c.I(0), c.I(1), c.I(2));
    [NwFunc("EffectSwarm")] static object? Swarm(Call c) => WithStrings(New(c, "SWARM", c.I(0)), c.S(1), c.S(2), c.S(3), c.S(4));
    [NwFunc("EffectTurnResistanceDecrease")] static object? TurnResDec(Call c) => New(c, "TURN_RESISTANCE_DECREASE", c.I(0));
    [NwFunc("EffectTurnResistanceIncrease")] static object? TurnResInc(Call c) => New(c, "TURN_RESISTANCE_INCREASE", c.I(0));
    [NwFunc("EffectPetrify")] static object? Petrify(Call c) => State(c, "PETRIFY", 15);
    [NwFunc("EffectCutsceneParalyze")] static object? CutsceneParalyze(Call c) => State(c, "CUTSCENE_PARALYZE", 20);
    [NwFunc("EffectCutsceneDominated")] static object? CutsceneDominated(Call c) => State(c, "CUTSCENE_DOMINATED", 21);
    /// <summary>Arcane-only failure is an arcane spell failure effect (it adds to GetArcaneSpellFailure).</summary>
    [NwFunc("EffectSpellFailure")] static object? SpellFailure(Call c) =>
        c.I(2) == K(c)["SPELL_FAILURE_TYPE_ARCANE"] ? New(c, "ARCANE_SPELL_FAILURE", c.I(0)) : New(c, "SPELL_FAILURE", c.I(0), c.I(1), c.I(2));
    [NwFunc("EffectEthereal")] static object? Ethereal(Call c) => New(c, "ETHEREAL");
    [NwFunc("EffectCutsceneGhost")] static object? CutsceneGhost(Call c) => Sub(New(c, "CUTSCENEGHOST"), 0);
    [NwFunc("EffectCutsceneImmobilize")] static object? CutsceneImmobilize(Call c) => New(c, "CUTSCENEIMMOBILIZE");
    /// <summary>sData is GetEffectString(e, 0) per nwscript.nss, so Strings = [sData, sOnApplied, sOnRemoved, sOnInterval].</summary>
    [NwFunc("EffectRunScript")] static object? RunScript(Call c) =>
        WithStrings(WithFloats(New(c, "RUNSCRIPT"), c.F(3)), c.S(4), c.S(0), c.S(1), c.S(2));
    [NwFunc("EffectIcon")] static object? Icon(Call c) => c.I(0) is < 1 or > 255 ? Blank() : New(c, "ICON", c.I(0));
    [NwFunc("EffectPacified")] static object? Pacified(Call c) => New(c, "PACIFY");
    [NwFunc("EffectBonusFeat")] static object? BonusFeat(Call c) => New(c, "BONUS_FEAT", c.I(0));
    [NwFunc("EffectTimeStopImmunity")] static object? TimeStopImmunity(Call c) => New(c, "TIMESTOP_IMMUNITY");
    [NwFunc("EffectForceWalk")] static object? ForceWalk(Call c) => New(c, "FORCE_WALK");
    [NwFunc("EffectEnemyAttackBonus")] static object? EnemyAttackBonus(Call c) => New(c, "ENEMY_ATTACK_BONUS", c.I(0));

    static bool ValidSkill(Call c, int skill)
    {
        var skills = c.World.Resources.TwoDA("skills");
        // SKILL_ALL_SKILLS (255) is accepted.
        return skill == 255 || (skill >= 0 && skill < skills.RowCount && !skills.IsEmpty(skill, "Label"));
    }

    // ------------------------------------------------------------------ modifiers (return modified copies)

    static object Modify(Call c, Action<Effect> change)
    {
        var e = c.E(0);
        return e.IsValid || e.Type == 0 ? e.With(change) : e;
    }

    [NwFunc("MagicalEffect")] static object? Magical(Call c) { int s = K(c).Magical; return Modify(c, e => e.SubType = s); }
    [NwFunc("SupernaturalEffect")] static object? Supernatural(Call c) { int s = K(c).Supernatural; return Modify(c, e => e.SubType = s); }
    [NwFunc("ExtraordinaryEffect")] static object? Extraordinary(Call c) { int s = K(c).Extraordinary; return Modify(c, e => e.SubType = s); }
    [NwFunc("UnyieldingEffect")] static object? Unyielding(Call c) { int s = K(c).Unyielding; return Modify(c, e => e.SubType = s); }
    [NwFunc("IgnoreEffectImmunity")] static object? IgnoreImmunity(Call c) => Modify(c, e => e.IgnoreImmunity = true);
    [NwFunc("TagEffect")] static object? TagEffect(Call c)
    {
        string t = c.S(1);
        var nwnx = c.World.System<Nwnx.NwnxRegistry>();
        if (t == Nwnx.NwnxRegistry.LegacyPush) { nwnx.Stack.Push(new Nwnx.NwnxValue(Nwnx.NwnxType.Effect, c.E(0))); return c.E(0); }
        if (t == Nwnx.NwnxRegistry.LegacyPop) return nwnx.PopAs(Nwnx.NwnxType.Effect) ?? c.E(0);
        return Modify(c, e => e.Tag = t);
    }
    [NwFunc("SetEffectCreator")] static object? SetCreator(Call c) { uint o = c.O(1); return Modify(c, e => e.Creator = o); }
    [NwFunc("SetEffectCasterLevel")] static object? SetCasterLevel(Call c) { int l = c.I(1); return Modify(c, e => e.CasterLevel = l); }
    [NwFunc("SetEffectSpellId")] static object? SetSpellId(Call c) { int s = c.I(1); return Modify(c, e => e.SpellId = s); }

    /// <summary>
    /// Effect types that take versus filters. The original keeps a race and alignment in these effects'
    /// integers; on any other type VersusAlignmentEffect and VersusRacialTypeEffect change nothing.
    /// </summary>
    static bool TakesVersus(Effect e) => e.Type is 15 or 40 or 41 or 42 or 43 or 46 or 47 or 50 or 51 or 54 or 55 or 56 or 57 or 63 or 72 or 81;

    [NwFunc("VersusAlignmentEffect")] static object? VersusAlignment(Call c)
    {
        var k = K(c);
        int Axis(int v) =>
            v == k["ALIGNMENT_NEUTRAL"] ? 1 :
            v == k["ALIGNMENT_LAWFUL"] || v == k["ALIGNMENT_GOOD"] ? 2 :
            v == k["ALIGNMENT_CHAOTIC"] || v == k["ALIGNMENT_EVIL"] ? 3 : 0;
        int lc = Axis(c.I(1)), ge = Axis(c.I(2));
        return Modify(c, e => { if (TakesVersus(e)) { e.VersusLawChaos = lc; e.VersusGoodEvil = ge; } });
    }
    [NwFunc("VersusRacialTypeEffect")] static object? VersusRace(Call c)
    {
        int race = c.I(1) == K(c).RacialInvalid ? -1 : c.I(1);
        return Modify(c, e => { if (TakesVersus(e)) e.VersusRace = race; });
    }
    [NwFunc("VersusTrapEffect")] static object? VersusTrap(Call c) => Modify(c, e => e.VersusTraps = true);

    [NwFunc("EffectLinkEffects")] static object? Link(Call c)
    {
        var child = c.E(0);
        var parent = c.E(1);
        if (!parent.IsValid) return child.IsValid ? child.Copy() : parent;
        var link = parent.Copy(false);
        link.Linked.Clear();
        // The child's effects go on before the parent's.
        if (child.IsValid) foreach (var leaf in child.Leaves) link.Linked.Add(leaf.Copy(false));
        foreach (var leaf in parent.Leaves) link.Linked.Add(leaf.Copy(false));
        link.LinkId = Sys(c).NewLinkId();
        link.ScriptId = Sys(c).NextId();
        foreach (var leaf in link.Linked) leaf.LinkId = link.LinkId;
        return link;
    }

    // ------------------------------------------------------------------ getters

    /// <summary>
    /// Valid means a real effect that is temporary or permanent: effects fresh from a constructor are instant
    /// (and so not valid) until applied, except negative levels, which start out temporary.
    /// </summary>
    [NwFunc("GetIsEffectValid")] static object? IsValid(Call c) => c.E(0).IsValid && c.E(0).DurationType != K(c).Instant ? 1 : 0;
    [NwFunc("GetEffectDurationType")] static object? DurationType(Call c) => c.E(0).IsValid ? c.E(0).DurationType : 0;
    [NwFunc("GetEffectSubType")] static object? SubType(Call c) => c.E(0).IsValid || c.E(0).Type == 0 ? c.E(0).SubType : 0;
    [NwFunc("GetEffectCreator")] static object? Creator(Call c) =>
        (c.E(0).IsValid || c.E(0).Type == 0) && c.World.Get(c.E(0).Creator) != null ? c.E(0).Creator : GameObject.InvalidId;
    /// <summary>A link and a resurrection report EFFECT_TYPE_INVALIDEFFECT.</summary>
    [NwFunc("GetEffectType")] static object? Type(Call c)
    {
        var e = c.E(0);
        if (!e.IsValid || e.IsLinked || e.Type == K(c).Resurrection) return 0;
        return e.Type >= K(c).FirstHidden && c.I(1) == 0 ? 0 : e.Type;
    }
    [NwFunc("GetEffectSpellId")] static object? SpellId(Call c) => c.E(0).IsValid || c.E(0).Type == 0 ? c.E(0).SpellId : -1;
    [NwFunc("GetEffectTag")] static object? Tag(Call c) => c.E(0).Tag;
    [NwFunc("GetEffectCasterLevel")] static object? CasterLevel(Call c) => c.E(0).IsValid ? c.E(0).CasterLevel : 0;
    [NwFunc("GetEffectDuration")] static object? Duration(Call c) =>
        c.E(0).IsValid && c.E(0).DurationType == K(c).Temporary ? (int)c.E(0).Duration : 0;
    [NwFunc("GetEffectDurationRemaining")] static object? DurationRemaining(Call c)
    {
        var e = c.E(0);
        if (!e.IsValid || e.DurationType != K(c).Temporary) return 0;
        // A copy that was never applied has its whole duration left.
        if (e.Target == GameObject.InvalidId && e.ExpiresAt == 0) return (int)e.Duration;
        return (int)Math.Max(0, e.ExpiresAt - c.World.Time);
    }
    [NwFunc("GetEffectInteger")] static object? Int(Call c) => c.I(1) is >= 0 and < 8 ? ReportedInts(c, c.E(0))[c.I(1)] : 0;
    [NwFunc("GetEffectFloat")] static object? Float(Call c) => c.I(1) >= 0 && c.I(1) < c.E(0).Floats.Length ? c.E(0).Floats[c.I(1)] : 0f;
    [NwFunc("GetEffectString")] static object? String(Call c) => c.I(1) >= 0 && c.I(1) < c.E(0).Strings.Length ? c.E(0).Strings[c.I(1)] : "";
    [NwFunc("GetEffectObject")] static object? Object(Call c) => c.I(1) >= 0 && c.I(1) < c.E(0).Objects.Length ? c.E(0).Objects[c.I(1)] : GameObject.InvalidId;
    [NwFunc("GetEffectVector")] static object? Vector(Call c) => c.I(1) >= 0 && c.I(1) < c.E(0).Vectors.Length ? c.E(0).Vectors[c.I(1)] : Vector3.Zero;
    [NwFunc("GetEffectLinkId")] static object? LinkId(Call c) => c.E(0).LinkId;

    /// <summary>
    /// The eight integers GetEffectInteger reports, in the original's layout. Effects here keep their
    /// constructor arguments in order (see <see cref="EffectQueries"/>); the original stores some in another
    /// order and adds versus filters (race, law/chaos, good/evil) to the types that take them.
    /// </summary>
    public static int[] ReportedInts(Call c, Effect e) => ReportedInts(K(c), e);

    public static int[] ReportedInts(EffectConstants k, Effect e)
    {
        var r = new int[8];
        if (!e.IsValid && e.Type != 0 || e.IsLinked) return r;
        int[] i = e.Ints;
        int race = e.VersusRace >= 0 ? e.VersusRace : k.RacialInvalid;
        int lc = e.VersusLawChaos;
        int ge = e.VersusGoodEvil switch { 1 => k["ALIGNMENT_NEUTRAL"], 2 => k["ALIGNMENT_GOOD"], 3 => k["ALIGNMENT_EVIL"], _ => 0 };
        bool applied = e.Target != GameObject.InvalidId;
        void Set(params int[] v) { for (int n = 0; n < v.Length && n < 8; n++) r[n] = v[n]; }
        switch (e.Type)
        {
            case 46 or 47: Set(i[1], i[0], race, lc, ge, i[2]); break;          // AC: type, amount, versus, damage type
            case 50 or 51: Set(i[1], i[0], i[2], race, lc, ge); break;          // saves: amount, save, save type, versus
            case 40 or 41 or 42 or 43: Set(i[0], i[1], race, lc, ge); break;    // attack and damage bonuses
            case 54 or 55: Set(i[0], i[1], race, lc, ge); break;                // skills
            case 15 or 56 or 57: Set(i[0], race, lc, ge); break;                // immunity, invisibility
            case 72: Set(i[0], race, lc, ge, MissType(k, i[1])); break;        // concealment
            case 71: Set(i[0], MissType(k, i[1])); break;                       // miss chance
            case 63: Set(8, race, lc, ge, i[0], 1); break;                      // sanctuary
            case 81: Set(8, race, lc, ge); break;                               // ethereal
            case 44 or 45: Set(i[0], Math.Min(i[1], 100)); break;               // damage immunity
            case 48: Set(applied ? CreatureRules.MovementIncreasePercent(i[0]) : i[0]); break;                   // movement speed: the factor once applied
            case 3: Set(i[0], (int)MathF.Round(e.Floats[0] * 1000)); break;     // regenerate: amount, interval in ms
            case 61: Set(i[0], applied ? 0 : i[1]); break;                      // negative level
            case 62: Set(i[0], i[5], i[1], 1, i[2], 1, i[3] & 0xFF, i[4]); break; // polymorph (second: ending)
            case 100: Set(i[0], i[1], 1, i[2]); break;                          // summon: vfx, appear, unsummon vfx
            case 74: Set(i[0], 0, i[1]); break;                                 // visual effect: vfx, miss
            case 68: Set(i[0], i[1], i[2], i[1] == 0 ? 1 : 0); break;           // spell level absorption: unlimited flag
            case 93:                                                            // damage: amount by damage type bit
                for (int n = 0; n < 8; n++) r[n] = (i[1] & (1 << n)) != 0 ? i[0] : -1;
                break;
            default: Array.Copy(i, r, Math.Min(8, i.Length)); break;
        }
        return r;
    }

    /// <summary>MISS_CHANCE_TYPE_* as the original stores it (normal 0, then ranged 2, melee 3).</summary>
    public static int MissType(EffectConstants k, int t) => t == k["MISS_CHANCE_TYPE_NORMAL"] ? 0 : t + 1;

    [NwFunc("GetHasSpellEffect")] static object? HasSpellEffect(Call c) =>
        c.Obj(1) is { } o && EffectQueries.HasSpell(o, c.I(0)) ? 1 : 0;

    // ------------------------------------------------------------------ applying / removing

    [NwFunc("ApplyEffectToObject")] static object? ApplyToObject(Call c)
    {
        if (c.Obj(2) is { } target) Sys(c).Apply(c.E(1), c.I(0), target, c.F(3));
        return null;
    }
    [NwFunc("ApplyEffectAtLocation")] static object? ApplyAtLocation(Call c)
    {
        Sys(c).ApplyAtLocation(c.E(1), c.I(0), c.L(2), c.F(3));
        return null;
    }
    [NwFunc("RemoveEffect")] static object? Remove(Call c)
    {
        if (c.Obj(0) is { } o) Sys(c).RemoveScripted(o, c.E(1));
        return null;
    }

    [NwFunc("SetEffectIconFlashing")] static object? IconFlashing(Call c)
    {
        if (c.Obj<Creature>(0) is not { } pc) return null;
        // The client maps effects to effecticons.2da and ignores icons the creature doesn't show.
        if (pc.Effects.Count > 0)
            c.World.Output?.Notify("effect-icon-flash", pc, new { icon = c.I(1), flashing = c.I(2) != 0 }, pc);
        return null;
    }

    // ------------------------------------------------------------------ iteration

    /// <summary>
    /// One cursor per object, shared by every script like the original's (an inner loop over the same
    /// object moves the outer loop's cursor). The cursor walks a snapshot, so RemoveEffect while iterating
    /// is safe. Past the end it starts over: the next GetNextEffect returns the first effect again.
    /// </summary>
    sealed class Cursor { public List<Effect> List = new(); public int Index = -1; }
    static readonly ConditionalWeakTable<GameObject, Cursor> _cursors = new();

    [NwFunc("GetFirstEffect")] static object? First(Call c)
    {
        if (c.Obj(0) is not { } o) return Blank();
        var cur = _cursors.GetOrCreateValue(o);
        cur.List = o.Effects.Where(e => !e.Hidden).ToList();
        cur.Index = -1;
        return Advance(o, cur);
    }

    [NwFunc("GetNextEffect")] static object? Next(Call c)
    {
        if (c.Obj(0) is not { } o) return Blank();
        var cur = _cursors.GetOrCreateValue(o);
        if (cur.Index < 0) cur.List = o.Effects.Where(e => !e.Hidden).ToList();
        return Advance(o, cur);
    }

    static Effect Advance(GameObject o, Cursor cur)
    {
        int i = cur.Index + 1;
        // Skip effects removed since the snapshot (e.g. the rest of a link that was just removed).
        while (i < cur.List.Count && !o.Effects.Contains(cur.List[i])) i++;
        if (i < cur.List.Count) { cur.Index = i; return cur.List[i]; }
        cur.Index = -1;
        return Blank();
    }

    /// <summary>What the iterators return when there is no effect: no subtype and no creator.</summary>
    static Effect Blank() => new() { Type = 0 };
}
