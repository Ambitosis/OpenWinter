// A hider whose Hide (Move Silently) total is exactly 0 is spotted (heard) as if it weren't sneaking; -1 is not
// (docs/STEALTH.md, "The Spot check"). Two groups of 24 idle observers with Spot and Listen -10, each with a still
// hider in stealth 3.3 m east, not silenced; trials as in stealth_check_rates (one perception pass each).
//   zero   Hide 0, Move Silently 0
//   minus1 Hide -1, Move Silently -1
#include "pstealth_inc"

const float END = 600.0;

void Group(int a, string g, int nHider)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    int m;
    for (m = 0; m < 24; m++)
    {
        object o = PsPair(g, oArea, m, a * 24 + m, 3.3);
        object h = GetLocalObject(o, "H");
        PsSkill(o, SKILL_SPOT, -10, g);
        PsSkill(o, SKILL_LISTEN, -10, g);
        PsSkill(h, SKILL_HIDE, nHider, g);
        PsSkill(h, SKILL_MOVE_SILENTLY, nHider, g);
        PsStealth(o);
        PsAway(o);
        PsTrials(o, 14.0 + IntToFloat(m) * 0.29, 14.0, END);
    }
}

void main()
{
    PStart();
    Group(0, "zero", 0);
    Group(1, "minus1", -1);
    DelayCommand(END + 6.0, PsReport("zero"));
    DelayCommand(END + 6.0, PsReport("minus1"));
    DelayCommand(END + 7.0, PDone());
}
