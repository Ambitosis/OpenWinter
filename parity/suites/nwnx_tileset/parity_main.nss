// nwnx_tileset: NWNX_Tileset readings of the .set files (general data, terrains, crossers, groups, tiles, doors),
// out-of-range answers, and tile overrides applied to areas made with CreateArea.
#include "parity_inc"
#include "nwnx_tileset"
#include "nwnx_area"

void Data(string p, string sSet)
{
    struct NWNX_Tileset_TilesetData d = NWNX_Tileset_GetTilesetData(sSet);
    PLog(p + ".data", IntToString(d.nNumTileData) + " ht=" + FloatToString(d.fHeightTransition, 0, 2) + " terr=" + IntToString(d.nNumTerrain)
        + " cross=" + IntToString(d.nNumCrossers) + " groups=" + IntToString(d.nNumGroups) + " border=" + d.sBorderTerrain
        + " default=" + d.sDefaultTerrain + " floor=" + d.sFloorTerrain + " name=" + IntToString(d.nDisplayNameStrRef)
        + " unloc=" + d.sUnlocalizedName + " int=" + IntToString(d.bInterior) + " hasht=" + IntToString(d.bHasHeightTransition));
}

void Tile(string p, string sSet, int nTile)
{
    PLog(p + ".model", NWNX_Tileset_GetTileModel(sSet, nTile));
    PLog(p + ".minimap", NWNX_Tileset_GetTileMinimapTexture(sSet, nTile));
    struct NWNX_Tileset_TileEdgesAndCorners e = NWNX_Tileset_GetTileEdgesAndCorners(sSet, nTile);
    PLog(p + ".edges", e.sTopLeft + "|" + e.sTop + "|" + e.sTopRight + "|" + e.sRight + "|" + e.sBottomRight + "|" + e.sBottom
        + "|" + e.sBottomLeft + "|" + e.sLeft);
    int nDoors = NWNX_Tileset_GetTileNumDoors(sSet, nTile);
    PLogI(p + ".doors", nDoors);
    int i;
    for (i = 0; i <= nDoors; i++)
    {
        struct NWNX_Tileset_TileDoorData d = NWNX_Tileset_GetTileDoorData(sSet, nTile, i);
        PLog(p + ".door" + IntToString(i), IntToString(d.nType) + " " + FloatToString(d.fX, 0, 2) + "," + FloatToString(d.fY, 0, 2)
            + "," + FloatToString(d.fZ, 0, 2) + " o" + FloatToString(d.fOrientation, 0, 2));
    }
}

void Group(string p, string sSet, int nGroup)
{
    struct NWNX_Tileset_TilesetGroupData g = NWNX_Tileset_GetTilesetGroupData(sSet, nGroup);
    string s = g.sName + " " + IntToString(g.nStrRef) + " " + IntToString(g.nRows) + "x" + IntToString(g.nColumns) + ":";
    int i;
    for (i = 0; i <= g.nRows * g.nColumns; i++) s += " " + IntToString(NWNX_Tileset_GetTilesetGroupTile(sSet, nGroup, i));
    PLog(p, s);
}

string AreaTiles(object a)
{
    string s = "";
    int n = GetAreaSize(AREA_WIDTH, a) * GetAreaSize(AREA_HEIGHT, a);
    int i;
    for (i = 0; i < n; i++)
    {
        struct NWNX_Area_TileInfo t = NWNX_Area_GetTileInfoByTileIndex(a, i);
        s += IntToString(t.nID) + "/" + IntToString(t.nOrientation) + "/" + IntToString(t.nHeight) + " ";
    }
    return s;
}

void Custom(string sOverride, int nIndex, int nTile, int nOrient, int nHeight, int bLoop1)
{
    struct NWNX_Tileset_CustomTileData d;
    d.nTileID = nTile;
    d.nOrientation = nOrient;
    d.nHeight = nHeight;
    d.nMainLightColor1 = 5;
    d.nMainLightColor2 = 6;
    d.nSourceLightColor1 = 7;
    d.nSourceLightColor2 = 8;
    d.bAnimLoop1 = bLoop1;
    d.bAnimLoop2 = TRUE;
    d.bAnimLoop3 = FALSE;
    NWNX_Tileset_SetOverrideTileData(sOverride, nIndex, d);
}

void Created(string p, object a)
{
    PLogI(p + ".valid", GetIsObjectValid(a));
    PLog(p + ".tileset", GetTilesetResRef(a));
    PLog(p + ".size", IntToString(GetAreaSize(AREA_WIDTH, a)) + "x" + IntToString(GetAreaSize(AREA_HEIGHT, a)));
    PLog(p + ".tiles", AreaTiles(a));
    PLog(p + ".loops", IntToString(NWNX_Area_GetTileAnimationLoop(a, 5.0, 5.0, 1)) + IntToString(NWNX_Area_GetTileAnimationLoop(a, 5.0, 5.0, 2))
        + IntToString(NWNX_Area_GetTileAnimationLoop(a, 5.0, 5.0, 3)));
    PLogI(p + ".mainlight1", GetTileMainLight1Color(Location(a, Vector(5.0, 5.0, 0.0), 0.0)));
    PLogI(p + ".srclight2", GetTileSourceLight2Color(Location(a, Vector(5.0, 5.0, 0.0), 0.0)));
}

