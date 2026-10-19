// Versus filters and immunities: which effects an immunity keeps off (also racial ones and versus
// alignment ones), GetIsImmune against a given object, saving throw bonuses versus alignment and
// race (WillSave with oSaveVersus), and attack/AC effects measured as touch attack hit rates.
#include "parity_fx"

const int N = 800;

int NextCell()
{
    int n = GetLocalInt(GetModule(), "cell");
    SetLocalInt(GetModule(), "cell", n + 1);
    return n;
}

object Make(string sResRef)
{
    object o = PFxSpawn(NextCell(), sResRef);
    int i;
    for (i = 5000; i <= 5012; i++) if (i != 5008) SetEventScript(o, i, "");
    return o;
}

object Aligned(string sResRef, int nAlign)
{
    object o = Make(sResRef);
    AdjustAlignment(o, nAlign, 100, FALSE);
    if (nAlign == ALIGNMENT_GOOD || nAlign == ALIGNMENT_EVIL) AdjustAlignment(o, ALIGNMENT_NEUTRAL, 0, FALSE);
    return o;
}

// Which of a set of effects land on a creature with the given immunity (or racial immunities).
string Lands(object o)
{
    string s = "";
    int i;
    for (i = 0; i < 26; i++)
    {
        effect e;
        switch (i)
        {
            case 0: e = EffectSleep(); break;
            case 1: e = EffectCharmed(); break;
            case 2: e = EffectDazed(); break;
            case 3: e = EffectFrightened(); break;
            case 4: e = EffectConfused(); break;
            case 5: e = EffectDominated(); break;
            case 6: e = EffectStunned(); break;
            case 7: e = EffectParalyze(); break;
            case 8: e = EffectPoison(POISON_NIGHTSHADE); break;
            case 9: e = EffectDisease(DISEASE_FILTH_FEVER); break;
            case 10: e = EffectAbilityDecrease(ABILITY_STRENGTH, 1); break;
            case 11: e = EffectCurse(); break;
            case 12: e = EffectNegativeLevel(1); break;
            case 13: e = EffectKnockdown(); break;
            case 14: e = EffectSlow(); break;
            case 15: e = EffectMovementSpeedDecrease(10); break;
            case 16: e = EffectEntangle(); break;
            case 17: e = EffectBlindness(); break;
            case 18: e = EffectDeaf(); break;
            case 19: e = EffectSilence(); break;
            case 20: e = EffectACDecrease(1); break;
            case 21: e = EffectAttackDecrease(1); break;
            case 22: e = EffectSavingThrowDecrease(SAVING_THROW_ALL, 1); break;
            case 23: e = EffectSkillDecrease(SKILL_SPOT, 1); break;
            case 24: e = EffectDamageImmunityDecrease(DAMAGE_TYPE_FIRE, 10); break;
            case 25: e = EffectPetrify(); break;
        }
        // Poison and disease take a fortitude save to land (see fx_poison): not compared here.
        if (i == 8 || i == 9) { s += "-"; continue; }
        int nBefore = PFxCount(o);
        ApplyEffectToObject(DURATION_TYPE_TEMPORARY, e, o, 30.0);
        s += PFxCount(o) > nBefore ? "1" : "0";
        // Take it off again so it doesn't affect the next one.
        effect x = GetFirstEffect(o);
        while (GetIsEffectValid(x)) { if (GetEffectDurationType(x) == DURATION_TYPE_TEMPORARY) RemoveEffect(o, x); x = GetNextEffect(o); }
    }
    return s;
}

void Immune(string sKey, int nImmunity)
{
    object o = Make("nw_bandit001");
    if (nImmunity >= 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(nImmunity), o);
    PLog("lands." + sKey, Lands(o));
}

