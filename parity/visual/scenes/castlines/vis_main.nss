#include "visual_inc"
// castlines: who is told which spell-casting line ("X casting <spell>", "X casts <spell>", TLK 8127 / 1501 / 1502).
// One client: the player casts, creatures that aren't hers cast at 5 to 45 m, her henchman casts, with her Spellcraft
// as it is, +10 (repeated, to see whether it is a roll) and +50; cheat, instant, item and spell-ability casts.
// Every case is announced in the chat window ("CL <case>") and each caster is named after its case, so chat.txt
// shows which line each one printed.

void Log(string s) { WriteTimestampedLogEntry("VISUAL|note|cl|" + s); }
void Mark(string sCase) { SendMessageToPC(GetFirstPC(), "CL " + sCase); Log("case|" + sCase); }

// A creature at fDistance metres from the player's start, fAngle degrees round from east.
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

// The dummy every hostile spell is aimed at: paralysed, immortal, hostile.
object Dummy() { return GetObjectByTag("cl_Dummy"); }

void CastAt(object o, int nSpell, object oTarget, int bCheat, int bInstant)
{
    AssignCommand(o, ClearAllActions(TRUE));
    AssignCommand(o, ActionCastSpellAtObject(nSpell, oTarget, METAMAGIC_ANY, bCheat, 0, PROJECTILE_PATH_TYPE_DEFAULT, bInstant));
}

// A caster that is not hers (commoner) at fDistance casts nSpell (on itself for Barkskin, else at the dummy).
void Npc(string sCase, float fDistance, float fAngle, int nSpell, int bInstant)
{
    Mark(sCase);
    object o = At("nw_bandit001", fDistance, fAngle, sCase, STANDARD_FACTION_COMMONER);
    Log(sCase + "|dist=" + FloatToString(GetDistanceBetween(o, GetFirstPC()), 0, 2) + "|spell=" + IntToString(nSpell));
    DelayCommand(0.3, CastAt(o, nSpell, nSpell == SPELL_BARKSKIN ? o : Dummy(), TRUE, bInstant));
    DelayCommand(4.0, DestroyObject(o));
}

void Self(string sCase, int nSpell, int bCheat)
{
    object pc = GetFirstPC();
    Mark(sCase);
    Log(sCase + "|hasSpell=" + IntToString(GetHasSpell(nSpell, pc)));
    CastAt(pc, nSpell, nSpell == SPELL_BARKSKIN ? pc : Dummy(), bCheat, FALSE);
}

void Hench(string sCase, int nSpell)
{
    object h = GetObjectByTag("cl_Hench");
    Mark(sCase);
    Log(sCase + "|master=" + GetName(GetMaster(h)) + "|dist=" + FloatToString(GetDistanceBetween(h, GetFirstPC()), 0, 2));
    CastAt(h, nSpell, nSpell == SPELL_BARKSKIN ? h : Dummy(), TRUE, FALSE);
}

void Wand(string sCase)
{
    Mark(sCase);
    object o = At("nw_bandit001", 5.0, 50.0, sCase, STANDARD_FACTION_COMMONER);
    object w = CreateItemOnObject("nw_wmgwn013", o);
    itemproperty ip = GetFirstItemProperty(w);
    Log(sCase + "|wand=" + GetName(w) + "|ipType=" + IntToString(GetItemPropertyType(ip)) + "|ipSub=" + IntToString(GetItemPropertySubType(ip)));
    DelayCommand(0.5, AssignCommand(o, ActionUseItemOnObject(w, ip, Dummy())));
    DelayCommand(4.5, DestroyObject(o));
}

