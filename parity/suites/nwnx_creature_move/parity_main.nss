// NWNX_Creature movement: the movement rate factor and its cap, the walk rate cap, SetMovementRate and the
// movement type, read back and measured as speed between 0.5 s and 2.5 s after a move order.
#include "parity_inc"
#include "nwnx_creature"

object Area() { return GetObjectByTag("PARITY_AREA"); }
location L(float x, float y) { return Location(Area(), Vector(x, y, 0.0), 0.0); }

int GetHasEffect(int nType, object o)
{
    effect e = GetFirstEffect(o);
    while (GetIsEffectValid(e)) { if (GetEffectType(e) == nType) return TRUE; e = GetNextEffect(o); }
    return FALSE;
}

void Mark(object o) { SetLocalFloat(o, "X05", GetPosition(o).x); }

void Speed(object o, string k)
{
    float v = (GetPosition(o).x - GetLocalFloat(o, "X05")) / 2.0;
    PLogF(k + ".speed", v, 0.02 + v * 0.03);
    PLogF(k + ".factor", NWNX_Creature_GetMovementRateFactor(o));
    PLogI(k + ".type", NWNX_Creature_GetMovementType(o));
}

object Make(string k, float y)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, "p_mover", L(2.0, y), FALSE, k);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

void Go(object o, string k, float y, int bRun)
{
    AssignCommand(o, ActionMoveToLocation(L(38.0, y), bRun));
    DelayCommand(0.5, Mark(o));
    DelayCommand(1.0, PLogI(k + ".typemoving", NWNX_Creature_GetMovementType(o)));
    DelayCommand(2.5, Speed(o, k));
    DelayCommand(3.0, DestroyObject(o));
}

void Lane(string k, float y, int bRun, float fFactor, float fCap, float fWalkCap, int nRate, effect e, int bEffect)
{
    object o = Make(k, y);
    if (bEffect) ApplyEffectToObject(DURATION_TYPE_PERMANENT, e, o);
    if (fCap > 0.0) NWNX_Creature_SetMovementRateFactorCap(o, fCap);
    if (fFactor > 0.0) NWNX_Creature_SetMovementRateFactor(o, fFactor);
    if (fWalkCap != 0.0) NWNX_Creature_SetWalkRateCap(o, fWalkCap);
    if (nRate >= 0) NWNX_Creature_SetMovementRate(o, nRate);
    PLogF(k + ".factor0", NWNX_Creature_GetMovementRateFactor(o));
    PLogF(k + ".cap0", NWNX_Creature_GetMovementRateFactorCap(o));
    PLogI(k + ".rate0", GetMovementRate(o));
    PLogI(k + ".type0", NWNX_Creature_GetMovementType(o));
    Go(o, k, y, bRun);
}

void Later()
{
    // An effect applied after the factor was set.
    effect n = EffectVisualEffect(VFX_DUR_GLOW_WHITE);
    object o = Make("late", 1.5);
    NWNX_Creature_SetMovementRateFactor(o, 1.2);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectMovementSpeedIncrease(30), o);
    PLogF("late.factor0", NWNX_Creature_GetMovementRateFactor(o));
    Go(o, "late", 1.5, TRUE);
    // The factor set, then the effect removed.
    object p = Make("rem", 3.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectHaste(), p);
    PLogF("rem.factorh", NWNX_Creature_GetMovementRateFactor(p));
    NWNX_Creature_SetMovementRateFactor(p, 0.8);
    effect x = GetFirstEffect(p);
    while (GetIsEffectValid(x)) { RemoveEffect(p, x); x = GetNextEffect(p); }
    PLogF("rem.factor0", NWNX_Creature_GetMovementRateFactor(p));
    PLogI("rem.hasted", GetHasEffect(EFFECT_TYPE_HASTE, p));
    DelayCommand(0.1, PLogF("rem.factorlater", NWNX_Creature_GetMovementRateFactor(p)));
    Go(p, "rem", 3.0, TRUE);
    // The walk cap lifted again.
    object q = Make("wcapoff", 4.5);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectMovementSpeedIncrease(50), q);
    NWNX_Creature_SetWalkRateCap(q, 1.0);
    NWNX_Creature_SetWalkRateCap(q, -1.0);
    Go(q, "wcapoff", 4.5, FALSE);
    DelayCommand(4.0, PDone());
}