void Immunities()
{
    Immune("none", -1);
    Immune("mind", IMMUNITY_TYPE_MIND_SPELLS);
    Immune("paralysis", IMMUNITY_TYPE_PARALYSIS);
    Immune("poison", IMMUNITY_TYPE_POISON);
    Immune("disease", IMMUNITY_TYPE_DISEASE);
    Immune("abilitydec", IMMUNITY_TYPE_ABILITY_DECREASE);
    Immune("negative", IMMUNITY_TYPE_NEGATIVE_LEVEL);
    Immune("knockdown", IMMUNITY_TYPE_KNOCKDOWN);
    Immune("slow", IMMUNITY_TYPE_SLOW);
    Immune("movedec", IMMUNITY_TYPE_MOVEMENT_SPEED_DECREASE);
    Immune("entangle", IMMUNITY_TYPE_ENTANGLE);
    Immune("fear", IMMUNITY_TYPE_FEAR);
    Immune("stun", IMMUNITY_TYPE_STUN);
    Immune("sleep", IMMUNITY_TYPE_SLEEP);
    Immune("cursed", IMMUNITY_TYPE_CURSED);
    Immune("blind", IMMUNITY_TYPE_BLINDNESS);
    Immune("acdec", IMMUNITY_TYPE_AC_DECREASE);
    Immune("savedec", IMMUNITY_TYPE_SAVING_THROW_DECREASE);
    Immune("skilldec", IMMUNITY_TYPE_SKILL_DECREASE);
    Immune("dmgimmdec", IMMUNITY_TYPE_DAMAGE_IMMUNITY_DECREASE);
    Immune("attackdec", IMMUNITY_TYPE_ATTACK_DECREASE);
    PLog("lands.undead", Lands(Make("nw_skeleton")));
    object e = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_PARALYSIS), e);
    int n = PFxCount(e);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, IgnoreEffectImmunity(EffectParalyze()), e);
    PLogI("lands.ignoreimmunity", PFxCount(e) - n);
    // Death effects against death immunity.
    object d = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectImmunity(IMMUNITY_TYPE_DEATH), d);
    ApplyEffectToObject(DURATION_TYPE_INSTANT, EffectDeath(), d);
    PLogI("death.immune.dead", GetIsDead(d));
}

void Versus()
{
    object oEvil = Aligned("nw_bandit001", ALIGNMENT_EVIL);
    object oGood = Aligned("nw_bandit001", ALIGNMENT_GOOD);
    PLog("align", IntToString(GetAlignmentGoodEvil(oEvil)) + " " + IntToString(GetAlignmentGoodEvil(oGood)));
    // Immunity versus evil: blocks effects an evil creature makes, not a good one's.
    object t = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, VersusAlignmentEffect(EffectImmunity(IMMUNITY_TYPE_MIND_SPELLS), ALIGNMENT_ALL, ALIGNMENT_EVIL), t);
    PLog("isimmune.vsevil", IntToString(GetIsImmune(t, IMMUNITY_TYPE_MIND_SPELLS, oEvil)) + IntToString(GetIsImmune(t, IMMUNITY_TYPE_MIND_SPELLS, oGood))
        + IntToString(GetIsImmune(t, IMMUNITY_TYPE_MIND_SPELLS)));
    int n = PFxCount(t);
    AssignCommand(oEvil, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSleep(), t));
    DelayCommand(0.2, PLogI("sleep.byevil", PFxCount(t) - n));
    DelayCommand(0.3, AssignCommand(oGood, ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCharmed(), t)));
    DelayCommand(0.5, PLogI("charm.bygood", PFxCount(t) - n));
    object r = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, VersusRacialTypeEffect(EffectImmunity(IMMUNITY_TYPE_PARALYSIS), RACIAL_TYPE_UNDEAD), r);
    PLog("isimmune.vsundead", IntToString(GetIsImmune(r, IMMUNITY_TYPE_PARALYSIS, Make("nw_skeleton"))) + IntToString(GetIsImmune(r, IMMUNITY_TYPE_PARALYSIS, oGood)));
}

void Rate(string sKey, object o, int nDC, object oVersus)
{
    int i, nOk = 0;
    for (i = 0; i < N; i++) if (WillSave(o, nDC, SAVING_THROW_TYPE_NONE, oVersus) == 1) nOk++;
    PLogF("save." + sKey, IntToFloat(nOk) / IntToFloat(N), 0.09);
}

void Saves()
{
    object oEvil = Aligned("nw_bandit001", ALIGNMENT_EVIL);
    object oGood = Aligned("nw_bandit001", ALIGNMENT_GOOD);
    object oUndead = Make("nw_skeleton");
    object t = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, VersusAlignmentEffect(EffectSavingThrowIncrease(SAVING_THROW_ALL, 10), ALIGNMENT_ALL, ALIGNMENT_EVIL), t);
    Rate("vsevil.evil", t, 16, oEvil);
    Rate("vsevil.good", t, 16, oGood);
    Rate("vsevil.none", t, 16, OBJECT_INVALID);
    object u = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, VersusRacialTypeEffect(EffectSavingThrowIncrease(SAVING_THROW_WILL, 10), RACIAL_TYPE_UNDEAD), u);
    Rate("vsundead.undead", u, 16, oUndead);
    Rate("vsundead.evil", u, 16, oEvil);
    object p = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, VersusAlignmentEffect(EffectSavingThrowDecrease(SAVING_THROW_ALL, 10), ALIGNMENT_ALL, ALIGNMENT_GOOD), p);
    Rate("penalty.vsgood.good", p, 6, oGood);
    Rate("penalty.vsgood.evil", p, 6, oEvil);
}

