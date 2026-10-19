// AoE OnEnter for the effects tests: counts entries; effects made here belong to the AoE creator.
void main()
{
    object oM = GetModule();
    SetLocalInt(oM, "aoe_in", GetLocalInt(oM, "aoe_in") + 1);
    SetLocalString(oM, "aoe_in_last", GetTag(GetEnteringObject()));
    ApplyEffectToObject(DURATION_TYPE_TEMPORARY, EffectACDecrease(1), GetEnteringObject(), 30.0);
}
