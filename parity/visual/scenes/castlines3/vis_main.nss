#include "visual_inc"
// castlines3 (after castlines and castlines2): the chat lines left unmeasured there. At Spellcraft +50 (every spell
// a player may identify is named): creatures' hostile spells at the player, spell-like abilities, wands, scrolls and
// potions used by others and by the player (the "uses" line and its range, "Lost Item" after a used potion and after
// a scripted DestroyObject), a feat that casts (Summon Familiar), fake casts, and the "will not stack" lines
// (dialog.tlk 66247-66250) for natural, deflection, dodge, armour and shield AC effects and Barkskin cast twice.
// Every case is announced in the chat window ("CL <case>"); each caster is named after its case.

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

// A caster at fDistance casts nSpell at the player, the dummy or itself two seconds after it appears.
void Cast(string sCase, string sResRef, int nFaction, float fDistance, int nSpell, int bAtPC, int bSelf, int bFake)
{
    Mark(sCase);
    object o = At(sResRef, fDistance, 30.0, sCase, nFaction);
    object t = bSelf ? o : (bAtPC ? GetFirstPC() : Dummy());
    if (bFake) DelayCommand(2.0, AssignCommand(o, ActionCastFakeSpellAtObject(nSpell, t)));
    else DelayCommand(2.0, AssignCommand(o, ActionCastSpellAtObject(nSpell, t, METAMAGIC_ANY, TRUE)));
    DelayCommand(2.5, Action(o, sCase));
    DelayCommand(6.0, DestroyObject(o));
}

// A commoner uses an item (created on it) on itself or at the dummy.
void NpcItem(string sCase, string sItem, float fDistance, int bSelf)
{
    Mark(sCase);
    object o = At("nw_bandit001", fDistance, 50.0, sCase, STANDARD_FACTION_COMMONER);
    object it = CreateItemOnObject(sItem, o);
    itemproperty ip = GetFirstItemProperty(it);
    Log(sCase + "|item=" + GetName(it) + "|ipType=" + IntToString(GetItemPropertyType(ip)) + "|ipSub=" + IntToString(GetItemPropertySubType(ip)));
    DelayCommand(1.0, AssignCommand(o, ActionUseItemOnObject(it, ip, bSelf ? o : Dummy())));
    DelayCommand(1.5, Action(o, sCase));
    DelayCommand(6.0, DestroyObject(o));
}

void PcItem(string sCase, string sItem, int bSelf)
{
    object pc = GetFirstPC();
    Mark(sCase);
    object it = CreateItemOnObject(sItem, pc);
    itemproperty ip = GetFirstItemProperty(it);
    DelayCommand(1.0, AssignCommand(pc, ClearAllActions(TRUE)));
    DelayCommand(1.0, AssignCommand(pc, ActionUseItemOnObject(it, ip, bSelf ? pc : Dummy())));
    DelayCommand(1.5, Action(pc, sCase));
}

void PcCast(string sCase, int nSpell, int bFake)
{
    object pc = GetFirstPC();
    Mark(sCase);
    AssignCommand(pc, ClearAllActions(TRUE));
    if (bFake) AssignCommand(pc, ActionCastFakeSpellAtObject(nSpell, Dummy()));
    else AssignCommand(pc, ActionCastSpellAtObject(nSpell, nSpell == SPELL_BARKSKIN ? pc : Dummy(), METAMAGIC_ANY, TRUE));
}

void PcFeat(string sCase, int nFeat)
{
    object pc = GetFirstPC();
    Mark(sCase);
    Log(sCase + "|hasFeat=" + IntToString(GetHasFeat(nFeat, pc)));
    AssignCommand(pc, ClearAllActions(TRUE));
    AssignCommand(pc, ActionUseFeat(nFeat, pc));
    DelayCommand(0.5, Action(pc, sCase));
}

