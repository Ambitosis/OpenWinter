#include "visual_inc"
// castlines4 (after castlines3, where creatures' AI fought the hostile dummy and the player's items were never used):
// the dummy is a commoner now and every caster clears its actions in the same moment it is told to cast. At
// Spellcraft +50: a hostile goblin's spell and ability at the player; a commoner's spell at the dummy; a creature
// ability (spells.2da UserType 2, AltMessage "<name> is surrounded by an aura.") at 5 to 40 m; an NPC's potion at
// 25 to 35 m (the "uses" line reached 20 m and not 40 m); a fake cast; the player's potion, scroll and wand; a
// wizard's scroll and wand; summons (the Summon Familiar feat, SummonFamiliar, a summoning effect). Every case is
// announced in the chat window ("CL <case>").

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
void Action(object o, string sCase) { Log(sCase + "|action=" + IntToString(GetCurrentAction(o))); }

void CastNow(object o, int nSpell, object t, int bFake)
{
    AssignCommand(o, ClearAllActions(TRUE));
    if (bFake) AssignCommand(o, ActionCastFakeSpellAtObject(nSpell, t));
    else AssignCommand(o, ActionCastSpellAtObject(nSpell, t, METAMAGIC_ANY, TRUE));
}

// A caster at fDistance casts nSpell at the player, the dummy or itself a second after it appears.
void Cast(string sCase, string sResRef, int nFaction, float fDistance, int nSpell, int bAtPC, int bSelf, int bFake)
{
    Mark(sCase);
    object o = At(sResRef, fDistance, 30.0, sCase, nFaction);
    object t = bSelf ? o : (bAtPC ? GetFirstPC() : Dummy());
    DelayCommand(1.0, CastNow(o, nSpell, t, bFake));
    DelayCommand(1.3, Action(o, sCase));
    DelayCommand(5.0, DestroyObject(o));
}

void UseNow(object o, object it, object t)
{
    itemproperty ip = GetFirstItemProperty(it);
    AssignCommand(o, ClearAllActions(TRUE));
    AssignCommand(o, ActionUseItemOnObject(it, ip, t));
}

void UseAtNow(object o, object it, location l)
{
    itemproperty ip = GetFirstItemProperty(it);
    AssignCommand(o, ClearAllActions(TRUE));
    AssignCommand(o, ActionUseItemAtLocation(it, ip, l));
}

// An NPC uses an item (created on it) on itself, at the dummy or (bAtLocation) at a spot beside it.
void NpcItem(string sCase, string sResRef, string sItem, float fDistance, int bSelf, int bAtLocation)
{
    Mark(sCase);
    object o = At(sResRef, fDistance, 50.0, sCase, STANDARD_FACTION_COMMONER);
    object it = CreateItemOnObject(sItem, o);
    SetIdentified(it, TRUE);
    Log(sCase + "|item=" + GetName(it));
    if (bAtLocation) DelayCommand(1.0, UseAtNow(o, it, Location(GetArea(o), GetPosition(o) + Vector(1.5, 0.0, 0.0), 0.0)));
    else DelayCommand(1.0, UseNow(o, it, bSelf ? o : Dummy()));
    DelayCommand(1.3, Action(o, sCase));
    DelayCommand(6.0, DestroyObject(GetAssociate(ASSOCIATE_TYPE_SUMMONED, o)));
    DelayCommand(6.0, DestroyObject(o));
}

void PcItem(string sCase, string sItem, int bSelf, int bAtLocation)
{
    object pc = GetFirstPC();
    Mark(sCase);
    object it = CreateItemOnObject(sItem, pc);
    SetIdentified(it, TRUE);
    if (bAtLocation) DelayCommand(1.0, UseAtNow(pc, it, Location(GetArea(pc), GetPosition(pc) + Vector(1.5, 0.0, 0.0), 0.0)));
    else DelayCommand(1.0, UseNow(pc, it, bSelf ? pc : Dummy()));
    DelayCommand(1.3, Action(pc, sCase));
    DelayCommand(5.5, DestroyObject(GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc)));
}

void PcFeat(string sCase, int nFeat)
{
    object pc = GetFirstPC();
    Mark(sCase);
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, ActionUseFeat(nFeat, pc));
    DelayCommand(0.3, Action(pc, sCase));
}

void ScriptFamiliar(string sCase) { Mark(sCase); SummonFamiliar(GetFirstPC()); }
void ScriptSummon(string sCase)
{
    object pc = GetFirstPC();
    Mark(sCase);
    AssignCommand(pc, ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectSummonCreature("nw_s_badgerdire"), Location(GetArea(pc), GetPosition(pc) + Vector(2.0, 0.0, 0.0), 0.0), 60.0));
}
void Unsummon(string sCase)
{
    object pc = GetFirstPC();
    Mark(sCase);
    DestroyObject(GetAssociate(ASSOCIATE_TYPE_FAMILIAR, pc));
    DestroyObject(GetAssociate(ASSOCIATE_TYPE_SUMMONED, pc));
}

