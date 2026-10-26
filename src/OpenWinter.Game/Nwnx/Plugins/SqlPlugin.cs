using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>A value bound to a prepared NWNX_SQL query: text, an integer, a float, an object id, raw bytes or NULL.</summary>
public readonly record struct NwnxSqlValue(NwnxSqlValue.Kinds Kind, string Text = "", long Integer = 0, float Real = 0, byte[]? Bytes = null)
{
    public enum Kinds { Null, Int, Float, String, ObjectId, Bytes }

    /// <summary>
    /// The value as NWNX binds it in text form: integers and object ids in decimal, floats as C's "%f" of the float
    /// (six decimals: 0.1 binds as "0.100000"; parity suite nwnx_sql_basics).
    /// </summary>
    public string AsText() => Kind switch
    {
        Kinds.Int or Kinds.ObjectId => Integer.ToString(CultureInfo.InvariantCulture),
        Kinds.Float => ((double)Real).ToString("F6", CultureInfo.InvariantCulture),
        _ => Text,
    };
}

/// <summary>One database NWNX_SQL talks to (NWNX_SQL_TYPE): SQLite, MySQL or PostgreSQL.</summary>
public interface INwnxSqlTarget : IDisposable
{
    /// <summary>The upper-case type name GetDatabaseType reports.</summary>
    string Type { get; }
    /// <summary>Compiles a query. The parameter count is that of its placeholders.</summary>
    bool Prepare(string query, out int parameterCount, out string error);
    /// <summary>
    /// Runs the prepared query with the bound values (null where nothing was bound). A query that returns columns
    /// gives its rows (each cell a string, raw bytes or null); one that does not gives the number of rows it changed.
    /// </summary>
    bool Execute(IReadOnlyList<NwnxSqlValue?> binds, out List<object?[]>? rows, out int affected, out string error);
    void DestroyPrepared();
    /// <summary>PostgreSQL only: the next query returns its first column in binary.</summary>
    bool NextQueryBinary { get; set; }
}

/// <summary>
/// NWNX_SQL: one connection, one prepared query and one result set at a time, configured by the NWNX_SQL_*
/// settings. Measured on NWNX's SQLite target (parity suites nwnx_sql_basics, nwnx_sql_rows, nwnx_sql_more,
/// nwnx_sql_env): every bound value is text but NULL, results are read in full when the query runs, the active row
/// outlives its query, query ids count every attempt to run a compiled query, and the last error is kept until it
/// is read once.
/// </summary>
public static class SqlPlugin
{
    const string P = "SQL";

    sealed class State
    {
        public INwnxSqlTarget? Target;
        public string ConnectError = "";
        public bool Prepared;
        public string Query = "";
        public int ParameterCount = -1;
        public readonly List<NwnxSqlValue?> Binds = new();
        public List<object?[]> Rows = new();
        public int NextRow;
        public object?[]? ActiveRow;
        public int Affected = -1;
        public int QueryId;
        public string LastError = "";
    }

    static readonly ConditionalWeakTable<NwnxRegistry, State> States = new();
    static State Get(NwnxCall n) => States.GetValue(n.Registry, _ => new State());

    /// <summary>Overrides how targets are made (tests); the default reads the NWNX_SQL_* settings.</summary>
    public static Func<NwnxRegistry, World, INwnxSqlTarget>? TargetFactory;

    static INwnxSqlTarget? Target(NwnxCall n, State st)
    {
        if (st.Target != null) return st.Target;
        try
        {
            st.Target = TargetFactory?.Invoke(n.Registry, n.World) ?? CreateTarget(n.Registry, n.World);
            n.Registry.Log($"[nwnx] SQL: connected to {st.Target.Type}");
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            st.ConnectError = e.Message;
            n.Registry.Log($"[nwnx] SQL: cannot connect: {e.Message}");
        }
        return st.Target;
    }

    /// <summary>The target NWNX_SQL_TYPE names (SQLite when unset), connected with the other NWNX_SQL_* settings.</summary>
    public static INwnxSqlTarget CreateTarget(NwnxRegistry reg, World world)
    {
        var type = (reg.Env("NWNX_SQL_TYPE") ?? "SQLITE").Trim().ToUpperInvariant();
        bool utf8 = reg.EnvBool("NWNX_SQL_USE_UTF8");
        return type switch
        {
            "SQLITE" => new NwnxSqliteTarget(Path.Combine(world.System<PersistenceSystem>().DatabaseDir,
                (reg.Env("NWNX_SQL_DATABASE") ?? "nwnx") + NwnxSqliteTarget.Extension), utf8),
            "MYSQL" => new NwnxAdoTarget(NwnxAdoTarget.Kind.MySql, reg),
            "POSTGRESQL" => new NwnxAdoTarget(NwnxAdoTarget.Kind.PostgreSql, reg),
            _ => throw new InvalidOperationException($"unknown NWNX_SQL_TYPE '{type}'"),
        };
    }

