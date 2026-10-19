// Inter-area movement suites (interarea_move, interarea_pursuit; docs/INTER_AREA_MOVEMENT.md): tracked creatures
// log every area they enter (area, time, position, current action), when their queued move finishes, and where they
// end up. Tolerances come from the creature's IA_TOLT (seconds) and IA_TOLP (metres) locals.
#include "parity_inc"
#include "parity_time"

int IAStarted() { return GetLocalInt(GetModule(), "IA_GO"); }
void IAStart() { PStart(); SetLocalInt(GetModule(), "IA_GO", 1); }

void IATrack(object o, float fTolT = 0.75, float fTolP = 0.35)
{
    SetLocalInt(o, "IA_TRACK", 1);
    SetLocalFloat(o, "IA_TOLT", fTolT);
    SetLocalFloat(o, "IA_TOLP", fTolP);
}

int IATracked(object o) { return IAStarted() && GetLocalInt(o, "IA_TRACK"); }

void IAPos(string sKey, object o)
{
    vector p = GetPosition(o);
    float fTol = GetLocalFloat(o, "IA_TOLP");
    PLog(sKey + ".area", GetTag(GetArea(o)));
    PLogF(sKey + ".x", p.x, fTol);
    PLogF(sKey + ".y", p.y, fTol);
}

// Each area crossed adds to the timing tolerance: where a door's leaf swings when the door opens itself (the original
// picks a side the suite can't control) and turning on the spot both shift later legs by a few tenths of a second.
void IAWhen(string sKey, object o, int nLegs = 0) { PLogF(sKey + ".t", PSince(), GetLocalFloat(o, "IA_TOLT") + 0.4 * IntToFloat(nLegs)); }

void IAEnter(object o)
{
    if (!IATracked(o)) return;
    int n = GetLocalInt(o, "IA_ENTERS") + 1;
    SetLocalInt(o, "IA_ENTERS", n);
    string k = GetTag(o) + ".enter" + IntToString(n);
    IAPos(k, o);
    if (!GetLocalInt(o, "IA_NOTIME")) IAWhen(k, o, n - 1);
    PLogI(k + ".action", GetCurrentAction(o));
}

void IAExit(object o)
{
    if (IATracked(o)) SetLocalInt(o, "IA_EXITS", GetLocalInt(o, "IA_EXITS") + 1);
}

void IADone(object o)
{
    string k = GetTag(o) + ".done";
    IAPos(k, o);
    IAWhen(k, o, GetLocalInt(o, "IA_ENTERS") + 1);
}

// The first time something happens to a tracked creature (a block, a click, a door it opened...): when, and what.
void IAFirst(object o, string sWhat, string sValue)
{
    if (!IATracked(o)) return;
    int n = GetLocalInt(o, "IA_N_" + sWhat) + 1;
    SetLocalInt(o, "IA_N_" + sWhat, n);
    if (n > 1) return;
    PLog(GetTag(o) + "." + sWhat, sValue);
    IAWhen(GetTag(o) + "." + sWhat, o, GetLocalInt(o, "IA_ENTERS"));
}

void IAEnd(object o, int bPosition = TRUE)
{
    string k = GetTag(o) + ".end";
    if (bPosition) IAPos(k, o); else PLog(k + ".area", GetTag(GetArea(o)));
    PLogI(k + ".enters", GetLocalInt(o, "IA_ENTERS"));
    PLogI(k + ".exits", GetLocalInt(o, "IA_EXITS"));
}
