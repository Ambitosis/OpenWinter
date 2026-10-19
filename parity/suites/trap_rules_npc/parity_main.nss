// Traps among NPCs: whether NPCs of other factions find traps (factions, detect mode, a henchman of an
// NPC), whom an NPC's kit trap goes off for (the setter's reputation), disarming in combat (how often a miss sets the
// trap off, by how much it must miss), Skill Mastery, the trap's creator disarming its own trap, and the +2 Set Trap
// synergy from base ranks only.
#include "ptrap_inc"
#include "parity_time"

object Actor(string sTag, string sRes, float x, float y, int nFaction = STANDARD_FACTION_COMMONER)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, PTrapL(x, y), FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

// ---- A: detection by NPCs (DC 1, +50 Search) ----

void Detect(string k, int nSearcher, int nTrap, int nChest = FALSE, int nMode = FALSE, float fDist = 2.5, string sRes = "tn_rogue", int nDC = 1, int nHate = TRUE)
{
    float x = GetLocalFloat(GetModule(), "DX") + 8.0;
    if (x > 75.0) { x = 6.0; SetLocalFloat(GetModule(), "DY", GetLocalFloat(GetModule(), "DY") + 12.0); }
    SetLocalFloat(GetModule(), "DX", x);
    float y = 5.0 + GetLocalFloat(GetModule(), "DY");
    object t;
    if (nChest)
    {
        t = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(x, y), FALSE, "T_" + k);
        CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, t, nTrap);
    }
    else t = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(x, y), 1.0, "T_" + k, nTrap);
    SetTrapDetectDC(t, nDC);
    // The trap trigger's edge (1 m from its centre) or the chest is fDist away.
    object a = Actor("S_" + k, sRes, x, y + fDist + (nChest ? 0.0 : 1.0), nSearcher);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_SEARCH, 50), a);
    if (nMode) SetActionMode(a, ACTION_MODE_DETECT, TRUE);
    SetPlotFlag(a, TRUE);
    object r = Actor("R_" + k, "nw_bandit001", x, y - 4.0, nTrap);
    SetPlotFlag(r, TRUE);
    SetLocalString(GetModule(), "DET", GetLocalString(GetModule(), "DET") + k + " ");
}

void Walker(string k, float fDist)
{
    float x = GetLocalFloat(GetModule(), "DX") + 14.0;
    if (x > 70.0) { x = 10.0; SetLocalFloat(GetModule(), "DY", GetLocalFloat(GetModule(), "DY") + 12.0); }
    SetLocalFloat(GetModule(), "DX", x);
    float y = 5.0 + GetLocalFloat(GetModule(), "DY");
    object t = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(x, y), 1.0, "T_" + k, STANDARD_FACTION_DEFENDER);
    SetTrapDetectDC(t, 1);
    object w = Actor("S_" + k, "tn_rogue", x - 6.0, y + 1.0 + fDist, STANDARD_FACTION_HOSTILE);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_SEARCH, 50), w);
    SetPlotFlag(w, TRUE);
    int i;
    for (i = 0; i < 4; i++)
    {
        AssignCommand(w, ActionMoveToLocation(PTrapL(x + 6.0, y + 1.0 + fDist)));
        AssignCommand(w, ActionMoveToLocation(PTrapL(x - 6.0, y + 1.0 + fDist)));
    }
    Actor("R_" + k, "nw_bandit001", x, y - 4.0, STANDARD_FACTION_DEFENDER);
    SetLocalString(GetModule(), "DET", GetLocalString(GetModule(), "DET") + k + " ");
}

void DetectRead()
{
    string s = GetLocalString(GetModule(), "DET");
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string k = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 10000);
        object a = GetObjectByTag("S_" + k);
        PLogI("det." + k + ".seen", GetTrapDetectedBy(GetObjectByTag("T_" + k), a));
        PLogI("det." + k + ".mode", GetActionMode(a, ACTION_MODE_DETECT));
        PLogI("det." + k + ".rep", GetReputation(GetObjectByTag("R_" + k), a));
        PLogI("det." + k + ".repback", GetReputation(a, GetObjectByTag("R_" + k)));
    }
}

