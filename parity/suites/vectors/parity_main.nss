// Vectors, angles, facing and locations.
#include "parity_inc"

string V(vector v) { return FloatToString(v.x, 0, 5) + "," + FloatToString(v.y, 0, 5) + "," + FloatToString(v.z, 0, 5); }
void LogV(string k, vector v) { PLog(k, V(v)); }
void Fl(string k, float f) { PLog(k, FloatToString(f, 0, 4)); }

void Vectors()
{
    Fl("v2a.zero", VectorToAngle(Vector(0.0, 0.0, 0.0)));
    Fl("v2a.zup", VectorToAngle(Vector(0.0, 0.0, 5.0)));
    Fl("v2a.e", VectorToAngle(Vector(1.0, 0.0, 0.0)));
    Fl("v2a.n", VectorToAngle(Vector(0.0, 1.0, 0.0)));
    Fl("v2a.w", VectorToAngle(Vector(-1.0, 0.0, 0.0)));
    Fl("v2a.s", VectorToAngle(Vector(0.0, -1.0, 0.0)));
    Fl("v2a.ne", VectorToAngle(Vector(1.0, 1.0, 0.0)));
    Fl("v2a.sw", VectorToAngle(Vector(-1.0, -1.0, 0.0)));
    Fl("v2a.se", VectorToAngle(Vector(1.0, -1.0, 0.0)));
    Fl("v2a.big", VectorToAngle(Vector(1000.0, -0.001, 0.0)));
    Fl("v2a.tiny", VectorToAngle(Vector(0.00001, 0.00001, 0.0)));
    Fl("v2a.negzero", VectorToAngle(Vector(-1.0, -0.0, 0.0)));
    Fl("v2a.3d", VectorToAngle(Vector(1.0, 1.0, 100.0)));
    float a;
    for (a = -720.0; a <= 720.0; a += 45.0)
        LogV("a2v." + IntToString(FloatToInt(a)), AngleToVector(a));
    LogV("a2v.1e6", AngleToVector(1000000.0));
    LogV("a2v.0.5", AngleToVector(0.5));
    LogV("norm.zero", VectorNormalize(Vector(0.0, 0.0, 0.0)));
    LogV("norm.345", VectorNormalize(Vector(3.0, 4.0, 0.0)));
    LogV("norm.z", VectorNormalize(Vector(0.0, 0.0, -7.0)));
    LogV("norm.big", VectorNormalize(Vector(10000000000000000000.0, 0.0, 0.0)));
    LogV("norm.tiny", VectorNormalize(Vector(0.0000000000000000000001, 0.0, 0.0)));
    Fl("mag.345", VectorMagnitude(Vector(3.0, 4.0, 12.0)));
    Fl("mag.zero", VectorMagnitude(Vector()));
    Fl("mag.neg", VectorMagnitude(Vector(-3.0, -4.0, 0.0)));
    LogV("arith.mul", Vector(1.0, 2.0, 3.0) * 2.5);
    LogV("arith.rmul", 2.5 * Vector(1.0, 2.0, 3.0));
    LogV("arith.div", Vector(1.0, 2.0, 3.0) / 4.0);
    LogV("arith.add", Vector(1.0, 2.0, 3.0) + Vector(0.5, 0.5, 0.5));
    LogV("arith.sub", Vector(1.0, 2.0, 3.0) - Vector(0.5, 0.5, 0.5));
    PLogI("eq.same", Vector(1.0, 2.0, 3.0) == Vector(1.0, 2.0, 3.0));
    PLogI("eq.diff", Vector(1.0, 2.0, 3.0) == Vector(1.0, 2.0, 3.5));
    PLogI("ne.diff", Vector(1.0, 2.0, 3.0) != Vector(1.0, 2.0, 3.5));
}

void Locations()
{
    object oArea = GetObjectByTag("PARITY_AREA");
    float f;
    for (f = -450.0; f <= 810.0; f += 90.0)
    {
        location l = Location(oArea, Vector(1.0, 2.0, 3.0), f);
        Fl("loc.facing." + IntToString(FloatToInt(f)), GetFacingFromLocation(l));
    }
    location l1 = Location(oArea, Vector(1.0, 1.0, 0.0), 0.0);
    location l2 = Location(oArea, Vector(4.0, 5.0, 0.0), 0.0);
    location l3 = Location(oArea, Vector(4.0, 5.0, 12.0), 0.0);
    location lx = Location(OBJECT_INVALID, Vector(4.0, 5.0, 0.0), 0.0);
    Fl("dist.loc.2d", GetDistanceBetweenLocations(l1, l2));
    Fl("dist.loc.3d", GetDistanceBetweenLocations(l1, l3));
    Fl("dist.loc.invalid", GetDistanceBetweenLocations(l1, lx));
    Fl("dist.loc.bothinvalid", GetDistanceBetweenLocations(lx, lx));
    PLogI("loc.area.invalid", GetIsObjectValid(GetAreaFromLocation(lx)));
    PLogI("loc.eq", l2 == Location(oArea, Vector(4.0, 5.0, 0.0), 0.0));
    PLogI("loc.eq.facing", l2 == Location(oArea, Vector(4.0, 5.0, 0.0), 90.0));
    PLogI("loc.eq.facing360", l2 == Location(oArea, Vector(4.0, 5.0, 0.0), 360.0));
    PLogI("loc.ne", l1 != l2);
    LogV("loc.pos", GetPositionFromLocation(l3));
    location lnull;
    PLogI("loc.null.area", GetIsObjectValid(GetAreaFromLocation(lnull)));
    LogV("loc.null.pos", GetPositionFromLocation(lnull));
    Fl("loc.null.facing", GetFacingFromLocation(lnull));
}

