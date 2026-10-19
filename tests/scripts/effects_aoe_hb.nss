// AoE OnHeartbeat for the effects tests.
void main()
{
    object oM = GetModule();
    SetLocalInt(oM, "aoe_hb", GetLocalInt(oM, "aoe_hb") + 1);
}
