// Movement paths: arrival on a point (braking profile, exact stop, time, facing), ActionMoveToObject
// distances for waypoints and creatures of several sizes, ActionMoveAwayFromObject, ActionForceMoveToLocation
// timeouts, and ActionForceFollowObject spacing.
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y, float f = 0.0) { return Location(Area(), Vector(x, y, 0.0), f); }

object Mover(string sRes, string k, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, L(x, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void At(object o, string k)
{
    vector p = GetPosition(o);
    PLogF(k + ".x", p.x, 0.005);
    PLogF(k + ".y", p.y, 0.005);
    PLogF(k + ".facing", GetFacing(o), 0.5);
    PLogI(k + ".action", GetCurrentAction(o));
}

void X(object o, string k, float fTol)
{
    PLogF(k, GetPosition(o).x, fTol);
}

// Seconds from the order to the next queued action.
void Arrived(string k)
{
    // The original moves off a frame or two later and starts the next action a frame after arriving
    // (its frames run 15-50 ms under this load).
    PLogF(k + ".time", PSince() - GetLocalFloat(OBJECT_SELF, "T0"), 0.1);
}

void Order(object o, string k)
{
    SetLocalFloat(o, "T0", PSince());
    AssignCommand(o, ActionDoCommand(Arrived(k)));
}

void Dist(object o, object t, string k, float fTol)
{
    PLogF(k + ".dist", GetDistanceBetween(o, t), fTol);
    PLogI(k + ".action", GetCurrentAction(o));
}

void Arrivals()
{
    // Braking: a walker covers the last 0.875 m in one second, then stops on the point.
    object w = Mover("p_mover", "walkend", 2.0, 2.0);
    AssignCommand(w, ActionMoveToLocation(L(8.0, 2.0, 200.0), FALSE));
    Order(w, "walkend");
    float t;
    // Up to a frame of latency on each side (the original's server frame, OpenWinter's 30 Hz tick): 0.05 s of travel.
    for (t = 3.0; t <= 3.9; t += 0.15) DelayCommand(t, X(w, "walkend.at" + FloatToString(t, 0, 2), 0.09));
    DelayCommand(4.5, At(w, "walkend"));
    object r = Mover("p_mover", "runend", 2.0, 4.0);
    AssignCommand(r, ActionMoveToLocation(L(16.0, 4.0), TRUE));
    Order(r, "runend");
    for (t = 3.3; t <= 3.9; t += 0.15) DelayCommand(t, X(r, "runend.at" + FloatToString(t, 0, 2), 0.18));
    DelayCommand(5.0, At(r, "runend"));
    // Facing follows the direction of travel, not the location's facing.
    object d = Mover("p_mover", "diag", 10.0, 10.0);
    AssignCommand(d, ActionMoveToLocation(L(14.0, 14.0, 300.0), TRUE));
    DelayCommand(3.0, At(d, "diag"));
    object west = Mover("p_mover", "west", 20.0, 8.0);
    AssignCommand(west, ActionMoveToLocation(L(16.0, 8.0), TRUE));
    DelayCommand(3.0, At(west, "west"));
    object south = Mover("p_mover", "south", 24.0, 12.0);
    AssignCommand(south, ActionMoveToLocation(L(24.0, 7.0, 45.0), TRUE));
    DelayCommand(3.0, At(south, "south"));
    // Already there: done at once.
    object here = Mover("p_mover", "here", 30.0, 8.0);
    AssignCommand(here, ActionMoveToLocation(L(30.0, 8.0, 90.0), TRUE));
    Order(here, "here");
    DelayCommand(1.0, At(here, "here"));
}

void ToObject(string sMover, string sTarget, float fRange, float y, int bWaypoint)
{
    string k = "to." + (bWaypoint ? "wp" : sTarget) + "." + sMover + "." + FloatToString(fRange, 0, 1);
    object t = bWaypoint ? CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", L(30.0, y), FALSE, "WP")
                         : Mover(sTarget, "T", 30.0, y);
    object m = Mover(sMover, k, 22.0, y);
    AssignCommand(m, ActionMoveToObject(t, TRUE, fRange));
    Order(m, k);
    DelayCommand(4.5, Dist(m, t, k, 0.01));
}

void Objects()
{
    float y = 17.0;
    ToObject("p_mover", "", 0.0, y, TRUE); y += 1.5;
    ToObject("p_mover", "", 1.0, y, TRUE); y += 1.5;
    ToObject("p_mover", "", 2.5, y, TRUE); y += 1.5;
    ToObject("p_mover", "p_mover", 0.0, y, FALSE); y += 2.0;
    ToObject("p_mover", "p_mover", 1.0, y, FALSE); y += 2.0;
    ToObject("p_mover", "p_mover", 1.5, y, FALSE); y += 2.0;
    ToObject("p_mover", "p_mover", 3.0, y, FALSE); y += 2.0;
    ToObject("p_mover", "p_ogre", 0.0, y, FALSE); y += 2.5;
    ToObject("p_ogre", "p_mover", 1.0, y, FALSE); y += 2.5;
    ToObject("p_mover", "p_dog", 0.0, y, FALSE); y += 2.0;
    ToObject("p_rat", "p_rat", 0.0, y, FALSE); y += 1.5;
    ToObject("p_human", "p_human", 0.0, y, FALSE); y += 2.0;
}

void Away()
{
    // ActionMoveAwayFromObject goes fMoveAwayRange metres further away, in a straight line.
    object from = Mover("p_mover", "from", 4.0, 30.0);
    object a = Mover("p_mover", "away", 6.0, 30.0);
    AssignCommand(a, ActionMoveAwayFromObject(from, TRUE, 6.0));
    Order(a, "away");
    DelayCommand(4.0, At(a, "away"));
    // Already that far away: nothing happens.
    object from2 = Mover("p_mover", "from2", 4.0, 36.0);
    object b = Mover("p_mover", "away.far", 7.0, 32.0);
    AssignCommand(b, ActionMoveAwayFromObject(from2, FALSE, 3.0));
    Order(b, "away.far");
    DelayCommand(4.0, At(b, "away.far"));
    object from3 = Mover("p_mover", "from3", 4.0, 22.0);
    object c = Mover("p_mover", "away.diag", 6.0, 24.0);
    AssignCommand(c, ActionMoveAwayFromObject(from3, FALSE, 3.0));
    Order(c, "away.diag");
    DelayCommand(0.6, X(c, "away.diag.at0.6", 0.09));
    DelayCommand(4.0, At(c, "away.diag"));
}

void Force()
{
    // Timed out: jumps to the destination, then waits a moment before the next action.
    object f = Mover("p_mover", "force", 2.0, 38.0);
    AssignCommand(f, ActionForceMoveToLocation(L(12.0, 38.0), TRUE, 2.0));
    Order(f, "force");
    DelayCommand(2.4, At(f, "force.waiting"));
    object g = Mover("p_mover", "force.near", 2.0, 36.0);
    AssignCommand(g, ActionForceMoveToLocation(L(5.0, 36.0), FALSE, 30.0));
    Order(g, "force.near");
    DelayCommand(3.0, At(g, "force.near"));
}

void Follow(string k, float fDist, float y)
{
    object leader = Mover("p_mover", k + ".leader", 10.0, y);
    object f = Mover("p_mover", k, 6.0, y);
    AssignCommand(f, ActionForceFollowObject(leader, fDist));
    AssignCommand(leader, ActionMoveToLocation(L(26.0, y), TRUE));
    DelayCommand(2.0, Dist(f, leader, k + ".moving", 100.0));
    DelayCommand(10.0, Dist(f, leader, k + ".stopped", 0.15));
}

void Follows()
{
    Follow("follow2", 2.0, 4.0);
    Follow("follow3", 3.0, 9.0);
    Follow("follow8", 8.0, 14.0);
}

void main()
{
    PStart();
    DelayCommand(1.0, Arrivals());
    DelayCommand(1.0, Objects());
    DelayCommand(1.0, Away());
    DelayCommand(1.0, Force());
    DelayCommand(7.0, Follows());
    DelayCommand(18.5, PDone());
}
