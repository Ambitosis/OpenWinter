using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;

namespace OpenWinter.Game.SaveGames;

public enum SaveSlotKind { Manual, Quick, Auto }

/// <summary>
/// One save game on disk, as listed for the Save/Load window. <see cref="ModulePath"/> is the module
/// file or folder the game was started from (to continue it from the main menu); <see cref="PlayedSeconds"/>
/// the total time played; <see cref="HasScreen"/> whether a screen.tga thumbnail was stored.
/// </summary>
public sealed record SaveInfo(
    string Folder, string Name, SaveSlotKind Kind, DateTime SavedAt, string Module, string AreaName, string AreaResRef,
    string PlayerName, string GameTime, string ModulePath = "", double PlayedSeconds = 0, bool HasScreen = false);

/// <summary>A save loaded into a new world (not yet running: the caller swaps it in).</summary>
public sealed class LoadedGame
{
    public required World World;
    public required SaveInfo Info;
    public required WorldSnapshot.Restored Restored;
}

/// <summary>
/// Single-player save games, laid out like NWN's: <c>saves/NNNNNN - Name/</c> holding
/// <c>savegame.sav</c> (an ERF of type "SAV "), the player BICs, <c>savename.txt</c> and an
/// optional <c>screen.tga</c>. Slot 000000 is the quick save and 000001 the autosave.
/// Saves live under <see cref="PersistenceSystem.UserRoot"/> (the dev sandbox unless live user
/// data is enabled). See docs/SAVEGAME.md for the format.
/// </summary>
public static class SaveGameService
{
    public const string SaveFile = "savegame.sav";
    public const string NameFile = "savename.txt";
    public const string ScreenFile = "screen.tga";
    public const string SnapshotResRef = "openwinter";
    public const string InfoResRef = "saveinfo";
    const string QuickFolder = "000000 - QUICKSAVE", AutoFolder = "000001 - AUTOSAVE";

    public static string SavesDir(World world) => Path.Combine(world.System<PersistenceSystem>().UserRoot, "saves");

    public static Action<string> Log = Console.WriteLine;

    /// <summary>Time played before this world started (the loaded save's play time); the world's own clock adds to it.</summary>
    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<World, System.Runtime.CompilerServices.StrongBox<double>> PlayedBefore = new();

    /// <summary>Total seconds played in this game, across saves and loads.</summary>
    public static double PlayedSeconds(World world) =>
        (PlayedBefore.TryGetValue(world, out var box) ? box.Value : 0) + world.Time;

    /// <summary>The module path a server was started from (written into saves so the main menu can continue them).</summary>
    static string ModulePathOf(World world) => world.Output is GameServer { Options.ModulePath: { } path } ? path : "";

    // ---------------------------------------------------------------- saving

    /// <summary>
    /// Saves the world between steps. <paramref name="overwriteFolder"/> replaces that manual save
    /// (otherwise a manual save with the same name is replaced, or a new slot is used).
    /// The save is written to a temporary folder and moved into place, so a failed save never
    /// damages an existing one.
    /// </summary>
    public static SaveInfo Save(World world, string name, SaveSlotKind kind = SaveSlotKind.Manual, byte[]? screenshotTga = null, string? overwriteFolder = null)
    {
        if (world.Scripts?.VM.Running.Count > 0) throw new InvalidOperationException("cannot save while a script is running");
        name = kind switch { SaveSlotKind.Quick => "Quick Save", SaveSlotKind.Auto => "Auto Save", _ => string.IsNullOrWhiteSpace(name) ? "Saved Game" : name.Trim() };
        var dir = SavesDir(world);
        Directory.CreateDirectory(dir);
        string folder = kind switch
        {
            SaveSlotKind.Quick => QuickFolder,
            SaveSlotKind.Auto => AutoFolder,
            _ => ManualFolder(dir, name, overwriteFolder),
        };

        var snapshot = WorldSnapshot.Capture(world, out var warnings);
        var pc = AuroraSaveResources.Players(world).FirstOrDefault();
        var info = new SaveInfo(folder, name, kind, DateTime.Now, world.Module.Name.Resolve(world.Resources.Talk),
            pc?.Area?.Name.Resolve(world.Resources.Talk) ?? "", pc?.Area?.ResRef ?? "", pc?.Name.Resolve(world.Resources.Talk) ?? "",
            GameTime(world), ModulePathOf(world), PlayedSeconds(world), screenshotTga is { Length: > 18 });

        var entries = AuroraSaveResources.Build(world, w => Log($"[save] {w}"));
        entries.Add(new ErfEntry(InfoResRef, ResType.Gff, Gff.Write(InfoGff(info), "SINF")));
        entries.Add(new ErfEntry(SnapshotResRef, ResType.Gff, Gff.Write(SnapshotGff(snapshot), "ELYS")));
        foreach (var w in warnings) Log($"[save] {w}");

        var temp = Path.Combine(dir, ".tmp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllBytes(Path.Combine(temp, SaveFile), Erf.Write(entries, "SAV "));
            File.WriteAllText(Path.Combine(temp, NameFile), name, OpenWinter.Aurora.Text.Windows1252);
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var player in AuroraSaveResources.Players(world))
                if (AuroraSaveResources.Bic(world, player, w => Log($"[save] {w}")) is { } bic)
                {
                    var file = BicName(player, used);
                    File.WriteAllBytes(Path.Combine(temp, file + ".bic"), Gff.Write(bic, "BIC "));
                }
            if (screenshotTga is { Length: > 18 }) File.WriteAllBytes(Path.Combine(temp, ScreenFile), screenshotTga);
            Replace(Path.Combine(dir, folder), temp);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
        Log($"[save] saved '{name}' to {folder}");
        return info;
    }