void DetectSetup()
{
    // Searcher and trap factions, with the reputations Reputations() sets: Hostile searchers and Defender traps (the
    // trap's faction dislikes the searcher, not the other way), Commoner searchers and Merchant traps (both dislike
    // each other), Defender searchers and Commoner traps (only the searcher dislikes), Merchant searchers and Hostile
    // traps (only the trap's faction dislikes).
    Detect("HOS_DEF", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_DEFENDER);
    Detect("COM_MER", STANDARD_FACTION_COMMONER, STANDARD_FACTION_MERCHANT);
    Detect("DEF_COM", STANDARD_FACTION_DEFENDER, STANDARD_FACTION_COMMONER);
    Detect("MER_HOS", STANDARD_FACTION_MERCHANT, STANDARD_FACTION_HOSTILE);
    Detect("HOS_MER", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_MERCHANT);
    // Merchant traps, whose faction likes defenders (100).
    Detect("DEF_MER", STANDARD_FACTION_DEFENDER, STANDARD_FACTION_MERCHANT);
    Detect("HOS_COM", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_COMMONER);
    Detect("DEF_HOS", STANDARD_FACTION_DEFENDER, STANDARD_FACTION_HOSTILE);
    Detect("CHEST", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_DEFENDER, TRUE);
    Detect("CHEST2", STANDARD_FACTION_COMMONER, STANDARD_FACTION_MERCHANT, TRUE);
    // Standing still at 3, 5, 6 and 7 m from the edge; and in detect mode at 7 m.
    string sR = "3 5 6 7 ";
    while (sR != "")
    {
        int q = FindSubString(sR, " ");
        string d = GetStringLeft(sR, q);
        sR = GetSubString(sR, q + 1, 100);
        Detect("HIDLE" + d, STANDARD_FACTION_HOSTILE, STANDARD_FACTION_DEFENDER, FALSE, FALSE, StringToFloat(d));
        Detect("CIDLE" + d, STANDARD_FACTION_COMMONER, STANDARD_FACTION_MERCHANT, FALSE, FALSE, StringToFloat(d));
    }
    Detect("HMODE7", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_DEFENDER, FALSE, TRUE, 7.0);
    Detect("HMODE5", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_DEFENDER, FALSE, TRUE, 5.0);
    Detect("CMODE5", STANDARD_FACTION_COMMONER, STANDARD_FACTION_MERCHANT, FALSE, TRUE, 5.0);
    // A fighter (no rogue levels) against DC 35 and 36, a rogue against 36.
    Detect("F35", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_DEFENDER, FALSE, FALSE, 2.0, "tn_fighter", 35);
    Detect("F36", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_DEFENDER, FALSE, FALSE, 2.0, "tn_fighter", 36);
    Detect("R36", STANDARD_FACTION_HOSTILE, STANDARD_FACTION_DEFENDER, FALSE, FALSE, 2.0, "tn_rogue", 36);
    // Walkers passing 2.5 m and 5 m from the edge, back and forth.
    Walker("WALK25", 2.5);
    Walker("WALK5", 5.0);
}

// ---- B: an NPC's kit traps and who sets them off ----

void SetKit(string k, int nSetter, float x)
{
    object a = Actor("K_" + k, "tn_rogue", x - 3.0, 42.0, nSetter);
    SetPlotFlag(a, TRUE);
    object kit = CreateItemOnObject("nw_it_trap001", a, 1);
    AssignCommand(a, ActionUseItemAtLocation(kit, GetFirstItemProperty(kit), PTrapL(x, 42.0)));
    SetLocalFloat(GetModule(), "KX_" + k, x);
}

