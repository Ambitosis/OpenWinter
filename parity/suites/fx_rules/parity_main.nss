// Effect rules: negative levels, helplessness, versus filters, instant application of lasting
// effects and the integers the getters report for less common arguments.
#include "parity_fx"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

void Later(string k, object o)
{
    PLog(k + "delta2", PFxDiff(o));
    PLog(k + "list2", PFxList(o));
}

// Applies eSetup (if valid) first, snapshots, then applies e.
effect NoFx() { return EffectHeal(-1); }

void Probe(string sName, effect e, string sResRef, int nDur, effect eSetup)
{
    string k = "rule." + sName + ".";
    object o = PFxSpawn(NextCell(), sResRef);
    if (GetEffectType(eSetup, TRUE) != 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, eSetup, o);
    PFxSnap(o);
    ApplyEffectToObject(nDur, e, o, 60.0);
    PLog(k + "delta0", PFxDiff(o));
    PLog(k + "list0", PFxList(o));
    DelayCommand(2.0, Later(k, o));
}

void Ints(string sName, effect e)
{
    PLog("ints." + sName, PFxDescribe(e));
}

void Base(string sResRef)
{
    object o = PFxSpawn(NextCell(), sResRef);
    string s = "";
    int i;
    for (i = 0; i < PFX_STATS; i++) s += PFxStatName(i) + "=" + IntToString(PFxStat(o, i)) + " ";
    PLog("base." + sResRef, s);
}

