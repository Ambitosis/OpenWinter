// Which statescripts.2da script runs on a creature under a condition, and when.
// statescripts.2da is overridden so rows 1-9 run ss_st<row>, which only logs. Every
// actor is a stripped nw_bandit001 (no event scripts, AI normal), alone in its cell; the condition lands at 2 s.
//   conf, fear, turned, charm, dom, daze, stun, hold, sleep   one condition each
//   both (confused + frightened), charmconf (charmed + confused), sleepconf (asleep + confused)   two at once
//   conf.hb    confused, with an OnHeartbeat that logs (order of OnHeartbeat and the state script)
//   conf.assoc confused henchman of an NPC master, with an OnHeartbeat that logs (does it still run?)
//   ctrl       no condition, OnHeartbeat logs
// Commandable is read on fear (its stock script locks; ss_st3 doesn't) and confusion, and after a 3 s fear ends.
#include "ss_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }

int nCell = 0;

object Actor(string g, int bHeartbeat = FALSE)
{
    float x = 8.0 + IntToFloat(nCell % 6) * 16.0, y = 8.0 + IntToFloat(nCell / 6) * 16.0;
    nCell++;
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(Area(), Vector(x, y, 0.0), 0.0));
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(o, ev, "");
    if (bHeartbeat) SetEventScript(o, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "ss_hb");
    SetAILevel(o, AI_LEVEL_NORMAL);
    ChangeToStandardFaction(o, STANDARD_FACTION_HOSTILE);
    SetLocalString(o, "G", g);
    SetLocalString(GetModule(), "GROUPS", GetLocalString(GetModule(), "GROUPS") + g + " ");
    return o;
}

// A creator standing 6 m off (defender faction), for effects that care who made them.
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
        case 10: return EffectPetrify();
        case 11: return EffectCutsceneParalyze();
        case 12: return EffectKnockdown();
        case 13: return EffectEntangle();
    }
    return EffectConfused();
}

void Land(object o, int n1, int n2, float fDur)
{
    SetLocalFloat(o, "T0", PNow());
    int d = fDur > 0.0 ? DURATION_TYPE_TEMPORARY : DURATION_TYPE_PERMANENT;
    ApplyEffectToObject(d, Fx(n1), o, fDur);
    if (n2 > 0) ApplyEffectToObject(d, Fx(n2), o, fDur);
    if (GetLocalInt(o, "CMD00")) PLogI(GetLocalString(o, "G") + ".cmd00", GetCommandable(o));
}

void LandBy(object k, object o, int n, int n2)
{
    SetLocalFloat(o, "T0", PNow());
    AssignCommand(k, ApplyEffectToObject(DURATION_TYPE_PERMANENT, Fx(n), o));
    if (GetLocalInt(o, "CMD00")) AssignCommand(k, PLogI(GetLocalString(o, "G") + ".cmd00", GetCommandable(o)));
    if (n2 > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, Fx(n2), o);
}

void Group(string g, int nCount, int n1, int n2 = 0, int bHeartbeat = FALSE)
{
    int i;
    for (i = 0; i < nCount; i++)
    {
        object o = Actor(g, bHeartbeat);
        if (n1 == 1 || n1 == 7) DelayCommand(2.0, LandBy(Creator(o), o, n1, n2));
        else if (n1 > 0) DelayCommand(2.0, Land(o, n1, n2, 0.0));
        SetLocalObject(GetModule(), g + IntToString(i), o);
    }
    SetLocalInt(GetModule(), g + ".count", nCount);
}

void Cmd(object o, string k)
{
    PLogI(k, GetCommandable(o));
}

void Has(object o, int nType, string k)
{
    int n = 0;
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { if (GetEffectType(e) == nType) n++; e = GetNextEffect(o); }
    PLogI(k, n);
}

void Report(string g)
{
    object m = GetModule();
    int n = GetLocalInt(m, g + ".count");
    string rows = "";
    int r;
    for (r = 1; r <= 9; r++) if (GetLocalInt(m, g + ".row" + IntToString(r)) > 0) rows += IntToString(r);
    PLog(g + ".rows", rows == "" ? "none" : rows);
    if (n > 0) PLogF(g + ".runs", IntToFloat(GetLocalInt(m, g + ".runs")) / IntToFloat(n), 1.01);
    int fn = GetLocalInt(m, g + ".firstn");
    if (fn > 0)
    {
        PLogF(g + ".first", GetLocalFloat(m, g + ".first") / IntToFloat(fn), 0.15);
        PLogF(g + ".firstmax", GetLocalFloat(m, g + ".firstmax"), 0.25);
    }
    int gn = GetLocalInt(m, g + ".gapn");
    if (gn > 0)
    {
        // Gaps are uniform over 1.2 s (standard deviation 0.35 s): 3.5 deviations of the difference of two means of
        // gn gaps, and more room for the extremes of a few.
        float tol = 3.5 * 0.35 * sqrt(2.0 / IntToFloat(gn));
        PLogF(g + ".gap", GetLocalFloat(m, g + ".gap") / IntToFloat(gn), tol < 0.3 ? 0.3 : tol);
        PLogF(g + ".gapmin", GetLocalFloat(m, g + ".gapmin"), gn < 10 ? 0.7 : 0.4);
        PLogF(g + ".gapmax", GetLocalFloat(m, g + ".gapmax"), gn < 10 ? 0.7 : 0.4);
    }
    int i;
    for (i = 0; i < n && i < 2; i++)
    {
        object o = GetLocalObject(m, g + IntToString(i));
        WriteTimestampedLogEntry("RAW|" + g + IntToString(i) + ".seq|" + GetLocalString(o, "SEQ"));
    }
}

