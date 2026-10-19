using System.Text;
using System.Text.RegularExpressions;

namespace OpenWinter.Game.Scripting;

/// <summary>
/// The regular expressions behind RegExpMatch/Iterate/Replace, as the original runs them (the C++ standard
/// library's std::regex): ECMAScript syntax with ASCII character classes, POSIX bracket classes and
/// multi-line anchors; every other grammar flag gives POSIX basic syntax. Patterns are translated to .NET
/// regexes; errors carry the original's "regex_error(...)" texts.
/// </summary>
public static class StdRegex
{
    // REGEXP_* syntax flags.
    const int Icase = 32, NoSubs = 64;
    // REGEXP_MATCH_* and REGEXP_FORMAT_* flags.
    const int NotBol = 1, NotEol = 2, NotBow = 4, NotEow = 8, NotNull = 32, Continuous = 64, PrevAvail = 128;
    const int FormatSed = 256, FormatNoCopy = 512, FormatFirstOnly = 1024;

    public sealed class RegexError : Exception
    {
        public RegexError(string code) : base(Describe(code)) { }

        static string Describe(string code)
        {
            string text = code switch
            {
                "error_collate" => "The expression contained an invalid collating element name.",
                "error_ctype" => "The expression contained an invalid character class name.",
                "error_escape" => "The expression contained an invalid escaped character, or a trailing escape.",
                "error_backref" => "The expression contained an invalid back reference.",
                "error_brack" => "The expression contained mismatched [ and ].",
                "error_paren" => "The expression contained mismatched ( and ).",
                "error_brace" => "The expression contained mismatched { and }.",
                "error_badbrace" => "The expression contained an invalid range in a { expression }.",
                "error_range" => "The expression contained an invalid character range, such as [b-a] in most encodings.",
                "error_badrepeat" => "One of *?+{ was not preceded by a valid regular expression.",
                _ => "",
            };
            return text.Length > 0 ? $"regex_error({code}): {text}" : $"regex_error({code})";
        }
    }

    sealed record Compiled(Regex Regex, int Groups);

    static readonly Dictionary<(string, int, int), Compiled> _cache = new();

    /// <summary>Compiles a pattern for the given syntax and match flags; throws <see cref="RegexError"/>.</summary>
    static Compiled Compile(string pattern, int syntax, int match)
    {
        var key = (pattern, syntax, match & (NotBol | NotEol | NotBow | NotEow | Continuous | PrevAvail));
        lock (_cache)
            if (_cache.TryGetValue(key, out var hit)) return hit;
        if ((match & PrevAvail) != 0) match |= NotBol | NotBow;
        var t = new Translator(pattern, (syntax & 0x1F) == 0, match);
        string net = t.Translate();
        if ((match & Continuous) != 0) net = @"\G(?:" + net + ")";
        var options = RegexOptions.CultureInvariant | ((syntax & Icase) != 0 ? RegexOptions.IgnoreCase : 0);
        Regex re;
        try { re = new Regex(net, options); }
        catch (ArgumentException) { throw new RegexError("error_syntax"); }
        var c = new Compiled(re, t.Groups);
        lock (_cache)
        {
            if (_cache.Count > 512) _cache.Clear();
            _cache[key] = c;
        }
        return c;
    }

    /// <summary>The matches std::regex_search / regex_iterator would find.</summary>
    static IEnumerable<Match> Matches(Compiled c, string input, int flags)
    {
        int at = 0;
        while (at <= input.Length)
        {
            var m = c.Regex.Match(input, at);
            if (!m.Success) yield break;
            if ((flags & NotNull) != 0 && m.Length == 0)
            {
                // Look for a non-empty match further on.
                var next = m;
                while (next.Success && next.Length == 0) next = next.NextMatch();
                if (!next.Success) yield break;
                m = next;
            }
            yield return m;
            at = m.Length == 0 ? m.Index + 1 : m.Index + m.Length;
            if ((flags & Continuous) != 0 && m.Length == 0) yield break;
        }
    }

    static List<string> Groups(Compiled c, Match m, int syntax)
    {
        var list = new List<string> { m.Value };
        if ((syntax & NoSubs) == 0)
            for (int g = 1; g <= c.Groups; g++) list.Add(m.Groups[g].Success ? m.Groups[g].Value : "");
        return list;
    }