// A trap trigger with no creator, of nFaction.
void PlainTrap(string k, int nFaction, float x)
{
    CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(x, 42.0), 2.0, "KT_" + k, nFaction, "", "tp_trig");
    SetLocalFloat(GetModule(), "KX_" + k, x);
}

void KitWalk(string k, int nWalker, int nDestroySetter = FALSE)
{
    float x = GetLocalFloat(GetModule(), "KX_" + k);
    object t = GetNearestObjectToLocation(OBJECT_TYPE_TRIGGER, PTrapL(x, 42.0));
    PLogI("kit." + k + ".made", GetIsObjectValid(t) && GetIsTrapped(t));
    PLog("kit." + k + ".creator", PTrapTag(GetTrapCreator(t)));
    if (GetTag(t) != "KT_" + k)
    {
        SetTag(t, "KT_" + k);
        SetEventScript(t, EVENT_SCRIPT_TRIGGER_ON_TRAPTRIGGERED, "tp_trig");
    }
    object s = GetObjectByTag("K_" + k);
    if (nDestroySetter) DestroyObject(s);
    object w = Actor("KW_" + k, "nw_bandit001", x, 36.0, nWalker);
    SetLocalInt(w, "HP0", GetCurrentHitPoints(w));
    // The setter joins the walker's own faction: the setter likes the walker, the trap's faction doesn't.
    if (k == "SWITCH") ChangeToStandardFaction(s, STANDARD_FACTION_HOSTILE);
    PLogI("kit." + k + ".rep", GetIsObjectValid(s) ? GetReputation(s, w) : -1);
    AssignCommand(w, ActionMoveToLocation(PTrapL(x, 48.0)));
}

void KitRead(string k)
{
    object w = GetObjectByTag("KW_" + k);
    PLogI("kit." + k + ".sprung", FindSubString(GetLocalString(GetModule(), "PTO_KT_" + k), "trig") >= 0);
    PLogI("kit." + k + ".hurt", GetCurrentHitPoints(w) < GetLocalInt(w, "HP0"));
    PLogI("kit." + k + ".crossed", GetPosition(w).y > 46.0);
}

// ---- C: disarming in combat ----

object Chest(string sTag, float x, float y, int nFaction = STANDARD_FACTION_HOSTILE)
{
    object c = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(x, y), FALSE, sTag);
    CreateTrapOnObject(TRAP_BASE_TYPE_MINOR_SPIKE, c, nFaction, "tp_disarm", "tp_trig");
    return c;
}

// An actor beside a chest, attacked by a hostile creature that can't hurt it; nOver is the DC above the total skill.
void Fight(string k, string sRes, float x, float y, int nOver, int nFaction = STANDARD_FACTION_COMMONER)
{
    object t = Chest("C_" + k, x, y, nFaction == STANDARD_FACTION_HOSTILE ? STANDARD_FACTION_DEFENDER : STANDARD_FACTION_HOSTILE);
    object a = Actor("A_" + k, sRes, x - 1.5, y, nFaction);
    SetPlotFlag(a, TRUE);
    object h = Actor("H_" + k, "nw_bandit001", x - 2.5, y, nFaction == STANDARD_FACTION_HOSTILE ? STANDARD_FACTION_DEFENDER : STANDARD_FACTION_HOSTILE);
    SetPlotFlag(h, TRUE);
    AssignCommand(h, ActionAttack(a));
    SetTrapDisarmDC(t, GetSkillRank(SKILL_DISABLE_TRAP, a) + nOver);
    SetTrapDetectedBy(t, a, TRUE);
}

