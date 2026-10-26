using System.Text.RegularExpressions;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// The text tokens an NWN client fills in itself: the server sends conversation and journal text as written (or as a
/// talk table reference), with the object the tokens describe and the custom token values set by SetCustomToken.
/// </summary>
public sealed partial class NwnTokens(INwnBridgeData data)
{
    /// <summary>What the tokens of one creature read: its names, race, classes, gender and alignment.</summary>
    public sealed class Who
    {
        public string FirstName = "", LastName = "", PlayerName = "", Subrace = "", Deity = "";
        public int Race = -1, Gender;
        public int GoodEvil = 50, LawChaos = 50;
        public List<(int Class, int Level)> Classes = [];
    }

    readonly Dictionary<uint, Who> _who = [];
    readonly Dictionary<int, string> _custom = [];

    /// <summary>The game clock, for the time tokens.</summary>
    public int Hour = 12, Minute, Month = 1, Year;
    public bool IsDay = true;

    public Who Of(uint id)
    {
        if (!_who.TryGetValue(id, out var w)) _who[id] = w = new Who();
        return w;
    }

    public void SetCustom(int number, string value) => _custom[number] = value;

    public void Forget(uint id) => _who.Remove(id);

    [GeneratedRegex("<([^<>]+)>")]
    private static partial Regex TokenPattern();

    /// <summary>
    /// Replaces the tokens describing <paramref name="target"/>. With <paramref name="markup"/> the action, check and
    /// highlight spans become the client's [[action]] ... [[/]] tags; without, they are dropped (chat, journal).
    /// Unknown tokens stay as written, as the original shows them.
    /// </summary>
    public string Resolve(string text, uint target, bool markup = true)
    {
        if (text.IndexOf('<') < 0) return text;
        var w = _who.GetValueOrDefault(target) ?? new Who();
        bool female = w.Gender == 1;
        string Cell(string table, int row, string column) =>
            row >= 0 && uint.TryParse(data.TwoDA(table, row, column), out var strRef) ? data.Text(strRef, 0) : "";
        // The first class taken, as the original does.
        string ClassName(string column) => w.Classes.Count > 0 ? Cell("classes", w.Classes[0].Class, column) : "";
        string RaceName(string column) => Cell("racialtypes", w.Race, column);
        return TokenPattern().Replace(text, m =>
        {
            var t = m.Groups[1].Value;
            if (t.StartsWith("CUSTOM", StringComparison.OrdinalIgnoreCase) && int.TryParse(t[6..], out var n))
                return _custom.GetValueOrDefault(n, "");
            string Cap(string v) => char.IsUpper(t[0]) && v.Length > 0 ? char.ToUpperInvariant(v[0]) + v[1..] : v;
            return t switch
            {
                "FirstName" => w.FirstName,
                "LastName" => w.LastName,
                "FullName" => $"{w.FirstName} {w.LastName}".Trim(),
                "PlayerName" => w.PlayerName,
                "Class" => ClassName("Name"),
                "class" => ClassName("Lower"),
                "Classes" => ClassName("Plural"),
                "classes" => ClassName("Plural").ToLowerInvariant(),
                "Race" => RaceName("ConverName"),
                "race" => RaceName("ConverNameLower"),
                "Races" => RaceName("NamePlural"),
                "races" => RaceName("NamePlural").ToLowerInvariant(),
                "Subrace" or "subrace" => w.Subrace,
                "Deity" or "deity" => w.Deity,
                "Level" or "level" => w.Classes.Sum(c => c.Level).ToString(),
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
                "Day/Night" or "day/night" => Cap(IsDay ? "day" : "night"),
                "QuarterDay" or "quarterday" => Cap(Hour switch { < 6 => "night", < 12 => "morning", < 18 => "afternoon", _ => "evening" }),
                "GameTime" or "gametime" => $"{Hour:00}:{Minute:00}",
                "GameYear" or "gameyear" => Year.ToString(),
                "GameMonth" or "gamemonth" => Month.ToString(),
                "Alignment" or "alignment" => Cap(Alignment(w)),
                "Good/Evil" or "good/evil" => Cap(w.GoodEvil >= 70 ? "good" : w.GoodEvil <= 30 ? "evil" : "neutral"),
                "Lawful/Chaotic" or "lawful/chaotic" => Cap(w.LawChaos >= 70 ? "lawful" : w.LawChaos <= 30 ? "chaotic" : "neutral"),
                "Law/Chaos" or "law/chaos" => Cap(w.LawChaos >= 70 ? "law" : w.LawChaos <= 30 ? "chaos" : "neutrality"),
                "StartAction" => markup ? "[[action]]" : "",
                "StartCheck" => markup ? "[[check]]" : "",
                "StartHighlight" => markup ? "[[highlight]]" : "",
                "/Start" => markup ? "[[/]]" : "",
                _ => m.Value,
            };
        });
    }

    static string Alignment(Who w)
    {
        string law = w.LawChaos >= 70 ? "lawful" : w.LawChaos <= 30 ? "chaotic" : "neutral";
        string good = w.GoodEvil >= 70 ? "good" : w.GoodEvil <= 30 ? "evil" : "neutral";
        return law == "neutral" && good == "neutral" ? "true neutral" : $"{law} {good}";
    }
}
