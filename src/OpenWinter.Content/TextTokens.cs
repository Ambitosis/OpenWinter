using System.Text.RegularExpressions;

namespace OpenWinter.Content;

/// <summary>What the text tokens of conversations and the journal read about the creature they describe.</summary>
public interface ITokenSubject
{
    string FirstName { get; }
    string LastName { get; }
    string FullName { get; }
    string PlayerName { get; }
    /// <summary>A classes.2da name column (Name, Lower, Plural) of the first class taken, as text.</summary>
    string ClassName(string column);
    /// <summary>A racialtypes.2da name column (ConverName, ConverNameLower, NamePlural), as text.</summary>
    string RaceName(string column);
    string Subrace { get; }
    string Deity { get; }
    int Level { get; }
    bool Female { get; }
    int GoodEvil { get; }
    int LawChaos { get; }
    bool IsDay { get; }
    int Hour { get; }
    int Minute { get; }
    int Year { get; }
    int Month { get; }
    /// <summary>A CUSTOMnnn token's value (SetCustomToken); "" when unset.</summary>
    string Custom(int number);
}

/// <summary>
/// The original's text tokens: names, gendered word pairs, class and race (in the forms classes.2da and
/// racialtypes.2da give), alignment, time, CUSTOMnnn, and the action/check/highlight markup. Capitalised tokens give
/// capitalised words; unknown tokens stay as written.
/// </summary>
public static partial class TextTokens
{
    [GeneratedRegex("<([^<>]+)>")]
    private static partial Regex Pattern();

    /// <param name="markup">Keep the action/check/highlight spans as the client's [[action]] ... [[/]] tags; without,
    /// they are dropped (text with no conversation styling: chat, the journal).</param>
    public static string Replace(string text, ITokenSubject s, bool markup = true)
    {
        if (text.IndexOf('<') < 0) return text;
        bool female = s.Female;
        return Pattern().Replace(text, m =>
        {
            var t = m.Groups[1].Value;
            if (t.StartsWith("CUSTOM", StringComparison.OrdinalIgnoreCase) && int.TryParse(t[6..], out var n)) return s.Custom(n);
            string Cap(string v) => char.IsUpper(t[0]) && v.Length > 0 ? char.ToUpperInvariant(v[0]) + v[1..] : v;
            return t switch
            {
                "FirstName" => s.FirstName,
                "LastName" => s.LastName,
                "FullName" => s.FullName,
                "PlayerName" => s.PlayerName,
                "Class" => s.ClassName("Name"),
                "class" => s.ClassName("Lower"),
                "Classes" => s.ClassName("Plural"),
                "classes" => s.ClassName("Plural").ToLowerInvariant(),
                // racialtypes.2da's conversation forms: "Dwarven" / "dwarven", and the plural "Dwarves".
                "Race" => s.RaceName("ConverName"),
                "race" => s.RaceName("ConverNameLower"),
                "Races" => s.RaceName("NamePlural"),
                "races" => s.RaceName("NamePlural").ToLowerInvariant(),
                "Subrace" or "subrace" => s.Subrace,
                "Deity" or "deity" => s.Deity,
                "Level" or "level" => s.Level.ToString(),
                "Gender" or "gender" => Cap(female ? "female" : "male"),
                "Lord/Lady" or "lord/lady" => Cap(female ? "lady" : "lord"),
                "Sir/Madam" or "sir/madam" => Cap(female ? "madam" : "sir"),
                "Brother/Sister" or "brother/sister" => Cap(female ? "sister" : "brother"),
                "Boy/Girl" or "boy/girl" => Cap(female ? "girl" : "boy"),
                "Lad/Lass" or "lad/lass" => Cap(female ? "lass" : "lad"),
                "Mister/Missus" or "mister/missus" => Cap(female ? "missus" : "mister"),
                "Man/Woman" or "man/woman" => Cap(female ? "woman" : "man"),
                "Male/Female" or "male/female" => Cap(female ? "female" : "male"),
                "Master/Mistress" or "master/mistress" => Cap(female ? "mistress" : "master"),
                "King/Queen" or "king/queen" => Cap(female ? "queen" : "king"),
                "He/She" or "he/she" => Cap(female ? "she" : "he"),
                "Him/Her" or "him/her" => Cap(female ? "her" : "him"),
                "His/Her" or "his/her" => Cap(female ? "her" : "his"),
                "His/Hers" or "his/hers" => Cap(female ? "hers" : "his"),
                "Bitch/Bastard" or "bitch/bastard" => Cap(female ? "bitch" : "bastard"),
                "Day/Night" or "day/night" => Cap(s.IsDay ? "day" : "night"),
                "QuarterDay" or "quarterday" => Cap(s.Hour switch { < 6 => "night", < 12 => "morning", < 18 => "afternoon", _ => "evening" }),
                "GameTime" or "gametime" => $"{s.Hour:00}:{s.Minute:00}",
                "GameYear" or "gameyear" => s.Year.ToString(),
                "GameMonth" or "gamemonth" => s.Month.ToString(),
                "Alignment" or "alignment" => Cap(Alignment(s.GoodEvil, s.LawChaos)),
                "Good/Evil" or "good/evil" => Cap(s.GoodEvil >= 70 ? "good" : s.GoodEvil <= 30 ? "evil" : "neutral"),
                "Lawful/Chaotic" or "lawful/chaotic" => Cap(s.LawChaos >= 70 ? "lawful" : s.LawChaos <= 30 ? "chaotic" : "neutral"),
                "Law/Chaos" or "law/chaos" => Cap(s.LawChaos >= 70 ? "law" : s.LawChaos <= 30 ? "chaos" : "neutrality"),
                // Markup the client renders: kept as lightweight tags.
                "StartAction" => markup ? "[[action]]" : "",
                "StartCheck" => markup ? "[[check]]" : "",
                "StartHighlight" => markup ? "[[highlight]]" : "",
                "/Start" => markup ? "[[/]]" : "",
                _ => m.Value,
            };
        });
    }

    public static string Alignment(int goodEvil, int lawChaos)
    {
        string law = lawChaos >= 70 ? "lawful" : lawChaos <= 30 ? "chaotic" : "neutral";
        string good = goodEvil >= 70 ? "good" : goodEvil <= 30 ? "evil" : "neutral";
        return law == "neutral" && good == "neutral" ? "true neutral" : $"{law} {good}";
    }
}
