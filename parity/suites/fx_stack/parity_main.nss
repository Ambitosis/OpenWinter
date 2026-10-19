// Stacking: AC types, same spell and same creator, increases against decreases, and the bonus
// limits (Get/Set*BonusLimit) on abilities, saves, skills and AC.
#include "parity_fx"

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

effect NoFx() { return EffectHeal(-1); }

// Applies up to four effects (invalid ones are skipped) and logs the stat change.
void Stack(string sName, effect a, effect b, effect c, effect d, string sResRef = "nw_bandit001")
{
    object o = PFxSpawn(NextCell(), sResRef);
    PFxSnap(o);
    if (GetEffectType(a, TRUE) != 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, a, o);
    if (GetEffectType(b, TRUE) != 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, b, o);
    if (GetEffectType(c, TRUE) != 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, c, o);
    if (GetEffectType(d, TRUE) != 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, d, o);
    PLog("stack." + sName, PFxDiff(o) + " n=" + IntToString(PFxCount(o)));
}

effect Ac(int n, int t) { return EffectACIncrease(n, t); }
effect AcDec(int n, int t) { return EffectACDecrease(n, t); }

void AcTypes()
{
    Stack("dodge.2+3", Ac(2, AC_DODGE_BONUS), Ac(3, AC_DODGE_BONUS), NoFx(), NoFx());
    Stack("natural.2+3", Ac(2, AC_NATURAL_BONUS), Ac(3, AC_NATURAL_BONUS), NoFx(), NoFx());
    Stack("armour.4+2", Ac(4, AC_ARMOUR_ENCHANTMENT_BONUS), Ac(2, AC_ARMOUR_ENCHANTMENT_BONUS), NoFx(), NoFx());
    Stack("shield.1+3", Ac(1, AC_SHIELD_ENCHANTMENT_BONUS), Ac(3, AC_SHIELD_ENCHANTMENT_BONUS), NoFx(), NoFx());
    Stack("deflect.2+2", Ac(2, AC_DEFLECTION_BONUS), Ac(2, AC_DEFLECTION_BONUS), NoFx(), NoFx());
    Stack("mixed", Ac(2, AC_DODGE_BONUS), Ac(2, AC_NATURAL_BONUS), Ac(2, AC_DEFLECTION_BONUS), Ac(2, AC_ARMOUR_ENCHANTMENT_BONUS));
    Stack("dodge.cap", Ac(12, AC_DODGE_BONUS), Ac(12, AC_DODGE_BONUS), NoFx(), NoFx());
    Stack("natural.big", Ac(25, AC_NATURAL_BONUS), NoFx(), NoFx(), NoFx());
    Stack("dodge.dec", Ac(3, AC_DODGE_BONUS), AcDec(2, AC_DODGE_BONUS), NoFx(), NoFx());
    Stack("natural.dec", Ac(3, AC_NATURAL_BONUS), AcDec(2, AC_NATURAL_BONUS), NoFx(), NoFx());
    Stack("natural.dec.other", Ac(3, AC_NATURAL_BONUS), AcDec(2, AC_DODGE_BONUS), NoFx(), NoFx());
    Stack("dec.dec", AcDec(2, AC_DODGE_BONUS), AcDec(3, AC_NATURAL_BONUS), NoFx(), NoFx());
    Stack("dec.deflect.2+2", AcDec(2, AC_DEFLECTION_BONUS), AcDec(2, AC_DEFLECTION_BONUS), NoFx(), NoFx());
    Stack("dec.big", AcDec(30, AC_DODGE_BONUS), NoFx(), NoFx(), NoFx());
    // Armour worn by the bandit (leather) against armour enchantment effects.
    Stack("armour.vs.worn", Ac(1, AC_ARMOUR_ENCHANTMENT_BONUS), NoFx(), NoFx(), NoFx(), "nw_bandit006");
    Stack("natural.vs.template", Ac(1, AC_NATURAL_BONUS), NoFx(), NoFx(), NoFx(), "nw_ogre01");
    Stack("natural.vs.template3", Ac(3, AC_NATURAL_BONUS), NoFx(), NoFx(), NoFx(), "nw_ogre01");
}

