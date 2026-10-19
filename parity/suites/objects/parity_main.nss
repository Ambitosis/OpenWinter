// Object queries: area iteration, nearest-object searches, shapes, tags, validity and OBJECT_SELF.
#include "parity_inc"

object Make(int nType, string sResRef, float x, float y, string sTag, float z = 0.0)
{
    object oArea = GetObjectByTag("PARITY_AREA");
    return CreateObject(nType, sResRef, Location(oArea, Vector(x, y, z), 0.0), FALSE, sTag);
}

// Tags of everything an iteration returns, in order (only our "o_" objects).
string AreaList(object oArea, int nFilter)
{
    string s = "";
    object o = GetFirstObjectInArea(oArea, nFilter);
    while (GetIsObjectValid(o))
    {
        if (GetStringLeft(GetTag(o), 2) == "o_") s += GetTag(o) + " ";
        o = GetNextObjectInArea(oArea, nFilter);
    }
    return s;
}

string ShapeList(int nShape, float fSize, location l, int nFilter, vector vOrigin = [0.0, 0.0, 0.0])
{
    string s = "";
    object o = GetFirstObjectInShape(nShape, fSize, l, FALSE, nFilter, vOrigin);
    while (GetIsObjectValid(o))
    {
        s += GetTag(o) + " ";
        o = GetNextObjectInShape(nShape, fSize, l, FALSE, nFilter, vOrigin);
    }
    return s;
}

void Setup()
{
    // Created in a scrambled order so creation order, type and distance all differ.
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 20.0, 20.0, "o_wp_c");
    Make(OBJECT_TYPE_PLACEABLE, "plc_chest1", 23.0, 20.0, "o_plc_e3");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 20.0, 25.0, "o_wp_n5");
    Make(OBJECT_TYPE_ITEM, "nw_wswls001", 18.0, 20.0, "o_item_w2");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 16.0, 20.0, "o_wp_w4");
    Make(OBJECT_TYPE_STORE, "nw_storebar01", 26.0, 26.0, "o_store");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 21.0, 20.0, "o_wp_e1");
    Make(OBJECT_TYPE_PLACEABLE, "plc_chest1", 20.0, 14.0, "o_plc_s6");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 22.0, 22.0, "o_wp_ne");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 22.0, 22.0, "o_wp_ne2");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 20.0, 20.0, "o_wp_c_up", 2.0);
    // Shape boundary markers along +x from (10,30): 2.9, 3.0, 3.1 and diagonals.
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 12.9, 30.0, "s_2.9");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 13.0, 30.0, "s_3.0");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 13.1, 30.0, "s_3.1");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 12.0, 32.0, "s_d2.83");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 12.9, 32.9, "s_corner");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 10.0, 30.0, "s_0", 2.5);
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 10.0, 30.0, "s_0lo", -2.9);
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 7.0, 30.0, "s_-3");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 10.0, 27.5, "s_y-2.5");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 11.0, 31.4, "s_off");
    Make(OBJECT_TYPE_PLACEABLE, "plc_chest1", 10.0, 33.0, "s_plc_y3");
    Make(OBJECT_TYPE_PLACEABLE, "plc_chest1", 10.0, 30.0, "s_caster", -5.0);
    // Tags for case and nth tests.
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 30.0, 10.0, "dup");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 31.0, 10.0, "dup");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 32.0, 10.0, "DUP");
    Make(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", 33.0, 10.0, "dup");
}

