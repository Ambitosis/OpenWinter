using OpenWinter.Aurora.Formats;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Persistence;

/// <summary>
/// The server vault: &lt;user&gt;/servervault/&lt;folder&gt;/&lt;character&gt;.bic, the folder named by the player's public
/// key, or by player name when the server keeps vaults that way (see <see cref="Folder"/>). Characters are listed and
/// loaded at login and written by ExportSingleCharacter / ExportAllCharacters and on logout, with their SQLite database.
/// </summary>
public static class ServerVault
{
    /// <summary>Folder used when a character has no player (login) name.</summary>
    public const string LocalFolder = "local";

    public static string VaultDir(World world, Creature c) =>
        Path.Combine(world.System<PersistenceSystem>().UserRoot, "servervault", Folder(world, c.Player ?? c.FamiliarController));

    /// <summary>
    /// A player's vault folder: nwserver names it by the public key unless server.vault.by-player-name is on (measured
    /// on nwserver: a character in servervault/&lt;key&gt;/ is listed, one in servervault/&lt;player name&gt;/ isn't). A
    /// player without a key goes by name.
    /// </summary>
    public static string Folder(World world, PlayerSession? player)
    {
        if (player == null) return LocalFolder;
        bool byName = world.System<PersistenceSystem>().VaultByPlayerName || player.PublicKey.Length == 0;
        var name = Sanitize(byName ? player.PlayerName : player.PublicKey, keepCase: true);
        return name.Length > 0 ? name : LocalFolder;
    }

    /// <summary>Character file names: lower-case letters and digits from the name, at most 16 characters.</summary>
    public static string BaseFileName(World world, Creature c)
    {
        var name = Sanitize(c.Name.Resolve(world.Resources.Talk), keepCase: false);
        if (name.Length > 16) name = name[..16];
        return name.Length > 0 ? name : "character";
    }

    static string Sanitize(string s, bool keepCase)
    {
        var chars = s.Where(ch => keepCase ? !Path.GetInvalidFileNameChars().Contains(ch) && ch != ' ' : char.IsAsciiLetterOrDigit(ch)).ToArray();
        var r = new string(chars).Trim('.');
        return keepCase ? r : r.ToLowerInvariant();
    }

    /// <summary>
    /// Vault folders to read a player's characters from: the folder exports go to, then (when
    /// development runs write to a sandbox) the real user folder, read-only.
    /// </summary>
    static IEnumerable<string> ReadDirs(World world, string folder)
    {
        var name = Sanitize(folder, keepCase: true);
        if (name.Length == 0) name = LocalFolder;
        var write = Path.Combine(world.System<PersistenceSystem>().UserRoot, "servervault", name);
        yield return write;
        var real = Path.Combine(world.Resources.Paths.UserRoot, "servervault", name);
        if (!real.Equals(write, StringComparison.OrdinalIgnoreCase)) yield return real;
    }

    /// <summary>The characters in a vault folder (<see cref="Folder"/>), to choose from on a server-vault server.</summary>
    public static List<Net.CharacterEntry> List(World world, string folder)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<Net.CharacterEntry>();
        var talk = world.Resources.Talk;
        var classTable = world.Resources.TwoDA("classes");
        foreach (var dir in ReadDirs(world, folder))
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var path in Directory.EnumerateFiles(dir, "*.bic").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var file = Path.GetFileNameWithoutExtension(path);
                if (!seen.Add(file)) continue;
                try
                {
                    var bic = Gff.Read(File.ReadAllBytes(path));
                    string first = bic.GetLocString("FirstName").Resolve(talk), last = bic.GetLocString("LastName").Resolve(talk);
                    var classes = bic.GetList("ClassList").Select(c => (Class: (int)c.GetInt("Class"), Level: (int)c.GetInt("ClassLevel"))).ToList();
                    list.Add(new Net.CharacterEntry
                    {
                        File = file,
                        Name = (first + " " + last).Trim(),
                        Classes = string.Join(" / ", classes.Select(c => $"{world.Resources.TlkString(unchecked((uint)classTable.GetInt(c.Class, "Name", 0)))} {c.Level}")),
                        Level = classes.Sum(c => c.Level),
                        Portrait = bic.GetString("Portrait"),
                    });
                }
                catch (Exception e) when (e is IOException or InvalidDataException or EndOfStreamException) { }
            }
        }
        return list;
    }

    /// <summary>A character file from a vault folder, or null (unknown file, bad name, unreadable).</summary>
    public static GffStruct? Load(World world, string folder, string file)
    {
        if (file.Length == 0 || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || file.Contains("..")) return null;
        foreach (var dir in ReadDirs(world, folder))
        {
            var path = Path.Combine(dir, file + ".bic");
            if (File.Exists(path)) return Gff.Read(File.ReadAllBytes(path));
        }
        return null;
    }

    /// <summary>Writes the character's BIC and returns its path ("" when an NWNX_ON_SERVER_CHARACTER_SAVE subscriber skipped it).</summary>
    public static string Export(World world, Creature c)
    {
        c = c.Player?.PlayerCreature ?? c;
        // NWNX_ON_SERVER_CHARACTER_SAVE; a skip writes nothing.
        var ev = Nwnx.NwnxEvent.Begin(world, "NWNX_ON_SERVER_CHARACTER_SAVE", c);
        if (ev?.Before() == true) { ev.After(); return ""; }
        using var after = new Nwnx.NwnxEvent.AfterScope(ev);
        var sys = world.System<PersistenceSystem>();
        var dir = VaultDir(world, c);
        Directory.CreateDirectory(dir);
        var uuid = sys.GetUuid(c);
        if (c.VaultFile.Length == 0) c.VaultFile = ChooseFileName(world, c, dir, uuid);
        var root = ObjectSerializer.CreatureGff(world, c, 0xFFFFFFFF, asCharacter: true);
        var path = Path.Combine(dir, c.VaultFile + ".bic");
        var temp = path + ".tmp";
        File.WriteAllBytes(temp, Gff.Write(root, "BIC "));
        File.Move(temp, path, overwrite: true);
        return path;
    }

    /// <summary>The base name if it is free or already holds this character (same UUID); otherwise a numbered variant.</summary>
    static string ChooseFileName(World world, Creature c, string dir, string uuid)
    {
        var baseName = BaseFileName(world, c);
        for (int n = 0; n < 1000; n++)
        {
            var name = n == 0 ? baseName : baseName[..Math.Min(baseName.Length, 15 - (int)Math.Log10(n))] + n;
            var path = Path.Combine(dir, name + ".bic");
            if (!File.Exists(path)) return name;
            try
            {
                var existing = Gff.Read(File.ReadAllBytes(path)).GetString("UUID");
                if (existing.Equals(uuid, StringComparison.OrdinalIgnoreCase)) return name;
            }
            catch (IOException) { }
        }
        return baseName;
    }
}
