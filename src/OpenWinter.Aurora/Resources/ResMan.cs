using System.Collections.Concurrent;
using OpenWinter.Aurora.Formats;

namespace OpenWinter.Aurora.Resources;

/// <summary>Where a game install and a user's content folder live.</summary>
public sealed record GamePaths(string GameRoot, string UserRoot, string Language = "en")
{
    /// <summary>
    /// The hak's file. Names come from modules and servers, so one that is not a plain file name (a path, a drive,
    /// "..") resolves to nothing: the returned path is empty and does not exist.
    /// </summary>
    public string Hak(string name) => !IsPlainName(name) ? "" :
        Find(Path.Combine(UserRoot, "hak"), name + ".hak")
        ?? Find(Path.Combine(GameRoot, "data", "hak"), name + ".hak")
        ?? Path.Combine(GameRoot, "data", "hak", name + ".hak");

    public string? Tlk(string name) => !IsPlainName(name) ? null :
        Find(Path.Combine(UserRoot, "tlk"), name + ".tlk") ?? Find(Path.Combine(GameRoot, "data", "tlk"), name + ".tlk");

    /// <summary>
    /// A file in a folder, matched without regard to case as modules name haks, TLKs and modules by resref
    /// while the files on disk keep whatever case they were made with. Null when missing.
    /// </summary>
    public static string? Find(string folder, string file)
    {
        if (!IsPlainName(file)) return null;
        var exact = Path.Combine(folder, file);
        if (File.Exists(exact)) return exact;
        if (OperatingSystem.IsWindows() || !Directory.Exists(folder)) return null;
        return Directory.EnumerateFiles(folder).FirstOrDefault(f => string.Equals(Path.GetFileName(f), file, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A name that stays inside the folder it is looked up in: not empty, no directory separators, drive or stream
    /// colon, or characters invalid in file names, and not "." or "..".
    /// </summary>
    public static bool IsPlainName(string name) =>
        name.Length > 0 && name != "." && name != ".." && name.IndexOfAny(Unsafe) < 0 && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0;

    static readonly char[] Unsafe = { '/', '\\', ':', '\0' };

    /// <summary>
    /// <paramref name="relative"/> under <paramref name="root"/>, or null when it is rooted or climbs out of it.
    /// </summary>
    public static string? Contained(string root, string relative)
    {
        if (relative.Length == 0 || Path.IsPathRooted(relative) || relative.Contains(':') || relative.Contains('\0')) return null;
        try
        {
            var full = Path.GetFullPath(Path.Combine(root, relative));
            var top = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
            return full.StartsWith(top, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ? full : null;
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    public string BaseTlk => Path.Combine(GameRoot, "lang", Language, "data", "dialog.tlk");

    /// <summary>
    /// Whether a module path (a .mod/.nwm file or a module folder) lies in the user's modules folder, the
    /// install's campaign or module folders, or <paramref name="extraFolder"/>. Paths recorded in saves are
    /// only followed when this holds.
    /// </summary>
    public bool IsModulePath(string path, string? extraFolder = null)
    {
        if (path.Length == 0) return false;
        string full;
        try { full = Path.GetFullPath(path); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return false; }
        var folders = new[] { Path.Combine(UserRoot, "modules"), Path.Combine(GameRoot, "data", "nwm"), Path.Combine(GameRoot, "data", "mod"), extraFolder };
        foreach (var folder in folders)
        {
            if (string.IsNullOrEmpty(folder)) continue;
            var relative = Path.GetRelativePath(folder, full);
            if (relative != "." && Contained(folder, relative) != null) return true;
        }
        return false;
    }
}

/// <summary>
/// Resolves resources across sources searched in priority order (first added
/// wins), with typed caches for frequently used tables.
/// </summary>
public sealed class ResMan : IDisposable
{
    readonly List<IResourceSource> _sources = new();
    readonly ConcurrentDictionary<string, TwoDA?> _twoDAs = new(StringComparer.OrdinalIgnoreCase);

    public GamePaths Paths { get; }
    public IReadOnlyList<IResourceSource> Sources => _sources;
    public TalkTable Talk { get; private set; }
    /// <summary>Server-only module source; client resources are distributed separately.</summary>
    public IResourceSource? ModuleSource { get; private set; }

    public ResMan(GamePaths paths)
    {
        Paths = paths;
        Talk = new TalkTable(File.Exists(paths.BaseTlk) ? File.ReadAllBytes(paths.BaseTlk) : null, null);
    }

    public void Add(IResourceSource source) => _sources.Add(source);

    /// <summary>Adds a source that takes priority over all others (the engine's TEMP: templates).</summary>
    public void AddFirst(IResourceSource source) => _sources.Insert(0, source);

    /// <summary>Removes a source added earlier (a replaced world's TEMP: templates).</summary>
    public bool Remove(IResourceSource source) => _sources.Remove(source);

    /// <summary>Base game sources, lowest priority: user override, EE texture packs, KEY/BIFs.</summary>
    public void AddBaseGame(bool includeUserOverride = true)
    {
        if (includeUserOverride) Add(new DirectorySource(Path.Combine(Paths.UserRoot, "override")));
        // nwn_retail.key (data/ovr.bif) patches the base data: newer classes/spells/skills/ruleset 2DAs, damagetypes.2da, ...
        // The original resolves it ahead of the loose ovr folder.
        var retail = Path.Combine(Paths.GameRoot, "data", "nwn_retail.key");
        if (File.Exists(retail)) Add(new KeyBifSource(retail, Paths.GameRoot));
        Add(new DirectorySource(Path.Combine(Paths.GameRoot, "ovr")));
        // Loose content folders: user music and portraits, then the install's music, ambient and portraits.
        foreach (var dir in new[] { Path.Combine(Paths.UserRoot, "music"), Path.Combine(Paths.UserRoot, "portraits"),
                     Path.Combine(Paths.GameRoot, "data", "mus"), Path.Combine(Paths.GameRoot, "data", "amb"), Path.Combine(Paths.GameRoot, "data", "prt") })
            if (Directory.Exists(dir)) Add(new DirectorySource(dir));
        foreach (var pack in new[] { "Textures_Tpa", "Tiles_Tpa", "xp1_tex_tpa", "xp2_tex_tpa", "GUI_32bit", "XP1_GUI", "xp2_gui" })
        {
            var path = Path.Combine(Paths.GameRoot, "data", "txpk", pack + ".erf");
            if (File.Exists(path)) Add(new ErfSource(path));
        }
        foreach (var key in new[] { Path.Combine("lang", Paths.Language, "data", "nwn_base_loc.key"), Path.Combine("data", "nwn_base.key") })
        {
            var path = Path.Combine(Paths.GameRoot, key);
            if (File.Exists(path)) Add(new KeyBifSource(path, Paths.GameRoot));
        }
    }

    /// <summary>
    /// Full server-side lookup order for a module: development folder, haks
    /// (in module order, first wins), the module itself, then the base game.
    /// </summary>
    public static ResMan ForModule(GamePaths paths, string modulePath)
    {
        var rm = new ResMan(paths);
        IResourceSource? module = null;
        try
        {
            var dev = Path.Combine(paths.UserRoot, "development");
            if (Directory.Exists(dev)) rm.Add(new DirectorySource(dev));
            module = Directory.Exists(modulePath) ? new DirectorySource(modulePath) : new ErfSource(modulePath);
            var ifo = Gff.Read(module.Read(ResKey.Of("module", ResType.Ifo)) ?? throw new FileNotFoundException("module.ifo missing", modulePath));
            foreach (var hak in ModuleHaks(ifo))
            {
                var path = paths.Hak(hak);
                if (File.Exists(path)) rm.Add(new ErfSource(path));
                else rm.MissingHaks.Add(hak);
            }
            rm.Add(module);
            rm.ModuleSource = module;
            rm.AddBaseGame();
            var customTlk = ifo.GetString("Mod_CustomTlk");
            if (!string.IsNullOrEmpty(customTlk) && paths.Tlk(customTlk) is { } tlk)
                rm.Talk = new TalkTable(File.Exists(paths.BaseTlk) ? File.ReadAllBytes(paths.BaseTlk) : null, File.ReadAllBytes(tlk));
            return rm;
        }
        catch
        {
            if (module is IDisposable disposable && !rm.Sources.Contains(module)) disposable.Dispose();
            rm.Dispose();
            throw;
        }
    }

    /// <summary>Client-side lookup order: announced haks, verified presentation packages, then base game. No module.</summary>
    /// <param name="downloaded">Downloaded or verified files by "hak:name" / "tlk:name", used before the usual folders.</param>
    public static ResMan ForClient(GamePaths paths, IEnumerable<string> haks, string customTlk, IReadOnlyDictionary<string, string>? downloaded = null, IEnumerable<string>? modulePackages = null)
    {
        var rm = new ResMan(paths);
        foreach (var hak in haks)
        {
            var path = downloaded != null && downloaded.TryGetValue("hak:" + hak, out var got) ? got : paths.Hak(hak);
            if (File.Exists(path)) rm.Add(new ErfSource(path));
            else rm.MissingHaks.Add(hak);
        }
        foreach (var package in modulePackages ?? Array.Empty<string>())
            if (downloaded != null && downloaded.TryGetValue("module:" + package, out var packagePath))
                rm.Add(new ErfSource(packagePath));
        rm.AddBaseGame();
        var customPath = string.IsNullOrEmpty(customTlk) ? null
            : downloaded != null && downloaded.TryGetValue("tlk:" + customTlk, out var gotTlk) ? gotTlk : paths.Tlk(customTlk);
        if (customPath != null)
            rm.Talk = new TalkTable(File.Exists(paths.BaseTlk) ? File.ReadAllBytes(paths.BaseTlk) : null, File.ReadAllBytes(customPath));
        return rm;
    }

    public static IEnumerable<string> ModuleHaks(GffStruct ifo)
    {
        var list = ifo.GetList("Mod_HakList");
        if (list.Count > 0)
            foreach (var h in list) yield return h.GetString("Mod_Hak");
        else if (ifo.GetString("Mod_Hak") is { Length: > 0 } single)
            yield return single;
    }

    /// <summary>Release this manager's archive handles when its owner unloads it.</summary>
    public void Dispose()
    {
        foreach (var source in _sources.OfType<IDisposable>().Distinct()) source.Dispose();
        _sources.Clear();
        _twoDAs.Clear();
    }

    public readonly List<string> MissingHaks = new();

    public bool Contains(string resref, ushort type) => Contains(ResKey.Of(resref, type));

    public bool Contains(ResKey key)
    {
        foreach (var s in _sources)
            if (s.Contains(key)) return true;
        return false;
    }

    public byte[]? Read(string resref, ushort type) => resref.Length == 0 ? null : Read(ResKey.Of(resref, type));

    public byte[]? Read(ResKey key)
    {
        foreach (var s in _sources)
            if (s.Contains(key)) return s.Read(key);
        return null;
    }

    public GffStruct? ReadGff(string resref, ushort type) => Read(resref, type) is { } b ? Gff.Read(b) : null;

    /// <summary>Cached 2DA lookup. Returns an empty table when missing.</summary>
    public TwoDA TwoDA(string name) => _twoDAs.GetOrAdd(name, n => Read(n, ResType.TwoDA) is { } b ? Formats.TwoDA.Parse(b) : null) ?? Formats.TwoDA.Empty;

    public string TlkString(uint strref) => Talk.Get(strref);

    /// <summary>All resources of a type across sources, highest priority wins per resref.</summary>
    public IEnumerable<ResKey> All(ushort type)
    {
        var seen = new HashSet<ResKey>();
        foreach (var s in _sources)
            foreach (var k in s.Keys)
                if (k.Type == type && seen.Add(k)) yield return k;
    }
}
