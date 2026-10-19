// Perception ranges in and out of combat (docs/STEALTH.md, "Distance limits"). One area per observer: blueprint
// PerceptionRange row 8 (short), 9 (medium), 10 (long) or 11 (default: appearance.2da PERCEPTIONDIST, 9 for a
// human), idle or immortal and attacked by a creature beside it. Ten plain targets (not sneaking) at set distances around
// it are made at 12 s, after the fight has started, and read at 30 s.
#include "pstealth_inc"

void Observer(int a, int nRow, int bCombat)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    object o = PsMake("sn_r" + IntToString(nRow), oArea, 40.0, 40.0, STANDARD_FACTION_COMMONER);
    SetLocalObject(GetModule(), "O" + IntToString(a), o);
    if (!bCombat) return;
    // The observer is attacked (so it stays put), and can't die.
    object d = PsMake("sn_tgt", oArea, 41.2, 40.0, STANDARD_FACTION_HOSTILE, 180.0);
    SetImmortal(o, TRUE);
    AssignCommand(d, ActionAttack(o));
}

void Targets(int a)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    object o = GetLocalObject(GetModule(), "O" + IntToString(a));
    float fDist; int i;
    for (i = 0; i < 10; i++)
    {
        switch (i)
        {
            case 0: fDist = 9.5; break;  case 1: fDist = 10.5; break; case 2: fDist = 14.5; break; case 3: fDist = 15.5; break;
            case 4: fDist = 19.5; break; case 5: fDist = 20.5; break; case 6: fDist = 27.5; break; case 7: fDist = 28.5; break;
            case 8: fDist = 34.5; break; default: fDist = 35.5; break;
        }
        float fAng = 18.0 + 36.0 * IntToFloat(i);
        object t = PsMake("sn_tgt", oArea, 40.0 + fDist * cos(fAng), 40.0 + fDist * sin(fAng), STANDARD_FACTION_MERCHANT);
        SetLocalObject(o, "T" + IntToString(i), t);
    }
}

void Read(int a, string k)
{
    object o = GetLocalObject(GetModule(), "O" + IntToString(a));
    PLogI(k + ".incombat", GetIsInCombat(o));
    int i;
    for (i = 0; i < 10; i++)
    {
        object t = GetLocalObject(o, "T" + IntToString(i));
        string d = FloatToString(GetDistanceBetween(o, t), 0, 1);
        PLogI(k + "." + d + ".seen", GetObjectSeen(t, o));
        PLogI(k + "." + d + ".heard", GetObjectHeard(t, o));
    }
}

void main()
{
    PStart();
    int a;
    for (a = 0; a < 8; a++)
    {
        int nRow = 8 + a % 4, bCombat = a >= 4;
        Observer(a, nRow, bCombat);
        DelayCommand(12.0, Targets(a));
        DelayCommand(30.0, Read(a, "row" + IntToString(nRow) + (bCombat ? ".combat" : ".calm")));
    }
    DelayCommand(31.0, PDone());
}
