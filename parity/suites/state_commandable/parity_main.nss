// Commandability under conditions.
// statescripts.2da is overridden so rows 1-9 run sc_st<row>, which only logs (and reads GetCommandable inside).
// Every actor is a stripped nw_bandit001 (no event scripts, AI normal, hostile faction), alone in its cell, with no
// player in the area. The condition lands 2 s in.
//   <c>.same   GetCommandable in the script that applied the condition, right after ApplyEffectToObject
//   <c>.d01    0.1 s later (DelayCommand); <c>.d30 3 s later
//   <c>.instate GetCommandable read inside the state script (mean over its runs)
//   daze.preset      SetCommandable(FALSE), then daze: commandable right after?
//   dazeconf.*       dazed (6 s), a 60 s ActionWait queued, then confused under the daze: commandable and the
//                    current action while dazed, and after the daze has ended
//   preset.conf.*    SetCommandable(FALSE), then confused, then the confusion removed by script
//   conf.remove      confused, then removed by script: commandable right after RemoveEffect, same script
//   fearstun.*       frightened + stunned, the stun removed by script at 5 s: commandable right after, same script
#include "sc_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }
int nCell = 0;

object Actor(string g)
{
    float x = 8.0 + IntToFloat(nCell % 6) * 16.0, y = 8.0 + IntToFloat(nCell / 6) * 16.0;
    nCell++;
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(Area(), Vector(x, y, 0.0), 0.0));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, STANDARD_FACTION_HOSTILE);
    SetLocalString(o, "G", g);
    SetLocalObject(GetModule(), g, o);
    return o;
}

object Creator(object o)
{
    vector v = GetPosition(o);
    object k = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(Area(), Vector(v.x, v.y + 6.0, 0.0), 180.0));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(k, ev, "");
    SetAILevel(k, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(k, STANDARD_FACTION_DEFENDER);
    SetPlotFlag(k, TRUE);
    return k;
}

effect Fx(int n)
{
    switch (n)
    {
        case 1: return EffectCharmed();
        case 2: return EffectConfused();
        case 3: return EffectFrightened();
        case 4: return EffectTurned();
        case 5: return EffectDazed();
        case 6: return EffectStunned();
        case 7: return EffectDominated();
        case 8: return EffectParalyze();
        case 9: return EffectSleep();
    }
    return EffectConfused();
}

void Cmd(object o, string k) { PLogI(k, GetCommandable(o)); }

// Applies condition n (for fDur seconds, or permanently) and reads GetCommandable in the same script.
void Land(object o, int n, float fDur, string sKey)
{
    if (GetLocalFloat(o, "T0") <= 0.0) SetLocalFloat(o, "T0", PNow());
    if (fDur > 0.0) ApplyEffectToObject(DURATION_TYPE_TEMPORARY, Fx(n), o, fDur);
    else ApplyEffectToObject(DURATION_TYPE_PERMANENT, Fx(n), o);
    if (sKey != "") PLogI(sKey + ".same", GetCommandable(o));
}

void Simple(string g, int n)
{
    object o = Actor(g);
    if (n == 1 || n == 7)
    {
        object k = Creator(o);
        DelayCommand(2.0, AssignCommand(k, Land(o, n, 0.0, g)));
    }
    else DelayCommand(2.0, Land(o, n, 0.0, g));
    DelayCommand(2.1, Cmd(o, g + ".d01"));
    DelayCommand(5.0, Cmd(o, g + ".d30"));
}

void DazePreset()
{
    object o = Actor("daze.preset");
    DelayCommand(1.9, SetCommandable(FALSE, o));
    DelayCommand(1.95, Cmd(o, "daze.preset.before"));
    DelayCommand(2.0, Land(o, 5, 0.0, "daze.preset"));
    DelayCommand(2.1, Cmd(o, "daze.preset.d01"));
}

void Remove(object o, int nType, string sKey)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == nType) { RemoveEffect(o, e); break; }
        e = GetNextEffect(o);
    }
    ScMark(o, "rm" + IntToString(nType));
    PLogI(sKey, GetCommandable(o));
}

void Action(object o, string k) { PLogI(k, GetCurrentAction(o)); }

void QueueWait(object o) { AssignCommand(o, ActionWait(60.0)); }

void DazeConf()
{
    object o = Actor("dazeconf");
    DelayCommand(2.0, Land(o, 5, 6.0, ""));
    DelayCommand(2.2, QueueWait(o));
    DelayCommand(2.3, Land(o, 2, 0.0, "dazeconf.conf"));
    DelayCommand(2.5, Cmd(o, "dazeconf.cmd.dazed"));
    DelayCommand(2.5, Action(o, "dazeconf.action.dazed"));
    DelayCommand(8.5, Cmd(o, "dazeconf.cmd.after"));
    DelayCommand(8.5, Action(o, "dazeconf.action.after"));
}

void PresetConf()
{
    object o = Actor("preset.conf");
    DelayCommand(1.9, SetCommandable(FALSE, o));
    DelayCommand(2.0, Land(o, 2, 0.0, "preset.conf"));
    DelayCommand(5.0, Remove(o, EFFECT_TYPE_CONFUSED, "preset.conf.removed"));
    DelayCommand(5.1, Cmd(o, "preset.conf.removed.d01"));
}

void ConfRemove()
{
    object o = Actor("conf.remove");
    DelayCommand(2.0, Land(o, 2, 0.0, ""));
    DelayCommand(5.0, Remove(o, EFFECT_TYPE_CONFUSED, "conf.remove.removed"));
}

void FearStun()
{
    object o = Actor("fearstun");
    DelayCommand(2.0, Land(o, 3, 0.0, ""));
    DelayCommand(2.0, Land(o, 6, 0.0, "fearstun"));
    DelayCommand(5.0, Remove(o, EFFECT_TYPE_STUNNED, "fearstun.removed"));
    DelayCommand(5.1, Cmd(o, "fearstun.removed.d01"));
}

void Report(string g)
{
    object m = GetModule();
    object o = GetLocalObject(m, g);
    int n = GetLocalInt(m, g + ".runs");
    PLogI(g + ".runs", n > 0);
    if (n > 0) PLogF(g + ".instate", IntToFloat(GetLocalInt(m, g + ".incmd")) / IntToFloat(n), 0.01);
    WriteTimestampedLogEntry("RAW|" + g + ".seq|" + GetLocalString(o, "SEQ"));
}

void main()
{
    PStart();
    Simple("conf", 2);
    Simple("stun", 6);
    Simple("hold", 8);
    Simple("sleep", 9);
    Simple("dom", 7);
    Simple("fear", 3);
    Simple("charm", 1);
    Simple("turned", 4);
    Simple("daze", 5);
    DazePreset();
    DazeConf();
    PresetConf();
    ConfRemove();
    FearStun();
    string gs = "conf stun hold sleep dom fear charm turned daze daze.preset dazeconf preset.conf conf.remove fearstun ";
    while (gs != "")
    {
        int sp = FindSubString(gs, " ");
        DelayCommand(30.0, Report(GetStringLeft(gs, sp)));
        gs = GetSubString(gs, sp + 1, 1000);
    }
    DelayCommand(31.0, PDone());
}
