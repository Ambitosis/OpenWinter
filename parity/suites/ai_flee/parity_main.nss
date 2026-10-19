// Stock AI fleeing (docs/STOCK_AI.md): commoners (nw_commale, the commoner class, whose stock tactics are
// TalentFlee: ActionMoveAwayFromObject for 4 s) against a hostile nw_bandit001 fighter 8 m away, one pair per area
// (12 areas, Prelude factions). Readings add the distance between them at 2-second steps after first perception.
#include "pai_inc"

object Ar(int i) { return GetObjectByTag("AI" + (i < 10 ? "0" : "") + IntToString(i)); }

void Setup()
{
    int i;
    for (i = 0; i < 12; i++)
    {
        object a = Ar(i);
        object c = PaiSpawn("nw_commale", a, 16.0, 20.0, 0.0, "com", STANDARD_FACTION_COMMONER);
        object f = PaiSpawn("nw_bandit001", a, 24.0, 20.0, 180.0, "comf", STANDARD_FACTION_HOSTILE);
        SetLocalObject(c, "FOE", f);
        PaiStart(c);
        PaiStart(f);
    }
}

void Dist(int nStep)
{
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    int i, k = 0;
    float fSum = 0.0, fSq = 0.0;
    for (i = 0; i < n; i++)
    {
        object o = GetLocalObject(m, "PAI_O" + IntToString(i));
        object f = GetLocalObject(o, "FOE");
        if (PaiRole(o) != "com" || !GetIsObjectValid(f) || GetIsDead(o)) continue;
        float d = GetDistanceBetween(o, f);
        fSum += d;
        fSq += d * d;
        k++;
    }
    SetLocalFloat(m, "D_SUM" + IntToString(nStep), fSum);
    SetLocalInt(m, "D_N" + IntToString(nStep), k);
}

void Report()
{
    PaiReportRole("com", 2);
    PaiReportRole("comf", 2);
    object m = GetModule();
    int s;
    // Until 10 s: later most commoners are dead, and the few left say more about the fight than the flight.
    for (s = 0; s < 4; s++)
        PaiMean("com.dist." + IntToString(4 + 2 * s), GetLocalFloat(m, "D_SUM" + IntToString(s)), GetLocalInt(m, "D_N" + IntToString(s)), 3.0, 0.5);
    PaiDumpRaw();
    PDone();
}

void main()
{
    PStart();
    SetLocalInt(GetModule(), "X2_L_NOTREASURE", TRUE);
    DelayCommand(1.0, Setup());
    DelayCommand(1.1, PaiPollLoop(60.0));
    int s;
    for (s = 0; s < 12; s++) DelayCommand(5.0 + 2.0 * IntToFloat(s), Dist(s));
    DelayCommand(63.0, Report());
}
