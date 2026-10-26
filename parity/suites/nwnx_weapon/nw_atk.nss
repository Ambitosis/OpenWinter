// The attack event as a measuring instrument: each group's attack bonus, ordinary-hit damage range and critical
// threat range, read off its attacker's attacks.
#include "pcombat_inc"
#include "nwnx_damage"

void Max(string sKey, int n)
{
    if (!GetLocalInt(GetModule(), sKey + ".set") || n > PGet(sKey)) { SetLocalInt(GetModule(), sKey + ".set", 1); SetLocalInt(GetModule(), sKey, n); }
}

void Min(string sKey, int n)
{
    if (!GetLocalInt(GetModule(), sKey + ".set") || n < PGet(sKey)) { SetLocalInt(GetModule(), sKey + ".set", 1); SetLocalInt(GetModule(), sKey, n); }
}

void main()
{
    struct NWNX_Damage_AttackEventData d = NWNX_Damage_GetAttackEventData();
    string g = GetLocalString(OBJECT_SELF, "G");
    if (GetLocalObject(OBJECT_SELF, "T") != d.oTarget) return;
    PAdd(g + ".ev");
    if (GetLocalInt(OBJECT_SELF, "DIST") == 0)
    {
        SetLocalInt(OBJECT_SELF, "DIST", 1);
        PAdd(g + ".dist", FloatToInt(GetDistanceBetween(OBJECT_SELF, d.oTarget) * 10.0));
        PAdd(g + ".distn");
    }
    // The attack bonus of a round's first attack from the second round on (the least: a target fighting someone
    // else gives a flanking bonus now and then).
    if (PClock() - GetLocalInt(OBJECT_SELF, "START") > 140 && d.iAttackNumber == 1) Min(g + ".mod", d.iToHitModifier);
    if (d.iAttackResult == 1) { Min(g + ".dmin", d.iBase); PAdd(g + ".hits"); }
    if (d.iAttackResult == 3 || d.iAttackResult == 10) PAdd(g + ".crits");
    if (d.iAttackResult == 10) PAdd(g + ".res10");
    SetLocalInt(d.oTarget, "LASTBASE", d.iBase);
    int threat = PGet(g + ".threat");
    if (threat > 0 && d.iAttackResult != 4)
    {
        int crit = d.iAttackResult == 3 || d.iAttackResult == 10;
        if (crit != (d.iToHitRoll >= threat)) PAdd(g + ".critwrong");
        if (d.iToHitRoll >= threat) PAdd(g + ".threats");
    }
}
