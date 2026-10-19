// Stock AI measurement (docs/STOCK_AI.md): creatures keep their own event scripts (nw_c2_default*, the henchman
// scripts...), which run unchanged; a one-line wrapper per event (pai_e0 .. pai_e12) logs the event first and then
// executes the creature's own script, which reads the same event state.
//
// Each tracked creature has a role (e.g. "a", "b") shared by its replicas; counters and sums live on the module as
// "<role>.<name>", per-creature readings on the creature. A poller samples GetCurrentAction and GetAttackTarget every 0.25 s
// (the attempted attack and spell targets can only be read by the creature itself, so the wrappers log them).
#include "parity_inc"
#include "parity_time"

const float PAI_POLL = 0.25;

string PaiEvName(int n)
{
    switch (n)
    {
        case 0: return "hb";
        case 1: return "per";
        case 2: return "spl";
        case 3: return "atk";
        case 4: return "dmg";
        case 5: return "dis";
        case 6: return "eor";
        case 7: return "cnv";
        case 8: return "spn";
        case 9: return "rst";
        case 10: return "dth";
        case 11: return "ud";
        case 12: return "blk";
    }
    return "?";
}

void PaiAdd(string sKey, int n = 1) { SetLocalInt(GetModule(), sKey, GetLocalInt(GetModule(), sKey) + n); }
int PaiGet(string sKey) { return GetLocalInt(GetModule(), sKey); }
void PaiAddF(string sKey, float f) { SetLocalFloat(GetModule(), sKey, GetLocalFloat(GetModule(), sKey) + f); }
float PaiGetF(string sKey) { return GetLocalFloat(GetModule(), sKey); }

// Seconds since the creature's fight began (PaiStart), on the game clock (independent of PStart).
float PaiT(object o) { return PNow() - GetLocalFloat(o, "PAI_T0"); }

string PaiRole(object o) { return GetLocalString(o, "PAI_ROLE"); }

// Appends a token to a creature's sequence (at most nMax tokens).
void PaiSeq(object o, string sVar, string sTok, int nMax = 14)
{
    int n = GetLocalInt(o, sVar + "_N");
    if (n >= nMax) return;
    SetLocalInt(o, sVar + "_N", n + 1);
    SetLocalString(o, sVar, GetLocalString(o, sVar) + (n == 0 ? "" : " ") + sTok);
}

// Adds a token to a module-wide list of distinct values for a key (used to report rates of patterns).
void PaiTally(string sKey, string sValue)
{
    object m = GetModule();
    string k = sKey + "#" + sValue;
    if (GetLocalInt(m, k) == 0) SetLocalString(m, sKey + "#list", GetLocalString(m, sKey + "#list") + sValue + "|");
    SetLocalInt(m, k, GetLocalInt(m, k) + 1);
}

int PaiCount(object o, string sEv) { return GetLocalInt(o, "PAI_C_" + sEv); }
// Time of the n-th (1-based) occurrence of an event, or -1.
float PaiNth(object o, string sEv, int n)
{
    if (n > GetLocalInt(o, "PAI_C_" + sEv) || n > 12) return -1.0;
    return GetLocalFloat(o, "PAI_T_" + sEv + "_" + IntToString(n));
}

string PaiTag(object o) { return GetIsObjectValid(o) ? PaiRole(o) : "-"; }

string PaiActName(int a) { return a == ACTION_INVALID ? "none" : IntToString(a); }

