// How close a creature gets before it uses something, and where ActionMoveToObject stops: doors (use
// radius PERSPACE + 0.75 from the nearest use point, for a human, a half-orc and a halfling), a chest
// (ActionInteractObject, ActionTakeItem, ActionMoveToObject), creatures, items on the ground, waypoints and an
// ordinary trigger whose position is its south-west corner. Each walker starts about 8 m off; its final place is
// logged relative to the target. Two humans stand 1.10 and 1.20 m from a door's use point and open it.
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y, float f = 90.0) { return Location(Area(), Vector(x, y, 0.0), f); }

object Actor(string k, string sRes, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, L(x, y), FALSE, "A_" + k);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, STANDARD_FACTION_MERCHANT);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetPlotFlag(o, TRUE);
    string s = GetLocalString(GetModule(), "CASES");
    SetLocalString(GetModule(), "CASES", s + k + " ");
    return o;
}

void Target(string k, object t)
{
    object o = GetObjectByTag("A_" + k);
    SetLocalObject(o, "TGT", t);
    SetLocalLocation(o, "TP", GetLocation(t));
}

void Setup()
{
    object a, t;
    // Doors (bearing 0: the use points at x +0.37, y -0.9 and +0.9 of the door), walkers from 8 m south.
    Actor("OD_H", "nw_bandit001", 10.0, 7.0); Target("OD_H", GetObjectByTag("D_OD_H"));
    Actor("OD_O", "ap_orc", 20.0, 7.0); Target("OD_O", GetObjectByTag("D_OD_O"));
    Actor("OD_L", "ap_half", 30.0, 7.0); Target("OD_L", GetObjectByTag("D_OD_L"));
    Actor("MD_H", "nw_bandit001", 40.0, 7.0); Target("MD_H", GetObjectByTag("D_MD_H"));
    Actor("MD_H3", "nw_bandit001", 50.0, 7.0); Target("MD_H3", GetObjectByTag("D_MD_H3"));
    Actor("J110", "nw_bandit001", 60.37, 15.0 - 0.9 - 1.10); Target("J110", GetObjectByTag("D_J110"));
    Actor("J120", "nw_bandit001", 70.37, 15.0 - 0.9 - 1.20); Target("J120", GetObjectByTag("D_J120"));
    // A chest each, walkers from 8 m west.
    string s = "IC_H IC_O IC_L TK_H MC_H MC_H01 ";
    float x = 10.0;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string k = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 10000);
        t = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", L(x + 8.0, 35.0, 0.0), FALSE, "C_" + k);
        Actor(k, k == "IC_O" ? "ap_orc" : k == "IC_L" ? "ap_half" : "nw_bandit001", x, 35.0);
        Target(k, t);
        x += 12.0;
    }
    CreateItemOnObject("nw_it_mpotion001", GetObjectByTag("C_TK_H"), 1, "TK_ITEM");
}

void Setup2()
{
    object t;
    string s;
    float x;
    // Creatures, walkers from 8 m west.
    s = "MCR_H MCR_O MCR_H3 GV_H ";
    x = 10.0;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string k = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 10000);
        t = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", L(x + 8.0, 47.0), FALSE, "T_" + k);
        int e; for (e = 5000; e <= 5012; e++) SetEventScript(t, e, "");
        ChangeToStandardFaction(t, STANDARD_FACTION_MERCHANT);
        SetPlotFlag(t, TRUE);
        Actor(k, k == "MCR_O" ? "ap_orc" : "nw_bandit001", x, 47.0);
        Target(k, t);
        x += 14.0;
    }
    CreateItemOnObject("nw_it_mpotion001", GetObjectByTag("A_GV_H"), 1, "GV_ITEM");
}

void Setup3()
{
    object t;
    string s;
    float x;
    // Items on the ground and waypoints, walkers from 8 m west.
    s = "PU_H MI_H MI_H01 MW_H MW_H01 ";
    x = 10.0;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string k = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 10000);
        if (GetStringLeft(k, 2) == "MW") t = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", L(x + 8.0, 75.0), FALSE, "W_" + k);
        else t = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mpotion001", L(x + 8.0, 75.0), FALSE, "I_" + k);
        Actor(k, "nw_bandit001", x, 75.0);
        Target(k, t);
        x += 12.0;
    }
    // Ordinary triggers whose position is the south-west corner, walkers from 6 m south-west.
    Actor("TG1", "nw_bandit001", 40.0 - 4.24, 60.0 - 4.24); Target("TG1", GetObjectByTag("TG1"));
    Actor("TG2", "nw_bandit001", 60.0 - 4.24, 60.0 - 4.24); Target("TG2", GetObjectByTag("TG2"));
    // From the north-east, across the trigger to its corner.
    Actor("TG3", "nw_bandit001", 20.0 + 10.0, 60.0 + 10.0); Target("TG3", GetObjectByTag("TG3"));
}

