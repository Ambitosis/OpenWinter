#include "er_inc"
void Later(string sLabel, int n)
{
    vector v = GetPosition(OBJECT_SELF), c = ErSpot(sLabel);
    Log("settled|" + sLabel + "|" + IntToString(n) + "|" + GetTag(OBJECT_SELF) + "|spot=" + F1(v.x - c.x) + "," + F1(v.y - c.y)
        + "|face=" + F1(GetFacing(OBJECT_SELF)));
}
void main()
{
    string sLabel = GetLocalString(Mod(), "cur");
    int n = Count(sLabel) + 1;
    SetLocalInt(Mod(), "n_" + sLabel, n);
    SetLocalObject(Mod(), "o_" + sLabel + "_" + IntToString(n), OBJECT_SELF);
    vector v = GetPosition(OBJECT_SELF), c = ErSpot(sLabel), p = GetPosition(GetFirstPC());
    Log("spawn|" + sLabel + "|" + IntToString(n) + "|" + GetTag(OBJECT_SELF) + "|spot=" + F1(v.x - c.x) + "," + F1(v.y - c.y)
        + "|pc=" + F1(v.x - p.x) + "," + F1(v.y - p.y) + "|face=" + F1(GetFacing(OBJECT_SELF)) + "|isenc=" + IntToString(GetIsEncounterCreature())
        + "|sameHostile=" + IntToString(GetFactionEqual(OBJECT_SELF, GetLocalObject(Mod(), "ref_hostile")))
        + "|sameCommoner=" + IntToString(GetFactionEqual(OBJECT_SELF, GetLocalObject(Mod(), "ref_commoner")))
        + "|enemyPC=" + IntToString(GetIsEnemy(GetFirstPC())) + "|ai=" + IntToString(GetAILevel()) + "|" + EncState(Enc(sLabel)));
    if (sLabel == "face" || sLabel == "nosp2" || n <= 2) DelayCommand(1.0, Later(sLabel, n));
}
