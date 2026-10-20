using System.Buffers.Binary;
using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using SQLitePCL;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>
/// EE SQLite access (SqlPrepareQuery*, SqlBind*, SqlStep, SqlGet*). A sqlquery holds a
/// prepared <see cref="SqlStatement"/>. As in the original, the first error sticks to the query:
/// SqlGetError keeps reporting it and the query does nothing more.
/// </summary>
static class SqlFunctions
{
    static PersistenceSystem Sys(Call c) => c.World.System<PersistenceSystem>();

    /// <summary>Records an error on the query, worded as the original's; it is logged and shown to connected players.</summary>
    static void Fail(Call c, SqlQuery q, string message, int code = 1)
    {
        message = $"sqlite error: {message} (code={code}, db={q.Db}, script={c.Thread.Program.Name}, query={q.Sql})";
        q.Error = message;
        c.Host.Log($"[sql] {c.Function.Name} ({c.Thread.Program.Name}): {message}");
        if (c.World.Output is { } output)
            foreach (var pc in c.World.AllObjects.OfType<Creature>().Where(x => x.IsPC && !x.IsDestroyed))
                output.SendMessage(pc, $"SQL error: {message}");
    }

    static SqlQuery Prepared(Call c, SqlDb? db, string sql, string dbName)
    {
        var q = new SqlQuery { Sql = sql };
        if (db == null) { Fail(c, q, "no such database"); return q; }
        q.Db = dbName;
        if (sql.Trim().Length == 0) return q; // nothing to run, and not an error
        q.Statement = db.Prepare(sql, out var error);
        if (q.Statement == null) Fail(c, q, error, raw.sqlite3_extended_errcode(db.Handle));
        return q;
    }

    // sqlquery SqlPrepareQueryCampaign(string sDatabase, string sQuery)
    [NwFunc("SqlPrepareQueryCampaign")]
    static object? PrepareCampaign(Call c)
    {
        SqlDb? db = null;
        try { db = Sys(c).Campaign(c.S(0)); }
        catch (IOException e) { c.Host.Log($"[sql] {e.Message}"); }
        return Prepared(c, db, c.S(1), $"Campaign({c.S(0)})");
    }

    // sqlquery SqlPrepareQueryObject(object oObject, string sQuery)
    [NwFunc("SqlPrepareQueryObject")]
    static object? PrepareObject(Call c) =>
        Prepared(c, Sys(c).ObjectDb(c.Obj(0)), c.S(1), c.Obj(0) is Module ? "Module()" : $"Player({c.Obj(0)?.Tag})");

    // void SqlDestroyDatabase(object oObject)
    [NwFunc("SqlDestroyDatabase")]
    static object? Destroy(Call c)
    {
        Sys(c).ObjectDb(c.Obj(0))?.DropSchema();
        return null;
    }

    [NwFunc("SqlGetError")] static object? Error(Call c) => c.Q(0).Error;

    /// <summary>The query's statement when it is usable (no earlier error); null otherwise.</summary>
    static SqlStatement? Usable(Call c, SqlQuery q)
    {
        if (q.Error.Length > 0) return null;
        if (q.Statement is SqlStatement { IsValid: true } st) return st;
        if (q.Statement != null) Fail(c, q, "query is not prepared or its database was destroyed");
        return null;
    }

    static object? Bind(Call c, Func<SqlStatement, int, int> bind)
    {
        var q = c.Q(0);
        if (Usable(c, q) is not { } st) return null;
        if (q.Executed) { Fail(c, q, "already executed"); return null; }
        int index = st.ParameterIndex(c.S(1));
        if (index == 0) { Fail(c, q, $"no such named parameter: {c.S(1)}"); return null; }
        int rc = bind(st, index);
        if (rc != raw.SQLITE_OK) Fail(c, q, st.Db.ErrorMessage, rc);
        return null;
    }

    /// <summary>Vectors are stored as text "x:y:z" with six decimals.</summary>
    static string VectorText(Vector3 v) =>
        $"{CoreFunctions.FloatToString(v.X, 0, 6)}:{CoreFunctions.FloatToString(v.Y, 0, 6)}:{CoreFunctions.FloatToString(v.Z, 0, 6)}";

    static readonly System.Text.RegularExpressions.Regex VectorPattern = new(
        @"^\s*([-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?):\s*([-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?):\s*([-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?)");

    /// <summary>"x:y:z" (three numbers, as scanf reads them); anything else is the zero vector.</summary>
    static Vector3 ParseVector(string text)
    {
        var m = VectorPattern.Match(text);
        return m.Success
            ? new Vector3(CoreFunctions.ParseLeadingFloat(m.Groups[1].Value), CoreFunctions.ParseLeadingFloat(m.Groups[2].Value), CoreFunctions.ParseLeadingFloat(m.Groups[3].Value))
            : Vector3.Zero;
    }

    [NwFunc("SqlBindInt")] static object? BindInt(Call c) => Bind(c, (st, i) => st.BindInt(i, c.I(2)));
    [NwFunc("SqlBindFloat")] static object? BindFloat(Call c) => Bind(c, (st, i) => st.BindDouble(i, c.F(2)));
    [NwFunc("SqlBindString")] static object? BindString(Call c) => Bind(c, (st, i) => st.BindText(i, c.S(2)));
    [NwFunc("SqlBindVector")] static object? BindVector(Call c) => Bind(c, (st, i) => st.BindText(i, VectorText(c.V(2))));
    [NwFunc("SqlBindJson")] static object? BindJson(Call c) => Bind(c, (st, i) => st.BindText(i, JsonFunctions.Dump(c.J(2))));