// The event wrapper body: log, then run the creature's own script for the event.
void PaiEvent(int n)
{
    object o = OBJECT_SELF;
    string e = PaiEvName(n);
    if (GetLocalInt(o, "PAI_ON"))
    {
        string r = PaiRole(o);
        float t = PaiT(o);
        int c = GetLocalInt(o, "PAI_C_" + e) + 1;
        SetLocalInt(o, "PAI_C_" + e, c);
        if (c <= 12) SetLocalFloat(o, "PAI_T_" + e + "_" + IntToString(c), t);
        PaiAdd(r + ".n." + e);
        string sDetail = "";
        if (n == 1)
        {
            object p = GetLastPerceived();
            sDetail = (GetLastPerceptionSeen() ? "s" : "") + (GetLastPerceptionHeard() ? "h" : "") + (GetLastPerceptionVanished() ? "v" : "")
                + (GetLastPerceptionInaudible() ? "i" : "") + ":" + PaiTag(p);
            string sK = GetLastPerceptionSeen() ? "s" : (GetLastPerceptionVanished() ? "v" : (GetLastPerceptionHeard() ? "h" : (GetLastPerceptionInaudible() ? "i" : "x")));
            int kc = GetLocalInt(o, "PAI_PK_" + sK) + 1;
            SetLocalInt(o, "PAI_PK_" + sK, kc);
            if (kc <= 6) SetLocalFloat(o, "PAI_PK_" + sK + "_" + IntToString(kc), t);
        }
        else if (n == 2)
        {
            object oCaster = GetLastSpellCaster();
            int nSpell = GetLastSpell();
            sDetail = IntToString(nSpell) + ":" + PaiTag(oCaster) + (GetLastSpellHarmful() ? "!" : "");
            if (GetIsObjectValid(oCaster) && GetLocalInt(oCaster, "PAI_ON"))
            {
                PaiAdd(PaiRole(oCaster) + ".cast." + IntToString(nSpell));
                PaiTally(PaiRole(oCaster) + ".castid", IntToString(nSpell) + (oCaster == o ? "self" : ""));
                if (GetLocalInt(oCaster, "PAI_NCAST") == 0) PaiTally(PaiRole(oCaster) + ".firstcast", IntToString(nSpell) + (oCaster == o ? "self" : ""));
                SetLocalInt(oCaster, "PAI_NCAST", GetLocalInt(oCaster, "PAI_NCAST") + 1);
                PaiSeq(oCaster, "PAI_CASTS", IntToString(nSpell) + (oCaster == o ? "@self" : "@" + r), 8);
            }
        }
        else if (n == 3)
        {
            object oAtk = GetLastAttacker();
            int nType = GetLastAttackType(oAtk);
            sDetail = PaiTag(oAtk) + ":" + IntToString(nType) + ":" + IntToString(GetLastAttackMode(oAtk));
            if (GetIsObjectValid(oAtk) && GetLocalInt(oAtk, "PAI_ON"))
            {
                PaiAdd(PaiRole(oAtk) + ".atype." + IntToString(nType));
                PaiTally(PaiRole(oAtk) + ".atypeid", IntToString(nType));
                PaiAdd(PaiRole(oAtk) + ".swings");
                PaiAdd(PaiRole(oAtk) + ".amode." + IntToString(GetLastAttackMode(oAtk)));
                if (GetLocalFloat(oAtk, "PAI_SWING1") == 0.0)
                {
                    SetLocalFloat(oAtk, "PAI_SWING1", PaiT(oAtk) + 0.0001);
                    SetLocalFloat(oAtk, "PAI_SWINGDIST", GetDistanceBetween(o, oAtk) + 0.0001);
                }
            }
        }
        else if (n == 4)
            sDetail = PaiTag(GetLastDamager());
        else if (n == 7)
        {
            sDetail = IntToString(GetListenPatternNumber()) + ":" + PaiTag(GetLastSpeaker());
            PaiTally(r + ".cnvpat", IntToString(GetListenPatternNumber()) + (GetFactionEqual(GetLastSpeaker(), o) ? "ally" : "other"));
        }
        else if (n == 10)
            sDetail = PaiTag(GetLastKiller());
        if (n != 0) PaiSeq(o, "PAI_EVS", e + (sDetail == "" ? "" : "(" + sDetail + ")"));
        string sRaw = sDetail;
        if (n == 6 || n == 0)
            sRaw = "at=" + PaiTag(GetAttackTarget(o)) + " aat=" + PaiTag(GetAttemptedAttackTarget()) + " mx=" + IntToString(GetLocalInt(o, "X2_L_MUTEXCOMBATROUND"))
                + " ne=" + PaiTag(GetNearestCreature(CREATURE_TYPE_REPUTATION, REPUTATION_TYPE_ENEMY, o, 1, CREATURE_TYPE_PERCEPTION, PERCEPTION_SEEN))
                + " ca=" + PaiActName(GetCurrentAction(o)) + " cb=" + IntToString(GetIsInCombat(o));
        PaiSeq(o, "PAI_EVT", e + (sRaw == "" ? "" : "(" + sRaw + ")") + "@" + FloatToString(t, 0, 2), 40);
    }
    string sScript = GetLocalString(o, "PAI_S" + IntToString(n));
    if (sScript != "") ExecuteScript(sScript, o);
}

