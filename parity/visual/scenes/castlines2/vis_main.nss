#include "visual_inc"
// castlines2 (after castlines): the Spellcraft roll behind "X casting <spell>" versus "unknown spell" (Barkskin, an
// innate level 2 spell, cast by a commoner 5 m away, ten times at each Spellcraft rank: 2, 7, 12 and 17), how far the
// two lines carry (26 to 38 m at Spellcraft 57, where every spell is named), and hostile, item and spell-ability casts
// by casters that have had time to see their target. Every case is announced in the chat window ("CL <case>").

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|cl|" + s); }
void Mark(string sCase) { SendMessageToPC(GetFirstPC(), "CL " + sCase); Log("case|" + sCase); }

object At(string sResRef, float fDistance, float fAngle, string sName, int nFaction)
{
    object pc = GetFirstPC();
    location l = Location(GetArea(pc), Vector(10.0 + fDistance * cos(fAngle), 10.0 + fDistance * sin(fAngle), 0.0), fAngle + 180.0);
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, l, FALSE, "cl_" + sName);
    if (nFaction >= 0) ChangeToStandardFaction(o, nFaction);
    SetName(o, sName);
    SetPlotFlag(o, TRUE);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

object Dummy() { return GetObjectByTag("cl_Dummy"); }

void CastAt(object o, int nSpell, object oTarget, int bInstant)
{
    AssignCommand(o, ClearAllActions(TRUE));
    AssignCommand(o, ActionCastSpellAtObject(nSpell, oTarget, METAMAGIC_ANY, TRUE, 0, PROJECTILE_PATH_TYPE_DEFAULT, bInstant));
}

// A commoner at fDistance casts nSpell: on itself (bSelf) or at the dummy, fWait seconds after it appears.
void Npc(string sCase, float fDistance, float fAngle, int nSpell, int bSelf, float fWait)
{
    Mark(sCase);
    object o = At("nw_bandit001", fDistance, fAngle, sCase, STANDARD_FACTION_COMMONER);
    DelayCommand(fWait, CastAt(o, nSpell, bSelf ? o : Dummy(), FALSE));
    DelayCommand(fWait + 3.2, DestroyObject(o));
}

void Potion(string sCase)
{
    Mark(sCase);
    object o = At("nw_bandit001", 5.0, 50.0, sCase, STANDARD_FACTION_COMMONER);
    object p = CreateItemOnObject("nw_it_mpotion001", o);
    itemproperty ip = GetFirstItemProperty(p);
    Log(sCase + "|item=" + GetName(p) + "|ipType=" + IntToString(GetItemPropertyType(ip)) + "|ipSub=" + IntToString(GetItemPropertySubType(ip)));
    DelayCommand(0.5, AssignCommand(o, ActionUseItemOnObject(p, ip, o)));
    DelayCommand(4.0, DestroyObject(o));
}

void SelfPotion(string sCase)
{
    object pc = GetFirstPC();
    Mark(sCase);
    object p = CreateItemOnObject("nw_it_mpotion001", pc);
    itemproperty ip = GetFirstItemProperty(p);
    DelayCommand(0.5, AssignCommand(pc, ActionUseItemOnObject(p, ip, pc)));
}

void Spellcraft(int nBonus)
{
    object pc = GetFirstPC();
    effect e = GetFirstEffect(pc);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_SKILL_INCREASE || GetEffectType(e) == EFFECT_TYPE_SKILL_DECREASE) RemoveEffect(pc, e);
        e = GetNextEffect(pc);
    }
    if (nBonus > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSkillIncrease(SKILL_SPELLCRAFT, nBonus)), pc);
    if (nBonus < 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSkillDecrease(SKILL_SPELLCRAFT, -nBonus)), pc);
    DelayCommand(0.2, Log("spellcraft|bonus=" + IntToString(nBonus) + "|rank=" + IntToString(GetSkillRank(SKILL_SPELLCRAFT, pc))));
    DelayCommand(0.2, Mark("spellcraft_rank_" + IntToString(GetSkillRank(SKILL_SPELLCRAFT, pc))));
}

float Trials(float t, int nBonus, string sTag)
{
    DelayCommand(t, Spellcraft(nBonus)); t += 1.0;
    int i;
    for (i = 1; i <= 10; i++)
    {
        DelayCommand(t, Npc(sTag + "_" + IntToString(i), 5.0, 30.0, SPELL_BARKSKIN, TRUE, 0.3)); t += 3.8;
    }
    return t;
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(pc, TRUE);
    DelayCommand(1.0, VCamera(pc, 225.0, 25.0, 45.0));
    object d = At("nw_goblina", 7.0, 90.0, "Dummy", STANDARD_FACTION_HOSTILE);
    SetPlotFlag(d, FALSE);
    SetImmortal(d, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), d);

    float t = 4.0;
    t = Trials(t, -5, "r2");
    t = Trials(t, 0, "r7");
    t = Trials(t, 5, "r12");
    t = Trials(t, 10, "r17");
    DelayCommand(t, Spellcraft(50)); t += 1.0;
    DelayCommand(t, Npc("d26", 26.0, 20.0, SPELL_BARKSKIN, TRUE, 0.3)); t += 4.0;
    DelayCommand(t, Npc("d28", 28.0, 20.0, SPELL_BARKSKIN, TRUE, 0.3)); t += 4.0;
    DelayCommand(t, Npc("d30", 30.0, 20.0, SPELL_BARKSKIN, TRUE, 0.3)); t += 4.0;
    DelayCommand(t, Npc("d32", 32.0, 20.0, SPELL_BARKSKIN, TRUE, 0.3)); t += 4.0;
    DelayCommand(t, Npc("d34", 34.0, 20.0, SPELL_BARKSKIN, TRUE, 0.3)); t += 4.0;
    DelayCommand(t, Npc("d36", 36.0, 20.0, SPELL_BARKSKIN, TRUE, 0.3)); t += 4.0;
    DelayCommand(t, Npc("d38", 38.0, 20.0, SPELL_BARKSKIN, TRUE, 0.3)); t += 4.0;
    DelayCommand(t, Npc("h5_mm_wait", 5.0, 30.0, SPELL_MAGIC_MISSILE, FALSE, 2.0)); t += 6.0;
    DelayCommand(t, Npc("h5_firebolt_wait", 5.0, 30.0, SPELLABILITY_BOLT_FIRE, FALSE, 2.0)); t += 6.0;
    DelayCommand(t, Potion("potion_npc")); t += 5.0;
    DelayCommand(t, SelfPotion("potion_self")); t += 5.0;
    DelayCommand(t, VShot("a_end")); t += 2.0;
    DelayCommand(t, Mark("end"));
    DelayCommand(t + 1.0, VDone());
    Log("total=" + FloatToString(t, 0, 0));
}