void SameSpell()
{
    Stack("spell.same", SetEffectSpellId(Ac(2, AC_DODGE_BONUS), 50), SetEffectSpellId(Ac(2, AC_DODGE_BONUS), 50), NoFx(), NoFx());
    Stack("spell.same.str", SetEffectSpellId(EffectAbilityIncrease(ABILITY_STRENGTH, 4), 50), SetEffectSpellId(EffectAbilityIncrease(ABILITY_STRENGTH, 3), 50), NoFx(), NoFx());
    Stack("spell.diff.str", SetEffectSpellId(EffectAbilityIncrease(ABILITY_STRENGTH, 4), 50), SetEffectSpellId(EffectAbilityIncrease(ABILITY_STRENGTH, 3), 51), NoFx(), NoFx());
    Stack("nospell.str", EffectAbilityIncrease(ABILITY_STRENGTH, 4), EffectAbilityIncrease(ABILITY_STRENGTH, 3), NoFx(), NoFx());
    Stack("spell.same.save", SetEffectSpellId(EffectSavingThrowIncrease(SAVING_THROW_ALL, 2), 50), SetEffectSpellId(EffectSavingThrowIncrease(SAVING_THROW_ALL, 1), 50), NoFx(), NoFx());
    Stack("spell.same.skill", SetEffectSpellId(EffectSkillIncrease(SKILL_SPOT, 2), 50), SetEffectSpellId(EffectSkillIncrease(SKILL_SPOT, 5), 50), NoFx(), NoFx());
    Stack("spell.same.strdec", SetEffectSpellId(EffectAbilityDecrease(ABILITY_STRENGTH, 4), 50), SetEffectSpellId(EffectAbilityDecrease(ABILITY_STRENGTH, 3), 50), NoFx(), NoFx());
    Stack("spell.same.savedec", SetEffectSpellId(EffectSavingThrowDecrease(SAVING_THROW_ALL, 2), 50), SetEffectSpellId(EffectSavingThrowDecrease(SAVING_THROW_ALL, 3), 50), NoFx(), NoFx());
    Stack("spell.same.skilldec", SetEffectSpellId(EffectSkillDecrease(SKILL_SPOT, 2), 50), SetEffectSpellId(EffectSkillDecrease(SKILL_SPOT, 5), 50), NoFx(), NoFx());
    Stack("spell.same.acdec", SetEffectSpellId(AcDec(2, AC_DODGE_BONUS), 50), SetEffectSpellId(AcDec(3, AC_DODGE_BONUS), 50), NoFx(), NoFx());
    Stack("spell.same.sr", SetEffectSpellId(EffectSpellResistanceIncrease(12), 50), SetEffectSpellId(EffectSpellResistanceIncrease(15), 50), NoFx(), NoFx());
    Stack("spell.same.inc.dec", SetEffectSpellId(EffectAbilityIncrease(ABILITY_STRENGTH, 4), 50), SetEffectSpellId(EffectAbilityDecrease(ABILITY_STRENGTH, 2), 50), NoFx(), NoFx());
    Stack("spell.same.otherstat", SetEffectSpellId(EffectAbilityIncrease(ABILITY_STRENGTH, 4), 50), SetEffectSpellId(EffectAbilityIncrease(ABILITY_DEXTERITY, 2), 50), NoFx(), NoFx());
    Stack("spell.same.otherskill", SetEffectSpellId(EffectSkillIncrease(SKILL_SPOT, 4), 50), SetEffectSpellId(EffectSkillIncrease(SKILL_HIDE, 2), 50), NoFx(), NoFx());
    Stack("spell.same.saves", SetEffectSpellId(EffectSavingThrowIncrease(SAVING_THROW_FORT, 4), 50), SetEffectSpellId(EffectSavingThrowIncrease(SAVING_THROW_ALL, 2), 50), NoFx(), NoFx());
    Stack("dodge.dec.2+3", AcDec(2, AC_DODGE_BONUS), AcDec(3, AC_DODGE_BONUS), NoFx(), NoFx());
    Stack("spell.same.temphp", SetEffectSpellId(EffectTemporaryHitpoints(4), 50), SetEffectSpellId(EffectTemporaryHitpoints(6), 50), NoFx(), NoFx());
    Stack("temphp.4+6", EffectTemporaryHitpoints(4), EffectTemporaryHitpoints(6), NoFx(), NoFx());
    Stack("sr.10+15", EffectSpellResistanceIncrease(10), EffectSpellResistanceIncrease(15), NoFx(), NoFx());
    Stack("sr.15.dec5", EffectSpellResistanceIncrease(15), EffectSpellResistanceDecrease(5), NoFx(), NoFx());
    Stack("sr.dec.only", EffectSpellResistanceDecrease(5), NoFx(), NoFx(), NoFx(), "nw_drowrogue010");
    Stack("sr.inc.drow", EffectSpellResistanceIncrease(12), NoFx(), NoFx(), NoFx(), "nw_drowrogue010");
    Stack("sr.inc.drow25", EffectSpellResistanceIncrease(25), NoFx(), NoFx(), NoFx(), "nw_drowrogue010");
    Stack("turnres.2+3", EffectTurnResistanceIncrease(2), EffectTurnResistanceIncrease(3), NoFx(), NoFx());
    Stack("turnres.3-1", EffectTurnResistanceIncrease(3), EffectTurnResistanceDecrease(1), NoFx(), NoFx());
    Stack("immunity.twice", EffectImmunity(IMMUNITY_TYPE_MIND_SPELLS), EffectImmunity(IMMUNITY_TYPE_MIND_SPELLS), NoFx(), NoFx());
}

