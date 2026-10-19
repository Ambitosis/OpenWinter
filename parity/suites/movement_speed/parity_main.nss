// Movement speed: walk and run for every creaturespeed.2da row, and speed effects (increase, decrease,
// haste, slow, and how they stack). Speeds are measured between 0.5 s and 2.5 s after the order, away
// from the start and the braking before the destination.
#include "parity_inc"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y) { return Location(Area(), Vector(x, y, 0.0), 0.0); }

void Mark(object o, string k)
{
    SetLocalFloat(o, k, GetPosition(o).x);
}

float Median(float a, float b, float c)
{
    if (a > b) { float t = a; a = b; b = t; }
    if (b > c) b = c;
    return a > b ? a : b;
}

// Every lane runs three times, seconds apart, and reports the median: the original now and then stalls
// its creatures for a moment, which upsets one run's samples.
void Speed(object o, string k, float fExpected)
{
    object m = GetModule();
    float v = (GetPosition(o).x - GetLocalFloat(o, "X05")) / 2.0;
    float x = GetLocalFloat(o, "X05") - 2.0;
    int n = GetLocalInt(m, "RUN_" + k) + 1;
    SetLocalInt(m, "RUN_" + k, n);
    SetLocalFloat(m, "V" + IntToString(n) + "_" + k, v);
    SetLocalFloat(m, "X" + IntToString(n) + "_" + k, x);
    if (n < 3) return;
    v = Median(GetLocalFloat(m, "V1_" + k), GetLocalFloat(m, "V2_" + k), v);
    x = Median(GetLocalFloat(m, "X1_" + k), GetLocalFloat(m, "X2_" + k), x);
    // Real-time jitter: a sample can be a frame late (about 1% over the two seconds).
    PLogF(k + ".speed", v, 0.01 + v * 0.012);
    // Creatures start at full speed: where they are half a second in.
    PLogF(k + ".at0.5", x, 0.02 + v * 0.02);
}

object Go(string sRes, string k, float y, int bRun, float fExpected)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, L(2.0, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    AssignCommand(o, ActionMoveToLocation(L(38.0, y), bRun));
    DelayCommand(0.5, Mark(o, "X05"));
    DelayCommand(2.5, Speed(o, k, fExpected));
    DelayCommand(3.0, DestroyObject(o));
    return o;
}

void Rows()
{
    int r; float y = 1.5;
    for (r = 0; r <= 8; r++)
    {
        if (r == 7) continue;
        string s = IntToString(r);
        object w = Go("p_spd" + s, "row" + s + ".walk", y, FALSE, 2.0);
        Go("p_spd" + s, "row" + s + ".run", y + 1.5, TRUE, 4.0);
        if (!GetLocalInt(GetModule(), "ROWS_DONE")) PLogI("row" + s + ".rate", GetMovementRate(w));
        y += 3.0;
    }
    SetLocalInt(GetModule(), "ROWS_DONE", 1);
}

void Fx(string k, float y, int bRun, effect e1, effect e2, int nEffects)
{
    object o = Go("p_mover", k, y, bRun, 3.5);
    if (nEffects >= 1) ApplyEffectToObject(DURATION_TYPE_PERMANENT, e1, o);
    if (nEffects >= 2) ApplyEffectToObject(DURATION_TYPE_PERMANENT, e2, o);
}

void Effects()
{
    effect n = EffectVisualEffect(VFX_DUR_GLOW_WHITE);
    Fx("fx.none", 1.5, TRUE, n, n, 0);
    Fx("fx.inc50", 3.0, TRUE, EffectMovementSpeedIncrease(50), n, 1);
    Fx("fx.inc99", 4.5, TRUE, EffectMovementSpeedIncrease(99), n, 1);
    Fx("fx.inc20x2", 6.0, TRUE, EffectMovementSpeedIncrease(20), EffectMovementSpeedIncrease(20), 2);
    Fx("fx.inc30x2", 7.5, TRUE, EffectMovementSpeedIncrease(30), EffectMovementSpeedIncrease(30), 2);
    Fx("fx.dec50", 9.0, TRUE, EffectMovementSpeedDecrease(50), n, 1);
    Fx("fx.dec30.dec40", 10.5, TRUE, EffectMovementSpeedDecrease(30), EffectMovementSpeedDecrease(40), 2);
    Fx("fx.dec90", 12.0, TRUE, EffectMovementSpeedDecrease(90), n, 1);
    Fx("fx.inc50.dec90", 13.5, TRUE, EffectMovementSpeedIncrease(50), EffectMovementSpeedDecrease(90), 2);
    Fx("fx.inc50.dec30", 15.0, TRUE, EffectMovementSpeedIncrease(50), EffectMovementSpeedDecrease(30), 2);
    Fx("fx.haste", 16.5, TRUE, EffectHaste(), n, 1);
    Fx("fx.haste.inc25", 18.0, TRUE, EffectHaste(), EffectMovementSpeedIncrease(25), 2);
    Fx("fx.haste.dec50", 19.5, TRUE, EffectHaste(), EffectMovementSpeedDecrease(50), 2);
    Fx("fx.slow", 21.0, TRUE, EffectSlow(), n, 1);
    Fx("fx.slow.walk", 22.5, FALSE, EffectSlow(), n, 1);
    Fx("fx.slow.haste", 24.0, TRUE, EffectSlow(), EffectHaste(), 2);
    Fx("fx.slow.inc50", 25.5, TRUE, EffectSlow(), EffectMovementSpeedIncrease(50), 2);
    Fx("fx.slow.dec10", 27.0, TRUE, EffectSlow(), EffectMovementSpeedDecrease(10), 2);
    Fx("fx.walk.dec50", 28.5, FALSE, EffectMovementSpeedDecrease(50), n, 1);
    Fx("fx.walk.inc50", 30.0, FALSE, EffectMovementSpeedIncrease(50), n, 1);
    Fx("fx.entangle", 31.5, TRUE, EffectEntangle(), n, 1);
    Fx("fx.paralyze", 33.0, TRUE, EffectParalyze(), n, 1);
    Fx("fx.immobilize", 34.5, TRUE, EffectCutsceneImmobilize(), n, 1);
}

void main()
{
    DelayCommand(1.0, Rows());
    DelayCommand(5.0, Effects());
    DelayCommand(9.0, Rows());
    DelayCommand(13.0, Effects());
    DelayCommand(17.0, Rows());
    DelayCommand(21.0, Effects());
    DelayCommand(25.0, PDone());
}
