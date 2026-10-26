using System.Globalization;
using System.Numerics;
using System.Text.Json.Nodes;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Aurora.Formats;

namespace OpenWinter.Game.Persistence;

/// <summary>
/// Get/Set/DeleteCampaign* storage: one row per (variable name, player) in the campaign
/// database's builtin <c>db</c> table, in the layout the original game uses (vartype is a
/// type letter, payload the value, compressed marks a "CPDB" compressed buffer). A variable
/// name is unique regardless of type; reading it as another type yields the default value.
/// </summary>
public static class CampaignDatabase
{
    /// <summary>vartype letters: 'I'nt, 'F'loat, 'S'tring, 'V'ector, 'L'ocation, 'O'bject, 'J'son.</summary>
    public const int TypeInt = 'I', TypeFloat = 'F', TypeString = 'S', TypeVector = 'V', TypeLocation = 'L', TypeObject = 'O', TypeJson = 'J';

    static PersistenceSystem Sys(World w) => w.System<PersistenceSystem>();

    /// <summary>Key for player-scoped variables: player (login) name followed by the character name; "" when no player is given.</summary>
    public static string PlayerId(World world, GameObject? player)
    {
        if (player is not Creature { Player: { } session } c) return "";
        var identity = session.PlayerCreature ?? c;
        return session.PlayerName + identity.Name.Resolve(world.Resources.Talk);
    }

    /// <summary>Writes a variable; <paramref name="compressed"/> marks a value bound as a "CPDB" compressed buffer (objects).</summary>
    public static void Set(World world, string campaign, string name, GameObject? player, int type, Action<SqlStatement, int> bindValue, bool compressed = false)
    {
        if (Sys(world).Campaign(campaign) is not { } db) return;
        using var q = db.PrepareInternal($"INSERT OR REPLACE INTO {PersistenceSystem.CampaignTable} (varname, playerid, vartype, payload, compressed) VALUES (?1, ?2, ?3, ?4, ?5)");
        q.BindText(1, name);
        q.BindText(2, PlayerId(world, player));
        q.BindInt(3, type);
        bindValue(q, 4);
        q.BindInt(5, compressed ? 1 : 0);
        if (q.Step() is not (SQLitePCL.raw.SQLITE_DONE or SQLitePCL.raw.SQLITE_ROW))
            Sys(world).Log($"[campaign] {campaign}.{name}: {db.ErrorMessage}");
    }

    /// <summary>Reads a variable of the given type.</summary>
    public static T Get<T>(World world, string campaign, string name, GameObject? player, int type, Func<CampaignValue, T> read, T fallback)
    {
        if (Sys(world).Campaign(campaign, create: false) is not { } db) return fallback;
        using var q = db.PrepareInternal($"SELECT vartype, payload, compressed FROM {PersistenceSystem.CampaignTable} WHERE varname = ?1 AND playerid = ?2");
        q.BindText(1, name);
        q.BindText(2, PlayerId(world, player));
        if (q.Step() != SQLitePCL.raw.SQLITE_ROW || q.Int(0) != type) return fallback;
        if (q.Int(2) == 0) return read(new CampaignValue(q, 1, null));
        try { return read(new CampaignValue(null, 0, CompressedBuffer.Unwrap("CPDB", q.Blob(1)))); }
        catch (InvalidDataException e)
        {
            Sys(world).Log($"[campaign] {campaign}.{name}: {e.Message}");
            return fallback;
        }
    }

    public static void Delete(World world, string campaign, string name, GameObject? player)
    {
        if (Sys(world).Campaign(campaign, create: false) is not { } db) return;
        using var q = db.PrepareInternal($"DELETE FROM {PersistenceSystem.CampaignTable} WHERE varname = ?1 AND playerid = ?2");
        q.BindText(1, name);
        q.BindText(2, PlayerId(world, player));
        q.Step();
    }

    // ---- Value encodings shared with the Sql* functions ----------------------------------

    static string F(float f) => f.ToString("R", CultureInfo.InvariantCulture);

    /// <summary>Vectors are stored as a json array text: [x,y,z].</summary>
    public static string VectorText(Vector3 v) => $"[{F(v.X)},{F(v.Y)},{F(v.Z)}]";

    /// <summary>Parses [x,y,z], "x y z" or "x,y,z"; zero vector when unreadable.</summary>
    public static Vector3 ParseVector(string text)
    {
        var parts = text.Trim().Trim('[', ']', '(', ')', '{', '}').Split(new[] { ',', ' ', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return Vector3.Zero;
        float P(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0f;
        return new Vector3(P(parts[0]), P(parts[1]), P(parts[2]));
    }

    /// <summary>Locations are stored by area resref (areas get new object ids every run).</summary>
    public static string LocationText(World world, Location l)
    {
        var area = world.Get(l.Area) as Area;
        var o = new JsonObject
        {
            ["area"] = area?.ResRef ?? "",
            ["areatag"] = area?.Tag ?? "",
            ["position"] = new JsonArray(l.Position.X, l.Position.Y, l.Position.Z),
            ["facing"] = l.FacingDegrees,
        };
        return o.ToJsonString();
    }

    public static Location ParseLocation(World world, string text)
    {
        try
        {
            if (JsonNode.Parse(text) is not JsonObject o) return Location.Invalid;
            var resref = o["area"]?.GetValue<string>() ?? "";
            var tag = o["areatag"]?.GetValue<string>() ?? "";
            var area = world.Areas.FirstOrDefault(a => resref.Length > 0 && a.ResRef.Equals(resref, StringComparison.OrdinalIgnoreCase))
                       ?? world.Areas.FirstOrDefault(a => tag.Length > 0 && a.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase));
            if (area == null) return Location.Invalid;
            var p = o["position"] as JsonArray;
            float C(int i) => p != null && i < p.Count && p[i] is JsonValue v && v.TryGetValue<double>(out var d) ? (float)d : 0f;
            float facing = o["facing"] is JsonValue fv && fv.TryGetValue<double>(out var fd) ? (float)fd : 0f;
            return new Location(area.Id, new Vector3(C(0), C(1), C(2)), facing);
        }
        catch (System.Text.Json.JsonException) { return Location.Invalid; }
        catch (InvalidOperationException) { return Location.Invalid; }
    }
}

/// <summary>A stored campaign value: a column of the current row, or the payload of a decompressed buffer.</summary>
public sealed class CampaignValue
{
    readonly SqlStatement? _q;
    readonly int _column;
    readonly byte[]? _bytes;

    public CampaignValue(SqlStatement? q, int column, byte[]? bytes) { _q = q; _column = column; _bytes = bytes; }

    public string Text() => _bytes != null ? System.Text.Encoding.UTF8.GetString(_bytes) : _q!.Text(_column);
    public byte[] Blob() => _bytes ?? _q!.Blob(_column);
    public long Int() => _bytes != null ? (long.TryParse(Text(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var l) ? l : 0) : _q!.Int(_column);
    public double Double() => _bytes != null ? (double.TryParse(Text(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0) : _q!.Double(_column);
}
