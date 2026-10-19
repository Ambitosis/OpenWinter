// Hearing through walls in exterior, interior and underground areas (docs/STEALTH.md, "The Listen check"). The
// default tile (tde01 121) is a walled corner: each tile's floor is its lower-left corner, walled off from the next
// tile's. In each area (ARE Flags 0 exterior, 1 interior, 2 underground, 3 both) an observer stands in one tile's
// corner with plain targets (not sneaking) in its own corner, the next tile's to the east and the next to the north.
#include "pstealth_inc"

void Setup(int a)
{
    object oArea = GetObjectByTag("SN_A" + IntToString(a));
    object o = PsMake("sn_obs", oArea, 3.0, 3.0, STANDARD_FACTION_COMMONER);
    SetLocalObject(GetModule(), "O" + IntToString(a), o);
    SetLocalObject(o, "T0", PsMake("sn_tgt", oArea, 5.0, 4.5, STANDARD_FACTION_MERCHANT));
    SetLocalObject(o, "T1", PsMake("sn_tgt", oArea, 13.0, 3.0, STANDARD_FACTION_MERCHANT));
    SetLocalObject(o, "T2", PsMake("sn_tgt", oArea, 3.0, 13.0, STANDARD_FACTION_MERCHANT));
    // Walkers: is there a walkable path from the observer's corner to the others?
    object w1 = PsMake("sn_tgt", oArea, 2.0, 5.0, STANDARD_FACTION_MERCHANT);
    object w2 = PsMake("sn_tgt", oArea, 5.0, 2.0, STANDARD_FACTION_MERCHANT);
    SetLocalObject(o, "W1", w1);
    SetLocalObject(o, "W2", w2);
    AssignCommand(w1, ActionMoveToLocation(Location(oArea, Vector(15.0, 4.0, 0.0), 0.0), TRUE));
    AssignCommand(w2, ActionMoveToLocation(Location(oArea, Vector(4.0, 15.0, 0.0), 0.0), TRUE));
}

void Read(int a)
{
    object o = GetLocalObject(GetModule(), "O" + IntToString(a));
    string k = "flags" + IntToString(a);
    int i;
    for (i = 0; i < 3; i++)
    {
        object t = GetLocalObject(o, "T" + IntToString(i));
        string kk = k + "." + (i == 0 ? "sameroom" : i == 1 ? "east" : "north");
        PLogI(kk + ".los", LineOfSightObject(o, t));
        PLogI(kk + ".seen", GetObjectSeen(t, o));
        PLogI(kk + ".heard", GetObjectHeard(t, o));
    }
    PLogI(k + ".interior", GetIsAreaInterior(GetArea(o)));
    PLogI(k + ".aboveground", GetIsAreaAboveGround(GetArea(o)));
    vector v1 = GetPosition(GetLocalObject(o, "W1")), v2 = GetPosition(GetLocalObject(o, "W2"));
    PLogI(k + ".walker.east.arrived", v1.x > 12.0);
    PLogI(k + ".walker.north.arrived", v2.y > 12.0);
    WriteTimestampedLogEntry("RAW|" + k + ".walkers|" + FloatToString(v1.x, 0, 1) + "," + FloatToString(v1.y, 0, 1) + " " + FloatToString(v2.x, 0, 1) + "," + FloatToString(v2.y, 0, 1));
}

void main()
{
    PStart();
    int a;
    for (a = 0; a < 4; a++)
    {
        Setup(a);
        DelayCommand(14.0, Read(a));
    }
    DelayCommand(15.0, PDone());
}
