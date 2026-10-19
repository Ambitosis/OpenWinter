// When observers' stealth checks happen (docs/STEALTH.md, "How often"). In each cell an idle observer O (long
// perception range 35/20) has:
// - a beacon B (not in stealth) 3 m away that leaves for the parking area and comes back every 10 s: O seeing it
//   come back marks one of O's perception passes, so the passes can be laid out on a grid (O notices a creature
//   leaving the area at once, so the leaving doesn't mark a pass);
// - a second beacon B2 that stays (so O always has someone to evaluate);
// - a hider H 15 m away in stealth, silenced, Spot - Hide = -8, out of everyone's 10 m range itself (so its own
//   cached Hide die never changes). O is idle without detect mode (passive: half its Spot 30 and a d10), H has
//   Hide 15, so O sees it when its die beats H's. Each time O sees it, H parks for 6 s and comes back. If O's dice
//   are refreshed only on every second pass, all of O's sightings of H fall on passes of one parity.
// - a second hider H2 15 m away with Hide 51; at a staggered time its Hide drops by 50 (O then always wins). The
//   delay to O seeing it is at most one pass if every pass checks (with the cached dice), up to two if only every
//   second pass does.
// - fresh observers F (one per cell) made beside B2 at staggered times: the delay to their first event shows when a
//   new creature's first pass comes.
// (Spot - Hide = -8 in the first version of this suite was never beaten: a passive observer can't make up 8.)
#include "pstealth_inc"

const int CELLS = 32;
const float END = 318.0;

void Beacon(object o, int bHome)
{
    if (PSince() > END) return;
    object b = GetLocalObject(o, "B");
    AssignCommand(b, ClearAllActions());
    AssignCommand(b, JumpToLocation(GetLocalLocation(o, bHome ? "BHOME" : "BPARK")));
    DelayCommand(10.0, Beacon(o, !bHome));
}

void Drop(object o)
{
    SetLocalFloat(o, "DROP", PSince());
    PsPerm(GetLocalObject(o, "H2"), EffectSkillDecrease(SKILL_HIDE, 50));
}

void Fresh(object o)
{
    location l = GetLocalLocation(o, "FHOME");
    object f = PsMake("sn_obs", GetAreaFromLocation(l), GetPositionFromLocation(l).x, GetPositionFromLocation(l).y, STANDARD_FACTION_COMMONER);
    SetLocalFloat(f, "BORN", PSince());
    SetLocalObject(o, "F", f);
}

void Cell(int i)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(i / 16));
    int c = i % 16;
    float cx = 20.0 + IntToFloat(c % 4) * 40.0, cy = 20.0 + IntToFloat(c / 4) * 40.0;
    object o = PsMake("sn_obsl", oArea, cx, cy, STANDARD_FACTION_COMMONER);
    object b = PsMake("sn_tgt", oArea, cx - 3.0, cy + 1.5, STANDARD_FACTION_MERCHANT);
    object b2 = PsMake("sn_tgt", oArea, cx - 3.0, cy - 1.5, STANDARD_FACTION_MERCHANT);
    object h = PsMake("sn_tgt", oArea, cx + 15.0, cy, STANDARD_FACTION_HOSTILE);
    object h2 = PsMake("sn_tgt", oArea, cx, cy + 15.0, STANDARD_FACTION_HOSTILE);
    SetLocalObject(GetModule(), "O" + IntToString(i), o);
    SetLocalObject(o, "B", b);
    SetLocalObject(o, "H", h);
    SetLocalObject(o, "H2", h2);
    SetLocalInt(o, "BOUNCE", 1);
    SetLocalLocation(o, "HOME", GetLocation(h));
    SetLocalLocation(o, "PARK", PsParkSpot(2 * i));
    SetLocalLocation(o, "BHOME", GetLocation(b));
    SetLocalLocation(o, "BPARK", PsParkSpot(2 * i + 1));
    SetLocalLocation(o, "FHOME", Location(oArea, Vector(cx - 3.0, cy - 4.5, 0.0), 0.0));
    PsSkill(o, SKILL_SPOT, 30, "cad");
    PsSkill(h, SKILL_HIDE, 15, "cad");
    PsSkill(h2, SKILL_HIDE, 51, "cad");
    PsPerm(h, EffectSilence());
    PsPerm(h2, EffectSilence());
    SetActionMode(h, ACTION_MODE_STEALTH, TRUE);
    SetActionMode(h2, ACTION_MODE_STEALTH, TRUE);
    DelayCommand(12.0 + IntToFloat(i) * 0.29, Beacon(o, FALSE));
    DelayCommand(40.0 + IntToFloat(i) * 0.61, Drop(o));
    DelayCommand(25.0 + IntToFloat(i) * 0.37, Fresh(o));
}

