// Shared helpers for the stealth-check suites stealth_check_* (docs/STEALTH.md): exact skill totals, observer/hider
// pairs, and trials where the hider leaves for a parking area and comes back, so each trial starts unperceived.
#include "parity_inc"
#include "parity_time"

void PsPerm(object o, effect e) { ApplyEffectToObject(DURATION_TYPE_PERMANENT, SupernaturalEffect(e), o); }

// Sets the total of nSkill (what GetSkillRank reports) to exactly nTotal with a skill increase or decrease.
// Returns the total read back; a mismatch is counted in "<g>.badskill".
int PsSkill(object o, int nSkill, int nTotal, string g = "")
{
    int d = nTotal - GetSkillRank(nSkill, o);
    if (d > 0) PsPerm(o, EffectSkillIncrease(nSkill, d));
    if (d < 0) PsPerm(o, EffectSkillDecrease(nSkill, -d));
    int r = GetSkillRank(nSkill, o);
    if (r != nTotal && g != "") SetLocalInt(GetModule(), g + ".badskill", GetLocalInt(GetModule(), g + ".badskill") + 1);
    return r;
}

object PsMake(string sRes, object oArea, float x, float y, int nFaction, float fFacing = 0.0)
{
    object o = CreateObject(OBJECT_TYPE_CREATURE, sRes, Location(oArea, Vector(x, y, 0.0), fFacing));
    ChangeToStandardFaction(o, nFaction);
    SetAILevel(o, AI_LEVEL_NORMAL);
    return o;
}

int PsGet(string k) { return GetLocalInt(GetModule(), k); }
void PsAdd(string k, int n = 1) { SetLocalInt(GetModule(), k, GetLocalInt(GetModule(), k) + n); }

// Walks o back and forth between a and b for as long as it has nothing else to do.
void PsPace(object o, location a, location b)
{
    AssignCommand(o, ActionMoveToLocation(a));
    AssignCommand(o, ActionMoveToLocation(b));
    AssignCommand(o, ActionDoCommand(PsPace(o, a, b)));
}

// The parking area (16 x 16 tiles): spots 12 m apart, beyond every parked creature's 10 m perception range.
location PsParkSpot(int n)
{
    object oPark = GetObjectByTag("SN_PARK");
    return Location(oPark, Vector(6.0 + IntToFloat(n % 13) * 12.0, 6.0 + IntToFloat(n / 13) * 12.0, 0.0), 0.0);
}

// The rate helper of pcombat_inc (kept here so these suites don't pull in the combat include).
void PLogRate(string sKey, int nNum, int nDen, float fFloor = 0.02)
{
    if (nDen <= 0) { PLog(sKey, "none"); return; }
    float p = IntToFloat(nNum) / IntToFloat(nDen);
    float q = p < 0.02 ? 0.02 : (p > 0.98 ? 0.98 : p);
    float tol = 3.5 * sqrt(2.0 * q * (1.0 - q) / IntToFloat(nDen));
    if (tol < fFloor) tol = fFloor;
    PLogF(sKey, p, tol);
}

// ---- Trials -------------------------------------------------------------------------------------------------
// A pair: observer o (local "H" = its hider, "G" = its group, "HOME" = the hider's spot, "PARK" = its parking spot,
// "PACE" = 1 when the hider paces between "PA" and "PB", 2 when it walks from "HOME" to "WALKTO"). Each trial the hider comes home, the observer gets one
// perception pass (read 4.2 s after the jump), then the hider parks until the next trial. Counted per group:
// n, seen, heard, and "bad" (the hider not home or out of stealth when read).

void PsRead(object o)
{
    object h = GetLocalObject(o, "H");
    string g = GetLocalString(o, "G");
    if (GetArea(h) != GetArea(o) || !GetActionMode(h, ACTION_MODE_STEALTH) && GetLocalInt(o, "STEALTH")) { PsAdd(g + ".bad"); return; }
    PsAdd(g + ".n");
    int s = GetObjectSeen(h, o), l = GetObjectHeard(h, o);
    PsAdd(g + ".seen", s);
    PsAdd(g + ".heard", l);
    PsAdd(g + ".both", s && l);
    if (GetIsInCombat(o)) PsAdd(g + ".incombat");
    PsAdd(g + ".cm", FloatToInt(GetDistanceBetween(o, h) * 100.0));
    if (GetLocalInt(o, "DETECT") && !GetActionMode(o, ACTION_MODE_DETECT)) PsAdd(g + ".detoff");
    // Per-pair outcomes in trial order (uncompared): 0 neither, 1 seen, 2 heard, 3 both.
    SetLocalString(o, "TR", GetLocalString(o, "TR") + IntToString(s + 2 * l));
}

void PsAway(object o)
{
    object h = GetLocalObject(o, "H");
    AssignCommand(h, ClearAllActions());
    AssignCommand(h, JumpToLocation(GetLocalLocation(o, "PARK")));
}

void PsHome(object o, float fCycle, float fEnd)
{
    if (PSince() > fEnd) return;
    object h = GetLocalObject(o, "H");
    AssignCommand(h, ClearAllActions());
    AssignCommand(h, JumpToLocation(GetLocalLocation(o, "HOME")));
    if (GetLocalInt(o, "PACE") == 1) PsPace(h, GetLocalLocation(o, "PA"), GetLocalLocation(o, "PB"));
    if (GetLocalInt(o, "PACE") == 2) AssignCommand(h, ActionMoveToLocation(GetLocalLocation(o, "WALKTO")));
    DelayCommand(4.2, PsRead(o));
    DelayCommand(fCycle / 2.0, PsAway(o));
    DelayCommand(fCycle, PsHome(o, fCycle, fEnd));
}