// Runs as the object whose facing is set.
void Facing(string k)
{
    float f;
    for (f = -720.0; f <= 900.0; f += 90.0)
    {
        SetFacing(f);
        Fl(k + ".facing." + IntToString(FloatToInt(f)), GetFacing(OBJECT_SELF));
    }
    SetFacing(359.9);
    Fl(k + ".facing.359.9", GetFacing(OBJECT_SELF));
    SetFacing(-0.1);
    Fl(k + ".facing.-0.1", GetFacing(OBJECT_SELF));
    SetFacing(1000000.0);
    Fl(k + ".facing.1e6", GetFacing(OBJECT_SELF));
    SetFacing(123.456);
    Fl(k + ".facing.123", GetFacing(OBJECT_SELF));
    Fl(k + ".locfacing", GetFacingFromLocation(GetLocation(OBJECT_SELF)));
    SetFacingPoint(GetPosition(OBJECT_SELF) + Vector(0.0, -3.0, 0.0));
    Fl(k + ".facingpoint.s", GetFacing(OBJECT_SELF));
    SetFacingPoint(GetPosition(OBJECT_SELF));
    Fl(k + ".facingpoint.self", GetFacing(OBJECT_SELF));
}

void Objects()
{
    object oArea = GetObjectByTag("PARITY_AREA");
    object c = CreateObject(OBJECT_TYPE_CREATURE, "nw_dog", Location(oArea, Vector(10.0, 10.0, 3.0), 450.0));
    object p = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", Location(oArea, Vector(14.0, 10.0, 3.0), -90.0));
    object w = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", Location(oArea, Vector(10.0, 14.0, 3.0), 720.3));
    object i = CreateObject(OBJECT_TYPE_ITEM, "nw_wswls001", Location(oArea, Vector(12.0, 12.0, 3.0), 200.0));
    LogV("pos.placeable", GetPosition(p));
    LogV("pos.waypoint", GetPosition(w));
    LogV("pos.item", GetPosition(i));
    Fl("create.facing.creature", GetFacing(c));
    Fl("create.facing.placeable", GetFacing(p));
    Fl("create.facing.waypoint", GetFacing(w));
    Fl("create.facing.item", GetFacing(i));
    Fl("dist.pw", GetDistanceBetween(p, w));
    Fl("dist.pi", GetDistanceBetween(p, i));
    Fl("dist.self", GetDistanceBetween(p, p));
    Fl("dist.invalid", GetDistanceBetween(p, OBJECT_INVALID));
    Fl("dist.module", GetDistanceBetween(p, GetModule()));
    Fl("dist.area", GetDistanceBetween(p, oArea));
    Fl("dist.modarea", GetDistanceBetween(GetModule(), oArea));
    Fl("distto.from", GetDistanceToObject(p, w));
    Fl("distto.invalid", GetDistanceToObject(OBJECT_INVALID, p));
    Fl("distto.self", GetDistanceToObject(p, p));
    Fl("distto.modself", GetDistanceToObject(p));
    Fl("distto.area", GetDistanceToObject(p, oArea));
    object a2 = CreateArea("parity", "AREA2");
    object q = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", Location(a2, Vector(14.0, 10.0, 0.0), 0.0));
    Fl("dist.otherarea", GetDistanceBetween(p, q));
    Fl("dist.otherarea.area", GetDistanceBetween(a2, p));
    Fl("dist.ownarea", GetDistanceBetween(a2, q));
    Fl("distto.otherarea", GetDistanceToObject(p, q));
    Fl("distloc.otherarea", GetDistanceBetweenLocations(GetLocation(p), GetLocation(q)));
    Fl("distloc.inv2", GetDistanceBetweenLocations(Location(OBJECT_INVALID, Vector(1.0, 0.0, 0.0), 0.0), Location(OBJECT_INVALID, Vector(4.0, 4.0, 0.0), 0.0)));
    // SetFacing with the object argument, and run as each object.
    SetFacing(30.0, p);
    Fl("setfacing.arg.placeable", GetFacing(p));
    SetFacing(400.0, c);
    Fl("setfacing.arg.creature", GetFacing(c));
    SetFacingPoint(GetPosition(c) + Vector(-1.0, 0.0, 0.0), c);
    Fl("setfacingpoint.arg.creature", GetFacing(c));
    AssignCommand(c, Facing("cre"));
    AssignCommand(p, Facing("plc"));
    // AssignCommand to a waypoint never runs in the original (see the exec suite).
    AssignCommand(w, Facing("wp"));
}

void main()
{
    DelayCommand(1.0, Vectors());
    DelayCommand(1.1, Locations());
    DelayCommand(1.2, Objects());
    DelayCommand(3.0, PDone());
}
