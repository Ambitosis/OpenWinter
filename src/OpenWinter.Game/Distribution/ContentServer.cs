using System.Security.Cryptography;
using OpenWinter.Net;

namespace OpenWinter.Game.Distribution;

/// <summary>
/// Sends players the module's haks and custom TLK they don't have (or whose copy differs).
/// The catalogue (size + SHA-1 per file) goes out in the Welcome message; clients ask for files by
/// hash and receive them in chunks, a few per tick per player so gameplay traffic still flows.
/// </summary>
public sealed class ContentServer
{
    public const int ChunkSize = 48 * 1024;
    /// <summary>Chunks sent per player per server tick (30 Hz): about 2.8 MB/s each.</summary>
    public int ChunksPerTick = 2;

    readonly Dictionary<string, (ContentFile File, string Path)> _byHash = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<PlayerSession, Queue<Transfer>> _transfers = new();

    sealed class Transfer
    {
        public required string Sha1;
        public required string Path;
        public long Offset, Total;
    }

    public IReadOnlyList<ContentFile> Files { get; }

    /// <summary>Hashes the module's haks and custom TLK (call once at server start; big haks take a moment).</summary>
    public ContentServer(World world)
    {
        var paths = world.Resources.Paths;
        var files = new List<ContentFile>();
        foreach (var hak in Aurora.Resources.ResMan.ModuleHaks(world.Module.Ifo))
            Add(files, ContentKind.Hak, hak, paths.Hak(hak));
        var tlk = world.Module.Ifo.GetString("Mod_CustomTlk");
        if (tlk.Length > 0 && paths.Tlk(tlk) is { } tlkPath) Add(files, ContentKind.Tlk, tlk, tlkPath);
        try
        {
            if (ModuleClientResources.Package(world.Resources, Path.Combine(world.System<Persistence.PersistenceSystem>().UserRoot, "cache", "content")) is { } package)
                Add(files, ContentKind.ModuleResources, "module_resources", package);
        }
        catch (Exception e)
        {
            // A packaging failure (disk full, unreadable resource, locked cache) must not stop the server;
            // players then get the haks/TLK but not the module's own presentation resources.
            Console.WriteLine($"[server] module presentation package not served: {e.Message}");
        }
        Files = files;
    }

    /// <summary>A catalogue of explicit files (tests, tools).</summary>
    public ContentServer(IEnumerable<(ContentKind Kind, string Name, string Path)> files)
    {
        var list = new List<ContentFile>();
        foreach (var (kind, name, path) in files) Add(list, kind, name, path);
        Files = list;
    }

    void Add(List<ContentFile> files, ContentKind kind, string name, string path)
    {
        if (!File.Exists(path)) return;
        using var stream = File.OpenRead(path);
        var file = new ContentFile { Kind = kind, Name = name, Size = stream.Length, Sha1 = Convert.ToHexString(SHA1.HashData(stream)).ToLowerInvariant() };
        files.Add(file);
        _byHash[file.Sha1] = (file, path);
    }

    public void Request(PlayerSession s, string sha1)
    {
        if (!_byHash.TryGetValue(sha1, out var entry)) return; // only files in the catalogue
        if (!_transfers.TryGetValue(s, out var queue)) _transfers[s] = queue = new Queue<Transfer>();
        if (queue.Any(t => t.Sha1 == entry.File.Sha1)) return;
        queue.Enqueue(new Transfer { Sha1 = entry.File.Sha1, Path = entry.Path, Total = entry.File.Size });
    }

    public void Forget(PlayerSession s) => _transfers.Remove(s);

    /// <summary>Sends the next chunks of each player's current download.</summary>
    public void Pump()
    {
        if (_transfers.Count == 0) return;
        var buffer = new byte[ChunkSize];
        foreach (var (session, queue) in _transfers.ToList())
        {
            if (!session.Connection.IsConnected) { _transfers.Remove(session); continue; }
            for (int n = 0; n < ChunksPerTick && queue.Count > 0; n++)
            {
                var t = queue.Peek();
                using (var stream = File.OpenRead(t.Path))
                {
                    stream.Position = t.Offset;
                    int read = stream.Read(buffer, 0, (int)Math.Min(ChunkSize, t.Total - t.Offset));
                    session.Send(new ContentChunkMessage { Sha1 = t.Sha1, Offset = t.Offset, Total = t.Total, Data = buffer.AsSpan(0, read).ToArray() }.Write());
                    t.Offset += read;
                    if (read == 0) t.Offset = t.Total; // file shrank; stop
                }
                if (t.Offset >= t.Total) queue.Dequeue();
            }
            if (queue.Count == 0) _transfers.Remove(session);
        }
    }
}
