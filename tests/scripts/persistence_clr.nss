// SqlDestroyDatabase drops the schema; prepared queries keep working.
void main()
{
    object oMod = GetModule();
    sqlquery q = SqlPrepareQueryObject(oMod, "SELECT count(*) FROM sqlite_master");
    SqlDestroyDatabase(oMod);
    SqlStep(q);
    SetLocalInt(oMod, "schema_after", SqlGetInt(q, 0));
    SetLocalString(oMod, "schema_after_err", SqlGetError(q));
    q = SqlPrepareQueryObject(oMod, "SELECT * FROM t");
    SetLocalString(oMod, "gone_err", SqlGetError(q));
}
