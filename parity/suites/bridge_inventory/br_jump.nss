// The chair in each area takes whoever uses it to the other area.
void main()
{
    object oUser = GetLastUsedBy();
    object oHere = GetArea(OBJECT_SELF);
    object oThere = GetTag(oHere) == "BR_TWO" ? GetObjectByTag("PARITY_AREA") : GetObjectByTag("BR_TWO");
    AssignCommand(oUser, JumpToLocation(Location(oThere, Vector(20.0, 20.0, 0.0), 0.0)));
}
