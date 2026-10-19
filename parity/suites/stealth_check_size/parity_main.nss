// Size in the Spot and Listen checks (docs/STEALTH.md). Groups of 24 idle observers, each with a still hider in
// stealth 4 m east (not silenced), trials as in stealth_check_rates. Totals after the appearance change:
// Spot - Hide = 0 and Listen - Move Silently = 1 (the 4 m costs 1 on Listen), so with no size modifier both
// rates would be 47.5%. Appearances (appearance.2da SIZECATEGORY): badger 8 tiny, halfling 3 small, human 6
// medium, brown bear 13 large, black dragon 41 huge.
//   tiny, small, medium, large, huge   the hider's size, a human observer
//   obstiny  a tiny observer and a large hider (three steps)
//   obshuge  a huge observer and a small hider (minus three steps)
#include "pstealth_inc"

const float END = 600.0;

void Group(int a, string g, int nObsApp, int nHidApp)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    int m;
    for (m = 0; m < 24; m++)
    {
        object o = PsPair(g, oArea, m, a * 24 + m, 4.0);
        object h = GetLocalObject(o, "H");
        if (nObsApp != APPEARANCE_TYPE_HUMAN) SetCreatureAppearanceType(o, nObsApp);
        if (nHidApp != APPEARANCE_TYPE_HUMAN) SetCreatureAppearanceType(h, nHidApp);
        PsSkill(o, SKILL_SPOT, 10, g);
        PsSkill(o, SKILL_LISTEN, 11, g);
        PsSkill(h, SKILL_HIDE, 10, g);
        PsSkill(h, SKILL_MOVE_SILENTLY, 10, g);
        if (m == 0) WriteTimestampedLogEntry("RAW|" + g + ".sizes|obs=" + IntToString(GetCreatureSize(o)) + " hider=" + IntToString(GetCreatureSize(h)));
        PsStealth(o);
        PsAway(o);
        PsTrials(o, 14.0 + IntToFloat(m) * 0.29, 14.0, END);
    }
}

void main()
{
    PStart();
    Group(0, "tiny", APPEARANCE_TYPE_HUMAN, APPEARANCE_TYPE_BADGER);
    Group(1, "small", APPEARANCE_TYPE_HUMAN, APPEARANCE_TYPE_HALFLING);
    Group(2, "medium", APPEARANCE_TYPE_HUMAN, APPEARANCE_TYPE_HUMAN);
    Group(3, "large", APPEARANCE_TYPE_HUMAN, APPEARANCE_TYPE_BEAR_BROWN);
    Group(4, "huge", APPEARANCE_TYPE_HUMAN, APPEARANCE_TYPE_DRAGON_BLACK);
    Group(5, "obstiny", APPEARANCE_TYPE_BADGER, APPEARANCE_TYPE_BEAR_BROWN);
    Group(6, "obshuge", APPEARANCE_TYPE_DRAGON_BLACK, APPEARANCE_TYPE_HALFLING);
    DelayCommand(END + 6.0, PsReport("tiny"));
    DelayCommand(END + 6.0, PsReport("small"));
    DelayCommand(END + 6.0, PsReport("medium"));
    DelayCommand(END + 6.0, PsReport("large"));
    DelayCommand(END + 6.0, PsReport("huge"));
    DelayCommand(END + 6.0, PsReport("obstiny"));
    DelayCommand(END + 6.0, PsReport("obshuge"));
    DelayCommand(END + 7.0, PDone());
}
