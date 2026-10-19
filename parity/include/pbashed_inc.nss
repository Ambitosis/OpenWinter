// door_bashed (docs/INTER_AREA_MOVEMENT.md): transition doors killed by damage, EffectDeath and bashing. Door events
// are logged the first time each happens to a door, with who caused it, when, and its place in that door's sequence;
// BState reads everything a script can see of a door.
#include "pinterarea_inc"

// A door's BT_TOL (seconds) widens its event times; BT_NOTIME leaves them out.
void BEv(object d, string sEv, object oBy)
{
    string t = GetTag(d);
    object m = GetModule();
    int n = GetLocalInt(m, "BT_N_" + t + sEv) + 1;
    SetLocalInt(m, "BT_N_" + t + sEv, n);
    float fTol = 0.75 + GetLocalFloat(m, "BT_TOL_" + t);
    int bTime = !GetLocalInt(m, "BT_NOTIME_" + t);
    if (n == 2 && bTime) PLogF(t + "." + sEv + "2.t", PSince(), fTol);
    if (n > 1) return;
    int nSeq = GetLocalInt(m, "BT_SEQ_" + t) + 1;
    SetLocalInt(m, "BT_SEQ_" + t, nSeq);
    PLog(t + "." + sEv, GetTag(oBy));
    PLogI(t + "." + sEv + ".seq", nSeq);
    if (bTime) PLogF(t + "." + sEv + ".t", PSince(), fTol);
}

// A transition's OnClick repeats every 0.67 s while it doesn't move the clicker, so only whether it ran is compared.
void BCount(string t, string e)
{
    int n = GetLocalInt(GetModule(), "BT_N_" + t + e);
    PLogI(t + "." + e + ".count", e == "click" && n > 1 ? 1 : n);
}

void BCounts(string t)
{
    BCount(t, "death"); BCount(t, "damaged"); BCount(t, "open"); BCount(t, "closed"); BCount(t, "fail");
    BCount(t, "lock"); BCount(t, "unlock"); BCount(t, "click"); BCount(t, "attacked");
}

// What stands within 2.5 m of where the door stood (creatures left out), as "type:tag" in the order the shape lists them.
string BAround(string t)
{
    location l = GetLocalLocation(GetModule(), "BT_LOC_" + t);
    string s = "";
    object o = GetFirstObjectInShape(SHAPE_SPHERE, 2.5, l, FALSE, OBJECT_TYPE_ALL);
    while (GetIsObjectValid(o))
    {
        if (GetObjectType(o) != OBJECT_TYPE_CREATURE) s += IntToString(GetObjectType(o)) + ":" + GetTag(o) + " ";
        o = GetNextObjectInShape(SHAPE_SPHERE, 2.5, l, FALSE, OBJECT_TYPE_ALL);
    }
    return s;
}

void BMark(string t)
{
    object d = GetObjectByTag(t);
    SetLocalInt(d, "mark", 7);
    SetLocalLocation(GetModule(), "BT_LOC_" + t, GetLocation(d));
}

void BState(object d, string t, string sPhase)
{
    string k = t + "." + sPhase;
    PLogI(k + ".valid", GetIsObjectValid(d));
    object oTag = GetObjectByTag(t);
    PLogI(k + ".bytag", GetIsObjectValid(oTag));
    PLogI(k + ".same", oTag == d);
    PLogI(k + ".type", GetObjectType(d));
    // A door killed by a weapon has a random number of hit points left: only their sign is compared (BT_HPSIGN).
    if (GetLocalInt(GetModule(), "BT_HPSIGN_" + t)) PLogI(k + ".hp.above0", GetCurrentHitPoints(d) > 0);
    else PLogI(k + ".hp", GetCurrentHitPoints(d));
    PLogI(k + ".maxhp", GetMaxHitPoints(d));
    PLogI(k + ".open", GetIsOpen(d));
    PLogI(k + ".locked", GetLocked(d));
    PLogI(k + ".plot", GetPlotFlag(d));
    PLogI(k + ".dead", GetIsDead(d));
    PLogI(k + ".mark", GetLocalInt(d, "mark"));
    PLog(k + ".area", GetTag(GetArea(d)));
    vector p = GetPosition(d);
    PLogF(k + ".x", p.x, 0.01);
    PLogF(k + ".y", p.y, 0.01);
    PLog(k + ".target", GetTag(GetTransitionTarget(d)));
    object r = GetObjectByTag(t + "_R");
    if (GetIsObjectValid(r))
    {
        PLog(k + ".back", GetTag(GetTransitionTarget(r)));
        PLogI(k + ".back.valid", GetIsObjectValid(GetTransitionTarget(r)));
        PLogI(k + ".back.same", GetTransitionTarget(r) == d);
        PLogI(k + ".back.open", GetIsOpen(r));
    }
    PLog(k + ".around", BAround(t));
}

// The door actions a creature could take on the door (run it with AssignCommand on the creature).
void BCan(object d, string t, string sPhase)
{
    string k = t + "." + sPhase + ".can";
    PLogI(k + ".open", GetIsDoorActionPossible(d, DOOR_ACTION_OPEN));
    PLogI(k + ".unlock", GetIsDoorActionPossible(d, DOOR_ACTION_UNLOCK));
    PLogI(k + ".bash", GetIsDoorActionPossible(d, DOOR_ACTION_BASH));
    PLogI(k + ".knock", GetIsDoorActionPossible(d, DOOR_ACTION_KNOCK));
}

// When the door object stops being valid (polled every 0.1 s from its death; "-1" if it never does).
void BGone(object d, string t, float fStart, int n)
{
    if (!GetIsObjectValid(d)) { PLogF(t + ".gone.after", PSince() - fStart, 0.3); return; }
    if (n >= 600) { PLog(t + ".gone.after", "-1"); return; }
    DelayCommand(0.1, BGone(d, t, fStart, n + 1));
}

void BFollow(object d, string t)
{
    AssignCommand(GetModule(), DelayCommand(0.0, BState(d, t, "d0")));
    AssignCommand(GetModule(), DelayCommand(1.0, BState(d, t, "d1")));
    AssignCommand(GetModule(), DelayCommand(10.0, BState(d, t, "d10")));
}