void Abilities()
{
    Stack("str.4+3", EffectAbilityIncrease(ABILITY_STRENGTH, 4), EffectAbilityIncrease(ABILITY_STRENGTH, 3), NoFx(), NoFx());
    Stack("str.8+8", EffectAbilityIncrease(ABILITY_STRENGTH, 8), EffectAbilityIncrease(ABILITY_STRENGTH, 8), NoFx(), NoFx());
    Stack("str.20", EffectAbilityIncrease(ABILITY_STRENGTH, 20), NoFx(), NoFx(), NoFx());
    Stack("str.4-2", EffectAbilityIncrease(ABILITY_STRENGTH, 4), EffectAbilityDecrease(ABILITY_STRENGTH, 2), NoFx(), NoFx());
    Stack("str.-2+4", EffectAbilityDecrease(ABILITY_STRENGTH, 2), EffectAbilityIncrease(ABILITY_STRENGTH, 4), NoFx(), NoFx());
    Stack("str.12-4", EffectAbilityIncrease(ABILITY_STRENGTH, 12), EffectAbilityIncrease(ABILITY_STRENGTH, 4), EffectAbilityDecrease(ABILITY_STRENGTH, 4), NoFx());
    Stack("str.-6-6", EffectAbilityDecrease(ABILITY_STRENGTH, 6), EffectAbilityDecrease(ABILITY_STRENGTH, 6), NoFx(), NoFx());
    Stack("str.-6-6+10", EffectAbilityDecrease(ABILITY_STRENGTH, 6), EffectAbilityDecrease(ABILITY_STRENGTH, 6), EffectAbilityIncrease(ABILITY_STRENGTH, 10), NoFx());
    Stack("str.+10-6-6", EffectAbilityIncrease(ABILITY_STRENGTH, 10), EffectAbilityDecrease(ABILITY_STRENGTH, 6), EffectAbilityDecrease(ABILITY_STRENGTH, 6), NoFx());
    Stack("con.curse", EffectCurse(0, 0, 4, 0, 0, 0), EffectAbilityIncrease(ABILITY_CONSTITUTION, 2), NoFx(), NoFx());
    Stack("save.15+10", EffectSavingThrowIncrease(SAVING_THROW_ALL, 15), EffectSavingThrowIncrease(SAVING_THROW_ALL, 10), NoFx(), NoFx());
    Stack("save.3-5", EffectSavingThrowIncrease(SAVING_THROW_ALL, 3), EffectSavingThrowDecrease(SAVING_THROW_ALL, 5), NoFx(), NoFx());
    Stack("save.-15-10", EffectSavingThrowDecrease(SAVING_THROW_FORT, 15), EffectSavingThrowDecrease(SAVING_THROW_FORT, 10), NoFx(), NoFx());
    Stack("save.fort+all", EffectSavingThrowIncrease(SAVING_THROW_FORT, 2), EffectSavingThrowIncrease(SAVING_THROW_ALL, 2), NoFx(), NoFx());
    Stack("skill.30+30", EffectSkillIncrease(SKILL_SPOT, 30), EffectSkillIncrease(SKILL_SPOT, 30), NoFx(), NoFx());
    Stack("skill.all+one", EffectSkillIncrease(SKILL_ALL_SKILLS, 2), EffectSkillIncrease(SKILL_SPOT, 3), NoFx(), NoFx());
    Stack("skill.4-6", EffectSkillIncrease(SKILL_SPOT, 4), EffectSkillDecrease(SKILL_SPOT, 6), NoFx(), NoFx());
    Stack("skill.-30-30", EffectSkillDecrease(SKILL_SPOT, 30), EffectSkillDecrease(SKILL_SPOT, 30), NoFx(), NoFx());
    Stack("neg.save.inc", EffectNegativeLevel(1), EffectSavingThrowIncrease(SAVING_THROW_ALL, 4), NoFx(), NoFx(), "nw_bandit006");
    Stack("neg.skill.inc", EffectNegativeLevel(1), EffectSkillIncrease(SKILL_SPOT, 4), NoFx(), NoFx(), "nw_bandit006");
    Stack("neg.save.cap", EffectNegativeLevel(5), EffectSavingThrowDecrease(SAVING_THROW_ALL, 18), NoFx(), NoFx(), "nw_bandit006");
}