void Spellcraft(int nBonus)
{
    object pc = GetFirstPC();
    effect e = GetFirstEffect(pc);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_SKILL_INCREASE) RemoveEffect(pc, e);
        e = GetNextEffect(pc);
    }
    if (nBonus > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSkillIncrease(SKILL_SPELLCRAFT, nBonus)), pc);
    DelayCommand(0.2, Log("spellcraft|bonus=" + IntToString(nBonus) + "|rank=" + IntToString(GetSkillRank(SKILL_SPELLCRAFT, pc))
        + "|base=" + IntToString(GetSkillRank(SKILL_SPELLCRAFT, pc, TRUE))));
    DelayCommand(0.2, Mark("spellcraft_" + IntToString(nBonus)));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(pc, TRUE);
    SetMaxHenchmen(4);
    DelayCommand(1.0, VCamera(pc, 225.0, 25.0, 45.0));
    object d = At("nw_goblina", 7.0, 90.0, "Dummy", STANDARD_FACTION_HOSTILE);
    SetPlotFlag(d, FALSE);
    SetImmortal(d, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), d);
    object h = At("nw_bandit001", 4.0, 120.0, "Hench", STANDARD_FACTION_COMMONER);
    Log("start|spellcraft=" + IntToString(GetSkillRank(SKILL_SPELLCRAFT, pc)) + "|class=" + IntToString(GetClassByPosition(1, pc))
        + "|hasMM=" + IntToString(GetHasSpell(SPELL_MAGIC_MISSILE, pc)));

    float t = 5.0;
    // As she is.
    DelayCommand(t, Spellcraft(0)); t += 1.0;
    DelayCommand(t, Self("self_mm_cheat", SPELL_MAGIC_MISSILE, TRUE)); t += 5.0;
    DelayCommand(t, Self("self_bark_cheat", SPELL_BARKSKIN, TRUE)); t += 5.0;
    DelayCommand(t, Self("self_mm_real", SPELL_MAGIC_MISSILE, FALSE)); t += 5.0;
    DelayCommand(t, Npc("n5_mm", 5.0, 30.0, SPELL_MAGIC_MISSILE, FALSE)); t += 5.0;
    DelayCommand(t, Npc("n5_bark", 5.0, 30.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Npc("n5_bark_instant", 5.0, 30.0, SPELL_BARKSKIN, TRUE)); t += 5.0;
    DelayCommand(t, Npc("n5_firebolt_ability", 5.0, 30.0, SPELLABILITY_BOLT_FIRE, FALSE)); t += 5.0;
    DelayCommand(t, Wand("n5_wand")); t += 6.0;
    DelayCommand(t, Npc("n10_bark", 10.0, 20.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Npc("n20_bark", 20.0, 20.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Npc("n25_bark", 25.0, 20.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Npc("n30_bark", 30.0, 20.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Npc("n35_bark", 35.0, 20.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Npc("n40_bark", 40.0, 20.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Npc("n45_bark", 45.0, 20.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Mark("hench_join"));
    DelayCommand(t, AddHenchman(pc, h)); t += 1.0;
    DelayCommand(t, Hench("hench_bark", SPELL_BARKSKIN)); t += 5.0;
    DelayCommand(t, Hench("hench_mm", SPELL_MAGIC_MISSILE)); t += 5.0;
    DelayCommand(t, VShot("a_low")); t += 2.0;
    // Spellcraft +10, several times over, to see whether it is a roll.
    DelayCommand(t, Spellcraft(10)); t += 1.0;
    DelayCommand(t, Npc("s10_mm_1", 5.0, 30.0, SPELL_MAGIC_MISSILE, FALSE)); t += 5.0;
    DelayCommand(t, Npc("s10_bark_1", 5.0, 30.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Npc("s10_mm_2", 5.0, 30.0, SPELL_MAGIC_MISSILE, FALSE)); t += 5.0;
    DelayCommand(t, Npc("s10_bark_2", 5.0, 30.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Npc("s10_mm_3", 5.0, 30.0, SPELL_MAGIC_MISSILE, FALSE)); t += 5.0;
    DelayCommand(t, Npc("s10_bark_3", 5.0, 30.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Npc("s10_fireball_1", 5.0, 30.0, SPELL_FIREBALL, FALSE)); t += 5.0;
    // Spellcraft +50.
    DelayCommand(t, Spellcraft(50)); t += 1.0;
    DelayCommand(t, Npc("s50_mm", 5.0, 30.0, SPELL_MAGIC_MISSILE, FALSE)); t += 5.0;
    DelayCommand(t, Npc("s50_bark", 5.0, 30.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Npc("s50_bark_instant", 5.0, 30.0, SPELL_BARKSKIN, TRUE)); t += 5.0;
    DelayCommand(t, Npc("s50_firebolt_ability", 5.0, 30.0, SPELLABILITY_BOLT_FIRE, FALSE)); t += 5.0;
    DelayCommand(t, Wand("s50_wand")); t += 6.0;
    DelayCommand(t, Npc("s50_n20_bark", 20.0, 20.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Npc("s50_n45_bark", 45.0, 20.0, SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, Hench("s50_hench_bark", SPELL_BARKSKIN)); t += 5.0;
    DelayCommand(t, Self("s50_self_bark_cheat", SPELL_BARKSKIN, TRUE)); t += 5.0;
    DelayCommand(t, VShot("b_end")); t += 2.0;
    DelayCommand(t, Mark("end"));
    DelayCommand(t + 1.0, VDone());
    Log("total=" + FloatToString(t, 0, 0));
}
