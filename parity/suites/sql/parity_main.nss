// EE SQLite on the module database: binds, column types and coercion, errors and query lifecycle.
#include "parity_inc"

sqlquery Q(string s) { return SqlPrepareQueryObject(GetModule(), s); }
string E(sqlquery q) { return "[" + SqlGetError(q) + "]"; }

void Basics()
{
    // The module database starts empty with every server start (it only persists in saved games).
    sqlquery p = Q("SELECT count(*) FROM sqlite_master WHERE name = 'persist';");
    SqlStep(p);
    PLogI("persist.fromlastrun", SqlGetInt(p, 0));
    sqlquery q = Q("CREATE TABLE t (i INTEGER, f REAL, s TEXT, v TEXT, j TEXT, b BLOB, n);");
    PLogI("create.step", SqlStep(q));
    PLog("create.err", E(q));
    PLogI("create.step2", SqlStep(q));
    PLog("create.err2", E(q));
    q = Q("INSERT INTO t (i, f, s, v, j, n) VALUES (@i, @f, @s, @v, @j, @n);");
    SqlBindInt(q, "@i", -2147483647 - 1);
    SqlBindFloat(q, "@f", 1.1);
    SqlBindString(q, "@s", "12abc");
    SqlBindVector(q, "@v", Vector(1.5, -2.0, 3.25));
    SqlBindJson(q, "@j", JsonParse("{\"b\":1,\"a\":[1,2]}"));
    PLogI("insert.step", SqlStep(q));
    PLog("insert.err", E(q));
    SqlBindInt(q, "@i", 1);
    PLog("bind.afterstep.err", E(q));
    PLogI("bind.afterstep.step", SqlStep(q));
    q = Q("SELECT @a;");
    SqlBindInt(q, "@missing", 1);
    PLog("bind.missing.err", E(q));
    PLogI("bind.missing.step", SqlStep(q));
    PLog("bind.missing.step.err", E(q));
    q = Q("SELECT @a;");
    SqlBindInt(q, "a", 1);
    PLog("bind.noprefix.err", E(q));
    q = Q("SELECT :a, $b;");
    SqlBindInt(q, ":a", 1);
    SqlBindInt(q, "$b", 2);
    SqlStep(q);
    PLog("bind.prefixes", IntToString(SqlGetInt(q, 0)) + IntToString(SqlGetInt(q, 1)) + E(q));
    q = Q("INSERT INTO t (i, f, s) VALUES (?, ?, ?);");
    SqlBindInt(q, "?1", 1);
    PLog("bind.question.err", E(q));
    q = Q("INSERT INTO t (i, f, s, n) VALUES (@i, @f, @s, @n);");
    SqlBindInt(q, "@i", 7);
    SqlBindFloat(q, "@f", -0.5);
    SqlBindString(q, "@s", "");
    SqlStep(q);
    SqlResetQuery(q);
    SqlBindInt(q, "@i", 8);
    PLogI("reset.step", SqlStep(q));
    PLog("reset.err", E(q));
    SqlResetQuery(q, TRUE);
    PLogI("reset.clear.step", SqlStep(q));

    q = Q("SELECT i, f, s, v, j, n, b, i + 1 AS plus FROM t ORDER BY rowid;");
    PLogI("cols", SqlGetColumnCount(q));
    int c;
    for (c = 0; c <= 7; c++) PLog("colname." + IntToString(c), "[" + SqlGetColumnName(q, c) + "]");
    PLog("colname.err", E(q));
    sqlquery qc = Q("SELECT 1;");
    PLog("colname.-1", "[" + SqlGetColumnName(qc, -1) + "]");
    PLog("colname.-1.err", E(qc));
    PLogI("colname.-1.step", SqlStep(qc));
    PLogI("cols.insert", SqlGetColumnCount(Q("INSERT INTO t (i) VALUES (1);")));
    int row = 0;
    while (SqlStep(q))
    {
        string k = "row" + IntToString(row) + ".";
        for (c = 0; c < 7; c++)
        {
            string kc = k + IntToString(c);
            PLogI(kc + ".int", SqlGetInt(q, c));
            PLog(kc + ".float", FloatToString(SqlGetFloat(q, c), 0, 6));
            PLog(kc + ".string", "[" + SqlGetString(q, c) + "]");
            vector v = SqlGetVector(q, c);
            PLog(kc + ".vector", FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2));
            PLog(kc + ".json", JsonDump(SqlGetJson(q, c)));
        }
        row++;
    }
    PLogI("rows", row);
    PLogI("step.after.done", SqlStep(q));
    PLog("step.after.done.err", E(q));
    q = Q("SELECT 1, 2;");
    SqlStep(q);
    PLogI("outofrange.int", SqlGetInt(q, 99));
    PLog("outofrange.err", E(q));
    PLogI("outofrange.then", SqlGetInt(q, 1));
    PLogI("outofrange.step", SqlStep(q));
    q = Q("SELECT 1, 2;");
    SqlStep(q);
    PLog("outofrange.neg", "[" + SqlGetString(q, -1) + "]" + E(q));
}

