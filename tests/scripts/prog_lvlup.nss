// Module OnPlayerLevelUp for ProgressionTests: records who levelled and their new level.
void main()
{
    object oPC = GetPCLevellingUp();
    object m = GetModule();
    SetLocalInt(m, "lvlup_count", GetLocalInt(m, "lvlup_count") + 1);
    SetLocalObject(m, "lvlup_pc", oPC);
    SetLocalInt(m, "lvlup_level", GetHitDice(oPC));
}
