// GetIsInSubArea with areas of effect: who counts as inside a 5 m acid fog laid on the ground and one
// laid 4 m up (a sphere or a cylinder), creatures standing there when it appears and ones walking through, what
// OnEnter and OnExit read, a mobile fear aura (radius 4) carried past a stander, and a wall of fire (10 x 2).
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y, float z = 0.0) { return Location(Area(), Vector(x, y, z), 0.0); }

object Actor(string sTag, float x, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", L(x, y), FALSE, sTag);
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, STANDARD_FACTION_MERCHANT);
    SetAILevel(o, AI_LEVEL_NORMAL);
    SetPlotFlag(o, TRUE);
    return o;
}

void Cast(int nType, location l)
{
    ApplyEffectAtLocation(DURATION_TYPE_TEMPORARY, EffectAreaOfEffect(nType, "sa_enter", "", "sa_exit"), l, 120.0);
}

void Tag(string k);

// The fog is laid by a caster far off (an area of effect whose creator is gone is removed).
void Aoe(string k, int nType, location l)
{
    AssignCommand(GetObjectByTag("CASTER"), Cast(nType, l));
    SetLocalLocation(GetModule(), "AL_" + k, l);
    DelayCommand(0.05, Tag(k));
}
void Tag(string k)
{
    location l = GetLocalLocation(GetModule(), "AL_" + k);
    object a = GetNearestObjectToLocation(OBJECT_TYPE_AREA_OF_EFFECT, l);
    SetLocalString(a, "K", k);
    SetLocalObject(GetModule(), "AOE_" + k, a);
}

// Tags can't hold a dot: "4.5" becomes "4p5".
string Dotless(string d)
{
    int i = FindSubString(d, ".");
    return i < 0 ? d : GetStringLeft(d, i) + "p" + GetSubString(d, i + 1, 10);
}

// Standers at these flat distances from (cx, cy), each at its own angle.
string g_d = "0 1.5 2 2.5 3 3.5 4 4.2 4.3 4.4 4.5 4.6 4.7 4.8 4.9 5 5.1 5.5 ";

void Standers(string k, float cx, float cy)
{
    string s = g_d;
    float f = 0.0;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string d = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 100);
        float r = StringToFloat(d);
        Actor(k + "_" + Dotless(d), cx + r * cos(f), cy + r * sin(f));
        f += 40.0;
    }
}

void ReadStanders(string k, string sAoe)
{
    object a = GetLocalObject(GetModule(), "AOE_" + sAoe);
    string s = g_d;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string d = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 100);
        object o = GetObjectByTag(k + "_" + Dotless(d));
        PLogI(sAoe + "." + d + ".in", GetIsInSubArea(o, a));
        vector va = GetPosition(a), vo = GetPosition(o);
        PLogF(sAoe + "." + d + ".dist", VectorMagnitude(Vector(vo.x - va.x, vo.y - va.y, 0.0)), 0.05);
        PLog(sAoe + "." + d + ".ev", GetLocalString(GetModule(), "EV_" + sAoe + "." + k + "_" + Dotless(d)));
    }
}

// Before: creatures standing when the fog appears. After: creatures created inside a fog already there.
void Phase0()
{
    Actor("CASTER", 2.0, 2.0);
    Standers("S1", 20.0, 20.0);
    Standers("S2", 20.0, 50.0);
}
void Phase1()
{
    Aoe("UP", AOE_PER_FOGACID, L(20.0, 50.0, 4.0));
    Aoe("FLAT", AOE_PER_FOGACID, L(20.0, 20.0));
    Aoe("LATE", AOE_PER_FOGACID, L(60.0, 50.0));
    Aoe("UP2", AOE_PER_FOGACID, L(40.0, 65.0, 4.0));
    Aoe("FLAT2", AOE_PER_FOGACID, L(15.0, 65.0));
}
void Phase2()
{
    Standers("S3", 60.0, 50.0);
    // A walker through the flat fog, west to east.
    object w = Actor("W1", 5.0, 66.0);
    AssignCommand(w, ActionMoveToLocation(L(25.0, 66.0)));
    // Walkers through the raised fog at 2 m and 0 m off its centre line.
    object w2 = Actor("W2", 30.0, 67.0);
    AssignCommand(w2, ActionMoveToLocation(L(50.0, 67.0)));
    object w3 = Actor("W3", 40.0, 72.0);
    DelayCommand(6.0, AssignCommand(w3, ActionMoveToLocation(L(40.0, 65.0))));
    // A fear aura on a walker passing a stander 2 m off its line.
    object c = Actor("CARRIER", 55.0, 74.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAreaOfEffect(AOE_MOB_FEAR, "sa_enter", "", "sa_exit"), c);
    Actor("PASSED", 65.0, 76.0);
    // A wall of fire, east-west at (60, 20); standers at offsets.
    Aoe("WALL", AOE_PER_WALLFIRE, L(60.0, 20.0));
    PLogF("up.z", GetPosition(GetLocalObject(GetModule(), "AOE_UP")).z, 0.05);
    Actor("WL_N05", 60.0, 20.5); Actor("WL_N15", 61.0, 21.5); Actor("WL_E4", 64.0, 19.0); Actor("WL_E6", 66.0, 20.0);
    Actor("WL_N4", 59.0, 24.0); Actor("WL_E45", 64.5, 20.0);
}
void Phase3()
{
    object c = GetObjectByTag("CARRIER");
    object a = GetNearestObjectToLocation(OBJECT_TYPE_AREA_OF_EFFECT, GetLocation(c));
    SetLocalString(a, "K", "MOB");
    SetLocalObject(GetModule(), "AOE_MOB", a);
    AssignCommand(c, ActionMoveToLocation(L(75.0, 74.0)));
}

