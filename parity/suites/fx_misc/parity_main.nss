// Dispelling (EffectDispelMagicAll/Best made by a creature or by the module, which effects go;
// fx_time shows they go on the next update, not at once),
// movement speed effects measured as distance walked, and concealment/miss chance stacking measured
// as touch attack hit rates.
#include "parity_fx"

const int N = 800;

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

object Make(string sResRef = "nw_bandit001")
{
    object o = PFxSpawn(NextCell(), sResRef);
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    return o;
}

effect Skill(int nSkill, int nSpell, int nLevel, string sTag)
{
    effect e = EffectSkillIncrease(nSkill, 1);
    if (nSpell >= 0) e = SetEffectSpellId(e, nSpell);
    if (nLevel >= 0) e = SetEffectCasterLevel(e, nLevel);
    return TagEffect(e, sTag);
}

// Buffs of several kinds made by oMaker on oTarget.
void Kit(object oTarget)
{
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, Skill(SKILL_SPOT, -1, -1, "nospell"), oTarget);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, Skill(SKILL_SPOT, SPELL_BLESS, 1, "cl1"), oTarget);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, Skill(SKILL_HIDE, SPELL_HASTE, 5, "cl5"), oTarget);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, Skill(SKILL_LISTEN, SPELL_STONESKIN, 3, "cl3"), oTarget);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, ExtraordinaryEffect(Skill(SKILL_LISTEN, SPELL_AID, 2, "extra")), oTarget);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(Skill(SKILL_LISTEN, SPELL_AID, 2, "super")), oTarget);
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, Skill(SKILL_SPOT, SPELL_MAGE_ARMOR, 4, "temp"), oTarget, 300.0);
}

string Tags(object o)
{
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { s += GetEffectTag(e) + " "; e = GetNextEffect(o); }
    return s == "" ? "-" : s;
}

void KitMadeBy(object oMaker, object oTarget)
{
    AssignCommand(oMaker, Kit(oTarget));
}

// The same buffs in the opposite order.
void KitReversed(object oTarget)
{
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, Skill(SKILL_SPOT, SPELL_MAGE_ARMOR, 4, "temp"), oTarget, 300.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, Skill(SKILL_LISTEN, SPELL_STONESKIN, 3, "cl3"), oTarget);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, Skill(SKILL_HIDE, SPELL_HASTE, 5, "cl5"), oTarget);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, Skill(SKILL_SPOT, SPELL_BLESS, 1, "cl1"), oTarget);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, Skill(SKILL_SPOT, -1, -1, "nospell"), oTarget);
}

void Dispel(string sKey, object oMaker, effect eDispel, object oKitMaker, int bReversed = FALSE, float fDelay = 0.2)
{
    object t = Make();
    if (bReversed) KitReversed(t);
    else if (GetIsObjectValid(oKitMaker)) KitMadeBy(oKitMaker, t);
    else Kit(t);
    if (GetIsObjectValid(oMaker)) DelayCommand(fDelay, AssignCommand(oMaker, ApplyEffectToObject(DURATION_TYPE_INSTANT, eDispel, t)));
    else if (fDelay > 0.0) DelayCommand(fDelay, ApplyEffectToObject(DURATION_TYPE_INSTANT, eDispel, t));
    else ApplyEffectToObject(DURATION_TYPE_INSTANT, eDispel, t);
    DelayCommand(0.4, PLog("dispel." + sKey, Tags(t)));
}

void Mixed(string sKey, effect eExtra, effect eDispel)
{
    object t = Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, eExtra, t);
    Kit(t);
    DelayCommand(0.2, ApplyEffectToObject(DURATION_TYPE_INSTANT, eDispel, t));
    DelayCommand(0.4, PLog("dispel." + sKey, Tags(t)));
}

