// nwnx_area_objects: objects NWNX_Area makes (CreateTransition, CreateGenericTrigger, CreateSoundObject), the default
// UI discovery masks, ExportARE / ExportGIT with the exclusion list (and an area created from the export), RotateArea.
#include "parity_inc"
#include "nwnx_area"
#include "nwnx_object"

// A trigger geometry string with each number to two decimals (the engines print different decimals).
string Geo(string s)
{
    string sOut = "", sNum = "";
    int i;
    for (i = 0; i <= GetStringLength(s); i++)
    {
        string c = GetSubString(s, i, 1);
        if (c != "" && FindSubString("0123456789.-", c) >= 0) sNum += c;
        else if (sNum != "")
        {
            sOut += FloatToString(StringToFloat(sNum), 0, 2) + (c == "}" ? "|" : ",");
            sNum = "";
        }
    }
    return sOut;
}

string V(vector v) { return FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2); }

void Obj(string p, object o)
{
    PLogI(p + ".valid", GetIsObjectValid(o));
    PLogI(p + ".type", GetObjectType(o));
    PLog(p + ".tag", GetTag(o));
    PLog(p + ".resref", GetResRef(o));
    PLog(p + ".name", GetName(o));
    PLog(p + ".pos", V(GetPosition(o)));
    PLogF(p + ".facing", GetFacing(o), 0.01);
    PLog(p + ".area", GetTag(GetArea(o)));
}

string TileInfo(object a, int i)
{
    struct NWNX_Area_TileInfo t = NWNX_Area_GetTileInfoByTileIndex(a, i);
    return IntToString(t.nID) + " o" + IntToString(t.nOrientation) + " h" + IntToString(t.nHeight) + " x" + IntToString(t.nGridX) + " y" + IntToString(t.nGridY);
}

void AfterExport()
{
    object e = CreateArea("nwexp1", "EXPCOPY");
    PLogI("export.created", GetIsObjectValid(e));
    PLog("export.created.tag", GetTag(e));
    PLog("export.created.name", GetName(e));
    PLog("export.created.resref", GetResRef(e));
    PLogI("export.created.w", GetAreaSize(AREA_WIDTH, e));
    PLog("export.created.tileset", GetTilesetResRef(e));
    PLogI("export.created.spot", NWNX_Area_GetAreaSpotModifier(e));
    string sObjs = "";
    object o = GetFirstObjectInArea(e);
    while (GetIsObjectValid(o))
    {
        sObjs += GetTag(o) + "/" + IntToString(GetObjectType(o)) + "/" + IntToString(GetLocalInt(o, "kept")) + " ";
        o = GetNextObjectInArea(e);
    }
    PLog("export.created.objects", sObjs);
    PLogI("export.created.local", GetLocalInt(e, "areavar"));
    object f = CreateArea("nwexp2", "EXPCOPY2");
    PLogI("export2.created", GetIsObjectValid(f));
    o = GetFirstObjectInArea(f);
    sObjs = "";
    while (GetIsObjectValid(o)) { sObjs += GetTag(o) + "/" + IntToString(GetObjectType(o)) + " "; o = GetNextObjectInArea(f); }
    PLog("export2.created.objects", sObjs);
    PLogI("export2.created.local", GetLocalInt(f, "areavar"));
    PDone();
}

// Waypoints at every 30 degrees in an area, their facings read after RotateArea(n).
void Facings(string sArea, int nRotation)
{
    object a = GetObjectByTag(sArea);
    int i;
    for (i = 0; i < 12; i++)
        CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", Location(a, Vector(2.0 + IntToFloat(i), 10.0, 0.0), IntToFloat(i * 30)), FALSE, sArea + "_W" + IntToString(i));
    NWNX_Area_RotateArea(a, nRotation);
    string s = "";
    for (i = 0; i < 12; i++) s += " " + FloatToString(GetFacing(GetObjectByTag(sArea + "_W" + IntToString(i))), 0, 1);
    PLog("facings.rot" + IntToString(nRotation), s);
}

