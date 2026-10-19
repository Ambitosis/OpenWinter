// Effect constructors: what each one reports through the getters, what lands on a creature,
// and how the creature's stats change (immediately and a second later).
#include "parity_fx"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

void Later(string k, object o)
{
    PLog(k + "delta1", PFxDiff(o));
    PLog(k + "list1", PFxList(o));
}

void Probe(string sName, effect e, int nDur = DURATION_TYPE_PERMANENT, float fDur = 0.0)
{
    string k = "ctor." + sName + ".";
    PLog(k + "pre", PFxDescribe(e));
    object o = PFxSpawn(NextCell());
    PFxSnap(o);
    ApplyEffectToObject(nDur, e, o, fDur);
    PLog(k + "delta0", PFxDiff(o));
    PLog(k + "list0", PFxList(o));
    effect a = GetFirstEffect(o);
    if (GetIsEffectValid(a)) PLog(k + "post", PFxDescribe(a));
    DelayCommand(1.0, Later(k, o));
}

void Invalid(string sName, effect e)
{
    PLog("invalid." + sName, PFxDescribe(e));
}

void Run()
{
    Probe("abilinc", EffectAbilityIncrease(ABILITY_STRENGTH, 4));
    Probe("abildec", EffectAbilityDecrease(ABILITY_DEXTERITY, 3));
    Probe("abildec.big", EffectAbilityDecrease(ABILITY_CONSTITUTION, 20));
    Probe("acinc", EffectACIncrease(3));
    Probe("acinc.natural", EffectACIncrease(2, AC_NATURAL_BONUS));
    Probe("acinc.armour", EffectACIncrease(2, AC_ARMOUR_ENCHANTMENT_BONUS));
    Probe("acinc.shield", EffectACIncrease(2, AC_SHIELD_ENCHANTMENT_BONUS));
    Probe("acinc.deflect", EffectACIncrease(2, AC_DEFLECTION_BONUS));
    Probe("acinc.vsfire", EffectACIncrease(2, AC_DODGE_BONUS, DAMAGE_TYPE_FIRE));
    Probe("acinc.vsslash", EffectACIncrease(2, AC_DODGE_BONUS, DAMAGE_TYPE_SLASHING));
    Probe("acdec", EffectACDecrease(2));
    Probe("acdec.natural", EffectACDecrease(2, AC_NATURAL_BONUS));
    Probe("saveinc.all", EffectSavingThrowIncrease(SAVING_THROW_ALL, 2));
    Probe("saveinc.fort", EffectSavingThrowIncrease(SAVING_THROW_FORT, 3));
    Probe("saveinc.willmind", EffectSavingThrowIncrease(SAVING_THROW_WILL, 2, SAVING_THROW_TYPE_MIND_SPELLS));
    Probe("savedec.ref", EffectSavingThrowDecrease(SAVING_THROW_REFLEX, 2));
    Probe("savedec.allfear", EffectSavingThrowDecrease(SAVING_THROW_ALL, 2, SAVING_THROW_TYPE_FEAR));
    Probe("attinc", EffectAttackIncrease(3));
    Probe("attinc.onhand", EffectAttackIncrease(2, ATTACK_BONUS_ONHAND));
    Probe("attdec", EffectAttackDecrease(2));
    Probe("dmginc", EffectDamageIncrease(2, DAMAGE_TYPE_FIRE));
    Probe("dmgdec", EffectDamageDecrease(2, DAMAGE_TYPE_FIRE));
    Probe("dmgres", EffectDamageResistance(DAMAGE_TYPE_FIRE, 10, 30));
    Probe("dmgred", EffectDamageReduction(5, DAMAGE_POWER_PLUS_ONE, 40));
    Probe("dmgimminc", EffectDamageImmunityIncrease(DAMAGE_TYPE_COLD, 50));
    Probe("dmgimmdec", EffectDamageImmunityDecrease(DAMAGE_TYPE_COLD, 25));
    Probe("srinc", EffectSpellResistanceIncrease(15));
    Probe("srdec", EffectSpellResistanceDecrease(5));
    Probe("skillinc", EffectSkillIncrease(SKILL_SPOT, 5));
    Probe("skillinc.all", EffectSkillIncrease(SKILL_ALL_SKILLS, 2));
    Probe("skilldec", EffectSkillDecrease(SKILL_HIDE, 3));
    Probe("temphp", EffectTemporaryHitpoints(10));
    Probe("immunity.mind", EffectImmunity(IMMUNITY_TYPE_MIND_SPELLS));
    Probe("immunity.death", EffectImmunity(IMMUNITY_TYPE_DEATH));
    Probe("curse", EffectCurse(1, 2, 3, 4, 5, 6));
    Probe("neglevel", EffectNegativeLevel(2));
    Probe("neglevel.hp", EffectNegativeLevel(1, TRUE));
    Probe("speedinc", EffectMovementSpeedIncrease(50));
    Probe("speeddec", EffectMovementSpeedDecrease(50));
    Probe("haste", EffectHaste());
    Probe("slow", EffectSlow());
    Probe("entangle", EffectEntangle());
    Probe("paralyze", EffectParalyze());
    Probe("stunned", EffectStunned());
    Probe("sleep", EffectSleep());
    Probe("dazed", EffectDazed());
    Probe("confused", EffectConfused());
    Probe("frightened", EffectFrightened());
    Probe("charmed", EffectCharmed());
    Probe("dominated", EffectDominated());
    Probe("blindness", EffectBlindness());
    Probe("deaf", EffectDeaf());
    Probe("silence", EffectSilence());
    Probe("knockdown", EffectKnockdown(), DURATION_TYPE_TEMPORARY, 30.0);
    Probe("petrify", EffectPetrify());
    Probe("turned", EffectTurned());
    Probe("invis", EffectInvisibility(INVISIBILITY_TYPE_NORMAL));
    Probe("invis.improved", EffectInvisibility(INVISIBILITY_TYPE_IMPROVED));
    Probe("concealment", EffectConcealment(50));
    Probe("misschance", EffectMissChance(20));
    Probe("darkness", EffectDarkness());
    Probe("ultravision", EffectUltravision());
    Probe("trueseeing", EffectTrueSeeing());
    Probe("seeinvis", EffectSeeInvisible());
    Probe("sanctuary", EffectSanctuary(20));
    Probe("ethereal", EffectEthereal());
    Probe("spellimm", EffectSpellImmunity(SPELL_MAGIC_MISSILE));
    Probe("spellabsorb", EffectSpellLevelAbsorption(4, 10));
    Probe("spellfail", EffectSpellFailure(50));
    // Poison and disease need a failed save to land: see fx_poison.
    Probe("regenerate", EffectRegenerate(2, 6.0));
    Probe("modattacks", EffectModifyAttacks(2));
    Probe("dmgshield", EffectDamageShield(3, DAMAGE_BONUS_1d6, DAMAGE_TYPE_FIRE));
    Probe("turnresinc", EffectTurnResistanceIncrease(4));
    Probe("turnresdec", EffectTurnResistanceDecrease(2));
    Probe("pacify", EffectPacified());
    Probe("bonusfeat", EffectBonusFeat(FEAT_ALERTNESS));
    Probe("icon", EffectIcon(5));
    Probe("hpdying", EffectHitPointChangeWhenDying(1.5));
    Probe("cutsceneghost", EffectCutsceneGhost());
    Probe("cutsceneimmob", EffectCutsceneImmobilize());
    Probe("cutsceneparalyze", EffectCutsceneParalyze());
    Probe("cutscenedom", EffectCutsceneDominated());
    Probe("forcewalk", EffectForceWalk());
    Probe("timestopimm", EffectTimeStopImmunity());
    Probe("enemyattack", EffectEnemyAttackBonus(3));
    Probe("vfx", EffectVisualEffect(VFX_DUR_GLOW_RED));
    Probe("beam", EffectBeam(VFX_BEAM_LIGHTNING, GetModule(), BODY_NODE_CHEST));
    Probe("aoe", EffectAreaOfEffect(AOE_PER_FOGACID, "****", "****", "****"));
    Probe("heal", EffectHeal(5), DURATION_TYPE_INSTANT);
    Probe("damage", EffectDamage(5, DAMAGE_TYPE_FIRE), DURATION_TYPE_INSTANT);
    Probe("damage.perm", EffectDamage(3, DAMAGE_TYPE_COLD));
    Probe("death", EffectDeath(), DURATION_TYPE_INSTANT);
    Probe("polymorph", EffectPolymorph(POLYMORPH_TYPE_BROWN_BEAR));
    Probe("runscript", EffectRunScript("", "", "", 0.0, "data"));
    Probe("temp.abilinc", EffectAbilityIncrease(ABILITY_WISDOM, 2), DURATION_TYPE_TEMPORARY, 60.0);
    Probe("instant.abilinc", EffectAbilityIncrease(ABILITY_WISDOM, 2), DURATION_TYPE_INSTANT);

    Invalid("heal", EffectHeal(-1));
    Invalid("temphp", EffectTemporaryHitpoints(-1));
    Invalid("concealment0", EffectConcealment(0));
    Invalid("concealment101", EffectConcealment(101));
    Invalid("skill", EffectSkillIncrease(999, 1));
    Invalid("icon", EffectIcon(0));
    Invalid("modattacks", EffectModifyAttacks(6));
    Invalid("invis", EffectInvisibility(3));
    Invalid("sanctuary", EffectSanctuary(0));
    Invalid("beam", EffectBeam(-1, GetModule(), BODY_NODE_CHEST));
    Invalid("abilinc0", EffectAbilityIncrease(ABILITY_STRENGTH, 0));
    Invalid("abilinc.neg", EffectAbilityIncrease(ABILITY_STRENGTH, -2));
    Invalid("abilinc.badability", EffectAbilityIncrease(9, 2));
    Invalid("acinc0", EffectACIncrease(0));
    Invalid("attinc.neg", EffectAttackIncrease(-3));
    Invalid("neglevel101", EffectNegativeLevel(101));
    Invalid("hpdying0", EffectHitPointChangeWhenDying(0.0));
    Invalid("link.invalid", EffectLinkEffects(EffectHeal(-1), EffectHeal(-1)));
    Invalid("default", GetFirstEffect(OBJECT_INVALID));

    DelayCommand(3.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