void main()
{
    effect n = EffectVisualEffect(VFX_DUR_GLOW_WHITE);
    Lane("base", 1.5, TRUE, 0.0, 0.0, 0.0, -1, n, FALSE);
    Lane("walk", 3.0, FALSE, 0.0, 0.0, 0.0, -1, n, FALSE);
    Lane("f2", 4.5, TRUE, 2.0, 0.0, 0.0, -1, n, FALSE);
    Lane("f12", 6.0, TRUE, 1.2, 0.0, 0.0, -1, n, FALSE);
    Lane("f05", 7.5, TRUE, 0.5, 0.0, 0.0, -1, n, FALSE);
    Lane("f2cap3", 9.0, TRUE, 2.0, 3.0, 0.0, -1, n, FALSE);
    Lane("haste", 10.5, TRUE, 0.0, 0.0, 0.0, -1, EffectHaste(), TRUE);
    Lane("hastecap2", 12.0, TRUE, 0.0, 2.0, 0.0, -1, EffectHaste(), TRUE);
    Lane("inc99cap3", 13.5, TRUE, 0.0, 3.0, 0.0, -1, EffectMovementSpeedIncrease(99), TRUE);
    Lane("slowf2", 15.0, TRUE, 2.0, 0.0, 0.0, -1, EffectMovementSpeedDecrease(50), TRUE);
    Lane("cap1", 16.5, TRUE, 0.0, 1.0, 0.0, -1, EffectHaste(), TRUE);
    Lane("wcap", 18.0, FALSE, 0.0, 0.0, 1.0, -1, EffectMovementSpeedIncrease(50), TRUE);
    Lane("wcaprun", 19.5, TRUE, 0.0, 0.0, 1.0, -1, EffectMovementSpeedIncrease(50), TRUE);
    Lane("wcapdef", 21.0, FALSE, 0.0, 0.0, 2000.0, -1, EffectMovementSpeedIncrease(50), TRUE);
    Lane("rfast", 22.5, TRUE, 0.0, 0.0, 0.0, NWNX_CREATURE_MOVEMENT_RATE_FAST, n, FALSE);
    Lane("rslowf2", 24.0, TRUE, 2.0, 0.0, 0.0, NWNX_CREATURE_MOVEMENT_RATE_SLOW, n, FALSE);
    Lane("rpc", 25.5, TRUE, 0.0, 0.0, 0.0, NWNX_CREATURE_MOVEMENT_RATE_PC, n, FALSE);
    Lane("rdef", 27.0, TRUE, 0.0, 0.0, 0.0, NWNX_CREATURE_MOVEMENT_RATE_DEFAULT, n, FALSE);
    Lane("rimm", 28.5, TRUE, 0.0, 0.0, 0.0, NWNX_CREATURE_MOVEMENT_RATE_IMMOBILE, n, FALSE);
    Lane("f0", 30.0, TRUE, 0.001, 0.0, 0.0, -1, n, FALSE);
    // A cap set first: effects then reach it.
    object a = Make("cap3fx", 31.5);
    NWNX_Creature_SetMovementRateFactorCap(a, 3.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectMovementSpeedIncrease(99), a);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectHaste(), a);
    PLogF("cap3fx.factor0", NWNX_Creature_GetMovementRateFactor(a));
    Go(a, "cap3fx", 31.5, TRUE);
    object b = Make("cap2fx", 33.0);
    NWNX_Creature_SetMovementRateFactorCap(b, 2.0);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectMovementSpeedIncrease(50), b);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectMovementSpeedIncrease(50), b);
    PLogF("cap2fx.factor0", NWNX_Creature_GetMovementRateFactor(b));
    Go(b, "cap2fx", 33.0, TRUE);
    PLogF("invalid.factor", NWNX_Creature_GetMovementRateFactor(OBJECT_INVALID));
    PLogF("invalid.cap", NWNX_Creature_GetMovementRateFactorCap(OBJECT_INVALID));
    PLogI("invalid.type", NWNX_Creature_GetMovementType(OBJECT_INVALID));
    DelayCommand(4.0, Later());
}