void Iteration()
{
    object oArea = GetObjectByTag("PARITY_AREA");
    PLog("area.all", AreaList(oArea, OBJECT_TYPE_ALL));
    PLog("area.wp", AreaList(oArea, OBJECT_TYPE_WAYPOINT));
    PLog("area.plc|item", AreaList(oArea, OBJECT_TYPE_PLACEABLE | OBJECT_TYPE_ITEM));
    PLog("area.store", AreaList(oArea, OBJECT_TYPE_STORE));
    PLog("area.none", AreaList(oArea, 0));
    PLog("area.invalidarea", AreaList(OBJECT_INVALID, OBJECT_TYPE_ALL));
    PLogI("area.first.module", GetIsObjectValid(GetFirstObjectInArea(GetModule())));
    // Restarting and interleaving.
    object a = GetFirstObjectInArea(oArea, OBJECT_TYPE_WAYPOINT);
    object b = GetNextObjectInArea(oArea, OBJECT_TYPE_WAYPOINT);
    object c = GetFirstObjectInArea(oArea, OBJECT_TYPE_WAYPOINT);
    PLogI("area.restart", a == c);
    object d = GetNextObjectInArea(oArea, OBJECT_TYPE_PLACEABLE);
    PLog("area.switchfilter", GetTag(d));
    // Destroying the current object mid-iteration (destruction is deferred).
    object o = GetFirstObjectInArea(oArea, OBJECT_TYPE_WAYPOINT);
    DestroyObject(GetObjectByTag("o_wp_n5"));
    string s = "";
    while (GetIsObjectValid(o)) { if (GetStringLeft(GetTag(o), 2) == "o_") s += GetTag(o) + " "; o = GetNextObjectInArea(oArea, OBJECT_TYPE_WAYPOINT); }
    PLog("area.destroying", s);
    // The first area in the module and the number of areas.
    int n = 0;
    object ar = GetFirstArea();
    PLog("areas.first", GetTag(ar));
    while (GetIsObjectValid(ar)) { n++; ar = GetNextArea(); }
    PLogI("areas.count", n);
}

void Nearest()
{
    object c = GetObjectByTag("o_wp_c");
    int i;
    for (i = -1; i <= 9; i++)
    {
        PLog("near.all." + IntToString(i), GetTag(GetNearestObject(OBJECT_TYPE_ALL, c, i)));
        PLog("near.wp." + IntToString(i), GetTag(GetNearestObject(OBJECT_TYPE_WAYPOINT, c, i)));
        PLog("near.plc|item." + IntToString(i), GetTag(GetNearestObject(OBJECT_TYPE_PLACEABLE | OBJECT_TYPE_ITEM, c, i)));
    }
    location l = Location(GetObjectByTag("PARITY_AREA"), Vector(22.0, 21.0, 0.0), 0.0);
    for (i = 0; i <= 4; i++)
        PLog("nearloc.wp." + IntToString(i), GetTag(GetNearestObjectToLocation(OBJECT_TYPE_WAYPOINT, l, i)));
    PLog("near.invalid", GetTag(GetNearestObject(OBJECT_TYPE_ALL, OBJECT_INVALID)));
    PLog("near.module", GetTag(GetNearestObject(OBJECT_TYPE_ALL, GetModule())));
    PLog("near.area", GetTag(GetNearestObject(OBJECT_TYPE_ALL, GetObjectByTag("PARITY_AREA"))));
    PLog("near.none", GetTag(GetNearestObject(OBJECT_TYPE_DOOR, c)));
    object w = GetObjectByTag("dup");
    for (i = 0; i <= 4; i++)
    {
        PLog("neartag.dup." + IntToString(i), FloatToString(GetPosition(GetNearestObjectByTag("dup", w, i)).x, 0, 1));
        PLog("neartag.DUP." + IntToString(i), FloatToString(GetPosition(GetNearestObjectByTag("DUP", w, i)).x, 0, 1));
    }
    PLog("neartag.self", GetTag(GetNearestObjectByTag("o_wp_c", c)));
    PLog("neartag.empty", GetTag(GetNearestObjectByTag("", c)));
    PLog("neartag.case", GetTag(GetNearestObjectByTag("O_WP_E1", c)));
}

void Tags()
{
    int i;
    for (i = -1; i <= 4; i++)
    {
        PLog("bytag.dup." + IntToString(i), FloatToString(GetPosition(GetObjectByTag("dup", i)).x, 0, 1));
        PLog("bytag.DUP." + IntToString(i), FloatToString(GetPosition(GetObjectByTag("DUP", i)).x, 0, 1));
    }
    PLogI("bytag.lower", GetIsObjectValid(GetObjectByTag("parity_area")));
    PLogI("bytag.empty", GetIsObjectValid(GetObjectByTag("")));
    PLog("bytag.module", GetTag(GetObjectByTag("PARITY")));
    PLogI("bytag.module.isarea", GetObjectByTag("PARITY") == GetModule());
    PLogI("waypointbytag", GetIsObjectValid(GetWaypointByTag("o_wp_c")));
    PLogI("waypointbytag.plc", GetIsObjectValid(GetWaypointByTag("o_plc_e3")));
    // Tag changes take effect for lookups straight away.
    object o = GetObjectByTag("o_wp_e1");
    SetTag(o, "renamed");
    PLogI("bytag.renamed.old", GetIsObjectValid(GetObjectByTag("o_wp_e1")));
    PLogI("bytag.renamed.new", GetIsObjectValid(GetObjectByTag("renamed")));
    SetTag(o, "o_wp_e1");
}

