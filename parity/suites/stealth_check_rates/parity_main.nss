// Spot rates for active and passive, still and moving observers (docs/STEALTH.md, "The Spot check"). Each group is
// 24 pairs, one per 20 m cell of its own area: an observer and a hider in stealth 3 m east of it, silenced so only
// sight counts. Every 14 s the hider comes back from the parking area and the observer has one perception pass to
// spot it (read 4.2 s after the jump). Spot and Hide totals are set exactly (GetSkillRank).
//   idle     observer idle (no detect mode), Spot 10 vs Hide 1, hider still
//   idledet  the same with the observer in detect mode
//   walk     the observer walking a 6 m patrol (no detect mode)
//   walkdet  walking, in detect mode
//   hpace    observer idle, Spot 4 vs Hide 1, the hider walking past it (from 6 m south of the usual spot to 6 m
//            north; the original's walker sets off about 0.6 s after the jump)
#include "pstealth_inc"

const float END = 600.0;

void Group(int a, string g, int nSpot, int nHide, int bDetect, int bWalk, int bPace)
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
        if (bDetect) SetActionMode(o, ACTION_MODE_DETECT, TRUE);
        if (bWalk) PsPace(o, GetLocalLocation(o, "OA"), GetLocalLocation(o, "OB"));
        if (bPace)
        {
            // A 12 m walk past the observer, from 6 m south of the hider's spot to 6 m north (about 7 s).
            vector v = GetPosition(h);
            SetLocalLocation(o, "HOME", Location(oArea, Vector(v.x, v.y - 6.0, 0.0), 0.0));
            SetLocalLocation(o, "WALKTO", Location(oArea, Vector(v.x, v.y + 6.0, 0.0), 0.0));
            SetLocalInt(o, "PACE", 2);
        }
        PsAway(o);
        PsTrials(o, 14.0 + IntToFloat(m) * 0.29, 14.0, END);
    }
}

void main()
{
    PStart();
    Group(0, "idle", 10, 1, FALSE, FALSE, FALSE);
    Group(1, "idledet", 10, 1, TRUE, FALSE, FALSE);
    Group(2, "walk", 10, 1, FALSE, TRUE, FALSE);
    Group(3, "walkdet", 10, 1, TRUE, TRUE, FALSE);
    Group(4, "hpace", 4, 1, FALSE, FALSE, TRUE);
    DelayCommand(END + 6.0, PsReport("idle"));
    DelayCommand(END + 6.0, PsReport("idledet"));
    DelayCommand(END + 6.0, PsReport("walk"));
    DelayCommand(END + 6.0, PsReport("walkdet"));
    DelayCommand(END + 6.0, PsReport("hpace"));
    DelayCommand(END + 7.0, PDone());
}
