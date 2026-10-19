// Listen against distance, and Spot in combat (docs/STEALTH.md). Trials as in stealth_check_rates, idle observers
// without detect mode, still hiders in stealth.
// Area 0: 16 pairs 40 m apart, observers with medium perception (20 m), hiders with Hide +50 (never seen) and
//   Move Silently -3 against Listen 10, at 2.5, 8.5, 14.5 or 17.5 m (four pairs each): lst2, lst8, lst14, lst17.
// Area 1: "calm": Spot 10 vs Hide -5, hiders silenced, 3 m (24 pairs).
// Area 2: "fight": the same, but each observer is immortal and attacked by a creature 1.5 m west of it.
#include "pstealth_inc"

const float END = 600.0;

void Listeners()
{
    object oArea = GetObjectByTag("SN_A0");
    int m;
    for (m = 0; m < 16; m++)
    {
        int k = m % 4;
        float fDist = k == 0 ? 2.5 : k == 1 ? 8.5 : k == 2 ? 14.5 : 17.5;
        string g = k == 0 ? "lst2" : k == 1 ? "lst8" : k == 2 ? "lst14" : "lst17";
        float x = 20.0 + IntToFloat(m % 4) * 40.0, y = 20.0 + IntToFloat(m / 4) * 40.0;
        object o = PsMake("sn_obs9", oArea, x - fDist / 2.0, y, STANDARD_FACTION_COMMONER);
        object h = PsMake("sn_tgt", oArea, x + fDist / 2.0, y, STANDARD_FACTION_HOSTILE, 180.0);
        SetLocalObject(o, "H", h);
        SetLocalString(o, "G", g);
        SetLocalObject(GetModule(), g + ".O" + IntToString(m / 4), o);
        SetLocalLocation(o, "HOME", GetLocation(h));
        SetLocalLocation(o, "PARK", PsParkSpot(m));
        PsSkill(o, SKILL_LISTEN, 10, g);
        PsSkill(h, SKILL_HIDE, 51, g);
        PsSkill(h, SKILL_MOVE_SILENTLY, -3, g);
        PsStealth(o);
        PsAway(o);
        PsTrials(o, 14.0 + IntToFloat(m) * 0.29, 14.0, END);
    }
}

void Group(int a, string g, int bFight)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    int m;
    for (m = 0; m < 24; m++)
    {
        object o = PsPair(g, oArea, m, 16 + (a - 1) * 24 + m, 3.0);
        object h = GetLocalObject(o, "H");
        PsSkill(o, SKILL_SPOT, 10, g);
        PsSkill(h, SKILL_HIDE, -5, g);
        PsPerm(h, EffectSilence());
        PsStealth(o);
        if (bFight)
        {
            vector v = GetPosition(o);
            object d = PsMake("sn_tgt", oArea, v.x - 1.5, v.y, STANDARD_FACTION_HOSTILE);
            SetImmortal(o, TRUE);
            AssignCommand(d, ActionAttack(o));
        }
        PsAway(o);
        PsTrials(o, 14.0 + IntToFloat(m) * 0.29, 14.0, END);
    }
}

void main()
{
    PStart();
    Listeners();
    Group(1, "calm", FALSE);
    Group(2, "fight", TRUE);
    DelayCommand(END + 6.0, PsReport("lst2"));
    DelayCommand(END + 6.0, PsReport("lst8"));
    DelayCommand(END + 6.0, PsReport("lst14"));
    DelayCommand(END + 6.0, PsReport("lst17"));
    DelayCommand(END + 6.0, PsReport("calm"));
    DelayCommand(END + 6.0, PsReport("fight"));
    DelayCommand(END + 7.0, PDone());
}