void Poll(int n)
{
    object m = GetModule();
    string s = "W1 W2 PASSED ";
    string sA = "FLAT2 UP2 MOB ";
    while (s != "")
    {
        int p = FindSubString(s, " "), q = FindSubString(sA, " ");
        string t = GetStringLeft(s, p), k = GetStringLeft(sA, q);
        s = GetSubString(s, p + 1, 100); sA = GetSubString(sA, q + 1, 100);
        object o = GetObjectByTag(t), a = GetLocalObject(m, "AOE_" + k);
        int in = GetIsInSubArea(o, a);
        if (in != GetLocalInt(o, "IN"))
        {
            SetLocalInt(o, "IN", in);
            vector va = GetPosition(a), vo = GetPosition(o);
            SetLocalString(o, "SEQ", GetLocalString(o, "SEQ") + IntToString(in) + " ");
        }
    }
    if (n > 0) DelayCommand(0.1, Poll(n - 1));
}

void Report()
{
    object m = GetModule();
    ReadStanders("S1", "FLAT");
    ReadStanders("S2", "UP");
    ReadStanders("S3", "LATE");
    PLogI("valid.FLAT", GetIsObjectValid(GetLocalObject(m, "AOE_FLAT")));
    PLogI("valid.UP", GetIsObjectValid(GetLocalObject(m, "AOE_UP")));
    PLog("W3.ev", GetLocalString(m, "EV_UP2.W3"));
    PLogI("W3.in", GetIsInSubArea(GetObjectByTag("W3"), GetLocalObject(m, "AOE_UP2")));
    PLog("W1.ev", GetLocalString(m, "EV_FLAT2.W1"));
    PLogF("W1.enterdist", GetLocalFloat(m, "D_FLAT2.W1"), 0.4);
    PLogF("W1.exitdist", GetLocalFloat(m, "X_FLAT2.W1"), 0.4);
    PLog("W2.ev", GetLocalString(m, "EV_UP2.W2"));
    PLogF("W2.enterdist", GetLocalFloat(m, "D_UP2.W2"), 0.4);
    PLog("PASSED.ev", GetLocalString(m, "EV_MOB.PASSED"));
    PLogF("PASSED.enterdist", GetLocalFloat(m, "D_MOB.PASSED"), 0.4);
    PLog("W1.seq", GetLocalString(GetObjectByTag("W1"), "SEQ"));
    PLog("W2.seq", GetLocalString(GetObjectByTag("W2"), "SEQ"));
    PLog("PASSED.seq", GetLocalString(GetObjectByTag("PASSED"), "SEQ"));
    object w = GetLocalObject(m, "AOE_WALL");
    string s = "WL_N05 WL_N15 WL_E4 WL_E6 WL_N4 WL_E45 ";
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string t = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 100);
        PLogI("wall." + t + ".in", GetIsInSubArea(GetObjectByTag(t), w));
        PLog("wall." + t + ".ev", GetLocalString(m, "EV_WALL." + t));
    }
    PLogI("nonCreature", GetIsInSubArea(GetLocalObject(m, "AOE_UP"), GetLocalObject(m, "AOE_FLAT")));
}

void main()
{
    DelayCommand(0.5, Phase0());
    DelayCommand(1.0, Phase1());
    DelayCommand(3.0, Phase2());
    DelayCommand(3.5, Phase3());
    DelayCommand(3.6, Poll(150));
    DelayCommand(20.0, Report());
    DelayCommand(21.0, PDone());
}