// The order of the first entries after the condition lands, without times ("h 2 h 2").
void Order(string g, int nTokens)
{
    object o = GetLocalObject(GetModule(), g + "0");
    string s = GetLocalString(o, "SEQ"), out = "";
    int i;
    for (i = 0; i < nTokens && s != ""; i++)
    {
        int sp = FindSubString(s, " ");
        string tok = GetStringLeft(s, sp);
        s = GetSubString(s, sp + 1, 100000);
        out += (i == 0 ? "" : " ") + GetStringLeft(tok, FindSubString(tok, "@"));
    }
    PLog(g + ".order", out);
}

void Assoc()
{
    object master = Actor("conf.master");
    object o = Actor("conf.assoc", TRUE);
    AddHenchman(master, o);
    SetLocalObject(GetModule(), "conf.assoc0", o);
    SetLocalInt(GetModule(), "conf.assoc.count", 1);
    DelayCommand(14.0, Land(o, 2, 0, 0.0));
    DelayCommand(45.0, PLogI("conf.assoc.hb.before", GetLocalInt(o, "HB0") > 0));
    DelayCommand(45.0, PLogI("conf.assoc.ismaster", GetMaster(o) == master));
}

void main()
{
    PStart();
    Group("conf", 6, 2);
    Group("conf.hb", 2, 2, 0, TRUE);
    Group("fear", 2, 3);
    Group("turned", 2, 4);
    Group("charm", 2, 1);
    Group("dom", 2, 7);
    Group("daze", 2, 5);
    Group("stun", 2, 6);
    Group("hold", 2, 8);
    Group("sleep", 2, 9);
    Group("both", 2, 2, 3);
    Group("charmconf", 2, 1, 2);
    Group("sleepconf", 2, 9, 2);
    Group("ctrl", 1, 0, 0, TRUE);
    Group("dazeconf", 2, 5, 2);
    Group("domconf", 2, 7, 2);
    Group("daze.hb", 1, 5, 0, TRUE);
    Group("petrify", 1, 10, 0, TRUE);
    Group("cutpara", 1, 11, 0, TRUE);
    Group("knock", 1, 12, 0, TRUE);
    Group("entangle", 1, 13, 0, TRUE);
    Group("dom.mod", 1, 0);
    DelayCommand(2.0, Land(GetLocalObject(GetModule(), "dom.mod0"), 7, 0, 0.0));
    Assoc();
    // A fear that ends after 3 s.
    object fe = Actor("fear.end");
    DelayCommand(2.0, Land(fe, 3, 0, 3.0));
    DelayCommand(8.0, Cmd(fe, "fear.end.cmd8"));
    object m = GetModule();
    SetLocalInt(GetLocalObject(m, "fear0"), "CMD00", TRUE);
    SetLocalInt(GetLocalObject(m, "charm0"), "CMD00", TRUE);
    SetLocalInt(GetLocalObject(m, "conf0"), "CMD00", TRUE);
    SetLocalInt(GetLocalObject(m, "turned0"), "CMD00", TRUE);
    DelayCommand(2.1, Cmd(GetLocalObject(m, "fear0"), "fear.cmd01"));
    DelayCommand(2.6, Cmd(GetLocalObject(m, "fear0"), "fear.cmd06"));
    DelayCommand(4.0, Cmd(GetLocalObject(m, "fear0"), "fear.cmd20"));
    DelayCommand(2.1, Cmd(GetLocalObject(m, "charm0"), "charm.cmd01"));
    DelayCommand(4.0, Cmd(GetLocalObject(m, "charm0"), "charm.cmd20"));
    DelayCommand(2.1, Cmd(GetLocalObject(m, "conf0"), "conf.cmd01"));
    DelayCommand(4.0, Cmd(GetLocalObject(m, "conf0"), "conf.cmd20"));
    DelayCommand(4.0, Cmd(GetLocalObject(m, "turned0"), "turned.cmd20"));
    DelayCommand(4.0, Cmd(GetLocalObject(m, "daze0"), "daze.cmd20"));
    DelayCommand(4.0, Cmd(GetLocalObject(m, "dom0"), "dom.cmd20"));
    DelayCommand(4.0, Has(GetLocalObject(m, "dom0"), EFFECT_TYPE_DOMINATED, "dom.has"));
    DelayCommand(4.0, Has(GetLocalObject(m, "daze0"), EFFECT_TYPE_DAZED, "daze.has"));
    DelayCommand(4.0, Has(GetLocalObject(m, "dom.mod0"), EFFECT_TYPE_DOMINATED, "dom.mod.has"));
    string gs = "dazeconf domconf daze.hb petrify cutpara knock entangle conf conf.hb fear turned charm dom dom.mod daze stun hold sleep both charmconf sleepconf ctrl conf.assoc ";
    while (gs != "")
    {
        int sp = FindSubString(gs, " ");
        DelayCommand(46.0, Report(GetStringLeft(gs, sp)));
        gs = GetSubString(gs, sp + 1, 1000);
    }
    DelayCommand(46.0, Order("conf.hb", 4));
    DelayCommand(46.0, Order("daze.hb", 2));
    DelayCommand(46.0, Order("petrify", 2));
    DelayCommand(46.0, Order("cutpara", 2));
    DelayCommand(46.0, Order("knock", 2));
    DelayCommand(46.0, Order("entangle", 2));
    DelayCommand(46.5, PDone());
}