    static void SetError(State st, string error)
    {
        if (error.Length > 0) st.LastError = error;
    }

    static void ClearPrepared(State st)
    {
        st.Target?.DestroyPrepared();
        st.Prepared = false;
        st.ParameterCount = -1;
        st.Binds.Clear();
    }

    [NwnxFunc(P, "PrepareQuery")]
    static void PrepareQuery(NwnxCall n)
    {
        var query = n.PopString();
        var st = Get(n);
        ClearPrepared(st);
        st.Rows = new();
        st.NextRow = 0;
        st.Affected = -1;
        if (Target(n, st) is not { } target) { SetError(st, st.ConnectError); n.Push(0); return; }
        if (!target.Prepare(query, out int count, out var error))
        {
            SetError(st, error);
            n.Push(0);
            return;
        }
        st.Prepared = true;
        st.Query = query;
        st.ParameterCount = count;
        n.Push(1);
    }

    [NwnxFunc(P, "ExecutePreparedQuery")]
    static void ExecutePreparedQuery(NwnxCall n)
    {
        var st = Get(n);
        if (!st.Prepared || st.Target == null)
        {
            n.Registry.Log("[nwnx] SQL: trying to execute a prepared query without a successful PrepareQuery");
            n.Push(0);
            return;
        }
        int id = ++st.QueryId;
        st.Rows = new();
        st.NextRow = 0;
        if (!st.Target.Execute(st.Binds, out var rows, out int affected, out var error))
        {
            st.Affected = -1;
            SetError(st, error);
            n.Registry.Log($"[nwnx] SQL: query {id} failed: {error}");
            n.Push(0);
            return;
        }
        st.Rows = rows ?? new();
        st.Affected = rows == null ? affected : -1;
        n.Push(id);
    }

    [NwnxFunc(P, "ReadyToReadNextRow")]
    static void Ready(NwnxCall n)
    {
        var st = Get(n);
        n.Push(st.NextRow < st.Rows.Count);
    }

    [NwnxFunc(P, "ReadNextRow")]
    static void ReadNextRow(NwnxCall n)
    {
        var st = Get(n);
        // Past the last row the active row stays as it is.
        if (st.NextRow < st.Rows.Count) st.ActiveRow = st.Rows[st.NextRow++];
    }

    /// <summary>The active row's cell, or false for a column outside it (NWNX fails the call: the script reads "").</summary>
    static bool Cell(State st, int column, out object? value)
    {
        value = null;
        if (st.ActiveRow is not { } row || column < 0 || column >= row.Length) return false;
        value = row[column];
        return true;
    }

    [NwnxFunc(P, "ReadDataInActiveRow")]
    static void ReadData(NwnxCall n)
    {
        int column = n.PopInt();
        var st = Get(n);
        if (!Cell(st, column, out var v)) return;
        n.Push(v switch
        {
            string s => s,
            byte[] b => st.Target is NwnxSqliteTarget lite ? lite.Decode(b) : OpenWinter.Aurora.Text.Windows1252.GetString(b),
            _ => "",
        });
    }

    static int Position(NwnxCall n, State st)
    {
        int position = n.PopInt();
        if (!st.Prepared || position < 0 || position >= st.ParameterCount)
        {
            n.Registry.Log($"[nwnx] SQL: prepared argument position {position} out of bounds");
            return -1;
        }
        while (st.Binds.Count <= position) st.Binds.Add(null);
        return position;
    }

    static void Bind(NwnxCall n, Func<NwnxCall, NwnxSqlValue> value)
    {
        var st = Get(n);
        int position = Position(n, st);
        var v = value(n);
        if (position >= 0) st.Binds[position] = v;
    }

    [NwnxFunc(P, "PreparedInt")] static void PreparedInt(NwnxCall n) => Bind(n, c => new(NwnxSqlValue.Kinds.Int, Integer: c.PopInt()));
    [NwnxFunc(P, "PreparedString")] static void PreparedString(NwnxCall n) => Bind(n, c => new(NwnxSqlValue.Kinds.String, c.PopString()));
    [NwnxFunc(P, "PreparedFloat")] static void PreparedFloat(NwnxCall n) => Bind(n, c => new(NwnxSqlValue.Kinds.Float, Real: c.PopFloat()));
    [NwnxFunc(P, "PreparedNULL")] static void PreparedNull(NwnxCall n) => Bind(n, _ => new(NwnxSqlValue.Kinds.Null));
    [NwnxFunc(P, "PreparedObjectId")]
    static void PreparedObjectId(NwnxCall n) => Bind(n, c => new(NwnxSqlValue.Kinds.ObjectId, Integer: unchecked((int)c.PopObjectId())));

