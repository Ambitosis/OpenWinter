// NWNX_Util: module and clock getters, last created objects, script helpers, resource files, doors and strings.
#include "parity_inc"
#include "nwnx_util"
#include "nwnx_consts"

int StartsWith(string s, string p) { return GetStringLeft(s, GetStringLength(p)) == p; }

void main()
{
    object oMod = GetModule();
    PLog("module.file", NWNX_Util_GetModuleFile());
    PLog("module.tlk", NWNX_Util_GetModuleTlkFile());

    PLog("token.unset", NWNX_Util_GetCustomToken(1234));
    SetCustomToken(1234, "hello");
    PLog("token.set", NWNX_Util_GetCustomToken(1234));
    SetCustomToken(1234, "");
    PLog("token.cleared", NWNX_Util_GetCustomToken(1234));
    SetCustomToken(0, "zero");
    PLog("token.zero", NWNX_Util_GetCustomToken(0));
    PLog("token.neg", NWNX_Util_GetCustomToken(-1));

    PLogI("dawn.default", NWNX_Util_GetDawnHour());
    PLogI("dusk.default", NWNX_Util_GetDuskHour());
    NWNX_Util_SetDawnHour(5);
    NWNX_Util_SetDuskHour(19);
    PLogI("dawn.set", NWNX_Util_GetDawnHour());
    PLogI("dusk.set", NWNX_Util_GetDuskHour());
    NWNX_Util_SetDawnHour(30);
    PLogI("dawn.30", NWNX_Util_GetDawnHour());
    NWNX_Util_SetDuskHour(-2);
    PLogI("dusk.neg", NWNX_Util_GetDuskHour());
    NWNX_Util_SetDawnHour(23);
    PLogI("dawn.23", NWNX_Util_GetDawnHour());
    NWNX_Util_SetDawnHour(24);
    PLogI("dawn.24", NWNX_Util_GetDawnHour());
    NWNX_Util_SetDuskHour(0);
    PLogI("dusk.0", NWNX_Util_GetDuskHour());
    NWNX_Util_SetDawnHour(6);
    NWNX_Util_SetDuskHour(18);

    location lC = PCentre();
    object oP = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chair1", lC);
    object oW = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", lC);
    object oC1 = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", lC);
    object oI = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mpotion001", lC);
    object oC2 = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblinb", lC);
    PLogI("last.creature1", NWNX_Util_GetLastCreatedObject(5) == oC2);
    PLogI("last.creature2", NWNX_Util_GetLastCreatedObject(5, 2) == oC1);
    PLogI("last.creature3", NWNX_Util_GetLastCreatedObject(5, 3) == OBJECT_INVALID);
    PLogI("last.item", NWNX_Util_GetLastCreatedObject(6) == oI);
    object oLI = NWNX_Util_GetLastCreatedObject(6);
    PLog("last.item.resref", GetResRef(oLI));
    PLogI("last.item.held", GetItemPossessor(oLI) == oC2);
    int k;
    for (k = 1; k <= 6; k++)
    {
        object oK = NWNX_Util_GetLastCreatedObject(0, k);
        PLog("last.all." + IntToString(k), IntToString(GetObjectType(oK)) + ":" + GetResRef(oK) + ":" + IntToString(GetItemPossessor(oK) == oC2));
        oK = NWNX_Util_GetLastCreatedObject(6, k);
        PLog("last.items." + IntToString(k), GetResRef(oK) + ":" + IntToString(GetItemPossessor(oK) == oC2) + ":" + IntToString(GetItemPossessor(oK) == oC1));
    }
    PLogI("last.placeable", NWNX_Util_GetLastCreatedObject(9) == oP);
    PLogI("last.waypoint", NWNX_Util_GetLastCreatedObject(12) == oW);
    PLogI("last.all", NWNX_Util_GetLastCreatedObject(0) == oC2);
    PLogI("last.all2", NWNX_Util_GetLastCreatedObject(0, 2) == oI);
    PLogI("last.zero", NWNX_Util_GetLastCreatedObject(5, 0) == oC2);
    DestroyObject(oC2);

    PLogI("lev.same", NWNX_Util_GetStringLevenshteinDistance("kitten", "kitten"));
    PLogI("lev.kitten", NWNX_Util_GetStringLevenshteinDistance("kitten", "sitting"));
    PLogI("lev.empty", NWNX_Util_GetStringLevenshteinDistance("", "abc"));
    PLogI("lev.case", NWNX_Util_GetStringLevenshteinDistance("ABC", "abc"));
    PLogI("lev.swap", NWNX_Util_GetStringLevenshteinDistance("ab", "ba"));

    NWNX_Util_SetItemActivator(oC1);
    PLogI("activator", GetItemActivator() == oC1);

    PLogI("event.before", GetCurrentlyRunningEvent());
    NWNX_Util_SetCurrentlyRunningEvent(EVENT_SCRIPT_MODULE_ON_HEARTBEAT);
    PLogI("event.set", GetCurrentlyRunningEvent());

    PLogI("instr.before.ok", NWNX_Util_GetInstructionsExecuted() > 0);
    NWNX_Util_SetInstructionsExecuted(0);
    int n = NWNX_Util_GetInstructionsExecuted();
    PLogI("instr.reset.small", n >= 0 && n < 50);
    NWNX_Util_SetInstructionsExecuted(-5);
    n = NWNX_Util_GetInstructionsExecuted();
    PLogI("instr.neg.small", n >= 0 && n < 50);

    PLog("script.add", NWNX_Util_AddScript("pw_added", "WriteTimestampedLogEntry(\"PARITY|script.ran|1\");", TRUE));
    ExecuteScript("pw_added", oMod);
    PLog("script.bad", NWNX_Util_AddScript("pw_bad", "void main() { int x = ; }") != "" ? "error" : "none");
    PLog("script.cond", NWNX_Util_AddScript("pw_cond", "int StartingConditional() { return 7; }"));
    ExecuteScript("pw_cond", oMod);
    PLogI("script.return", NWNX_Util_GetScriptReturnValue());
    PLog("script.exists", ResManGetAliasFor("pw_added", NWNX_UTIL_RESREF_TYPE_NCS));
    PLogI("nss.add", NWNX_Util_AddNSSFile("pw_nss", "void main() {}"));
    PLog("nss.exists", ResManGetAliasFor("pw_nss", NWNX_UTIL_RESREF_TYPE_NSS));
    PLogI("nss.remove", NWNX_Util_RemoveNWNXResourceFile("pw_nss", NWNX_UTIL_RESREF_TYPE_NSS));
    PLog("nss.gone", ResManGetAliasFor("pw_nss", NWNX_UTIL_RESREF_TYPE_NSS));
    PLogI("nss.remove.again", NWNX_Util_RemoveNWNXResourceFile("pw_nss", NWNX_UTIL_RESREF_TYPE_NSS));
    PLogI("nss.badalias", NWNX_Util_AddNSSFile("pw_nss2", "void main() {}", "NOSUCHALIAS"));
    PLogI("clean.ncs", NWNX_Util_CleanResourceDirectory("NWNX", NWNX_UTIL_RESREF_TYPE_NCS));
    PLog("clean.gone", ResManGetAliasFor("pw_added", NWNX_UTIL_RESREF_TYPE_NCS));
    PLogI("clean.badalias", NWNX_Util_CleanResourceDirectory("NOSUCHALIAS"));
    PLogI("update.nwnx", NWNX_Util_UpdateResourceDirectory("NWNX"));
    PLogI("update.bad", NWNX_Util_UpdateResourceDirectory("NOSUCHALIAS"));

    location lDoor = PCentre(4.0, 0.0);
    object oD = NWNX_Util_CreateDoor("nw_door_metal", lDoor, "PW_DOOR");
    PLogI("door.valid", GetIsObjectValid(oD));
    PLogI("door.type", GetObjectType(oD));
    PLog("door.tag", GetTag(oD));
    PLog("door.resref", GetResRef(oD));
    PLogI("door.isopen", GetIsOpen(oD));
    PLogI("door.area", GetArea(oD) == GetArea(oC1));
    PLogF("door.x", GetPosition(oD).x);
    PLogI("door.last", NWNX_Util_GetLastCreatedObject(10) == oD);
    PLogI("door.bad", GetIsObjectValid(NWNX_Util_CreateDoor("no_such_door", lDoor)));
    object oD2 = NWNX_Util_CreateDoor("nw_door_metal", lDoor, "", 2);
    PLog("door2.tag", GetTag(oD2));

    location lStart = GetStartingLocation();
    PLogI("start.set", NWNX_Util_SetStartingLocation(GetResRef(GetArea(oC1)), PCentre(3.0, 4.0), Vector(0.0, 1.0, 0.0)));
    vector vS = GetPositionFromLocation(GetStartingLocation());
    PLogF("start.x", vS.x);
    PLogF("start.y", vS.y);
    PLogF("start.facing", GetFacingFromLocation(GetStartingLocation()));
    PLogI("start.bad", NWNX_Util_SetStartingLocation("no_such_area", PCentre(), Vector(1.0, 0.0, 0.0)));

    effect e = EffectDamageIncrease(3, DAMAGE_TYPE_FIRE);
    itemproperty ip = NWNX_Util_EffectToItemProperty(e);
    PLogI("cast.ip.valid", GetIsItemPropertyValid(ip));
    effect e2 = NWNX_Util_ItemPropertyToEffect(ItemPropertyLight(IP_CONST_LIGHTBRIGHTNESS_BRIGHT, IP_CONST_LIGHTCOLOR_RED));
    PLogI("cast.effect.type", GetEffectType(e2, TRUE));
    PLogI("cast.effect.valid", GetIsEffectValid(e2));

    NWNX_Util_UpdateClientObject(oC1);
    NWNX_Util_RawPrint("PARITY|raw.print|1");
    PDone();
}
