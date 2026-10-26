// NWNX_Object_Serialize / Deserialize: what the string holds and what a deserialized object is.
#include "parity_inc"
#include "nwnx_object"
#include "pserial_probe"

void PRaw(string sKey, string s)
{
    int i = 0;
    while (s != "")
    {
        WriteTimestampedLogEntry("RAW|" + sKey + "." + IntToString(i) + "|" + GetStringLeft(s, 800));
        s = GetSubString(s, 800, 1000000);
        i++;
    }
}

void Describe(string k, object o)
{
    PLogI(k + ".valid", GetIsObjectValid(o));
    PLogI(k + ".type", GetObjectType(o));
    PLog(k + ".tag", GetTag(o));
    PLog(k + ".resref", GetResRef(o));
    PLog(k + ".name", GetName(o));
    // The original leaves a deserialized creature its old area (but at the origin, in no area's object list).
    if (GetObjectType(o) != OBJECT_TYPE_CREATURE || k != "gobcopy") PLogI(k + ".areavalid", GetIsObjectValid(GetArea(o)));
    else WriteTimestampedLogEntry("RAW|" + k + ".areavalid|" + IntToString(GetIsObjectValid(GetArea(o))));
    PLogI(k + ".possessor", GetIsObjectValid(GetItemPossessor(o)));
    PLogI(k + ".local", GetLocalInt(o, "probe"));
    PLog(k + ".localstr", GetLocalString(o, "probes"));
    PLogI(k + ".pos", NWNX_Object_GetInt(o, "posvar"));
    PLogI(k + ".locals", NWNX_Object_GetLocalVariableCount(o));
    vector v = GetPosition(o);
    PLog(k + ".position", FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2));
    PLogI(k + ".stack", GetItemStackSize(o));
    if (GetObjectType(o) == OBJECT_TYPE_CREATURE || GetObjectType(o) == OBJECT_TYPE_PLACEABLE) PLogI(k + ".hp", GetCurrentHitPoints(o));
    int n = 0;
    object it = GetFirstItemInInventory(o);
    while (GetIsObjectValid(it)) { n++; it = GetNextItemInInventory(o); }
    if (k != "gobjumped") PLogI(k + ".items", n); // the spawn script may add random treasure
    int e = 0;
    effect fx = GetFirstEffect(o);
    while (GetIsEffectValid(fx)) { e++; fx = GetNextEffect(o); }
    if (GetObjectType(o) != OBJECT_TYPE_ITEM) PLogI(k + ".effects", e);
}