    /// <summary>The first match and its groups, an empty list when nothing matches.</summary>
    public static List<string> Match(string pattern, string input, int syntax, int flags)
    {
        var c = Compile(pattern, syntax, flags);
        foreach (var m in Matches(c, input, flags)) return Groups(c, m, syntax);
        return new List<string>();
    }

    public static List<List<string>> Iterate(string pattern, string input, int syntax, int flags)
    {
        var c = Compile(pattern, syntax, flags);
        return Matches(c, input, flags).Select(m => Groups(c, m, syntax)).ToList();
    }

    /// <summary>std::regex_replace with the default (ECMAScript $) or sed format, and the no-copy / first-only flags.</summary>
    public static string Replace(string pattern, string input, string format, int syntax, int flags)
    {
        var c = Compile(pattern, syntax, flags);
        var sb = new StringBuilder();
        int last = 0;
        foreach (var m in Matches(c, input, flags))
        {
            if ((flags & FormatNoCopy) == 0) sb.Append(input, last, m.Index - last);
            Format(sb, m, c.Groups, input, format, (flags & FormatSed) != 0);
            last = m.Index + m.Length;
            if ((flags & FormatFirstOnly) != 0) break;
        }
        if ((flags & FormatNoCopy) == 0) sb.Append(input, last, input.Length - last);
        return sb.ToString();
    }

    static void Format(StringBuilder sb, Match m, int groups, string input, string fmt, bool sed)
    {
        string Group(int n) => n <= groups && m.Groups[n].Success ? m.Groups[n].Value : "";
        for (int i = 0; i < fmt.Length; i++)
        {
            char ch = fmt[i];
            if (sed)
            {
                if (ch == '&') sb.Append(m.Value);
                else if (ch == '\\' && i + 1 < fmt.Length)
                {
                    char n = fmt[++i];
                    if (n is >= '0' and <= '9') sb.Append(n == '0' ? m.Value : Group(n - '0'));
                    else sb.Append(n);
                }
                else sb.Append(ch);
                continue;
            }
            if (ch != '$' || i + 1 == fmt.Length) { sb.Append(ch); continue; }
            char next = fmt[i + 1];
            switch (next)
            {
                case '$': sb.Append('$'); i++; break;
                case '&': sb.Append(m.Value); i++; break;
                case '`': sb.Append(input, 0, m.Index); i++; break;
                case '\'': sb.Append(input, m.Index + m.Length, input.Length - m.Index - m.Length); i++; break;
                case >= '0' and <= '9':
                {
                    int n = next - '0', used = 1;
                    if (i + 2 < fmt.Length && fmt[i + 2] is >= '0' and <= '9') { n = n * 10 + (fmt[i + 2] - '0'); used = 2; }
                    // $0 / $00 are not references and stay as written; other numbers name a group (or nothing).
                    if (n == 0) sb.Append(fmt, i, used + 1);
                    else sb.Append(Group(n));
                    i += used;
                    break;
                }
                default: sb.Append('$'); break;
            }
        }
    }

    /// <summary>Rewrites an ECMAScript or POSIX basic pattern as a .NET pattern, validating it the way std::regex does.</summary>
    sealed class Translator
    {
        const string Word = "A-Za-z0-9_";
        readonly string _p;
        readonly bool _ecma;
        readonly int _flags;
        readonly int _declaredGroups;
        int _i;
        public int Groups;
        readonly StringBuilder _out = new();

        public Translator(string pattern, bool ecma, int flags)
        {
            _p = pattern;
            _ecma = ecma;
            _flags = flags;
            _declaredGroups = CountGroups();
        }

        int CountGroups()
        {
            int n = 0;
            for (int i = 0; i < _p.Length; i++)
            {
                if (_p[i] == '\\') { if (!_ecma && i + 1 < _p.Length && _p[i + 1] == '(') n++; i++; continue; }
                if (_p[i] == '[')
                {
                    int j = i + 1;
                    if (j < _p.Length && _p[j] == '^') j++;
                    if (j < _p.Length && _p[j] == ']') j++;
                    while (j < _p.Length && _p[j] != ']') { if (_p[j] == '[' && j + 1 < _p.Length && _p[j + 1] is ':' or '=' or '.') { int k = _p.IndexOf(_p[j + 1] + "]", j + 2, StringComparison.Ordinal); j = k < 0 ? _p.Length : k + 2; continue; } if (_ecma && _p[j] == '\\') j++; j++; }
                    i = j;
                    continue;
                }
                if (_ecma && _p[i] == '(' && !(i + 1 < _p.Length && _p[i + 1] == '?')) n++;
            }
            return n;
        }