void main()
{
    Data("ttr01", "ttr01");
    Data("tde01", "tde01");
    Data("TTR01up", "TTR01");
    Data("bad", "nosuchset");
    PLog("ttr01.terrain0", NWNX_Tileset_GetTilesetTerrain("ttr01", 0));
    PLog("ttr01.terrain2", NWNX_Tileset_GetTilesetTerrain("ttr01", 2));
    PLog("ttr01.terrain3", NWNX_Tileset_GetTilesetTerrain("ttr01", 3));
    PLog("ttr01.terrainneg", NWNX_Tileset_GetTilesetTerrain("ttr01", -1));
    PLog("ttr01.crosser0", NWNX_Tileset_GetTilesetCrosser("ttr01", 0));
    PLog("ttr01.crosser3", NWNX_Tileset_GetTilesetCrosser("ttr01", 3));
    PLog("ttr01.crosser4", NWNX_Tileset_GetTilesetCrosser("ttr01", 4));
    PLog("bad.terrain0", NWNX_Tileset_GetTilesetTerrain("nosuchset", 0));
    Group("ttr01.group0", "ttr01", 0);
    Group("ttr01.group3", "ttr01", 3);
    Group("ttr01.group66", "ttr01", 66);
    Group("ttr01.group67", "ttr01", 67);
    Group("ttr01.groupneg", "ttr01", -1);
    PLogI("ttr01.grouptileneg", NWNX_Tileset_GetTilesetGroupTile("ttr01", 3, -1));
    Tile("ttr01.t35", "ttr01", 35);
    Tile("ttr01.t60", "ttr01", 60);
    Tile("ttr01.t0", "ttr01", 0);
    Tile("ttr01.t282", "ttr01", 282);
    Tile("ttr01.t283", "ttr01", 283);
    Tile("ttr01.tneg", "ttr01", -1);
    Tile("tde01.t4", "tde01", 4);
    Tile("tde01.t121", "tde01", 121);
    Tile("bad.t0", "nosuchset", 0);

    // Overrides: a 3x3 rural override on the 2x2 "tovr" area.
    NWNX_Tileset_CreateTileOverride("ovr1", "ttr01", 3, 3);
    Custom("ovr1", 0, 35, 2, 0, TRUE);
    Custom("ovr1", 1, 36, 3, 1, FALSE);
    Custom("ovr1", 4, 118, 1, 0, TRUE);
    Custom("ovr1", 9, 35, 0, 0, TRUE);
    Custom("ovr1", -1, 35, 0, 0, TRUE);
    Custom("nosuchovr", 0, 35, 0, 0, TRUE);
    NWNX_Tileset_SetAreaTileOverride("tovr", "ovr1");
    Created("ovr", CreateArea("tovr", "TOVR_A"));
    // Removing one tile's data and all of it.
    NWNX_Tileset_DeleteOverrideTileData("ovr1", 4);
    Created("ovr.del4", CreateArea("tovr", "TOVR_B"));
    NWNX_Tileset_DeleteOverrideTileData("ovr1", -1);
    Created("ovr.delall", CreateArea("tovr", "TOVR_C"));
    // An override on another tileset, then removed with "".
    NWNX_Tileset_CreateTileOverride("ovr2", "tde01", 2, 1);
    Custom("ovr2", 1, 121, 1, 0, TRUE);
    NWNX_Tileset_SetAreaTileOverride("tovr", "ovr2");
    Created("ovr2", CreateArea("tovr", "TOVR_D"));
    NWNX_Tileset_DeleteTileOverride("ovr2");
    Created("ovr2.deleted", CreateArea("tovr", "TOVR_E"));
    NWNX_Tileset_SetAreaTileOverride("tovr", "");
    Created("ovr.cleared", CreateArea("tovr", "TOVR_F"));
    // Sizes out of range, and an override on the module's own area (not created with CreateArea).
    NWNX_Tileset_CreateTileOverride("ovr3", "ttr01", 40, 0);
    NWNX_Tileset_SetAreaTileOverride("tovr", "ovr3");
    Created("ovr3", CreateArea("tovr", "TOVR_G"));
    NWNX_Tileset_SetAreaTileOverride("tovr", "");
    PLog("orig.tiles", AreaTiles(GetObjectByTag("TOVR")));
    PDone();
}
