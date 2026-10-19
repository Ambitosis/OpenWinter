using System.Globalization;
using System.Text;

namespace OpenWinter.Game.Scripting.Compiler;

public enum TokenKind { Identifier, Keyword, Int, Float, String, Operator, Include, Define, End }

public readonly record struct Token(TokenKind Kind, string Text, int Line, string File)
{
    public int IntValue { get; init; }
    public float FloatValue { get; init; }
    public override string ToString() => Kind == TokenKind.End ? "end of file" : Text;
}

/// <summary>A compile error, reported the way the original compiler words it.</summary>
public sealed class NssCompileException : Exception
{
    public string File { get; }
    public int Line { get; }
    public NssCompileException(string file, int line, string message) : base(message) { File = file; Line = line; }
}

/// <summary>Splits NWScript source into tokens (comments dropped, #include/#define kept as tokens).</summary>
public static class NssLexer
{
    static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "int", "float", "string", "object", "vector", "void", "struct", "action", "const",
        "if", "else", "while", "do", "for", "switch", "case", "default", "break", "continue", "return",
    };

    // Longest first, so ">>>=" wins over ">>>" over ">>" over ">".
    static readonly string[] Operators =
    {
        ">>>=", ">>>", "<<=", ">>=", "==", "!=", "<=", ">=", "&&", "||", "++", "--", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=",
        "<<", ">>", "+", "-", "*", "/", "%", "=", "<", ">", "!", "~", "&", "|", "^", "?", ":", ";", ",", ".", "(", ")", "{", "}", "[", "]",
    };

    public static List<Token> Tokenize(string src, string file, IEnumerable<string> engineTypes)
    {
        var keywords = new HashSet<string>(Keywords, StringComparer.Ordinal);
        foreach (var t in engineTypes) keywords.Add(t);
        var list = new List<Token>();
        int i = 0, line = 1;
        while (i < src.Length)
        {
            char c = src[i];
            if (c == '\n') { line++; i++; continue; }
            // A stray backslash (a line continuation) is ignored, as the original compiler does.
            if (char.IsWhiteSpace(c) || c == '\\') { i++; continue; }
            if (c == '/' && i + 1 < src.Length && src[i + 1] == '/')
            {
                while (i < src.Length && src[i] != '\n') i++;
                continue;
            }
            if (c == '/' && i + 1 < src.Length && src[i + 1] == '*')
            {
                i += 2;
                while (i < src.Length && !(src[i] == '*' && i + 1 < src.Length && src[i + 1] == '/')) { if (src[i] == '\n') line++; i++; }
                i += 2;
                continue;
            }
            if (c == '#')
            {
                int start = i;
                while (i < src.Length && src[i] != '\n') i++;
                var directive = src[start..i].Trim();
                if (directive.StartsWith("#include", StringComparison.Ordinal))
                {
                    int q1 = directive.IndexOf('"'), q2 = directive.LastIndexOf('"');
                    if (q1 < 0 || q2 <= q1) throw new NssCompileException(file, line, "INVALID INCLUDE DIRECTIVE");
                    list.Add(new Token(TokenKind.Include, directive[(q1 + 1)..q2], line, file));
                }
                else if (directive.StartsWith("#define", StringComparison.Ordinal))
                    list.Add(new Token(TokenKind.Define, directive[7..].Trim(), line, file));
                continue;
            }
            if (c == '"')
            {
                var sb = new StringBuilder();
                i++;
                while (true)
                {
                    if (i >= src.Length || src[i] == '\n') throw new NssCompileException(file, line, "UNEXPECTED END OF FILE");
                    char ch = src[i++];
                    if (ch == '"') break;
                    if (ch == '\\' && i < src.Length)
                    {
                        char e = src[i++];
                        switch (e)
                        {
                            case 'n': sb.Append('\n'); break;
                            case 'x' when i + 1 < src.Length && char.IsAsciiHexDigit(src[i]) && char.IsAsciiHexDigit(src[i + 1]):
                                sb.Append(Aurora.Text.Windows1252.GetString(new[] { byte.Parse(src.AsSpan(i, 2), NumberStyles.HexNumber) }));
                                i += 2;
                                break;
                            default: sb.Append(e); break;
                        }
                        continue;
                    }
                    sb.Append(ch);
                }
                list.Add(new Token(TokenKind.String, sb.ToString(), line, file));
                continue;
            }
            if (char.IsAsciiDigit(c) || c == '.' && i + 1 < src.Length && char.IsAsciiDigit(src[i + 1]))
            {
                list.Add(Number(src, ref i, line, file));
                continue;
            }
            if (char.IsAsciiLetter(c) || c == '_')
            {
                int start = i;
                while (i < src.Length && (char.IsAsciiLetterOrDigit(src[i]) || src[i] == '_')) i++;
                var word = src[start..i];
                list.Add(new Token(keywords.Contains(word) ? TokenKind.Keyword : TokenKind.Identifier, word, line, file));
                continue;
            }
            string? op = null;
            foreach (var o in Operators)
                if (string.CompareOrdinal(src, i, o, 0, o.Length) == 0) { op = o; break; }
            if (op == null) throw new NssCompileException(file, line, "UNEXPECTED CHARACTER");
            list.Add(new Token(TokenKind.Operator, op, line, file));
            i += op.Length;
        }
        list.Add(new Token(TokenKind.End, "", line, file));
        return list;
    }

    static Token Number(string src, ref int i, int line, string file)
    {
        int start = i;
        if (src[i] == '0' && i + 1 < src.Length && (src[i + 1] | 0x20) is 'x' or 'b' or 'o')
        {
            char kind = (char)(src[i + 1] | 0x20);
            i += 2;
            int ds = i;
            while (i < src.Length && char.IsAsciiHexDigit(src[i])) i++;
            var digits = src[ds..i];
            long v = 0;
            int radix = kind == 'x' ? 16 : kind == 'b' ? 2 : 8;
            foreach (char d in digits)
            {
                int n = Convert.ToInt32(d.ToString(), 16);
                if (n >= radix) throw new NssCompileException(file, line, "INVALID INTEGER CONSTANT");
                v = unchecked(v * radix + n);
            }
            return new Token(TokenKind.Int, src[start..i], line, file) { IntValue = unchecked((int)v) };
        }
        while (i < src.Length && char.IsAsciiDigit(src[i])) i++;
        bool isFloat = false;
        if (i < src.Length && src[i] == '.')
        {
            isFloat = true;
            i++;
            while (i < src.Length && char.IsAsciiDigit(src[i])) i++;
        }
        if (i < src.Length && (src[i] | 0x20) == 'e' && i + 1 < src.Length && (char.IsAsciiDigit(src[i + 1]) || src[i + 1] is '+' or '-'))
        {
            isFloat = true;
            i += 2;
            while (i < src.Length && char.IsAsciiDigit(src[i])) i++;
        }
        string text = src[start..i];
        if (i < src.Length && (src[i] | 0x20) == 'f') { isFloat = true; i++; }
        if (isFloat)
            return new Token(TokenKind.Float, text, line, file) { FloatValue = text.Contains('e') || text.Contains('E') ? float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) : FloatLiteral(text) };
        long iv = long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var l) ? l : long.MaxValue;
        return new Token(TokenKind.Int, text, line, file) { IntValue = unchecked((int)iv) };
    }

    /// <summary>
    /// The original compiler's float literal value: digits accumulated in single precision, each fraction digit
    /// weighted by a scale that starts at 0.1 and is divided by 10 per digit (so 0.05 is 0.049999997).
    /// </summary>
    static float FloatLiteral(string text)
    {
        float f = 0;
        int i = 0;
        for (; i < text.Length && text[i] != '.'; i++) f = f * 10f + (text[i] - '0');
        float scale = 0.1f;
        for (i++; i < text.Length; i++)
        {
            f += (text[i] - '0') * scale;
            scale /= 10f;
        }
        return f;
    }
}
