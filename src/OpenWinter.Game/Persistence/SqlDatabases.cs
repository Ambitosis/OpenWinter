using SQLitePCL;

namespace OpenWinter.Game.Persistence;

/// <summary>
/// One SQLite database the scripting layer can reach: a campaign database file, the
/// module database file, or a player character's in-memory database (saved into the BIC).
/// Wraps the SQLite C API directly so NWScript's prepare/bind/step/get model maps 1:1.
/// </summary>
public sealed class SqlDb : IDisposable
{
    static bool _initialised;

    public sqlite3 Handle { get; private set; }
    /// <summary>File on disk, or null for an in-memory database.</summary>
    public string? FilePath { get; }
    /// <summary>Tables scripts may read but not write (the campaign database's builtin tables).</summary>
    public string[]? ProtectedTables { get; set; }
    /// <summary>Tables scripts may change but not drop or alter (NWNX_NWSQLiteExtensions' 2DA tables).</summary>
    public readonly HashSet<string> UndroppableTables = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Set while engine code (not a script) prepares statements, so protected tables stay writable for it.</summary>
    internal bool Internal;
    // A weak wrapper can disappear before its native handle's finalizer releases a read lock.
    // Keep the handle reachable so housekeeping can finalize orphaned queries deterministically,
    // without retaining every script query wrapper until the database closes.
    sealed record TrackedStatement(WeakReference<SqlStatement> Owner, sqlite3_stmt Handle);
    readonly List<TrackedStatement> _statements = new();
    public bool IsOpen { get; private set; }

    public static void Init()
    {
        if (_initialised) return;
        Batteries_V2.Init();
        _initialised = true;
    }

    SqlDb(sqlite3 handle, string? path)
    {
        Handle = handle;
        FilePath = path;
        IsOpen = true;
        SqlBuiltins.Register(this);
        raw.sqlite3_set_authorizer(handle, Authorize, null);
    }

    public static SqlDb Open(string path)
    {
        Init();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        int rc = raw.sqlite3_open_v2(path, out var db, raw.SQLITE_OPEN_READWRITE | raw.SQLITE_OPEN_CREATE, null);
        if (rc != raw.SQLITE_OK) throw new IOException($"sqlite: cannot open {path}: {raw.sqlite3_errstr(rc).utf8_to_string()}");
        raw.sqlite3_busy_timeout(db, 2000);
        return new SqlDb(db, path);
    }

    public static SqlDb OpenMemory()
    {
        Init();
        int rc = raw.sqlite3_open_v2(":memory:", out var db, raw.SQLITE_OPEN_READWRITE | raw.SQLITE_OPEN_CREATE, null);
        if (rc != raw.SQLITE_OK) throw new IOException("sqlite: cannot open an in-memory database");
        return new SqlDb(db, null);
    }

    int Authorize(object userData, int action, string p0, string p1, string dbName, string trigger)
    {
        if (Internal) return raw.SQLITE_OK;
        if (UndroppableTables.Count > 0 && (action is raw.SQLITE_DROP_TABLE && UndroppableTables.Contains(p0)
            || action is raw.SQLITE_ALTER_TABLE or raw.SQLITE_DROP_TRIGGER && UndroppableTables.Contains(p1))) return raw.SQLITE_DENY;
        if (ProtectedTables == null) return raw.SQLITE_OK;
        string? table = action switch
        {
            raw.SQLITE_INSERT or raw.SQLITE_UPDATE or raw.SQLITE_DELETE or raw.SQLITE_DROP_TABLE => p0,
            raw.SQLITE_ALTER_TABLE => p1,
            raw.SQLITE_CREATE_INDEX or raw.SQLITE_CREATE_TRIGGER or raw.SQLITE_DROP_INDEX or raw.SQLITE_DROP_TRIGGER => p1,
            _ => null,
        };
        return table != null && ProtectedTables.Contains(table, StringComparer.OrdinalIgnoreCase) ? raw.SQLITE_DENY : raw.SQLITE_OK;
    }

    public string ErrorMessage => IsOpen ? raw.sqlite3_errmsg(Handle).utf8_to_string() ?? "" : "database is closed";