void NpcFeat(string sCase, int nFeat)
{
    Mark(sCase);
    object o = At("nw_hen_bod_01", 5.0, 30.0, sCase, STANDARD_FACTION_COMMONER);
    Log(sCase + "|hasFeat=" + IntToString(GetHasFeat(nFeat, o)));
    DelayCommand(1.0, AssignCommand(o, ActionUseFeat(nFeat, o)));
    DelayCommand(1.5, Action(o, sCase));
    DelayCommand(7.0, DestroyObject(GetAssociate(ASSOCIATE_TYPE_FAMILIAR, o)));
    DelayCommand(7.0, DestroyObject(o));
}

void DestroyCarried(string sCase)
{
    object pc = GetFirstPC();
    Mark(sCase);
    object it = CreateItemOnObject("nw_it_mpotion001", pc);
    DelayCommand(1.0, DestroyObject(it));
}

void Ac(string sCase, object o, int nAmount, int nType)
{
    Mark(sCase);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(nAmount, nType), o);
    DelayCommand(0.5, Log(sCase + "|ac=" + IntToString(GetAC(o))));
}

void ClearAc(string sCase, object o)
{
    Mark(sCase);
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == EFFECT_TYPE_AC_INCREASE) RemoveEffect(o, e);
        e = GetNextEffect(o);
    }
    DelayCommand(0.5, Log(sCase + "|ac=" + IntToString(GetAC(o))));
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
    object d = At("nw_goblina", 7.0, 90.0, "Dummy", STANDARD_FACTION_HOSTILE);
    SetPlotFlag(d, FALSE);
    SetImmortal(d, TRUE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectCutsceneParalyze(), d);
    object n = At("nw_bandit001", 4.0, 120.0, "AcNpc", STANDARD_FACTION_COMMONER);
    Log("start|spellcraft=" + IntToString(GetSkillRank(SKILL_SPELLCRAFT, pc)) + "|familiarFeat=" + IntToString(GetHasFeat(FEAT_SUMMON_FAMILIAR, pc)));

    float t = 4.0;
    DelayCommand(t, Spellcraft(50)); t += 1.0;
    // Hostile creatures' spells and abilities at the player; a commoner's at the dummy and on itself.
    DelayCommand(t, Cast("h_mm_pc", "nw_goblina", STANDARD_FACTION_HOSTILE, 5.0, SPELL_MAGIC_MISSILE, TRUE, FALSE, FALSE)); t += 6.5;
    DelayCommand(t, Cast("h_firebolt_pc", "nw_goblina", STANDARD_FACTION_HOSTILE, 5.0, SPELLABILITY_BOLT_FIRE, TRUE, FALSE, FALSE)); t += 6.5;
    DelayCommand(t, Cast("c_firebolt_dummy", "nw_bandit001", STANDARD_FACTION_COMMONER, 5.0, SPELLABILITY_BOLT_FIRE, FALSE, FALSE, FALSE)); t += 6.5;
    DelayCommand(t, Cast("c_mm_dummy", "nw_bandit001", STANDARD_FACTION_COMMONER, 5.0, SPELL_MAGIC_MISSILE, FALSE, FALSE, FALSE)); t += 6.5;
    DelayCommand(t, Cast("c_ability_self", "nw_bandit001", STANDARD_FACTION_COMMONER, 5.0, SPELLABILITY_AURA_UNEARTHLY_VISAGE, FALSE, TRUE, FALSE)); t += 6.5;
    // Fake casts.
    DelayCommand(t, Cast("c_fake_mm", "nw_bandit001", STANDARD_FACTION_COMMONER, 5.0, SPELL_MAGIC_MISSILE, FALSE, FALSE, TRUE)); t += 6.5;
    DelayCommand(t, PcCast("pc_fake_mm", SPELL_MAGIC_MISSILE, TRUE)); t += 5.0;
    // Items used by a commoner: a potion at 5, 20 and 40 m, a wand and a scroll.
    DelayCommand(t, NpcItem("c_potion_5", "nw_it_mpotion001", 5.0, TRUE)); t += 6.5;
    DelayCommand(t, NpcItem("c_potion_20", "nw_it_mpotion001", 20.0, TRUE)); t += 6.5;
    DelayCommand(t, NpcItem("c_potion_40", "nw_it_mpotion001", 40.0, TRUE)); t += 6.5;
    DelayCommand(t, NpcItem("c_wand_5", "nw_wmgwn013", 5.0, FALSE)); t += 6.5;
    DelayCommand(t, NpcItem("c_scroll_5", "nw_it_sparscr104", 5.0, TRUE)); t += 6.5;
    // Items used by the player.
    DelayCommand(t, PcItem("pc_potion", "nw_it_mpotion001", TRUE)); t += 6.0;
    DelayCommand(t, PcItem("pc_wand", "nw_wmgwn013", FALSE)); t += 6.0;
    DelayCommand(t, PcItem("pc_scroll", "nw_it_sparscr104", TRUE)); t += 7.0;
    DelayCommand(t, DestroyCarried("pc_destroy_carried")); t += 3.0;
    // A feat that casts: Summon Familiar, the player's and a wizard's.
    DelayCommand(t, PcFeat("pc_feat_familiar", FEAT_SUMMON_FAMILIAR)); t += 6.0;
    DelayCommand(t, NpcFeat("c_feat_familiar", FEAT_SUMMON_FAMILIAR)); t += 8.0;
    // AC effects that don't stack.
    DelayCommand(t, ClearAc("ac_clear0", pc)); t += 1.5;
    DelayCommand(t, Ac("ac_nat2_a", pc, 2, AC_NATURAL_BONUS)); t += 2.5;
    DelayCommand(t, Ac("ac_nat2_b", pc, 2, AC_NATURAL_BONUS)); t += 2.5;
    DelayCommand(t, Ac("ac_nat4_c", pc, 4, AC_NATURAL_BONUS)); t += 2.5;
    DelayCommand(t, Ac("ac_nat1_d", pc, 1, AC_NATURAL_BONUS)); t += 2.5;
    DelayCommand(t, ClearAc("ac_clear1", pc)); t += 2.5;
    DelayCommand(t, PcCast("ac_bark_1", SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, PcCast("ac_bark_2", SPELL_BARKSKIN, FALSE)); t += 5.0;
    DelayCommand(t, ClearAc("ac_clear2", pc)); t += 2.5;
    DelayCommand(t, Ac("ac_defl2_a", pc, 2, AC_DEFLECTION_BONUS)); t += 2.5;
    DelayCommand(t, Ac("ac_defl2_b", pc, 2, AC_DEFLECTION_BONUS)); t += 2.5;
    DelayCommand(t, Ac("ac_dodge2_a", pc, 2, AC_DODGE_BONUS)); t += 2.5;
    DelayCommand(t, Ac("ac_dodge2_b", pc, 2, AC_DODGE_BONUS)); t += 2.5;
    DelayCommand(t, Ac("ac_armor2_a", pc, 2, AC_ARMOUR_ENCHANTMENT_BONUS)); t += 2.5;
    DelayCommand(t, Ac("ac_armor2_b", pc, 2, AC_ARMOUR_ENCHANTMENT_BONUS)); t += 2.5;
    DelayCommand(t, Ac("ac_shield2_a", pc, 2, AC_SHIELD_ENCHANTMENT_BONUS)); t += 2.5;
    DelayCommand(t, Ac("ac_shield2_b", pc, 2, AC_SHIELD_ENCHANTMENT_BONUS)); t += 2.5;
    DelayCommand(t, ClearAc("ac_clear3", pc)); t += 2.5;
    DelayCommand(t, Ac("ac_npc_a", n, 2, AC_NATURAL_BONUS)); t += 2.5;
    DelayCommand(t, Ac("ac_npc_b", n, 2, AC_NATURAL_BONUS)); t += 2.5;
    DelayCommand(t, VShot("a_end")); t += 2.0;
    DelayCommand(t, Mark("end"));
    DelayCommand(t + 1.0, VDone());
    Log("total=" + FloatToString(t, 0, 0));
}
