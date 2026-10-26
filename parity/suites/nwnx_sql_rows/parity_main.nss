// NWNX_SQL on SQLite: the active row across queries, bound value types, error persistence and serialized objects.
#include "parity_inc"
#include "nwnx_sql"

void main()
{
    NWNX_SQL_ExecuteQuery("DROP TABLE IF EXISTS r");
    NWNX_SQL_ExecuteQuery("CREATE TABLE r (v)");
    NWNX_SQL_ExecuteQuery("INSERT INTO r VALUES ('a'), ('b'), ('c')");
    PLogI("multi.affected", NWNX_SQL_GetAffectedRows());

    NWNX_SQL_ExecuteQuery("SELECT v FROM r ORDER BY v");
    NWNX_SQL_ReadNextRow();
    PLog("row1", NWNX_SQL_ReadDataInActiveRow(0));
    NWNX_SQL_ReadNextRow();
    NWNX_SQL_ReadNextRow();
    PLog("row3", NWNX_SQL_ReadDataInActiveRow(0));
    NWNX_SQL_ReadNextRow();
    PLog("pastend", NWNX_SQL_ReadDataInActiveRow(0));
    // A new query: the previous active row before reading.
    NWNX_SQL_ExecuteQuery("SELECT 'x', 'y'");
    PLog("stale0", NWNX_SQL_ReadDataInActiveRow(0));
    PLog("stale1", NWNX_SQL_ReadDataInActiveRow(1));
    NWNX_SQL_ReadNextRow();
    PLog("fresh1", NWNX_SQL_ReadDataInActiveRow(1));
    // A non-row query keeps the active row too?
    NWNX_SQL_ExecuteQuery("UPDATE r SET v = v");
    PLog("afterupdate", NWNX_SQL_ReadDataInActiveRow(0));
    NWNX_SQL_PrepareQuery("SELECT 1");
    PLog("afterprepare", NWNX_SQL_ReadDataInActiveRow(0));
    NWNX_SQL_DestroyPreparedQuery();
    PLog("afterdestroy", NWNX_SQL_ReadDataInActiveRow(0));
    NWNX_SQL_ExecuteQuery("SELECT 1 WHERE 0");
    PLog("afterempty", NWNX_SQL_ReadDataInActiveRow(0));
    PLogI("afterempty.ready", NWNX_SQL_ReadyToReadNextRow());
    NWNX_SQL_ExecuteQuery("SELEC");
    PLog("afterfail", NWNX_SQL_ReadDataInActiveRow(0));

    // How values are bound.
    NWNX_SQL_PrepareQuery("SELECT typeof(?), typeof(?), typeof(?), typeof(?), typeof(?), typeof(?), ?");
    NWNX_SQL_PreparedInt(0, 5);
    NWNX_SQL_PreparedFloat(1, 2.5);
    NWNX_SQL_PreparedString(2, "s");
    NWNX_SQL_PreparedObjectId(3, OBJECT_SELF);
    NWNX_SQL_PreparedNULL(4);
    NWNX_SQL_PreparedJson(5, JsonInt(3));
    NWNX_SQL_PreparedObjectId(6, GetObjectByTag("PARITY_AREA"));
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    int i;
    for (i = 0; i < 6; i++) PLog("bindtype." + IntToString(i), NWNX_SQL_ReadDataInActiveRow(i));
    PLogI("areaid.matches", NWNX_SQL_ReadDataInActiveRow(6) == IntToString(StringToInt(ObjectToString(GetObjectByTag("PARITY_AREA")))));

    // Rebinding one value with another type.
    NWNX_SQL_PrepareQuery("SELECT typeof(?)");
    NWNX_SQL_PreparedInt(0, 1);
    NWNX_SQL_PreparedString(0, "z");
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    PLog("rebind", NWNX_SQL_ReadDataInActiveRow(0));

    // Floats as text.
    NWNX_SQL_PrepareQuery("SELECT ?, ?, ?, ?");
    NWNX_SQL_PreparedFloat(0, 123456789012.0);
    NWNX_SQL_PreparedFloat(1, 0.0000001);
    NWNX_SQL_PreparedFloat(2, -0.0);
    NWNX_SQL_PreparedFloat(3, 3.0);
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    for (i = 0; i < 4; i++) PLog("float." + IntToString(i), NWNX_SQL_ReadDataInActiveRow(i));

    // Text bytes go through as they are (no USE_UTF8).
    NWNX_SQL_PrepareQuery("SELECT hex(?), length(?)");
    NWNX_SQL_PreparedString(0, "caf" + GetSubString("\xE9", 0, 1));
    NWNX_SQL_PreparedString(1, "abc");
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    PLog("bytes.hex", NWNX_SQL_ReadDataInActiveRow(0));
    PLog("bytes.len", NWNX_SQL_ReadDataInActiveRow(1));
    NWNX_SQL_ExecuteQuery("SELECT char(233), x'E9'");
    NWNX_SQL_ReadNextRow();
    PLogI("utf8.len", GetStringLength(NWNX_SQL_ReadDataInActiveRow(0)));
    PLogI("raw.len", GetStringLength(NWNX_SQL_ReadDataInActiveRow(1)));
    PLogI("raw.e9", NWNX_SQL_ReadDataInActiveRow(1) == "\xE9");

    // The last error: kept until the next query?
    NWNX_SQL_ExecuteQuery("SELECT * FROM nosuch");
    PLog("err1", NWNX_SQL_GetLastError());
    PLogI("err1.again", NWNX_SQL_GetLastError() != "");
    NWNX_SQL_PrepareQuery("SELECT 1");
    PLog("err.afterprepare", NWNX_SQL_GetLastError());
    NWNX_SQL_ExecuteQuery("SELECT * FROM nosuch");
    NWNX_SQL_DestroyPreparedQuery();
    PLog("err.afterdestroy", NWNX_SQL_GetLastError());
    NWNX_SQL_ExecutePreparedQuery();
    PLog("err.afternoprep", NWNX_SQL_GetLastError());
    NWNX_SQL_ExecuteQuery("CREATE TABLE IF NOT EXISTS k (a INTEGER PRIMARY KEY)");
    NWNX_SQL_ExecuteQuery("INSERT OR IGNORE INTO k VALUES (1)");
    NWNX_SQL_ExecuteQuery("INSERT INTO k VALUES (1)");
    PLog("err.step", NWNX_SQL_GetLastError());
    PLogI("err.step.ready", NWNX_SQL_ReadyToReadNextRow());
    NWNX_SQL_PrepareQuery("SELECT 2");
    PLog("err.step.afterprepare", NWNX_SQL_GetLastError());
    NWNX_SQL_ExecutePreparedQuery();
    PLog("err.step.afterexec", NWNX_SQL_GetLastError());
    PLogI("empty.prep", NWNX_SQL_PrepareQuery(""));
    PLog("empty.err", NWNX_SQL_GetLastError());
    PLogI("empty.params", NWNX_SQL_GetPreparedQueryParamCount());
    PLogI("empty.exec", NWNX_SQL_ExecutePreparedQuery());
    PLogI("space.prep", NWNX_SQL_PrepareQuery("   "));
    PLogI("comment.prep", NWNX_SQL_PrepareQuery("-- nothing"));
    PLogI("comment.exec", NWNX_SQL_ExecutePreparedQuery());
    PLogI("named.prep", NWNX_SQL_PrepareQuery("SELECT :a, ?2, @b, $c, ?"));
    PLogI("named.params", NWNX_SQL_GetPreparedQueryParamCount());
    NWNX_SQL_PreparedString(0, "p0");
    NWNX_SQL_PreparedString(1, "p1");
    NWNX_SQL_PreparedString(2, "p2");
    NWNX_SQL_PreparedString(3, "p3");
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    string s = "";
    for (i = 0; i < 5; i++) s += "[" + NWNX_SQL_ReadDataInActiveRow(i) + "]";
    PLog("named.row", s);

    // Serialized objects.
    object oItem = CreateObject(OBJECT_TYPE_ITEM, "nw_it_mpotion001", PCentre());
    SetLocalInt(oItem, "marker", 77);
    SetName(oItem, "Stored Potion");
    NWNX_SQL_ExecuteQuery("CREATE TABLE IF NOT EXISTS o (b64 TEXT, bin BLOB)");
    NWNX_SQL_ExecuteQuery("DELETE FROM o");
    NWNX_SQL_PrepareQuery("INSERT INTO o VALUES (?, ?)");
    NWNX_SQL_PreparedObjectFull(0, oItem);
    NWNX_SQL_PreparedObjectFull(1, oItem, FALSE);
    PLogI("obj.insert", NWNX_SQL_ExecutePreparedQuery() > 0);
    NWNX_SQL_ExecuteQuery("SELECT typeof(b64), typeof(bin), substr(b64, 1, 8), hex(substr(bin, 1, 8)), b64, bin FROM o");
    NWNX_SQL_ReadNextRow();
    PLog("obj.type64", NWNX_SQL_ReadDataInActiveRow(0));
    PLog("obj.typebin", NWNX_SQL_ReadDataInActiveRow(1));
    PLog("obj.head64", NWNX_SQL_ReadDataInActiveRow(2));
    PLog("obj.headbin", NWNX_SQL_ReadDataInActiveRow(3));
    object oChest = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre(2.0));
    object oArea = GetObjectByTag("PARITY_AREA");
    object o1 = NWNX_SQL_ReadFullObjectInActiveRow(4, oChest);
    PLogI("obj.inchest", GetItemPossessor(o1) == oChest);
    PLog("obj.name", GetName(o1));
    PLogI("obj.local", GetLocalInt(o1, "marker"));
    PLog("obj.tag", GetTag(o1));
    PLogI("obj.new", o1 != oItem);
    object o2 = NWNX_SQL_ReadFullObjectInActiveRow(5, oArea, 3.0, 4.0, 0.0, FALSE);
    PLogI("obj.bin.valid", GetIsObjectValid(o2));
    PLogI("obj.bin.inarea", GetArea(o2) == oArea);
    vector v = GetPosition(o2);
    PLogF("obj.bin.x", v.x);
    PLogF("obj.bin.y", v.y);
    object o3 = NWNX_SQL_ReadFullObjectInActiveRow(4);
    PLogI("obj.limbo.valid", GetIsObjectValid(o3));
    PLogI("obj.limbo.area", GetIsObjectValid(GetArea(o3)));
    PLogI("obj.limbo.possessor", GetIsObjectValid(GetItemPossessor(o3)));
    object o4 = NWNX_SQL_ReadFullObjectInActiveRow(2);
    PLogI("obj.bad.valid", GetIsObjectValid(o4));
    // Reading base64 text as binary crashes NWNX's server, so it is not measured.
    // A creature.
    object oGob = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(-3.0));
    SetLocalString(oGob, "who", "gob");
    NWNX_SQL_PrepareQuery("SELECT ?");
    NWNX_SQL_PreparedObjectFull(0, oGob);
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    object oGob2 = NWNX_SQL_ReadFullObjectInActiveRow(0, oArea, 6.0, 6.0, 0.0);
    PLogI("gob.valid", GetIsObjectValid(oGob2));
    PLogI("gob.type", GetObjectType(oGob2));
    PLog("gob.local", GetLocalString(oGob2, "who"));
    PLogI("gob.inarea", GetArea(oGob2) == oArea);
    object oGob3 = NWNX_SQL_ReadFullObjectInActiveRow(0, oChest);
    PLogI("gob.inchest.valid", GetIsObjectValid(oGob3));
    PLogI("gob.inchest.area", GetArea(oGob3) == oArea);
    NWNX_SQL_PrepareQuery("SELECT ?");
    NWNX_SQL_PreparedObjectFull(0, OBJECT_INVALID);
    NWNX_SQL_ExecutePreparedQuery();
    NWNX_SQL_ReadNextRow();
    PLog("invalid.full", NWNX_SQL_ReadDataInActiveRow(0));
    NWNX_SQL_ExecuteQuery("DROP TABLE r");
    NWNX_SQL_ExecuteQuery("DROP TABLE k");
    NWNX_SQL_ExecuteQuery("DROP TABLE o");
    PDone();
}
