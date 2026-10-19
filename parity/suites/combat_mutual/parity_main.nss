// Two creatures attacking each other: their rounds get longer than a lone attacker's (5 / 5.5 seconds),
// about 0.15 s for one attack each, 0.4 s against a foe with one attack, a second against more. The
// exact length varies from pair to pair on nwserver (it depends on how their swings interleave), so
// these are averages over pairs with wide tolerances. Pairs of nw_drowfight010.
#include "pcombat_inc"

void Mutual(string g, int na, int nt)
{
    object a = PPair(g + ".a", "nw_drowfight010", "nw_drowfight010");
    object t = GetLocalObject(a, "T");
    SetLocalString(t, "G", g + ".t");
    SetLocalObject(t, "T", a);
    SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_MELEE_ATTACKED, "pc_atk");
    SetEventScript(a, EVENT_SCRIPT_CREATURE_ON_DAMAGED, "pc_dmg");
    SetEventScript(t, EVENT_SCRIPT_CREATURE_ON_END_COMBATROUND, "pc_eor");
    SetImmortal(t, TRUE);
    ChangeToStandardFaction(t, STANDARD_FACTION_DEFENDER);
    SetBaseAttackBonus(na, a);
    SetBaseAttackBonus(nt, t);
}

void Setup()
{
    int i;
    for (i = 0; i < 4; i++)
    {
        Mutual("m11", 1, 1);
        Mutual("m21", 2, 1);
        Mutual("m22", 2, 2);
        Mutual("m31", 3, 1);
        Mutual("m42", 4, 2);
    }
    PStartAll(1.0);
}

void Report()
{
    string s = "m11.a m11.t m21.a m21.t m22.a m22.t m31.a m31.t m42.a m42.t ";
    while (s != "")
    {
        int sp = FindSubString(s, " ");
        string g = GetStringLeft(s, sp);
        PLogMean(g + ".apr", IntToFloat(PGet(g + ".ratk")), PGet(g + ".rounds"), 0.05, 0.05);
        PLogMean(g + ".round", IntToFloat(PGet(g + ".rlen")) / 20.0, PGet(g + ".rounds"), 0.4, 0.3);
        s = GetSubString(s, sp + 1, 1000);
    }
    PDone();
}

void main()
{
    PClockStart(85.0);
    DelayCommand(1.0, Setup());
    DelayCommand(72.0, Report());
}
