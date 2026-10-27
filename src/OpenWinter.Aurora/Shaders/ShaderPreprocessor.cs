using System.Globalization;
using System.Text;

namespace OpenWinter.Aurora.Shaders;

/// <summary>
/// The C-style preprocessor of shader sources: #include (resolved by name through <see cref="Resolve"/>),
/// object- and function-like #define, #undef, #if / #ifdef / #ifndef / #elif / #else / #endif with
/// <c>defined</c>, and #error. Comments are removed. The output has no directives left and every macro expanded.
/// #version, #extension and #pragma lines are dropped.
/// </summary>
public sealed class ShaderPreprocessor
{
    public sealed record Macro(string Name, string[]? Parameters, List<string> Body);

    /// <summary>Source of an included file by name (without extension), or null when there is none.</summary>
    public Func<string, string?> Resolve { get; init; } = _ => null;
    public readonly Dictionary<string, Macro> Macros = new(StringComparer.Ordinal);
    public readonly List<string> Problems = new();
    /// <summary>Every file included, in the order first seen.</summary>
    public readonly List<string> Included = new();
    /// <summary>Marks where each file's text starts and resumes in the output ("@@file name" lines).</summary>
    public bool FileMarkers { get; init; }
    public const string Marker = "@@file ";

    // Budgets: a source that would nest, include or expand without end is refused, not followed.
    const int MaxDepth = 32;
    const int MaxIncludes = 512;
    const int MaxOutput = 8 << 20;
    const int MaxExpandedTokens = 4 << 20;
    const int MaxRecursion = 200;
    int _includes, _expanded, _recursion;

    sealed class LimitException(string message) : Exception(message);

    public void Define(string name, string value) => Macros[name] = new Macro(name, null, Lex(value).Where(t => !IsSpace(t)).ToList());

    public string Process(string source, string name = "")
    {
        var output = new StringBuilder();
        try
        {
            ProcessFile(source, name, output, 0);
        }
        catch (LimitException e)
        {
            Problems.Add($"{(name.Length > 0 ? name : "shader")}: {e.Message}");
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            Problems.Add($"{(name.Length > 0 ? name : "shader")}: can't be read ({e.GetType().Name})");
        }
        return output.ToString();
    }

