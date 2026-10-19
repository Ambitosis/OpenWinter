// Hearing invisible creatures (docs/STEALTH.md, "Invisibility, darkness, blindness"). An invisible creature is
// heard only within the observer's appearance.2da PREFATCKDIST + its own HITDIST + 1.5 m.
// Areas 0-3: an idle observer and invisible targets (not sneaking) at set distances around it, read at 14 and 22 s:
//   0 human observer (PREFATCKDIST 1.7), human targets (HITDIST 0.3): 3.5 m
//   1 halfling observer (1.1), human targets: 2.9 m
//   2 human observer, halfling targets (0.25): 3.45 m
//   3 human observer, brown bear targets (1.2): 4.4 m
// Area 4: eight pairs 20 m apart. Pairs 0-3: a target in plain sight 5 m away turns invisible (expect sight lost at
//   the next pass, hearing at the one after). Pairs 4-7: an invisible target 3 m away (heard) jumps to 5 m (expect
//   hearing lost at the next pass), then back to 3 m (heard again at the next pass).
// Area 5: 24 pairs, the target invisible and in stealth 3.3 m away, Listen 11 vs Move Silently 10: no Spot check
//   and, while invisible, Listen tested on every pass (47.5% heard if each pass compares the cached dice). Sampled
//   every 2 s.
#include "pstealth_inc"

const float END = 420.0;

void Ring(int a, int nObsApp, int nTgtApp, string sDists)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    object o = PsMake("sn_obs", oArea, 30.0, 30.0, STANDARD_FACTION_COMMONER);
    if (nObsApp != APPEARANCE_TYPE_HUMAN) SetCreatureAppearanceType(o, nObsApp);
    SetLocalObject(GetModule(), "R" + IntToString(a), o);
    int i = 0, n = 0;
    string s = sDists;
    while (s != "") { int p = FindSubString(s, " "); s = p < 0 ? "" : GetSubString(s, p + 1, 1000); n++; }
    s = sDists;
    while (s != "")
    {
        int p = FindSubString(s, " ");
        float fDist = StringToFloat(p < 0 ? s : GetStringLeft(s, p));
        s = p < 0 ? "" : GetSubString(s, p + 1, 1000);
        float fAng = 360.0 * IntToFloat(i) / IntToFloat(n);
        object t = PsMake("sn_tgt", oArea, 30.0 + fDist * cos(fAng), 30.0 + fDist * sin(fAng), STANDARD_FACTION_HOSTILE);
        if (nTgtApp != APPEARANCE_TYPE_HUMAN) SetCreatureAppearanceType(t, nTgtApp);
        PsPerm(t, EffectInvisibility(INVISIBILITY_TYPE_NORMAL));
        SetLocalObject(o, "T" + IntToString(i), t);
        i++;
    }
    SetLocalInt(o, "TN", n);
}

void ReadRing(int a, string k)
{
    object o = GetLocalObject(GetModule(), "R" + IntToString(a));
    int i, n = GetLocalInt(o, "TN");
    for (i = 0; i < n; i++)
    {
        object t = GetLocalObject(o, "T" + IntToString(i));
        string d = k + "." + FloatToString(GetDistanceBetween(o, t), 0, 2);
        PLogI(d + ".seen", GetObjectSeen(t, o));
        PLogI(d + ".heard", GetObjectHeard(t, o));
    }
}

// ---- Area 4 ----
void Vanish(object o)
{
    SetLocalFloat(o, "AT", PSince());
    PsPerm(GetLocalObject(o, "H"), EffectInvisibility(INVISIBILITY_TYPE_NORMAL));
}

void Move(object o, float fDist, string sAt)
{
    SetLocalFloat(o, sAt, PSince());
    object h = GetLocalObject(o, "H");
    vector v = GetPosition(o);
    AssignCommand(h, JumpToLocation(Location(GetArea(o), Vector(v.x + fDist, v.y, 0.0), 180.0)));
}

// The first event of a kind after a time, as a delay (-1: none).
float After(object o, string sWhoKind, float fAt)
{
    int j, n = GetLocalInt(o, sWhoKind + "N");
    for (j = 0; j < n; j++)
    {
        float t = GetLocalFloat(o, sWhoKind + IntToString(j));
        if (t >= fAt) return t - fAt;
    }
    return -1.0;
}

void Transitions()
{
    object oArea = GetObjectByTag("SN_A4");
    int i;
    for (i = 0; i < 8; i++)
    {
        float x = 10.0 + IntToFloat(i % 4) * 20.0, y = 10.0 + IntToFloat(i / 4) * 20.0;
        object o = PsMake("sn_obs", oArea, x, y, STANDARD_FACTION_COMMONER);
        object h = PsMake("sn_tgt", oArea, x + (i < 4 ? 5.0 : 3.0), y, STANDARD_FACTION_HOSTILE, 180.0);
        SetLocalObject(o, "H", h);
        SetLocalObject(GetModule(), "X" + IntToString(i), o);
        if (i < 4) DelayCommand(16.0 + IntToFloat(i) * 1.1, Vanish(o));
        else
        {
            PsPerm(h, EffectInvisibility(INVISIBILITY_TYPE_NORMAL));
            DelayCommand(16.0 + IntToFloat(i) * 1.1, Move(o, 5.0, "AT"));
            DelayCommand(32.0 + IntToFloat(i) * 1.1, Move(o, 3.0, "BACK"));
        }
    }
}

