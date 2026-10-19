// Secret doors are scripted: the stock x0_sectrig_wdr trigger (DC 15 in its KeyName) runs x0_o2_sec_door1 OnEnter,
// whose DetectSecretItem rolls d20 + GetSkillRank(SKILL_SEARCH) > DC and marks the finder FOUND_<tag>. NPCs walk
// into 36 such triggers with Search ranks that never, sometimes and always find it; the rates must agree.
#include "pcombat_inc"

void Walker(int n, int nBoost, string g)
{
    object oArea = GetObjectByTag("PARITY_AREA");
    float x = 10.0 + IntToFloat(n % 6) * 12.0, y = 10.0 + IntToFloat(n / 6) * 12.0;
    object o = CreateObject(OBJECT_TYPE_CREATURE, "nw_bandit001", Location(oArea, Vector(x - 6.0, y, 0.0), 0.0));
    int e; for (e = 5000; e <= 5012; e++) SetEventScript(o, e, "");
    ChangeToStandardFaction(o, STANDARD_FACTION_COMMONER);
    SetAILevel(o, AI_LEVEL_NORMAL);
    if (nBoost > 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillIncrease(SKILL_SEARCH, nBoost), o);
    if (nBoost < 0) ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectSkillDecrease(SKILL_SEARCH, -nBoost), o);
    SetLocalObject(GetModule(), "W" + IntToString(n), o);
    SetLocalString(GetModule(), "G" + IntToString(n), g);
    DelayCommand(2.0, AssignCommand(o, ActionMoveToLocation(Location(oArea, Vector(x, y, 0.0), 0.0))));
    if (n < 3) DelayCommand(1.0, PLogI(g + ".rank", GetSkillRank(SKILL_SEARCH, o)));
}

void Report()
{
    int n;
    for (n = 0; n < 36; n++)
    {
        object o = GetLocalObject(GetModule(), "W" + IntToString(n));
        string g = GetLocalString(GetModule(), "G" + IntToString(n));
        object t = GetObjectByTag("SECTRIG_" + IntToString(n));
        PAdd(g + ".found", GetLocalInt(o, "FOUND_SECTRIG_" + IntToString(n)));
        PAdd(g + ".revealed", GetLocalInt(t, "IS_REVEALED"));
    }
    string g; int i;
    for (i = 0; i < 3; i++)
    {
        g = i == 0 ? "never" : i == 1 ? "half" : "always";
        PLogRate(g + ".found", PGet(g + ".found"), 12);
        PLogI(g + ".revealed", PGet(g + ".revealed"));
    }
    PDone();
}

void main()
{
    int n;
    // The bandit has Search 0: boosts give ranks -10 (needs more than 15: never), 4 (45%) and 19 (always).
    for (n = 0; n < 36; n++)
    {
        int nGroup = n % 3;
        Walker(n, nGroup == 0 ? -10 : nGroup == 1 ? 4 : 19, nGroup == 0 ? "never" : nGroup == 1 ? "half" : "always");
    }
    DelayCommand(15.0, Report());
}