object T(string k) { return GetLocalObject(GetObjectByTag("A_" + k), "TGT"); }

void Go()
{
    AssignCommand(GetObjectByTag("A_OD_H"), ActionOpenDoor(T("OD_H")));
    AssignCommand(GetObjectByTag("A_OD_O"), ActionOpenDoor(T("OD_O")));
    AssignCommand(GetObjectByTag("A_OD_L"), ActionOpenDoor(T("OD_L")));
    AssignCommand(GetObjectByTag("A_MD_H"), ActionMoveToObject(T("MD_H")));
    AssignCommand(GetObjectByTag("A_MD_H3"), ActionMoveToObject(T("MD_H3"), FALSE, 3.0));
    AssignCommand(GetObjectByTag("A_J110"), ActionOpenDoor(T("J110")));
    AssignCommand(GetObjectByTag("A_J120"), ActionOpenDoor(T("J120")));
    AssignCommand(GetObjectByTag("A_IC_H"), ActionInteractObject(T("IC_H")));
    AssignCommand(GetObjectByTag("A_IC_O"), ActionInteractObject(T("IC_O")));
    AssignCommand(GetObjectByTag("A_IC_L"), ActionInteractObject(T("IC_L")));
    AssignCommand(GetObjectByTag("A_TK_H"), ActionTakeItem(GetObjectByTag("TK_ITEM"), T("TK_H")));
    AssignCommand(GetObjectByTag("A_MC_H"), ActionMoveToObject(T("MC_H")));
    AssignCommand(GetObjectByTag("A_MC_H01"), ActionMoveToObject(T("MC_H01"), FALSE, 0.1));
    AssignCommand(GetObjectByTag("A_MCR_H"), ActionMoveToObject(T("MCR_H")));
    AssignCommand(GetObjectByTag("A_MCR_O"), ActionMoveToObject(T("MCR_O")));
    AssignCommand(GetObjectByTag("A_MCR_H3"), ActionMoveToObject(T("MCR_H3"), FALSE, 3.0));
    AssignCommand(GetObjectByTag("A_GV_H"), ActionGiveItem(GetObjectByTag("GV_ITEM"), T("GV_H")));
    AssignCommand(GetObjectByTag("A_PU_H"), ActionPickUpItem(T("PU_H")));
    AssignCommand(GetObjectByTag("A_MI_H"), ActionMoveToObject(T("MI_H")));
    AssignCommand(GetObjectByTag("A_MI_H01"), ActionMoveToObject(T("MI_H01"), FALSE, 0.1));
    AssignCommand(GetObjectByTag("A_MW_H"), ActionMoveToObject(T("MW_H")));
    AssignCommand(GetObjectByTag("A_MW_H01"), ActionMoveToObject(T("MW_H01"), FALSE, 0.1));
    AssignCommand(GetObjectByTag("A_TG1"), ActionMoveToObject(T("TG1")));
    AssignCommand(GetObjectByTag("A_TG2"), ActionMoveToObject(T("TG2"), FALSE, 0.1));
    AssignCommand(GetObjectByTag("A_TG3"), ActionMoveToObject(T("TG3")));
}

void ReadOne(string k)
{
    object o = GetObjectByTag("A_" + k), t = GetLocalObject(o, "TGT");
    // Measured from where the target was set up (an item picked up has no place of its own any more).
    vector a = GetPosition(o), b = GetPositionFromLocation(GetLocalLocation(o, "TP"));
    PLogF(k + ".dist", VectorMagnitude(Vector(a.x - b.x, a.y - b.y, 0.0)), 0.08);
    PLogF(k + ".dx", a.x - b.x, 0.08);
    PLogF(k + ".dy", a.y - b.y, 0.08);
    PLogI(k + ".entered", GetLocalInt(o, "ENTERED_" + GetTag(t)));
    if (GetObjectType(t) == OBJECT_TYPE_DOOR) PLogI(k + ".open", GetIsOpen(t));
}

void Read()
{
    string s = GetLocalString(GetModule(), "CASES");
    float d = 0.0;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string k = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 10000);
        DelayCommand(d, ReadOne(k));
        d += 0.01;
    }
    PLog("TK.item", GetTag(GetItemPossessor(GetObjectByTag("TK_ITEM"))));
    PLog("GV.item", GetTag(GetItemPossessor(GetObjectByTag("GV_ITEM"))));
    PLog("PU.item", GetTag(GetItemPossessor(T("PU_H"))));
}

void main()
{
    DelayCommand(1.0, Setup());
    DelayCommand(1.5, Setup2());
    DelayCommand(2.0, Setup3());
    DelayCommand(6.0, Go());
    DelayCommand(25.0, Read());
    DelayCommand(27.0, PDone());
}