// Touch attack hit rate of oAttacker against oTarget.
void Touch(string sKey, object oAttacker, object oTarget)
{
    int i, nHit = 0;
    for (i = 0; i < N; i++) if (TouchAttackMelee(oTarget, FALSE, oAttacker) > 0) nHit++;
    PLogF("touch." + sKey, IntToFloat(nHit) / IntToFloat(N), 0.09);
}

void Attacks()
{
    object a = Make("nw_bandit001");
    object t = Make("nw_bandit001");
    Touch("base", a, t);
    object a2 = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(5), a2);
    Touch("attack+5", a2, t);
    object a3 = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackDecrease(4), a3);
    Touch("attack-4", a3, t);
    object a4 = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAbilityIncrease(ABILITY_STRENGTH, 6), a4);
    Touch("str+6", a4, t);
    object t2 = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(6, AC_DODGE_BONUS), t2);
    Touch("dodge+6", a, t2);
    object t3 = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(6, AC_NATURAL_BONUS), t3);
    Touch("natural+6", a, t3);
    object t4 = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(6, AC_DEFLECTION_BONUS), t4);
    Touch("deflect+6", a, t4);
    object t5 = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(6, AC_ARMOUR_ENCHANTMENT_BONUS), t5);
    Touch("armour+6", a, t5);
    // Versus filters on AC and attack.
    object oUndead = Make("nw_skeleton");
    object t6 = Make("nw_bandit001");
    // Deflection: a dodge bonus would depend on whether the target has noticed the attacker yet.
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, VersusRacialTypeEffect(EffectACIncrease(8, AC_DEFLECTION_BONUS), RACIAL_TYPE_UNDEAD), t6);
    Touch("acvsundead.undead", oUndead, t6);
    Touch("acvsundead.human", a, t6);
    Touch("skeleton.base", oUndead, t);
    object a5 = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, VersusRacialTypeEffect(EffectAttackIncrease(8), RACIAL_TYPE_UNDEAD), a5);
    Touch("attackvsundead.undead", a5, oUndead);
    Touch("attackvsundead.human", a5, t);
    Touch("human.vs.skeleton", a, oUndead);
}

void Caps()
{
    object t = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(20, AC_DEFLECTION_BONUS), t);
    object a = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(15), a);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(13), a);
    Touch("attack.cap", a, t);
    object a2 = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(15), a2);
    Touch("attack.15", a2, t);
    // States of the target and the attacker.
    object b = Make("nw_bandit001");
    object p = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectParalyze(), p);
    Touch("target.paralyzed", b, p);
    object c = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectConcealment(50), c);
    Touch("target.concealed", b, c);
    object bl = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectBlindness(), bl);
    Touch("attacker.blind", bl, Make("nw_bandit001"));
    object iv = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectInvisibility(INVISIBILITY_TYPE_NORMAL), iv);
    Touch("target.invisible", b, iv);
    object h = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectHaste(), h);
    Touch("attacker.hasted", h, Make("nw_bandit001"));
    object s = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSlow(), s);
    Touch("attacker.slowed", s, Make("nw_bandit001"));
    object f = Make("nw_bandit001");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectFrightened(), f);
    Touch("attacker.frightened", f, Make("nw_bandit001"));
    object n = Make("nw_bandit006");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectNegativeLevel(4), n);
    Touch("attacker.neglevel", n, Make("nw_bandit001"));
    Touch("attacker.barbarian", Make("nw_bandit006"), Make("nw_bandit001"));
}

void Run()
{
    Immunities();
    DelayCommand(0.5, Versus());
    DelayCommand(1.5, Saves());
    DelayCommand(2.5, Attacks());
    DelayCommand(3.5, Caps());
    DelayCommand(6.0, PDone());
}

void main()
{
    DelayCommand(2.0, Run());
}