void SpellCones()
{
    object oArea = GetObjectByTag("PARITY_AREA");
    int f = OBJECT_TYPE_WAYPOINT | OBJECT_TYPE_PLACEABLE;
    location e = Location(oArea, Vector(13.0, 30.0, 0.0), 0.0);
    PLog("shape.spellcone3", ShapeList(SHAPE_SPELLCONE, 3.0, e, f, Vector(10.0, 30.0, 0.0)));
    PLog("shape.spellcone6", ShapeList(SHAPE_SPELLCONE, 6.0, e, f));
    PLog("shape.spellcone6.diag", ShapeList(SHAPE_SPELLCONE, 6.0, Location(oArea, Vector(12.0, 32.0, 0.0), 0.0), f));
    PLog("shape.spellcone.self", ShapeList(SHAPE_SPELLCONE, 6.0, Location(oArea, Vector(10.0, 30.0, 0.0), 0.0), f));
}

void Shapes()
{
    object oArea = GetObjectByTag("PARITY_AREA");
    location l = Location(oArea, Vector(10.0, 30.0, 0.0), 0.0);
    int f = OBJECT_TYPE_WAYPOINT | OBJECT_TYPE_PLACEABLE;
    PLog("shape.sphere3", ShapeList(SHAPE_SPHERE, 3.0, l, f));
    PLog("shape.sphere2.5", ShapeList(SHAPE_SPHERE, 2.5, l, f));
    PLog("shape.sphere0", ShapeList(SHAPE_SPHERE, 0.0, l, f));
    PLog("shape.sphere-1", ShapeList(SHAPE_SPHERE, -1.0, l, f));
    PLog("shape.cube3", ShapeList(SHAPE_CUBE, 3.0, l, f));
    PLog("shape.cube2.5", ShapeList(SHAPE_CUBE, 2.5, l, f));
    PLog("shape.sphere3.wp", ShapeList(SHAPE_SPHERE, 3.0, l, OBJECT_TYPE_WAYPOINT));
    PLog("shape.sphere3.plc", ShapeList(SHAPE_SPHERE, 3.0, l, OBJECT_TYPE_PLACEABLE));
    vector o = Vector(10.0, 30.0, 0.0);
    // Cylinder and cones from (10,30) towards +x.
    location e = Location(oArea, Vector(13.0, 30.0, 0.0), 0.0);
    PLog("shape.cyl3", ShapeList(SHAPE_SPELLCYLINDER, 3.0, e, f, o));
    PLog("shape.cyl6", ShapeList(SHAPE_SPELLCYLINDER, 6.0, e, f, o));
    PLog("shape.cyl.zeroorigin", ShapeList(SHAPE_SPELLCYLINDER, 3.0, e, f));
    PLog("shape.cyl.sameorigin", ShapeList(SHAPE_SPELLCYLINDER, 3.0, l, f, o));
    PLog("shape.cyl.sameorigin90", ShapeList(SHAPE_SPELLCYLINDER, 3.0, Location(oArea, Vector(10.0, 30.0, 0.0), 90.0), f, o));
    // SHAPE_CONE is left out: the original's cone is far wider than documented near its apex.
    // Spell cones start at the caller, so run them as a placeable at the origin.
    AssignCommand(GetObjectByTag("s_caster"), SpellCones());
    PLog("shape.badshape", ShapeList(9, 3.0, l, f));
    PLog("shape.invalidloc", ShapeList(SHAPE_SPHERE, 3.0, Location(OBJECT_INVALID, Vector(10.0, 30.0, 0.0), 0.0), f));
    // Ordering with many objects at different distances around the centre.
    location c = Location(oArea, Vector(20.0, 20.0, 0.0), 0.0);
    PLog("shape.sphere.centre", ShapeList(SHAPE_SPHERE, 10.0, c, OBJECT_TYPE_ALL));
    PLog("shape.cube.centre", ShapeList(SHAPE_CUBE, 10.0, c, OBJECT_TYPE_ALL));
}

