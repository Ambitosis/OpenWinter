using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Persistence;

/// <summary>
/// Owns persistent storage for a world: campaign databases (database/&lt;name&gt;.sqlite3),
/// the module database (in memory, kept in saved games), per-character databases,
/// the object UUID index and TEMP: templates (JsonToTemplate).
/// </summary>
public sealed class PersistenceSystem : IWorldSystem, IDisposable
{
    readonly World _world;
    readonly Dictionary<string, SqlDb> _campaigns = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<uint, SqlDb> _characters = new();
    readonly Dictionary<uint, SqlDb> _others = new();
    SqlDb? _module;
    readonly Dictionary<string, uint> _uuids = new(StringComparer.OrdinalIgnoreCase);
    MemorySource? _temp;

    /// <summary>Overrides where database/ and servervault/ live (tests); defaults to the user folder.</summary>
    public string? RootOverride;

    /// <summary>
    /// Where development runs keep databases and vault exports instead of the real user folder.
    /// Hosts set this unless the user opted into live data (config "liveUserData": true).
    /// </summary>
    public static string? SandboxRoot;
    /// <summary>Server-vault folders by player name rather than public key (the server's <see cref="ServerOptions.VaultByPlayerName"/>).</summary>
    public bool VaultByPlayerName = true;
    public Action<string> Log = Console.WriteLine;

    public PersistenceSystem(World world) { _world = world; }

    public int Order => 1000;

    public string UserRoot => RootOverride ?? SandboxRoot ?? _world.Resources.Paths.UserRoot;
    public string DatabaseDir => Path.Combine(UserRoot, "database");

    /// <summary>Runs after scripts each step: releases queries scripts left mid-iteration and rolls back abandoned transactions.</summary>
    public void Step(float dt) => EndOfScripts();

    public void EndOfScripts()
    {
        foreach (var db in AllOpen()) db.EndOfScripts(Log);
        foreach (var (id, db) in _characters.ToList())
            if (_world.Get(id) == null) { db.Dispose(); _characters.Remove(id); }
        foreach (var (id, db) in _others.ToList())
            if (_world.Get(id) == null) { db.Dispose(); _others.Remove(id); }
    }

    IEnumerable<SqlDb> AllOpen()
    {
        foreach (var db in _campaigns.Values) if (db.IsOpen) yield return db;
        foreach (var db in _characters.Values) if (db.IsOpen) yield return db;
        foreach (var db in _others.Values) if (db.IsOpen) yield return db;
        if (_module is { IsOpen: true }) yield return _module;
    }

    // ---- Campaign databases ----------------------------------------------------------

    /// <summary>
    /// The builtin campaign tables, laid out like the game's own campaign files (observed in
    /// database/*.sqlite3 written by the original), so the same files work in both engines.
    /// Scripts may read them but not write them.
    /// </summary>
    public const string CampaignTable = "db";
    static readonly string[] BuiltinTables = { "db", "migrations", "meta" };
    const string CampaignSchema =
        "CREATE TABLE IF NOT EXISTS migrations (migration_label text not null, created_at int not null default (strftime('%s','now')), primary key (migration_label));" +
        "INSERT OR IGNORE INTO migrations (migration_label) VALUES ('schema');" +
        "INSERT OR IGNORE INTO migrations (migration_label) VALUES ('20191203190521');" +
        "CREATE TABLE IF NOT EXISTS db (varname varchar not null, playerid varchar not null, vartype tinyint not null, payload blob not null, compressed bool not null default false, unique(varname, playerid));" +
        "CREATE TABLE IF NOT EXISTS meta (key varchar, value varchar);";

