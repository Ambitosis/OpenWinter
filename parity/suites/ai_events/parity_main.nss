// Event timing the stock AI runs on, by AI level (docs/STOCK_AI.md):
// - hb: OnHeartbeat of creatures without scripts at AI levels default (-1), 0..3: first beat and later beats.
// - per: OnPerception of an observer at each level: first sight of a hostile 6 m away, its vanishing when the
//   target jumps away at 12 s, and seeing it again when it jumps back at 24 s.
// - atk: a stock nw_goblina (nw_c2_default*) attacked by a scripted defender at 1 s, its level default or normal:
//   when it is hit, its OnEndCombatRound beats, its first swing back, its AI level.
// - mut: two stock creatures (hostile goblin, defender bandit) 8 m apart, both immortal: who perceives and swings when.
// Every pair stands in its own small area.
#include "pai_inc"

object Ar(string sTag) { return GetObjectByTag(sTag); }

void Strip(object o)
{
    int i;
    for (i = 0; i <= 12; i++) SetLocalString(o, "PAI_S" + IntToString(i), "");
    AssignCommand(o, ClearAllActions(TRUE));
}

string LevelName(int L) { return L < 0 ? "d" : IntToString(L); }

void Heartbeats()
{
    object a = Ar("AIH00");
    int L, i;
    for (L = -1; L <= 3; L++)
        for (i = 0; i < 4; i++)
        {
            object o = PaiSpawn("nw_bandit001", a, 4.0 + IntToFloat(i) * 3.0, 4.0 + IntToFloat(L + 1) * 6.0, 0.0,
                "hb" + LevelName(L), STANDARD_FACTION_COMMONER);
            Strip(o);
            SetAILevel(o, L);
            PaiStart(o);
        }
}

void JumpTarget(object t, float x, float y)
{
    AssignCommand(t, ClearAllActions(TRUE));
    AssignCommand(t, JumpToLocation(Location(GetArea(t), Vector(x, y, 0.0), 0.0)));
}

void Perception()
{
    int L, i;
    for (L = -1; L <= 2; L++)
        for (i = 0; i < 4; i++)
        {
            object a = Ar("AIP" + (L * 4 + 4 + i < 10 ? "0" : "") + IntToString(L * 4 + 4 + i));
            object o = PaiSpawn("nw_bandit001", a, 4.0, 10.0, 0.0, "per" + LevelName(L), STANDARD_FACTION_DEFENDER);
            Strip(o);
            SetAILevel(o, L);
            object t = PaiSpawn("nw_goblina", a, 10.0, 10.0, 180.0, "pert", STANDARD_FACTION_HOSTILE);
            Strip(t);
            SetPlotFlag(t, TRUE);
            PaiStart(o);
            PaiStart(t);
            SetLocalObject(o, "TARGET", t);
            DelayCommand(12.0, JumpTarget(t, 36.0, 36.0));
            DelayCommand(24.0, JumpTarget(t, 10.0, 10.0));
        }
}

void Attacked()
{
    int k, i;
    for (k = 0; k < 2; k++)
        for (i = 0; i < 4; i++)
        {
            object a = Ar("AIC0" + IntToString(k * 4 + i));
            string sRole = k == 0 ? "atkd" : "atk2";
            object o = PaiSpawn("nw_goblina", a, 10.0, 10.0, 180.0, sRole);
            if (k == 0) SetAILevel(o, AI_LEVEL_DEFAULT);
            SetImmortal(o, TRUE);
            object b = PaiSpawn("nw_bandit001", a, 8.5, 10.0, 0.0, sRole + "x", STANDARD_FACTION_DEFENDER);
            Strip(b);
            SetImmortal(b, TRUE);
            PaiStart(o);
            PaiStart(b);
            SetLocalObject(o, "TARGET", b);
            DelayCommand(1.0, AssignCommand(b, ActionAttack(o)));
        }
}

void Mutual()
{
    int i;
    for (i = 0; i < 6; i++)
    {
        object a = Ar("AIR0" + IntToString(i));
        object g = PaiSpawn("nw_goblina", a, 11.0, 15.0, 180.0, "mutg");
        object b = PaiSpawn("nw_bandit001", a, 19.0, 15.0, 0.0, "mutb", STANDARD_FACTION_DEFENDER);
        SetImmortal(g, TRUE);
        SetImmortal(b, TRUE);
        PaiStart(g);
        PaiStart(b);
    }
}

void LevelCheck(string sRole, string sKey)
{
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    int i;
    for (i = 0; i < n; i++)
    {
        object o = GetLocalObject(m, "PAI_O" + IntToString(i));
        if (PaiRole(o) == sRole) PaiTally(sRole + "." + sKey, IntToString(GetAILevel(o)) + "/" + IntToString(GetIsInCombat(o)));
    }
}

