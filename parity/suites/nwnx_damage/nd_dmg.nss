// NWNX_Damage damage event: checks weapon blows against their attack event, logs scripted damage, changes some.
#include "pcombat_inc"
#include "nwnx_damage"

string Data(struct NWNX_Damage_DamageEventData d)
{
    return "b" + IntToString(d.iBludgeoning) + " p" + IntToString(d.iPierce) + " s" + IntToString(d.iSlash)
        + " m" + IntToString(d.iMagical) + " a" + IntToString(d.iAcid) + " c" + IntToString(d.iCold)
        + " f" + IntToString(d.iFire) + " base" + IntToString(d.iBase) + " c1" + IntToString(d.iCustom1)
        + " spell" + IntToString(d.iSpellId) + " by" + GetTag(d.oDamager);
}

void main()
{
    struct NWNX_Damage_DamageEventData d = NWNX_Damage_GetDamageEventData();
    object o = OBJECT_SELF;
    string g = GetLocalString(o, "G");
    if (g == "S8") { PLogI("S8.spell", d.iSpellId); return; }
    if (GetStringLeft(g, 1) == "S")
    {
        int n = GetLocalInt(o, "DEVN") + 1;
        SetLocalInt(o, "DEVN", n);
        PLog(g + ".dev" + IntToString(n), Data(d));
        if (g == "S5") { d.iFire = 20; NWNX_Damage_SetDamageEventData(d); }
        return;
    }
    // A pair's target hit by its attacker.
    if (d.oDamager != GetLocalObject(o, "A")) return;
    PAdd(g + ".dev");
    if (g != "nat20" && d.iBase != GetLocalInt(o, "LASTBASE")) PAdd(g + ".basediff");
    if (g != "nat20" && d.iFire != GetLocalInt(o, "LASTFIRE")) PAdd(g + ".firediff");
    if (d.iSpellId != -1) PAdd(g + ".spell");
    if (g == "dmgmod") { d.iFire = 5; d.iBase = 1; d.iSlash = -1; NWNX_Damage_SetDamageEventData(d); }
}
