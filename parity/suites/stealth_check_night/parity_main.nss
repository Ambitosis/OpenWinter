// Night and light in the Spot check (docs/STEALTH.md). The clock is set to 19:00 (night: dusk 18, dawn 6), then
// groups of 24 idle observers, each with a still hider in stealth 3.3 m east (not silenced), run trials as in
// stealth_check_rates. Spot - Hide = 0 and Listen - Move Silently = 1 (Listen has no night or light modifier and
// the 3.3 m costs 1), so in daylight both rates would be 47.5% (stealth_check_size "medium").
//   none     no light anywhere
//   olight   the observer carries VFX_DUR_LIGHT_WHITE_20
//   hlight   the hider carries it
//   odark    the observer has VFX_DUR_DARKVISION
//   both     both carry light
#include "pstealth_inc"

const float END = 545.0;

void Group(int a, string g, int nObsFx, int nHidFx)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    int m;
    for (m = 0; m < 24; m++)
    {
        object o = PsPair(g, oArea, m, a * 24 + m, 3.3);
        object h = GetLocalObject(o, "H");
        PsSkill(o, SKILL_SPOT, 10, g);
        PsSkill(o, SKILL_LISTEN, 11, g);
        PsSkill(h, SKILL_HIDE, 10, g);
        PsSkill(h, SKILL_MOVE_SILENTLY, 10, g);
        if (nObsFx >= 0) PsPerm(o, EffectVisualEffect(nObsFx));
        if (nHidFx >= 0) PsPerm(h, EffectVisualEffect(nHidFx));
        PsStealth(o);
        PsAway(o);
        PsTrials(o, 14.0 + IntToFloat(m) * 0.29, 14.0, END);
    }
}

void Start()
{
    PStart();
    WriteTimestampedLogEntry("RAW|clock|" + IntToString(GetTimeHour()) + ":" + IntToString(GetTimeMinute()) + " night=" + IntToString(GetIsNight()));
    Group(0, "none", -1, -1);
    Group(1, "olight", VFX_DUR_LIGHT_WHITE_20, -1);
    Group(2, "hlight", -1, VFX_DUR_LIGHT_WHITE_20);
    Group(3, "odark", VFX_DUR_DARKVISION, -1);
    Group(4, "both", VFX_DUR_LIGHT_WHITE_20, VFX_DUR_LIGHT_WHITE_20);
    DelayCommand(END + 6.0, WriteTimestampedLogEntry("RAW|clock.end|" + IntToString(GetTimeHour()) + ":" + IntToString(GetTimeMinute()) + " night=" + IntToString(GetIsNight())));
    DelayCommand(END + 6.0, PsReport("none"));
    DelayCommand(END + 6.0, PsReport("olight"));
    DelayCommand(END + 6.0, PsReport("hlight"));
    DelayCommand(END + 6.0, PsReport("odark"));
    DelayCommand(END + 6.0, PsReport("both"));
    DelayCommand(END + 7.0, PDone());
}

void main()
{
    SetTime(19, 0, 0, 0);
    DelayCommand(1.0, Start());
}