// A pair in cell m of oArea (cells 20 m apart, 6 to a row): an idle observer (blueprint sObs, commoner) facing a
// hider (sHid, hostile) fDist metres east of it, which parks at parking spot nPark between trials. Returns the
// observer; the hider is its local "H".
object PsPair(string g, object oArea, int m, int nPark, float fDist, string sObs = "sn_obs", string sHid = "sn_tgt")
{
    float x = 10.0 + IntToFloat(m % 6) * 20.0, y = 10.0 + IntToFloat(m / 6) * 20.0;
    object o = PsMake(sObs, oArea, x - fDist / 2.0, y, STANDARD_FACTION_COMMONER);
    object h = PsMake(sHid, oArea, x + fDist / 2.0, y, STANDARD_FACTION_HOSTILE, 180.0);
    SetLocalObject(o, "H", h);
    SetLocalString(o, "G", g);
    SetLocalObject(GetModule(), g + ".O" + IntToString(m), o);
    SetLocalLocation(o, "HOME", GetLocation(h));
    SetLocalLocation(o, "PARK", PsParkSpot(nPark));
    SetLocalLocation(o, "PA", Location(oArea, Vector(x + fDist / 2.0, y + 2.0, 0.0), 0.0));
    SetLocalLocation(o, "PB", Location(oArea, Vector(x + fDist / 2.0, y - 2.0, 0.0), 0.0));
    SetLocalLocation(o, "OA", Location(oArea, Vector(x - fDist / 2.0, y + 3.0, 0.0), 0.0));
    SetLocalLocation(o, "OB", Location(oArea, Vector(x - fDist / 2.0, y - 3.0, 0.0), 0.0));
    return o;
}

// Puts the pair's hider in stealth (and remembers that it should stay in it).
void PsStealth(object o)
{
    SetActionMode(GetLocalObject(o, "H"), ACTION_MODE_STEALTH, TRUE);
    SetLocalInt(o, "STEALTH", 1);
}

// Starts a pair's trials at fStart (seconds from now), every fCycle seconds until fEnd (PSince time).
void PsTrials(object o, float fStart, float fCycle, float fEnd)
{
    DelayCommand(fStart, PsHome(o, fCycle, fEnd));
}

// The group's rates, compared with a tolerance from the sample size, and its raw counts (uncompared).
void PsReport(string g)
{
    int n = PsGet(g + ".n");
    PLogRate(g + ".seen", PsGet(g + ".seen"), n);
    PLogRate(g + ".heard", PsGet(g + ".heard"), n);
    WriteTimestampedLogEntry("RAW|" + g + "|n=" + IntToString(n) + " seen=" + IntToString(PsGet(g + ".seen"))
        + " heard=" + IntToString(PsGet(g + ".heard")) + " both=" + IntToString(PsGet(g + ".both"))
        + " bad=" + IntToString(PsGet(g + ".bad")) + " badskill=" + IntToString(PsGet(g + ".badskill"))
        + " incombat=" + IntToString(PsGet(g + ".incombat")) + " detoff=" + IntToString(PsGet(g + ".detoff"))
        + " meandist=" + FloatToString(n > 0 ? IntToFloat(PsGet(g + ".cm")) / IntToFloat(n) / 100.0 : 0.0, 0, 2));
}

// Each pair's outcomes in trial order (RAW, uncompared). The observers are the module's "<g>.O<m>" locals.
void PsReportTrials(string g, int nPairs)
{
    int m;
    for (m = 0; m < nPairs; m++)
        WriteTimestampedLogEntry("RAW|" + g + ".tr" + IntToString(m) + "|" + GetLocalString(GetLocalObject(GetModule(), g + ".O" + IntToString(m)), "TR"));
}

// ---- Perception event log --------------------------------------------------------------------------------------
// The observers' OnPerception script (each suite's sn_percept.nss calls this). Records the first event of any kind
// ("FIRST", seconds since the suite started), and for the observer's tracked objects (locals "H", "H2", "B") the
// event times: "<who><n>" floats and "<who>N" counts per who+kind, who = H, I (H2), B; kind = s, h, v, i.
void PsOnNotice()
{
    object me = OBJECT_SELF, p = GetLastPerceived();
    float t = PSince();
    if (GetLocalFloat(me, "FIRST") == 0.0) SetLocalFloat(me, "FIRST", t);
    string w = p == GetLocalObject(me, "H") ? "H" : p == GetLocalObject(me, "H2") ? "I" : p == GetLocalObject(me, "B") ? "B" : "";
    if (w == "") return;
    string k = GetLastPerceptionSeen() ? "s" : GetLastPerceptionHeard() ? "h" : GetLastPerceptionVanished() ? "v" : "i";
    int n = GetLocalInt(me, w + k + "N");
    SetLocalFloat(me, w + k + IntToString(n), t);
    SetLocalInt(me, w + k + "N", n + 1);
    // B coming into view marks one of the observer's perception passes (leaving the area is noticed at once).
    if (w == "B" && k == "s")
    {
        int m = GetLocalInt(me, "BN");
        SetLocalFloat(me, "B" + IntToString(m), t);
        SetLocalInt(me, "BN", m + 1);
    }
    SetLocalString(me, "EV", GetLocalString(me, "EV") + w + k + "@" + FloatToString(t, 0, 2) + " ");
    if (w == "H" && k == "s" && GetLocalInt(me, "BOUNCE"))
    {
        DelayCommand(0.3, PsAway(me));
        DelayCommand(6.3, AssignCommand(GetLocalObject(me, "H"), JumpToLocation(GetLocalLocation(me, "HOME"))));
    }
}