    /// <summary>Prepares a statement for a script. On failure <paramref name="error"/> is set and null returned.</summary>
    public SqlStatement? Prepare(string sql, out string error)
    {
        error = "";
        if (!IsOpen) { error = "database is closed"; return null; }
        int rc = raw.sqlite3_prepare_v2(Handle, sql, out var stmt);
        if (rc != raw.SQLITE_OK || stmt == null || stmt.IsInvalid)
        {
            error = ErrorMessage;
            stmt?.Dispose();
            if (error.Length == 0) error = "empty query";
            return null;
        }
        var s = new SqlStatement(this, stmt);
        lock (_statements)
        {
            CollectStatements();
            _statements.Add(new(new WeakReference<SqlStatement>(s), stmt));
        }
        return s;
    }

    /// <summary>Runs engine-owned SQL (bypasses the protected-table rule). Throws on error.</summary>
    public void Exec(string sql)
    {
        Internal = true;
        try
        {
            int rc = raw.sqlite3_exec(Handle, sql, out string? err);
            if (rc != raw.SQLITE_OK) throw new InvalidOperationException($"sqlite: {err ?? ErrorMessage}");
        }
        finally { Internal = false; }
    }

    /// <summary>Prepares engine-owned SQL (bypasses the protected-table rule). Throws on error.</summary>
    public SqlStatement PrepareInternal(string sql)
    {
        Internal = true;
        try { return Prepare(sql, out var err) ?? throw new InvalidOperationException($"sqlite: {err}"); }
        finally { Internal = false; }
    }

    IEnumerable<SqlStatement> LiveStatements()
    {
        lock (_statements) return CollectStatements();
    }

    // Called under the tracking lock. Live wrappers are held throughout the caller's cleanup;
    // dead wrappers and explicitly disposed statements no longer need a tracking entry.
    List<SqlStatement> CollectStatements()
    {
        var live = new List<SqlStatement>();
        for (int i = _statements.Count - 1; i >= 0; i--)
        {
            var entry = _statements[i];
            if (entry.Owner.TryGetTarget(out var statement) && statement.IsValid) live.Add(statement);
            else
            {
                entry.Handle.Dispose();
                _statements.RemoveAt(i);
            }
        }
        return live;
    }

    /// <summary>
    /// End-of-script housekeeping: resets statements left mid-iteration (scripts
    /// drop their sqlquery when they end, which releases its read lock) and rolls back
    /// transactions left open.
    /// </summary>
    public void EndOfScripts(Action<string>? log)
    {
        if (!IsOpen) return;
        foreach (var s in LiveStatements()) s.ReleaseIfActive();
        if (raw.sqlite3_get_autocommit(Handle) == 0)
        {
            log?.Invoke($"[sql] rolling back a transaction left open by a script ({FilePath ?? "memory"})");
            raw.sqlite3_exec(Handle, "ROLLBACK");
        }
    }

    /// <summary>Drops every table, view, index and trigger (SqlDestroyDatabase). Prepared statements stay usable.</summary>
    public void DropSchema()
    {
        if (!IsOpen) return;
        foreach (var s in LiveStatements()) s.ReleaseIfActive();
        if (raw.sqlite3_get_autocommit(Handle) == 0) raw.sqlite3_exec(Handle, "ROLLBACK");
        var objects = new List<(string Type, string Name)>();
        var q = PrepareInternal("SELECT type, name FROM sqlite_master WHERE name NOT LIKE 'sqlite_%'");
        while (q.Step() == raw.SQLITE_ROW) objects.Add((q.Text(0), q.Text(1)));
        q.Dispose();
        foreach (var kind in new[] { "trigger", "view", "table" })
            foreach (var o in objects.Where(o => o.Type == kind))
                Exec($"DROP {kind.ToUpperInvariant()} IF EXISTS \"{o.Name.Replace("\"", "\"\"")}\"");
        Exec("VACUUM");
    }

    /// <summary>The whole database as a SQLite file image.</summary>
    public byte[] Serialize()
    {
        string temp = Path.Combine(Path.GetTempPath(), $"openwinter-sql-{Guid.NewGuid():N}.sqlite3");
        try
        {
            raw.sqlite3_open_v2(temp, out var dest, raw.SQLITE_OPEN_READWRITE | raw.SQLITE_OPEN_CREATE, null);
            using (dest)
            {
                var b = raw.sqlite3_backup_init(dest, "main", Handle, "main");
                raw.sqlite3_backup_step(b, -1);
                raw.sqlite3_backup_finish(b);
                raw.sqlite3_close_v2(dest);
            }
            return File.ReadAllBytes(temp);
        }
        finally { TryDelete(temp); }
    }

    /// <summary>Replaces this database's contents with a SQLite file image.</summary>
    public void Load(byte[] image)
    {
        string temp = Path.Combine(Path.GetTempPath(), $"openwinter-sql-{Guid.NewGuid():N}.sqlite3");
        try
        {
            File.WriteAllBytes(temp, image);
            int rc = raw.sqlite3_open_v2(temp, out var src, raw.SQLITE_OPEN_READONLY, null);
            using (src)
            {
                if (rc != raw.SQLITE_OK) throw new InvalidDataException("sqlite: unreadable database image");
                var b = raw.sqlite3_backup_init(Handle, "main", src, "main");
                rc = raw.sqlite3_backup_step(b, -1);
                raw.sqlite3_backup_finish(b);
                raw.sqlite3_close_v2(src);
                if (rc != raw.SQLITE_DONE) throw new InvalidDataException($"sqlite: restoring database failed ({rc})");
            }
        }
        finally { TryDelete(temp); }
    }

    static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>Finalizes every statement (they fail from now on) and closes the connection.</summary>
    public void Dispose()
    {
        if (!IsOpen) return;
        foreach (var s in LiveStatements()) s.Dispose();
        lock (_statements) _statements.Clear();
        IsOpen = false;
        raw.sqlite3_close_v2(Handle);
        Handle.Dispose();
    }
}

