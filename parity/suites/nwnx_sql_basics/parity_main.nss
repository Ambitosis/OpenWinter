// NWNX_SQL on SQLite: preparing, binding, executing, reading rows, affected rows and errors.
#include "parity_inc"
#include "nwnx_sql"

void Rows(string sKey, int nCols)
{
    string s = "";
    int n = 0;
    while (NWNX_SQL_ReadyToReadNextRow())
    {
        NWNX_SQL_ReadNextRow();
        int i;
        for (i = 0; i < nCols; i++) s += "[" + NWNX_SQL_ReadDataInActiveRow(i) + "]";
        s += ";";
        n++;
        if (n > 20) break;
    }
    PLog(sKey, s);
}

void main()
{
    PLog("type", NWNX_SQL_GetDatabaseType());
    PLogI("params.none", NWNX_SQL_GetPreparedQueryParamCount());
    // GetAffectedRows before any query is not measured: NWNX returns an uninitialised value.
    PLogI("ready.none", NWNX_SQL_ReadyToReadNextRow());
    PLog("data.none", NWNX_SQL_ReadDataInActiveRow(0));
    PLog("err.none", NWNX_SQL_GetLastError());

    PLogI("drop", NWNX_SQL_ExecuteQuery("DROP TABLE IF EXISTS t"));
    PLogI("drop.affected", NWNX_SQL_GetAffectedRows());
    PLogI("create", NWNX_SQL_ExecuteQuery("CREATE TABLE t (i INTEGER, f REAL, s TEXT, o INTEGER, n TEXT)"));
    PLogI("create.affected", NWNX_SQL_GetAffectedRows());
    PLogI("create.ready", NWNX_SQL_ReadyToReadNextRow());

    PLogI("prep", NWNX_SQL_PrepareQuery("INSERT INTO t (i, f, s, o, n) VALUES (?, ?, ?, ?, ?)"));
    PLogI("prep.params", NWNX_SQL_GetPreparedQueryParamCount());
    NWNX_SQL_PreparedInt(0, 42);
    NWNX_SQL_PreparedFloat(1, 1.5);
    NWNX_SQL_PreparedString(2, "it's \"quoted\"");
    NWNX_SQL_PreparedObjectId(3, OBJECT_INVALID);
    NWNX_SQL_PreparedNULL(4);
    PLogI("exec1", NWNX_SQL_ExecutePreparedQuery());
    PLogI("exec1.affected", NWNX_SQL_GetAffectedRows());
    PLogI("exec1.ready", NWNX_SQL_ReadyToReadNextRow());
    PLogI("exec1.params", NWNX_SQL_GetPreparedQueryParamCount());
    // Run the same prepared statement again with new values.
    NWNX_SQL_PreparedInt(0, -7);
    NWNX_SQL_PreparedFloat(1, 0.1);
    NWNX_SQL_PreparedString(2, "");
    NWNX_SQL_PreparedObjectId(3, OBJECT_INVALID);
    NWNX_SQL_PreparedString(4, "x");
    PLogI("exec2", NWNX_SQL_ExecutePreparedQuery());
    PLogI("exec2.affected", NWNX_SQL_GetAffectedRows());
    // Third run without rebinding: are values kept?
    PLogI("exec3", NWNX_SQL_ExecutePreparedQuery());
    PLogI("exec3.affected", NWNX_SQL_GetAffectedRows());
    NWNX_SQL_DestroyPreparedQuery();
    PLogI("destroyed.params", NWNX_SQL_GetPreparedQueryParamCount());
    PLogI("destroyed.exec", NWNX_SQL_ExecutePreparedQuery());
    PLog("destroyed.error", NWNX_SQL_GetLastError());

    PLogI("select", NWNX_SQL_ExecuteQuery("SELECT i, f, s, o, n, typeof(o), typeof(f) FROM t ORDER BY rowid"));
    PLogI("select.affected", NWNX_SQL_GetAffectedRows());
    Rows("select.rows", 8);
    PLogI("select.after.ready", NWNX_SQL_ReadyToReadNextRow());
    PLog("select.after.data", NWNX_SQL_ReadDataInActiveRow(0));

    // Reading without calling ReadyToReadNextRow first.
    NWNX_SQL_ExecuteQuery("SELECT i FROM t ORDER BY rowid");
    PLog("noready.data0", NWNX_SQL_ReadDataInActiveRow(0));
    NWNX_SQL_ReadNextRow();
    PLog("noready.data1", NWNX_SQL_ReadDataInActiveRow(0));
    PLog("noready.col9", NWNX_SQL_ReadDataInActiveRow(9));
    PLog("noready.colneg", NWNX_SQL_ReadDataInActiveRow(-1));
    NWNX_SQL_ReadNextRow();
    PLog("noready.data2", NWNX_SQL_ReadDataInActiveRow(0));
    NWNX_SQL_ReadNextRow();
    PLog("noready.data3", NWNX_SQL_ReadDataInActiveRow(0));
    NWNX_SQL_ReadNextRow();
    PLog("noready.data4", NWNX_SQL_ReadDataInActiveRow(0));
    PLogI("noready.ready", NWNX_SQL_ReadyToReadNextRow());

    NWNX_SQL_ExecuteQuery("SELECT count(*) FROM t");
    PLogI("count.ready", NWNX_SQL_ReadyToReadNextRow());
    NWNX_SQL_ReadNextRow();
    PLog("count", NWNX_SQL_ReadDataInActiveRow(0));

    // Errors.
    PLogI("bad.prep", NWNX_SQL_PrepareQuery("SELEC 1"));
    PLog("bad.prep.error", NWNX_SQL_GetLastError());
    PLogI("bad.prep.params", NWNX_SQL_GetPreparedQueryParamCount());
    PLogI("bad.prep.exec", NWNX_SQL_ExecutePreparedQuery());
    PLog("bad.prep.exec.error", NWNX_SQL_GetLastError());
    PLogI("bad.table", NWNX_SQL_ExecuteQuery("SELECT * FROM nosuchtable"));
    PLog("bad.table.error", NWNX_SQL_GetLastError());
    PLogI("bad.affected", NWNX_SQL_GetAffectedRows());
    PLogI("bad.ready", NWNX_SQL_ReadyToReadNextRow());
    PLogI("good.after.bad", NWNX_SQL_ExecuteQuery("SELECT 1"));
    PLog("good.after.bad.error", NWNX_SQL_GetLastError());
    PLogI("constraint.create", NWNX_SQL_ExecuteQuery("CREATE TABLE IF NOT EXISTS u (k INTEGER PRIMARY KEY, v TEXT NOT NULL)"));
    NWNX_SQL_ExecuteQuery("DELETE FROM u");
    PLogI("constraint.ins1", NWNX_SQL_ExecuteQuery("INSERT INTO u VALUES (1, 'a')"));
    PLogI("constraint.ins2", NWNX_SQL_ExecuteQuery("INSERT INTO u VALUES (1, 'b')"));
    PLog("constraint.error", NWNX_SQL_GetLastError());
    PLogI("constraint.affected", NWNX_SQL_GetAffectedRows());
    PLogI("null.ins", NWNX_SQL_ExecuteQuery("INSERT INTO u VALUES (2, NULL)"));
    PLog("null.error", NWNX_SQL_GetLastError());

    // Binding out of range and with too few binds.
    PLogI("range.prep", NWNX_SQL_PrepareQuery("SELECT ?, ?"));
    PLogI("range.params", NWNX_SQL_GetPreparedQueryParamCount());
    NWNX_SQL_PreparedInt(5, 1);
    NWNX_SQL_PreparedInt(-1, 1);
    NWNX_SQL_PreparedString(1, "b");
    PLogI("range.exec", NWNX_SQL_ExecutePreparedQuery());
    PLog("range.error", NWNX_SQL_GetLastError());
    Rows("range.rows", 2);
    // Several statements in one query.
    PLogI("multi", NWNX_SQL_ExecuteQuery("SELECT 1; SELECT 2"));
    PLog("multi.error", NWNX_SQL_GetLastError());
    Rows("multi.rows", 1);

    // Updates and deletes report affected rows.
    PLogI("update", NWNX_SQL_ExecuteQuery("UPDATE t SET s = 'u' WHERE i = -7"));
    PLogI("update.affected", NWNX_SQL_GetAffectedRows());
    PLogI("update0", NWNX_SQL_ExecuteQuery("UPDATE t SET s = 'u' WHERE i = 9999"));
    PLogI("update0.affected", NWNX_SQL_GetAffectedRows());
    PLogI("delete", NWNX_SQL_ExecuteQuery("DELETE FROM t WHERE i = 42"));
    PLogI("delete.affected", NWNX_SQL_GetAffectedRows());
    PLogI("select0", NWNX_SQL_ExecuteQuery("SELECT * FROM t WHERE i = 9999"));
    PLogI("select0.affected", NWNX_SQL_GetAffectedRows());
    PLogI("select0.ready", NWNX_SQL_ReadyToReadNextRow());

    // Value formatting.
    NWNX_SQL_ExecuteQuery("SELECT 1.0, 2.5e10, 1e-7, 3.14159265358979, -0.0, 9223372036854775807, x'414243', NULL, 'abc'");
    Rows("fmt", 9);
    NWNX_SQL_PrepareQuery("SELECT ?, ?, ?, ?, ?");
    NWNX_SQL_PreparedFloat(0, 0.1);
    NWNX_SQL_PreparedFloat(1, 123456.789);
    NWNX_SQL_PreparedFloat(2, -2.0);
    NWNX_SQL_PreparedInt(3, -2147483647 - 1);
    NWNX_SQL_PreparedJson(4, JsonParse("{\"a\":[1,2]}"));
    NWNX_SQL_ExecutePreparedQuery();
    Rows("fmt.bound", 5);

    // Query ids.
    PLogI("id.a", NWNX_SQL_ExecuteQuery("SELECT 1"));
    PLogI("id.b", NWNX_SQL_ExecuteQuery("SELECT 1"));
    NWNX_SQL_PrepareQuery("SELECT 1");
    PLogI("id.c", NWNX_SQL_ExecutePreparedQuery());
    PLogI("id.d", NWNX_SQL_ExecutePreparedQuery());

    // Prepare clears the previous result set.
    NWNX_SQL_ExecuteQuery("SELECT 5 UNION SELECT 6");
    NWNX_SQL_PrepareQuery("SELECT 7");
    PLogI("prepclears.ready", NWNX_SQL_ReadyToReadNextRow());
    NWNX_SQL_ExecuteQuery("DROP TABLE t");
    NWNX_SQL_ExecuteQuery("DROP TABLE u");
    PDone();
}