// Mean over a role's creatures of the n-th perception event of a kind, minus fFrom.
void PerKind(string sRole, string sKind, int nth, float fFrom, string sName)
{
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    int i, k = 0, miss = 0;
    float fSum = 0.0;
    for (i = 0; i < n; i++)
    {
        object o = GetLocalObject(m, "PAI_O" + IntToString(i));
        if (PaiRole(o) != sRole) continue;
        int c = GetLocalInt(o, "PAI_PK_" + sKind);
        int j, found = FALSE;
        for (j = 1; j <= c && j <= 6; j++)
        {
            float t = GetLocalFloat(o, "PAI_PK_" + sKind + "_" + IntToString(j));
            if (t >= fFrom) { found = TRUE; fSum += t - fFrom; k++; break; }
        }
        if (!found) miss++;
    }
    PaiMean(sRole + "." + sName, fSum, k, 0.6, 0.25);
    PaiRate(sRole + "." + sName + ".have", k, k + miss);
}

void Report()
{
    int L;
    for (L = -1; L <= 3; L++)
    {
        string r = "hb" + LevelName(L);
        PaiReportNth(r, "hb", 1, 0.5);
        PaiReportNth(r, "hb", 2, 0.6);
        PaiReportNth(r, "hb", 3, 0.7);
        PaiReportNth(r, "hb", 5, 0.9);
        PaiReportNth(r, "hb", 8, 1.2);
    }
    for (L = -1; L <= 2; L++)
    {
        string r = "per" + LevelName(L);
        PerKind(r, "s", 1, 0.0, "seen1");
        PerKind(r, "h", 1, 0.0, "heard1");
        PerKind(r, "v", 1, 12.0, "vanish");
        PerKind(r, "i", 1, 12.0, "inaudible");
        PerKind(r, "s", 1, 24.0, "seen2");
        PaiReportNth(r, "per", 1, 0.6);
        PaiRate(r + ".perevents", PaiGet(r + ".n.per"), PaiGet(r + ".count") * 8, 0.1);
    }
    string s = "atkd atk2 ";
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string r = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 1000);
        PaiReportNth(r, "atk", 1, 0.4);
        PaiReportNth(r, "dmg", 1, 4.0);
        PaiReportNth(r, "eor", 1, 0.6);
        PaiReportNth(r, "eor", 2, 0.6);
        PaiReportNth(r, "eor", 4, 0.8);
        PaiReportNth(r, "per", 1, 0.6);
        PaiReportNth(r, "hb", 1, 0.6);
        PaiReportLocal(r, "PAI_SWING1", 0.6, "swing1");
        PaiReportLocal(r, "PAI_FIRSTACT", 0.5, "firstact");
        PaiReportNth(r + "x", "atk", 1, 0.6);
        PaiReportNth(r + "x", "atk", 4, 1.0);
        PaiRate(r + ".eorcount", PaiGet(r + ".n.eor"), PaiGet(r + ".count") * 12, 0.08);
        PaiReportSeq(r, "PAI_EVS", 1, "evs");
    }
    s = "mutg mutb ";
    while (s != "")
    {
        int p = FindSubString(s, " ");
        string r = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 1000);
        PaiReportNth(r, "per", 1, 0.6);
        PaiReportNth(r, "atk", 1, 0.8);
        PaiReportNth(r, "eor", 1, 0.8);
        PaiReportNth(r, "eor", 3, 1.0);
        PaiReportNth(r, "hb", 1, 0.6);
        PaiReportLocal(r, "PAI_SWING1", 0.8, "swing1");
        PaiReportLocal(r, "PAI_FIRSTACT", 0.6, "firstact");
        PaiRate(r + ".eorcount", PaiGet(r + ".n.eor"), PaiGet(r + ".count") * 12, 0.08);
        PaiReportSeq(r, "PAI_EVS", 2, "evs");
        PaiReportSeq(r, "PAI_ACTS", 2, "acts");
    }
    PaiReportTally("atkd.level5", 4);
    PaiReportTally("atkd.level30", 4);
    PaiReportTally("hbd.level30", 4);
    PaiReportTally("mutg.level30", 6);
    PaiDumpRaw();
    PDone();
}

void main()
{
    PStart();
    SetLocalInt(GetModule(), "X2_L_NOTREASURE", TRUE);
    DelayCommand(1.0, Heartbeats());
    DelayCommand(1.0, Perception());
    DelayCommand(1.0, Attacked());
    DelayCommand(1.0, Mutual());
    DelayCommand(1.2, PaiPollLoop(60.0));
    DelayCommand(6.0, LevelCheck("atkd", "level5"));
    DelayCommand(31.0, LevelCheck("atkd", "level30"));
    DelayCommand(31.0, LevelCheck("hbd", "level30"));
    DelayCommand(31.0, LevelCheck("mutg", "level30"));
    DelayCommand(55.0, Report());
}
