// World presentation test: red main lights and pale red source lights on the caller's tile.
void main()
{
    object oArea = GetArea(OBJECT_SELF);
    vector v = GetPosition(OBJECT_SELF);
    location lTile = Location(oArea, Vector(IntToFloat(FloatToInt(v.x / 10.0)), IntToFloat(FloatToInt(v.y / 10.0)), 0.0), 0.0);
    SetTileMainLightColor(lTile, TILE_MAIN_LIGHT_COLOR_RED, TILE_MAIN_LIGHT_COLOR_RED);
    SetTileSourceLightColor(lTile, TILE_SOURCE_LIGHT_COLOR_PALE_RED, TILE_SOURCE_LIGHT_COLOR_PALE_RED);
    SetAreaLightColor(AREA_LIGHT_COLOR_MOON_AMBIENT, 0x402020, oArea);
}