void Limits()
{
    PLog("limit.defaults", IntToString(GetAbilityBonusLimit()) + " " + IntToString(GetAbilityPenaltyLimit()) + " "
        + IntToString(GetAttackBonusLimit()) + " " + IntToString(GetDamageBonusLimit()) + " "
        + IntToString(GetSavingThrowBonusLimit()) + " " + IntToString(GetSkillBonusLimit()));
    SetAbilityBonusLimit(5);
    SetAbilityPenaltyLimit(3);
    SetSavingThrowBonusLimit(3);
    SetSkillBonusLimit(4);
    PLog("limit.set", IntToString(GetAbilityBonusLimit()) + " " + IntToString(GetAbilityPenaltyLimit()) + " "
        + IntToString(GetSavingThrowBonusLimit()) + " " + IntToString(GetSkillBonusLimit()));
    Stack("limit.str.8", EffectAbilityIncrease(ABILITY_STRENGTH, 8), NoFx(), NoFx(), NoFx());
    Stack("limit.str.-6", EffectAbilityDecrease(ABILITY_STRENGTH, 6), NoFx(), NoFx(), NoFx());
    Stack("limit.save.6", EffectSavingThrowIncrease(SAVING_THROW_ALL, 6), NoFx(), NoFx(), NoFx());
    Stack("limit.save.-6", EffectSavingThrowDecrease(SAVING_THROW_ALL, 6), NoFx(), NoFx(), NoFx());
    Stack("limit.skill.9", EffectSkillIncrease(SKILL_SPOT, 9), NoFx(), NoFx(), NoFx());
    Stack("limit.skill.-9", EffectSkillDecrease(SKILL_SPOT, 9), NoFx(), NoFx(), NoFx());
    SetAbilityBonusLimit(-5);
    SetSkillBonusLimit(300);
    PLog("limit.odd", IntToString(GetAbilityBonusLimit()) + " " + IntToString(GetSkillBonusLimit()));
    SetAbilityBonusLimit(12);
    SetAbilityPenaltyLimit(30);
    SetSavingThrowBonusLimit(20);
    SetSkillBonusLimit(50);
}

// The same spell id from two different creators.
void ByCreator(object oMaker, object oTarget, int nAmount)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, SetEffectSpellId(EffectAbilityIncrease(ABILITY_STRENGTH, nAmount), 50), oTarget);
}

void Creators()
{
    object a = PFxSpawn(NextCell());
    object b = PFxSpawn(NextCell());
    object t = PFxSpawn(NextCell());
    PFxSnap(t);
    AssignCommand(a, ByCreator(a, t, 4));
    AssignCommand(b, ByCreator(b, t, 3));
    DelayCommand(0.3, PLog("stack.spell.same.twocreators", PFxDiff(t)));
}

void Run()
{
    AcTypes();
    DelayCommand(0.4, Creators());
    DelayCommand(0.1, SameSpell());
    DelayCommand(0.2, Abilities());
    DelayCommand(0.3, Limits());
    DelayCommand(2.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
