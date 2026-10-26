using System.Buffers.Binary;
using System.Text;
using Microsoft.Win32.SafeHandles;
using OpenWinter.Aurora.Formats;

namespace OpenWinter.Aurora.Resources;

/// <summary>Something that can provide resources by key. Implementations are thread-safe.</summary>
public interface IResourceSource
{
    string Description { get; }
    bool Contains(ResKey key);
    byte[]? Read(ResKey key);
    IEnumerable<ResKey> Keys { get; }
}

/// <summary>A loose folder (module directory, override, development).</summary>
public sealed class DirectorySource : IResourceSource
{
    readonly Dictionary<ResKey, string> _files = new();
    public string Description { get; }

    public DirectorySource(string path)
    {
        Description = path;
        Refresh();
    }

    /// <summary>Indexes the folder again (files added or removed since).</summary>
    public void Refresh()
    {
        _files.Clear();
        if (!Directory.Exists(Description)) return;
        foreach (var file in Directory.EnumerateFiles(Description))
            if (ResKey.TryParse(Path.GetFileName(file), out var key))
                _files[key] = file;
    }

    public bool Contains(ResKey key) => _files.ContainsKey(key);
    public byte[]? Read(ResKey key) => _files.TryGetValue(key, out var f) ? File.ReadAllBytes(f) : null;
    public IEnumerable<ResKey> Keys => _files.Keys;
}

/// <summary>
/// ERF-family archive: .erf, .hak, .mod, .nwm (V1.0 16-char resrefs, V1.1 32-char). E1.0, which nwn_erf writes
/// with --data-version E1, adds each resource's SHA-1 to its key and may store resources as compressed buffers
/// ("XRES"), with the stored and the full size in the resource list.
/// </summary>
public sealed class ErfSource : IResourceSource, IDisposable
{
    readonly SafeFileHandle _handle;
    readonly Dictionary<ResKey, (long Offset, int Size, bool Compressed)> _entries = new();
    public string Description { get; }
    public string FileType { get; }
    public uint DescriptionStrRef { get; }

    public ErfSource(string path)
    {
        Description = path;
        _handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            Span<byte> header = stackalloc byte[160];
            if (RandomAccess.Read(_handle, header, 0) != header.Length) throw new InvalidDataException("Truncated ERF header.");
            FileType = Encoding.ASCII.GetString(header[..4]).Trim();
            string version = Encoding.ASCII.GetString(header[4..8]);
            int resrefLen = version switch { "V1.0" or "E1.0" => 16, "V1.1" => 32, _ => throw new InvalidDataException("Unsupported ERF version.") };
            bool e1 = version == "E1.0";
            // E1.0 keys end in the resource's SHA-1; its resource entries add the compression and the full size.
            int keySize = resrefLen + 8 + (e1 ? 20 : 0), entrySize = e1 ? 16 : 8;
            int count = (int)Le32(header, 16);
            long keyOffset = Le32(header, 24);
            long resOffset = Le32(header, 28);
            DescriptionStrRef = Le32(header, 40);

            long length = RandomAccess.GetLength(_handle);
            if (count < 0 || count > int.MaxValue / keySize || keyOffset < 160 || resOffset < 160
                || keyOffset + (long)count * keySize > length || resOffset + (long)count * entrySize > length)
                throw new InvalidDataException("Invalid ERF resource tables.");
            var keys = new byte[count * keySize];
            RandomAccess.Read(_handle, keys, keyOffset);
            var res = new byte[count * entrySize];
            RandomAccess.Read(_handle, res, resOffset);
            for (int i = 0; i < count; i++)
            {
                int k = i * keySize;
                string resref = Bin.FixedString(keys, k, resrefLen);
                int resId = (int)Le32(keys, k + resrefLen);
                ushort type = BinaryPrimitives.ReadUInt16LittleEndian(keys.AsSpan(k + resrefLen + 4));
                if (resId < 0 || resId >= count || resref.Length == 0) continue;
                int r = resId * entrySize;
                long offset = Le32(res, r), size = Le32(res, r + 4);
                bool compressed = e1 && Le32(res, r + 8) != 0;
                // Entries whose data lies outside the file are skipped.
                if (size > int.MaxValue || offset + size > length) continue;
                _entries[ResKey.Of(resref, type)] = (offset, (int)size, compressed);
            }
        }
        catch
        {
            _handle.Dispose();
            throw;
        }
    }

    public bool Contains(ResKey key) => _entries.ContainsKey(key);

    public byte[]? Read(ResKey key)
    {
        if (!_entries.TryGetValue(key, out var e)) return null;
        var data = new byte[e.Size];
        RandomAccess.Read(_handle, data, e.Offset);
        if (!e.Compressed) return data;
        // A damaged resource reads as missing, like one whose data lies outside the file.
        try { return CompressedBuffer.Unwrap("XRES", data); }
        catch (InvalidDataException) { return null; }
    }

    public IEnumerable<ResKey> Keys => _entries.Keys;
    public void Dispose() => _handle.Dispose();

    static uint Le32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
}

