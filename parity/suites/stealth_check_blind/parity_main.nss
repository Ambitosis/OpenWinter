// Blind and darkened observers, and darkness on the target (docs/STEALTH.md, "Invisibility, darkness, blindness").
// In each area an idle observer and plain targets (not sneaking) around it at 2.5-6 m, read at 14 s:
//   blind     the observer has EffectBlindness
//   obsdark   the observer has EffectDarkness
//   obsdarkuv the observer has EffectDarkness and EffectUltravision
//   tgtdark   the targets have EffectDarkness
//   blindts   the observer has EffectBlindness and EffectTrueSeeing
#include "pstealth_inc"

void Ring(int a, string k, effect eObs, effect eObs2, effect eTgt)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    object o = PsMake("sn_obs", oArea, 30.0, 30.0, STANDARD_FACTION_COMMONER);
    if (GetEffectType(eObs) != EFFECT_TYPE_INVALIDEFFECT) PsPerm(o, eObs);
    if (GetEffectType(eObs2) != EFFECT_TYPE_INVALIDEFFECT) PsPerm(o, eObs2);
    float fDist; int i;
    for (i = 0; i < 6; i++)
    {
        fDist = i == 0 ? 2.5 : i == 1 ? 3.0 : i == 2 ? 3.4 : i == 3 ? 3.6 : i == 4 ? 4.0 : 6.0;
        float fAng = 60.0 * IntToFloat(i);
        object t = PsMake("sn_tgt", oArea, 30.0 + fDist * cos(fAng), 30.0 + fDist * sin(fAng), STANDARD_FACTION_HOSTILE);
        if (GetEffectType(eTgt) != EFFECT_TYPE_INVALIDEFFECT) PsPerm(t, eTgt);
        SetLocalObject(o, "T" + IntToString(i), t);
    }
    SetLocalObject(GetModule(), k, o);
}

void Read(string k)
{
    object o = GetLocalObject(GetModule(), k);
    int i;
    for (i = 0; i < 6; i++)
    {
        object t = GetLocalObject(o, "T" + IntToString(i));
        string d = k + "." + FloatToString(GetDistanceBetween(o, t), 0, 1);
        PLogI(d + ".seen", GetObjectSeen(t, o));
        PLogI(d + ".heard", GetObjectHeard(t, o));
    }
}

void main()
{
    PStart();
    effect eNone;
    Ring(0, "blind", EffectBlindness(), eNone, eNone);
    Ring(1, "obsdark", EffectDarkness(), eNone, eNone);
    Ring(2, "obsdarkuv", EffectDarkness(), EffectUltravision(), eNone);
    Ring(3, "tgtdark", eNone, eNone, EffectDarkness());
    Ring(4, "blindts", EffectBlindness(), EffectTrueSeeing(), eNone);
    DelayCommand(14.0, Read("blind"));
    DelayCommand(14.0, Read("obsdark"));
    DelayCommand(14.0, Read("obsdarkuv"));
    DelayCommand(14.0, Read("tgtdark"));
    DelayCommand(14.0, Read("blindts"));
    DelayCommand(15.0, PDone());
}
