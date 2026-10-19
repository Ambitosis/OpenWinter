// Natural rolls: a check that can't fail on the numbers (observer's term - hider's skill far above the dice
// range), for active (detect mode) and passive observers, by sight and by hearing. If a 1 on the observer's die
// or a 20 on the hider's always fails, these rates fall short of 100% (by 5% for each rule with a d20, 10% for a
// 1 on a d10). Trials as in stealth_check_rates, idle observers, still hiders.
//   spotact   detect mode, Spot 10 vs Hide -20, hider silenced (3 m)
//   spotpas   no detect mode, the same
//   lstact    detect mode, Listen 10 vs Move Silently -20, Hide +50 (2.5 m)
//   lstpas    no detect mode, the same
#include "pstealth_inc"

const float END = 600.0;

void Group(int a, string g, int bDetect, int bSight)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    int m;
    for (m = 0; m < 24; m++)
    {
        object o = PsPair(g, oArea, m, a * 24 + m, bSight ? 3.0 : 2.5);
        object h = GetLocalObject(o, "H");
        if (bSight)
        {
            PsSkill(o, SKILL_SPOT, 10, g);
            PsSkill(h, SKILL_HIDE, -20, g);
            PsPerm(h, EffectSilence());
        }
        else
        {
            PsSkill(o, SKILL_LISTEN, 10, g);
            PsSkill(h, SKILL_MOVE_SILENTLY, -20, g);
            PsSkill(h, SKILL_HIDE, 51, g);
        }
        PsStealth(o);
        if (bDetect) { SetActionMode(o, ACTION_MODE_DETECT, TRUE); SetLocalInt(o, "DETECT", 1); }
        PsAway(o);
        PsTrials(o, 14.0 + IntToFloat(m) * 0.29, 14.0, END);
    }
}

void main()
{
    PStart();
    Group(0, "spotact", TRUE, TRUE);
    Group(1, "spotpas", FALSE, TRUE);
    Group(2, "lstact", TRUE, FALSE);
    Group(3, "lstpas", FALSE, FALSE);
    DelayCommand(END + 6.0, PsReport("spotact"));
    DelayCommand(END + 6.0, PsReport("spotpas"));
    DelayCommand(END + 6.0, PsReport("lstact"));
    DelayCommand(END + 6.0, PsReport("lstpas"));
    DelayCommand(END + 7.0, PDone());
}