    /// <summary>Normalised campaign file name (case-insensitive; letters, digits and underscores), or null if unusable.</summary>
    public static string? CampaignKey(string name)
    {
        var key = new string(name.Where(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_').ToArray()).ToLowerInvariant();
        return key.Length == 0 ? null : key;
    }

    public string CampaignPath(string key) => Path.Combine(DatabaseDir, key + ".sqlite3");

    /// <summary>
    /// The campaign database; with <paramref name="create"/> false, null when it does not exist yet. A file that is
    /// not a usable database is left untouched and reads as null, so campaign calls fall back to their defaults.
    /// </summary>
    public SqlDb? Campaign(string name, bool create = true)
    {
        if (CampaignKey(name) is not { } key) return null;
        if (_campaigns.TryGetValue(key, out var db) && db.IsOpen) return db;
        var path = CampaignPath(key);
        if (!create && !File.Exists(path)) return null;
        try
        {
            db = SqlDb.Open(path);
        }
        catch (IOException e)
        {
            Log($"[campaign] {key}: cannot open ({e.Message})");
            return null;
        }
        db.ProtectedTables = BuiltinTables;
        Opened(db);
        try
        {
            db.Exec(CampaignSchema);
        }
        catch (InvalidOperationException e)
        {
            db.Dispose();
            Log($"[campaign] {key}: unreadable database ({e.Message})");
            return null;
        }
        _campaigns[key] = db;
        return db;
    }

    /// <summary>DestroyCampaignDatabase: closes the database (its queries stop working) and deletes the file.</summary>
    public void DestroyCampaign(string name)
    {
        if (CampaignKey(name) is not { } key) return;
        if (_campaigns.Remove(key, out var db)) db.Dispose();
        var path = CampaignPath(key);
        foreach (var p in new[] { path, path + "-journal", path + "-wal", path + "-shm" })
            try { if (File.Exists(p)) File.Delete(p); }
            catch (IOException e) { Log($"[campaign] cannot delete {p}: {e.Message}"); }
    }

    // ---- Object databases --------------------------------------------------------------

    /// <summary>The database SqlPrepareQueryObject uses for an object: the module's or a player character's; null for anything else.</summary>
    public SqlDb? ObjectDb(GameObject? o) => o switch
    {
        Module m => ModuleDb(m),
        // A possessed familiar and its possessing master both use the master's (the player character's) database.
        Creature c when Players.FamiliarPossession.SessionOf(c) is { } s => CharacterDb(s.PlayerCreature ?? c),
        // NWNX_NWSQLiteExtensions can give every other object but areas an in-memory database of its own.
        not (null or Area) when Nwnx.Plugins.NWSQLiteExtensionsPlugin.DatabaseOnAllObjects(_world) => OtherDb(o),
        _ => null,
    };

    SqlDb OtherDb(GameObject o)
    {
        if (_others.TryGetValue(o.Id, out var open) && open.IsOpen) return open;
        var db = Opened(SqlDb.OpenMemory());
        _others[o.Id] = db;
        return db;
    }

    /// <summary>Every database the game opens passes here, for NWNX_NWSQLiteExtensions' SQL functions.</summary>
    SqlDb Opened(SqlDb db)
    {
        Nwnx.Plugins.NWSQLiteExtensionsPlugin.Opened(_world, db);
        return db;
    }

    public SqlDb ModuleDb(Module m)
    {
        if (_module is { IsOpen: true }) return _module;
        // In memory, as in the original: it starts empty with every server start and persists only in saved games.
        _module = Opened(SqlDb.OpenMemory());
        m.Database = _module;
        return _module;
    }

    /// <summary>A character's database, restored from its BIC's "SQLite" field the first time it is used.</summary>
    public SqlDb CharacterDb(Creature c)
    {
        if (c.Database is { IsOpen: true } open) return open;
        var db = Opened(SqlDb.OpenMemory());
        if (c.Template.GetStruct("SQLite") is { } s && s.GetBytes("Data") is { Length: > 0 } data)
        {
            try { db.Load(CompressedBuffer.Unwrap("SQL3", data)); }
            catch (InvalidDataException e)
            {
                // Keep the stored database untouched on export rather than replacing it with an empty one.
                c.DatabaseUnreadable = true;
                Log($"[sql] {c.Name.FirstString}: character database not restored (it will be kept as stored): {e.Message}");
            }
        }
        c.Database = db;
        _characters[c.Id] = db;
        return db;
    }

    /// <summary>The "SQLite" struct a BIC carries for a character database, or null if the character never opened one.</summary>
    public static GffStruct? CharacterDbStruct(Creature c)
    {
        if (c.Database is not { IsOpen: true } db || c.DatabaseUnreadable) return null;
        var data = CompressedBuffer.Wrap("SQL3", db.Serialize());
        return new GffStruct(10).Set("Data", GffType.Void, data).SetDWord("Size", (uint)data.Length);
    }

    // ---- UUIDs --------------------------------------------------------------------------

    public static bool CanCarryUuid(GameObject? o) =>
        o is Item or Creature or Placeable or Trigger or Door or Waypoint or Store or Encounter or Area;

    /// <summary>GetObjectUUID: the object's UUID, assigning one (from its template if it carried one, else random) on first request.</summary>
    public string GetUuid(GameObject o)
    {
        if (!CanCarryUuid(o)) return "";
        if (o.Uuid != null && Owner(o.Uuid) == o) return o.Uuid;
        if (o.Uuid == null && TemplateAccess.TemplateOf(o)?.GetString("UUID") is { Length: > 0 } stored && TryClaim(o, stored)) return stored;
        string fresh;
        do fresh = Guid.NewGuid().ToString(); while (_uuids.ContainsKey(fresh));
        TryClaim(o, fresh);
        return fresh;
    }

    public void RefreshUuid(GameObject o)
    {
        if (!CanCarryUuid(o)) return;
        if (o.Uuid != null && Owner(o.Uuid) == o) _uuids.Remove(o.Uuid);
        o.Uuid = null;
        string fresh;
        do fresh = Guid.NewGuid().ToString(); while (_uuids.ContainsKey(fresh));
        TryClaim(o, fresh);
    }

    GameObject? Owner(string uuid) => _uuids.TryGetValue(uuid, out var id) && _world.Get(id) is { } o && o.Uuid == uuid ? o : null;

    /// <summary>Gives an object a UUID unless a live object already holds it (then it gets none, like the original, and a warning is logged).</summary>
    public bool TryClaim(GameObject o, string uuid)
    {
        var owner = Owner(uuid);
        if (owner != null && owner != o)
        {
            // NWNX_ON_UUID_COLLISION, OBJECT_SELF the newcomer.
            var ev = Nwnx.NwnxEvent.Begin(_world, "NWNX_ON_UUID_COLLISION", o)?.Add("UUID", uuid);
            ev?.Before();
            Log($"[uuid] {o}: UUID {uuid} already belongs to {owner}; the new object gets none");
            ev?.After();
            return false;
        }
        if (o.Uuid != null && o.Uuid != uuid && Owner(o.Uuid) == o) _uuids.Remove(o.Uuid);
        o.Uuid = uuid;
        _uuids[uuid] = o.Id;
        return true;
    }

    /// <summary>GetObjectByUUID. Objects that carried a UUID in their template but were never asked for it are found too.</summary>
    public GameObject? ByUuid(string uuid)
    {
        if (uuid.Length == 0) return null;
        if (Owner(uuid) is { } o) return o;
        foreach (var candidate in _world.AllObjects)
            if (candidate.Uuid == null && !candidate.IsDestroyed && CanCarryUuid(candidate)
                && string.Equals(TemplateAccess.TemplateOf(candidate)?.GetString("UUID"), uuid, StringComparison.OrdinalIgnoreCase)
                && TryClaim(candidate, uuid))
                return candidate;
        return null;
    }

    // ---- TEMP: templates ------------------------------------------------------------------

    /// <summary>Stores (or with null, removes) a template that overrides every other resource source.</summary>
    public void SetTemplate(string resref, ushort type, byte[]? data)
    {
        if (_temp == null)
        {
            _temp = new MemorySource("TEMP:");
            _world.Resources.AddFirst(_temp);
        }
        _temp.Set(ResKey.Of(resref, type), data);
    }

    public void Dispose()
    {
        // The world is shutting down: so does what NWNX plugins keep open for it.
        _world.System<Nwnx.NwnxRegistry>().Shutdown();
        foreach (var db in _campaigns.Values) db.Dispose();
        foreach (var db in _characters.Values) db.Dispose();
        _module?.Dispose();
        _campaigns.Clear();
        _characters.Clear();
        _module = null;
    }
}

/// <summary>In-memory resources (the TEMP: alias JsonToTemplate writes to).</summary>
public sealed class MemorySource : IResourceSource
{
    readonly Dictionary<ResKey, byte[]> _data = new();
    public string Description { get; }
    public MemorySource(string description) { Description = description; }
    public void Set(ResKey key, byte[]? data) { lock (_data) { if (data == null) _data.Remove(key); else _data[key] = data; } }
    public bool Contains(ResKey key) { lock (_data) return _data.ContainsKey(key); }
    public byte[]? Read(ResKey key) { lock (_data) return _data.TryGetValue(key, out var d) ? d : null; }
    public IEnumerable<ResKey> Keys { get { lock (_data) return _data.Keys.ToList(); } }
}
