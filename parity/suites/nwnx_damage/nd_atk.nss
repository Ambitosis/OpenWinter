// NWNX_Damage attack event: tallies what each group's attacker sees and changes it for some groups.
#include "pcombat_inc"
#include "nwnx_damage"

void Max(string sKey, int n)
{
    if (!GetLocalInt(GetModule(), sKey + ".set") || n > PGet(sKey))
    {
        SetLocalInt(GetModule(), sKey + ".set", 1);
        SetLocalInt(GetModule(), sKey, n);
    }
}

void main()
{
    struct NWNX_Damage_AttackEventData d = NWNX_Damage_GetAttackEventData();
    string g = GetLocalString(OBJECT_SELF, "G");
    object t = d.oTarget;
    // Only the pair's attacker: a target fighting back is counted apart.
    if (GetLocalObject(OBJECT_SELF, "T") != t) { PAdd(g + ".backev"); return; }
    string n = IntToString(d.iAttackNumber);
    PAdd(g + ".ev");
    PAdd(g + ".res" + IntToString(d.iAttackResult));
    PAdd(g + ".wat" + IntToString(d.iWeaponAttackType));
    PAdd(g + ".wat" + IntToString(d.iWeaponAttackType) + ".n" + n);
    PAdd(g + ".sneak" + IntToString(d.iSneakAttack));
    PAdd(g + ".kill" + IntToString(d.bKillingBlow));
    PAdd(g + ".rng" + IntToString(d.bRangedAttack));
    PAdd(g + ".type" + IntToString(d.iAttackType));
    if (d.iToHitRoll == 20) PAdd(g + ".r20.res" + IntToString(d.iAttackResult));
    // The full attack bonus of each attack number shows from the second round on.
    if (PClock() - GetLocalInt(OBJECT_SELF, "START") > 140) Max(g + ".mod" + n, d.iToHitModifier);
    int bHit = d.iAttackResult == 1 || d.iAttackResult == 3 || d.iAttackResult == 7 || d.iAttackResult == 10;
    if (bHit && d.iFire != -1) PAdd(g + ".hitfire" + IntToString(d.iFire));
    if (bHit && d.iBase < 0) PAdd(g + ".hitnobase");
    if (!bHit && d.iBase != -1) PAdd(g + ".missbase");

    if (g == "miss2hit") { d.iAttackResult = 1; d.iBase = 5; NWNX_Damage_SetAttackEventData(d); }
    if (g == "hit2miss") { d.iAttackResult = 4; NWNX_Damage_SetAttackEventData(d); }
    if (g == "atkmod" && bHit)
    {
        d.iSlash = 0; d.iPierce = 0; d.iBludgeoning = 0; d.iBase = 0; d.iFire = 7;
        NWNX_Damage_SetAttackEventData(d);
    }
    // What the damage event should see for this blow (nat20's attacks come too close together to tell).
    if (g == "nat20") return;
    SetLocalInt(t, "LASTBASE", d.iBase);
    SetLocalInt(t, "LASTFIRE", d.iFire);
}
