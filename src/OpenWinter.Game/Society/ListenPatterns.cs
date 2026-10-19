using System.Text;

namespace OpenWinter.Game.Society;

/// <summary>
/// The listen pattern language used by SetListenPattern and TestStringAgainstPattern.
/// A pattern must match the whole string (letters in any case, as in the original). Tokens:
/// <list type="bullet">
/// <item><c>**</c> any text, possibly empty</item>
/// <item><c>*w</c> one or more whitespace characters</item>
/// <item><c>*n</c> one or more digits (0-9)</item>
/// <item><c>*a</c> one or more letters (A-Z, a-z)</item>
/// <item><c>*p</c> one or more sentence marks (. ! ?)</item>
/// <item><c>(a|b)</c> alternatives (each may contain wildcards); <c>|</c> at the top level splits the whole pattern</item>
/// <item>anything else is literal text; a <c>*</c> not followed by one of the letters above, or an empty
/// pattern or alternative, never matches</item>
/// </list>
/// Each top-level piece of the matching alternative (a literal run, a wildcard or a group) yields
/// one matched substring, in order; GetMatchedSubstring(n) reads them.
/// </summary>
public static class ListenPatterns
{
    abstract record Node;
    sealed record Literal(string Text) : Node;
    sealed record Wild(char Kind) : Node;
    sealed record Group(List<List<Node>> Alternatives) : Node;

    static readonly Dictionary<string, List<List<Node>>> _cache = new(StringComparer.Ordinal);

    /// <summary>Matches <paramref name="text"/> against <paramref name="pattern"/>; returns the matched substrings, or null when it doesn't match.</summary>
    public static List<string>? Match(string pattern, string text)
    {
        List<List<Node>> top;
        lock (_cache)
        {
            if (!_cache.TryGetValue(pattern, out top!))
            {
                int pos = 0;
                top = ParseAlternatives(pattern, ref pos, topLevel: true);
                if (_cache.Count > 4096) _cache.Clear();
                _cache[pattern] = top;
            }
        }
        foreach (var seq in top)
        {
            var captures = new List<string>();
            if (MatchTop(seq, 0, text, 0, captures)) return captures;
        }
        return null;
    }

    public static bool IsMatch(string pattern, string text) => Match(pattern, text) != null;

    // ---- parsing ----

    static List<List<Node>> ParseAlternatives(string p, ref int pos, bool topLevel)
    {
        var alts = new List<List<Node>> { new() };
        var literal = new StringBuilder();
        void Flush() { if (literal.Length > 0) { alts[^1].Add(new Literal(literal.ToString())); literal.Clear(); } }
        while (pos < p.Length)
        {
            char ch = p[pos];
            if (ch == '*')
            {
                Flush();
                char kind = pos + 1 < p.Length && p[pos + 1] is '*' or 'w' or 'n' or 'a' or 'p' or 'W' or 'N' or 'A' or 'P' ? char.ToLowerInvariant(p[pos + 1]) : '?';
                alts[^1].Add(new Wild(kind)); // '?' never matches
                pos += kind == '?' && pos + 1 == p.Length ? 1 : 2;
            }
            else if (ch == '(')
            {
                Flush();
                pos++;
                var inner = ParseAlternatives(p, ref pos, topLevel: false);
                alts[^1].Add(new Group(inner));
            }
            else if (ch == ')' && !topLevel)
            {
                pos++;
                Flush();
                return alts;
            }
            else if (ch == '|')
            {
                Flush();
                alts.Add(new List<Node>());
                pos++;
            }
            else
            {
                literal.Append(ch);
                pos++;
            }
        }
        Flush();
        return alts;
    }

    // ---- matching (backtracking) ----

    /// <summary>Matches the top-level sequence, recording one capture per node.</summary>
    static bool MatchTop(List<Node> seq, int index, string s, int pos, List<string> captures)
    {
        if (seq.Count == 0) return false;
        if (index == seq.Count) return pos == s.Length;
        foreach (int end in Ends(seq[index], s, pos))
        {
            captures.Add(s[pos..end]);
            if (MatchTop(seq, index + 1, s, end, captures)) return true;
            captures.RemoveAt(captures.Count - 1);
        }
        return false;
    }

    /// <summary>Every position where <paramref name="node"/> can end when it starts at <paramref name="pos"/>, in preference order.</summary>
    static IEnumerable<int> Ends(Node node, string s, int pos)
    {
        switch (node)
        {
            case Literal l:
                if (pos + l.Text.Length <= s.Length && string.Compare(s, pos, l.Text, 0, l.Text.Length, StringComparison.OrdinalIgnoreCase) == 0) yield return pos + l.Text.Length;
                break;
            case Wild { Kind: '*' }:
                // Shortest first, so literals that follow split at their first occurrence.
                for (int e = pos; e <= s.Length; e++) yield return e;
                break;
            case Wild w:
            {
                int max = pos;
                while (max < s.Length && Is(w.Kind, s[max])) max++;
                // Longest first: "*n" takes the whole number.
                for (int e = max; e > pos; e--) yield return e;
                break;
            }
            case Group g:
                foreach (var alt in g.Alternatives.Where(a => a.Count > 0))
                    foreach (int e in SeqEnds(alt, 0, s, pos))
                        yield return e;
                break;
        }
    }

    static IEnumerable<int> SeqEnds(List<Node> seq, int index, string s, int pos)
    {
        if (index == seq.Count) { yield return pos; yield break; }
        foreach (int mid in Ends(seq[index], s, pos))
            foreach (int e in SeqEnds(seq, index + 1, s, mid))
                yield return e;
    }

    static bool Is(char kind, char c) => kind switch
    {
        'w' => char.IsWhiteSpace(c),
        'n' => char.IsAsciiDigit(c),
        'a' => char.IsAsciiLetter(c),
        'p' => c is '.' or '!' or '?',
        _ => false,
    };
}
