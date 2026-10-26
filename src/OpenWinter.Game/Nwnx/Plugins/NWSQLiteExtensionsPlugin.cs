using System.Runtime.CompilerServices;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using SQLitePCL;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_NWSQLiteExtensions: extensions to the game's own SQLite databases, each behind its
/// NWNX_NWSQLITEEXTENSIONS_* setting (parity suite nwnx_nwsqliteext):
/// read-only 2DA tables in the module database, Mersenne Twister functions in every database, and a database on
/// every object but areas.
/// </summary>
public static class NWSQLiteExtensionsPlugin
{
    const string P = "NWSQLiteExtensions";
    const string Env2DA = "NWNX_NWSQLITEEXTENSIONS_ENABLE_2DA_VIRTUAL_TABLE_MODULE";
    const string EnvMersenne = "NWNX_NWSQLITEEXTENSIONS_ENABLE_MERSENNE_TWISTER_FUNCTIONS";
    const string EnvAllObjects = "NWNX_NWSQLITEEXTENSIONS_ENABLE_DATABASE_ON_ALL_OBJECT_TYPES";

    static NwnxRegistry Registry(World w) => w.System<NwnxRegistry>();

    /// <summary>Whether SqlPrepareQueryObject works on objects other than the module and player characters.</summary>
    public static bool DatabaseOnAllObjects(World w) => Registry(w).EnvBool(EnvAllObjects);

    /// <summary>Called for every database the game opens: adds the Mersenne Twister functions when they are enabled.</summary>
    public static void Opened(World w, SqlDb db)
    {
        if (!Registry(w).EnvBool(EnvMersenne)) return;
        var twisters = Twisters.GetValue(Registry(w), _ => new Dictionary<string, MersenneTwister>(StringComparer.Ordinal));
        const int Utf8 = 1;
        raw.sqlite3_create_function(db.Handle, "MT_SEED", 2, Utf8, null, (ctx, _, a) =>
        {
            var name = raw.sqlite3_value_text(a[0]).utf8_to_string() ?? "";
            if (name.Length == 0) { raw.sqlite3_result_int(ctx, 0); return; }
            twisters[name] = new MersenneTwister(unchecked((uint)raw.sqlite3_value_int64(a[1])));
            raw.sqlite3_result_int(ctx, 1);
        });
        raw.sqlite3_create_function(db.Handle, "MT_VALUE", 1, Utf8, null, (ctx, _, a) =>
        {
            var name = raw.sqlite3_value_text(a[0]).utf8_to_string() ?? "";
            if (name.Length == 0) { raw.sqlite3_result_int(ctx, -1); return; }
            raw.sqlite3_result_int64(ctx, Twister(twisters, name).Next());
        });
        raw.sqlite3_create_function(db.Handle, "MT_DISCARD", 2, Utf8, null, (ctx, _, a) =>
        {
            var name = raw.sqlite3_value_text(a[0]).utf8_to_string() ?? "";
            if (name.Length == 0) { raw.sqlite3_result_int(ctx, 0); return; }
            var mt = Twister(twisters, name);
            for (long i = raw.sqlite3_value_int64(a[1]); i > 0; i--) mt.Next();
            raw.sqlite3_result_int(ctx, 1);
        });
    }

    static readonly ConditionalWeakTable<NwnxRegistry, Dictionary<string, MersenneTwister>> Twisters = new();

    /// <summary>A generator not seeded yet starts from the standard default seed (5489), as a fresh std::mt19937.</summary>
    static MersenneTwister Twister(Dictionary<string, MersenneTwister> all, string name)
    {
        if (!all.TryGetValue(name, out var mt)) all[name] = mt = new MersenneTwister(5489);
        return mt;
    }

    static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