    // void SqlBindObject(sqlquery sqlQuery, string sParam, object oObject, int bSaveObjectState = FALSE)
    [NwFunc("SqlBindObject")]
    static object? BindObject(Call c)
    {
        var o = c.Obj(2);
        // Areas serialize for ObjectToJson but can't be bound (parity suite persist_objects).
        var bytes = o == null || !ObjectSerializer.CanStore(o) ? null : ObjectSerializer.ToBytes(c.World, o, c.I(3) != 0);
        if (bytes == null)
        {
            var q = c.Q(0);
            if (Usable(c, q) is { } st && st.ParameterIndex(c.S(1)) is > 0 and var idx) st.BindNull(idx);
            Fail(c, q, o == null ? "SqlBindObject: invalid object" : $"SqlBindObject: {o.Type} objects cannot be serialized");
            return null;
        }
        return Bind(c, (st, i) => st.BindBlob(i, bytes));
    }

    // int SqlStep(sqlquery sqlQuery)
    [NwFunc("SqlStep")]
    static object? Step(Call c)
    {
        var q = c.Q(0);
        if (q.Statement == null && q.Error.Length == 0 && q.Sql.Length == 0 && q.Db == "(unknown db)") { Fail(c, q, "no query to step"); return 0; }
        if (Usable(c, q) is not { } st || q.Done) return 0;
        q.Executed = true;
        int rc = st.Step();
        if (rc == raw.SQLITE_ROW) return 1;
        q.Done = true;
        if (rc != raw.SQLITE_DONE) Fail(c, q, st.Db.ErrorMessage, raw.sqlite3_extended_errcode(st.Db.Handle));
        return 0;
    }

    /// <summary>The statement when a row is available and the column index is valid (quietly null otherwise).</summary>
    static SqlStatement? Row(Call c)
    {
        var q = c.Q(0);
        if (Usable(c, q) is not { } st || !st.HasRow) return null;
        int i = c.I(1);
        return i >= 0 && i < st.ColumnCount ? st : null;
    }

    [NwFunc("SqlGetInt")] static object? GetInt(Call c) => Row(c) is { } st ? unchecked((int)st.Int(c.I(1))) : 0;
    [NwFunc("SqlGetFloat")] static object? GetFloat(Call c) => Row(c) is { } st ? (float)st.Double(c.I(1)) : 0f;
    [NwFunc("SqlGetString")] static object? GetString(Call c) => Row(c) is { } st ? st.Text(c.I(1)) : "";

    [NwFunc("SqlGetVector")]
    static object? GetVector(Call c)
    {
        if (Row(c) is not { } st) return Vector3.Zero;
        int i = c.I(1);
        if (st.ColumnType(i) == raw.SQLITE_BLOB && st.Blob(i) is { Length: 12 } b)
            return new Vector3(BinaryPrimitives.ReadSingleLittleEndian(b), BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(4)), BinaryPrimitives.ReadSingleLittleEndian(b.AsSpan(8)));
        return ParseVector(st.Text(i));
    }

    [NwFunc("SqlGetJson")]
    static object? GetJson(Call c)
    {
        // The column's text is parsed as it is, so NULL gives the parse error of "".
        if (Row(c) is not { } st) return JsonValue.Null;
        return JsonFunctions.Parse(st.Text(c.I(1)));
    }

    // object SqlGetObject(sqlquery sqlQuery, int nIndex, location lSpawnAt, object oInventory = OBJECT_INVALID, int bLoadObjectState = FALSE)
    [NwFunc("SqlGetObject")]
    static object? GetObject(Call c)
    {
        if (Row(c) is not { } st) return GameObject.InvalidId;
        var o = ObjectSerializer.FromBytes(c.World, st.Blob(c.I(1)), c.L(2), c.Obj(3), c.I(4) != 0);
        if (o == null) { Fail(c, c.Q(0), "SqlGetObject: column does not hold a valid object for that location"); return GameObject.InvalidId; }
        return o.Id;
    }

    // void SqlResetQuery(sqlquery sqlQuery, int bClearBinds = FALSE)
    [NwFunc("SqlResetQuery")]
    static object? Reset(Call c)
    {
        var q = c.Q(0);
        if (Usable(c, q) is { } st) { st.Reset(c.I(1) != 0); q.Executed = q.Done = false; }
        return null;
    }

    [NwFunc("SqlGetColumnCount")] static object? ColumnCount(Call c) => Usable(c, c.Q(0)) is { } st ? st.ColumnCount : 0;

    [NwFunc("SqlGetColumnName")]
    static object? ColumnName(Call c)
    {
        var q = c.Q(0);
        if (Usable(c, q) is not { } st) return "";
        int i = c.I(1);
        if (i < 0 || i >= st.ColumnCount) { Fail(c, q, "column index out of range"); return ""; }
        return st.ColumnName(i);
    }
}
