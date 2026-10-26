// NWNX_NWSQLiteExtensions: 2DA tables in the module database, Mersenne Twister functions, databases on any object.
#include "parity_inc"
#include "nwnx_nwsqliteext"

string Query(object oDb, string sQuery, int nCols = 1)
{
    sqlquery q = SqlPrepareQueryObject(oDb, sQuery);
    string s = "";
    int n = 0;
    while (SqlStep(q) && n < 12)
    {
        int i;
        for (i = 0; i < nCols; i++) s += "[" + SqlGetString(q, i) + "]";
        s += ";";
        n++;
    }
    string sErr = SqlGetError(q);
    // The message without its context (the object's id and name differ between servers).
    int nCtx = FindSubString(sErr, " (code=");
    if (sErr != "") s += "ERR:" + (nCtx < 0 ? sErr : GetStringLeft(sErr, nCtx));
    return s;
}

void main()
{
    object oMod = GetModule();
    PLogI("create", NWNX_NWSQLiteExtensions_CreateVirtual2DATable("effecticons", "001"));
    PLog("rows", Query(oMod, "SELECT rowid, label, icon, strref, typeof(strref), typeof(label) FROM effecticons WHERE rowid < 4", 6));
    PLog("filter", Query(oMod, "SELECT label FROM effecticons WHERE label IS NOT NULL AND strref >= 8030 AND strref <= 8035"));
    PLog("count", Query(oMod, "SELECT count(*) FROM effecticons"));
    PLog("case", Query(oMod, "SELECT LABEL, Icon FROM EFFECTICONS WHERE rowid = 2", 2));
    PLog("insert", Query(oMod, "INSERT INTO effecticons (label) VALUES ('x')"));
    PLog("update", Query(oMod, "UPDATE effecticons SET label = 'y' WHERE rowid = 1"));
    PLog("after", Query(oMod, "SELECT label FROM effecticons WHERE rowid = 1"));
    PLogI("again", NWNX_NWSQLiteExtensions_CreateVirtual2DATable("effecticons", "001"));
    PLogI("named", NWNX_NWSQLiteExtensions_CreateVirtual2DATable("effecticons", "", "icons_text"));
    PLog("named.rows", Query(oMod, "SELECT strref, typeof(strref) FROM icons_text WHERE rowid = 1", 2));
    PLogI("real", NWNX_NWSQLiteExtensions_CreateVirtual2DATable("appearance", "0000022", "app"));
    PLog("real.rows", Query(oMod, "SELECT label, typeof(perspace), perspace FROM app WHERE rowid = 0", 3));
    PLogI("missing", NWNX_NWSQLiteExtensions_CreateVirtual2DATable("nosuch2da"));
    PLogI("empty", NWNX_NWSQLiteExtensions_CreateVirtual2DATable(""));
    PLogI("badhint", NWNX_NWSQLiteExtensions_CreateVirtual2DATable("effecticons", "9", "icons_bad"));
    PLog("badhint.rows", Query(oMod, "SELECT strref, typeof(strref) FROM icons_bad WHERE rowid = 1", 2));
    PLog("master", Query(oMod, "SELECT type, name FROM sqlite_master WHERE name LIKE '%icons%' OR name = 'app' ORDER BY name", 2));
    PLog("drop", Query(oMod, "DROP TABLE icons_text"));
    PLog("master2", Query(oMod, "SELECT name FROM sqlite_master WHERE name = 'icons_text'"));

    PLog("mt.seed", Query(oMod, "SELECT MT_SEED('a', 5489)"));
    PLog("mt.values", Query(oMod, "SELECT MT_VALUE('a') UNION ALL SELECT MT_VALUE('a') UNION ALL SELECT MT_VALUE('a')"));
    PLog("mt.discard", Query(oMod, "SELECT MT_DISCARD('a', 10)"));
    PLog("mt.after", Query(oMod, "SELECT MT_VALUE('a')"));
    PLog("mt.unseeded", Query(oMod, "SELECT MT_VALUE('b')"));
    PLog("mt.badname", Query(oMod, "SELECT MT_SEED('', 1), MT_VALUE(''), MT_DISCARD('', 1)", 3));
    PLog("mt.negseed", Query(oMod, "SELECT MT_SEED('c', -1)"));
    PLog("mt.negdiscard", Query(oMod, "SELECT MT_DISCARD('a', -5)"));
    PLog("mt.seed2", Query(oMod, "SELECT MT_SEED('a', 1), MT_VALUE('a')", 2));

    object oArea = GetObjectByTag("PARITY_AREA");
    object oPlc = CreateObject(OBJECT_TYPE_PLACEABLE, "plc_chest1", PCentre());
    object oGob = CreateObject(OBJECT_TYPE_CREATURE, "nw_goblina", PCentre(2.0));
    PLog("plc.create", Query(oPlc, "CREATE TABLE t (v)"));
    PLog("plc.insert", Query(oPlc, "INSERT INTO t VALUES (1)"));
    PLog("plc.read", Query(oPlc, "SELECT v FROM t"));
    PLog("gob.read", Query(oGob, "SELECT v FROM t"));
    PLog("gob.create", Query(oGob, "CREATE TABLE t (v)"));
    PLog("gob.read2", Query(oGob, "SELECT count(*) FROM t"));
    PLog("area.read", Query(oArea, "SELECT 1"));
    PLog("mod.mt", Query(oPlc, "SELECT MT_VALUE('a')"));
    object oCopy = CopyObject(oPlc, PCentre(4.0));
    PLog("copy.read", Query(oCopy, "SELECT v FROM t"));
    PDone();
}
