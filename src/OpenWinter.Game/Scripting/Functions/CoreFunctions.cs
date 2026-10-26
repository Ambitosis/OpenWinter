using System.Globalization;
using System.Numerics;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Math, strings, conversions and logging.</summary>
public static class CoreFunctions
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>A 15-bit random number reduced modulo n, as in the original: never above 32767, and negative for negative n.</summary>
    [NwFunc("Random")] static object? Random(Call c) => Random15Mod(c.I(0));
    public static int Random15Mod(int n)
    {
        long r = System.Random.Shared.Next(32768);
        return n == 0 ? 0 : (int)(n > 0 ? r % n : -(r % -(long)n));
    }
    [NwFunc("PrintString")] static object? PrintString(Call c) { c.Host.Log($"[script] {c.S(0)}"); return null; }
    [NwFunc("PrintInteger")] static object? PrintInteger(Call c) { c.Host.Log($"[script] {c.I(0)}"); return null; }
    [NwFunc("PrintFloat")] static object? PrintFloat(Call c) { c.Host.Log($"[script] {FloatToString(c.F(0), c.I(1, 18), c.I(2, 9))}"); return null; }
    [NwFunc("PrintObject")] static object? PrintObject(Call c) { c.Host.Log($"[script] {c.O(0):x}"); return null; }
    [NwFunc("PrintVector")] static object? PrintVector(Call c) { var v = c.V(0); c.Host.Log($"[script] {(c.I(1) != 0 ? "PRINTVECTOR:" : "")}({v.X.ToString(Inv)}, {v.Y.ToString(Inv)}, {v.Z.ToString(Inv)})"); return null; }
    [NwFunc("WriteTimestampedLogEntry")] static object? Log(Call c) { c.Host.Log($"[log {DateTime.Now:HH:mm:ss}] {c.S(0)}"); return null; }

    [NwFunc("IntToString")] static object? IntToString(Call c) => c.I(0).ToString(Inv);
    [NwFunc("FloatToString")] static object? FloatToStringF(Call c) => FloatToString(c.F(0), c.I(1, 18), c.I(2, 9));

    /// <summary>
    /// The original's formatting: width is capped at 18 and decimals at 0..9, the digits are the float's exact
    /// value rounded half away from zero, and infinities and NaN print as inf / nan.
    /// </summary>
    public static string FloatToString(float f, int width, int decimals)
    {
        decimals = Math.Clamp(decimals, 0, 9);
        width = Math.Min(width, 18);
        int bits = BitConverter.SingleToInt32Bits(f);
        // The C runtime's names; 0xFFC00000 is the NaN invalid operations produce.
        string s = float.IsNaN(f) ? (bits == unchecked((int)0xFFC00000) ? "-nan(ind)" : bits < 0 ? "-nan" : "nan")
            : float.IsInfinity(f) ? (f < 0 ? "-inf" : "inf")
            : FormatFixed(f, decimals);
        return width > s.Length ? s.PadLeft(width) : s;
    }

    static readonly ulong[] Pow10 = { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000 };

    /// <summary>Exact fixed-point digits of a finite float, rounded half away from zero.</summary>
    static string FormatFixed(float f, int decimals)
    {
        int bits = BitConverter.SingleToInt32Bits(f);
        int exp = (bits >> 23) & 0xFF;
        ulong mant = (ulong)(bits & 0x7FFFFF);
        if (exp != 0) mant |= 1UL << 23; else exp = 1;
        int e = exp - 150; // value = mant * 2^e
        string digits;
        if (e >= 0)
            digits = ((UInt128)mant << e).ToString(Inv) + new string('0', decimals);
        else
        {
            // mant * 10^decimals < 2^54, so for shifts of 64 or more the value rounds to zero.
            int k = -e;
            ulong n = mant * Pow10[decimals], q = 0;
            if (k < 64)
            {
                q = n >> k;
                if ((n & ((1UL << k) - 1)) >= 1UL << (k - 1)) q++;
            }
            digits = q.ToString(Inv).PadLeft(decimals + 1, '0');
        }
        if (decimals > 0) digits = digits[..^decimals] + "." + digits[^decimals..];
        return bits < 0 ? "-" + digits : digits;
    }

    [NwFunc("StringToInt")]
    static object? StringToInt(Call c) =>
        Nwnx.Plugins.NwnxTweaks.Of(c.World).StringToIntBaseToAuto ? Nwnx.Plugins.NwnxTweaks.StringToIntAuto(c.S(0)) : ParseLeadingInt(c.S(0));
    [NwFunc("StringToFloat")] static object? StringToFloat(Call c) => ParseLeadingFloat(c.S(0));

    /// <summary>C isspace in the "C" locale.</summary>
    static bool IsCSpace(char ch) => ch is ' ' or '\t' or '\n' or '\v' or '\f' or '\r';

    /// <summary>Like C strtol: leading whitespace, optional sign, digits; anything else stops. Saturates on overflow.</summary>
    public static int ParseLeadingInt(string s)
    {
        int i = 0;
        while (i < s.Length && IsCSpace(s[i])) i++;
        bool neg = false;
        if (i < s.Length && (s[i] == '-' || s[i] == '+')) neg = s[i++] == '-';
        long v = 0;
        for (; i < s.Length && s[i] is >= '0' and <= '9'; i++)
            v = Math.Min(v * 10 + (s[i] - '0'), 1L << 32);
        return (int)Math.Clamp(neg ? -v : v, int.MinValue, int.MaxValue);
    }

    /// <summary>Like C strtod (then narrowed to float): decimal and hex forms, inf, infinity and nan.</summary>
    public static float ParseLeadingFloat(string s)
    {
        int i = 0;
        while (i < s.Length && IsCSpace(s[i])) i++;
        bool neg = false;
        if (i < s.Length && (s[i] == '-' || s[i] == '+')) neg = s[i++] == '-';
        var rest = s.AsSpan(i);
        double v;
        if (rest.StartsWith("inf", StringComparison.OrdinalIgnoreCase)) v = double.PositiveInfinity;
        else if (rest.StartsWith("nan", StringComparison.OrdinalIgnoreCase)) return neg ? -ParsedNaN : ParsedNaN;
        else if (rest.Length > 2 && rest[0] == '0' && (rest[1] | 0x20) == 'x' && (IsHex(rest[2]) || rest[2] == '.' && rest.Length > 3 && IsHex(rest[3])))
            v = ParseHexFloat(rest[2..]);
        else
        {
            int j = 0, digits = 0;
            while (j < rest.Length && char.IsAsciiDigit(rest[j])) { j++; digits++; }
            if (j < rest.Length && rest[j] == '.') for (j++; j < rest.Length && char.IsAsciiDigit(rest[j]); j++) digits++;
            if (digits == 0) return 0f;
            if (j < rest.Length && (rest[j] | 0x20) == 'e')
            {
                int k = j + 1;
                if (k < rest.Length && rest[k] is '+' or '-') k++;
                if (k < rest.Length && char.IsAsciiDigit(rest[k]))
                {
                    while (k < rest.Length && char.IsAsciiDigit(rest[k])) k++;
                    j = k;
                }
            }
            v = double.Parse(rest[..j], NumberStyles.Float, Inv);
        }
        return (float)(neg ? -v : v);
    }

    /// <summary>The NaN StringToFloat gives: positive, and printed as "-nan" (not "-nan(ind)") once negated.</summary>
    static readonly float ParsedNaN = BitConverter.Int32BitsToSingle(0x7FFFFFFF);

    static bool IsHex(char ch) => char.IsAsciiHexDigit(ch);

    static double ParseHexFloat(ReadOnlySpan<char> s)
    {
        double v = 0;
        int j = 0, scale = 0;
        for (; j < s.Length && IsHex(s[j]); j++) v = v * 16 + Convert.ToInt32(s[j].ToString(), 16);
        if (j < s.Length && s[j] == '.')
            for (j++; j < s.Length && IsHex(s[j]); j++) { v = v * 16 + Convert.ToInt32(s[j].ToString(), 16); scale -= 4; }
        if (j + 1 < s.Length && (s[j] | 0x20) == 'p')
        {
            int k = j + 1;
            bool eneg = false;
            if (s[k] is '+' or '-') eneg = s[k++] == '-';
            int p = 0;
            bool any = false;
            for (; k < s.Length && char.IsAsciiDigit(s[k]); k++) { p = Math.Min(p * 10 + (s[k] - '0'), 100000); any = true; }
            if (any) scale += eneg ? -p : p;
        }
        return Math.ScaleB(v, scale);
    }

    [NwFunc("IntToFloat")] static object? IntToFloat(Call c) => (float)c.I(0);
    /// <summary>Truncates; NaN and out-of-range values give int.MinValue (the x86 "integer indefinite").</summary>
    [NwFunc("FloatToInt")] static object? FloatToInt(Call c) => FloatToIntX86(c.F(0));
    public static int FloatToIntX86(float f) => f is >= -2147483648f and < 2147483648f ? (int)f : int.MinValue;
    [NwFunc("IntToHexString")] static object? IntToHex(Call c) => "0x" + c.I(0).ToString("x8", Inv);
    [NwFunc("HexStringToInt")]
    static object? HexToInt(Call c)
    {
        var s = c.S(0).Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return int.TryParse(s, NumberStyles.HexNumber, Inv, out var v) ? v : 0;
    }
    [NwFunc("ObjectToString")] static object? ObjectToString(Call c) => c.O(0).ToString("x", Inv);
    [NwFunc("StringToObject")]
    static object? StringToObject(Call c) =>
        uint.TryParse(c.S(0), NumberStyles.HexNumber, Inv, out var id) && c.World.Get(id) != null ? id : GameObject.InvalidId;

    [NwFunc("GetStringLength")] static object? Len(Call c) => c.S(0).Length;
    // Only ASCII letters change case, as in the original.
    [NwFunc("GetStringUpperCase")] static object? Upper(Call c) => string.Create(c.S(0).Length, c.S(0), (d, s) => { for (int i = 0; i < s.Length; i++) d[i] = s[i] is >= 'a' and <= 'z' ? (char)(s[i] - 32) : s[i]; });
    [NwFunc("GetStringLowerCase")] static object? Lower(Call c) => string.Create(c.S(0).Length, c.S(0), (d, s) => { for (int i = 0; i < s.Length; i++) d[i] = s[i] is >= 'A' and <= 'Z' ? (char)(s[i] + 32) : s[i]; });
    [NwFunc("GetStringRight")] static object? Right(Call c) { var s = c.S(0); int n = Math.Clamp(c.I(1), 0, s.Length); return s[(s.Length - n)..]; }
    [NwFunc("GetStringLeft")] static object? Left(Call c) { var s = c.S(0); return s[..Math.Clamp(c.I(1), 0, s.Length)]; }
    [NwFunc("InsertString")] static object? Insert(Call c) { var s = c.S(0); int at = c.I(2); return at < 0 || at > s.Length ? s : s.Insert(at, c.S(1)); }
    [NwFunc("GetSubString")]
    static object? Sub(Call c)
    {
        var s = c.S(0);
        int start = c.I(1), count = c.I(2);
        // A negative count takes the rest of the string.
        if (start < 0 || start >= s.Length || count == 0) return "";
        return count < 0 ? s[start..] : s.Substring(start, Math.Min(count, s.Length - start));
    }
    [NwFunc("FindSubString")]
    static object? Find(Call c)
    {
        var s = c.S(0);
        int start = c.I(2);
        // An empty needle is never found.
        if (start < 0 || start > s.Length || c.S(1).Length == 0) return -1;
        return s.IndexOf(c.S(1), start, StringComparison.Ordinal);
    }
    /// <summary>A custom-table strref with no text reads "BadStrRef(n)", as in the original.</summary>
    [NwFunc("GetStringByStrRef")]
    static object? StrRef(Call c)
    {
        var s = c.World.TlkStringOverridden(c.I(0));
        return s.Length == 0 && c.I(0) > 0 && (c.I(0) & 0x01000000) != 0 ? $"BadStrRef({c.I(0)})" : s;
    }
    [NwFunc("RegExpMatch")] static object? RegexMatch(Call c) => Strings.RegexMatch(c);
    [NwFunc("RegExpIterate")] static object? RegexIterate(Call c) => Strings.RegexIterate(c);
    [NwFunc("RegExpReplace")] static object? RegexReplace(Call c) => Strings.RegexReplace(c);
    [NwFunc("GetRandomUUID")] static object? Uuid(Call c) => Guid.NewGuid().ToString();
    [NwFunc("GetTickCount")] static object? Tick(Call c) => Environment.TickCount;
    [NwFunc("GetMicrosecondCounter")] static object? Micros(Call c) => (int)(System.Diagnostics.Stopwatch.GetTimestamp() * 1_000_000 / System.Diagnostics.Stopwatch.Frequency);
    [NwFunc("GetUnixTime")] static object? Unix(Call c) => (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    [NwFunc("GetStringColorRGB")] static object? Rgb(Call c) => $"<c{(char)Math.Clamp(c.I(0), 1, 255)}{(char)Math.Clamp(c.I(1), 1, 255)}{(char)Math.Clamp(c.I(2), 1, 255)}>";
    [NwFunc("GetSmallLetterIcon")] static object? Icon(Call c) => "";

    [NwFunc("abs")] static object? Abs(Call c) => c.I(0) < 0 ? unchecked(-c.I(0)) : c.I(0);
    [NwFunc("fabs")] static object? FAbs(Call c) => MathF.Abs(c.F(0));
    // Out-of-domain arguments (including NaN) give 0, as in the original.
    [NwFunc("sqrt")] static object? Sqrt(Call c) => c.F(0) >= 0 ? MathF.Sqrt(c.F(0)) : 0f;
    [NwFunc("pow")] static object? Pow(Call c) => c.F(0) == 0 && c.F(1) < 0 ? 0f : MathF.Pow(c.F(0), c.F(1));
    [NwFunc("log")] static object? Log10(Call c) => c.F(0) > 0 ? MathF.Log(c.F(0)) : 0f;
    [NwFunc("cos")] static object? Cos(Call c) => (float)Math.Cos(Radians(c.F(0)));
    [NwFunc("sin")] static object? Sin(Call c) => (float)Math.Sin(Radians(c.F(0)));
    [NwFunc("tan")] static object? Tan(Call c) => (float)Math.Tan(Radians(c.F(0)));
    [NwFunc("acos")] static object? Acos(Call c) => c.F(0) is >= -1 and <= 1 ? MathF.Acos(c.F(0)) * 180f / MathF.PI : 0f;
    [NwFunc("asin")] static object? Asin(Call c) => c.F(0) is >= -1 and <= 1 ? MathF.Asin(c.F(0)) * 180f / MathF.PI : 0f;
    [NwFunc("atan")] static object? Atan(Call c) => MathF.Atan(c.F(0)) * 180f / MathF.PI;

    /// <summary>Degrees to radians the way the original does it: a float factor, multiplied in double precision.</summary>
    public static double Radians(float degrees) => degrees * (double)(float)(Math.PI / 180);

    static int Dice(int sides, int n)
    {
        int total = 0;
        for (int i = 0; i < Math.Max(1, n); i++) total += System.Random.Shared.Next(sides) + 1;
        return total;
    }
    [NwFunc("d2")] static object? D2(Call c) => Dice(2, c.I(0, 1));
    [NwFunc("d3")] static object? D3(Call c) => Dice(3, c.I(0, 1));
    [NwFunc("d4")] static object? D4(Call c) => Dice(4, c.I(0, 1));
    [NwFunc("d6")] static object? D6(Call c) => Dice(6, c.I(0, 1));
    [NwFunc("d8")] static object? D8(Call c) => Dice(8, c.I(0, 1));
    [NwFunc("d10")] static object? D10(Call c) => Dice(10, c.I(0, 1));
    [NwFunc("d12")] static object? D12(Call c) => Dice(12, c.I(0, 1));
    [NwFunc("d20")] static object? D20(Call c) => Dice(20, c.I(0, 1));
    [NwFunc("d100")] static object? D100(Call c) => Dice(100, c.I(0, 1));

    [NwFunc("Vector")] static object? Vector(Call c) => new Vector3(c.F(0), c.F(1), c.F(2));
    [NwFunc("VectorMagnitude")] static object? Magnitude(Call c) => MathF.Sqrt(LengthSquaredFtz(c.V(0)));
    /// <summary>
    /// Float maths with denormals flushed to zero, as the original runs: a vector too short to square
    /// normalises to inf/nan components; the zero vector stays zero.
    /// </summary>
    [NwFunc("VectorNormalize")] static object? Normalize(Call c) => NormalizeVector(c.V(0));
    public static Vector3 NormalizeVector(Vector3 v) => v == Vector3.Zero ? v : v * (1f / MathF.Sqrt(LengthSquaredFtz(v)));
    static float LengthSquaredFtz(Vector3 v)
    {
        float ls = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        return ls < 1.17549435E-38f ? 0f : ls;
    }
    /// <summary>0..360, but only a negative y adds 360 (so (-1, -0.0) gives -180).</summary>
    [NwFunc("VectorToAngle")]
    static object? ToAngle(Call c)
    {
        var v = c.V(0);
        float a = MathF.Atan2(v.Y, v.X) * 180f / MathF.PI;
        return v.Y < 0 ? a + 360f : a;
    }
    [NwFunc("AngleToVector")] static object? AngleToVector(Call c) { float a = c.F(0) * MathF.PI / 180f; return new Vector3(MathF.Cos(a), MathF.Sin(a), 0); }
    /// <summary>
    /// A location stores its facing as a direction, so the angle comes back in [0, 360]: -90 gives 270,
    /// 450 gives 90, and 720 just under 360.
    /// </summary>
    public static float NormalizeFacing(float degrees)
    {
        double r = Radians(degrees);
        float x = (float)Math.Cos(r), y = (float)Math.Sin(r);
        double a = Math.Atan2(y, x) * (180 / Math.PI);
        return (float)(y < 0 ? a + 360 : a);
    }
    [NwFunc("feet")] static object? Feet(Call c) => c.F(0) * 0.3048f;
    [NwFunc("yards")] static object? Yards(Call c) => c.F(0) * 0.9144f;
    [NwFunc("RoundsToSeconds")] static object? Rounds(Call c) => c.I(0) * 6f;
    [NwFunc("TurnsToSeconds")] static object? Turns(Call c) => c.I(0) * 60f;
    [NwFunc("HoursToSeconds")] static object? Hours(Call c) => c.I(0) * c.World.Clock.MinutesPerHour * 60f;
}

