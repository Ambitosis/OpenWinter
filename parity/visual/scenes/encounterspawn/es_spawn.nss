#include "es_inc"
void main()
{
    string sLabel = GetLocalString(Mod(), "cur");
    int n = Count(sLabel) + 1;
    SetLocalInt(Mod(), "n_" + sLabel, n);
    SetLocalObject(Mod(), "o_" + sLabel + "_" + IntToString(n), OBJECT_SELF);
    vector v = GetPosition(OBJECT_SELF), c = GetPosition(Enc(sLabel)), p = GetPosition(GetFirstPC());
    Log("spawn|" + sLabel + "|" + IntToString(n) + "|" + GetTag(OBJECT_SELF) + "|enc=" + F1(v.x - c.x) + "," + F1(v.y - c.y)
        + "|pc=" + F1(v.x - p.x) + "," + F1(v.y - p.y) + "|face=" + F1(GetFacing(OBJECT_SELF))
        + "|isenc=" + IntToString(GetIsEncounterCreature()) + "|hostile=" + IntToString(GetStandardFactionReputation(STANDARD_FACTION_HOSTILE, OBJECT_SELF))
        + "|commoner=" + IntToString(GetStandardFactionReputation(STANDARD_FACTION_COMMONER, OBJECT_SELF))
        + "|enemyPC=" + IntToString(GetIsEnemy(GetFirstPC())) + "|" + EncState(Enc(sLabel)));
}
