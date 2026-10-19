// AoE OnExit for the effects tests.
void main()
{
    object oM = GetModule();
    SetLocalInt(oM, "aoe_out", GetLocalInt(oM, "aoe_out") + 1);
    SetLocalString(oM, "aoe_out_last", GetTag(GetExitingObject()));
}
