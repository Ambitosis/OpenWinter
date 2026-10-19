// Idle stance, natural 20s and moving hiders.
// As stealth_check_rates: 20 pairs per group, each an observer (bandit, commoner) and a silenced hider in
// stealth 3 m east of it (hostile); every 14 s the hider comes back from the parking area and the result is read
// 4.2 s after the jump. New here: a beacon B (a plain creature, not sneaking) jumps back with the hider, 2 m west
// of the observer, so the observer's OnPerception "seen" for B marks its first pass after the return. A trial counts
// as "passed" when that pass came before the reading; rates are given over all trials and over passed ones.
//   natdet   observer made by CreateObject, never moved, detect mode; Spot 10 vs Hide -20 (stealth_check_natural)
//   natnew   never moved, no detect mode; Spot 21 vs Hide 1 (passive: 67.5%; active: 100%)
//   natjump  as natnew, but the observer was jumped into its spot once at the start (idle stance: 100% if active)
//   idlejump observer jumped once; Spot 10 vs Hide 1 (active 83.5%, passive 42.5%)
//   idlewalk observer walked 2 m into its spot at the start; Spot 10 vs Hide 1
//   hpace    observer jumped once; Spot 10 vs Hide 1; the hider walks 16 m north past the observer (3 m east of it,
//            from 8 m south): whether it was moving (GetCurrentAction = move) when B was seen is recorded, and the
//            rate is given for both kinds of pass (moving 94.75% / still 83.5% if the observer is active)
#include "pstealth_inc"

const float END = 600.0;
const int PAIRS = 20;

void Away(object o)
{
    object h = GetLocalObject(o, "H"), b = GetLocalObject(o, "B");
    AssignCommand(h, ClearAllActions());
    AssignCommand(h, JumpToLocation(GetLocalLocation(o, "PARK")));
    AssignCommand(b, ClearAllActions());
    AssignCommand(b, JumpToLocation(GetLocalLocation(o, "BPARK")));
}

void Read(object o)
{
    object h = GetLocalObject(o, "H");
    string g = GetLocalString(o, "G");
    if (GetArea(h) != GetArea(o) || !GetActionMode(h, ACTION_MODE_STEALTH)) { PsAdd(g + ".bad"); return; }
    int s = GetObjectSeen(h, o);
    float tj = GetLocalFloat(o, "TJ");
    int nb = GetLocalInt(o, "BN");
    int passed = nb > 0 && GetLocalFloat(o, "B" + IntToString(nb - 1)) >= tj;
    PsAdd(g + ".n");
    PsAdd(g + ".seen", s);
    PsAdd(g + ".passed", passed);
    PsAdd(g + ".seenpassed", s && passed);
    if (GetIsInCombat(o)) PsAdd(g + ".incombat");
    if (passed)
    {
        // The time from the jump to the pass that saw B, in centiseconds.
        PsAdd(g + ".passcs", FloatToInt((GetLocalFloat(o, "B" + IntToString(nb - 1)) - tj) * 100.0));
        if (GetLocalInt(o, "PACE"))
        {
            string k = GetLocalInt(o, "HMOV") ? ".mov" : ".still";
            PsAdd(g + k + ".n");
            PsAdd(g + k + ".seen", s);
        }
    }
    SetLocalString(o, "TR", GetLocalString(o, "TR") + IntToString(s + 2 * passed));
}

void Home(object o, float fCycle)
{
    if (PSince() > END) return;
    object h = GetLocalObject(o, "H"), b = GetLocalObject(o, "B");
    SetLocalFloat(o, "TJ", PSince());
    AssignCommand(h, ClearAllActions());
    AssignCommand(h, JumpToLocation(GetLocalLocation(o, "HOME")));
    AssignCommand(b, ClearAllActions());
    AssignCommand(b, JumpToLocation(GetLocalLocation(o, "BHOME")));
    if (GetLocalInt(o, "PACE")) AssignCommand(h, ActionMoveToLocation(GetLocalLocation(o, "WALKTO")));
    DelayCommand(4.2, Read(o));
    DelayCommand(fCycle / 2.0, Away(o));
    DelayCommand(fCycle, Home(o, fCycle));
}

