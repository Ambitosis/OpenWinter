using System.Buffers.Binary;
using System.Text;

namespace OpenWinter.Aurora.Formats;

/// <summary>
/// LTR V1.0 letter-combination tables used by RandomName: a third-order Markov
/// model of names. After the 8-byte signature comes one byte with the alphabet
/// size (26 = a-z, 28 = a-z plus apostrophe and hyphen), then probability blocks
/// of three float arrays each (start, middle, end), one float per letter:
/// one block for single letters, one per letter for pairs, one per letter pair
/// for triples. Arrays are cumulative: the chosen letter is the first whose
/// value exceeds a uniform random number.
/// </summary>
public sealed class Ltr
{
    const string Alphabet28 = "abcdefghijklmnopqrstuvwxyz'-";

    public readonly int LetterCount;
    public string Letters => Alphabet28[..LetterCount];

    /// <summary>One start/middle/end set.</summary>
    public sealed class Block
    {
        public readonly float[] Start, Middle, End;
        public Block(int n) { Start = new float[n]; Middle = new float[n]; End = new float[n]; }
    }

    public readonly Block Singles;
    /// <summary>Indexed by the previous letter.</summary>
    public readonly Block[] Doubles;
    /// <summary>Indexed by [second-to-last letter, last letter].</summary>
    public readonly Block[,] Triples;

    Ltr(int n)
    {
        LetterCount = n;
        Singles = new Block(n);
        Doubles = new Block[n];
        Triples = new Block[n, n];
    }

    public static Ltr Parse(byte[] data)
    {
        if (data.Length < 9 || Encoding.ASCII.GetString(data, 0, 8) != "LTR V1.0")
            throw new InvalidDataException("not an LTR V1.0 file");
        int n = data[8];
        if (n is not (26 or 28)) throw new InvalidDataException($"LTR: unsupported letter count {n}");
        var ltr = new Ltr(n);
        int pos = 9;
        void Read(Block b)
        {
            foreach (var arr in new[] { b.Start, b.Middle, b.End })
                for (int i = 0; i < n; i++, pos += 4)
                    arr[i] = pos + 4 <= data.Length ? BinaryPrimitives.ReadSingleLittleEndian(data.AsSpan(pos)) : 0f;
        }
        Read(ltr.Singles);
        for (int a = 0; a < n; a++) Read(ltr.Doubles[a] = new Block(n));
        for (int a = 0; a < n; a++)
            for (int b = 0; b < n; b++) Read(ltr.Triples[a, b] = new Block(n));
        return ltr;
    }

    /// <summary>First letter index whose cumulative probability exceeds r, or -1.</summary>
    static int Pick(float[] cumulative, double r)
    {
        for (int i = 0; i < cumulative.Length; i++)
            if (r < cumulative[i]) return i;
        return -1;
    }

    /// <summary>
    /// Generates a name: three opening letters from the start tables, then
    /// letters from the middle tables until the end table for the last two
    /// letters says the name stops. Returns it capitalised, or "" if the
    /// tables are degenerate.
    /// </summary>
    public string Generate(Random rng, int minLength = 3, int maxLength = 12)
    {
        var sb = new StringBuilder();
        for (int attempt = 0; attempt < 200; attempt++)
        {
            sb.Clear();
            int a = Pick(Singles.Start, rng.NextDouble());
            if (a < 0) continue;
            int b = Pick(Doubles[a].Start, rng.NextDouble());
            if (b < 0) continue;
            int c = Pick(Triples[a, b].Start, rng.NextDouble());
            if (c < 0) continue;
            sb.Append(Letters[a]).Append(Letters[b]).Append(Letters[c]);
            bool done = false;
            while (sb.Length < maxLength)
            {
                var t = Triples[Letters.IndexOf(sb[^2]), Letters.IndexOf(sb[^1])];
                if (sb.Length + 1 >= minLength)
                {
                    int end = Pick(t.End, rng.NextDouble());
                    if (end >= 0) { sb.Append(Letters[end]); done = true; break; }
                }
                int next = Pick(t.Middle, rng.NextDouble());
                if (next < 0) break;
                sb.Append(Letters[next]);
            }
            if (!done || !char.IsLetter(sb[0]) || !char.IsLetter(sb[^1])) continue;
            return char.ToUpperInvariant(sb[0]) + sb.ToString(1, sb.Length - 1);
        }
        return "";
    }
}