    /// <summary>
    /// A table of a 2DA's rows (rowid the row number, "****" as NULL, columns typed by the hints: 0 text, 1 integer,
    /// 2 real), which scripts can read but not change or drop.
    /// </summary>
    [NwnxFunc(P, "CreateVirtual2DATable")]
    static void CreateVirtual2DATable(NwnxCall n)
    {
        var twoDA = n.PopString(); var hints = n.PopString(); var table = n.PopString();
        if (!n.Registry.EnvBool(Env2DA) || twoDA.Length == 0 || !n.World.Resources.Contains(twoDA, (ushort)Aurora.Resources.ResType.TwoDA))
        {
            n.Push(0);
            return;
        }
        if (table.Length == 0) table = twoDA;
        var data = n.World.Resources.TwoDA(twoDA);
        var db = n.World.System<PersistenceSystem>().ModuleDb(n.World.Module);
        var columns = data.Columns.Select((c, i) => Quote(c) + (i < hints.Length ? hints[i] switch { '1' => " INTEGER", '2' => " REAL", _ => " TEXT" } : " TEXT"));
        var t = Quote(table);
        try
        {
            db.Exec($"DROP TABLE IF EXISTS {t};CREATE TABLE {t} ({string.Join(", ", columns)});");
            db.Internal = true;
            try
            {
                var insert = db.Prepare($"INSERT INTO {t} (rowid{string.Concat(data.Columns.Select(c => ", " + Quote(c)))}) VALUES (?{string.Concat(data.Columns.Select(_ => ", ?"))})", out var error)
                    ?? throw new InvalidOperationException(error);
                using (insert)
                {
                    db.Exec("BEGIN");
                    for (int r = 0; r < data.RowCount; r++)
                    {
                        insert.Reset(clearBinds: true);
                        insert.BindInt(1, r);
                        for (int c = 0; c < data.Columns.Length; c++)
                            if (data.Get(r, c) is { Length: > 0 } v) insert.BindText(c + 2, v);
                        insert.Step();
                    }
                    db.Exec("COMMIT");
                }
            }
            finally { db.Internal = false; }
            // Changing the rows fails with SQLite's message for a read-only virtual table. The triggers are temporary,
            // so the schema scripts see holds the table alone.
            var message = $"table {table} may not be modified".Replace("'", "''");
            foreach (var op in new[] { "INSERT", "UPDATE", "DELETE" })
                db.Exec($"DROP TRIGGER IF EXISTS temp.{Quote(table + "_nwnx_" + op.ToLowerInvariant())};" +
                    $"CREATE TEMP TRIGGER {Quote(table + "_nwnx_" + op.ToLowerInvariant())} BEFORE {op} ON main.{t} BEGIN SELECT RAISE(ABORT, '{message}'); END;");
            db.UndroppableTables.Add(table);
            n.Push(1);
        }
        catch (InvalidOperationException e)
        {
            n.Registry.Log($"[nwnx] NWSQLiteExtensions: cannot create table {table} for {twoDA}: {e.Message}");
            n.Push(0);
        }
    }
}

/// <summary>MT19937, the 32-bit Mersenne Twister (as C++'s std::mt19937).</summary>
public sealed class MersenneTwister
{
    readonly uint[] _mt = new uint[624];
    int _index;

    public MersenneTwister(uint seed)
    {
        _mt[0] = seed;
        for (int i = 1; i < 624; i++) _mt[i] = unchecked(1812433253u * (_mt[i - 1] ^ (_mt[i - 1] >> 30)) + (uint)i);
        _index = 624;
    }

    public uint Next()
    {
        if (_index >= 624)
        {
            for (int i = 0; i < 624; i++)
            {
                uint y = (_mt[i] & 0x80000000u) | (_mt[(i + 1) % 624] & 0x7fffffffu);
                _mt[i] = _mt[(i + 397) % 624] ^ (y >> 1) ^ ((y & 1) != 0 ? 0x9908b0dfu : 0u);
            }
            _index = 0;
        }
        uint x = _mt[_index++];
        x ^= x >> 11;
        x ^= (x << 7) & 0x9d2c5680u;
        x ^= (x << 15) & 0xefc60000u;
        x ^= x >> 18;
        return x;
    }
}