        public string Translate()
        {
            int depth = 0;
            bool canRepeat = false;
            while (_i < _p.Length)
            {
                char ch = _p[_i++];
                if (_ecma)
                {
                    switch (ch)
                    {
                        case '\\': Escape(); canRepeat = true; continue;
                        case '[': Bracket(); canRepeat = true; continue;
                        case '(':
                            if (_i < _p.Length && _p[_i] == '?')
                            {
                                if (_i + 1 < _p.Length && _p[_i + 1] is ':' or '=' or '!') { _out.Append("(?").Append(_p[_i + 1]); _i += 2; }
                                else throw new RegexError("error_syntax");
                            }
                            else { _out.Append('('); Groups++; }
                            depth++;
                            canRepeat = false;
                            continue;
                        case ')':
                            if (depth == 0) throw new RegexError("error_paren");
                            depth--;
                            _out.Append(')');
                            canRepeat = true;
                            continue;
                        case '*': case '+': case '?':
                            if (!canRepeat) throw new RegexError("error_badrepeat");
                            _out.Append(ch);
                            if (_i < _p.Length && _p[_i] == '?') { _out.Append('?'); _i++; }
                            canRepeat = false;
                            continue;
                        case '{':
                            if (!canRepeat) throw new RegexError("error_badrepeat");
                            Interval('}', 1);
                            if (_i < _p.Length && _p[_i] == '?') { _out.Append('?'); _i++; }
                            canRepeat = false;
                            continue;
                        case '|': _out.Append('|'); canRepeat = false; continue;
                        case '^': _out.Append((_flags & NotBol) != 0 ? @"(?<=\n)" : "(?m:^)"); canRepeat = false; continue;
                        case '$': _out.Append((_flags & NotEol) != 0 ? @"(?=\n)" : "(?m:$)"); canRepeat = false; continue;
                        case '.': _out.Append(@"[^\n\r  ]"); canRepeat = true; continue;
                        default: _out.Append(Regex.Escape(ch.ToString())); canRepeat = true; continue;
                    }
                }
                // POSIX basic.
                switch (ch)
                {
                    case '\\':
                        if (_i == _p.Length) throw new RegexError("error_escape");
                        char e = _p[_i++];
                        switch (e)
                        {
                            case '(': _out.Append('('); Groups++; depth++; canRepeat = false; break;
                            case ')':
                                if (depth == 0) throw new RegexError("error_paren");
                                depth--;
                                _out.Append(')');
                                canRepeat = true;
                                break;
                            case '{':
                                if (!canRepeat) throw new RegexError("error_badrepeat");
                                Interval('}', 2);
                                canRepeat = false;
                                break;
                            case '.': case '[': case ']': case '\\': case '*': case '^': case '$': case '}':
                                _out.Append(Regex.Escape(e.ToString()));
                                canRepeat = true;
                                break;
                            default: throw new RegexError("error_escape");
                        }
                        continue;
                    case '[': Bracket(); canRepeat = true; continue;
                    case '*':
                        if (!canRepeat) { _out.Append(@"\*"); canRepeat = true; continue; }
                        _out.Append('*');
                        canRepeat = false;
                        continue;
                    case '.': _out.Append(@"[^\n]"); canRepeat = true; continue;
                    case '^' when _out.Length == 0 || _out[^1] == '(': _out.Append((_flags & NotBol) != 0 ? @"(?<=\n)" : "(?m:^)"); canRepeat = false; continue;
                    case '$' when _i == _p.Length || _p.AsSpan(_i).StartsWith(@"\)"): _out.Append((_flags & NotEol) != 0 ? @"(?=\n)" : "(?m:$)"); canRepeat = false; continue;
                    default: _out.Append(Regex.Escape(ch.ToString())); canRepeat = true; continue;
                }
            }
            if (depth != 0) throw new RegexError("error_paren");
            return _out.ToString();
        }

