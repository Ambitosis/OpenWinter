// Paths round a creature that stands near the destination (as in the Prelude: Bim between the player
// and the academy door, the door's stopping point within Bim's personal space plus a hair). Each lane has a mover
// and a blocker that is busy (ActionWait) or neutral and idle (temporarily neutral both ways).
// Readings: where the mover ends, its current action, how far it strayed from its straight line, the closest it
// came to the blocker, and OnBlocked (cg_blocked).
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y) { return Location(Area(), Vector(x, y, 0.0), 0.0); }

object Mk(string sRes, string k, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, L(x, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Neutral(string mover, string blocker)
{
    object a = GetObjectByTag(mover), b = GetObjectByTag(blocker);
    ChangeToStandardFaction(a, STANDARD_FACTION_COMMONER);
    ChangeToStandardFaction(b, STANDARD_FACTION_MERCHANT);
    SetIsTemporaryNeutral(a, b); SetIsTemporaryNeutral(b, a);
}

void Busy(string k) { AssignCommand(GetObjectByTag(k), ActionWait(60.0)); }

void Arrived(string k) { PLogF(k + ".arrived", PSince(), 0.3); }

void Go(string k, float x, float y, int bRun = FALSE)
{
    object o = GetObjectByTag(k);
    AssignCommand(o, ActionMoveToLocation(L(x, y), bRun));
    AssignCommand(o, ActionDoCommand(Arrived(k)));
}

void GoTo(string k, string sTarget, float fRange, int bRun = FALSE)
{
    object o = GetObjectByTag(k);
    AssignCommand(o, ActionMoveToObject(GetObjectByTag(sTarget), bRun, fRange));
    AssignCommand(o, ActionDoCommand(Arrived(k)));
}

// The furthest a mover strays from its straight line y0, and the closest it comes to its blocker.
void Track(string k, float y0)
{
    object m = GetModule();
    object o = GetObjectByTag(k);
    float d = GetPosition(o).y - y0;
    if (fabs(d) > fabs(GetLocalFloat(m, "DEV_" + k))) SetLocalFloat(m, "DEV_" + k, d);
    float e = GetDistanceBetween(o, GetObjectByTag(k + "b"));
    if (GetLocalFloat(m, "NEAR_" + k) == 0.0 || e < GetLocalFloat(m, "NEAR_" + k)) SetLocalFloat(m, "NEAR_" + k, e);
}

void Tracks(string k, float y0)
{
    float t;
    for (t = 0.0; t < 10.0; t += 0.1) DelayCommand(t, Track(k, y0));
}

void Pos(string k, float fTol)
{
    object o = GetObjectByTag(k);
    vector p = GetPosition(o);
    PLogF(k + ".x", p.x, fTol);
    PLogF(k + ".y", p.y, fTol);
    PLogI(k + ".action", GetCurrentAction(o));
}

void Lane(string k, float fTol)
{
    Pos(k, fTol);
    Pos(k + "b", 0.01);
    PLogF(k + ".deviation", GetLocalFloat(GetModule(), "DEV_" + k), 0.15);
    PLogF(k + ".closest", GetLocalFloat(GetModule(), "NEAR_" + k), 0.05);
}

void Setup()
{
    // Busy blocker, destination 0.83 m from it: inside its planning circle, outside bumping range (0.8 m).
    Mk("p_gob", "beside", 14.0, 4.0); Mk("p_gob", "besideb", 20.0, 4.0);
    // Busy blocker, destination 0.6 m from it: inside bumping range.
    Mk("p_gob", "inside", 14.0, 10.0); Mk("p_gob", "insideb", 20.0, 10.0);
    // Busy blocker standing on the stopping point 1.8 m short of an object.
    Mk("p_gob", "objstop", 14.0, 16.0); Mk("p_gob", "objstopb", 22.2, 16.0);
    CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", L(24.0, 16.0), FALSE, "objstopw");
    // Neutral idle humans, like Bim: destination beside it, the stopping point of an object, a straight line past.
    Mk("p_human", "neutral", 14.0, 22.0); Mk("p_human", "neutralb", 20.0, 22.0); Neutral("neutral", "neutralb");
    Mk("p_human", "neutobj", 14.0, 28.0); Mk("p_human", "neutobjb", 22.2, 28.0); Neutral("neutobj", "neutobjb");
    CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", L(24.0, 28.0), FALSE, "neutobjw");
    Mk("p_human", "neutline", 14.0, 34.0); Mk("p_human", "neutlineb", 20.0, 34.0); Neutral("neutline", "neutlineb");
}

void Relations(string k)
{
    object a = GetObjectByTag(k), b = GetObjectByTag(k + "b");
    PLogI(k + ".blocker_friendly", GetIsFriend(a, b));
    PLogI(k + ".blocker_enemy", GetIsEnemy(a, b));
    PLogI(k + ".seen", GetObjectSeen(b, a));
}

void Start()
{
    Relations("beside"); Relations("neutral"); Relations("neutobj"); Relations("neutline");
    Busy("besideb"); Busy("insideb"); Busy("objstopb");
    Go("beside", 20.82, 4.1); Tracks("beside", 4.0);
    Go("inside", 20.6, 10.0); Tracks("inside", 10.0);
    GoTo("objstop", "objstopw", 1.8); Tracks("objstop", 16.0);
    Go("neutral", 21.03, 22.1); Tracks("neutral", 22.0);
    GoTo("neutobj", "neutobjw", 1.8, TRUE); Tracks("neutobj", 28.0);
    Go("neutline", 26.0, 34.0, TRUE); Tracks("neutline", 34.0);
}

void Report()
{
    Lane("beside", 0.05);
    Lane("inside", 0.1);
    Lane("objstop", 0.1);
    PLogF("objstop.range", GetDistanceBetween(GetObjectByTag("objstop"), GetObjectByTag("objstopw")), 0.05);
    Lane("neutral", 0.05);
    Lane("neutobj", 0.1);
    PLogF("neutobj.range", GetDistanceBetween(GetObjectByTag("neutobj"), GetObjectByTag("neutobjw")), 0.05);
    Lane("neutline", 0.05);
}

void main()
{
    PStart();
    DelayCommand(0.5, Setup());
    DelayCommand(6.0, Start());
    DelayCommand(18.0, Report());
    DelayCommand(18.5, PDone());
}