/// <summary>EE regular expressions (see <see cref="StdRegex"/>): results are json arrays of strings; errors give null with the error.</summary>
static class Strings
{
    static System.Text.Json.Nodes.JsonArray Array(List<string> items) =>
        new(items.Select(x => (System.Text.Json.Nodes.JsonNode?)System.Text.Json.Nodes.JsonValue.Create(JsonCore.FromScript(x))).ToArray());

    public static object? RegexMatch(Call c)
    {
        try { return new JsonValue(Array(StdRegex.Match(c.S(0), c.S(1), c.I(2), c.I(3)))); }
        catch (StdRegex.RegexError e) { return JsonFunctions.Error(e.Message); }
    }

    public static object? RegexIterate(Call c)
    {
        try { return new JsonValue(new System.Text.Json.Nodes.JsonArray(StdRegex.Iterate(c.S(0), c.S(1), c.I(2), c.I(3)).Select(m => (System.Text.Json.Nodes.JsonNode?)Array(m)).ToArray())); }
        catch (StdRegex.RegexError e) { return JsonFunctions.Error(e.Message); }
    }

    /// <summary>"" on error.</summary>
    public static object? RegexReplace(Call c)
    {
        try { return StdRegex.Replace(c.S(0), c.S(1), c.S(2), c.I(3), c.I(4)); }
        catch (StdRegex.RegexError) { return ""; }
    }
}