    /// <summary>The folder of a manual save: an explicit slot to overwrite, the existing save with this name, or the next free number.</summary>
    static string ManualFolder(string dir, string name, string? overwrite)
    {
        if (!string.IsNullOrEmpty(overwrite) && IsSlotFolder(overwrite) && Directory.Exists(Path.Combine(dir, overwrite))
            && !overwrite.StartsWith("000000", StringComparison.Ordinal) && !overwrite.StartsWith("000001", StringComparison.Ordinal))
            return overwrite;
        int next = 2;
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            var f = Path.GetFileName(d);
            if (!IsSlotFolder(f)) continue;
            int n = int.Parse(f[..6], CultureInfo.InvariantCulture);
            if (n >= 2 && ReadName(d).Equals(name, StringComparison.OrdinalIgnoreCase)) return f;
            next = Math.Max(next, n + 1);
        }
        return $"{next:D6} - {SafeName(name)}";
    }

    static bool IsSlotFolder(string f) => f.Length >= 9 && f[..6].All(char.IsAsciiDigit) && f[6..9] == " - " && !f.Contains("..") && f.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    static string SafeName(string name)
    {
        var s = new string(name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) || ch < ' ' ? '_' : ch).ToArray()).Trim().TrimEnd('.');
        if (s.Length > 40) s = s[..40].TrimEnd();
        return s.Length == 0 ? "Saved Game" : s;
    }

    static string BicName(Creature pc, HashSet<string> used)
    {
        var baseName = pc.VaultFile.Length > 0 ? pc.VaultFile : new string(pc.Name.FirstString.ToLowerInvariant().Where(char.IsAsciiLetterOrDigit).ToArray());
        if (baseName.Length == 0) baseName = "player";
        if (baseName.Length > 16) baseName = baseName[..16];
        var name = baseName;
        for (int i = 1; !used.Add(name); i++) name = baseName[..Math.Min(baseName.Length, 14)] + i.ToString("D2", CultureInfo.InvariantCulture);
        return name;
    }

    /// <summary>Moves the finished save into place, keeping the previous one until the move succeeded.</summary>
    static void Replace(string target, string temp)
    {
        string? backup = null;
        if (Directory.Exists(target))
        {
            backup = target + ".old-" + Guid.NewGuid().ToString("N")[..8];
            Directory.Move(target, backup);
        }
        try { Directory.Move(temp, target); }
        catch
        {
            if (backup != null) Directory.Move(backup, target);
            throw;
        }
        if (backup != null) TryDelete(backup);
    }

    static void TryDelete(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    static string GameTime(World world)
    {
        var c = world.Clock;
        return $"Year {c.Year}, month {c.Month}, day {c.Day}, {c.Hour:D2}:{c.Minute:D2}";
    }

    static GffStruct InfoGff(SaveInfo info) => new GffStruct()
        .SetString("Name", info.Name).SetInt("Kind", (int)info.Kind)
        .SetString("SavedAt", info.SavedAt.ToString("o", CultureInfo.InvariantCulture))
        .SetString("Module", info.Module).SetString("AreaName", info.AreaName).SetResRef("AreaResRef", info.AreaResRef)
        .SetString("PlayerName", info.PlayerName).SetString("GameTime", info.GameTime)
        .SetString("ModulePath", info.ModulePath).SetString("PlayedSeconds", info.PlayedSeconds.ToString("R", CultureInfo.InvariantCulture));

    static GffStruct SnapshotGff(JsonObject snapshot)
    {
        var json = Encoding.UTF8.GetBytes(snapshot.ToJsonString());
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Fastest)) gz.Write(json);
        return new GffStruct()
            .SetInt("Format", WorldSnapshot.FormatVersion)
            .SetString("Engine", snapshot["engine"]?.GetValue<string>() ?? "")
            .SetString("Encoding", "json+gzip")
            .SetDWord("RawSize", (uint)json.Length)
            .Set("Snapshot", GffType.Void, ms.ToArray());
    }

    // ---------------------------------------------------------------- listing

    /// <summary>Saves for the Save/Load window, newest first.</summary>
    public static List<SaveInfo> List(World world) => List(SavesDir(world));

    /// <summary>Saves in a saves folder, newest first (the main menu reads them without a running world).</summary>
    public static List<SaveInfo> List(string dir)
    {
        var result = new List<SaveInfo>();
        if (!Directory.Exists(dir)) return result;
        foreach (var d in Directory.EnumerateDirectories(dir))
        {
            var folder = Path.GetFileName(d);
            if (!IsSlotFolder(folder)) continue;
            bool own = File.Exists(Path.Combine(d, SaveFile)) && !IsNativeArchive(Path.Combine(d, SaveFile));
            if (!own && NativeSaveGame.Archive(d) == null) continue;
            try { result.Add(own ? ReadInfo(d) : NativeSaveGame.ReadInfo(d)); }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or EndOfStreamException)
            {
                Log($"[save] {folder}: unreadable ({e.Message})");
            }
        }
        result.Sort((a, b) => b.SavedAt.CompareTo(a.SavedAt));
        return result;
    }

    /// <summary>The original's save archives are module ERFs ("MOD "); OpenWinter's are "SAV ".</summary>
    static bool IsNativeArchive(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            var head = new byte[4];
            return fs.Read(head, 0, 4) == 4 && Encoding.ASCII.GetString(head) == "MOD ";
        }
        catch (IOException) { return false; }
    }

    static string ReadName(string dir)
    {
        try { return File.Exists(Path.Combine(dir, NameFile)) ? File.ReadAllText(Path.Combine(dir, NameFile), OpenWinter.Aurora.Text.Windows1252).Trim() : ""; }
        catch (IOException) { return ""; }
    }

    static SaveInfo ReadInfo(string dir)
    {
        var folder = Path.GetFileName(dir);
        using var erf = new ErfSource(Path.Combine(dir, SaveFile));
        var kind = folder.StartsWith("000000", StringComparison.Ordinal) ? SaveSlotKind.Quick
            : folder.StartsWith("000001", StringComparison.Ordinal) ? SaveSlotKind.Auto : SaveSlotKind.Manual;
        var data = erf.Read(ResKey.Of(InfoResRef, ResType.Gff));
        bool screen = File.Exists(Path.Combine(dir, ScreenFile));
        if (data == null)
            return new SaveInfo(folder, ReadName(dir), kind, Directory.GetLastWriteTime(dir), "", "", "", "", "", HasScreen: screen);
        var g = Gff.Read(data);
        DateTime.TryParse(g.GetString("SavedAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at);
        double.TryParse(g.GetString("PlayedSeconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out var played);
        return new SaveInfo(folder, ReadName(dir) is { Length: > 0 } n ? n : g.GetString("Name"), kind, at == default ? Directory.GetLastWriteTime(dir) : at,
            g.GetString("Module"), g.GetString("AreaName"), g.GetString("AreaResRef"), g.GetString("PlayerName"), g.GetString("GameTime"),
            g.GetString("ModulePath"), played, screen);
    }

    /// <summary>A save's screen.tga thumbnail, or null when it has none (or the folder is not a save slot).</summary>
    public static byte[]? ReadScreen(string savesDir, string folder)
    {
        if (!IsSlotFolder(folder)) return null;
        var path = Path.Combine(savesDir, folder, ScreenFile);
        try { return File.Exists(path) ? File.ReadAllBytes(path) : null; }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public static string? QuickSaveFolder(World world) => Directory.Exists(Path.Combine(SavesDir(world), QuickFolder)) ? QuickFolder : null;

    // ---------------------------------------------------------------- loading

    /// <summary>
    /// Loads a save into a new world built from the same module (<paramref name="createWorld"/>
    /// overrides how; the default reuses <paramref name="current"/>'s resources and script
    /// definitions). The module's load and spawn events do not run, as in NWN.
    /// </summary>
    public static LoadedGame Load(World current, string folder, Func<World>? createWorld = null)
    {
        if (!IsSlotFolder(folder)) throw new ArgumentException($"not a save folder: '{folder}'");
        var dir = Path.Combine(SavesDir(current), folder);
        var path = Path.Combine(dir, SaveFile);
        // A save made by the original game: its own module archive and object state (NativeSaveGame).
        if ((!File.Exists(path) || IsNativeArchive(path)) && NativeSaveGame.Archive(dir) != null)
        {
            var native = NativeSaveGame.Load(current, dir, ModulePathOf(current) is { Length: > 0 } m ? m : null);
            foreach (var w in native.Restored.Warnings) Log($"[load] {w}");
            Log($"[load] loaded the original's save '{native.Info.Name}' ({folder})");
            return native;
        }
        if (!File.Exists(path)) throw new FileNotFoundException($"no saved game in '{folder}'");
        JsonObject snapshot;
        using (var erf = new ErfSource(path))
        {
            var data = erf.Read(ResKey.Of(SnapshotResRef, ResType.Gff))
                ?? throw new InvalidDataException("this save has no OpenWinter engine state (saves made by other engines cannot be loaded yet)");
            snapshot = ReadSnapshot(Gff.Read(data));
        }
        var info = ReadInfo(dir);
        // A save of another module (loaded from inside a game) is restored into that module, not the running one.
        string? otherModule = OtherModule(current, info.ModulePath);
        var world = (createWorld ?? (() => FreshWorld(current, otherModule)))();
        PlayedBefore.AddOrUpdate(world, new System.Runtime.CompilerServices.StrongBox<double>(info.PlayedSeconds));
        var restored = WorldSnapshot.Apply(world, snapshot);
        foreach (var w in restored.Warnings) Log($"[load] {w}");
        Log($"[load] loaded '{info.Name}' ({folder})");
        return new LoadedGame { World = world, Info = info, Restored = restored };
    }

    /// <summary>The module a save was made in when it isn't the one running (and can be found), else null.</summary>
    static string? OtherModule(World current, string savedPath)
    {
        if (savedPath.Length == 0 || !(File.Exists(savedPath) || Directory.Exists(savedPath))) return null;
        var running = ModulePathOf(current);
        if (running.Length == 0) return null;
        // The path comes from the save file: follow it only into the module folders (or the running module's).
        string? runningFolder;
        try { runningFolder = Path.GetDirectoryName(Path.GetFullPath(running)); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { runningFolder = null; }
        if (!current.Resources.Paths.IsModulePath(savedPath, runningFolder)) return null;
        static string Norm(string p) => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        try { return string.Equals(Norm(savedPath), Norm(running), StringComparison.OrdinalIgnoreCase) ? null : savedPath; }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return null; }
    }

    /// <summary>Largest decompressed engine-state snapshot accepted.</summary>
    public const long MaxSnapshotBytes = 1L << 30;

    /// <summary>
    /// Deepest JSON nesting accepted in a snapshot. The parser enforces it without recursion, and it keeps the
    /// recursive snapshot reader well inside the thread's stack.
    /// </summary>
    public const int MaxSnapshotDepth = 256;

    /// <summary>
    /// The engine-state snapshot stored in a save's GFF. A damaged or oversized snapshot throws
    /// <see cref="InvalidDataException"/>.
    /// </summary>
    public static JsonObject ReadSnapshot(GffStruct g, long maxBytes = MaxSnapshotBytes)
    {
        if (g.GetString("Encoding") != "json+gzip") throw new InvalidDataException($"unknown save encoding '{g.GetString("Encoding")}'");
        var json = new MemoryStream();
        try
        {
            using var gz = new GZipStream(new MemoryStream(g.GetBytes("Snapshot")), CompressionMode.Decompress);
            var chunk = new byte[81920];
            for (int n; (n = gz.Read(chunk, 0, chunk.Length)) > 0;)
            {
                if (json.Length + n > maxBytes) throw new InvalidDataException($"save snapshot is larger than {maxBytes} bytes");
                json.Write(chunk, 0, n);
            }
            json.Position = 0;
            return JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = MaxSnapshotDepth }) as JsonObject
                ?? throw new InvalidDataException("save snapshot is not a JSON object");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("save snapshot is damaged: " + e.Message, e);
        }
    }

    /// <summary>
    /// A world for loading into: the module loaded from the same resources without running its
    /// load or spawn scripts (scripts are enabled afterwards), with the host's settings copied.
    /// </summary>
    public static World FreshWorld(World current, string? modulePath = null)
    {
        var rm = current.Resources;
        if (modulePath != null) rm = ResMan.ForModule(rm.Paths, modulePath);
        // TEMP: templates belong to the world being replaced; the save brings its own.
        foreach (var temp in rm.Sources.OfType<MemorySource>().Where(s => s.Description == "TEMP:").ToList()) rm.Remove(temp);
        var world = new World(rm) { GameDifficulty = current.GameDifficulty };
        var oldPersistence = current.System<PersistenceSystem>();
        var persistence = world.System<PersistenceSystem>();
        persistence.RootOverride = oldPersistence.RootOverride;
        persistence.Log = oldPersistence.Log;
        world.LoadModule();
        world.EnableScripts(current.Scripts?.Definitions);
        if (current.Scripts is { } oldHost && world.Scripts is { } host)
        {
            host.Log = oldHost.Log;
            host.VM.JitThreshold = oldHost.VM.JitThreshold;
            host.VM.InstructionLimit = oldHost.VM.InstructionLimit;
        }
        return world;
    }
}