void Dispels()
{
    Mixed("with.supernatural.paralysis", TagEffect(SupernaturalEffect(EffectParalyze()), "para"), EffectDispelMagicAll(60));
    Mixed("with.magical.paralysis", TagEffect(EffectParalyze(), "mpara"), EffectDispelMagicAll(60));
    object c = Make("nw_drowmage010");
    object k = Make("nw_bandit006");
    // A caster level that always wins (d20 + 60 against 11 + caster level).
    Dispel("all.creature", c, EffectDispelMagicAll(60), OBJECT_INVALID);
    Dispel("all.module", OBJECT_INVALID, EffectDispelMagicAll(60), OBJECT_INVALID);
    Dispel("best.creature", c, EffectDispelMagicBest(60), OBJECT_INVALID);
    Dispel("best.module", OBJECT_INVALID, EffectDispelMagicBest(60), OBJECT_INVALID);
    Dispel("all.creature.kitbycreature", c, EffectDispelMagicAll(60), k);
    Dispel("best.creature.kitbycreature", c, EffectDispelMagicBest(60), k);
    Dispel("best.creature.reversed", c, EffectDispelMagicBest(60), OBJECT_INVALID, TRUE);
    Dispel("all.module.sameframe", OBJECT_INVALID, EffectDispelMagicAll(60), OBJECT_INVALID, FALSE, 0.0);
    // The creature's own level (USE_CREATURE_LEVEL).
    Dispel("all.creature.ownlevel", c, EffectDispelMagicAll(), k);
}

// Distance walked in 3 seconds towards a point 30 m away.
void Walk(string sKey, effect e, int bApply, float fY)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", PCentre(-18.0, 32.0 + fY));
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    if (bApply) ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
    vector v = GetPosition(o);
    AssignCommand(o, ActionMoveToLocation(PCentre(14.0, 32.0 + fY), FALSE));
    DelayCommand(3.0, PLogF("walk." + sKey, VectorMagnitude(GetPosition(o) - v), 0.6));
    DelayCommand(3.0, PLogI("walk." + sKey + ".rate", GetMovementRate(o)));
}

void Walks()
{
    Walk("plain", EffectHaste(), FALSE, -15.0);
    Walk("speed+50", EffectMovementSpeedIncrease(50), TRUE, -12.0);
    Walk("speed-50", EffectMovementSpeedDecrease(50), TRUE, -9.0);
    Walk("speed+200", EffectMovementSpeedIncrease(200), TRUE, -6.0);
    Walk("speed-95", EffectMovementSpeedDecrease(95), TRUE, -3.0);
    Walk("haste", EffectHaste(), TRUE, 0.0);
    Walk("slow", EffectSlow(), TRUE, 3.0);
    Walk("entangle", EffectEntangle(), TRUE, 6.0);
    Walk("speed+50-50", EffectLinkEffects(EffectMovementSpeedIncrease(50), EffectMovementSpeedDecrease(50)), TRUE, 9.0);
    Walk("haste+speed50", EffectLinkEffects(EffectHaste(), EffectMovementSpeedIncrease(50)), TRUE, 12.0);
    Walk("forcewalk", EffectForceWalk(), TRUE, 15.0);
}

void Touch(string sKey, object oAttacker, object oTarget)
{
    int i, nHit = 0;
    for (i = 0; i < N; i++) if (TouchAttackMelee(oTarget, FALSE, oAttacker) > 0) nHit++;
    PLogF("touch." + sKey, IntToFloat(nHit) / IntToFloat(N), 0.09);
}

void Concealment()
{
    object a = Make();
    object t1 = Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectConcealment(20), t1);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectConcealment(50), t1);
    Touch("conceal.20+50", a, t1);
    object t2 = Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectConcealment(50, MISS_CHANCE_TYPE_VS_RANGED), t2);
    Touch("conceal.ranged.vs.melee", a, t2);
    object t3 = Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectConcealment(50, MISS_CHANCE_TYPE_VS_MELEE), t3);
    Touch("conceal.melee", a, t3);
    object b = Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectMissChance(50), b);
    Touch("misschance.attacker", b, Make());
    object b2 = Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectMissChance(50), b2);
    object t4 = Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectConcealment(50), t4);
    Touch("misschance.and.conceal", b2, t4);
    object s = Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectTrueSeeing(), s);
    object t5 = Make();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectConcealment(50), t5);
    Touch("trueseeing.vs.conceal", s, t5);
}

void Run()
{
    Dispels();
    DelayCommand(0.5, Walks());
    DelayCommand(1.0, Concealment());
    DelayCommand(5.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
