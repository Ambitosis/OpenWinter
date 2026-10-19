// DestroyCampaignDatabase removes every variable.
void main()
{
    object oMod = GetModule();
    DestroyCampaignDatabase("TestCampaign");
    SetLocalInt(oMod, "after_destroy", GetCampaignInt("TestCampaign", "i"));
    SetLocalString(oMod, "after_destroy_s", GetCampaignString("TestCampaign", "s"));
}