// The same against a hostile trap trigger (2 m square) the actor stands at the edge of.
void FightTrigger(string k, float x, float y)
{
    object t = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PTrapL(x, y), 1.0, "C_" + k, STANDARD_FACTION_HOSTILE, "tp_disarm", "tp_trig");
    object a = Actor("A_" + k, "tn_rogue", x - 1.6, y);
    SetPlotFlag(a, TRUE);
    object h = Actor("H_" + k, "nw_bandit001", x - 2.6, y, STANDARD_FACTION_HOSTILE);
    SetPlotFlag(h, TRUE);
    AssignCommand(h, ActionAttack(a));
    SetTrapDisarmDC(t, GetSkillRank(SKILL_DISABLE_TRAP, a) + 27);
    SetTrapDetectedBy(t, a, TRUE);
}

void FightGo(string k)
{
    object a = GetObjectByTag("A_" + k), t = GetObjectByTag("C_" + k);
    SetLocalInt(a, "COMBAT", GetIsInCombat(a));
    AssignCommand(a, ActionUseSkill(SKILL_DISABLE_TRAP, t, 0));
}

void FightSetup()
{
    int i;
    // Skill 10 + 2 (synergy) = 12 against DC 37: misses by 6 to 25.
    for (i = 0; i < 24; i++) Fight("F25_" + IntToString(i), "tn_rogue", 6.0 + 6.0 * IntToFloat(i % 12), 54.0 + 6.0 * IntToFloat(i / 12), 27);
    // Hostile disarmers on a defender chest trap (the defenders dislike the hostiles).
    for (i = 0; i < 12; i++) Fight("FH_" + IntToString(i), "tn_rogue", 6.0 + 6.0 * IntToFloat(i), 84.0, 27, STANDARD_FACTION_HOSTILE);
    for (i = 0; i < 12; i++) FightTrigger("FT_" + IntToString(i), 6.0 + 7.0 * IntToFloat(i), 92.0);
    // The same, by defenders the trap's (hostile) faction likes.
    for (i = 0; i < 12; i++) Fight("FL_" + IntToString(i), "tn_rogue", 6.0 + 6.0 * IntToFloat(i), 72.0, 27, STANDARD_FACTION_DEFENDER);
    // Skill Mastery against DC 32 (exactly 20 + 12).
    for (i = 0; i < 6; i++) Fight("SM_" + IntToString(i), "tn_master", 6.0 + 6.0 * IntToFloat(i), 66.0, 22);
}

void FightGoAll()
{
    int i;
    for (i = 0; i < 24; i++) FightGo("F25_" + IntToString(i));
    for (i = 0; i < 6; i++) FightGo("SM_" + IntToString(i));
    for (i = 0; i < 12; i++) FightGo("FL_" + IntToString(i));
    for (i = 0; i < 12; i++) FightGo("FH_" + IntToString(i));
    for (i = 0; i < 12; i++) FightGo("FT_" + IntToString(i));
}

