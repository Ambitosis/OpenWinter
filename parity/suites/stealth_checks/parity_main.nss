// Detection rates of stationary (or pacing) hiders in stealth by one observer each, 3 m away, in groups of 36
// pairs: how many are spotted / heard by 10 s (the first check, 4-8 s after the observer appears), by 30 s and by
// 70 s. A hider rolls its Hide and Move Silently once when it enters stealth; the observer checks on every second
// perception update (docs/STEALTH.md). Base ranks: hider Hide 1, Move Silently 1; observer Spot 3, Listen 3.
#include "pcombat_inc"
#include "parity_time"

object Make(string sRes, object oArea, float x, float y, int nFaction)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, Location(oArea, Vector(x, y, 0.0), 0.0));
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Boost(object o, int nSkill, int n)
{
    if (n > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(nSkill, n), o);
    if (n < 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillDecrease(nSkill, -n), o);
}

void Pace(object h, location a, location b)
{
    AssignCommand(h, ActionMoveToLocation(a));
    AssignCommand(h, ActionMoveToLocation(b));
    AssignCommand(h, ActionDoCommand(Pace(h, a, b)));
}

void Count(string g, float t)
{
    if (t > 0.0 && t <= 10.0) SetLocalInt(GetModule(), g + ".by10", GetLocalInt(GetModule(), g + ".by10") + 1);
    if (t > 0.0 && t <= 30.0) SetLocalInt(GetModule(), g + ".by30", GetLocalInt(GetModule(), g + ".by30") + 1);
    if (t > 0.0) SetLocalInt(GetModule(), g + ".by70", GetLocalInt(GetModule(), g + ".by70") + 1);
}

void Tally(object o, string g, int bHearing)
{
    Count(g, GetLocalFloat(o, bHearing ? "HEARD" : "SEEN"));
}

// nKind 0: sight (the hider's Move Silently +50), 1: hearing (its Hide +50).
void Pair(object oArea, int m, string g, int nKind, int bActive, int nObs, int bPace)
{
    float x = 10.0 + IntToFloat(m % 6) * 20.0, y = 10.0 + IntToFloat(m / 6) * 20.0;
    object o = Make("sc_obs", oArea, x - 1.5, y, STANDARD_FACTION_COMMONER);
    object h = Make("sc_hid", oArea, x + 1.5, y, STANDARD_FACTION_HOSTILE);
    SetLocalFloat(o, "BORN", PSince());
    SetLocalObject(o, "H", h);
    if (nKind == 0) { Boost(h, SKILL_MOVE_SILENTLY, 50); Boost(o, SKILL_SPOT, nObs); }
    else { Boost(h, SKILL_HIDE, 50); Boost(o, SKILL_LISTEN, nObs); }
    if (bActive) SetActionMode(o, ACTION_MODE_DETECT, TRUE);
    SetActionMode(h, ACTION_MODE_STEALTH, TRUE);
    if (bPace) Pace(h, Location(oArea, Vector(x + 1.5, y + 2.0, 0.0), 0.0), Location(oArea, Vector(x + 1.5, y - 2.0, 0.0), 0.0));
    DelayCommand(70.5, Tally(o, g, nKind));
}

void Group(int a, string g, int nKind, int bActive, int nObs, int bPace)
{
    object oArea = GetObjectByTag("SC_A" + IntToString(a));
    int m;
    for (m = 0; m < 36; m++) Pair(oArea, m, g, nKind, bActive, nObs, bPace);
}

void Report(string g)
{
    PLogRate(g + ".by10", GetLocalInt(GetModule(), g + ".by10"), 36);
    PLogRate(g + ".by30", GetLocalInt(GetModule(), g + ".by30"), 36);
    PLogRate(g + ".by70", GetLocalInt(GetModule(), g + ".by70"), 36);
}

void main()
{
    PStart();
    Group(0, "sight.active.d2", 0, TRUE, 0, FALSE);
    Group(1, "sight.active.d-8", 0, TRUE, -10, FALSE);
    Group(2, "sight.passive.d2", 0, FALSE, 0, FALSE);
    Group(3, "hearing.active.d-8", 1, TRUE, -10, FALSE);
    Group(4, "sight.active.d-8.pacing", 0, TRUE, -10, TRUE);
    Group(5, "sight.active.d2.areaspot-10", 0, TRUE, 0, FALSE);
    DelayCommand(71.0, Report("sight.active.d2"));
    DelayCommand(71.0, Report("sight.active.d-8"));
    DelayCommand(71.0, Report("sight.passive.d2"));
    DelayCommand(71.0, Report("hearing.active.d-8"));
    DelayCommand(71.0, Report("sight.active.d-8.pacing"));
    DelayCommand(71.0, Report("sight.active.d2.areaspot-10"));
    DelayCommand(72.0, PDone());
}