void Run()
{
    Base("nw_bandit001");
    Base("nw_bandit006");
    Base("nw_drowrogue010");

    // Negative levels on a 10th level fighter.
    Probe("neg1", EffectNegativeLevel(1), "nw_bandit006", DURATION_TYPE_PERMANENT, NoFx());
    Probe("neg2", EffectNegativeLevel(2), "nw_bandit006", DURATION_TYPE_PERMANENT, NoFx());
    Probe("neg3", EffectNegativeLevel(3), "nw_bandit006", DURATION_TYPE_PERMANENT, NoFx());
    Probe("neg5", EffectNegativeLevel(5), "nw_bandit006", DURATION_TYPE_PERMANENT, NoFx());
    Probe("neg2hp", EffectNegativeLevel(2, TRUE), "nw_bandit006", DURATION_TYPE_PERMANENT, NoFx());
    Probe("neg10", EffectNegativeLevel(10), "nw_bandit006", DURATION_TYPE_PERMANENT, NoFx());
    Probe("neg9", EffectNegativeLevel(9), "nw_bandit006", DURATION_TYPE_PERMANENT, NoFx());

    // Helplessness on a high-dexterity creature, alone and with a dexterity bonus.
    Probe("paralyze.dex21", EffectParalyze(), "nw_drowrogue010", DURATION_TYPE_PERMANENT, NoFx());
    Probe("paralyze.dexbonus", EffectParalyze(), "nw_drowrogue010", DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_DEXTERITY, 4));
    Probe("dexbonus.paralyzed", EffectAbilityIncrease(ABILITY_DEXTERITY, 4), "nw_drowrogue010", DURATION_TYPE_PERMANENT, EffectParalyze());
    Probe("dexpenalty.paralyzed", EffectAbilityDecrease(ABILITY_DEXTERITY, 4), "nw_drowrogue010", DURATION_TYPE_PERMANENT, EffectParalyze());
    Probe("entangle.dex21", EffectEntangle(), "nw_drowrogue010", DURATION_TYPE_PERMANENT, NoFx());
    Probe("stun.dex21", EffectStunned(), "nw_drowrogue010", DURATION_TYPE_PERMANENT, NoFx());
    Probe("knockdown.dex21", EffectKnockdown(), "nw_drowrogue010", DURATION_TYPE_TEMPORARY, NoFx());
    Probe("dazed.dex21", EffectDazed(), "nw_drowrogue010", DURATION_TYPE_PERMANENT, NoFx());
    Probe("haste.dex21", EffectHaste(), "nw_drowrogue010", DURATION_TYPE_PERMANENT, NoFx());
    Probe("slow.dex21", EffectSlow(), "nw_drowrogue010", DURATION_TYPE_PERMANENT, NoFx());
    Probe("blind.rogue", EffectBlindness(), "nw_drowrogue010", DURATION_TYPE_PERMANENT, NoFx());
    Probe("frightened.rogue", EffectFrightened(), "nw_drowrogue010", DURATION_TYPE_PERMANENT, NoFx());

    // Haste and slow against other AC and save effects.
    Probe("haste.dodge", EffectHaste(), "nw_bandit001", DURATION_TYPE_PERMANENT, EffectACIncrease(3, AC_DODGE_BONUS));
    Probe("haste.deflect", EffectHaste(), "nw_bandit001", DURATION_TYPE_PERMANENT, EffectACIncrease(5, AC_DEFLECTION_BONUS));
    Probe("dodge.hasted", EffectACIncrease(2, AC_DODGE_BONUS), "nw_bandit001", DURATION_TYPE_PERMANENT, EffectHaste());
    Probe("slow.hasted", EffectSlow(), "nw_bandit001", DURATION_TYPE_PERMANENT, EffectHaste());
    Probe("haste.slowed", EffectHaste(), "nw_bandit001", DURATION_TYPE_PERMANENT, EffectSlow());
    Probe("slow.acdec", EffectSlow(), "nw_bandit001", DURATION_TYPE_PERMANENT, EffectACDecrease(3));
    Probe("frightened.saveinc", EffectFrightened(), "nw_bandit001", DURATION_TYPE_PERMANENT, EffectSavingThrowIncrease(SAVING_THROW_ALL, 3));

    // Lasting effects applied as instant.
    Probe("instant.ac", EffectACIncrease(2, AC_DEFLECTION_BONUS), "nw_bandit001", DURATION_TYPE_INSTANT, NoFx());
    Probe("instant.save", EffectSavingThrowIncrease(SAVING_THROW_ALL, 2), "nw_bandit001", DURATION_TYPE_INSTANT, NoFx());
    Probe("instant.skill", EffectSkillIncrease(SKILL_SPOT, 4), "nw_bandit001", DURATION_TYPE_INSTANT, NoFx());
    Probe("instant.sr", EffectSpellResistanceIncrease(12), "nw_bandit001", DURATION_TYPE_INSTANT, NoFx());
    Probe("instant.temphp", EffectTemporaryHitpoints(7), "nw_bandit001", DURATION_TYPE_INSTANT, NoFx());
    Probe("instant.immunity", EffectImmunity(IMMUNITY_TYPE_POISON), "nw_bandit001", DURATION_TYPE_INSTANT, NoFx());
    Probe("instant.haste", EffectHaste(), "nw_bandit001", DURATION_TYPE_INSTANT, NoFx());
    Probe("instant.paralyze", EffectParalyze(), "nw_bandit001", DURATION_TYPE_INSTANT, NoFx());
    Probe("instant.abildec", EffectAbilityDecrease(ABILITY_STRENGTH, 2), "nw_bandit001", DURATION_TYPE_INSTANT, NoFx());
    Probe("instant.curse", EffectCurse(2, 2, 2, 2, 2, 2), "nw_bandit001", DURATION_TYPE_INSTANT, NoFx());
    Probe("instant.acdec", EffectACDecrease(2), "nw_bandit001", DURATION_TYPE_INSTANT, NoFx());
    Probe("instant.link", EffectLinkEffects(EffectACIncrease(1), EffectSkillIncrease(SKILL_HIDE, 3)), "nw_bandit001", DURATION_TYPE_INSTANT, NoFx());

    // Reported integers.
    Ints("ac.vsalign", VersusAlignmentEffect(EffectACIncrease(2), ALIGNMENT_LAWFUL, ALIGNMENT_EVIL));
    Ints("ac.vsrace", VersusRacialTypeEffect(EffectACIncrease(2, AC_NATURAL_BONUS), RACIAL_TYPE_UNDEAD));
    Ints("ac.vsboth", VersusRacialTypeEffect(VersusAlignmentEffect(EffectACIncrease(1), ALIGNMENT_NEUTRAL, ALIGNMENT_GOOD), RACIAL_TYPE_HUMANOID_ORC));
    Ints("save.vsrace", VersusRacialTypeEffect(EffectSavingThrowIncrease(SAVING_THROW_WILL, 2, SAVING_THROW_TYPE_FEAR), RACIAL_TYPE_UNDEAD));
    Ints("save.vsalign", VersusAlignmentEffect(EffectSavingThrowIncrease(SAVING_THROW_ALL, 1), ALIGNMENT_CHAOTIC, ALIGNMENT_ALL));
    Ints("attack.vsalign", VersusAlignmentEffect(EffectAttackIncrease(2), ALIGNMENT_ALL, ALIGNMENT_EVIL));
    Ints("damage.vsrace", VersusRacialTypeEffect(EffectDamageIncrease(3, DAMAGE_TYPE_DIVINE), RACIAL_TYPE_UNDEAD));
    Ints("skill.vsrace", VersusRacialTypeEffect(EffectSkillIncrease(SKILL_SPOT, 2), RACIAL_TYPE_ELF));
    Ints("immunity.vsalign", VersusAlignmentEffect(EffectImmunity(IMMUNITY_TYPE_MIND_SPELLS), ALIGNMENT_ALL, ALIGNMENT_EVIL));
    Ints("concealment.vsrace", VersusRacialTypeEffect(EffectConcealment(20), RACIAL_TYPE_DWARF));
    Ints("concealment.ranged", EffectConcealment(30, MISS_CHANCE_TYPE_VS_RANGED));
    Ints("concealment.melee", EffectConcealment(40, MISS_CHANCE_TYPE_VS_MELEE));
    Ints("misschance.melee", EffectMissChance(20, MISS_CHANCE_TYPE_VS_MELEE));
    Ints("invis.vsalign", VersusAlignmentEffect(EffectInvisibility(INVISIBILITY_TYPE_NORMAL), ALIGNMENT_LAWFUL, ALIGNMENT_ALL));
    Ints("invis.darkness", EffectInvisibility(INVISIBILITY_TYPE_DARKNESS));
    Ints("dmgres.vsalign", VersusAlignmentEffect(EffectDamageResistance(DAMAGE_TYPE_FIRE, 5), ALIGNMENT_ALL, ALIGNMENT_EVIL));
    Ints("dmgres.ranged", EffectDamageResistance(DAMAGE_TYPE_SLASHING, 5, 0, TRUE));
    Ints("dmgred.ranged", EffectDamageReduction(5, DAMAGE_POWER_PLUS_TWO, 0, TRUE));
    Ints("polymorph.full", EffectPolymorph(POLYMORPH_TYPE_WEREWOLF, TRUE, 100, 2, 7));
    Ints("polymorph.default", EffectPolymorph(POLYMORPH_TYPE_WEREWOLF));
    Ints("damage.magic", EffectDamage(4));
    Ints("damage.bludg", EffectDamage(6, DAMAGE_TYPE_BLUDGEONING, DAMAGE_POWER_PLUS_THREE));
    Ints("damage.sonic", EffectDamage(2, DAMAGE_TYPE_SONIC));
    Ints("death.spect", EffectDeath(TRUE, FALSE));
    Ints("summon", EffectSummonCreature("nw_dog", VFX_IMP_FLAME_S, 1.5, 1));
    Ints("sanctuary", EffectSanctuary(17));
    Ints("spellfail.arcane", EffectSpellFailure(30, SPELL_SCHOOL_EVOCATION, SPELL_FAILURE_TYPE_ARCANE));
    Ints("spellabsorb.school", EffectSpellLevelAbsorption(9, 0, SPELL_SCHOOL_NECROMANCY));
    Ints("dispelall", EffectDispelMagicAll(12));
    Ints("dispelbest", EffectDispelMagicBest());
    Ints("vfx.miss", EffectVisualEffect(VFX_IMP_FLAME_S, TRUE));
    Ints("swarm", EffectSwarm(TRUE, "nw_dog", "nw_bandit001"));
    Ints("disappear", EffectDisappear(2));
    Ints("appear", EffectAppear());
    Ints("resurrection", EffectResurrection());
    Ints("timestop", EffectTimeStop());
    Ints("abildec.wis", EffectAbilityDecrease(ABILITY_WISDOM, 4));
    Ints("abilinc.6", EffectAbilityIncrease(6, 2));
    Ints("abilinc.neg1", EffectAbilityIncrease(-1, 2));
    Ints("link.ac.skill", EffectLinkEffects(EffectACIncrease(1), EffectSkillIncrease(SKILL_HIDE, 3)));
    Ints("link.skill.ac", EffectLinkEffects(EffectSkillIncrease(SKILL_HIDE, 3), EffectACIncrease(1)));
    Ints("neglevel.pre", EffectNegativeLevel(3));
    Ints("extra.neglevel", ExtraordinaryEffect(EffectNegativeLevel(3)));
    Ints("super.ac", SupernaturalEffect(EffectACIncrease(1)));
    Ints("extra.ac", ExtraordinaryEffect(EffectACIncrease(1)));
    Ints("magical.disease", MagicalEffect(EffectDisease(DISEASE_FILTH_FEVER)));
    Ints("unyielding.ac", UnyieldingEffect(EffectACIncrease(1)));
    Ints("tag.ac", TagEffect(EffectACIncrease(1), "mytag"));
    Ints("spellid.ac", SetEffectSpellId(EffectACIncrease(1), 77));
    Ints("cl.ac", SetEffectCasterLevel(EffectACIncrease(1), 9));
    Ints("creator.ac", SetEffectCreator(EffectACIncrease(1), OBJECT_INVALID));

    DelayCommand(4.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