void FightRead()
{
    int i, nOk = 0, nSprung = 0, nCombat = 0;
    for (i = 0; i < 24; i++)
    {
        string k = "F25_" + IntToString(i);
        string ev = GetLocalString(GetModule(), "PTO_C_" + k);
        nOk += FindSubString(ev, "disarm") >= 0;
        nSprung += FindSubString(ev, "trig") >= 0;
        nCombat += GetLocalInt(GetObjectByTag("A_" + k), "COMBAT");
    }
    PLogI("f25.incombat", nCombat);
    PLogI("f25.disarmed", nOk);
    PLog("f25.sprung", IntToString(nSprung) + "|~6");
    nOk = 0; nCombat = 0;
    for (i = 0; i < 6; i++)
    {
        string k = "SM_" + IntToString(i);
        nOk += FindSubString(GetLocalString(GetModule(), "PTO_C_" + k), "disarm") >= 0;
        nCombat += GetLocalInt(GetObjectByTag("A_" + k), "COMBAT");
    }
    PLogI("sm.incombat", nCombat);
    PLogI("sm.disarmed", nOk);
    nOk = 0; nCombat = 0; nSprung = 0;
    for (i = 0; i < 12; i++)
    {
        string k = "FL_" + IntToString(i);
        string ev = GetLocalString(GetModule(), "PTO_C_" + k);
        nOk += FindSubString(ev, "disarm") >= 0;
        nSprung += FindSubString(ev, "trig") >= 0;
        nCombat += GetLocalInt(GetObjectByTag("A_" + k), "COMBAT");
    }
    PLogI("fl.incombat", nCombat);
    PLogI("fl.disarmed", nOk);
    PLog("fl.sprung", IntToString(nSprung) + "|~4");
    PLogI("fl.rep", GetReputation(GetObjectByTag("H_FL_0"), GetObjectByTag("A_FL_0")));
    nOk = 0; nCombat = 0; nSprung = 0;
    for (i = 0; i < 12; i++)
    {
        string k = "FH_" + IntToString(i);
        string ev = GetLocalString(GetModule(), "PTO_C_" + k);
        nOk += FindSubString(ev, "disarm") >= 0;
        nSprung += FindSubString(ev, "trig") >= 0;
        nCombat += GetLocalInt(GetObjectByTag("A_" + k), "COMBAT");
    }
    PLogI("fh.incombat", nCombat);
    PLogI("fh.disarmed", nOk);
    PLog("fh.sprung", IntToString(nSprung) + "|~4");
    nOk = 0; nCombat = 0; nSprung = 0;
    for (i = 0; i < 12; i++)
    {
        string k = "FT_" + IntToString(i);
        string ev = GetLocalString(GetModule(), "PTO_C_" + k);
        nOk += FindSubString(ev, "disarm") >= 0;
        nSprung += FindSubString(ev, "trig") >= 0;
        nCombat += GetLocalInt(GetObjectByTag("A_" + k), "COMBAT");
    }
    PLogI("ft.incombat", nCombat);
    PLogI("ft.disarmed", nOk);
    PLog("ft.sprung", IntToString(nSprung) + "|~4");
    PLogI("f25.rep", GetReputation(GetObjectByTag("H_F25_0"), GetObjectByTag("A_F25_0")));
}

// ---- D: the creator; E: the synergy ----

void CreatorSetup()
{
    object a = Actor("CR_SETTER", "tn_rogue", 77.0, 30.0, STANDARD_FACTION_DEFENDER);
    object c = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(80.0, 30.0), FALSE, "CR_CHEST");
    object kit = CreateItemOnObject("nw_it_trap001", a, 1);
    AssignCommand(a, ActionUseItemOnObject(kit, GetFirstItemProperty(kit), c));
    object c2 = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PTrapL(90.0, 30.0), FALSE, "CR_CHEST2");
    object a2 = Actor("CR_SETTER2", "tn_rogue", 87.0, 30.0, STANDARD_FACTION_DEFENDER);
    object kit2 = CreateItemOnObject("nw_it_trap001", a2, 1);
    AssignCommand(a2, ActionUseItemOnObject(kit2, GetFirstItemProperty(kit2), c2));
    Actor("CR_OTHER", "tn_rogue", 93.0, 30.0, STANDARD_FACTION_DEFENDER);
}

void CreatorGo()
{
    object c = GetObjectByTag("CR_CHEST"), c2 = GetObjectByTag("CR_CHEST2");
    PLog("cr.creator", PTrapTag(GetTrapCreator(c)));
    PLogI("cr.trapped", GetIsTrapped(c));
    SetTrapDisarmDC(c, 120);
    SetTrapDisarmDC(c2, 120);
    SetEventScript(c, EVENT_SCRIPT_PLACEABLE_ON_DISARM, "tp_disarm");
    SetEventScript(c2, EVENT_SCRIPT_PLACEABLE_ON_DISARM, "tp_disarm");
    AssignCommand(GetObjectByTag("CR_SETTER"), ActionUseSkill(SKILL_DISABLE_TRAP, c, 0));
    AssignCommand(GetObjectByTag("CR_OTHER"), ActionUseSkill(SKILL_DISABLE_TRAP, c2, 0));
}