void ReportTransitions()
{
    int i;
    float fMaxV = 0.0, fMaxI = 0.0, fMaxGap = 0.0, fMinGap = 99.0, fMaxOut = 0.0, fMaxBack = 0.0;
    int nBad = 0;
    for (i = 0; i < 8; i++)
    {
        object o = GetLocalObject(GetModule(), "X" + IntToString(i));
        float at = GetLocalFloat(o, "AT");
        string k = "trans." + IntToString(i);
        WriteTimestampedLogEntry("RAW|" + k + "|at=" + FloatToString(at, 0, 2) + " back=" + FloatToString(GetLocalFloat(o, "BACK"), 0, 2) + " ev=" + GetLocalString(o, "EV"));
        if (i < 4)
        {
            float v = After(o, "Hv", at), l = After(o, "Hi", at);
            if (v < 0.0 || l < 0.0) { nBad++; continue; }
            if (v > fMaxV) fMaxV = v;
            if (l > fMaxI) fMaxI = l;
            if (l - v > fMaxGap) fMaxGap = l - v;
            if (l - v < fMinGap) fMinGap = l - v;
        }
        else
        {
            float l = After(o, "Hi", at), b = After(o, "Hh", GetLocalFloat(o, "BACK"));
            if (l < 0.0 || b < 0.0) { nBad++; continue; }
            if (l > fMaxOut) fMaxOut = l;
            if (b > fMaxBack) fMaxBack = b;
        }
    }
    // Within one pass (4 s and a frame or two) of the change; hearing lost exactly one pass after sight.
    PLogI("trans.missing", nBad);
    PLogI("trans.invisible.vanished.withinpass", fMaxV <= 4.2);
    PLogI("trans.invisible.inaudible.onepasslater", fMinGap >= 3.8 && fMaxGap <= 4.2);
    PLogI("trans.out.inaudible.withinpass", fMaxOut <= 4.2);
    PLogI("trans.back.heard.withinpass", fMaxBack <= 4.2);
}

// ---- Area 5 ----
void Sample()
{
    if (PSince() > END) return;
    int m;
    for (m = 0; m < 24; m++)
    {
        object o = GetLocalObject(GetModule(), "S" + IntToString(m));
        object h = GetLocalObject(o, "H");
        PsAdd("sneak.n");
        PsAdd("sneak.heard", GetObjectHeard(h, o));
        PsAdd("sneak.seen", GetObjectSeen(h, o));
    }
    DelayCommand(2.0, Sample());
}

void Sneakers()
{
    object oArea = GetObjectByTag("SN_A5");
    int m;
    for (m = 0; m < 24; m++)
    {
        object o = PsPair("sneak", oArea, m, m, 3.3);
        object h = GetLocalObject(o, "H");
        SetLocalObject(GetModule(), "S" + IntToString(m), o);
        PsSkill(o, SKILL_LISTEN, 11, "sneak");
        PsSkill(h, SKILL_MOVE_SILENTLY, 10, "sneak");
        PsPerm(h, EffectInvisibility(INVISIBILITY_TYPE_NORMAL));
        PsStealth(o);
    }
    DelayCommand(30.0, Sample());
}

void ReportSneakers()
{
    int n = PsGet("sneak.n");
    // Samples 2 s apart share dice (refreshed about every 8 s): the tolerance counts a quarter of them.
    float p = n > 0 ? IntToFloat(PsGet("sneak.heard")) / IntToFloat(n) : 0.0;
    float q = p < 0.02 ? 0.02 : p;
    PLogF("sneak.heard", p, 3.5 * sqrt(2.0 * q * (1.0 - q) / IntToFloat(n / 4)));
    PLogI("sneak.seen", PsGet("sneak.seen"));
    WriteTimestampedLogEntry("RAW|sneak|n=" + IntToString(n) + " heard=" + IntToString(PsGet("sneak.heard"))
        + " seen=" + IntToString(PsGet("sneak.seen")) + " badskill=" + IntToString(PsGet("sneak.badskill")));
}

void main()
{
    PStart();
    Ring(0, APPEARANCE_TYPE_HUMAN, APPEARANCE_TYPE_HUMAN, "2.5 2.75 3.0 3.25 3.4 3.6 3.75 4.0 4.5 5.0");
    Ring(1, APPEARANCE_TYPE_HALFLING, APPEARANCE_TYPE_HUMAN, "2.5 2.7 2.8 3.0 3.1 3.3 3.6");
    Ring(2, APPEARANCE_TYPE_HUMAN, APPEARANCE_TYPE_HALFLING, "3.2 3.38 3.52 3.7");
    Ring(3, APPEARANCE_TYPE_HUMAN, APPEARANCE_TYPE_BEAR_BROWN, "4.1 4.3 4.5 4.7");
    int a;
    for (a = 0; a < 4; a++)
    {
        DelayCommand(14.0, ReadRing(a, "ring" + IntToString(a) + ".t14"));
        DelayCommand(22.0, ReadRing(a, "ring" + IntToString(a) + ".t22"));
    }
    Transitions();
    DelayCommand(60.0, ReportTransitions());
    Sneakers();
    DelayCommand(END + 2.0, ReportSneakers());
    DelayCommand(END + 3.0, PDone());
}
