// EffectTimeStop on nwserver: what freezes while it lasts. Measured: nothing. On the dedicated server the effect isn't
// even kept (T.timestop stays 0), and every clock, walker, attack, heartbeat and effect duration runs on. The caster T (defender, no scripts) applies a 6 s time
// stop to itself at 2.0 s. Two clocks count half seconds: one runs DelayCommand on the module ("m"), the other on T
// ("t"). T reads the world at its own times (its clock ticks), so the readings don't depend on whether the module's
// timers stop.
//   W     told at 1.0 s to walk 30 m east (same area, 10 m from T)
//   WF    the same in a second area
//   A, D  A attacks D (immortal, counts the attacks) from 0.5 s
//   X     blinded for 4 s at 1.0 s (would end at 5 s)
//   HB    a heartbeat script counting beats
//   T     told at 3.0 s (its own clock) to walk 6 m north; a 4 s blindness of its own from 2.5 s
#include "pcond_inc"

object O(string t) { return GetObjectByTag(t); }

void MTick()
{
    object m = GetModule();
    SetLocalInt(m, "m", GetLocalInt(m, "m") + 1);
    if (GetLocalInt(m, "m") < 60) DelayCommand(0.5, MTick());
}

// Runs on T.
void TTick()
{
    object m = GetModule();
    SetLocalInt(m, "t", GetLocalInt(m, "t") + 1);
    if (GetLocalInt(m, "t") < 60) DelayCommand(0.5, TTick());
}

int HasType(object o, int nType)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e))
    {
        if (GetEffectType(e) == nType) return TRUE;
        e = GetNextEffect(o);
    }
    return FALSE;
}

void Read(string k)
{
    object m = GetModule();
    // Half-second DelayCommand chains drift by a tick over ten seconds.
    PLogF(k + ".m", IntToFloat(GetLocalInt(m, "m")), 1.0);
    PLogF(k + ".t", IntToFloat(GetLocalInt(m, "t")), 1.0);
    PLogF(k + ".W", CDist0(O("TS_W")), 1.5);
    PLogF(k + ".WF", CDist0(O("TS_WF")), 1.5);
    PLogF(k + ".T", CDist0(O("TS_T")), 1.5);
    PLogI(k + ".W.action", GetCurrentAction(O("TS_W")));
    PLogF(k + ".attacks", IntToFloat(GetLocalInt(m, "ts.atk.D")), 2.0);
    PLogI(k + ".X.blind", HasType(O("TS_X"), EFFECT_TYPE_BLINDNESS));
    PLogI(k + ".T.blind", HasType(O("TS_T"), EFFECT_TYPE_BLINDNESS));
    PLogI(k + ".T.timestop", HasType(O("TS_T"), EFFECT_TYPE_TIMESTOP));
    // A creature's heartbeat comes 6 to 7.2 s after the last.
    PLogF(k + ".beats", IntToFloat(GetLocalInt(O("TS_HB"), "beats")), 1.0);
    PLogI(k + ".clock", GetTimeSecond() + 60 * GetTimeMinute());
    PLogI(k + ".T.commandable", GetCommandable(O("TS_T")));
    PLogI(k + ".W.commandable", GetCommandable(O("TS_W")));
}

// The module's own reading at module time 5 s: how far T's clock has got.
void MRead(string k)
{
    object m = GetModule();
    PLogF(k + ".t", IntToFloat(GetLocalInt(m, "t")), 1.0);
    PLogF(k + ".W", CDist0(O("TS_W")), 1.5);
}

void Walk(object o, float dx, float dy)
{
    vector v = GetPosition(o);
    AssignCommand(o, ActionMoveToLocation(Location(GetArea(o), v + Vector(dx, dy, 0.0), 0.0), FALSE));
}

void TStart()
{
    object t = OBJECT_SELF;
    DelayCommand(0.3, TTick());
    DelayCommand(2.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectTimeStop(), t, 6.0));
    DelayCommand(2.5, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectBlindness(), t, 4.0));
    DelayCommand(3.0, Walk(t, 0.0, 6.0));
    DelayCommand(1.8, Read("t1_8"));
    DelayCommand(2.4, Read("t2_4"));
    DelayCommand(3.5, Read("t3_5"));
    DelayCommand(5.5, Read("t5_5"));
    DelayCommand(7.0, Read("t7"));
    DelayCommand(7.8, Read("t7_8"));
    DelayCommand(8.5, Read("t8_5"));
    DelayCommand(10.0, Read("t10"));
    DelayCommand(14.0, Read("t14"));
}

void main()
{
    object a = CArea();
    object far = O("TS_FAR");
    object t = CSpawn(20.0, 20.0, "ts", STANDARD_FACTION_DEFENDER, FALSE, "nw_bandit001", "TS_T");
    object w = CSpawn(10.0, 30.0, "ts", STANDARD_FACTION_DEFENDER, FALSE, "nw_bandit001", "TS_W");
    object wf = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(far, Vector(2.0, 15.0, 0.0), 0.0), FALSE, "TS_WF");
    int ev;
    for (ev = 5000; ev <= 5012; ev++) SetEventScript(wf, ev, "");
    SetAILevel(wf, AI_LEVEL_NORMAL);
    SetLocalFloat(wf, "X0", 2.0);
    SetLocalFloat(wf, "Y0", 15.0);
    object atk = CSpawn(40.0, 10.0, "ts", STANDARD_FACTION_HOSTILE, FALSE, "nw_bandit001", "TS_A");
    object d = CNeighbour(41.5, 10.0, "ts", "D", STANDARD_FACTION_DEFENDER);
    SetTag(d, "TS_D");
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectAttackIncrease(20), atk);
    object x = CSpawn(40.0, 25.0, "ts", STANDARD_FACTION_DEFENDER, FALSE, "nw_bandit001", "TS_X");
    object hb = CSpawn(40.0, 30.0, "ts", STANDARD_FACTION_DEFENDER, FALSE, "nw_bandit001", "TS_HB");
    SetEventScript(hb, EVENT_SCRIPT_CREATURE_ON_HEARTBEAT, "ts_hb");

    DelayCommand(0.3, MTick());
    AssignCommand(t, TStart());
    DelayCommand(0.5, AssignCommand(atk, ActionAttack(d)));
    DelayCommand(1.0, Walk(w, 30.0, 0.0));
    DelayCommand(1.0, Walk(wf, 25.0, 0.0));
    DelayCommand(1.0, ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectBlindness(), x, 4.0));
    DelayCommand(5.0, MRead("m5"));
    DelayCommand(12.0, MRead("m12"));
    DelayCommand(16.0, PDone());
}
