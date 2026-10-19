using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI.FrontEnd;

/// <summary>
/// Game data for the screens outside a game (main menu, first loading screen): the base game's resources
/// (no module, no haks), opened in the background on first use; module summaries for the module browser;
/// loading-screen art and hints.
/// </summary>
public static class FrontEndContent
{
    static Task<ClientContent?>? _base;
    static GamePaths? _paths;

    public static GamePaths? Paths => _paths;

    /// <summary>Starts opening the base game's resources (key/bif index, dialog.tlk) on a worker thread.</summary>
    public static void Begin(GamePaths paths)
    {
        if (_base != null) return;
        _paths = paths;
        _base = Task.Run(() =>
        {
            try { return (ClientContent?)new ClientContent(ResMan.ForClient(paths, Array.Empty<string>(), "")); }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                GD.PrintErr("[frontend] base resources: " + e.Message);
                return null;
            }
        });
    }

    /// <summary>The base game's content once it is open (null while loading or when the game data is missing).</summary>
    public static ClientContent? Base => _base is { IsCompletedSuccessfully: true } t ? t.Result : null;

    public static Task<ClientContent?> BaseAsync => _base ?? Task.FromResult<ClientContent?>(null);

    // ------------------------------------------------------------------ loading screens and hints

    /// <summary>
    /// The loading-screen image resref for a loadscreens.2da row; row 0 ("Random") and unknown rows pick one
    /// made for the area's tileset (or any), stable per <paramref name="seed"/>.
    /// </summary>
    public static string LoadScreen(ResMan rm, int row, string tileset, int seed)
    {
        var table = rm.TwoDA("loadscreens");
        if (row > 1 && table.Get(row, "BMPResRef") is { Length: > 0 } fixedArt) return fixedArt;
        var rows = new List<int>();
        for (int i = 2; i < table.RowCount; i++)
            if (table.Get(i, "BMPResRef").Length > 0 && string.Equals(table.Get(i, "TileSet"), tileset, StringComparison.OrdinalIgnoreCase)) rows.Add(i);
        if (rows.Count == 0)
            for (int i = 2; i < table.RowCount; i++)
                if (table.Get(i, "BMPResRef").Length > 0) rows.Add(i);
        return rows.Count == 0 ? "" : table.Get(rows[(int)((uint)seed % (uint)rows.Count)], "BMPResRef");
    }

    /// <summary>A texture by resref from the given content, flipped upright, or null.</summary>
    public static Texture2D? Texture(ClientContent? content, string resref)
    {
        if (content == null || resref.Length == 0) return null;
        if (content.Textures.Get(resref) is not { } e) return null;
        if (!e.FlipV) return e.Texture;
        var image = e.Texture.GetImage();
        if (image == null) return e.Texture;
        if (image.IsCompressed()) image.Decompress();
        image.FlipY();
        return ImageTexture.CreateFromImage(image);
    }

    static readonly Dictionary<string, List<Texture2D>> Shots = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The pictures in a loading-screen texture. The game's own are 512 x 512 with two framed shots,
    /// one above the other and staggered (each about 480 x 250); those come back as two textures, any
    /// other image whole.
    /// </summary>
    public static List<Texture2D> LoadScreenShots(ClientContent? content, string resref)
    {
        if (content == null || resref.Length == 0) return new();
        if (Shots.TryGetValue(resref, out var cached)) return cached;
        var list = new List<Texture2D>();
        if (content.Textures.Get(resref) is { } e && e.Texture.GetImage() is { } image)
        {
            if (image.IsCompressed()) image.Decompress();
            if (e.FlipV) image.FlipY();
            image.ClearMipmaps();
            if (image.GetWidth() == 512 && image.GetHeight() == 512 && Dark(image, 490, 20, 20, 210) && Dark(image, 2, 282, 22, 200))
            {
                list.Add(Shot(image.GetRegion(new Rect2I(3, 3, 477, 248))));
                list.Add(Shot(image.GetRegion(new Rect2I(35, 261, 473, 246))));
            }
            else list.Add(Shot(image));
        }
        Shots[resref] = list;
        return list;
    }

    static Texture2D Shot(Image image)
    {
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }

    static bool Dark(Image image, int x, int y, int w, int h)
    {
        float sum = 0;
        int n = 0;
        for (int yy = y; yy < y + h; yy += 4)
            for (int xx = x; xx < x + w; xx += 3) { var c = image.GetPixel(xx, yy); sum += c.R + c.G + c.B; n++; }
        return n > 0 && sum / (n * 3) < 0.05f;
    }

    /// <summary>A random gameplay hint from loadhints.2da (campaign-only hints only for the official campaign).</summary>
    public static string Hint(ResMan rm, bool campaign, Random? random = null)
    {
        var table = rm.TwoDA("loadhints");
        var rows = new List<int>();
        for (int i = 0; i < table.RowCount; i++)
            if (table.GetInt(i, "HINT", -1) > 0 && (campaign || table.GetInt(i, "CAMPAIGNONLY") == 0)) rows.Add(i);
        if (rows.Count == 0) return "";
        int row = rows[(random ?? Random.Shared).Next(rows.Count)];
        return Kit.NwnText.Strip(rm.TlkString((uint)table.GetInt(row, "HINT"))).Trim();
    }

    // ------------------------------------------------------------------ modules

    public sealed class ModuleInfo
    {
        public required string Path;
        public required string Group;
        public string FileName => System.IO.Path.GetFileNameWithoutExtension(Path.TrimEnd('/', '\\'));
        public string Name = "", Description = "", EntryArea = "", Tileset = "", MinVersion = "";
        public List<string> Haks = new();
        public List<string> MissingHaks = new();
        public int LoadScreen, AreaCount;
        public long SizeBytes;
        public bool Read, Readable;
        public bool Official => Group == "Official campaign";
        public string Title => Name.Length > 0 ? Name : FileName;
    }

    /// <summary>The playable modules: the player's modules folder (folders and .mod files), then the official campaign in story order.</summary>
    public static List<ModuleInfo> FindModules(GamePaths paths)
    {
        var list = new List<ModuleInfo>();
        var userModules = System.IO.Path.Combine(paths.UserRoot, "modules");
        if (Directory.Exists(userModules))
        {
            foreach (var dir in Directory.EnumerateDirectories(userModules).Where(d => File.Exists(System.IO.Path.Combine(d, "module.ifo"))).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                list.Add(new ModuleInfo { Path = dir, Group = "Your modules" });
            foreach (var mod in Directory.EnumerateFiles(userModules, "*.mod").OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
                list.Add(new ModuleInfo { Path = mod, Group = "Your modules" });
        }
        var campaign = System.IO.Path.Combine(paths.GameRoot, "data", "nwm");
        if (Directory.Exists(campaign))
            foreach (var nwm in Directory.EnumerateFiles(campaign, "*.nwm").OrderBy(CampaignOrder))
                list.Add(new ModuleInfo { Path = nwm, Group = "Official campaign" });
        return list;
    }

    /// <summary>Story order for the campaign files (Prelude, chapters, then the expansions).</summary>
    static string CampaignOrder(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        return name.StartsWith("Prelude") ? "0" : name.StartsWith("Chapter") ? "1" + name : name.StartsWith("XP1") ? "2" + name : "3" + name;
    }

    /// <summary>Reads a module's name, description, haks, entry area and its loading screen (safe to call on a worker thread).</summary>
    public static void Describe(ModuleInfo m, GamePaths paths, TalkTable? talk)
    {
        if (m.Read) return;
        m.Read = true;
        try
        {
            bool folder = Directory.Exists(m.Path);
            m.SizeBytes = folder ? new DirectoryInfo(m.Path).EnumerateFiles("*", SearchOption.TopDirectoryOnly).Sum(f => f.Length) : new FileInfo(m.Path).Length;
            var source = folder ? (IResourceSource)new DirectorySource(m.Path) : new ErfSource(m.Path);
            using var dispose = source as IDisposable;
            var ifo = Gff.Read(source.Read(ResKey.Of("module", ResType.Ifo)) ?? []);
            m.Name = Kit.NwnText.Strip(ifo.GetLocString("Mod_Name").Resolve(talk)).Trim();
            m.Description = Kit.NwnText.Strip(ifo.GetLocString("Mod_Description").Resolve(talk)).Trim();
            m.Haks = ResMan.ModuleHaks(ifo).ToList();
            m.MissingHaks = m.Haks.Where(h => !File.Exists(paths.Hak(h))).ToList();
            m.EntryArea = ifo.GetString("Mod_Entry_Area");
            m.AreaCount = ifo.GetList("Mod_Area_list").Count;
            m.MinVersion = ifo.GetString("Mod_MinGameVer");
            if (m.EntryArea.Length > 0 && source.Read(ResKey.Of(m.EntryArea, ResType.Are)) is { } areBytes)
            {
                var are = Gff.Read(areBytes);
                m.LoadScreen = (int)are.GetInt("LoadScreenID");
                m.Tileset = are.GetString("Tileset");
            }
            m.Readable = true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or EndOfStreamException or UnauthorizedAccessException or ArgumentException)
        {
            m.Description = "This module could not be read: " + e.Message;
        }
    }

    public static TalkTable? BaseTalk(GamePaths paths) =>
        File.Exists(paths.BaseTlk) ? new TalkTable(File.ReadAllBytes(paths.BaseTlk), null) : null;

    public static string FormatSize(long bytes) =>
        bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.0} GB" : bytes >= 1L << 20 ? $"{bytes / (double)(1L << 20):0.#} MB" : $"{Math.Max(1, bytes / 1024)} KB";

    public static string FormatPlayTime(double seconds)
    {
        var t = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{Math.Max(1, t.Minutes)}m";
    }

    public static string FormatAgo(DateTime when)
    {
        var d = DateTime.Now - when;
        if (d.TotalMinutes < 1) return "just now";
        if (d.TotalHours < 1) return $"{(int)d.TotalMinutes} min ago";
        if (d.TotalDays < 1) return $"{(int)d.TotalHours} h ago";
        if (d.TotalDays < 2) return "yesterday";
        if (d.TotalDays < 30) return $"{(int)d.TotalDays} days ago";
        return when.ToString("d MMM yyyy");
    }

    /// <summary>Decodes a screen.tga (or any TGA) thumbnail.</summary>
    public static Texture2D? Tga(byte[]? data)
    {
        if (data is not { Length: > 18 }) return null;
        var image = new Image();
        if (image.LoadTgaFromBuffer(data) != Error.Ok) return null;
        image.GenerateMipmaps();
        return ImageTexture.CreateFromImage(image);
    }
}