void CreatorRead()
{
    PLogI("cr.self.trapped", GetIsTrapped(GetObjectByTag("CR_CHEST")));
    PLogI("cr.other.trapped", GetIsTrapped(GetObjectByTag("CR_CHEST2")));
}

void Synergy(string k, string sRes, float x, int nEffect = 0)
{
    object t = Chest("SY_" + k, x, 66.0);
    object a = Actor("Y_" + k, sRes, x - 1.5, 66.0);
    if (nEffect > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_SET_TRAP, nEffect), a);
    // Take 20 + Disable Trap 10 + 2 only with the synergy.
    SetTrapDisarmDC(t, 32);
    PLogI("sy." + k + ".settrap", GetSkillRank(SKILL_SET_TRAP, a));
    AssignCommand(a, ActionUseSkill(SKILL_DISABLE_TRAP, t, 0));
}

void SynergyRead(string k) { PLogI("sy." + k + ".trapped", GetIsTrapped(GetObjectByTag("SY_" + k))); }

string g_f = "1 2 3 4 ";
string FName(int n) { return n == 1 ? "HOS" : n == 2 ? "COM" : n == 3 ? "MER" : "DEF"; }

void Reputations()
{
    object h = Actor("REF_1", "nw_bandit001", 78.0, 78.0, STANDARD_FACTION_HOSTILE);
    object c = Actor("REF_2", "nw_bandit001", 76.0, 78.0, STANDARD_FACTION_COMMONER);
    object m = Actor("REF_3", "nw_bandit001", 74.0, 78.0, STANDARD_FACTION_MERCHANT);
    object d = Actor("REF_4", "nw_bandit001", 72.0, 78.0, STANDARD_FACTION_DEFENDER);
    SetStandardFactionReputation(STANDARD_FACTION_DEFENDER, 0, h);
    SetStandardFactionReputation(STANDARD_FACTION_MERCHANT, 0, c);
    SetStandardFactionReputation(STANDARD_FACTION_COMMONER, 0, m);
    SetStandardFactionReputation(STANDARD_FACTION_DEFENDER, 0, c);
    SetStandardFactionReputation(STANDARD_FACTION_HOSTILE, 0, m);
    SetStandardFactionReputation(STANDARD_FACTION_HOSTILE, 0, c);
    SetStandardFactionReputation(STANDARD_FACTION_MERCHANT, 0, h);
    int i, j;
    for (i = 1; i <= 4; i++)
        for (j = 1; j <= 4; j++)
            if (i != j) PLogI("rep." + FName(i) + "." + FName(j), GetReputation(GetObjectByTag("REF_" + IntToString(i)), GetObjectByTag("REF_" + IntToString(j))));
}