/// <summary>A KEY file and the BIF archives it indexes.</summary>
public sealed class KeyBifSource : IResourceSource
{
    readonly string[] _bifPaths;
    readonly Dictionary<ResKey, uint> _entries = new();
    readonly Dictionary<int, Bif> _bifs = new();
    readonly object _lock = new();
    public string Description { get; }

    sealed class Bif
    {
        public required SafeFileHandle Handle;
        public required Dictionary<int, (long Offset, int Size)> Table;
        public required long Length;
    }

    public KeyBifSource(string keyPath, string gameRoot)
    {
        Description = keyPath;
        var buf = File.ReadAllBytes(keyPath);
        if (buf.Length < 24 || Encoding.ASCII.GetString(buf, 0, 4) != "KEY ") throw new InvalidDataException($"Not a KEY file: {keyPath}");
        long bifCount = Le32(buf, 8);
        long keyCount = Le32(buf, 12);
        long fileTable = Le32(buf, 16);
        long keyTable = Le32(buf, 20);
        if (fileTable + bifCount * 12 > buf.Length || keyTable + keyCount * 22 > buf.Length)
            throw new InvalidDataException($"KEY tables out of range: {keyPath}");
        _bifPaths = new string[bifCount];
        for (int i = 0; i < bifCount; i++)
        {
            int e = (int)(fileTable + i * 12);
            int nameOffset = (int)Le32(buf, e + 4);
            int nameSize = BinaryPrimitives.ReadUInt16LittleEndian(buf.AsSpan(e + 8));
            string name = Bin.FixedString(buf, nameOffset, nameSize).Replace('\\', '/');
            // A BIF must lie inside the game folder; any other name reads as a missing archive.
            _bifPaths[i] = GamePaths.Contained(gameRoot, name) ?? "";
        }
        for (int i = 0; i < keyCount; i++)
        {
            int e = (int)(keyTable + i * 22);
            string resref = Bin.FixedString(buf, e, 16);
            ushort type = BinaryPrimitives.ReadUInt16LittleEndian(buf.AsSpan(e + 16));
            _entries[ResKey.Of(resref, type)] = Le32(buf, e + 18);
        }
    }

    public bool Contains(ResKey key) => _entries.ContainsKey(key);

    public byte[]? Read(ResKey key)
    {
        if (!_entries.TryGetValue(key, out var id)) return null;
        var bif = GetBif((int)(id >> 20));
        if (bif == null || !bif.Table.TryGetValue((int)(id & 0xFFFFF), out var e)) return null;
        if (e.Size < 0 || e.Offset + e.Size > bif.Length) return null;
        var data = new byte[e.Size];
        RandomAccess.Read(bif.Handle, data, e.Offset);
        return data;
    }

    public IEnumerable<ResKey> Keys => _entries.Keys;

    Bif? GetBif(int index)
    {
        lock (_lock)
        {
            if (_bifs.TryGetValue(index, out var b)) return b;
            if (index >= _bifPaths.Length || !File.Exists(_bifPaths[index])) return null;
            var handle = File.OpenHandle(_bifPaths[index], FileMode.Open, FileAccess.Read, FileShare.Read);
            Span<byte> header = stackalloc byte[20];
            RandomAccess.Read(handle, header, 0);
            long length = RandomAccess.GetLength(handle);
            int varCount = (int)Le32(header, 8);
            long tableOffset = Le32(header, 16);
            // A table that cannot fit in the file reads as an empty archive.
            if (varCount < 0 || tableOffset + (long)varCount * 16 > length) varCount = 0;
            var table = new byte[varCount * 16];
            RandomAccess.Read(handle, table, tableOffset);
            var dict = new Dictionary<int, (long, int)>(varCount);
            for (int i = 0; i < varCount; i++)
            {
                int e = i * 16;
                dict[(int)(Le32(table, e) & 0xFFFFF)] = (Le32(table, e + 4), (int)Le32(table, e + 8));
            }
            b = new Bif { Handle = handle, Table = dict, Length = length };
            _bifs[index] = b;
            return b;
        }
    }

    static uint Le32(ReadOnlySpan<byte> b, int o) => BinaryPrimitives.ReadUInt32LittleEndian(b[o..]);
}