void Validity()
{
    object w = GetObjectByTag("o_wp_w4");
    DestroyObject(w);
    PLogI("destroy.immediate.valid", GetIsObjectValid(w));
    PLog("destroy.immediate.tag", GetTag(w));
    PLogI("destroy.immediate.bytag", GetIsObjectValid(GetObjectByTag("o_wp_w4")));
    object p = GetObjectByTag("o_plc_s6");
    DestroyObject(p, 0.5);
    DelayCommand(0.1, PLogI("destroy.after.valid", GetIsObjectValid(w)));
    DelayCommand(0.1, PLog("destroy.after.tag", GetTag(w)));
    DelayCommand(0.1, PLogI("destroy.delayed.early", GetIsObjectValid(p)));
    DelayCommand(1.0, PLogI("destroy.delayed.late", GetIsObjectValid(p)));
    DestroyObject(GetModule());
    DestroyObject(GetObjectByTag("PARITY_AREA"));
    DelayCommand(0.2, PLogI("destroy.module.valid", GetIsObjectValid(GetModule())));
    DelayCommand(0.2, PLogI("destroy.area.valid", GetIsObjectValid(GetObjectByTag("PARITY_AREA"))));
    PLogI("valid.invalid", GetIsObjectValid(OBJECT_INVALID));
    PLogI("valid.module", GetIsObjectValid(GetModule()));
    PLog("objtostr.invalid", ObjectToString(OBJECT_INVALID));
    PLogI("strtoobj.garbage", GetIsObjectValid(StringToObject("zzz")));
    PLogI("strtoobj.roundtrip", StringToObject(ObjectToString(GetObjectByTag("o_wp_c"))) == GetObjectByTag("o_wp_c"));
    PLogI("strtoobj.invalid", StringToObject("7f000000") == OBJECT_INVALID);
}

void SelfReport(string k)
{
    PLogI(k + ".type", GetObjectType(OBJECT_SELF));
    PLog(k + ".tag", GetTag(OBJECT_SELF));
    PLogI(k + ".ismodule", OBJECT_SELF == GetModule());
}

void Types()
{
    PLogI("type.module", GetObjectType(GetModule()));
    PLogI("type.area", GetObjectType(GetObjectByTag("PARITY_AREA")));
    PLogI("type.invalid", GetObjectType(OBJECT_INVALID));
    PLogI("type.store", GetObjectType(GetObjectByTag("o_store")));
    PLogI("type.item", GetObjectType(GetObjectByTag("o_item_w2")));
    PLog("area.ofmodule", GetTag(GetArea(GetModule())));
    PLog("area.ofarea", GetTag(GetArea(GetObjectByTag("PARITY_AREA"))));
    PLog("area.ofwp", GetTag(GetArea(GetObjectByTag("o_wp_c"))));
    PLog("resref.area", GetResRef(GetObjectByTag("PARITY_AREA")));
    PLog("resref.module", GetResRef(GetModule()));
    PLog("resref.wp", GetResRef(GetObjectByTag("o_wp_c")));
    SelfReport("self.main");
    DelayCommand(0.0, SelfReport("self.delay"));
    AssignCommand(GetObjectByTag("o_plc_e3"), SelfReport("self.assign.plc"));
    AssignCommand(GetObjectByTag("o_store"), SelfReport("self.assign.store"));
    AssignCommand(GetObjectByTag("o_item_w2"), SelfReport("self.assign.item"));
    AssignCommand(GetObjectByTag("o_wp_c"), SelfReport("self.assign.wp"));
    AssignCommand(GetObjectByTag("PARITY_AREA"), SelfReport("self.assign.area"));
    AssignCommand(GetModule(), SelfReport("self.assign.module"));
    AssignCommand(OBJECT_INVALID, SelfReport("self.assign.invalid"));
    AssignCommand(GetObjectByTag("o_plc_e3"), DelayCommand(0.1, SelfReport("self.assign.delay.plc")));
    DelayCommand(0.1, AssignCommand(GetObjectByTag("o_plc_e3"), SelfReport("self.delay.assign.plc")));
    ExecuteScript("obj_self", GetObjectByTag("o_plc_e3"));
    ExecuteScript("obj_self", GetObjectByTag("o_wp_c"));
    ExecuteScript("obj_self", GetObjectByTag("PARITY_AREA"));
    ExecuteScript("obj_self", OBJECT_INVALID);
    ExecuteScript("obj_self");
}

void main()
{
    DelayCommand(1.0, Setup());
    DelayCommand(1.5, Iteration());
    DelayCommand(1.6, Nearest());
    DelayCommand(1.7, Tags());
    DelayCommand(1.8, Shapes());
    DelayCommand(1.9, Types());
    DelayCommand(2.5, Validity());
    DelayCommand(3.0, ExecuteScript("obj_tags"));
    DelayCommand(5.0, PDone());
}
