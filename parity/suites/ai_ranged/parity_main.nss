// Stock AI with bows (docs/STOCK_AI.md), one fight per area (Prelude factions, 16 m apart):
// - arch (areas 0-5): nw_bandit002, a 1st-level ranger with a longbow, against a nw_bandit001 fighter (archf).
// - rng (areas 6-11): nw_elfranger005, a 5th-level elf ranger with a longbow, against two nw_bandit001 (rngf).
// Readings include where each creature first attacks from (distance to its target at its first swing).
#include "pai_inc"

object Ar(int i) { return GetObjectByTag("AI" + (i < 10 ? "0" : "") + IntToString(i)); }

void Setup()
{
    int i;
    for (i = 0; i < 12; i++)
    {
        object a = Ar(i);
        if (i < 6)
        {
            PaiStart(PaiSpawn("nw_bandit002", a, 12.0, 20.0, 0.0, "arch", STANDARD_FACTION_HOSTILE));
            PaiStart(PaiSpawn("nw_bandit001", a, 28.0, 20.0, 180.0, "archf", STANDARD_FACTION_DEFENDER));
        }
        else
        {
            PaiStart(PaiSpawn("nw_elfranger005", a, 12.0, 20.0, 0.0, "rng", STANDARD_FACTION_HOSTILE));
            PaiStart(PaiSpawn("nw_bandit001", a, 28.0, 18.8, 180.0, "rngf", STANDARD_FACTION_DEFENDER));
            PaiStart(PaiSpawn("nw_bandit001", a, 28.0, 21.2, 180.0, "rngf", STANDARD_FACTION_DEFENDER));
        }
    }
}

void Report()
{
    string s = "arch archf rng rngf ";
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string r = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 1000);
        PaiReportRole(r);
        PaiReportLocal(r, "PAI_SWINGDIST", 1.5, "swingdist");
    }
    PaiDumpRaw();
    PDone();
}

void main()
{
    PStart();
    SetLocalInt(GetModule(), "X2_L_NOTREASURE", TRUE);
    DelayCommand(1.0, Setup());
    DelayCommand(1.1, PaiPollLoop(80.0));
    DelayCommand(83.0, Report());
}
