// World presentation test: the tile north of the caller becomes a plain cobblestone tile.
void main()
{
    object oArea = GetArea(OBJECT_SELF);
    vector v = GetPosition(OBJECT_SELF);
    location lTile = Location(oArea, Vector(v.x, v.y + 10.0, v.z), 0.0);
    SetTile(lTile, 0, 0, GetTileHeight(lTile), SETTILE_FLAG_RELOAD_GRASS | SETTILE_FLAG_RECOMPUTE_LIGHTING);
}
