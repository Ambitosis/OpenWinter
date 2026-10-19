// SQLite access: module, campaign and character databases.
void main()
{
    object oMod = GetModule();
    object oPC = GetLocalObject(oMod, "pc");
    object oItem = GetLocalObject(oMod, "item");
    object oArea = GetLocalObject(oMod, "area");

    sqlquery q = SqlPrepareQueryObject(oMod, "CREATE TABLE IF NOT EXISTS t (id INTEGER PRIMARY KEY, name TEXT, f REAL, v TEXT, j TEXT, o BLOB)");
    SqlStep(q);
    SetLocalString(oMod, "create_err", SqlGetError(q));

    q = SqlPrepareQueryObject(oMod, "INSERT INTO t (name, f, v, j, o) VALUES (@name, @f, @v, @j, @o)");
    SqlBindString(q, "@name", "alpha");
    SqlBindFloat(q, "@f", 2.5);
    SqlBindVector(q, "@v", Vector(1.0, 2.0, 3.0));
    SqlBindJson(q, "@j", JsonParse("[1,\"x\"]"));
    SqlBindObject(q, "@o", oItem);
    SetLocalInt(oMod, "insert_step", SqlStep(q));
    SetLocalString(oMod, "insert_err", SqlGetError(q));
    SqlResetQuery(q);
    SqlBindString(q, "@name", "beta");
    SqlStep(q);
    SqlResetQuery(q, TRUE);
    SqlBindString(q, "@name", "gamma");
    SqlStep(q);

    q = SqlPrepareQueryObject(oMod, "SELECT id, name AS label, f, v, j, o FROM t ORDER BY id");
    SetLocalInt(oMod, "cols", SqlGetColumnCount(q));
    SetLocalString(oMod, "col1", SqlGetColumnName(q, 1));
    // An error sticks to its query (as in the original), so try the bad column on a query of its own.
    sqlquery qbad = SqlPrepareQueryObject(oMod, "SELECT id FROM t");
    SetLocalString(oMod, "colbad", SqlGetColumnName(qbad, 9));
    SetLocalString(oMod, "colbad_err", SqlGetError(qbad));
    SetLocalInt(oMod, "norow", SqlGetInt(q, 0));
    SetLocalString(oMod, "norow_err", SqlGetError(q));
    int n = 0;
    string names = "";
    while (SqlStep(q))
    {
        n++;
        names += SqlGetString(q, 1) + ";";
        if (n == 1)
        {
            SetLocalInt(oMod, "id", SqlGetInt(q, 0));
            SetLocalFloat(oMod, "f", SqlGetFloat(q, 2));
            vector v = SqlGetVector(q, 3);
            SetLocalFloat(oMod, "vy", v.y);
            SetLocalString(oMod, "j", JsonDump(SqlGetJson(q, 4)));
            object oCopy = SqlGetObject(q, 5, Location(oArea, Vector(5.0, 5.0, 0.0), 0.0), oPC);
            SetLocalObject(oMod, "sqlcopy", oCopy);
            SetLocalInt(oMod, "badindex", SqlGetInt(q, 17));
            SetLocalString(oMod, "badindex_err", SqlGetError(q));
            SqlGetString(q, 1);
            SetLocalString(oMod, "ok_err", SqlGetError(q));
        }
        if (n == 3)
        {
            SetLocalInt(oMod, "cleared_f_null", SqlGetFloat(q, 2) == 0.0);
            SetLocalString(oMod, "cleared_v", SqlGetString(q, 3));
        }
    }
    SetLocalInt(oMod, "rows", n);
    SetLocalString(oMod, "names", names);

    q = SqlPrepareQueryObject(oMod, "SELEC nonsense");
    SetLocalString(oMod, "syntax_err", SqlGetError(q));
    SetLocalInt(oMod, "syntax_step", SqlStep(q));
    q = SqlPrepareQueryObject(oMod, "SELECT * FROM missing_table");
    SetLocalString(oMod, "missing_err", SqlGetError(q));
    q = SqlPrepareQueryObject(oItem, "SELECT 1");
    SetLocalString(oMod, "badobj_err", SqlGetError(q));
    q = SqlPrepareQueryObject(oMod, "SELECT @a");
    SqlBindInt(q, "@nope", 1);
    SetLocalString(oMod, "param_err", SqlGetError(q));
    q = SqlPrepareQueryObject(oMod, "INSERT INTO t (id, name) VALUES (1, 'dup')");
    SetLocalInt(oMod, "constraint_step", SqlStep(q));
    SetLocalString(oMod, "constraint_err", SqlGetError(q));

    // Campaign SQL shares the campaign database; its builtin table is read-only to scripts.
    q = SqlPrepareQueryCampaign("sqltest", "CREATE TABLE IF NOT EXISTS c (x INTEGER)");
    SqlStep(q);
    q = SqlPrepareQueryCampaign("sqltest", "INSERT INTO c VALUES (@x)");
    SqlBindInt(q, "@x", 99);
    SqlStep(q);
    q = SqlPrepareQueryCampaign("sqltest", "SELECT sum(x) FROM c");
    SqlStep(q);
    SetLocalInt(oMod, "csum", SqlGetInt(q, 0));
    SetCampaignInt("sqltest", "guard", 1);
    q = SqlPrepareQueryCampaign("sqltest", "DELETE FROM db");
    SetLocalString(oMod, "protected_err", SqlGetError(q));
    q = SqlPrepareQueryCampaign("sqltest", "SELECT count(*) FROM db");
    SqlStep(q);
    SetLocalInt(oMod, "builtin_read", SqlGetInt(q, 0));

    // Builtin SQL functions.
    q = SqlPrepareQueryObject(oMod, "SELECT base64encode('hi'), sha1('abc'), xxh32(''), 'abc' REGEXP '^a.c$', xxh64('abc'), base64decode('aGk=')");
    SqlStep(q);
    SetLocalString(oMod, "b64", SqlGetString(q, 0));
    SetLocalString(oMod, "sha1", SqlGetString(q, 1));
    SetLocalInt(oMod, "xxh32", SqlGetInt(q, 2));
    SetLocalInt(oMod, "regexp", SqlGetInt(q, 3));
    SetLocalString(oMod, "b64d", SqlGetString(q, 5));

    // Character database (saved into the BIC on export).
    q = SqlPrepareQueryObject(oPC, "CREATE TABLE pcdata (k TEXT, v INTEGER)");
    SqlStep(q);
    q = SqlPrepareQueryObject(oPC, "INSERT INTO pcdata VALUES ('gold', 123)");
    SqlStep(q);
    SetLocalString(oMod, "pc_err", SqlGetError(q));

    // A single-row read left mid-iteration must not block later writes.
    q = SqlPrepareQueryObject(oMod, "SELECT count(*) FROM t");
    SqlStep(q);
    SetLocalInt(oMod, "count", SqlGetInt(q, 0));
}