void main()
{
    PStart();
    Reputations();
    DelayCommand(0.5, DetectSetup());
    // Kit traps: a defender's against a hostile walker (the defenders dislike the hostiles), against a defender, with
    // the setter gone, with the setter turned commoner (who like the hostiles); a merchant's against a commoner
    // (mutual dislike); a hostile's against a merchant (the hostiles dislike merchants). Trap triggers with no creator.
    DelayCommand(1.0, SetKit("DEF_HOS", STANDARD_FACTION_DEFENDER, 6.0));
    DelayCommand(1.0, SetKit("DEF_DEF", STANDARD_FACTION_DEFENDER, 14.0));
    DelayCommand(1.0, SetKit("GONE_HOS", STANDARD_FACTION_DEFENDER, 22.0));
    DelayCommand(1.0, SetKit("SWITCH", STANDARD_FACTION_DEFENDER, 30.0));
    DelayCommand(1.0, SetKit("MER_COM", STANDARD_FACTION_MERCHANT, 38.0));
    DelayCommand(1.0, SetKit("HOS_MER", STANDARD_FACTION_HOSTILE, 46.0));
    DelayCommand(1.0, PlainTrap("PDEF_HOS", STANDARD_FACTION_DEFENDER, 54.0));
    DelayCommand(1.0, PlainTrap("PMER_COM", STANDARD_FACTION_MERCHANT, 62.0));
    DelayCommand(1.0, PlainTrap("PHOS_MER", STANDARD_FACTION_HOSTILE, 70.0));
    DelayCommand(1.0, PlainTrap("PDEF_DEF", STANDARD_FACTION_DEFENDER, 78.0));
    DelayCommand(1.0, PlainTrap("PMER_DEF", STANDARD_FACTION_MERCHANT, 86.0));
    DelayCommand(1.0, PlainTrap("PCOM_DEF", STANDARD_FACTION_COMMONER, 94.0));
    DelayCommand(1.5, FightSetup());
    DelayCommand(2.0, CreatorSetup());
    DelayCommand(2.5, Synergy("BASE5", "sy_5", 50.0));
    DelayCommand(2.5, Synergy("BASE4", "sy_4", 58.0));
    DelayCommand(2.5, Synergy("EFFECT", "sy_0", 66.0, 10));
    DelayCommand(5.0, FightGoAll());
    DelayCommand(14.0, KitWalk("DEF_HOS", STANDARD_FACTION_HOSTILE));
    DelayCommand(14.0, KitWalk("DEF_DEF", STANDARD_FACTION_DEFENDER));
    DelayCommand(14.0, KitWalk("GONE_HOS", STANDARD_FACTION_HOSTILE, TRUE));
    DelayCommand(14.0, KitWalk("SWITCH", STANDARD_FACTION_HOSTILE));
    DelayCommand(14.0, KitWalk("MER_COM", STANDARD_FACTION_COMMONER));
    DelayCommand(14.0, KitWalk("HOS_MER", STANDARD_FACTION_MERCHANT));
    DelayCommand(14.0, KitWalk("PDEF_HOS", STANDARD_FACTION_HOSTILE));
    DelayCommand(14.0, KitWalk("PMER_COM", STANDARD_FACTION_COMMONER));
    DelayCommand(14.0, KitWalk("PHOS_MER", STANDARD_FACTION_MERCHANT));
    DelayCommand(14.0, KitWalk("PDEF_DEF", STANDARD_FACTION_DEFENDER));
    DelayCommand(14.0, KitWalk("PMER_DEF", STANDARD_FACTION_DEFENDER));
    DelayCommand(14.0, KitWalk("PCOM_DEF", STANDARD_FACTION_DEFENDER));
    DelayCommand(14.0, CreatorGo());
    DelayCommand(16.0, SynergyRead("BASE5"));
    DelayCommand(16.0, SynergyRead("BASE4"));
    DelayCommand(16.0, SynergyRead("EFFECT"));
    DelayCommand(24.0, FightRead());
    DelayCommand(26.0, KitRead("DEF_HOS"));
    DelayCommand(26.0, KitRead("DEF_DEF"));
    DelayCommand(26.0, KitRead("GONE_HOS"));
    DelayCommand(26.0, KitRead("SWITCH"));
    DelayCommand(26.0, KitRead("MER_COM"));
    DelayCommand(26.0, KitRead("HOS_MER"));
    DelayCommand(26.0, KitRead("PDEF_HOS"));
    DelayCommand(26.0, KitRead("PMER_COM"));
    DelayCommand(26.0, KitRead("PHOS_MER"));
    DelayCommand(26.0, KitRead("PDEF_DEF"));
    DelayCommand(26.0, KitRead("PMER_DEF"));
    DelayCommand(26.0, KitRead("PCOM_DEF"));
    DelayCommand(26.0, CreatorRead());
    DelayCommand(40.0, DetectRead());
    DelayCommand(41.0, PDone());
}
