// Surface and ground height at the location stored in module local "probe".
void main()
{
    object m = GetModule();
    location l = GetLocalLocation(m, "probe");
    SetLocalInt(m, "surface", GetSurfaceMaterial(l));
    SetLocalFloat(m, "height", GetGroundHeight(l));
    location bad = Location(OBJECT_INVALID, Vector(1.0, 1.0, 0.0), 0.0);
    SetLocalFloat(m, "bad_height", GetGroundHeight(bad));
    SetLocalInt(m, "bad_surface", GetSurfaceMaterial(bad));
    SetLocalInt(m, "tile_id", GetTileID(l));
    SetLocalInt(m, "bad_tile", GetTileID(bad));
}
