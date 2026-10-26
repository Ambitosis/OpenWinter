// nwnx_area_tiles: NWNX_Area tile readings (GetTileInfo, GetTileInfoByTileIndex, GetTileModelResRef, the
// animation loops), what coordinates they take, and their answers outside the area; GetPathExists.
// (TestDirectLine is left out: on this generated area it crashed nwserver.)
#include "parity_inc"
#include "nwnx_area"

string Info(struct NWNX_Area_TileInfo t)
{
    return IntToString(t.nID) + " h" + IntToString(t.nHeight) + " o" + IntToString(t.nOrientation)
        + " x" + IntToString(t.nGridX) + " y" + IntToString(t.nGridY);
}

void Tile(string p, object a, float x, float y)
{
    PLog(p + ".info", Info(NWNX_Area_GetTileInfo(a, x, y)));
    PLog(p + ".model", NWNX_Area_GetTileModelResRef(a, x, y));
    PLog(p + ".loops", IntToString(NWNX_Area_GetTileAnimationLoop(a, x, y, 1)) + IntToString(NWNX_Area_GetTileAnimationLoop(a, x, y, 2))
        + IntToString(NWNX_Area_GetTileAnimationLoop(a, x, y, 3)) + " l0=" + IntToString(NWNX_Area_GetTileAnimationLoop(a, x, y, 0))
        + " l4=" + IntToString(NWNX_Area_GetTileAnimationLoop(a, x, y, 4)));
}

void Path(string p, object a, vector v1, vector v2, int nDepth)
{
    PLogI(p, NWNX_Area_GetPathExists(a, v1, v2, nDepth));
}

void main()
{
    object a = GetObjectByTag("PARITY_AREA");
    object b = GetObjectByTag("PARITY2");
    // Different tiles in area b so positions can be told apart.
    SetTile(Location(b, Vector(15.0, 5.0, 0.0), 0.0), 35, 2, 0);
    SetTile(Location(b, Vector(5.0, 15.0, 0.0), 0.0), 36, 3, 1);
    SetTile(Location(b, Vector(25.0, 25.0, 0.0), 0.0), 118, 1, 0);
    Tile("b.m15_5", b, 15.0, 5.0);
    Tile("b.m5_15", b, 5.0, 15.0);
    Tile("b.m25_25", b, 25.0, 25.0);
    Tile("b.m0_0", b, 0.0, 0.0);
    Tile("b.g1_0", b, 1.0, 0.0);
    Tile("b.m29_9", b, 29.9, 9.9);
    Tile("b.m30_5", b, 30.0, 5.0);
    Tile("b.mneg", b, -1.0, 5.0);
    Tile("a.m15_15", a, 15.0, 15.0);
    Tile("inv", OBJECT_INVALID, 5.0, 5.0);
    int i;
    for (i = -1; i <= 9; i++) PLog("b.idx" + IntToString(i), Info(NWNX_Area_GetTileInfoByTileIndex(b, i)));
    PLog("a.idx15", Info(NWNX_Area_GetTileInfoByTileIndex(a, 15)));
    PLog("a.idx16", Info(NWNX_Area_GetTileInfoByTileIndex(a, 16)));
    PLog("inv.idx0", Info(NWNX_Area_GetTileInfoByTileIndex(OBJECT_INVALID, 0)));

    // Animation loops set through NWNX, read back through NWNX.
    NWNX_Area_SetTileAnimationLoop(b, 15.0, 5.0, 2, FALSE);
    NWNX_Area_SetTileAnimationLoop(b, 25.0, 25.0, 1, FALSE);
    NWNX_Area_SetTileAnimationLoop(b, 25.0, 25.0, 3, 7);
    NWNX_Area_SetTileAnimationLoop(b, 5.0, 5.0, 4, FALSE);
    NWNX_Area_SetTileAnimationLoop(b, 5.0, 5.0, 0, FALSE);
    Tile("set.m15_5", b, 15.0, 5.0);
    Tile("set.m25_25", b, 25.0, 25.0);
    Tile("set.m5_5", b, 5.0, 5.0);
    SetTileAnimationLoops(Location(b, Vector(5.0, 25.0, 0.0), 0.0), FALSE, TRUE, FALSE);
    Tile("native.m5_25", b, 5.0, 25.0);

    Path("path.a", a, Vector(5.0, 5.0, 0.0), Vector(35.0, 35.0, 0.0), 16);
    Path("path.a.depth1", a, Vector(5.0, 5.0, 0.0), Vector(35.0, 35.0, 0.0), 1);
    Path("path.a.depth0", a, Vector(5.0, 5.0, 0.0), Vector(35.0, 35.0, 0.0), 0);
    Path("path.a.same", a, Vector(5.0, 5.0, 0.0), Vector(6.0, 6.0, 0.0), 1);
    Path("path.a.out", a, Vector(5.0, 5.0, 0.0), Vector(45.0, 5.0, 0.0), 16);
    Path("path.b", b, Vector(5.0, 5.0, 0.0), Vector(25.0, 25.0, 0.0), 9);
    Path("path.inv", OBJECT_INVALID, Vector(5.0, 5.0, 0.0), Vector(6.0, 6.0, 0.0), 9);
    PDone();
}