void Group(int a, string g, int nSpot, int nHide, int bDetect, int nStart, int bPace)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    int m;
    for (m = 0; m < PAIRS; m++)
    {
        object o = PsPair(g, oArea, m, a * PAIRS + m, 3.0);
        object h = GetLocalObject(o, "H");
        vector vo = GetPosition(o);
        object b = PsMake("sn_tgt", oArea, vo.x - 2.0, vo.y, STANDARD_FACTION_MERCHANT);
        SetLocalObject(o, "B", b);
        SetLocalLocation(o, "BHOME", GetLocation(b));
        vector vp = GetPositionFromLocation(GetLocalLocation(o, "PARK"));
        SetLocalLocation(o, "BPARK", Location(GetAreaFromLocation(GetLocalLocation(o, "PARK")), Vector(vp.x, vp.y + 3.0, 0.0), 0.0));
        PsSkill(o, SKILL_SPOT, nSpot, g);
        PsSkill(h, SKILL_HIDE, nHide, g);
        PsPerm(h, EffectSilence());
        PsStealth(o);
        if (bDetect) SetActionMode(o, ACTION_MODE_DETECT, TRUE);
        // 1: jumped into its spot once; 2: walks 2 m into it.
        location lSpot = GetLocation(o);
        if (nStart > 0)
        {
            AssignCommand(o, JumpToLocation(Location(oArea, Vector(vo.x, vo.y + 2.0, 0.0), 0.0)));
            if (nStart == 1) DelayCommand(1.0, AssignCommand(o, JumpToLocation(lSpot)));
            else DelayCommand(1.0, AssignCommand(o, ActionMoveToLocation(lSpot)));
        }
        if (bPace)
        {
            vector v = GetPosition(h);
            SetLocalLocation(o, "HOME", Location(oArea, Vector(v.x, v.y - 8.0, 0.0), 0.0));
            SetLocalLocation(o, "WALKTO", Location(oArea, Vector(v.x, v.y + 8.0, 0.0), 0.0));
            SetLocalInt(o, "PACE", 1);
        }
        Away(o);
        DelayCommand(14.0 + IntToFloat(m) * 0.37, Home(o, 14.0));
    }
}

void Report(string g)
{
    // Only the rate over trials with a pass is compared: on nwserver, with this many creatures, the observer's pass
    // came before the reading in only about one trial in five (raw counts below), so "seen" over all trials says more
    // about load than about the check. The walking hider's group is kept uncompared (its passes mostly came before
    // the hider was back: see docs/STEALTH.md).
    int n = PsGet(g + ".n"), np = PsGet(g + ".passed");
    if (g != "hpace") PLogRate(g + ".seen.passed", PsGet(g + ".seenpassed"), np);
    WriteTimestampedLogEntry("RAW|" + g + "|n=" + IntToString(n) + " seen=" + IntToString(PsGet(g + ".seen"))
        + " passed=" + IntToString(np) + " seenpassed=" + IntToString(PsGet(g + ".seenpassed")) + " bad=" + IntToString(PsGet(g + ".bad"))
        + " badskill=" + IntToString(PsGet(g + ".badskill")) + " incombat=" + IntToString(PsGet(g + ".incombat"))
        + " mov=" + IntToString(PsGet(g + ".mov.seen")) + "/" + IntToString(PsGet(g + ".mov.n"))
        + " still=" + IntToString(PsGet(g + ".still.seen")) + "/" + IntToString(PsGet(g + ".still.n")));
    PsReportTrials(g, 3);
}

void main()
{
    PStart();
    Group(0, "natdet", 10, -20, TRUE, 0, FALSE);
    Group(1, "natnew", 21, 1, FALSE, 0, FALSE);
    Group(2, "natjump", 21, 1, FALSE, 1, FALSE);
    Group(3, "idlejump", 10, 1, FALSE, 1, FALSE);
    Group(4, "idlewalk", 10, 1, FALSE, 2, FALSE);
    Group(5, "hpace", 10, 1, FALSE, 1, TRUE);
    string gs = "natdet natnew natjump idlejump idlewalk hpace ";
    while (gs != "")
    {
        int sp = FindSubString(gs, " ");
        DelayCommand(END + 6.0, Report(GetStringLeft(gs, sp)));
        gs = GetSubString(gs, sp + 1, 1000);
    }
    DelayCommand(END + 7.0, PDone());
}
