// NWNX_SQL on SQLite: when the last error is cleared, empty queries, and where deserialized objects go.
#include "parity_inc"
#include "nwnx_sql"

void main()
{
    int nBase = NWNX_SQL_ExecuteQuery("SELECT 1");
    NWNX_SQL_ExecuteQuery("SELECT * FROM nosuch");
    NWNX_SQL_PrepareQuery("SELECT 1");
    PLog("err.prepare", NWNX_SQL_GetLastError());
    NWNX_SQL_ExecuteQuery("SELECT * FROM nosuch");
    NWNX_SQL_ExecuteQuery("SELECT 1");
    PLog("err.exec", NWNX_SQL_GetLastError());
    NWNX_SQL_ExecuteQuery("SELECT * FROM nosuch");
    NWNX_SQL_GetAffectedRows();
    NWNX_SQL_ReadyToReadNextRow();
    PLog("err.other", NWNX_SQL_GetLastError());
    NWNX_SQL_ExecuteQuery("CREATE TABLE IF NOT EXISTS k (a INTEGER PRIMARY KEY)");
    NWNX_SQL_ExecuteQuery("INSERT OR IGNORE INTO k VALUES (1)");
    NWNX_SQL_ExecuteQuery("INSERT INTO k VALUES (1)");
    NWNX_SQL_PrepareQuery("SELECT 1");
    PLog("err.step.prepare", NWNX_SQL_GetLastError());
    NWNX_SQL_ExecuteQuery("INSERT INTO k VALUES (1)");
    NWNX_SQL_PrepareQuery("SELEC");
    PLog("err.twice", NWNX_SQL_GetLastError());

    int nId = NWNX_SQL_ExecuteQuery("SELECT 1");
    PLogI("empty.exec", NWNX_SQL_ExecuteQuery(""));
    PLog("empty.err", NWNX_SQL_GetLastError());
    PLogI("empty.affected", NWNX_SQL_GetAffectedRows());
    PLogI("empty.ids", NWNX_SQL_ExecuteQuery("SELECT 1") - nId);
    nId = NWNX_SQL_ExecuteQuery("SELECT 1");
    NWNX_SQL_ExecuteQuery("INSERT INTO k VALUES (1)");
    PLogI("fail.ids", NWNX_SQL_ExecuteQuery("SELECT 1") - nId);
    nId = NWNX_SQL_ExecuteQuery("SELECT 1");
    NWNX_SQL_ExecuteQuery("SELEC");
    PLogI("badprep.ids", NWNX_SQL_ExecuteQuery("SELECT 1") - nId);
    PLogI("ids.base", nBase);

    NWNX_SQL_PrepareQuery("SELECT typeof(?), ?");
    NWNX_SQL_PreparedObjectFull(0, OBJECT_INVALID);
    NWNX_SQL_PreparedObjectFull(1, GetModule());
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    PLog("invalid.type", NWNX_SQL_ReadDataInActiveRow(0));
    PLog("module.full", NWNX_SQL_ReadDataInActiveRow(1));
    NWNX_SQL_PrepareQuery("SELECT typeof(?)");
    NWNX_SQL_PreparedObjectFull(0, GetObjectByTag("PARITY_AREA"));
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    PLog("area.type", NWNX_SQL_ReadDataInActiveRow(0));
    NWNX_SQL_PrepareQuery("SELECT length(?)");
    NWNX_SQL_PreparedObjectFull(0, GetObjectByTag("PARITY_AREA"));
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    PLog("area.len", NWNX_SQL_ReadDataInActiveRow(0));

    object oArea = GetObjectByTag("PARITY_AREA");
    object oChest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(2.0));
    object oGob = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-3.0));
    object oItem = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mpotion001", PCentre());
    object oBag = CreateItemOnObject("nw_it_contain001", oChest);
    object oPlc = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_barrel", PCentre(4.0));
    object oWp = CreateObject(OBJECT_TYPE_WAYPOINT, "nw_waypoint001", PCentre(5.0));
    NWNX_SQL_PrepareQuery("SELECT ?, ?, ?, ?");
    NWNX_SQL_PreparedObjectFull(0, oGob);
    NWNX_SQL_PreparedObjectFull(1, oItem);
    NWNX_SQL_PreparedObjectFull(2, oPlc);
    NWNX_SQL_PreparedObjectFull(3, oWp);
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    PLogI("wp.stored", NWNX_SQL_ReadDataInActiveRow(3) != "");

    object o = NWNX_SQL_ReadFullObjectInActiveRow(0);
    PLogI("gob.limbo.valid", GetIsObjectValid(o));
    PLogI("gob.limbo.area", GetArea(o) == oArea);
    vector v = GetPosition(o);
    PLog("gob.limbo.pos", FloatToString(v.x, 0, 1) + "," + FloatToString(v.y, 0, 1));
    o = NWNX_SQL_ReadFullObjectInActiveRow(0, oChest, 7.0, 8.0);
    PLogI("gob.chest.area", GetArea(o) == oArea);
    v = GetPosition(o);
    PLog("gob.chest.pos", FloatToString(v.x, 0, 1) + "," + FloatToString(v.y, 0, 1));
    o = NWNX_SQL_ReadFullObjectInActiveRow(0, oArea, 7.0, 8.0);
    v = GetPosition(o);
    PLog("gob.area.pos", FloatToString(v.x, 0, 1) + "," + FloatToString(v.y, 0, 1));
    PLogI("gob.area.hp", GetCurrentHitPoints(o) == GetCurrentHitPoints(oGob));

    o = NWNX_SQL_ReadFullObjectInActiveRow(1, oGob);
    PLogI("item.creature", GetItemPossessor(o) == oGob);
    o = NWNX_SQL_ReadFullObjectInActiveRow(1, oBag);
    PLogI("item.bag", GetItemPossessor(o) == oBag);
    PLogI("item.bag.chest", GetItemPossessor(o) == oChest);
    o = NWNX_SQL_ReadFullObjectInActiveRow(1, oArea, 9.0, 9.5, 0.0);
    v = GetPosition(o);
    PLogI("item.area", GetArea(o) == oArea);
    PLog("item.area.pos", FloatToString(v.x, 0, 1) + "," + FloatToString(v.y, 0, 1));
    o = NWNX_SQL_ReadFullObjectInActiveRow(1, oWp);
    PLogI("item.wp.valid", GetIsObjectValid(o));
    PLogI("item.wp.area", GetIsObjectValid(GetArea(o)));
    o = NWNX_SQL_ReadFullObjectInActiveRow(2, oArea, 11.0, 12.0);
    PLogI("plc.valid", GetIsObjectValid(o));
    v = GetPosition(o);
    PLog("plc.pos", FloatToString(v.x, 0, 1) + "," + FloatToString(v.y, 0, 1));
    PLog("plc.tag", GetTag(o));
    o = NWNX_SQL_ReadFullObjectInActiveRow(2);
    PLogI("plc.limbo.valid", GetIsObjectValid(o));
    PLogI("plc.limbo.area", GetIsObjectValid(GetArea(o)));
    o = NWNX_SQL_ReadFullObjectInActiveRow(3, oArea, 1.0, 2.0);
    PLogI("wp.valid", GetIsObjectValid(o));
    PLog("wp.tag", GetTag(o));
    o = NWNX_SQL_ReadFullObjectInActiveRow(7, oArea);
    PLogI("col.range.valid", GetIsObjectValid(o));
    PDone();
}
