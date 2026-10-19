// Follow-up to stealth_check_rates: the shape of the active (detect mode) observer's roll, and whether an invisible
// hider in stealth stays heard once heard. Trial groups as in stealth_check_rates (silenced hiders 3 m away, idle
// observers), with each pair's outcomes logged in order:
//   det9     detect mode, Spot 10 vs Hide 1
//   det0     detect mode, Spot 10 vs Hide 10
//   detm8    detect mode, Spot 10 vs Hide 18
//   pas4     no detect mode, Spot 10 vs Hide 1
//   walk     no detect mode, walking, Spot 10 vs Hide 1
// Areas 5 and 6: 24 pairs each, the hider invisible and in stealth 3.3 m away (Listen 11 / 31 vs Move Silently 10);
// heard / inaudible events are logged with their times.
#include "pstealth_inc"

const float END = 600.0;

void Group(int a, string g, int nSpot, int nHide, int bDetect, int bWalk)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    int m;
    for (m = 0; m < 24; m++)
    {
        object o = PsPair(g, oArea, m, a * 24 + m, 3.0);
        object h = GetLocalObject(o, "H");
        PsSkill(o, SKILL_SPOT, nSpot, g);
        PsSkill(h, SKILL_HIDE, nHide, g);
        PsPerm(h, EffectSilence());
        PsStealth(o);
        if (bDetect) { SetActionMode(o, ACTION_MODE_DETECT, TRUE); SetLocalInt(o, "DETECT", 1); }
        if (bWalk) PsPace(o, GetLocalLocation(o, "OA"), GetLocalLocation(o, "OB"));
        PsAway(o);
        PsTrials(o, 14.0 + IntToFloat(m) * 0.29, 14.0, END);
    }
}

void Sneakers(int a, string g, int nListen)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    int m;
    for (m = 0; m < 24; m++)
    {
        object o = PsPair(g, oArea, m, 0, 3.3);
        object h = GetLocalObject(o, "H");
        PsSkill(o, SKILL_LISTEN, nListen, g);
        PsSkill(h, SKILL_MOVE_SILENTLY, 10, g);
        PsPerm(h, EffectInvisibility(INVISIBILITY_TYPE_NORMAL));
        PsStealth(o);
    }
}

void ReportSneakers(string g)
{
    int m;
    for (m = 0; m < 24; m++)
        WriteTimestampedLogEntry("RAW|" + g + ".ev" + IntToString(m) + "|" + GetLocalString(GetLocalObject(GetModule(), g + ".O" + IntToString(m)), "EV"));
}

void main()
{
    PStart();
    Group(0, "det9", 10, 1, TRUE, FALSE);
    Group(1, "det0", 10, 10, TRUE, FALSE);
    Group(2, "detm8", 10, 18, TRUE, FALSE);
    Group(3, "pas4", 10, 1, FALSE, FALSE);
    Group(4, "walk", 10, 1, FALSE, TRUE);
    Sneakers(5, "sneak11", 11);
    Sneakers(6, "sneak31", 31);
    string s = "det9 det0 detm8 pas4 walk";
    DelayCommand(END + 6.0, PsReport("det9"));
    DelayCommand(END + 6.0, PsReport("det0"));
    DelayCommand(END + 6.0, PsReport("detm8"));
    DelayCommand(END + 6.0, PsReport("pas4"));
    DelayCommand(END + 6.0, PsReport("walk"));
    DelayCommand(END + 6.0, PsReportTrials("det9", 24));
    DelayCommand(END + 6.0, PsReportTrials("det0", 24));
    DelayCommand(END + 6.0, PsReportTrials("detm8", 24));
    DelayCommand(END + 6.0, PsReportTrials("pas4", 24));
    DelayCommand(END + 6.0, PsReportTrials("walk", 24));
    DelayCommand(END + 6.0, ReportSneakers("sneak11"));
    DelayCommand(END + 6.0, ReportSneakers("sneak31"));
    DelayCommand(END + 7.0, PDone());
}