// Puts the wrappers on a creature, remembering its own scripts. The spawn script has already run.
void PaiWrap(object o, string sRole)
{
    SetLocalString(o, "PAI_ROLE", sRole);
    int i;
    for (i = 0; i <= 12; i++)
    {
        if (i == 8) continue;
        SetLocalString(o, "PAI_S" + IntToString(i), GetEventScript(o, 5000 + i));
        SetEventScript(o, 5000 + i, "pai_e" + IntToString(i));
    }
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    SetLocalObject(m, "PAI_O" + IntToString(n), o);
    SetLocalInt(m, "PAI_N", n + 1);
    PaiAdd(sRole + ".count");
}

// Starts a creature's clock and logging.
void PaiStart(object o)
{
    SetLocalFloat(o, "PAI_T0", PNow());
    SetLocalInt(o, "PAI_ON", TRUE);
}

object PaiArea(string sTag) { return GetObjectByTag(sTag); }

object PaiSpawn(string sResRef, object oArea, float x, float y, float fFacing, string sRole, int nFaction = -1)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sResRef, Location(oArea, Vector(x, y, 0.0), fFacing));
    if (nFaction >= 0) ChangeToStandardFaction(o, nFaction);
    // Corpses stay (not raiseable, not selectable) so the report can still read the dead.
    AssignCommand(o, SetIsDestroyable(FALSE, FALSE, FALSE));
    SetAILevel(o, AI_LEVEL_NORMAL);
    PaiWrap(o, sRole);
    return o;
}


// Samples every tracked creature's action and targets.
void PaiPoll()
{
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    int i;
    for (i = 0; i < n; i++)
    {
        object o = GetLocalObject(m, "PAI_O" + IntToString(i));
        if (!GetIsObjectValid(o) || !GetLocalInt(o, "PAI_ON") || GetLocalInt(o, "PAI_DEAD")) continue;
        string r = PaiRole(o);
        if (GetIsDead(o))
        {
            SetLocalInt(o, "PAI_DEAD", TRUE);
            SetLocalFloat(o, "PAI_DIED", PaiT(o));
            continue;
        }
        int a = GetCurrentAction(o);
        object oA = GetAttackTarget(o);
        string sState = PaiActName(a) + (GetIsObjectValid(oA) ? "/a" + PaiTag(oA) : "");
        if (sState != GetLocalString(o, "PAI_LASTSTATE"))
        {
            SetLocalString(o, "PAI_LASTSTATE", sState);
            PaiSeq(o, "PAI_ACTS", sState, 10);
            PaiSeq(o, "PAI_ACTT", sState + "@" + FloatToString(PaiT(o), 0, 2), 40);
        }
        PaiAdd(r + ".samp." + PaiActName(a));
        PaiAdd(r + ".samples");
        if (GetIsInCombat(o)) PaiAdd(r + ".samp.combat");
        if (GetLocalFloat(o, "PAI_FIRSTACT") == 0.0 && a != ACTION_INVALID) SetLocalFloat(o, "PAI_FIRSTACT", PaiT(o) + 0.0001);
        if (GetIsObjectValid(oA) && !GetIsObjectValid(GetLocalObject(o, "PAI_FIRSTTGT"))) SetLocalObject(o, "PAI_FIRSTTGT", oA);
        if (GetIsObjectValid(oA) && oA != GetLocalObject(o, "PAI_LASTTGT"))
        {
            if (GetIsObjectValid(GetLocalObject(o, "PAI_LASTTGT"))) PaiAdd(r + ".switches");
            SetLocalObject(o, "PAI_LASTTGT", oA);
        }
    }
}

