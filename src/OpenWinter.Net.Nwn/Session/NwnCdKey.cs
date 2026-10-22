using System.Security.Cryptography;
using System.Text;

namespace OpenWinter.Net.Nwn.Session;

/// <summary>
/// An NWN CD key: eight public characters (sent to the server), twenty private ones and a seven-character CRC, mixed
/// into one 41-character string with dashes. The private part only ever leaves the machine inside an MD5 answer to
/// the server's challenge.
/// </summary>
public sealed class NwnCdKey
{
    const string Alphabet = "UANPXTFWCQ9DVLE3GYJHK74R6M";
    const int PublicSize = 8, PrivateSize = 20, CrcSize = 7, KeyLength = 41, DashEvery = 6;
    static readonly uint[] CrcTable = BuildCrcTable();

    readonly string _private;
    readonly string _crc;

    public string PublicKey { get; }

    NwnCdKey(string publicPart, string privatePart, string crc)
    {
        PublicKey = publicPart;
        _private = privatePart;
        _crc = crc;
    }

    public static NwnCdKey Parse(string key)
    {
        key = key.Trim();
        if (key.Length != KeyLength) throw new FormatException($"a CD key has {KeyLength} characters");
        for (int i = DashEvery - 1; i < KeyLength; i += DashEvery)
            if (key[i] != '-') throw new FormatException($"expected a dash at position {i + 1}");
        var clean = key.Replace("-", "");
        if (clean.Any(c => !Alphabet.Contains(c))) throw new FormatException("the key has characters CD keys don't use");
        var (pub, priv, crc) = Unscramble(clean);
        if (Crc(pub, priv) != crc) throw new FormatException("the key's check characters don't match");
        return new NwnCdKey(pub, priv, crc);
    }

    /// <summary>The key in an NWN cdkey.ini (YourKey under [NWN1]). The file is only read.</summary>
    public static NwnCdKey FromIni(string path)
    {
        foreach (var line in File.ReadLines(path))
        {
            var t = line.Trim();
            if (t.StartsWith("YourKey", StringComparison.OrdinalIgnoreCase) && t.IndexOf('=') is var eq and > 0)
                return Parse(t[(eq + 1)..]);
        }
        throw new FormatException($"no YourKey in {path}");
    }

    /// <summary>A random, well-formed key for local test servers that can't reach the master server.</summary>
    public static NwnCdKey Generate(Random? random = null)
    {
        random ??= Random.Shared;
        string Pick(int n) => new(Enumerable.Range(0, n).Select(_ => Alphabet[random.Next(Alphabet.Length)]).ToArray());
        var pub = Pick(PublicSize);
        var priv = Pick(PrivateSize);
        return new NwnCdKey(pub, priv, Crc(pub, priv));
    }

    /// <summary>The full key, private part included. Never send it anywhere.</summary>
    public string FullKey => Scramble(PublicKey, _private, _crc);

    /// <summary>The answer to the server's CD key challenge: the public key and MD5(full key + challenge) in hex.</summary>
    public byte[] ChallengeResponse(byte[] challenge) =>
        [.. Encoding.ASCII.GetBytes(PublicKey), .. Md5Hex([.. Encoding.ASCII.GetBytes(FullKey), .. challenge])];

    /// <summary>MD5(secret + challenge) in lowercase hex: the answer to a password challenge.</summary>
    public static byte[] Md5Hex(byte[] data) =>
        Encoding.ASCII.GetBytes(Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant());

    public override string ToString() => PublicKey;

    static (string Public, string Private, string Crc) Unscramble(string clean)
    {
        var pub = new StringBuilder();
        var priv = new StringBuilder();
        var crc = new StringBuilder();
        int part = 0;
        const int crcStart = PrivateSize - CrcSize - 1;
        foreach (var c in clean)
        {
            while (true)
            {
                if (part == 0)
                {
                    part = 1;
                    if (priv.Length < PrivateSize)
                    {
                        priv.Append(c);
                        break;
                    }
                }
                if (part == 1)
                {
                    part = 2;
                    if (priv.Length > crcStart && crc.Length < CrcSize)
                    {
                        crc.Append(c);
                        break;
                    }
                }
                part = 0;
                if (pub.Length < PublicSize)
                {
                    pub.Append(c);
                    break;
                }
                if (priv.Length < PrivateSize || crc.Length < CrcSize) continue;
                break;
            }
        }
        return (pub.ToString(), priv.ToString(), crc.ToString());
    }

    static string Scramble(string pub, string priv, string crc)
    {
        var result = new char[KeyLength];
        int dashes = 0, pi = 0, si = 0, ci = 0;
        for (int i = 0; i < KeyLength; i++)
        {
            if ((i + 1) % DashEvery == 0)
            {
                result[i] = '-';
                dashes++;
            }
            else if ((i + dashes) % 2 == 1 && pi < PublicSize) result[i] = pub[pi++];
            else if ((i + dashes) % 2 == 1 && si > PrivateSize - CrcSize - 1 && ci < CrcSize) result[i] = crc[ci++];
            else if (si < PrivateSize) result[i] = priv[si++];
            else throw new InvalidOperationException("CD key layout overflow");
        }
        return new string(result);
    }

    static string Crc(string pub, string priv)
    {
        uint crc = 0;
        foreach (var b in Encoding.ASCII.GetBytes(pub + priv)) crc = ((crc >> 8) & 0x00FFFFFF) ^ CrcTable[(crc ^ b) & 0xFF];
        var chars = new char[CrcSize];
        for (int i = CrcSize - 1; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(crc % 26)];
            crc /= 26;
        }
        return new string(chars);
    }

    static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var v = i;
            for (int k = 0; k < 8; k++) v = (v & 1) != 0 ? (v >> 1) ^ 0xEDB88320 : v >> 1;
            table[i] = v;
        }
        return table;
    }
}
