using System.Security.Cryptography;

namespace OpenWinter.Net;

/// <summary>
/// Client side of content download: decides which of the server's files the player already has
/// (a matching hak/TLK in their own folders or in the download cache) and assembles the rest from
/// chunks into the cache, verifying each file's SHA-1 before it is used.
/// </summary>
public sealed class ContentSync : IDisposable
{
    readonly IReadOnlyList<string> _hakDirs, _tlkDirs;
    readonly string _cacheDir;
    readonly Dictionary<string, (ContentFile File, FileStream Stream)> _active = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where each needed file lives once present: "hak:name" / "tlk:name" → path.</summary>
    public readonly Dictionary<string, string> Resolved = new(StringComparer.OrdinalIgnoreCase);
    public readonly List<ContentFile> Missing = new();
    public long BytesNeeded => Missing.Sum(f => f.Size);
    public long BytesReceived { get; private set; }
    public bool Done => Missing.Count == 0;
    /// <summary>A file arrived corrupt (hash mismatch); the download should be retried or abandoned.</summary>
    public string? Error { get; private set; }

    public ContentSync(IReadOnlyList<string> hakDirs, IReadOnlyList<string> tlkDirs, string cacheDir)
    {
        _hakDirs = hakDirs;
        _tlkDirs = tlkDirs;
        _cacheDir = cacheDir;
        Directory.CreateDirectory(cacheDir);
    }

    public static string Key(ContentFile f) => f.Kind switch
    {
        ContentKind.Hak => "hak:" + f.Name,
        ContentKind.Tlk => "tlk:" + f.Name,
        ContentKind.ModuleResources => "module:" + f.Name,
        _ => throw new InvalidDataException("Unknown content kind")
    };
    static string Extension(ContentKind kind) => kind switch
    {
        ContentKind.Hak => ".hak",
        ContentKind.Tlk => ".tlk",
        ContentKind.ModuleResources => ".erf",
        _ => throw new InvalidDataException("Unknown content kind")
    };
    string CachePath(ContentFile f) => Path.Combine(_cacheDir, f.Sha1 + Extension(f.Kind));

    /// <summary>Checks each offered file against local copies; what isn't found goes in <see cref="Missing"/>.</summary>
    public void Plan(IEnumerable<ContentFile> files)
    {
        Dispose();
        Missing.Clear();
        Resolved.Clear();
        BytesReceived = 0;
        Error = null;
        foreach (var f in files)
        {
            var dirs = f.Kind == ContentKind.Hak ? _hakDirs : f.Kind == ContentKind.Tlk ? _tlkDirs : Array.Empty<string>();
            var ext = Extension(f.Kind);
            var candidates = dirs.Select(d => Path.Combine(d, f.Name + ext)).Append(CachePath(f));
            var match = candidates.FirstOrDefault(p => Matches(p, f));
            if (match != null) Resolved[Key(f)] = match;
            else Missing.Add(f);
        }
    }

    /// <summary>Abandons incomplete downloads when a new module replaces the catalogue.</summary>
    public void Dispose()
    {
        foreach (var active in _active.Values)
        {
            active.Stream.Dispose();
            File.Delete(CachePath(active.File) + ".part");
        }
        _active.Clear();
    }

    // Hashing big haks on every login is slow; remember hashes by path, size and modification time.
    static readonly Dictionary<(string, long, DateTime), string> HashCache = new();

    static bool Matches(string path, ContentFile f)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != f.Size) return false;
        var key = (info.FullName, info.Length, info.LastWriteTimeUtc);
        if (!HashCache.TryGetValue(key, out var hash))
        {
            using var stream = info.OpenRead();
            HashCache[key] = hash = Convert.ToHexString(SHA1.HashData(stream)).ToLowerInvariant();
        }
        return hash.Equals(f.Sha1, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Stores one chunk. Returns true when that chunk completed a file (verified and moved into the cache).</summary>
    public bool Accept(ContentChunkMessage chunk)
    {
        var file = Missing.FirstOrDefault(f => f.Sha1.Equals(chunk.Sha1, StringComparison.OrdinalIgnoreCase));
        if (file == null) return false;
        if (!_active.TryGetValue(file.Sha1, out var active))
        {
            var part = CachePath(file) + ".part";
            active = (file, new FileStream(part, FileMode.Create, FileAccess.ReadWrite));
            _active[file.Sha1] = active;
        }
        active.Stream.Position = chunk.Offset;
        active.Stream.Write(chunk.Data);
        BytesReceived += chunk.Data.Length;
        if (chunk.Offset + chunk.Data.Length < file.Size) return false;

        active.Stream.Position = 0;
        var hash = Convert.ToHexString(SHA1.HashData(active.Stream)).ToLowerInvariant();
        active.Stream.Dispose();
        _active.Remove(file.Sha1);
        var partPath = CachePath(file) + ".part";
        if (!hash.Equals(file.Sha1, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(partPath);
            Error = $"{file.Name}: download corrupt";
            return false;
        }
        File.Move(partPath, CachePath(file), overwrite: true);
        Resolved[Key(file)] = CachePath(file);
        Missing.Remove(file);
        return true;
    }
}