// Polls every PAI_POLL seconds for fSeconds.
void PaiPollLoop(float fSeconds)
{
    PaiPoll();
    if (fSeconds > 0.0) DelayCommand(PAI_POLL, PaiPollLoop(fSeconds - PAI_POLL));
}

// ---------------------------------------------------------------- reports

void PaiRate(string sKey, int nNum, int nDen, float fFloor = 0.05)
{
    if (nDen <= 0) { PLog(sKey, "none"); return; }
    float p = IntToFloat(nNum) / IntToFloat(nDen);
    float q = p < 0.05 ? 0.05 : (p > 0.95 ? 0.95 : p);
    float tol = 3.5 * sqrt(2.0 * q * (1.0 - q) / IntToFloat(nDen));
    if (tol < fFloor) tol = fFloor;
    PLogF(sKey, p, tol);
}

void PaiMean(string sKey, float fSum, int n, float fSd, float fFloor = 0.1)
{
    if (n <= 0) { PLog(sKey, "none"); return; }
    float tol = 3.5 * fSd * sqrt(2.0 / IntToFloat(n));
    if (tol < fFloor) tol = fFloor;
    PLogF(sKey, fSum / IntToFloat(n), tol);
}

// Rates of the distinct values tallied under a key, out of nDen.
void PaiReportTally(string sKey, int nDen)
{
    object m = GetModule();
    string s = GetLocalString(m, sKey + "#list");
    while (s != "")
    {
        int p = FindSubString(s, "|");
        string v = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 100000);
        PaiRate(sKey + "=" + v, GetLocalInt(m, sKey + "#" + v), nDen);
    }
}

// Mean over a role's creatures of the n-th time of an event (creatures without it are counted in "<key>.missing").
void PaiReportNth(string sRole, string sEv, int nth, float fSd)
{
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    int i, k = 0, miss = 0;
    float fSum = 0.0;
    for (i = 0; i < n; i++)
    {
        object o = GetLocalObject(m, "PAI_O" + IntToString(i));
        if (GetLocalString(o, "PAI_ROLE") != sRole) continue;
        float t = PaiNth(o, sEv, nth);
        if (t < 0.0) { miss++; continue; }
        fSum += t;
        k++;
    }
    string sKey = sRole + "." + sEv + IntToString(nth);
    PaiMean(sKey + ".t", fSum, k, fSd);
    PaiRate(sKey + ".have", k, k + miss);
}

// Mean over a role's creatures of a per-creature float local (0 = none).
void PaiReportLocal(string sRole, string sVar, float fSd, string sName)
{
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    int i, k = 0, miss = 0;
    float fSum = 0.0;
    for (i = 0; i < n; i++)
    {
        object o = GetLocalObject(m, "PAI_O" + IntToString(i));
        if (GetLocalString(o, "PAI_ROLE") != sRole) continue;
        float t = GetLocalFloat(o, sVar);
        if (t == 0.0) { miss++; continue; }
        fSum += t;
        k++;
    }
    PaiMean(sRole + "." + sName + ".t", fSum, k, fSd);
    PaiRate(sRole + "." + sName + ".have", k, k + miss);
}

// Tallies the first nTok tokens of each of a role's creatures' sequences and reports the rates of each prefix.
void PaiReportSeq(string sRole, string sVar, int nTok, string sName)
{
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    int i, k = 0;
    for (i = 0; i < n; i++)
    {
        object o = GetLocalObject(m, "PAI_O" + IntToString(i));
        if (GetLocalString(o, "PAI_ROLE") != sRole) continue;
        string s = GetLocalString(o, sVar);
        string sPre = "";
        int j;
        for (j = 0; j < nTok && s != ""; j++)
        {
            int p = FindSubString(s, " ");
            string sTok = p < 0 ? s : GetStringLeft(s, p);
            s = p < 0 ? "" : GetSubString(s, p + 1, 100000);
            sPre += (j == 0 ? "" : " ") + sTok;
        }
        PaiTally(sRole + "." + sName, sPre == "" ? "-" : sPre);
        k++;
    }
    PaiReportTally(sRole + "." + sName, k);
}