        /// <summary>{n}, {n,} or {n,m}; for basic syntax the closing brace is escaped.</summary>
        void Interval(char close, int closeLength)
        {
            int start = _i;
            while (_i < _p.Length && char.IsAsciiDigit(_p[_i])) _i++;
            if (_i == start) throw new RegexError("error_badbrace");
            int min = int.Parse(_p.AsSpan(start, _i - start));
            int max = min;
            if (_i < _p.Length && _p[_i] == ',')
            {
                _i++;
                int s2 = _i;
                while (_i < _p.Length && char.IsAsciiDigit(_p[_i])) _i++;
                max = _i == s2 ? -1 : int.Parse(_p.AsSpan(s2, _i - s2));
            }
            bool closed = closeLength == 1 ? _i < _p.Length && _p[_i] == close : _i + 1 < _p.Length && _p[_i] == '\\' && _p[_i + 1] == close;
            if (!closed || max != -1 && max < min) throw new RegexError("error_badbrace");
            _i += closeLength;
            _out.Append('{').Append(min);
            if (max != min) _out.Append(',').Append(max == -1 ? "" : max.ToString());
            _out.Append('}');
        }

        void Escape()
        {
            if (_i == _p.Length) throw new RegexError("error_escape");
            char e = _p[_i++];
            switch (e)
            {
                case 'd': _out.Append("[0-9]"); return;
                case 'D': _out.Append("[^0-9]"); return;
                case 's': _out.Append(@"[ \t\n\v\f\r]"); return;
                case 'S': _out.Append(@"[^ \t\n\v\f\r]"); return;
                case 'w': _out.Append($"[{Word}]"); return;
                case 'W': _out.Append($"[^{Word}]"); return;
                case 'b':
                {
                    string b = $"(?:(?<=[{Word}])(?![{Word}])|(?<![{Word}])(?=[{Word}]))";
                    if ((_flags & NotBow) != 0) b = $"(?:(?<=[{Word}])(?![{Word}])|(?<=[^{Word}])(?=[{Word}]))";
                    if ((_flags & NotEow) != 0) b = $"(?:(?<=[{Word}])(?=[^{Word}])|(?<![{Word}])(?=[{Word}]))";
                    _out.Append(b);
                    return;
                }
                case 'B': _out.Append($"(?:(?<=[{Word}])(?=[{Word}])|(?<![{Word}])(?![{Word}]))"); return;
                case >= '1' and <= '9':
                {
                    int n = e - '0';
                    while (_i < _p.Length && char.IsAsciiDigit(_p[_i])) n = n * 10 + (_p[_i++] - '0');
                    if (n > _declaredGroups) throw new RegexError("error_backref");
                    _out.Append(@"\").Append(n);
                    return;
                }
                default:
                    _out.Append(Regex.Escape(EscapedChar(e).ToString()));
                    return;
            }
        }

        /// <summary>The character a non-class escape stands for (\n, \x41, A, \cJ, or the character itself).</summary>
        char EscapedChar(char e)
        {
            switch (e)
            {
                case 'f': return '\f';
                case 'n': return '\n';
                case 'r': return '\r';
                case 't': return '\t';
                case 'v': return '\v';
                case '0': return '\0';
                case 'c':
                    if (_i < _p.Length && char.IsAsciiLetter(_p[_i])) return (char)(_p[_i++] % 32);
                    throw new RegexError("error_escape");
                case 'x': return Hex(2);
                case 'u': return Hex(4);
                default: return e;
            }
        }

        char Hex(int digits)
        {
            if (_i + digits > _p.Length) throw new RegexError("error_escape");
            int v = 0;
            for (int k = 0; k < digits; k++)
            {
                int d = Convert.ToInt32(char.IsAsciiHexDigit(_p[_i + k]) ? _p[_i + k].ToString() : throw new RegexError("error_escape"), 16);
                v = v * 16 + d;
            }
            _i += digits;
            return (char)v;
        }

        static readonly Dictionary<string, string> PosixClasses = new()
        {
            ["alnum"] = "A-Za-z0-9", ["alpha"] = "A-Za-z", ["blank"] = @" \t", ["cntrl"] = @"\x00-\x1F\x7F",
            ["digit"] = "0-9", ["graph"] = @"\x21-\x7E", ["lower"] = "a-z", ["print"] = @"\x20-\x7E",
            ["punct"] = @"!-/:-@\[-`{-~", ["space"] = @" \t\n\v\f\r", ["upper"] = "A-Z", ["xdigit"] = "0-9A-Fa-f",
            ["d"] = "0-9", ["s"] = @" \t\n\v\f\r", ["w"] = Word,
        };

        /// <summary>A bracket expression, from just after '['.</summary>
        void Bracket()
        {
            var sb = new StringBuilder();
            bool negate = _i < _p.Length && _p[_i] == '^';
            if (negate) _i++;
            bool first = true;
            int? pending = null; // the previous single character, for ranges
            var negClasses = new List<string>();
            while (true)
            {
                if (_i >= _p.Length) throw new RegexError("error_brack");
                char ch = _p[_i++];
                if (ch == ']' && !(first && !_ecma)) break;
                first = false;
                int single = -1;
                if (ch == '[' && _i < _p.Length && _p[_i] is ':' or '=' or '.')
                {
                    char kind = _p[_i];
                    int end = _p.IndexOf(kind + "]", _i + 1, StringComparison.Ordinal);
                    if (end < 0) throw new RegexError("error_brack");
                    string name = _p.Substring(_i + 1, end - _i - 1);
                    _i = end + 2;
                    if (kind == ':')
                    {
                        if (!PosixClasses.TryGetValue(name, out var set)) throw new RegexError("error_ctype");
                        sb.Append(set);
                        pending = null;
                        continue;
                    }
                    if (name.Length != 1) throw new RegexError("error_collate");
                    single = name[0];
                }
                else if (ch == '\\' && _ecma)
                {
                    if (_i >= _p.Length) throw new RegexError("error_escape");
                    char e = _p[_i++];
                    switch (e)
                    {
                        case 'd': sb.Append("0-9"); pending = null; continue;
                        case 's': sb.Append(@" \t\n\v\f\r"); pending = null; continue;
                        case 'w': sb.Append(Word); pending = null; continue;
                        case 'D': negClasses.Add("0-9"); pending = null; continue;
                        case 'S': negClasses.Add(@" \t\n\v\f\r"); pending = null; continue;
                        case 'W': negClasses.Add(Word); pending = null; continue;
                        case 'b': single = '\b'; break;
                        default: single = EscapedChar(e); break;
                    }
                }
                else if (ch == '-' && pending != null && _i < _p.Length && _p[_i] != ']')
                {
                    // A range: the end may itself be an escape or a collating element.
                    int lo = pending.Value;
                    char hiCh = _p[_i++];
                    int hi;
                    if (hiCh == '\\' && _ecma)
                    {
                        if (_i >= _p.Length) throw new RegexError("error_escape");
                        hi = EscapedChar(_p[_i++]);
                    }
                    else if (hiCh == '[' && _i < _p.Length && _p[_i] is '.' or '=')
                    {
                        char kind = _p[_i];
                        int end = _p.IndexOf(kind + "]", _i + 1, StringComparison.Ordinal);
                        if (end < 0) throw new RegexError("error_brack");
                        string name = _p.Substring(_i + 1, end - _i - 1);
                        if (name.Length != 1) throw new RegexError("error_collate");
                        hi = name[0];
                        _i = end + 2;
                    }
                    else hi = hiCh;
                    if (hi < lo) throw new RegexError("error_range");
                    sb.Append('-').Append(EscapeInClass((char)hi));
                    pending = null;
                    continue;
                }
                else single = ch;
                sb.Append(EscapeInClass((char)single));
                pending = single;
            }
            string body = sb.ToString();
            if (negClasses.Count > 0)
            {
                // [\D...] and friends: a union, written as alternatives.
                var parts = new List<string>();
                if (body.Length > 0) parts.Add("[" + body + "]");
                parts.AddRange(negClasses.Select(n => "[^" + n + "]"));
                string union = "(?:" + string.Join("|", parts) + ")";
                _out.Append(negate ? @"(?!" + union + @")[\s\S]" : union);
                return;
            }
            if (body.Length == 0) _out.Append(negate ? @"[\s\S]" : "(?!)");
            else _out.Append(negate ? "[^" : "[").Append(body).Append(']');
        }

        static string EscapeInClass(char c) => c is '\\' or ']' or '[' or '^' or '-' ? "\\" + c : c < 0x20 || c > 0x7E ? $"\\u{(int)c:X4}" : c.ToString();
    }
}