void main()
{
    object a = GetObjectByTag("PARITY_AREA");
    object b = GetObjectByTag("PARITY2");
    object c = GetObjectByTag("PARITY3");
    Facings("PARITY4", 1);
    Facings("PARITY5", 2);
    Facings("PARITY6", 3);
    object wp = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", Location(a, Vector(30.0, 30.0, 0.0), 0.0), FALSE, "WP_DEST");

    // Transitions and generic triggers.
    object t1 = NWNX_Area_CreateTransition(a, wp, 10.0, 10.0, 0.0, 3.0, "TRANS1");
    Obj("trans", t1);
    PLog("trans.target", GetTag(GetTransitionTarget(t1)));
    PLog("trans.geometry", Geo(NWNX_Object_GetTriggerGeometry(t1)));
    object t2 = NWNX_Area_CreateTransition(a, wp, 12.0, 12.0, 0.0);
    PLog("trans.default.tag", GetTag(t2));
    PLog("trans.default.geometry", Geo(NWNX_Object_GetTriggerGeometry(t2)));
    object t3 = NWNX_Area_CreateTransition(a, GetModule(), 12.0, 12.0, 0.0);
    PLogI("trans.badtarget", GetIsObjectValid(t3));
    object t4 = NWNX_Area_CreateTransition(OBJECT_INVALID, wp, 12.0, 12.0, 0.0);
    PLogI("trans.badarea", GetIsObjectValid(t4));
    object g1 = NWNX_Area_CreateGenericTrigger(a, 20.0, 20.0, 0.0, "GEN1", 4.0);
    Obj("gen", g1);
    PLog("gen.geometry", Geo(NWNX_Object_GetTriggerGeometry(g1)));
    PLog("gen.onenter", GetEventScript(g1, EVENT_SCRIPT_TRIGGER_ON_OBJECT_ENTER));
    object g2 = NWNX_Area_CreateGenericTrigger(a, 25.0, 25.0, 1.5);
    PLog("gen.default.tag", GetTag(g2));
    PLog("gen.default.geometry", Geo(NWNX_Object_GetTriggerGeometry(g2)));
    PLogI("gen.badarea", GetIsObjectValid(NWNX_Area_CreateGenericTrigger(OBJECT_INVALID, 1.0, 1.0, 0.0)));

    // Sound objects.
    object s1 = NWNX_Area_CreateSoundObject(a, Vector(15.0, 16.0, 1.0), "campfire");
    Obj("sound", s1);
    object s2 = NWNX_Area_CreateSoundObject(a, Vector(15.0, 16.0, 1.0), "nosuchsound");
    PLogI("sound.bad", GetIsObjectValid(s2));

    // Default UI discovery masks.
    object cr0 = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-8.0), FALSE, "CR0");
    PLogI("disc.before", GetObjectUiDiscoveryMask(cr0));
    NWNX_Area_SetDefaultObjectUiDiscoveryMask(a, OBJECT_TYPE_CREATURE, OBJECT_UI_DISCOVERY_HILITE_TAB);
    object cr1 = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-6.0), FALSE, "CR1");
    object it1 = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mpotion001", PCentre(-6.0, 2.0));
    PLogI("disc.cr0", GetObjectUiDiscoveryMask(cr0));
    PLogI("disc.cr1", GetObjectUiDiscoveryMask(cr1));
    PLogI("disc.it1", GetObjectUiDiscoveryMask(it1));
    NWNX_Area_SetDefaultObjectUiDiscoveryMask(OBJECT_INVALID, OBJECT_TYPE_ITEM | OBJECT_TYPE_CREATURE, OBJECT_UI_DISCOVERY_TEXTBUBBLE_TAB);
    object cr2 = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-4.0), FALSE, "CR2");
    object it2 = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mpotion001", PCentre(-4.0, 2.0));
    object crb = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(b, Vector(5.0, 5.0, 0.0), 0.0), FALSE, "CRB");
    PLogI("disc.cr2", GetObjectUiDiscoveryMask(cr2));
    PLogI("disc.it2", GetObjectUiDiscoveryMask(it2));
    PLogI("disc.crb", GetObjectUiDiscoveryMask(crb));
    NWNX_Area_SetDefaultObjectUiDiscoveryMask(a, OBJECT_TYPE_CREATURE, OBJECT_UI_DISCOVERY_HILITE_MOUSEOVER, TRUE);
    PLogI("disc.force.cr0", GetObjectUiDiscoveryMask(cr0));
    PLogI("disc.force.cr2", GetObjectUiDiscoveryMask(cr2));
    PLogI("disc.force.it2", GetObjectUiDiscoveryMask(it2));
    PLogI("disc.force.crb", GetObjectUiDiscoveryMask(crb));
    NWNX_Area_SetDefaultObjectUiDiscoveryMask(a, OBJECT_TYPE_CREATURE, OBJECT_UI_DISCOVERY_DEFAULT);
    object cr3 = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-2.0), FALSE, "CR3");
    PLogI("disc.cr3", GetObjectUiDiscoveryMask(cr3));
    SetObjectUiDiscoveryMask(cr3, OBJECT_UI_DISCOVERY_DEFAULT);
    PLogI("disc.cr3.setdefault", GetObjectUiDiscoveryMask(cr3));
    SetObjectUiDiscoveryMask(cr3, 3);
    PLogI("disc.cr3.set3", GetObjectUiDiscoveryMask(cr3));
    PLogI("disc.wp", GetObjectUiDiscoveryMask(wp));
    NWNX_Area_SetDefaultObjectUiDiscoveryMask(OBJECT_INVALID, OBJECT_TYPE_ALL, OBJECT_UI_DISCOVERY_DEFAULT);

    // Export with an excluded object and a filter.
    object keep = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", PCentre(5.0), FALSE, "KEEP");
    SetLocalInt(keep, "kept", 3);
    object skip = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", PCentre(6.0), FALSE, "SKIP");
    NWNX_Area_AddObjectToExclusionList(skip);
    object skip2 = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", PCentre(7.0), FALSE, "SKIP2");
    NWNX_Area_AddObjectToExclusionList(skip2);
    NWNX_Area_RemoveObjectFromExclusionList(skip2);
    SetLocalInt(a, "areavar", 9);
    NWNX_Area_SetAreaSpotModifier(a, 4);
    PLogI("export.are", NWNX_Area_ExportARE(a, "nwexp1", "Exported", "EXPTAG"));
    PLogI("export.git", NWNX_Area_ExportGIT(a, "nwexp1"));
    PLogI("export.are2", NWNX_Area_ExportARE(a, "nwexp2"));
    PLogI("export.git2", NWNX_Area_ExportGIT(a, "nwexp2", FALSE, TRUE, OBJECT_TYPE_CREATURE | OBJECT_TYPE_TRIGGER));
    PLogI("export.badarea", NWNX_Area_ExportGIT(OBJECT_INVALID, "nwexp3"));
    PLogI("export.longname", NWNX_Area_ExportARE(a, "abcdefghijklmnopq"));
    PLogI("export.badalias", NWNX_Area_ExportARE(a, "nwexp4", "", "", "NOSUCHALIAS"));

    // RotateArea on a 4x4 area with distinct tiles and objects.
    SetTile(Location(c, Vector(15.0, 5.0, 0.0), 0.0), 35, 2, 0);
    SetTile(Location(c, Vector(5.0, 25.0, 0.0), 0.0), 36, 0, 1);
    object rp = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", Location(c, Vector(5.0, 6.0, 0.0), 30.0), FALSE, "RP");
    object rp0 = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", Location(c, Vector(25.0, 15.0, 0.0), 0.0), FALSE, "RP0");
    object rp100 = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", Location(c, Vector(25.0, 25.0, 0.0), 100.0), FALSE, "RP100");
    object rp200 = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", Location(c, Vector(35.0, 35.0, 0.0), 200.0), FALSE, "RP200");
    object rc = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(c, Vector(12.0, 17.0, 0.0), 90.0), FALSE, "RC");
    object rc30 = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", Location(c, Vector(32.0, 27.0, 0.0), 30.0), FALSE, "RC30");
    object rw = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", Location(c, Vector(33.0, 21.0, 0.0), 180.0), FALSE, "RW");
    object ri = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mpotion001", Location(c, Vector(14.0, 33.0, 0.0), 45.0), FALSE, "RI");
    object rs = NWNX_Area_CreateSoundObject(c, Vector(16.0, 3.0, 0.0), "campfire");
    object rt = NWNX_Area_CreateGenericTrigger(c, 30.0, 10.0, 0.0, "RT", 2.0);
    Obj("pre.p", rp);
    Obj("pre.p0", rp0);
    Obj("pre.p100", rp100);
    Obj("pre.p200", rp200);
    Obj("pre.c", rc);
    Obj("pre.c30", rc30);
    Obj("pre.w", rw);
    Obj("pre.i", ri);
    NWNX_Area_RotateArea(c, 1);
    PLog("rot.size", IntToString(GetAreaSize(AREA_WIDTH, c)) + "x" + IntToString(GetAreaSize(AREA_HEIGHT, c)));
    Obj("rot.p", rp);
    Obj("rot.p0", rp0);
    Obj("rot.p100", rp100);
    Obj("rot.p200", rp200);
    Obj("rot.c", rc);
    Obj("rot.c30", rc30);
    Obj("rot.w", rw);
    Obj("rot.i", ri);
    PLog("rot.s.pos", V(GetPosition(rs)));
    PLog("rot.t.geometry", Geo(NWNX_Object_GetTriggerGeometry(rt)));
    PLog("rot.t.pos", V(GetPosition(rt)));
    int i;
    for (i = 0; i < 16; i++) PLog("rot.tile" + IntToString(i), TileInfo(c, i));
    NWNX_Area_RotateArea(c, 2);
    Obj("rot3.p", rp);
    Obj("rot3.p0", rp0);
    Obj("rot3.c30", rc30);
    Obj("rot3.w", rw);
    PLog("rot3.tile1", TileInfo(c, 1));
    NWNX_Area_RotateArea(c, 0);
    Obj("rot0.p", rp);
    NWNX_Area_RotateArea(c, 4);
    Obj("rot4.p", rp);
    DelayCommand(1.0, AfterExport());
}
