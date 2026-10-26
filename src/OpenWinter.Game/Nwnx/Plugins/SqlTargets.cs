using System.Data.Common;
using System.Globalization;
using System.Text;
using MySqlConnector;
using Npgsql;
using NpgsqlTypes;
using OpenWinter.Game.Persistence;
using SQLitePCL;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_SQL's SQLite target: a file in the user folder's database/ directory, named as NWNX names it
/// (&lt;NWNX_SQL_DATABASE&gt;.sqlite3nwnxee, observed on NWNX's server). Script strings go in and come out as their bytes,
/// or with NWNX_SQL_USE_UTF8 converted to and from UTF-8, character for character as Latin-1, with anything outside
/// it read back as "?" (parity suite nwnx_sql_env).
/// </summary>
public sealed class NwnxSqliteTarget : INwnxSqlTarget
{
    public const string Extension = ".sqlite3nwnxee";
    static readonly Encoding Game = OpenWinter.Aurora.Text.Windows1252;

    readonly sqlite3 _db;
    readonly bool _utf8;
    sqlite3_stmt? _stmt;
    bool _empty;

    public string Type => "SQLITE";
    public bool NextQueryBinary { get; set; }
    public string Path { get; }

    public NwnxSqliteTarget(string path, bool utf8)
    {
        SqlDb.Init();
        Path = path;
        _utf8 = utf8;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        int rc = raw.sqlite3_open_v2(path, out _db, raw.SQLITE_OPEN_READWRITE | raw.SQLITE_OPEN_CREATE, null);
        if (rc != raw.SQLITE_OK) throw new IOException($"cannot open {path}: {raw.sqlite3_errstr(rc).utf8_to_string()}");
        raw.sqlite3_busy_timeout(_db, 2000);
    }

    /// <summary>A script string as the bytes handed to SQLite.</summary>
    public byte[] Encode(string s)
    {
        var bytes = Game.GetBytes(s);
        if (!_utf8) return bytes;
        var latin = new char[bytes.Length];
        for (int i = 0; i < bytes.Length; i++) latin[i] = (char)bytes[i];
        return Encoding.UTF8.GetBytes(latin);
    }

    /// <summary>Bytes from SQLite as a script string.</summary>
    public string Decode(ReadOnlySpan<byte> bytes)
    {
        if (!_utf8) return Game.GetString(bytes);
        var text = Encoding.UTF8.GetString(bytes);
        var raw = new byte[text.Length];
        for (int i = 0; i < text.Length; i++) raw[i] = text[i] <= 0xFF ? (byte)text[i] : (byte)'?';
        return Game.GetString(raw);
    }

    string Error() => Decode(Encoding.UTF8.GetBytes(raw.sqlite3_errmsg(_db).utf8_to_string() ?? ""));

    public bool Prepare(string query, out int parameterCount, out string error)
    {
        DestroyPrepared();
        parameterCount = -1;
        error = "";
        // NUL-terminated, so empty text is an empty statement rather than a missing one.
        int rc = raw.sqlite3_prepare_v2(_db, [.. Encode(query), 0], out var stmt, out _);
        if (rc != raw.SQLITE_OK)
        {
            error = Error();
            stmt?.Dispose();
            return false;
        }
        // Empty text (or only a comment) prepares, with no parameters, and then fails to run.
        if (stmt == null || stmt.IsInvalid)
        {
            stmt?.Dispose();
            _empty = true;
            parameterCount = 0;
            return true;
        }
        _stmt = stmt;
        parameterCount = raw.sqlite3_bind_parameter_count(stmt);
        return true;
    }

    public bool Execute(IReadOnlyList<NwnxSqlValue?> binds, out List<object?[]>? rows, out int affected, out string error)
    {
        rows = null;
        affected = -1;
        error = "";
        if (_stmt == null)
        {
            error = _empty ? Error() : "no prepared query";
            return false;
        }
        raw.sqlite3_reset(_stmt);
        raw.sqlite3_clear_bindings(_stmt);
        for (int i = 0; i < binds.Count; i++)
        {
            if (binds[i] is not { } v) continue;
            // Binary values bind as NULL on SQLite (parity suite nwnx_sql_rows).
            if (v.Kind is NwnxSqlValue.Kinds.Null or NwnxSqlValue.Kinds.Bytes) raw.sqlite3_bind_null(_stmt, i + 1);
            else raw.sqlite3_bind_text(_stmt, i + 1, Encode(v.AsText()));
        }
        int columns = raw.sqlite3_column_count(_stmt);
        var result = new List<object?[]>();
        int rc;
        while ((rc = raw.sqlite3_step(_stmt)) == raw.SQLITE_ROW)
        {
            var row = new object?[columns];
            for (int c = 0; c < columns; c++)
                row[c] = raw.sqlite3_column_type(_stmt, c) == raw.SQLITE_NULL ? null : raw.sqlite3_column_blob(_stmt, c).ToArray();
            result.Add(row);
        }
        if (rc != raw.SQLITE_DONE)
        {
            error = Error();
            raw.sqlite3_reset(_stmt);
            return false;
        }
        raw.sqlite3_reset(_stmt);
        if (columns > 0) rows = result;
        else affected = raw.sqlite3_changes(_db);
        return true;
    }