void Spellcraft(int nBonus)
{
    object pc = GetFirstPC();
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(EffectSkillIncrease(SKILL_SPELLCRAFT, nBonus)), pc);
    DelayCommand(0.2, Mark("spellcraft_rank_" + IntToString(GetSkillRank(SKILL_SPELLCRAFT, pc))));
}

void main()
{
    object pc = GetEnteringObject(); if (!GetIsPC(pc)) return;
    SetCameraMode(pc, CAMERA_MODE_TOP_DOWN);
    SetCameraLimits(pc, 1.0, 89.0, 1.0, 40.0);
    SetPlotFlag(pc, TRUE);
    DelayCommand(1.0, VCamera(pc, 225.0, 25.0, 45.0));
    object d = At("nw_goblina", 7.0, 90.0, "Dummy", STANDARD_FACTION_COMMONER);
    SetPlotFlag(d, FALSE);
    SetImmortal(d, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), d);

    float t = 4.0;
    DelayCommand(t, Spellcraft(50)); t += 1.0;
    DelayCommand(t, Cast("h_mm_pc", "nw_goblina", STANDARD_FACTION_HOSTILE, 5.0, SPELL_MAGIC_MISSILE, TRUE, FALSE, FALSE)); t += 5.5;
    DelayCommand(t, Cast("h_firebolt_pc", "nw_goblina", STANDARD_FACTION_HOSTILE, 5.0, SPELLABILITY_BOLT_FIRE, TRUE, FALSE, FALSE)); t += 5.5;
    DelayCommand(t, Cast("c_mm_dummy", "nw_bandit001", STANDARD_FACTION_COMMONER, 5.0, SPELL_MAGIC_MISSILE, FALSE, FALSE, FALSE)); t += 5.5;
    DelayCommand(t, Cast("c_firebolt_dummy", "nw_bandit001", STANDARD_FACTION_COMMONER, 5.0, SPELLABILITY_BOLT_FIRE, FALSE, FALSE, FALSE)); t += 5.5;
    DelayCommand(t, Cast("c_aura_5", "nw_bandit001", STANDARD_FACTION_COMMONER, 5.0, SPELLABILITY_AURA_UNEARTHLY_VISAGE, FALSE, TRUE, FALSE)); t += 5.5;
    DelayCommand(t, Cast("c_aura_20", "nw_bandit001", STANDARD_FACTION_COMMONER, 20.0, SPELLABILITY_AURA_UNEARTHLY_VISAGE, FALSE, TRUE, FALSE)); t += 5.5;
    DelayCommand(t, Cast("c_aura_30", "nw_bandit001", STANDARD_FACTION_COMMONER, 30.0, SPELLABILITY_AURA_UNEARTHLY_VISAGE, FALSE, TRUE, FALSE)); t += 5.5;
    DelayCommand(t, Cast("c_aura_40", "nw_bandit001", STANDARD_FACTION_COMMONER, 40.0, SPELLABILITY_AURA_UNEARTHLY_VISAGE, FALSE, TRUE, FALSE)); t += 5.5;
    DelayCommand(t, Cast("c_fake_mm", "nw_bandit001", STANDARD_FACTION_COMMONER, 5.0, SPELL_MAGIC_MISSILE, FALSE, FALSE, TRUE)); t += 5.5;
    DelayCommand(t, NpcItem("c_potion_25", "nw_bandit001", "nw_it_mpotion001", 25.0, TRUE, FALSE)); t += 6.5;
    DelayCommand(t, NpcItem("c_potion_30", "nw_bandit001", "nw_it_mpotion001", 30.0, TRUE, FALSE)); t += 6.5;
    DelayCommand(t, NpcItem("c_potion_35", "nw_bandit001", "nw_it_mpotion001", 35.0, TRUE, FALSE)); t += 6.5;
    DelayCommand(t, NpcItem("w_scroll_mm", "nw_hen_bod_01", "nw_it_sparscr109", 5.0, FALSE, FALSE)); t += 6.5;
    DelayCommand(t, NpcItem("w_wand_summon", "nw_hen_bod_01", "nw_wmgwn013", 5.0, FALSE, TRUE)); t += 6.5;
    DelayCommand(t, PcItem("pc_potion", "nw_it_mpotion001", TRUE, FALSE)); t += 6.0;
    DelayCommand(t, PcItem("pc_scroll_mm", "nw_it_sparscr109", FALSE, FALSE)); t += 6.0;
    DelayCommand(t, PcItem("pc_wand_summon", "nw_wmgwn013", FALSE, TRUE)); t += 6.5;
    DelayCommand(t, PcFeat("pc_feat_familiar", FEAT_SUMMON_FAMILIAR)); t += 5.0;
    DelayCommand(t, Unsummon("unsummon_1")); t += 2.0;
    DelayCommand(t, ScriptFamiliar("script_familiar")); t += 4.0;
    DelayCommand(t, Unsummon("unsummon_2")); t += 2.0;
    DelayCommand(t, ScriptSummon("script_summon")); t += 4.0;
    DelayCommand(t, Unsummon("unsummon_3")); t += 2.0;
    DelayCommand(t, VShot("a_end")); t += 2.0;
    DelayCommand(t, Mark("end"));
    DelayCommand(t + 1.0, VDone());
    Log("total=" + FloatToString(t, 0, 0));
}
