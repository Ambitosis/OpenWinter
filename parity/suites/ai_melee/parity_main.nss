// Stock creature AI in melee (docs/STOCK_AI.md): nw_bandit001 fighters with their nw_c2_default* scripts, a hostile
// side against a defender side 8 m away (Prelude factions), one pair per area: eight 1-against-1 fights (m1a / m1b)
// and eight 2-against-2 (m2a / m2b). Everything the creatures do comes from the stock scripts; the suite only logs.
#include "pai_inc"

object Ar(int i) { return GetObjectByTag("AI" + (i < 10 ? "0" : "") + IntToString(i)); }

void Setup()
{
    int i;
    for (i = 0; i < 16; i++)
    {
        object a = Ar(i);
        if (i < 8)
        {
            PaiStart(PaiSpawn("nw_bandit001", a, 11.0, 15.0, 0.0, "m1a", STANDARD_FACTION_HOSTILE));
            PaiStart(PaiSpawn("nw_bandit001", a, 19.0, 15.0, 180.0, "m1b", STANDARD_FACTION_DEFENDER));
        }
        else
        {
            object a1 = PaiSpawn("nw_bandit001", a, 11.0, 13.8, 0.0, "m2a", STANDARD_FACTION_HOSTILE);
            object a2 = PaiSpawn("nw_bandit001", a, 11.0, 16.2, 0.0, "m2a", STANDARD_FACTION_HOSTILE);
            object b1 = PaiSpawn("nw_bandit001", a, 19.0, 13.8, 180.0, "m2b", STANDARD_FACTION_DEFENDER);
            object b2 = PaiSpawn("nw_bandit001", a, 19.0, 16.2, 180.0, "m2b", STANDARD_FACTION_DEFENDER);
            SetLocalObject(a1, "PAI_ALLY", a2);
            SetLocalObject(b1, "PAI_ALLY", b2);
            PaiStart(a1); PaiStart(a2); PaiStart(b1); PaiStart(b2);
        }
    }
}

// 2-against-2: do the two allies go for the same enemy first, and is it the nearer one?
void Focus(string sRole)
{
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    int i, k = 0, same = 0;
    for (i = 0; i < n; i++)
    {
        object o = GetLocalObject(m, "PAI_O" + IntToString(i));
        object b = GetLocalObject(o, "PAI_ALLY");
        if (PaiRole(o) != sRole || !GetIsObjectValid(b)) continue;
        object t1 = GetLocalObject(o, "PAI_FIRSTTGT"), t2 = GetLocalObject(b, "PAI_FIRSTTGT");
        if (!GetIsObjectValid(t1) || !GetIsObjectValid(t2)) continue;
        k++;
        if (t1 == t2) same++;
    }
    PaiRate(sRole + ".samefirsttarget", same, k);
    PaiMean(sRole + ".switches", IntToFloat(PaiGet(sRole + ".switches")), PaiGet(sRole + ".count"), 1.0, 0.3);
}

void Report()
{
    PaiReportRole("m1a");
    PaiReportRole("m1b");
    PaiReportRole("m2a");
    PaiReportRole("m2b");
    Focus("m2a");
    Focus("m2b");
    PaiDumpRaw();
    PDone();
}

void main()
{
    PStart();
    SetLocalInt(GetModule(), "X2_L_NOTREASURE", TRUE);
    DelayCommand(1.0, Setup());
    DelayCommand(1.1, PaiPollLoop(75.0));
    DelayCommand(78.0, Report());
}
