// Equal routes between areas: two exits from the first area lead to the goal's area through one area
// each; the walker stands nearer the exit at x=22, and the exit at x=8 leads through a larger area (TA) or the one at
// x=22 does (UA). Logs the areas each walker crosses, where it ends, and how soon it sets off.
#include "parity_inc"
#include "parity_time"

void Watch(object o, vector v0, float t0, int n)
{
    if (VectorMagnitude(GetPosition(o) - v0) > 0.05 || GetArea(o) != GetLocalObject(o, "A0")) { SetLocalFloat(o, "T", PSince() - t0); return; }
    if (n > 0) DelayCommand(0.05, Watch(o, v0, t0, n - 1));
}

void Walker(string p)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", GetLocation(GetWaypointByTag(p + "_START")), FALSE, p + "_W");
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, STANDARD_FACTION_MERCHANT);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetLocalInt(o, "RT", 1);
}

void Go(string p)
{
    object o = GetObjectByTag(p + "_W");
    SetLocalObject(o, "A0", GetArea(o));
    AssignCommand(o, ActionMoveToObject(GetWaypointByTag(p + "_GOAL")));
    Watch(o, GetPosition(o), PSince(), 60);
}

void Report(string p)
{
    object o = GetObjectByTag(p + "_W");
    PLog(p + ".route", GetLocalString(o, "ROUTE"));
    PLog(p + ".area", GetTag(GetArea(o)));
    PLogF(p + ".goaldist", GetDistanceBetween(o, GetWaypointByTag(p + "_GOAL")), 0.5);
    PLogF(p + ".start", GetLocalFloat(o, "T"), 0.1);
}

void main()
{
    PStart();
    Walker("TA"); Walker("UA");
    DelayCommand(5.0, Go("TA"));
    DelayCommand(5.0, Go("UA"));
    DelayCommand(75.0, Report("TA"));
    DelayCommand(75.0, Report("UA"));
    DelayCommand(76.0, PDone());
}
