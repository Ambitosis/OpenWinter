// Campaign database round trips. The test sets module locals "area", "pc" and "item".
void main()
{
    object oMod = GetModule();
    object oArea = GetLocalObject(oMod, "area");
    object oPC = GetLocalObject(oMod, "pc");
    object oItem = GetLocalObject(oMod, "item");
    string db = "TestCampaign";

    SetCampaignInt(db, "i", 42);
    SetCampaignFloat(db, "f", 1.5);
    SetCampaignString(db, "s", "hello world");
    SetCampaignVector(db, "v", Vector(1.0, 2.5, -3.0));
    SetCampaignLocation(db, "l", Location(oArea, Vector(4.0, 5.0, 6.0), 90.0));
    SetCampaignJson(db, "j", JsonParse("{\"a\":[1,2]}"));
    SetCampaignInt(db, "pi", 7, oPC);
    SetCampaignInt(db, "gone", 1);
    DeleteCampaignVariable(db, "gone");
    SetCampaignInt(db, "over", 1);
    SetCampaignString(db, "over", "replaced");

    // Campaign names are case-insensitive.
    SetLocalInt(oMod, "i", GetCampaignInt("testcampaign", "i"));
    SetLocalFloat(oMod, "f", GetCampaignFloat(db, "f"));
    SetLocalString(oMod, "s", GetCampaignString(db, "s"));
    vector v = GetCampaignVector(db, "v");
    SetLocalFloat(oMod, "vx", v.x);
    SetLocalFloat(oMod, "vy", v.y);
    SetLocalFloat(oMod, "vz", v.z);
    location l = GetCampaignLocation(db, "l");
    SetLocalInt(oMod, "larea", GetAreaFromLocation(l) == oArea);
    SetLocalFloat(oMod, "lx", GetPositionFromLocation(l).x);
    SetLocalFloat(oMod, "lz", GetPositionFromLocation(l).z);
    SetLocalFloat(oMod, "lfacing", GetFacingFromLocation(l));
    SetLocalString(oMod, "j", JsonDump(GetCampaignJson(db, "j")));
    SetLocalInt(oMod, "pi", GetCampaignInt(db, "pi", oPC));
    SetLocalInt(oMod, "pi_global", GetCampaignInt(db, "pi"));
    SetLocalInt(oMod, "gone", GetCampaignInt(db, "gone"));
    SetLocalInt(oMod, "mismatch", GetCampaignInt(db, "s"));
    SetLocalInt(oMod, "over_int", GetCampaignInt(db, "over"));
    SetLocalString(oMod, "over", GetCampaignString(db, "over"));
    SetLocalInt(oMod, "missing_db", GetCampaignInt("nosuchcampaign", "i"));

    // Objects: stored serialized, retrieved as a new object into the owner's inventory.
    SetLocalInt(oMod, "stored", StoreCampaignObject(db, "obj", oItem));
    object oCopy = RetrieveCampaignObject(db, "obj", Location(oArea, Vector(1.0, 1.0, 0.0), 0.0), oPC);
    SetLocalObject(oMod, "copy", oCopy);
    SetLocalString(oMod, "copy_tag", GetTag(oCopy));
    object oGround = RetrieveCampaignObject(db, "obj", Location(oArea, Vector(2.0, 3.0, 0.0), 0.0));
    SetLocalObject(oMod, "ground", oGround);
    SetLocalInt(oMod, "store_invalid", StoreCampaignObject(db, "bad", oMod));
    SetLocalInt(oMod, "retrieve_missing", GetIsObjectValid(RetrieveCampaignObject(db, "nothing", Location(oArea, Vector(1.0, 1.0, 0.0), 0.0))));
}
