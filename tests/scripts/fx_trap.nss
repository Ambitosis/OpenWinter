// World presentation test: a spike trap four metres east of the caller, detected by the caller.
void main()
{
    vector v = GetPosition(OBJECT_SELF);
    location lTrap = Location(GetArea(OBJECT_SELF), Vector(v.x + 4.0, v.y, v.z), 0.0);
    object oTrap = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, lTrap, 3.0, "FX_TRAP", STANDARD_FACTION_HOSTILE);
    SetTrapDetectedBy(oTrap, OBJECT_SELF);
}