void Errors()
{
    sqlquery q = Q("SELEC 1;");
    PLog("syntax.err", E(q));
    PLogI("syntax.step", SqlStep(q));
    PLog("syntax.step.err", E(q));
    q = Q("SELECT * FROM nosuchtable;");
    PLog("notable.err", E(q));
    q = Q("SELECT 1; SELECT 2;");
    PLog("multi.err", E(q));
    PLogI("multi.step", SqlStep(q));
    PLogI("multi.value", SqlGetInt(q, 0));
    q = Q("");
    PLog("empty.err", E(q));
    PLogI("empty.step", SqlStep(q));
    q = Q("INSERT INTO t (i) VALUES ('x'); SELECT 1;");
    PLogI("multi.insert.step", SqlStep(q));
    sqlquery u;
    PLogI("uninit.step", SqlStep(u));
    PLog("uninit.err", E(u));
    PLogI("uninit.int", SqlGetInt(u, 0));
    q = Q("SELECT 1;");
    PLogI("before.step.int", SqlGetInt(q, 0));
    PLog("before.step.err", E(q));
    q = Q("CREATE TABLE u (k TEXT PRIMARY KEY);");
    SqlStep(q);
    q = Q("INSERT INTO u VALUES ('a');");
    SqlStep(q);
    q = Q("INSERT INTO u VALUES ('a');");
    PLogI("constraint.step", SqlStep(q));
    PLog("constraint.err", E(q));
    q = SqlPrepareQueryObject(GetObjectByTag("PARITY_AREA"), "SELECT 1;");
    PLog("area.err", E(q));
    PLogI("area.step", SqlStep(q));
    q = SqlPrepareQueryObject(OBJECT_INVALID, "SELECT 1;");
    PLog("invalid.err", E(q));
}

void Values()
{
    sqlquery q = Q("SELECT 1.5, '3.9xyz', 'abc', NULL, x'414243', 9223372036854775807, -1e300, '1 2 3', '{\"a\":1}', 'not json', 0.1, 1e10, 2147483648;");
    SqlStep(q);
    int c;
    for (c = 0; c < 13; c++)
    {
        string k = "val." + IntToString(c);
        PLogI(k + ".int", SqlGetInt(q, c));
        PLog(k + ".float", FloatToString(SqlGetFloat(q, c), 0, 4));
        PLog(k + ".string", "[" + SqlGetString(q, c) + "]");
        vector v = SqlGetVector(q, c);
        PLog(k + ".vector", FloatToString(v.x, 0, 2) + "," + FloatToString(v.y, 0, 2) + "," + FloatToString(v.z, 0, 2));
        PLog(k + ".json", "[" + JsonDump(SqlGetJson(q, c)) + "] " + JsonGetError(SqlGetJson(q, c)));
    }
    q = Q("SELECT typeof(@f), @f, typeof(@v), @v, typeof(@j), @j;");
    SqlBindFloat(q, "@f", 0.1);
    SqlBindVector(q, "@v", Vector(0.1, 2.0, -3.0));
    SqlBindJson(q, "@j", JsonFloat(0.5));
    SqlStep(q);
    for (c = 0; c < 6; c++) PLog("bindval." + IntToString(c), SqlGetString(q, c));
    q = Q("SELECT json_extract(@j, '$.a'), json_type(@j);");
    SqlBindJson(q, "@j", JsonParse("{\"a\":[1,2]}"));
    SqlStep(q);
    PLog("jsonfn.extract", SqlGetString(q, 0));
    PLog("jsonfn.type", SqlGetString(q, 1));
    // Transactions left open roll back when the script ends.
    SqlStep(Q("CREATE TABLE tx (a);"));
    SqlStep(Q("BEGIN;"));
    SqlStep(Q("INSERT INTO tx VALUES (1);"));
}

void After()
{
    sqlquery q = Q("SELECT count(*) FROM tx;");
    SqlStep(q);
    PLogI("tx.rolledback", SqlGetInt(q, 0));
    PLog("tx.err", E(q));
    SqlDestroyDatabase(GetModule());
    q = Q("SELECT count(*) FROM t;");
    PLog("destroyed.err", E(q));
    SqlStep(Q("CREATE TABLE persist (a);"));
}

void main()
{
    DelayCommand(1.0, Basics());
    DelayCommand(1.1, Errors());
    DelayCommand(1.2, Values());
    DelayCommand(1.4, After());
    DelayCommand(2.0, PDone());
}
