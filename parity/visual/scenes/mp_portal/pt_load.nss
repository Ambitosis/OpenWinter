// mp_portal: the waypoint the portals name, there from the module's start (nwserver reloads the module when the
// last player leaves, so a portal back to this server arrives in a fresh module).
void main()
{
    CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", Location(GetObjectByTag("VISUAL_AREA"), Vector(30.0, 30.0, 0.0), 180.0), FALSE, "WP_PORTAL");
}