/// <summary>A prepared statement: what an NWScript sqlquery holds.</summary>
public sealed class SqlStatement : IDisposable
{
    public SqlDb Db { get; }
    sqlite3_stmt? _stmt;
    /// <summary>A row is available (the last step returned SQLITE_ROW).</summary>
    public bool HasRow { get; private set; }
    /// <summary>The last step failed; the statement must be reset before it runs again.</summary>
    public bool Failed { get; private set; }

    internal SqlStatement(SqlDb db, sqlite3_stmt stmt) { Db = db; _stmt = stmt; }

    public bool IsValid => _stmt != null && Db.IsOpen;
    sqlite3_stmt S => _stmt ?? throw new ObjectDisposedException("statement finalized");

    public int Step()
    {
        if (!IsValid) { HasRow = false; return raw.SQLITE_MISUSE; }
        int rc = raw.sqlite3_step(S);
        HasRow = rc == raw.SQLITE_ROW;
        Failed = rc != raw.SQLITE_ROW && rc != raw.SQLITE_DONE;
        if (rc == raw.SQLITE_DONE) raw.sqlite3_reset(S); // done: release locks, keep binds
        return rc;
    }

    public int Reset(bool clearBinds)
    {
        if (!IsValid) return raw.SQLITE_MISUSE;
        HasRow = false;
        Failed = false;
        int rc = raw.sqlite3_reset(S);
        if (clearBinds) raw.sqlite3_clear_bindings(S);
        return rc;
    }

    internal void ReleaseIfActive()
    {
        if (_stmt != null && HasRow) { raw.sqlite3_reset(_stmt); HasRow = false; }
    }

    public int ParameterIndex(string name) => IsValid ? raw.sqlite3_bind_parameter_index(S, name) : 0;
    public int BindInt(int i, long v) => raw.sqlite3_bind_int64(S, i, v);
    public int BindDouble(int i, double v) => raw.sqlite3_bind_double(S, i, v);
    public int BindText(int i, string v) => raw.sqlite3_bind_text(S, i, v);
    public int BindBlob(int i, byte[] v) => v.Length == 0 ? raw.sqlite3_bind_zeroblob(S, i, 0) : raw.sqlite3_bind_blob(S, i, v);
    public int BindNull(int i) => raw.sqlite3_bind_null(S, i);

    public int ColumnCount => IsValid ? raw.sqlite3_column_count(S) : 0;
    public string ColumnName(int i) => raw.sqlite3_column_name(S, i).utf8_to_string() ?? "";
    public int ColumnType(int i) => raw.sqlite3_column_type(S, i);
    public long Int(int i) => raw.sqlite3_column_int64(S, i);
    public double Double(int i) => raw.sqlite3_column_double(S, i);
    public string Text(int i) => raw.sqlite3_column_text(S, i).utf8_to_string() ?? "";
    public byte[] Blob(int i) => raw.sqlite3_column_blob(S, i).ToArray();

    public void Dispose()
    {
        HasRow = false;
        _stmt?.Dispose();
        _stmt = null;
    }
}