    public void DestroyPrepared()
    {
        _stmt?.Dispose();
        _stmt = null;
        _empty = false;
    }

    public void Dispose()
    {
        DestroyPrepared();
        raw.sqlite3_close_v2(_db);
        _db.Dispose();
    }
}

/// <summary>
/// NWNX_SQL's MySQL and PostgreSQL targets, through MySqlConnector and Npgsql. MySQL takes "?" placeholders and
/// PostgreSQL "$1, $2 ...", both numbered from 0 by the Prepared* functions. Values bind typed on MySQL; on
/// PostgreSQL they bind as untyped text, so the server infers each one's type from the query. The query is checked
/// by the server when it runs rather than when it is prepared.
/// </summary>
public sealed class NwnxAdoTarget : INwnxSqlTarget
{
    public enum Kind { MySql, PostgreSql }

    readonly Kind _kind;
    readonly string _connectionString;
    DbConnection? _connection;
    string? _query;

    public string Type => _kind == Kind.MySql ? "MYSQL" : "POSTGRESQL";
    public bool NextQueryBinary { get; set; }

    public NwnxAdoTarget(Kind kind, NwnxRegistry reg)
    {
        _kind = kind;
        var host = reg.Env("NWNX_SQL_HOST") ?? "localhost";
        var port = reg.EnvInt("NWNX_SQL_PORT");
        var charset = reg.Env("NWNX_SQL_CHARACTER_SET");
        if (kind == Kind.MySql)
        {
            var b = new MySqlConnectionStringBuilder
            {
                Server = host,
                UserID = reg.Env("NWNX_SQL_USERNAME") ?? "",
                Password = reg.Env("NWNX_SQL_PASSWORD") ?? "",
                Database = reg.Env("NWNX_SQL_DATABASE") ?? "",
                AllowUserVariables = true,
            };
            if (port > 0) b.Port = (uint)port;
            if (charset != null) b.CharacterSet = charset;
            _connectionString = b.ConnectionString;
        }
        else
        {
            var b = new NpgsqlConnectionStringBuilder
            {
                Host = host,
                Username = reg.Env("NWNX_SQL_USERNAME"),
                Password = reg.Env("NWNX_SQL_PASSWORD"),
                Database = reg.Env("NWNX_SQL_DATABASE"),
            };
            if (port > 0) b.Port = port;
            if (charset != null) b.ClientEncoding = charset;
            _connectionString = b.ConnectionString;
        }
        Connection();
    }

    DbConnection Connection()
    {
        if (_connection is { State: System.Data.ConnectionState.Open }) return _connection;
        _connection?.Dispose();
        _connection = _kind == Kind.MySql ? new MySqlConnection(_connectionString) : new NpgsqlConnection(_connectionString);
        _connection.Open();
        return _connection;
    }

    /// <summary>The number of placeholders: "?" for MySQL, the highest "$n" for PostgreSQL, outside quotes and comments.</summary>
    public static int CountPlaceholders(string query, Kind kind)
    {
        int count = 0;
        for (int i = 0; i < query.Length; i++)
        {
            char c = query[i];
            if (c is '\'' or '"' or '`')
            {
                for (i++; i < query.Length; i++)
                {
                    if (query[i] == '\\' && kind == Kind.MySql) { i++; continue; }
                    if (query[i] == c) { if (i + 1 < query.Length && query[i + 1] == c) { i++; continue; } break; }
                }
            }
            else if (c == '-' && i + 1 < query.Length && query[i + 1] == '-' || c == '#' && kind == Kind.MySql)
            {
                while (i < query.Length && query[i] != '\n') i++;
            }
            else if (c == '/' && i + 1 < query.Length && query[i + 1] == '*')
            {
                int end = query.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? query.Length : end + 1;
            }
            else if (kind == Kind.MySql && c == '?') count++;
            else if (kind == Kind.PostgreSql && c == '$' && i + 1 < query.Length && char.IsAsciiDigit(query[i + 1]))
            {
                int j = i + 1;
                while (j < query.Length && char.IsAsciiDigit(query[j])) j++;
                if (int.TryParse(query.AsSpan(i + 1, j - i - 1), NumberStyles.None, CultureInfo.InvariantCulture, out int nth)) count = Math.Max(count, nth);
                i = j - 1;
            }
        }
        return count;
    }

