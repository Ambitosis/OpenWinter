#include "visual_inc"
// conditionfreeze: which conditions stop a creature's animation. Five bandits close to the camera (held,
// cutscene-paralysed, petrified, dazed, none); pairs of shots a quarter of a second apart, so a still pose shows
// as an unchanged picture (VISUAL notes give each creature's screen box). Then the real Sleep spell from a
// prepared drow mage (instant cast) on a bandit with -30 saves.

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|conditionfreeze|" + s); }

object Npc(string sResRef, float fDx, float fDy, string sTag, int nFaction)
{
    object pc = GetFirstPC();
    vector v = GetPosition(pc);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(GetArea(pc), Vector(v.x + fDx, v.y + fDy, v.z), 270.0), FALSE, sTag);
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, nFaction);
    return o;
}

void Afflict(object o, int i)
{
    effect e;
    if (i == 0) e = EffectParalyze();
    else if (i == 1) e = EffectCutsceneParalyze();
    else if (i == 2) e = EffectPetrify();
    else if (i == 3) e = EffectDazed();
    else if (i == 4) e = EffectStunned();
    else return;
    AssignCommand(GetObjectByTag("cf_caster"), ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o));
}

void Has(string sTag)
{
    object o = GetObjectByTag(sTag);
    string s = "";
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { s += IntToString(GetEffectType(e)) + "/" + IntToString(GetEffectSpellId(e)) + " "; e = GetNextEffect(o); }
    Log(sTag + "|" + s);
}

void main()
{
    object pc = GetEnteringObject();
    if (!GetIsPC(pc)) return;
    VBegin(pc);
    SetPlotFlag(pc, FALSE);
    Npc("nw_bandit001", 0.0, -14.0, "cf_caster", STANDARD_FACTION_HOSTILE);
    int i;
    for (i = 0; i < 6; i++)
    {
        object o = Npc("nw_bandit001", -3.0 + IntToFloat(i) * 1.2, 3.0, "cf_" + IntToString(i), STANDARD_FACTION_MERCHANT);
        DelayCommand(2.0, Afflict(o, i));
    }
    DelayCommand(1.0, VCamera(pc, 90.0, 5.5, 70.0));
    // creatures 0..5: held, cutscene paralysed, petrified, dazed, stunned, none (left to right)
    DelayCommand(4.0, VShot("a_freeze_1"));
    DelayCommand(4.25, VShot("a_freeze_2"));
    DelayCommand(5.5, VShot("a_freeze_3"));
    DelayCommand(5.75, VShot("a_freeze_4"));
    DelayCommand(7.0, VShot("a_freeze_5"));
    DelayCommand(7.5, VShot("a_freeze_6"));
    for (i = 0; i < 6; i++) DelayCommand(9.0, DestroyObject(GetObjectByTag("cf_" + IntToString(i))));
    object mage = Npc("nw_drowmage020", -3.0, 6.0, "cf_mage", STANDARD_FACTION_MERCHANT);
    object sleeper = Npc("nw_bandit001", 1.0, 3.0, "cf_sleeper", STANDARD_FACTION_HOSTILE);
    SetIsTemporaryEnemy(mage, sleeper);
    SetIsTemporaryEnemy(sleeper, mage);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSavingThrowDecrease(SAVING_THROW_ALL, 30), sleeper);
    SetMemorizedSpell(mage, CLASS_TYPE_WIZARD, 1, 0, SPELL_SLEEP, TRUE, METAMAGIC_NONE);
    DelayCommand(10.0, VCamera(pc, 90.0, 9.0, 60.0));
    DelayCommand(12.0, AssignCommand(mage, ActionCastSpellAtObject(SPELL_SLEEP, sleeper, METAMAGIC_NONE, FALSE, 0, PROJECTILE_PATH_TYPE_DEFAULT, TRUE, CLASS_TYPE_WIZARD)));
    DelayCommand(12.4, VShot("b_sleep_1"));
    DelayCommand(13.0, VShot("b_sleep_2"));
    DelayCommand(14.0, VShot("b_sleep_3"));
    DelayCommand(16.0, VShot("b_sleep_4"));
    DelayCommand(16.1, Has("cf_sleeper"));
    // The player knocked down for 4 s, as in conditions (p04).
    DelayCommand(18.0, VCamera(pc, 90.0, 6.0, 60.0));
    DelayCommand(19.0, AssignCommand(GetObjectByTag("cf_caster"), ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectKnockdown(), pc, 4.0)));
    DelayCommand(19.6, VShot("c_pckd_1"));
    DelayCommand(20.5, VShot("c_pckd_2"));
    DelayCommand(21.5, VShot("c_pckd_3"));
    DelayCommand(23.5, VShot("c_pckd_4"));
    DelayCommand(25.0, VShot("c_pckd_5"));
    DelayCommand(27.0, VDone());
}