void main()
{
    object oItem = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mpotion001", PCentre(), FALSE, "SERIAL_ITEM");
    SetLocalInt(oItem, "probe", 7);
    SetLocalString(oItem, "probes", "seven");
    NWNX_Object_SetInt(oItem, "posvar", 11, TRUE);
    SetItemStackSize(oItem, 3);
    string sItem = NWNX_Object_Serialize(oItem);
    PLogI("item.len", GetStringLength(sItem) > 0);
    PRaw("item", sItem);
    object oCopy = NWNX_Object_Deserialize(sItem);
    Describe("itemcopy", oCopy);
    PLogI("itemcopy.same", oCopy == oItem);

    object oC = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(2.0), FALSE, "SERIAL_GOB");
    SetLocalInt(oC, "probe", 9);
    ApplyEffectToObject(DURATION_TYPE_PERMANENT, EffectACIncrease(2), oC);
    object oGive = CreateItemOnObject("nw_it_mpotion002", oC);
    string sC = NWNX_Object_Serialize(oC);
    PLogI("gob.len", GetStringLength(sC) > 0);
    PRaw("gob", sC);
    object oGob = NWNX_Object_Deserialize(sC);
    Describe("gobcopy", oGob);
    AssignCommand(oGob, ActionJumpToLocation(PCentre(4.0)));
    DelayCommand(1.0, Describe("gobjumped", oGob));

    object oP = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(-2.0), FALSE, "SERIAL_CHEST");
    CreateItemOnObject("nw_it_mpotion003", oP);
    SetLocalInt(oP, "probe", 3);
    string sP = NWNX_Object_Serialize(oP);
    PLogI("chest.len", GetStringLength(sP) > 0);
    PRaw("chest", sP);
    object oPc = NWNX_Object_Deserialize(sP);
    Describe("chestcopy", oPc);

    object oW = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(), FALSE, "SERIAL_WP");
    string sW = NWNX_Object_Serialize(oW);
    PLogI("wp.len", GetStringLength(sW));
    PRaw("wp", sW);
    PLogI("wpcopy.valid", GetIsObjectValid(NWNX_Object_Deserialize(sW)));

    object oV = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(), FALSE, "SERIAL_POS");
    NWNX_Object_SetInt(oV, "pi", 5, TRUE);
    NWNX_Object_SetInt(oV, "ti", 6, FALSE);
    NWNX_Object_SetString(oV, "ps", "a b;c", TRUE);
    NWNX_Object_SetString(oV, "ts", "t", FALSE);
    NWNX_Object_SetFloat(oV, "pf", 2.5, TRUE);
    NWNX_Object_SetFloat(oV, "tf", 3.5, FALSE);
    NWNX_Object_SetInt(oV, "pi2", -7, TRUE);
    string sV = NWNX_Object_Serialize(oV);
    PRaw("posvars", sV);
    object oVc = NWNX_Object_Deserialize(sV);
    PLog("posvars.copy", IntToString(NWNX_Object_GetInt(oVc, "pi")) + "/" + IntToString(NWNX_Object_GetInt(oVc, "ti")) + "/"
        + NWNX_Object_GetString(oVc, "ps") + "/" + NWNX_Object_GetString(oVc, "ts") + "/"
        + FloatToString(NWNX_Object_GetFloat(oVc, "pf"), 0, 2) + "/" + FloatToString(NWNX_Object_GetFloat(oVc, "tf"), 0, 2) + "/"
        + IntToString(NWNX_Object_GetInt(oVc, "pi2")));

    PLog("module", NWNX_Object_Serialize(GetModule()));
    PLog("area", GetStringLeft(NWNX_Object_Serialize(GetArea(oW)), 20));
    PLog("invalid", NWNX_Object_Serialize(OBJECT_INVALID));
    PLogI("bad", GetIsObjectValid(NWNX_Object_Deserialize("notbase64!!")));
    PLogI("empty", GetIsObjectValid(NWNX_Object_Deserialize("")));

    // A deserialized item acquired by a creature.
    object oAcq = NWNX_Object_Deserialize(sItem);
    PLogI("acquire", NWNX_Object_AcquireItem(oC, oAcq));
    PLogI("acquire.possessor", GetItemPossessor(oAcq) == oC);
    object oHeld = CreateItemOnObject("nw_it_mpotion002", oC);
    object oOther = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(6.0), FALSE, "SERIAL_OTHER");
    PLogI("acquire.held", NWNX_Object_AcquireItem(oOther, oHeld));
    PLogI("acquire.heldowner", GetItemPossessor(oHeld) == oOther);
    object oGround = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mpotion003", PCentre(1.0));
    PLogI("acquire.ground", NWNX_Object_AcquireItem(oP, oGround));
    PLogI("acquire.groundarea", GetIsObjectValid(GetArea(oGround)));
    PLogI("acquire.waypoint", NWNX_Object_AcquireItem(oW, NWNX_Object_Deserialize(sItem)));

    // Placing deserialized objects.
    object oItem2 = NWNX_Object_Deserialize(sItem);
    object oCopied = CopyObject(oItem2, PCentre(1.0));
    PLogI("copy.valid", GetIsObjectValid(oCopied));
    PLogI("copy.area", GetArea(oCopied) == GetArea(oW));
    PLogI("copy.local", GetLocalInt(oCopied, "probe"));
    object oGob2 = NWNX_Object_Deserialize(sC);
    NWNX_Object_AddToArea(oGob2, GetArea(oW), Vector(15.0, 15.0, 0.0));
    PLogI("added.area", GetArea(oGob2) == GetArea(oW));
    vector va = GetPosition(oGob2);
    PLog("added.pos", FloatToString(va.x, 0, 1) + "," + FloatToString(va.y, 0, 1));
    object oChest2 = NWNX_Object_Deserialize(sP);
    NWNX_Object_AddToArea(oChest2, GetArea(oW), Vector(12.0, 12.0, 0.0));
    PLogI("addedchest.area", GetArea(oChest2) == GetArea(oW));
    object oFirst = GetFirstItemInInventory(oChest2);
    PLog("addedchest.item", GetResRef(oFirst));

    // A round trip of a round trip keeps the same content.
    PLogI("roundtrip", GetStringLength(NWNX_Object_Serialize(NWNX_Object_Deserialize(sW))) == GetStringLength(sW));
    // Hand-built strings in the original's layout (pserial_probe), read by both engines, then written and read again.
    object oProbeW = NWNX_Object_Deserialize(SerialProbeWaypoint());
    object oProbeI = NWNX_Object_Deserialize(SerialProbeItem());
    Describe("probe.wp", oProbeW);
    Describe("probe.item", oProbeI);
    PLog("probe.wp.posstr", NWNX_Object_GetString(oProbeW, "poss"));
    PLog("probe.item.posstr", NWNX_Object_GetString(oProbeI, "poss"));
    object oProbeW2 = NWNX_Object_Deserialize(NWNX_Object_Serialize(oProbeW));
    object oProbeI2 = NWNX_Object_Deserialize(NWNX_Object_Serialize(oProbeI));
    Describe("probe.wp2", oProbeW2);
    Describe("probe.item2", oProbeI2);
    PLog("probe.wp2.posstr", NWNX_Object_GetString(oProbeW2, "poss"));
    PLog("probe.item2.posstr", NWNX_Object_GetString(oProbeI2, "poss"));
    DelayCommand(2.0, PDone());
}
