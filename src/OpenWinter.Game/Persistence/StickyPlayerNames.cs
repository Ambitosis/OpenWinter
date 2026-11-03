using System.Text;
using OpenWinter.Aurora.Formats;

namespace OpenWinter.Game.Persistence;

/// <summary>
/// Sticky player names (nwserver's server.vault.sticky-player-names): a player name stays with the first public key
/// that logged in with it, and another key using the name is refused. nwserver keeps the pairs in the user folder's
/// override/knownservernames.2da (CDKey, ConnectType, PlayerName), one row per name, so a key may hold several names;
/// names compare without case.
/// </summary>
public static class StickyPlayerNames
{
    public const string FileName = "knownservernames.2da";
    /// <summary>The ConnectType nwserver writes for a player.</summary>
    const string PlayerConnection = "016";

    /// <summary>Whether <paramref name="key"/> may use <paramref name="name"/>; a name new to the server is recorded for it.</summary>
    public static bool Claim(World world, string name, string key)
    {
        var write = Path.Combine(world.System<PersistenceSystem>().UserRoot, "override", FileName);
        var rows = Read(write) ?? Read(Path.Combine(world.Resources.Paths.UserRoot, "override", FileName)) ?? [];
        var owners = rows.Where(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
        if (owners.Count > 0) return owners.Any(r => r.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        rows.Add((key, PlayerConnection, name));
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(write)!);
            var temp = write + ".tmp";
            File.WriteAllText(temp, Format(rows), Encoding.ASCII);
            File.Move(temp, write, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"[server] recording the player name {name} failed: {e.Message}");
        }
        return true;
    }

    static List<(string Key, string Type, string Name)>? Read(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var table = TwoDA.Parse(File.ReadAllBytes(path));
            return Enumerable.Range(0, table.RowCount)
                .Select(i => (table.Get(i, "CDKey"), table.Get(i, "ConnectType"), table.Get(i, "PlayerName")))
                .Where(r => r.Item1.Length > 0 && r.Item3.Length > 0).ToList();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or IndexOutOfRangeException or FormatException)
        {
            return null;
        }
    }

    /// <summary>The 2DA as nwserver lays it out: quoted keys and names, columns padded to line up.</summary>
    static string Format(List<(string Key, string Type, string Name)> rows)
    {
        static string Quote(string s) => "\"" + s.Replace("\"", "") + "\"";
        int keyWidth = Math.Max(12, rows.Max(r => Quote(r.Key).Length) + 2);
        var sb = new StringBuilder("2DA V2.0\n\n");
        sb.Append(new string(' ', 8)).Append("CDKey".PadRight(keyWidth)).Append("ConnectType  ").Append("PlayerName\n");
        for (int i = 0; i < rows.Count; i++)
            sb.Append(i.ToString("D5").PadRight(8)).Append(Quote(rows[i].Key).PadRight(keyWidth))
                .Append(rows[i].Type.PadRight(13)).Append(Quote(rows[i].Name)).Append('\n');
        return sb.ToString();
    }
}
