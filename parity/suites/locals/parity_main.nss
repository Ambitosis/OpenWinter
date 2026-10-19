// Local variables on every kind of object: types, overwriting, deleting, names and invalid objects.
#include "parity_inc"

string L(location l)
{
    vector v = GetPositionFromLocation(l);
    return GetTag(GetAreaFromLocation(l)) + "@" + FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2) + "/" + FloatToString(GetFacingFromLocation(l), 0, 2);
}

void Probe(string k, object o)
{
    PLogI(k + ".valid", GetIsObjectValid(o));
    object oArea = GetObjectByTag("PARITY_AREA");
    location l = Location(oArea, Vector(1.0, 2.0, 3.0), 45.0);
    SetLocalInt(o, "v", 7);
    SetLocalFloat(o, "v", 1.5);
    SetLocalString(o, "v", "str");
    SetLocalObject(o, "v", oArea);
    SetLocalLocation(o, "v", l);
    SetLocalJson(o, "v", JsonParse("{\"a\":1}"));
    PLogI(k + ".int", GetLocalInt(o, "v"));
    PLogF(k + ".float", GetLocalFloat(o, "v"));
    PLog(k + ".string", GetLocalString(o, "v"));
    PLog(k + ".object", GetTag(GetLocalObject(o, "v")));
    PLog(k + ".location", L(GetLocalLocation(o, "v")));
    PLog(k + ".json", JsonDump(GetLocalJson(o, "v")));
    // Overwrite, then delete one type only.
    SetLocalInt(o, "v", -3);
    SetLocalString(o, "v", "");
    PLogI(k + ".int.overwrite", GetLocalInt(o, "v"));
    PLog(k + ".string.empty", "[" + GetLocalString(o, "v") + "]");
    DeleteLocalInt(o, "v");
    PLogI(k + ".int.deleted", GetLocalInt(o, "v"));
    PLogF(k + ".float.afterintdelete", GetLocalFloat(o, "v"));
    DeleteLocalFloat(o, "v");
    DeleteLocalString(o, "v");
    DeleteLocalObject(o, "v");
    DeleteLocalLocation(o, "v");
    DeleteLocalJson(o, "v");
    PLogF(k + ".float.deleted", GetLocalFloat(o, "v"));
    PLog(k + ".string.deleted", "[" + GetLocalString(o, "v") + "]");
    PLogI(k + ".object.deleted", GetIsObjectValid(GetLocalObject(o, "v")));
    PLog(k + ".location.deleted", L(GetLocalLocation(o, "v")));
    PLog(k + ".json.deleted", JsonDump(GetLocalJson(o, "v")));
    PLogI(k + ".json.deleted.type", JsonGetType(GetLocalJson(o, "v")));
    // Names: case, empty, long, odd characters.
    SetLocalInt(o, "Case", 1);
    SetLocalInt(o, "case", 2);
    PLogI(k + ".name.Case", GetLocalInt(o, "Case"));
    PLogI(k + ".name.case", GetLocalInt(o, "case"));
    PLogI(k + ".name.CASE", GetLocalInt(o, "CASE"));
    SetLocalInt(o, "", 9);
    PLogI(k + ".name.empty", GetLocalInt(o, ""));
    SetLocalInt(o, " sp ", 10);
    PLogI(k + ".name.spaces", GetLocalInt(o, " sp ") * 100 + GetLocalInt(o, "sp"));
    string longname = "n";
    int i;
    for (i = 0; i < 10; i++) longname += longname;
    SetLocalInt(o, longname, 11);
    PLogI(k + ".name.long", GetLocalInt(o, longname));
    PLogI(k + ".name.long.prefix", GetLocalInt(o, GetStringLeft(longname, 64)));
    SetLocalFloat(o, "f", -0.0);
    PLog(k + ".float.negzero", FloatToString(GetLocalFloat(o, "f"), 0, 2));
}

void Stale()
{
    object m = GetModule();
    object w = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(), FALSE, "stale");
    SetLocalObject(m, "stale", w);
    SetLocalString(m, "staleid", ObjectToString(w));
    SetLocalInt(w, "x", 5);
    DestroyObject(w);
    PLogI("stale.before.valid", GetIsObjectValid(GetLocalObject(m, "stale")));
    PLogI("stale.before.int", GetLocalInt(w, "x"));
}

void StaleAfter()
{
    object m = GetModule();
    object o = GetLocalObject(m, "stale");
    PLogI("stale.after.valid", GetIsObjectValid(o));
    PLogI("stale.after.sameid", ObjectToString(o) == GetLocalString(m, "staleid"));
    PLogI("stale.after.isinvalid", o == OBJECT_INVALID);
    PLogI("stale.after.int", GetLocalInt(StringToObject(GetLocalString(m, "staleid")), "x"));
}

void Json()
{
    object m = GetModule();
    json j = JsonParse("{\"a\":[1,2]}");
    SetLocalJson(m, "j", j);
    // In-place edits of the value read back, and of the original after storing.
    json r = GetLocalJson(m, "j");
    JsonObjectSetInplace(r, "b", JsonInt(3));
    JsonObjectSetInplace(j, "c", JsonInt(4));
    PLog("json.inplace", JsonDump(GetLocalJson(m, "j")));
    SetLocalJson(m, "n", JsonNull());
    PLog("json.null", JsonDump(GetLocalJson(m, "n")));
    PLogI("json.null.type", JsonGetType(GetLocalJson(m, "n")));
    PLogI("json.missing.type", JsonGetType(GetLocalJson(m, "nothere")));
    PLog("json.missing.error", JsonGetError(GetLocalJson(m, "nothere")));
}

void Main()
{
    object oArea = GetObjectByTag("PARITY_AREA");
    Probe("module", GetModule());
    Probe("area", oArea);
    Probe("creature", CreateObject(OBJECT_TYPE_CREATURE, "nw_dog", PCentre(3.0, 0.0)));
    Probe("placeable", CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-3.0, 0.0)));
    Probe("item", CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", PCentre(0.0, 3.0)));
    Probe("waypoint", CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(0.0, -3.0)));
    Probe("store", CreateObject(OBJECT_TYPE_STORE, "nw_storebar01", PCentre(5.0, 5.0)));
    Probe("trigger", CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, PCentre(-6.0, -6.0), 2.0, "trap"));
    Probe("invalid", OBJECT_INVALID);
    Probe("inventoryitem", CreateItemOnObject("nw_wswls001", GetObjectByTag("trap") == OBJECT_INVALID ? GetModule() : CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-5.0, 5.0))));
    Stale();
    DelayCommand(0.2, StaleAfter());
    Json();
}

void main()
{
    DelayCommand(1.0, Main());
    DelayCommand(2.0, PDone());
}