void Report()
{
    int i, j;
    float fSumT = 0.0; int nT = 0;
    float fFirst = 0.0, fFirstMax = 0.0, fFirstMin = 99.0; int nFirst = 0;
    float fDrop = 0.0, fDropMax = 0.0; int nDrop = 0, nDropLate = 0;
    int nMixed = 0, nUniform = 0, nOff = 0, nDet = 0;
    for (i = 0; i < CELLS; i++)
    {
        object o = GetLocalObject(GetModule(), "O" + IntToString(i));
        // The pass interval from the beacon's events: each gap holds a whole number of passes.
        int nb = GetLocalInt(o, "BN");
        float fGap = 0.0; int nPass = 0;
        for (j = 1; j < nb; j++)
        {
            float d = GetLocalFloat(o, "B" + IntToString(j)) - GetLocalFloat(o, "B" + IntToString(j - 1));
            int k = FloatToInt(d / 4.0 + 0.5);
            fGap += d; nPass += k;
            SetLocalInt(o, "BI" + IntToString(j), nPass);
        }
        float T = nPass > 0 ? fGap / IntToFloat(nPass) : 4.0;
        if (nPass > 0) { fSumT += T; nT++; }
        // Which passes the sightings of H fall on, counted from the last time B was seen coming back.
        int nh = GetLocalInt(o, "HsN"), nEven = 0, nOdd = 0;
        for (j = 0; j < nh; j++)
        {
            float t = GetLocalFloat(o, "Hs" + IntToString(j));
            int a = 0, i2;
            for (i2 = 1; i2 < nb; i2++) if (GetLocalFloat(o, "B" + IntToString(i2)) <= t + 0.05) a = i2;
            float d = t - GetLocalFloat(o, "B" + IntToString(a));
            int k = FloatToInt(d / 4.0 + (d >= 0.0 ? 0.5 : -0.5));
            float r = d - IntToFloat(k) * 4.0;
            if (r > 0.6 || r < -0.6) nOff++;
            k += GetLocalInt(o, "BI" + IntToString(a));
            if (k % 2 == 0) nEven++; else nOdd++;
            nDet++;
        }
        if (nEven > 0 && nOdd > 0) nMixed++;
        if (nh >= 2 && (nEven == 0 || nOdd == 0)) nUniform++;
        // Hide drop to sighting.
        if (GetLocalInt(o, "IsN") > 0)
        {
            float d = GetLocalFloat(o, "Is0") - GetLocalFloat(o, "DROP");
            fDrop += d; nDrop++;
            if (d > fDropMax) fDropMax = d;
            if (d > 4.3) nDropLate++;
        }
        // A fresh observer's first event.
        object f = GetLocalObject(o, "F");
        float ff = GetLocalFloat(f, "FIRST");
        if (ff > 0.0)
        {
            float d = ff - GetLocalFloat(f, "BORN");
            fFirst += d; nFirst++;
            if (d > fFirstMax) fFirstMax = d;
            if (d < fFirstMin) fFirstMin = d;
        }
        WriteTimestampedLogEntry("RAW|cad." + IntToString(i) + "|T=" + FloatToString(T, 0, 3) + " even=" + IntToString(nEven)
            + " odd=" + IntToString(nOdd) + " drop=" + FloatToString(GetLocalFloat(o, "DROP"), 0, 2)
            + " fborn=" + FloatToString(GetLocalFloat(f, "BORN"), 0, 2) + " ffirst=" + FloatToString(ff, 0, 2)
            + " ev=" + GetLocalString(o, "EV"));
    }
    PLogF("pass.interval", nT > 0 ? fSumT / IntToFloat(nT) : 0.0, 0.05);
    PLogI("hider.sightings.offgrid", nOff);
    PLogI("hider.observers.mixedparity", nMixed);
    WriteTimestampedLogEntry("RAW|cad.summary|sightings=" + IntToString(nDet) + " uniform=" + IntToString(nUniform)
        + " mixed=" + IntToString(nMixed) + " offgrid=" + IntToString(nOff) + " drops=" + IntToString(nDrop)
        + " droplate=" + IntToString(nDropLate) + " dropmax=" + FloatToString(fDropMax, 0, 2)
        + " firstmin=" + FloatToString(fFirstMin, 0, 2) + " firstmax=" + FloatToString(fFirstMax, 0, 2)
        + " badskill=" + IntToString(PsGet("cad.badskill")));
    PLogF("drop.mean", nDrop > 0 ? fDrop / IntToFloat(nDrop) : -1.0, 0.6);
    PLogF("drop.max", fDropMax, 0.3);
    PLogI("drop.late", nDropLate);
    // The first pass comes 4.0-4.6 s after a creature is made on nwserver (the spread isn't compared).
    PLogF("first.mean", nFirst > 0 ? fFirst / IntToFloat(nFirst) : -1.0, 0.3);
    PLogI("first.before4", fFirstMin < 3.95);
}

void main()
{
    PStart();
    int i;
    for (i = 0; i < CELLS; i++) Cell(i);
    DelayCommand(END + 2.0, Report());
    DelayCommand(END + 3.0, PDone());
}
