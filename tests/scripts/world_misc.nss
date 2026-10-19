// Area properties, weather, fog, visual transforms, traps (run on a creature in an area).
void main()
{
    object m = GetModule();
    object oArea = GetArea(OBJECT_SELF);
    SetLocalInt(m, "interior", GetIsAreaInterior(oArea));
    SetLocalInt(m, "natural", GetIsAreaNatural(oArea));
    SetLocalInt(m, "natural_bad", GetIsAreaNatural(OBJECT_INVALID));
    SetLocalInt(m, "width", GetAreaSize(AREA_WIDTH));
    SetLocalInt(m, "height", GetAreaSize(AREA_HEIGHT, oArea));
    SetLocalString(m, "tileset", GetTilesetResRef(oArea));
    SetWeather(oArea, WEATHER_SNOW);
    SetLocalInt(m, "weather", GetWeather(oArea));
    SetFogAmount(FOG_TYPE_ALL, 40);
    SetFogAmount(FOG_TYPE_MOON, 10, OBJECT_SELF);
    SetLocalInt(m, "fog_sun", GetFogAmount(FOG_TYPE_SUN));
    SetLocalInt(m, "fog_moon", GetFogAmount(FOG_TYPE_MOON, oArea));
    SetSkyBox(SKYBOX_DESERT_CLEAR);
    SetLocalInt(m, "skybox", GetSkyBox(oArea));
    SetAreaNoRestFlag(TRUE);
    SetLocalInt(m, "norest", GetAreaNoRestFlag(oArea));
    SetLocalFloat(m, "vt_default", GetObjectVisualTransform(OBJECT_SELF, OBJECT_VISUAL_TRANSFORM_SCALE));
    SetLocalFloat(m, "vt_old", SetObjectVisualTransform(OBJECT_SELF, OBJECT_VISUAL_TRANSFORM_SCALE, 2.5));
    SetLocalFloat(m, "vt_new", GetObjectVisualTransform(OBJECT_SELF, OBJECT_VISUAL_TRANSFORM_SCALE));
    SetLocalInt(m, "vt_clear", ClearObjectVisualTransform(OBJECT_SELF));
    SetLocalInt(m, "vt_clear_again", ClearObjectVisualTransform(OBJECT_SELF));
    SetLocalInt(m, "music_day", MusicBackgroundGetDayTrack(oArea));
    MusicBackgroundChangeDay(oArea, 7);
    SetLocalInt(m, "music_day2", MusicBackgroundGetDayTrack(oArea));
    location lTrap = Location(oArea, GetPosition(OBJECT_SELF) + Vector(2.0, 0.0, 0.0), 0.0);
    object oTrap = CreateTrapAtLocation(TRAP_BASE_TYPE_MINOR_SPIKE, lTrap, 3.0, "WORLD_TRAP", STANDARD_FACTION_HOSTILE, "", "world_evt");
    SetLocalObject(m, "trap", oTrap);
    SetLocalInt(m, "trapped", GetIsTrapped(oTrap));
    SetLocalInt(m, "trap_type", GetTrapBaseType(oTrap));
    SetLocalInt(m, "trap_detected", GetTrapDetectedBy(oTrap, OBJECT_SELF));
    SetTrapDetectedBy(oTrap, OBJECT_SELF);
    SetLocalInt(m, "trap_detected2", GetTrapDetectedBy(oTrap, OBJECT_SELF));
    SetLocalObject(m, "nearest_trap", GetNearestTrapToObject(OBJECT_SELF));
    SetLocalObject(m, "last_detected", GetLastTrapDetected());
    SetLocalInt(m, "explored_before", GetTileExplored(OBJECT_SELF, oArea, 0, 0));
    SetLocalInt(m, "explored_set", SetTileExplored(OBJECT_SELF, oArea, 0, 0, TRUE));
    SetLocalInt(m, "explored_after", GetTileExplored(OBJECT_SELF, oArea, 0, 0));
    SetLocalInt(m, "explored_bad", GetTileExplored(OBJECT_SELF, oArea, 999, 0));
    location lStart = GetStartingLocation();
    SetLocalInt(m, "start_valid", GetIsObjectValid(GetAreaFromLocation(lStart)));
}
