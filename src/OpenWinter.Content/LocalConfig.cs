using System.Text.Json;

namespace OpenWinter.Content;

/// <summary>Machine-specific paths from config/local.json (gitignored), found by walking up from a start directory.</summary>
public sealed class LocalConfig
{
    public string GameRoot { get; set; } = "";
    public string UserRoot { get; set; } = "";
    public string Module { get; set; } = "";
    public string Language { get; set; } = "en";
    /// <summary>Let the engine write campaign databases and character exports into the real user folder.</summary>
    public bool LiveUserData { get; set; }
    /// <summary>The monitor dev windows open on (e.g. "DISPLAY2"), so test runs leave the main one free.</summary>
    public string? DevDisplay { get; set; }
    /// <summary>Optional folder holding the neverwinter.nim tools (nwn_gff, nwn_erf); tests that cross-check against them skip without it.</summary>
    public string? NimTools { get; set; }

    /// <summary>The folder development runs write user data into: &lt;repo&gt;/out/sandbox-userdata.</summary>
    public string? SandboxFolder { get; private set; }

    static LocalConfig WithSandbox(LocalConfig c, string configPath)
    {
        var repo = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(configPath)))!;
        c.SandboxFolder = Path.Combine(repo, "out", "sandbox-userdata");
        return c;
    }

    public static LocalConfig? Find(string? startDir = null)
    {
        var env = Environment.GetEnvironmentVariable("OPENWINTER_CONFIG");
        if (!string.IsNullOrEmpty(env) && File.Exists(env)) return Load(env);
        // Exported clients have no res:// folder on disk; they look beside the program instead.
        var dir = new DirectoryInfo(string.IsNullOrEmpty(startDir) ? AppContext.BaseDirectory : startDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "config", "local.json");
            if (File.Exists(candidate)) return Load(candidate);
            dir = dir.Parent;
        }
        return null;
    }

    /// <summary>
    /// Settings for an installed copy with no config/local.json: the game from the usual Steam, GOG and Beamdog
    /// folders, the player's NWN user folder (read only), and writes kept in <paramref name="dataDir"/>.
    /// Null when no NWN:EE installation is found.
    /// </summary>
    public static LocalConfig? Detect(string dataDir)
    {
        var game = GameFolderCandidates().FirstOrDefault(IsGameFolder);
        if (game == null) return null;
        return new LocalConfig { GameRoot = game, UserRoot = DefaultUserFolder(), SandboxFolder = dataDir };
    }

    /// <summary>A folder holding NWN:EE (its data/nwn_base.key).</summary>
    public static bool IsGameFolder(string dir) => File.Exists(Path.Combine(dir, "data", "nwn_base.key"));

    static IEnumerable<string> GameFolderCandidates()
    {
        foreach (var library in SteamLibraries())
            yield return Path.Combine(library, "steamapps", "common", "Neverwinter Nights");
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).Select(d => d.RootDirectory.FullName))
        {
            yield return Path.Combine(drive, "GOG Games", "Neverwinter Nights Enhanced Edition");
            yield return Path.Combine(drive, "Program Files (x86)", "GOG Galaxy", "Games", "Neverwinter Nights Enhanced Edition");
        }
        yield return Path.Combine(appData, "Beamdog Client", "00829");
        yield return Path.Combine(home, "Beamdog Library", "00829");
        yield return Path.Combine(home, "GOG Games", "Neverwinter Nights Enhanced Edition");
    }

    /// <summary>Steam's library folders, from its libraryfolders.vdf.</summary>
    static IEnumerable<string> SteamLibraries()
    {
        var roots = new List<string>();
        if (OperatingSystem.IsWindows())
        {
            if (Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) is string path) roots.Add(path);
            roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            roots.Add(Path.Combine(home, ".local", "share", "Steam"));
            roots.Add(Path.Combine(home, ".steam", "steam"));
            roots.Add(Path.Combine(home, "Library", "Application Support", "Steam"));
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            if (seen.Add(Path.GetFullPath(root))) yield return root;
            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;
            foreach (var line in File.ReadLines(vdf))
            {
                // "path"		"D:\\Games\\Steam"
                var parts = line.Split('"').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                if (parts.Length != 2 || parts[0] != "path") continue;
                var library = parts[1].Replace(@"\\", @"\");
                if (seen.Add(Path.GetFullPath(library))) yield return library;
            }
        }
    }

    /// <summary>The game's own user folder: Documents/Neverwinter Nights (~/.local/share on Linux).</summary>
    static string DefaultUserFolder()
    {
        if (OperatingSystem.IsLinux())
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "Neverwinter Nights");
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Neverwinter Nights");
    }

    public static LocalConfig Load(string path) =>
        WithSandbox(JsonSerializer.Deserialize<LocalConfig>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new LocalConfig(), path);

    /// <summary>The user-data root writes should go to: the sandbox unless live data was enabled.</summary>
    public string? WriteRoot => LiveUserData ? null : SandboxFolder;
}
