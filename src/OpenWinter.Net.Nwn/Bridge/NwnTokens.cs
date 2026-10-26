using OpenWinter.Content;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// The text tokens an NWN client fills in itself: the server sends conversation and journal text as written (or as a
/// talk table reference), with the object the tokens describe and the custom token values set by SetCustomToken.
/// </summary>
public sealed class NwnTokens(INwnBridgeData data)
{
    /// <summary>What the tokens of one creature read: its names, race, classes, gender and alignment.</summary>
    public sealed class Who
    {
        public string FirstName = "", LastName = "", PlayerName = "", Subrace = "", Deity = "";
        public int Race = -1, Gender;
        public int GoodEvil = 50, LawChaos = 50;
        public List<(int Class, int Level)> Classes = [];
        /// <summary>The creature's attributes (race, gender, alignment, classes) have arrived.</summary>
        public bool Known;
    }

    readonly INwnBridgeData _data = data;
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

    /// <summary>A creature left: its attributes go; a player's names (from the player list) stay.</summary>
    public void Forget(uint id)
    {
        if (!_who.TryGetValue(id, out var w)) return;
        if (w.PlayerName.Length == 0) _who.Remove(id);
        else (w.Known, w.Race, w.Classes) = (false, -1, []);
    }

    /// <summary>
    /// Replaces the tokens describing <paramref name="target"/> (<see cref="TextTokens"/>). Until the target's
    /// attributes have arrived, the gender words follow <paramref name="gender"/>, the gender the server looked the
    /// text up with.
    /// </summary>
    public string Resolve(string text, uint target, bool markup = true, int gender = 0) =>
        text.IndexOf('<') < 0 ? text : TextTokens.Replace(text, new Subject(this, _who.GetValueOrDefault(target) ?? new Who(), gender), markup);

    sealed class Subject(NwnTokens t, Who w, int gender) : ITokenSubject
    {
        string Cell(string table, int row, string column) =>
            row >= 0 && uint.TryParse(t._data.TwoDA(table, row, column), out var strRef) ? t._data.Text(strRef, 0) : "";
        public string FirstName => w.FirstName;
        public string LastName => w.LastName;
        public string FullName => $"{w.FirstName} {w.LastName}".Trim();
        public string PlayerName => w.PlayerName;
        // The first class taken, as the original does.
        public string ClassName(string column) => w.Classes.Count > 0 ? Cell("classes", w.Classes[0].Class, column) : "";
        public string RaceName(string column) => Cell("racialtypes", w.Race, column);
        public string Subrace => w.Subrace;
        public string Deity => w.Deity;
        public int Level => w.Classes.Sum(c => c.Level);
        public bool Female => (w.Known ? w.Gender : gender) == 1;
        public int GoodEvil => w.GoodEvil;
        public int LawChaos => w.LawChaos;
        public bool IsDay => t.IsDay;
        public int Hour => t.Hour;
        public int Minute => t.Minute;
        public int Year => t.Year;
        public int Month => t.Month;
        public string Custom(int number) => t._custom.GetValueOrDefault(number, "");
    }
}
