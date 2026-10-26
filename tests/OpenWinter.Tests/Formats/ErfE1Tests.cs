using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using Xunit;

namespace OpenWinter.Tests.Formats;

/// <summary>E1.0 archives: a SHA-1 in each key, resources optionally stored as "XRES" compressed buffers.</summary>
public class ErfE1Tests
{
    static readonly byte[] Alpha = Encoding.ASCII.GetBytes("hello world hello world hello world");
    static readonly byte[] Bravo = Encoding.ASCII.GetBytes(new string('x', 3000));

    /// <summary>
    /// A hak written by neverwinter.nim's nwn_erf (--data-version E1 --data-compression zstd) holding alpha.txt and
    /// bravo.2da above.
    /// </summary>
    internal static readonly byte[] ZstdHak = System.Convert.FromBase64String(
        "SEFLIEUxLjAAAAAAAAAAAAIAAACgAAAAoAAAAPgAAAB+AAAAKgEAAAAAAAAwMDAwMDAwMDAwMDAwMDAwMDAwMDAwMDAAAAAAAAAA" +
        "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA" +
        "AAAAAAAAAAAAAGFscGhhAAAAAAAAAAAAAAAAAAAACgAAALgVh6ErXOKxmJxsDr6FzVM0c09ZYnJhdm8AAAAAAAAAAAAAAAEAAADh" +
        "BwAA8hLwzra4aITwUVxo01ezomHAA88YAQAAMwAAAAEAAAAjAAAASwEAACsAAAABAAAAuAsAAFhSRVMDAAAAAgAAACMAAAABAAAA" +
        "AAAAACi1L/0gI5UAAGBoZWxsbyB3b3JsZCABAK9LElhSRVMDAAAAAgAAALgLAAABAAAAAAAAACi1L/1guApNAAAQeHgBALPzARY=");

    /// <summary>The same archive stored uncompressed, laid out as nwn_erf lays out --data-compression none.</summary>
    internal static byte[] PlainHak()
    {
        var items = new[] { ("alpha", (ushort)10, Alpha), ("bravo", (ushort)2017, Bravo) };
        int keys = 160, entries = keys + items.Length * 44, data = entries + items.Length * 16;
        var o = new byte[data + items.Sum(i => i.Item3.Length)];
        Encoding.ASCII.GetBytes("HAK E1.0").CopyTo(o, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(16), (uint)items.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(20), (uint)keys);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(24), (uint)keys);
        BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(28), (uint)entries);
        for (int i = 0, at = data; i < items.Length; at += items[i].Item3.Length, i++)
        {
            var (resref, type, bytes) = items[i];
            int k = keys + i * 44, r = entries + i * 16;
            Encoding.ASCII.GetBytes(resref).CopyTo(o, k);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(k + 16), (uint)i);
            BinaryPrimitives.WriteUInt16LittleEndian(o.AsSpan(k + 20), type);
            SHA1.HashData(bytes).CopyTo(o, k + 24);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(r), (uint)at);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(r + 4), (uint)bytes.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(o.AsSpan(r + 12), (uint)bytes.Length);
            bytes.CopyTo(o, at);
        }
        return o;
    }

    public static TheoryData<string> Archives => new() { "zstd", "plain" };

    static byte[] Archive(string name) => name == "zstd" ? ZstdHak : PlainHak();

    [Theory, MemberData(nameof(Archives))]
    public void ErfSourceReadsE1(string name)
    {
        string path = Path.Combine(Directory.CreateTempSubdirectory("ow-erf-e1").FullName, name + ".hak");
        try
        {
            File.WriteAllBytes(path, Archive(name));
            using var source = new ErfSource(path);
            Assert.Equal("HAK", source.FileType);
            Assert.Equal(Alpha, source.Read(ResKey.Of("alpha", 10)));
            Assert.Equal(Bravo, source.Read(ResKey.Of("bravo", 2017)));
            Assert.Equal(2, source.Keys.Count());
        }
        finally
        {
            try { Directory.Delete(Path.GetDirectoryName(path)!, true); } catch (IOException) { }
        }
    }

    [Theory, MemberData(nameof(Archives))]
    public void ErfReadReadsE1(string name)
    {
        var entries = Erf.Read(Archive(name), out var type);
        Assert.Equal("HAK ", type);
        Assert.Equal(Alpha, entries.Single(e => e.ResRef == "alpha").Data);
        Assert.Equal(Bravo, entries.Single(e => e.ResRef == "bravo" && e.ResType == 2017).Data);
    }

    [Fact]
    public void E1KeysCarryTheResourceSha1()
    {
        // The key's last 20 bytes are the SHA-1 of the uncompressed resource.
        Assert.Equal(SHA1.HashData(Alpha), ZstdHak.AsSpan(160 + 24, 20).ToArray());
        Assert.Equal(SHA1.HashData(Bravo), ZstdHak.AsSpan(160 + 44 + 24, 20).ToArray());
    }
}
