// How long an NPC stands before it sets off (expected: the server turns at once, with a 0.3 s path-planning wait
// for NPCs before some moves). Each walker faces east; its position is sampled every 0.05 s from the order.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y, float f = 0.0) { return Location(Area(), Vector(x, y, 0.0), f); }

object Actor(string sTag, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", L(x, y, 0.0), FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, STANDARD_FACTION_MERCHANT);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Watch(object o, vector v0, float t0, int n)
{
    vector v = GetPosition(o);
    if (VectorMagnitude(v - v0) > 0.05) { SetLocalFloat(o, "T", PSince() - t0); return; }
    if (n > 0) DelayCommand(0.05, Watch(o, v0, t0, n - 1));
}

void Go(string k, int nKind, float dx)
{
    object o = GetObjectByTag("A_" + k);
    vector p = GetPosition(o);
    object w = GetObjectByTag("W_" + k);
    if (nKind == 0) AssignCommand(o, ActionMoveToObject(w));
    else if (nKind == 1) AssignCommand(o, ActionMoveToLocation(GetLocation(w)));
    else if (nKind == 2) AssignCommand(o, ActionMoveToObject(w, TRUE));
    else if (nKind == 3) AssignCommand(o, ActionMoveAwayFromObject(w, FALSE, 10.0));
    else if (nKind == 4) AssignCommand(o, ActionForceMoveToObject(w));
    else if (nKind == 5) AssignCommand(o, ActionForceMoveToLocation(GetLocation(w)));
    else if (nKind == 6) { AssignCommand(o, SetFacing(180.0)); AssignCommand(o, ActionMoveToLocation(GetLocation(w))); }
    Watch(o, p, PSince(), 60);
}

int g_n = 0;
void Case(string k, int nKind, float dx)
{
    float y = 5.0 + 5.0 * IntToFloat(g_n++);
    Actor("A_" + k, 40.0, y);
    CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", L(40.0 + dx, y), FALSE, "W_" + k);
    DelayCommand(5.0, Go(k, nKind, dx));
}

void Report()
{
    string s = "MO_W MO_E ML_W ML_E MR_W MA_W FMO_W FML_W SF_W ";
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string k = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 100);
        PLogF(k + ".start", GetLocalFloat(GetObjectByTag("A_" + k), "T"), 0.1);
    }
}

void main()
{
    PStart();
    Case("MO_W", 0, -10.0);
    Case("MO_E", 0, 10.0);
    Case("ML_W", 1, -10.0);
    Case("ML_E", 1, 10.0);
    Case("MR_W", 2, -10.0);
    Case("MA_W", 3, 1.0);
    Case("FMO_W", 4, -10.0);
    Case("FML_W", 5, -10.0);
    Case("SF_W", 6, -10.0);
    DelayCommand(15.0, Report());
    DelayCommand(16.0, PDone());
}