// Writes every tracked creature's raw sequences as notes (keys under "raw." are not compared: the
// harness compares them, so they carry a wide-open tolerance by being logged with PLog only when asked).
void PaiDumpRaw()
{
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    int i;
    for (i = 0; i < n; i++)
    {
        object o = GetLocalObject(m, "PAI_O" + IntToString(i));
        string k = "RAW|" + IntToString(i) + "." + GetLocalString(o, "PAI_ROLE");
        WriteTimestampedLogEntry(k + ".evs|" + GetLocalString(o, "PAI_EVS"));
        WriteTimestampedLogEntry(k + ".acts|" + GetLocalString(o, "PAI_ACTS"));
        WriteTimestampedLogEntry(k + ".casts|" + GetLocalString(o, "PAI_CASTS"));
        WriteTimestampedLogEntry(k + ".evt|" + GetLocalString(o, "PAI_EVT"));
        WriteTimestampedLogEntry(k + ".actt|" + GetLocalString(o, "PAI_ACTT"));
    }
}

// Mean of tallied counts per creature of a role: "<role>.<name>.<value>" = count / creatures, tolerance from a
// Poisson-like spread.
void PaiReportCounts(string sRole, string sName)
{
    object m = GetModule();
    int n = PaiGet(sRole + ".count");
    string s = GetLocalString(m, sRole + "." + sName + "#list");
    while (s != "")
    {
        int p = FindSubString(s, "|");
        string v = GetStringLeft(s, p);
        s = GetSubString(s, p + 1, 100000);
        int c = GetLocalInt(m, sRole + "." + sName + "#" + v);
        float mean = IntToFloat(c) / IntToFloat(n);
        PaiMean(sRole + "." + sName + "." + v, IntToFloat(c), n, sqrt(mean) + 0.5, 0.3);
    }
}

// Deaths of a role's creatures: the rate, and the mean time of death of those that died.
void PaiReportDeaths(string sRole, float fSd)
{
    object m = GetModule();
    int n = GetLocalInt(m, "PAI_N");
    int i, k = 0, d = 0;
    float fSum = 0.0;
    for (i = 0; i < n; i++)
    {
        object o = GetLocalObject(m, "PAI_O" + IntToString(i));
        if (GetLocalString(o, "PAI_ROLE") != sRole) continue;
        k++;
        if (GetLocalInt(o, "PAI_DEAD") || !GetIsObjectValid(o) || GetIsDead(o))
        {
            d++;
            fSum += GetLocalFloat(o, "PAI_DIED");
        }
    }
    PaiRate(sRole + ".died", d, k);
    PaiMean(sRole + ".died.t", fSum, d, fSd, 0.5);
}

// The usual readings for a role.
void PaiReportRole(string sRole, int nSeq = 2)
{
    int n = PaiGet(sRole + ".count");
    PaiReportNth(sRole, "per", 1, 0.8);
    PaiReportLocal(sRole, "PAI_FIRSTACT", 0.8, "firstact");
    PaiReportLocal(sRole, "PAI_SWING1", 1.5, "swing1");
    PaiReportNth(sRole, "atk", 1, 1.5);
    PaiReportNth(sRole, "eor", 1, 1.5);
    PaiReportCounts(sRole, "cnvpat");
    PaiMean(sRole + ".swings", IntToFloat(PaiGet(sRole + ".swings")), n, sqrt(IntToFloat(PaiGet(sRole + ".swings")) / IntToFloat(n > 0 ? n : 1)) + 1.0, 0.5);
    PaiReportCounts(sRole, "atypeid");
    PaiReportCounts(sRole, "castid");
    PaiReportTally(sRole + ".firstcast", n);
    PaiReportDeaths(sRole, 4.0);
    PaiReportSeq(sRole, "PAI_EVS", nSeq, "evs");
    PaiReportSeq(sRole, "PAI_ACTS", nSeq, "acts");
}
