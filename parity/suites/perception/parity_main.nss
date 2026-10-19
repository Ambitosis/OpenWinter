// Perception: when OnPerception fires (every 4 s from the observer's creation), GetLastPerceived and
// seen / heard / vanished / inaudible and their order, and the sight and hearing ranges for each
// PerceptionRange. (Invisible creatures are left out: the original hears them on some updates and not others.)
#include "parity_inc"
#include "parity_time"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y, float f = 0.0) { return Location(Area(), Vector(x, y, 0.0), f); }

object Make(string sRes, string sTag, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, L(x, y), FALSE, sTag);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetLocalFloat(o, "BORN", PSince());
    return o;
}

void Seen(object o, object t, string k)
{
    PLogI(k + ".seen", GetObjectSeen(t, o));
    PLogI(k + ".heard", GetObjectHeard(t, o));
}

// One walker passes observers with each PerceptionRange; the distances where events fire give the ranges.
void Ranges()
{
    Make("p_obs8", "OBS_SHORT", 2.0, 20.0);
    Make("p_obs9", "OBS_MEDIUM", 2.0, 20.5);
    Make("p_obs10", "OBS_LONG", 2.0, 19.5);
    Make("p_obs11", "OBS_DEFAULT", 2.0, 21.0);
    object w = Make("p_tgt", "WALKER", 38.0, 20.0);
    AssignCommand(w, ActionMoveToLocation(L(4.0, 20.0), FALSE));
    AssignCommand(w, ActionMoveToLocation(L(38.0, 20.0), FALSE));
}

void Jump(float x, float y)
{
    AssignCommand(GetObjectByTag("JUMPER"), JumpToLocation(L(x, y)));
}

// A creature that jumps into or out of range is noticed at the observer's next update, not at once.
void Jumps()
{
    Make("p_obs8", "OBS_JUMP", 36.0, 4.0);
    Make("p_tgt", "JUMPER", 4.0, 36.0);
    DelayCommand(5.5, Jump(36.0, 6.0));
    DelayCommand(14.5, Jump(4.0, 36.0));
}

void Stop()
{
    SetLocalInt(GetModule(), "PARITY_STOP", 1);
    PDone();
}

void main()
{
    PStart();
    DelayCommand(1.0, Ranges());
    DelayCommand(1.0, Jumps());
    DelayCommand(46.0, Stop());
}
