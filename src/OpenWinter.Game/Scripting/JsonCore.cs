using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using JValue = System.Text.Json.Nodes.JsonValue;

namespace OpenWinter.Game.Scripting;

/// <summary>Kinds of json value as the original distinguishes them (signed and unsigned integers are separate).</summary>
public enum JsonKind { Null, Bool, Int, UInt, Float, String, Object, Array }

/// <summary>
/// The json model the original's script functions expose, measured on the original server: objects keep
/// their keys sorted, integers and floats stay distinct, dumps are ASCII with \u escapes and C++-style
/// float formatting, and parse errors read like the original's. Values live in System.Text.Json nodes.
/// </summary>
public static class JsonCore
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // ---- kinds and scalar access --------------------------------------------

    public static JsonKind Kind(JsonNode? n)
    {
        switch (n)
        {
            case null: return JsonKind.Null;
            case JsonObject: return JsonKind.Object;
            case JsonArray: return JsonKind.Array;
        }
        var v = n.AsValue().GetValue<object>();
        switch (v)
        {
            case JsonElement e:
                return e.ValueKind switch
                {
                    JsonValueKind.True or JsonValueKind.False => JsonKind.Bool,
                    JsonValueKind.String => JsonKind.String,
                    JsonValueKind.Number => e.TryGetInt64(out _) ? JsonKind.Int : e.TryGetUInt64(out _) ? JsonKind.UInt : JsonKind.Float,
                    _ => JsonKind.Null,
                };
            case bool: return JsonKind.Bool;
            case string or char: return JsonKind.String;
            case ulong: return JsonKind.UInt;
            case float or double or decimal: return JsonKind.Float;
            default: return JsonKind.Int;
        }
    }

    static object Raw(JsonNode n)
    {
        var v = n.AsValue().GetValue<object>();
        if (v is not JsonElement e) return v;
        return e.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => e.GetString()!,
            JsonValueKind.Number => e.TryGetInt64(out var l) ? l : e.TryGetUInt64(out var u) ? u : e.GetDouble(),
            _ => 0L,
        };
    }

    public static long Long(JsonNode n) => Raw(n) switch
    {
        ulong u => unchecked((long)u),
        double d => (long)d,
        float f => (long)f,
        decimal m => (long)m,
        bool b => b ? 1 : 0,
        string => 0,
        var o => Convert.ToInt64(o, Inv),
    };

    public static ulong ULong(JsonNode n) => Raw(n) is ulong u ? u : unchecked((ulong)Long(n));

    public static double Double(JsonNode n) => Raw(n) switch
    {
        double d => d,
        float f => f,
        decimal m => (double)m,
        ulong u => u,
        bool b => b ? 1 : 0,
        string => 0,
        var o => Convert.ToInt64(o, Inv),
    };

    public static bool Bool(JsonNode n) => Raw(n) is bool b && b;
    public static string Str(JsonNode n) => Raw(n) switch { string s => s, char ch => ch.ToString(), _ => "" };

    public static JsonNode? Int(long v) => JValue.Create(v);
    public static JsonNode? Float(double v) => JValue.Create(v);

    // ---- script text ----------------------------------------------------------

    /// <summary>A script string (Windows-1252 bytes) as json text: each byte becomes the code point of the same value.</summary>
    public static string FromScript(string s)
    {
        foreach (char ch in s)
            if (ch >= 0x80) goto convert;
        return s;
        convert:
        var bytes = Aurora.Text.Windows1252.GetBytes(s);
        var sb = new StringBuilder(bytes.Length);
        foreach (var b in bytes) sb.Append((char)b);
        return sb.ToString();
    }

    /// <summary>Json text back to a script string: code points up to 255 are bytes, anything else becomes '?'.</summary>
    public static string ToScript(string s)
    {
        foreach (char ch in s)
            if (ch >= 0x80) goto convert;
        return s;
        convert:
        var bytes = new List<byte>(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { bytes.Add((byte)'?'); i++; continue; }
            bytes.Add(s[i] <= 0xFF ? (byte)s[i] : (byte)'?');
        }
        return Aurora.Text.Windows1252.GetString(bytes.ToArray());
    }

    // ---- comparison -----------------------------------------------------------

    static int Rank(JsonKind k) => k switch
    {
        JsonKind.Null => 0,
        JsonKind.Bool => 1,
        JsonKind.Int or JsonKind.UInt or JsonKind.Float => 2,
        JsonKind.Object => 3,
        JsonKind.Array => 4,
        _ => 5,
    };

    /// <summary>Total order: null &lt; bool &lt; number &lt; object &lt; array &lt; string; numbers compare by value.</summary>
    public static int Compare(JsonNode? a, JsonNode? b)
    {
        JsonKind ka = Kind(a), kb = Kind(b);
        int ra = Rank(ka), rb = Rank(kb);
        if (ra != rb) return ra.CompareTo(rb);
        switch (ra)
        {
            case 0: return 0;
            case 1: return Bool(a!).CompareTo(Bool(b!));
            case 2:
                if (ka == JsonKind.Int && kb == JsonKind.Int) return Long(a!).CompareTo(Long(b!));
                if (ka == JsonKind.UInt && kb == JsonKind.UInt) return ULong(a!).CompareTo(ULong(b!));
                if (ka != JsonKind.Float && kb != JsonKind.Float)
                    return (ka == JsonKind.Int ? (decimal)Long(a!) : ULong(a!)).CompareTo(kb == JsonKind.Int ? (decimal)Long(b!) : ULong(b!));
                return Double(a!).CompareTo(Double(b!));
            case 5: return string.CompareOrdinal(Str(a!), Str(b!));
            case 4:
            {
                var x = (JsonArray)a!;
                var y = (JsonArray)b!;
                for (int i = 0; i < Math.Min(x.Count, y.Count); i++)
                {
                    int c = Compare(x[i], y[i]);
                    if (c != 0) return c;
                }
                return x.Count.CompareTo(y.Count);
            }
            default:
            {
                var x = Sorted((JsonObject)a!).ToList();
                var y = Sorted((JsonObject)b!).ToList();
                for (int i = 0; i < Math.Min(x.Count, y.Count); i++)
                {
                    int c = string.CompareOrdinal(x[i].Key, y[i].Key);
                    if (c != 0) return c;
                    c = Compare(x[i].Value, y[i].Value);
                    if (c != 0) return c;
                }
                return x.Count.CompareTo(y.Count);
            }
        }
    }

    public static bool Equal(JsonNode? a, JsonNode? b)
    {
        // NaN is never equal, even to itself.
        if (Kind(a) == JsonKind.Float && double.IsNaN(Double(a!)) || Kind(b) == JsonKind.Float && double.IsNaN(Double(b!))) return false;
        return Compare(a, b) == 0;
    }

    /// <summary>Object members in key order (the original keeps objects sorted).</summary>
    public static IEnumerable<KeyValuePair<string, JsonNode?>> Sorted(JsonObject o) => o.OrderBy(kv => kv.Key, StringComparer.Ordinal);

    // ---- dumping ----------------------------------------------------------------

    /// <summary>Compact when <paramref name="indent"/> is negative, otherwise one value per line indented by that many spaces.</summary>
    public static string Dump(JsonNode? n, int indent = -1)
    {
        var sb = new StringBuilder();
        Write(sb, n, indent, 0);
        return sb.ToString();
    }

    static void Write(StringBuilder sb, JsonNode? n, int indent, int depth)
    {
        switch (Kind(n))
        {
            case JsonKind.Null: sb.Append("null"); return;
            case JsonKind.Bool: sb.Append(Bool(n!) ? "true" : "false"); return;
            case JsonKind.Int: sb.Append(Long(n!).ToString(Inv)); return;
            case JsonKind.UInt: sb.Append(ULong(n!).ToString(Inv)); return;
            case JsonKind.Float: sb.Append(FormatDouble(Double(n!))); return;
            case JsonKind.String: WriteString(sb, Str(n!)); return;
            case JsonKind.Array:
            {
                var a = (JsonArray)n!;
                if (a.Count == 0) { sb.Append("[]"); return; }
                sb.Append('[');
                for (int i = 0; i < a.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    NewLine(sb, indent, depth + 1);
                    Write(sb, a[i], indent, depth + 1);
                }
                NewLine(sb, indent, depth);
                sb.Append(']');
                return;
            }
            default:
            {
                var o = (JsonObject)n!;
                if (o.Count == 0) { sb.Append("{}"); return; }
                sb.Append('{');
                bool first = true;
                foreach (var kv in Sorted(o))
                {
                    if (!first) sb.Append(',');
                    first = false;
                    NewLine(sb, indent, depth + 1);
                    WriteString(sb, kv.Key);
                    sb.Append(indent >= 0 ? ": " : ":");
                    Write(sb, kv.Value, indent, depth + 1);
                }
                NewLine(sb, indent, depth);
                sb.Append('}');
                return;
            }
        }
    }

    static void NewLine(StringBuilder sb, int indent, int depth)
    {
        if (indent < 0) return;
        sb.Append('\n');
        sb.Append(' ', indent * depth);
    }

    /// <summary>ASCII only: control characters and everything from 0x7F up become \u escapes.</summary>
    static void WriteString(StringBuilder sb, string s)
    {
        sb.Append('"');
        foreach (char ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < 0x20 || ch >= 0x7F) sb.Append("\\u").Append(((int)ch).ToString("x4", Inv));
                    else sb.Append(ch);
                    break;
            }
        }
        sb.Append('"');
    }

    /// <summary>
    /// Shortest round-trip digits, written plainly for decimal exponents -4 &lt; n &lt;= 15 (always with a
    /// fractional part) and as d.ddde+XX otherwise; NaN and infinities are null.
    /// </summary>
    public static string FormatDouble(double d)
    {
        if (double.IsNaN(d) || double.IsInfinity(d)) return "null";
        if (d == 0) return BitConverter.DoubleToInt64Bits(d) < 0 ? "-0.0" : "0.0";
        string r = Math.Abs(d).ToString("R", Inv);
        int e = r.IndexOf('E');
        int exp10 = 0;
        string mant = r;
        if (e >= 0) { exp10 = int.Parse(r[(e + 1)..], Inv); mant = r[..e]; }
        int dot = mant.IndexOf('.');
        string digits = dot < 0 ? mant : mant.Remove(dot, 1);
        int point = dot < 0 ? mant.Length : dot;
        int lead = 0;
        while (lead < digits.Length - 1 && digits[lead] == '0') lead++;
        digits = digits[lead..].TrimEnd('0');
        point -= lead;
        int k = digits.Length, n = point + exp10;
        var sb = new StringBuilder();
        if (d < 0) sb.Append('-');
        if (k <= n && n <= 15) sb.Append(digits).Append('0', n - k).Append(".0");
        else if (0 < n && n <= 15) sb.Append(digits, 0, n).Append('.').Append(digits, n, k - n);
        else if (-4 < n && n <= 0) sb.Append("0.").Append('0', -n).Append(digits);
        else
        {
            sb.Append(digits[0]);
            if (k > 1) sb.Append('.').Append(digits, 1, k - 1);
            int x = n - 1;
            sb.Append('e').Append(x < 0 ? '-' : '+').Append(Math.Abs(x).ToString(Math.Abs(x) < 10 ? "00" : "0", Inv));
        }
        return sb.ToString();
    }

    // ---- parsing ----------------------------------------------------------------

    public sealed class ParseException : Exception
    {
        public ParseException(string message) : base(message) { }
    }

    /// <summary>Parses json text; throws <see cref="ParseException"/> with the original's message on errors.</summary>
    public static JsonNode? Parse(string text) => new Parser(text).ParseDocument();

    enum Tok { Uninitialized, True, False, Null, String, Number, BeginArray, BeginObject, EndArray, EndObject, NameSeparator, ValueSeparator, ParseError, EndOfInput, LiteralOrValue }

    static string TokenName(Tok t) => t switch
    {
        Tok.True => "true literal",
        Tok.False => "false literal",
        Tok.Null => "null literal",
        Tok.String => "string literal",
        Tok.Number => "number literal",
        Tok.BeginArray => "'['",
        Tok.BeginObject => "'{'",
        Tok.EndArray => "']'",
        Tok.EndObject => "'}'",
        Tok.NameSeparator => "':'",
        Tok.ValueSeparator => "','",
        Tok.ParseError => "<parse error>",
        Tok.EndOfInput => "end of input",
        Tok.LiteralOrValue => "'[', '{', or a literal",
        _ => "<uninitialized>",
    };

    sealed class Parser
    {
        readonly string _s;
        int _pos = -1, _line, _col;
        int _current = -1;
        bool _ungot;
        readonly StringBuilder _tokenString = new();
        readonly StringBuilder _buffer = new();
        string _error = "";
        Tok _last;
        JsonNode? _number;

        public Parser(string s) { _s = s; }

        int Get()
        {
            _col++;
            if (_ungot) _ungot = false;
            else _current = ++_pos < _s.Length ? _s[_pos] : -1;
            if (_current != -1) _tokenString.Append((char)_current);
            if (_current == '\n') { _line++; _col = 0; }
            return _current;
        }

        void Unget()
        {
            _ungot = true;
            if (_col == 0) { if (_line > 0) _line--; }
            else _col--;
            if (_current != -1 && _tokenString.Length > 0) _tokenString.Length--;
        }

        void Reset()
        {
            _buffer.Clear();
            _tokenString.Clear();
            if (_current != -1) _tokenString.Append((char)_current);
        }

        static readonly string[] ControlNames =
        {
            "NUL", "SOH", "STX", "ETX", "EOT", "ENQ", "ACK", "BEL", "BS", "HT", "LF", "VT", "FF", "CR", "SO", "SI",
            "DLE", "DC1", "DC2", "DC3", "DC4", "NAK", "SYN", "ETB", "CAN", "EM", "SUB", "ESC", "FS", "GS", "RS", "US",
        };

        Tok Fail(string message) { _error = message; return Tok.ParseError; }

        Tok Scan()
        {
            do Get(); while (_current is ' ' or '\t' or '\n' or '\r');
            switch (_current)
            {
                case '[': return Tok.BeginArray;
                case ']': return Tok.EndArray;
                case '{': return Tok.BeginObject;
                case '}': return Tok.EndObject;
                case ':': return Tok.NameSeparator;
                case ',': return Tok.ValueSeparator;
                case 't': return Literal("true", Tok.True);
                case 'f': return Literal("false", Tok.False);
                case 'n': return Literal("null", Tok.Null);
                case '"': return ScanString();
                case '-': case >= '0' and <= '9': return ScanNumber();
                case -1: return Tok.EndOfInput;
                default: return Fail("invalid literal");
            }
        }

        Tok Literal(string word, Tok t)
        {
            for (int i = 1; i < word.Length; i++)
                if (Get() != word[i]) return Fail("invalid literal");
            return t;
        }

        int Hex4()
        {
            int v = 0;
            for (int i = 0; i < 4; i++)
            {
                int ch = Get();
                int d = ch is >= '0' and <= '9' ? ch - '0' : ch is >= 'a' and <= 'f' ? ch - 'a' + 10 : ch is >= 'A' and <= 'F' ? ch - 'A' + 10 : -1;
                if (d < 0) return -1;
                v = v * 16 + d;
            }
            return v;
        }

        Tok ScanString()
        {
            Reset();
            while (true)
            {
                int ch = Get();
                switch (ch)
                {
                    case -1: return Fail("invalid string: missing closing quote");
                    case '"': return Tok.String;
                    case '\\':
                        switch (Get())
                        {
                            case '"': _buffer.Append('"'); break;
                            case '\\': _buffer.Append('\\'); break;
                            case '/': _buffer.Append('/'); break;
                            case 'b': _buffer.Append('\b'); break;
                            case 'f': _buffer.Append('\f'); break;
                            case 'n': _buffer.Append('\n'); break;
                            case 'r': _buffer.Append('\r'); break;
                            case 't': _buffer.Append('\t'); break;
                            case 'u':
                            {
                                int cp = Hex4();
                                if (cp < 0) return Fail("invalid string: '\\u' must be followed by 4 hex digits");
                                if (cp is >= 0xD800 and <= 0xDBFF)
                                {
                                    if (Get() != '\\' || Get() != 'u') return Fail("invalid string: surrogate U+D800..U+DBFF must be followed by U+DC00..U+DFFF");
                                    int lo = Hex4();
                                    if (lo < 0) return Fail("invalid string: '\\u' must be followed by 4 hex digits");
                                    if (lo is < 0xDC00 or > 0xDFFF) return Fail("invalid string: surrogate U+D800..U+DBFF must be followed by U+DC00..U+DFFF");
                                    _buffer.Append((char)cp).Append((char)lo);
                                }
                                else if (cp is >= 0xDC00 and <= 0xDFFF) return Fail("invalid string: surrogate U+DC00..U+DFFF must follow U+D800..U+DBFF");
                                else _buffer.Append((char)cp);
                                break;
                            }
                            default: return Fail("invalid string: forbidden character after backslash");
                        }
                        break;
                    default:
                        if (ch < 0x20)
                        {
                            string hex = ch.ToString("X4", Inv);
                            string shortForm = ch switch { 8 => "\\b", 9 => "\\t", 10 => "\\n", 12 => "\\f", 13 => "\\r", _ => "" };
                            return Fail($"invalid string: control character U+{hex} ({ControlNames[ch]}) must be escaped to \\u{hex}" + (shortForm.Length > 0 ? " or " + shortForm : ""));
                        }
                        _buffer.Append((char)ch);
                        break;
                }
            }
        }

        Tok ScanNumber()
        {
            Reset();
            _buffer.Append((char)_current);
            bool isFloat = false;
            int ch = _current;
            if (ch == '-')
            {
                ch = Get();
                if (ch is < '0' or > '9') return Fail("invalid number; expected digit after '-'");
                _buffer.Append((char)ch);
            }
            if (ch != '0')
                while ((ch = Get()) is >= '0' and <= '9') _buffer.Append((char)ch);
            else ch = Get();
            if (ch == '.')
            {
                isFloat = true;
                _buffer.Append('.');
                ch = Get();
                if (ch is < '0' or > '9') return Fail("invalid number; expected digit after '.'");
                _buffer.Append((char)ch);
                while ((ch = Get()) is >= '0' and <= '9') _buffer.Append((char)ch);
            }
            if (ch is 'e' or 'E')
            {
                isFloat = true;
                _buffer.Append('e');
                ch = Get();
                if (ch is '+' or '-')
                {
                    _buffer.Append((char)ch);
                    ch = Get();
                    if (ch is < '0' or > '9') return Fail("invalid number; expected digit after exponent sign");
                }
                else if (ch is < '0' or > '9') return Fail("invalid number; expected '+', '-', or digit after exponent");
                _buffer.Append((char)ch);
                while ((ch = Get()) is >= '0' and <= '9') _buffer.Append((char)ch);
            }
            Unget();
            string text = _buffer.ToString();
            if (!isFloat)
            {
                if (text[0] == '-' ? long.TryParse(text, NumberStyles.AllowLeadingSign, Inv, out var l) && (_number = Int(l)) != null
                    : ulong.TryParse(text, NumberStyles.None, Inv, out var u) && (_number = u <= long.MaxValue ? Int((long)u) : JValue.Create(u)) != null)
                    return Tok.Number;
            }
            _number = Float(double.Parse(text, NumberStyles.Float, Inv));
            return Tok.Number;
        }

        string TokenString()
        {
            var sb = new StringBuilder();
            foreach (char c in _tokenString.ToString())
                if (c <= 0x1F) sb.Append("<U+").Append(((int)c).ToString("X4", Inv)).Append('>');
                else sb.Append(c);
            return sb.ToString();
        }

        ParseException Error(string context, Tok expected)
        {
            string msg = "syntax error while parsing " + context + " - ";
            msg += _last == Tok.ParseError ? _error + "; last read: '" + TokenString() + "'" : "unexpected " + TokenName(_last);
            if (expected != Tok.Uninitialized) msg += "; expected " + TokenName(expected);
            return new ParseException($"[json.exception.parse_error.101] parse error at line {_line + 1}, column {_col}: {msg}");
        }

        Tok Next() => _last = Scan();

        public JsonNode? ParseDocument()
        {
            Next();
            var v = Value();
            if (Next() != Tok.EndOfInput) throw Error("value", Tok.EndOfInput);
            return v;
        }

        /// <summary>The value starting at the current token.</summary>
        JsonNode? Value()
        {
            switch (_last)
            {
                case Tok.BeginObject:
                {
                    var o = new JsonObject();
                    if (Next() == Tok.EndObject) return o;
                    while (true)
                    {
                        if (_last != Tok.String) throw Error("object key", Tok.String);
                        string key = _buffer.ToString();
                        if (Next() != Tok.NameSeparator) throw Error("object separator", Tok.NameSeparator);
                        Next();
                        o[key] = Value();
                        Next();
                        if (_last == Tok.ValueSeparator) { Next(); continue; }
                        if (_last == Tok.EndObject) return o;
                        throw Error("object", Tok.EndObject);
                    }
                }
                case Tok.BeginArray:
                {
                    var a = new JsonArray();
                    if (Next() == Tok.EndArray) return a;
                    while (true)
                    {
                        a.Add(Value());
                        Next();
                        if (_last == Tok.ValueSeparator) { Next(); continue; }
                        if (_last == Tok.EndArray) return a;
                        throw Error("array", Tok.EndArray);
                    }
                }
                case Tok.True: return JValue.Create(true);
                case Tok.False: return JValue.Create(false);
                case Tok.Null: return null;
                case Tok.String: return JValue.Create(_buffer.ToString());
                case Tok.Number:
                    if (Kind(_number) == JsonKind.Float && double.IsInfinity(Double(_number!)))
                        throw new ParseException($"[json.exception.out_of_range.406] number overflow parsing '{_tokenString}'");
                    return _number;
                case Tok.ParseError: throw Error("value", Tok.Uninitialized);
                default: throw Error("value", Tok.LiteralOrValue);
            }
        }
    }

    // ---- JSON pointers ----------------------------------------------------------

    /// <summary>Reference tokens of a pointer; throws <see cref="ParseException"/> for malformed pointers.</summary>
    public static List<string> ParsePointer(string pointer)
    {
        var tokens = new List<string>();
        if (pointer.Length == 0) return tokens;
        if (pointer[0] != '/')
            throw new ParseException($"[json.exception.parse_error.107] parse error at byte 1: JSON pointer must be empty or begin with '/' - was: '{pointer}'");
        foreach (var raw in pointer[1..].Split('/'))
        {
            for (int i = 0; i < raw.Length; i++)
                if (raw[i] == '~' && (i + 1 == raw.Length || raw[i + 1] is not ('0' or '1')))
                    throw new ParseException("[json.exception.parse_error.108] parse error: escape character '~' must be followed with '0' or '1'");
            tokens.Add(raw.Replace("~1", "/").Replace("~0", "~"));
        }
        return tokens;
    }

    public static string EscapeToken(string key) => key.Replace("~", "~0").Replace("/", "~1");

    /// <summary>An array index token: digits without a leading zero.</summary>
    public static int ArrayIndex(string token)
    {
        if (token.Length > 1 && token[0] == '0')
            throw new ParseException($"[json.exception.parse_error.106] parse error: array index '{token}' must not begin with '0'");
        if (token.Length == 0 || token[0] is < '0' or > '9' || !token.All(char.IsAsciiDigit))
            throw new ParseException($"[json.exception.parse_error.109] parse error: array index '{token}' is not a number");
        return ulong.TryParse(token, NumberStyles.None, Inv, out var v) && v <= int.MaxValue ? (int)v : int.MaxValue;
    }

    /// <summary>Follows the first <paramref name="count"/> tokens; throws the original's out_of_range errors for missing members.</summary>
    public static JsonNode? At(JsonNode? root, List<string> tokens, int count)
    {
        var n = root;
        for (int i = 0; i < count; i++)
        {
            var t = tokens[i];
            switch (n)
            {
                case JsonObject o:
                    if (!o.TryGetPropertyValue(t, out var v)) throw new ParseException($"[json.exception.out_of_range.403] key '{t}' not found");
                    n = v;
                    break;
                case JsonArray a:
                    if (t == "-") throw new ParseException($"[json.exception.out_of_range.402] array index '-' ({a.Count}) is out of range");
                    int idx = ArrayIndex(t);
                    if (idx >= a.Count) throw new ParseException($"[json.exception.out_of_range.401] array index {idx} is out of range");
                    n = a[idx];
                    break;
                default:
                    throw new ParseException($"[json.exception.out_of_range.404] unresolved reference token '{t}'");
            }
        }
        return n;
    }
}