    public bool Prepare(string query, out int parameterCount, out string error)
    {
        error = "";
        _query = query;
        parameterCount = CountPlaceholders(query, _kind);
        return true;
    }

    DbParameter Parameter(DbCommand cmd, NwnxSqlValue? value)
    {
        if (_kind == Kind.PostgreSql)
        {
            var p = new NpgsqlParameter();
            switch (value)
            {
                case null or { Kind: NwnxSqlValue.Kinds.Null }: p.Value = DBNull.Value; p.NpgsqlDbType = NpgsqlDbType.Unknown; break;
                case { Kind: NwnxSqlValue.Kinds.Bytes } b: p.Value = b.Bytes ?? []; p.NpgsqlDbType = NpgsqlDbType.Bytea; break;
                case { } v: p.Value = v.AsText(); p.NpgsqlDbType = NpgsqlDbType.Unknown; break;
            }
            return p;
        }
        var m = cmd.CreateParameter();
        m.Value = value switch
        {
            null or { Kind: NwnxSqlValue.Kinds.Null } => DBNull.Value,
            { Kind: NwnxSqlValue.Kinds.Int or NwnxSqlValue.Kinds.ObjectId } v => v.Integer,
            { Kind: NwnxSqlValue.Kinds.Float } v => v.Real,
            { Kind: NwnxSqlValue.Kinds.Bytes } v => v.Bytes ?? [],
            { } v => v.Text,
        };
        return m;
    }

    public bool Execute(IReadOnlyList<NwnxSqlValue?> binds, out List<object?[]>? rows, out int affected, out string error)
    {
        rows = null;
        affected = -1;
        error = "";
        bool binary = NextQueryBinary;
        NextQueryBinary = false;
        if (_query == null) { error = "no prepared query"; return false; }
        try
        {
            using var cmd = Connection().CreateCommand();
            cmd.CommandText = _query;
            int count = CountPlaceholders(_query, _kind);
            for (int i = 0; i < count; i++) cmd.Parameters.Add(Parameter(cmd, i < binds.Count ? binds[i] : null));
            using var reader = cmd.ExecuteReader();
            if (reader.FieldCount == 0)
            {
                affected = reader.RecordsAffected;
                return true;
            }
            var result = new List<object?[]>();
            while (reader.Read())
            {
                var row = new object?[reader.FieldCount];
                for (int c = 0; c < row.Length; c++) row[c] = Cell(reader.IsDBNull(c) ? null : reader.GetValue(c), binary && c == 0);
                result.Add(row);
            }
            rows = result;
            return true;
        }
        catch (DbException e)
        {
            error = GameText(e.Message);
            return false;
        }
        catch (InvalidOperationException e)
        {
            error = GameText(e.Message);
            return false;
        }
    }

    /// <summary>A result value as the text the client libraries' text protocols give.</summary>
    object? Cell(object? v, bool binary) => v switch
    {
        null => null,
        byte[] b when _kind == Kind.PostgreSql && !binary => "\\x" + Convert.ToHexString(b).ToLowerInvariant(),
        byte[] b => b,
        string s => GameText(s),
        bool b => _kind == Kind.PostgreSql ? (b ? "t" : "f") : (b ? "1" : "0"),
        DateTime d => d.ToString(d.TimeOfDay == TimeSpan.Zero && _kind == Kind.PostgreSql ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        float f => f.ToString("R", CultureInfo.InvariantCulture),
        double d => d.ToString("R", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => GameText(v.ToString() ?? ""),
    };

    /// <summary>Text from the server with characters scripts cannot hold (outside Windows-1252) as "?".</summary>
    static string GameText(string s)
    {
        var enc = OpenWinter.Aurora.Text.Windows1252;
        var sb = new StringBuilder(s.Length);
        foreach (char c in s) sb.Append(enc.GetString(enc.GetBytes(c.ToString())) == c.ToString() ? c : '?');
        return sb.ToString();
    }

    public void DestroyPrepared() => _query = null;

    public void Dispose()
    {
        _connection?.Dispose();
        _connection = null;
    }
}