    void ProcessFile(string source, string file, StringBuilder output, int depth)
    {
        if (FileMarkers) output.Append(Marker).Append(file).Append('\n');
        var lines = SplitLines(StripComments(source));
        // Each entry: (this branch is active, some branch of the group was taken, the enclosing group is active).
        var stack = new Stack<(bool Active, bool Taken, bool Parent)>();
        bool active = true;
        for (int li = 0; li < lines.Count; li++)
        {
            string line = lines[li];
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith('#'))
            {
                string directive = trimmed[1..].TrimStart();
                int split = 0;
                while (split < directive.Length && char.IsAsciiLetter(directive[split])) split++;
                string keyword = directive[..split];
                string rest = directive[split..].Trim();
                switch (keyword)
                {
                    case "if":
                    case "ifdef":
                    case "ifndef":
                    {
                        bool value = active && (keyword switch
                        {
                            "ifdef" => Macros.ContainsKey(FirstWord(rest)),
                            "ifndef" => !Macros.ContainsKey(FirstWord(rest)),
                            _ => Evaluate(rest, file, li) != 0,
                        });
                        stack.Push((value, value, active));
                        active = value;
                        continue;
                    }
                    case "elif":
                    {
                        if (stack.Count == 0) { Problem(file, li, "#elif without #if"); continue; }
                        var top = stack.Pop();
                        bool value = top.Parent && !top.Taken && Evaluate(rest, file, li) != 0;
                        stack.Push((value, top.Taken || value, top.Parent));
                        active = value;
                        continue;
                    }
                    case "else":
                    {
                        if (stack.Count == 0) { Problem(file, li, "#else without #if"); continue; }
                        var top = stack.Pop();
                        bool value = top.Parent && !top.Taken;
                        stack.Push((value, true, top.Parent));
                        active = value;
                        continue;
                    }
                    case "endif":
                    {
                        if (stack.Count == 0) { Problem(file, li, "#endif without #if"); continue; }
                        active = stack.Pop().Parent;
                        continue;
                    }
                }
                if (!active) continue;
                switch (keyword)
                {
                    case "define": DefineLine(rest, file, li); break;
                    case "undef": Macros.Remove(FirstWord(rest)); break;
                    case "include":
                    {
                        string target = rest.Trim().Trim('"', '<', '>');
                        if (target.EndsWith(".shd", StringComparison.OrdinalIgnoreCase)) target = target[..^4];
                        if (depth >= MaxDepth) { Problem(file, li, $"includes nest deeper than {MaxDepth}"); break; }
                        if (Resolve(target) is not { } included) { Problem(file, li, $"include \"{target}\" not found"); break; }
                        if (++_includes > MaxIncludes) throw new LimitException($"more than {MaxIncludes} includes");
                        if (!Included.Contains(target, StringComparer.OrdinalIgnoreCase)) Included.Add(target);
                        ProcessFile(included, target, output, depth + 1);
                        if (FileMarkers) output.Append(Marker).Append(file).Append('\n');
                        break;
                    }
                    case "error": Problem(file, li, "#error " + rest); break;
                    case "version" or "extension" or "pragma" or "line" or "": break;
                    default: Problem(file, li, $"unknown directive #{keyword}"); break;
                }
                continue;
            }
            if (!active) continue;
            // A function-like macro call can run over several lines: gather lines until its parentheses close.
            for (int joined = 0; joined < 64 && NeedsMore(line) && li + 1 < lines.Count && !lines[li + 1].TrimStart().StartsWith('#'); joined++)
                line += "\n" + lines[++li];
            output.Append(Expand(Lex(line), new HashSet<string>()).Aggregate(new StringBuilder(), (b, t) => b.Append(t)));
            output.Append('\n');
            if (output.Length > MaxOutput) throw new LimitException($"the source grows past {MaxOutput >> 20} MB");
        }
        if (stack.Count > 0) Problem(file, lines.Count, "#if without #endif");
    }

    void Problem(string file, int line, string message) => Problems.Add($"{(file.Length > 0 ? file : "shader")}:{line + 1}: {message}");

    void DefineLine(string rest, string file, int line)
    {
        int i = 0;
        while (i < rest.Length && IsIdent(rest[i], i == 0)) i++;
        if (i == 0) { Problem(file, line, "#define without a name"); return; }
        string name = rest[..i];
        string[]? parameters = null;
        string body = rest[i..];
        if (body.StartsWith('('))
        {
            int close = body.IndexOf(')');
            if (close < 0) { Problem(file, line, $"#define {name}: unclosed parameter list"); return; }
            parameters = body[1..close].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            body = body[(close + 1)..];
        }
        Macros[name] = new Macro(name, parameters, Trim(Lex(body.Trim())));
    }

    static List<string> Trim(List<string> tokens)
    {
        while (tokens.Count > 0 && IsSpace(tokens[0])) tokens.RemoveAt(0);
        while (tokens.Count > 0 && IsSpace(tokens[^1])) tokens.RemoveAt(tokens.Count - 1);
        return tokens;
    }

    static string FirstWord(string s)
    {
        int i = 0;
        while (i < s.Length && IsIdent(s[i], i == 0)) i++;
        return s[..i];
    }

    bool NeedsMore(string line)
    {
        var tokens = Lex(line);
        for (int i = 0; i < tokens.Count; i++)
        {
            if (!Macros.TryGetValue(tokens[i], out var m) || m.Parameters == null) continue;
            int j = i + 1;
            while (j < tokens.Count && IsSpace(tokens[j])) j++;
            if (j >= tokens.Count || tokens[j] != "(") continue;
            int depth = 0;
            for (; j < tokens.Count; j++)
            {
                if (tokens[j] == "(") depth++;
                else if (tokens[j] == ")" && --depth == 0) break;
            }
            if (depth > 0) return true;
        }
        return false;
    }

    /// <summary>Expands macros in a token list; <paramref name="hidden"/> holds the macros being expanded (no recursion).</summary>
    List<string> Expand(List<string> tokens, HashSet<string> hidden)
    {
        if (++_recursion > MaxRecursion) throw new LimitException($"macros nest deeper than {MaxRecursion}");
        try
        {
            var expanded = ExpandOnce(tokens, hidden);
            if ((_expanded += expanded.Count) > MaxExpandedTokens) throw new LimitException("macros expand to too much text");
            return expanded;
        }
        finally
        {
            _recursion--;
        }
    }

    List<string> ExpandOnce(List<string> tokens, HashSet<string> hidden)
    {
        var result = new List<string>();
        for (int i = 0; i < tokens.Count; i++)
        {
            string t = tokens[i];
            if (!Macros.TryGetValue(t, out var m) || hidden.Contains(t)) { result.Add(t); continue; }
            if (m.Parameters == null)
            {
                result.AddRange(Expand(m.Body, new HashSet<string>(hidden) { t }));
                continue;
            }
            int j = i + 1;
            while (j < tokens.Count && IsSpace(tokens[j])) j++;
            if (j >= tokens.Count || tokens[j] != "(") { result.Add(t); continue; }
            var args = new List<List<string>> { new() };
            int depth = 0;
            for (j++; j < tokens.Count; j++)
            {
                string a = tokens[j];
                if (a is "(" or "[") depth++;
                else if (a is ")" or "]")
                {
                    if (depth == 0 && a == ")") break;
                    depth--;
                }
                else if (a == "," && depth == 0) { args.Add(new()); continue; }
                args[^1].Add(a);
            }
            if (j >= tokens.Count) { result.Add(t); continue; }
            if (m.Parameters.Length == 0 && args.Count == 1 && args[0].All(IsSpace)) args.Clear();
            var expanded = args.Select(a => Expand(Trim(a), hidden)).ToList();
            var body = new List<string>();
            for (int b = 0; b < m.Body.Count; b++)
            {
                string bt = m.Body[b];
                if (bt == "#" && b + 1 < m.Body.Count && Array.IndexOf(m.Parameters, m.Body[b + 1]) is var sp and >= 0)
                {
                    body.Add("\"" + string.Concat(sp < args.Count ? Trim(args[sp]) : []) + "\"");
                    b++;
                    continue;
                }
                int p = Array.IndexOf(m.Parameters, bt);
                if (p >= 0) body.AddRange(p < expanded.Count ? expanded[p] : []);
                else body.Add(bt);
            }
            // Token pasting: a ## b.
            for (int k = body.Count - 1; k >= 0; k--)
            {
                if (body[k] != "##") continue;
                int left = k - 1, right = k + 1;
                while (left >= 0 && IsSpace(body[left])) left--;
                while (right < body.Count && IsSpace(body[right])) right++;
                if (left < 0 || right >= body.Count) { body.RemoveAt(k); continue; }
                string pasted = body[left] + body[right];
                body.RemoveRange(left, right - left + 1);
                body.Insert(left, pasted);
                k = left;
            }
            result.AddRange(Expand(body, new HashSet<string>(hidden) { t }));
            i = j;
        }
        return result;
    }

    // ---- #if expressions ----

    long Evaluate(string expression, string file, int line)
    {
        var raw = Lex(expression).Where(t => !IsSpace(t)).ToList();
        // "defined X" and "defined(X)" are decided before expansion.
        var tokens = new List<string>();
        for (int i = 0; i < raw.Count; i++)
        {
            if (raw[i] != "defined") { tokens.Add(raw[i]); continue; }
            string name;
            if (i + 1 < raw.Count && raw[i + 1] == "(" && i + 3 < raw.Count && raw[i + 3] == ")") { name = raw[i + 2]; i += 3; }
            else if (i + 1 < raw.Count) { name = raw[i + 1]; i += 1; }
            else { Problem(file, line, "defined without a name"); return 0; }
            tokens.Add(Macros.ContainsKey(name) ? "1" : "0");
        }
        var expanded = Expand(tokens, new HashSet<string>()).Where(t => !IsSpace(t)).ToList();
        try
        {
            int pos = 0;
            _depth = 0;
            long value = Ternary(expanded, ref pos);
            if (pos != expanded.Count) throw new FormatException($"unexpected '{expanded[pos]}'");
            return value;
        }
        catch (Exception e) when (e is FormatException or DivideByZeroException or ArgumentOutOfRangeException or OverflowException)
        {
            Problem(file, line, $"#if {expression}: {e.Message}");
            return 0;
        }
    }

    static readonly string[][] Binary =
    [
        ["||"], ["&&"], ["|"], ["^"], ["&"], ["==", "!="], ["<", ">", "<=", ">="], ["<<", ">>"], ["+", "-"], ["*", "/", "%"],
    ];

    int _depth;

    long Ternary(List<string> t, ref int pos)
    {
        if (++_depth > MaxRecursion) throw new FormatException($"nested deeper than {MaxRecursion}");
        long condition = BinaryLevel(t, ref pos, 0);
        if (pos < t.Count && t[pos] == "?")
        {
            pos++;
            long a = Ternary(t, ref pos);
            if (pos >= t.Count || t[pos] != ":") throw new FormatException("missing ':'");
            pos++;
            long b = Ternary(t, ref pos);
            return condition != 0 ? a : b;
        }
        return condition;
    }

    long BinaryLevel(List<string> t, ref int pos, int level)
    {
        if (level == Binary.Length) return Unary(t, ref pos);
        long left = BinaryLevel(t, ref pos, level + 1);
        while (pos < t.Count && Binary[level].Contains(t[pos]))
        {
            string op = t[pos++];
            long right = BinaryLevel(t, ref pos, level + 1);
            left = op switch
            {
                "||" => left != 0 || right != 0 ? 1 : 0,
                "&&" => left != 0 && right != 0 ? 1 : 0,
                "|" => left | right,
                "^" => left ^ right,
                "&" => left & right,
                "==" => left == right ? 1 : 0,
                "!=" => left != right ? 1 : 0,
                "<" => left < right ? 1 : 0,
                ">" => left > right ? 1 : 0,
                "<=" => left <= right ? 1 : 0,
                ">=" => left >= right ? 1 : 0,
                "<<" => left << (int)right,
                ">>" => left >> (int)right,
                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" => left / right,
                _ => left % right,
            };
        }
        return left;
    }

    long Unary(List<string> t, ref int pos)
    {
        if (++_depth > MaxRecursion * 32) throw new FormatException("expression too deep");
        if (pos >= t.Count) throw new FormatException("expression ends early");
        string token = t[pos++];
        switch (token)
        {
            case "!": return Unary(t, ref pos) == 0 ? 1 : 0;
            case "~": return ~Unary(t, ref pos);
            case "-": return -Unary(t, ref pos);
            case "+": return Unary(t, ref pos);
            case "(":
            {
                long v = Ternary(t, ref pos);
                if (pos >= t.Count || t[pos] != ")") throw new FormatException("missing ')'");
                pos++;
                return v;
            }
        }
        if (char.IsAsciiDigit(token[0])) return ParseInt(token);
        // An identifier left after expansion is not a macro: it counts as 0.
        if (IsIdent(token[0], true)) return 0;
        throw new FormatException($"unexpected '{token}'");
    }

    static long ParseInt(string token)
    {
        string s = token.TrimEnd('u', 'U', 'l', 'L');
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return long.Parse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (s.Contains('.') || s.Contains('e') || s.Contains('E'))
            return (long)double.Parse(s.TrimEnd('f', 'F'), CultureInfo.InvariantCulture);
        if (s.Length > 1 && s[0] == '0') return System.Convert.ToInt64(s, 8);
        return long.Parse(s, CultureInfo.InvariantCulture);
    }

    // ---- lexing ----

    static bool IsIdent(char c, bool first) => c == '_' || char.IsAsciiLetter(c) || (!first && char.IsAsciiDigit(c));
    static bool IsSpace(string t) => t.Length > 0 && char.IsWhiteSpace(t[0]);

    static readonly string[] Punctuators = ["##", "<<=", ">>=", "<<", ">>", "<=", ">=", "==", "!=", "&&", "||", "++", "--", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^="];

    /// <summary>Splits text into identifiers, numbers, strings, punctuation and runs of whitespace (kept as tokens).</summary>
    public static List<string> Lex(string s)
    {
        var tokens = new List<string>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            int start = i;
            if (char.IsWhiteSpace(c))
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }
            else if (IsIdent(c, true))
            {
                while (i < s.Length && IsIdent(s[i], false)) i++;
            }
            else if (char.IsAsciiDigit(c) || (c == '.' && i + 1 < s.Length && char.IsAsciiDigit(s[i + 1])))
            {
                // pp-number: digits, letters, dots, and signs after an exponent.
                i++;
                while (i < s.Length && (char.IsAsciiLetterOrDigit(s[i]) || s[i] == '.' || s[i] == '_'
                    || ((s[i] == '+' || s[i] == '-') && (s[i - 1] == 'e' || s[i - 1] == 'E') && !s[start..i].StartsWith("0x", StringComparison.OrdinalIgnoreCase))))
                    i++;
            }
            else if (c == '"')
            {
                i++;
                while (i < s.Length && s[i] != '"' && s[i] != '\n') i++;
                if (i < s.Length && s[i] == '"') i++;
            }
            else
            {
                string? p = Punctuators.FirstOrDefault(p => string.CompareOrdinal(s, i, p, 0, p.Length) == 0);
                i += p?.Length ?? 1;
            }
            tokens.Add(s[start..i]);
        }
        return tokens;
    }

    /// <summary>Removes comments (keeping line breaks) and joins lines continued with a backslash.</summary>
    public static string StripComments(string s)
    {
        var b = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n') i++;
                if (i < s.Length) b.Append('\n');
            }
            else if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                i += 2;
                b.Append(' ');
                while (i + 1 < s.Length && !(s[i] == '*' && s[i + 1] == '/'))
                {
                    if (s[i] == '\n') b.Append('\n');
                    i++;
                }
                i++;
            }
            else if (c == '"')
            {
                int end = s.IndexOfAny(['"', '\n'], i + 1);
                if (end < 0) end = s.Length - 1;
                b.Append(s, i, end - i + 1);
                i = end;
            }
            else if (c == '\\' && i + 1 < s.Length && (s[i + 1] == '\n' || (s[i + 1] == '\r' && i + 2 < s.Length && s[i + 2] == '\n')))
            {
                i += s[i + 1] == '\r' ? 2 : 1;
            }
            else if (c != '\r') b.Append(c);
        }
        return b.ToString();
    }

    static List<string> SplitLines(string s) => s.Split('\n').ToList();
}