    [NwnxFunc(P, "PreparedObjectFull")]
    static void PreparedObjectFull(NwnxCall n) => Bind(n, c =>
    {
        var o = c.PopObject();
        bool base64 = c.PopInt() != 0;
        // Areas, modules and invalid objects bind as empty text (parity suite nwnx_sql_more).
        var bytes = o != null && ObjectSerializer.CanStore(o) ? ObjectSerializer.ToBytes(c.World, o, saveObjectState: true) : null;
        if (base64) return new(NwnxSqlValue.Kinds.String, bytes == null ? "" : Convert.ToBase64String(bytes));
        return new(NwnxSqlValue.Kinds.Bytes, Bytes: bytes ?? []);
    });

    [NwnxFunc(P, "ReadFullObjectInActiveRow")]
    static void ReadFullObject(NwnxCall n)
    {
        int column = n.PopInt();
        var owner = n.PopObject();
        float x = n.PopFloat(), y = n.PopFloat(), z = n.PopFloat();
        bool base64 = n.PopInt() != 0;
        var st = Get(n);
        if (!Cell(st, column, out var cell)) { n.PushObject(GameObject.InvalidId); return; }
        byte[]? data = null;
        if (base64)
        {
            var text = cell switch { string s => s, byte[] b => Encoding.Latin1.GetString(b), _ => "" };
            try { data = Convert.FromBase64String(text.Trim()); }
            catch (FormatException) { data = null; }
        }
        else data = cell as byte[];
        var o = data is { Length: > 0 } ? Deserialize(n.World, data, owner, new System.Numerics.Vector3(x, y, z)) : null;
        n.PushObject(o?.Id ?? GameObject.InvalidId);
    }

    /// <summary>
    /// Creates a stored object (parity suite nwnx_sql_more): in an area owner at the given position; an item in the
    /// inventory of a creature, placeable or store owner (into the bag's holder for a bag); a creature anywhere else
    /// stands at the area's origin; anything else is made outside the world.
    /// </summary>
    static GameObject? Deserialize(World world, byte[] data, GameObject? owner, System.Numerics.Vector3 at)
    {
        if (owner is Areas.Area area) return Placed(world, ObjectSerializer.FromBytes(world, data, new Location(area.Id, at, 0), null, loadObjectState: true), area, at);
        var home = owner?.Area ?? world.Areas.FirstOrDefault();
        if (home == null) return null;
        var origin = new Location(home.Id, System.Numerics.Vector3.Zero, 0);
        GameObject? inventory = owner switch
        {
            Item bag when world.Inventory.Owner(bag) is { } holder => holder,
            Creature or Store => owner,
            Placeable { HasInventory: true } => owner,
            _ => null,
        };
        var o = Placed(world, ObjectSerializer.FromBytes(world, data, origin, inventory, loadObjectState: true), home, System.Numerics.Vector3.Zero);
        if (o is Item item && item.Area != null || o is not (null or Creature or Item)) world.LiftFromArea(o);
        return o;
    }

    /// <summary>A creature stands exactly where it is put, even where another stands (parity suite nwnx_sql_more).</summary>
    static GameObject? Placed(World world, GameObject? o, Areas.Area area, System.Numerics.Vector3 at)
    {
        if (o is Creature c && c.Area == area) world.AddToArea(c, area, at, c.Facing, exact: true, enterEvent: false);
        return o;
    }

    [NwnxFunc(P, "GetAffectedRows")] static void AffectedRows(NwnxCall n) => n.Push(Get(n).Affected);

    [NwnxFunc(P, "GetDatabaseType")]
    static void DatabaseType(NwnxCall n)
    {
        var st = Get(n);
        n.Push(Target(n, st)?.Type ?? (n.Registry.Env("NWNX_SQL_TYPE") ?? "").ToUpperInvariant());
    }

    [NwnxFunc(P, "DestroyPreparedQuery")] static void Destroy(NwnxCall n) => ClearPrepared(Get(n));

    [NwnxFunc(P, "GetLastError")]
    static void LastError(NwnxCall n)
    {
        var st = Get(n);
        n.Push(st.LastError);
        st.LastError = "";
    }

    [NwnxFunc(P, "GetPreparedQueryParamCount")] static void ParamCount(NwnxCall n) => n.Push(Get(n).ParameterCount);

    [NwnxFunc(P, "PostgreSQL_SetNextQueryResultsBinaryMode")]
    static void BinaryMode(NwnxCall n)
    {
        var st = Get(n);
        if (Target(n, st) is { } t) t.NextQueryBinary = true;
    }

    /// <summary>Closes the connection (tests, server shutdown).</summary>
    public static void Close(NwnxRegistry reg)
    {
        if (!States.TryGetValue(reg, out var st)) return;
        st.Target?.Dispose();
        States.Remove(reg);
    }
}
