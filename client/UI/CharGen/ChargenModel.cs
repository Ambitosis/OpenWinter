using System.Text.Json;
using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI.CharGen;

/// <summary>One named row of the server's chargen data (race, class, skill, feat, spell, ...).</summary>
public sealed record ChargenEntry(int Id, string Name, string Icon, string Description, JsonElement Raw)
{
    public int Int(string name, int fallback = 0) => Raw.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : fallback;
    public bool Bool(string name) => Raw.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;
    public string Str(string name) => Raw.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    public int[] Ints(string name) => Raw.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.TryGetInt32(out int i) ? i : 0).ToArray() : Array.Empty<int>();
    public bool[] Bools(string name) => Raw.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.True).ToArray() : Array.Empty<bool>();
}

/// <summary>The server's "chargen-data": everything the creation wizard and level-up window list, resolved through the module's haks.</summary>
public sealed class ChargenModel
{
    public static readonly string[] AbilityNames = { "Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma" };
    public static readonly string[] AbilityShort = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };

    public readonly Dictionary<int, ChargenEntry> Races = new(), Classes = new(), Skills = new(), Feats = new(), Spells = new(),
        Packages = new(), Domains = new(), Schools = new(), Familiars = new(), Companions = new(), Phenotypes = new(), SoundSets = new(), MasterFeats = new();
    public readonly List<(int LawChaos, int GoodEvil, string Name)> Alignments = new();
    public int AbilityMin = 8, AbilityMax = 18, PrimaryMin = 8;
    public int[] Cost = Array.Empty<int>();
    public bool CustomPortraits, Recommended;
    public int Wings, Tails;
    readonly JsonDocument _doc;

    ChargenModel(JsonDocument doc) { _doc = doc; }

    public static ChargenModel Parse(string json)
    {
        var doc = JsonDocument.Parse(json);
        var m = new ChargenModel(doc);
        var root = doc.RootElement;
        void Fill(string name, Dictionary<int, ChargenEntry> into)
        {
            if (!root.TryGetProperty(name, out var arr)) return;
            foreach (var e in arr.EnumerateArray())
            {
                int id = e.GetProperty("id").GetInt32();
                into[id] = new ChargenEntry(id, S(e, "name"), S(e, "icon"), S(e, "description"), e);
            }
        }
        Fill("races", m.Races); Fill("classes", m.Classes); Fill("skills", m.Skills); Fill("feats", m.Feats); Fill("spells", m.Spells);
        Fill("packages", m.Packages); Fill("domains", m.Domains); Fill("schools", m.Schools); Fill("familiars", m.Familiars);
        Fill("companions", m.Companions); Fill("phenotypes", m.Phenotypes); Fill("soundsets", m.SoundSets); Fill("masterfeats", m.MasterFeats);
        foreach (var a in root.GetProperty("alignments").EnumerateArray())
            m.Alignments.Add((a.GetProperty("lawChaos").GetInt32(), a.GetProperty("goodEvil").GetInt32(), S(a, "name")));
        var pb = root.GetProperty("pointBuy");
        m.AbilityMin = pb.GetProperty("min").GetInt32();
        m.AbilityMax = pb.GetProperty("max").GetInt32();
        m.PrimaryMin = pb.GetProperty("primaryMin").GetInt32();
        m.Cost = pb.GetProperty("cost").EnumerateArray().Select(x => x.GetInt32()).ToArray();
        m.CustomPortraits = root.TryGetProperty("customPortraits", out var cp) && cp.ValueKind == JsonValueKind.True;
        m.Recommended = !root.TryGetProperty("recommended", out var rec) || rec.ValueKind == JsonValueKind.True;
        m.Wings = root.TryGetProperty("wings", out var wv) ? wv.GetInt32() : 0;
        m.Tails = root.TryGetProperty("tails", out var tv) ? tv.GetInt32() : 0;
        return m;
    }

    /// <summary>A top-level field of the chargen data, as sent.</summary>
    public JsonElement? Raw(string name) => _doc.RootElement.TryGetProperty(name, out var v) ? v : null;

    /// <summary>A string field with NWN colour tokens removed.</summary>
    static string S(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? Kit.NwnText.Strip(v.GetString() ?? "") : "";

    /// <summary>Point-buy cost of a score (the server's table).</summary>
    public int CostOf(int score) => score <= AbilityMin ? 0 : Cost[Math.Min(score - AbilityMin, Cost.Length - 1)];

    public string FeatName(int id) => Feats.TryGetValue(id, out var f) ? f.Name : $"Feat {id}";
    public string SpellName(int id) => Spells.TryGetValue(id, out var s) ? s.Name : $"Spell {id}";
    public string SkillName(int id) => Skills.TryGetValue(id, out var s) ? s.Name : $"Skill {id}";
    public string ClassName(int id) => Classes.TryGetValue(id, out var c) ? c.Name : $"Class {id}";

    public static string Signed(int v) => v >= 0 ? $"+{v}" : v.ToString();
    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ClientContent, Dictionary<string, Texture2D?>> Cache = new();

    static Dictionary<string, Texture2D?> CacheFor(ClientContent content) => Cache.GetValue(content, _ => new Dictionary<string, Texture2D?>());

    /// <summary>
    /// A portrait texture (size letter h/l/m/s/t) cropped to the picture (portraits fill the top 100 of
    /// 128 rows) and turned upright when stored bottom-up. Cached per resref and size.
    /// </summary>
    public static Texture2D? Portrait(ClientContent? content, string resref, string size = "m")
    {
        if (content == null || resref.Length == 0) return null;
        var cache = CacheFor(content);
        string key = "portrait:" + resref + ":" + size;
        if (cache.TryGetValue(key, out var hit)) return hit;
        var tex = content.Textures.Get(resref + size) ?? content.Textures.Get(resref + "m") ?? content.Textures.Get(resref + "l");
        Texture2D? result = null;
        if (tex != null)
        {
            var s = tex.Texture.GetSize();
            float h = s.Y * 100f / 128f;
            if (!tex.FlipV) result = new AtlasTexture { Atlas = tex.Texture, Region = new Rect2(0, 0, s.X, h) };
            else if (tex.Texture.GetImage() is { } img)
            {
                if (img.IsCompressed()) img.Decompress();
                img.FlipY();
                result = ImageTexture.CreateFromImage(img.GetRegion(new Rect2I(0, 0, img.GetWidth(), (int)(img.GetHeight() * 100f / 128f))));
            }
        }
        return cache[key] = result;
    }

    /// <summary>An icon texture, flipped upright when stored bottom-up. Cached per resref.</summary>
    public static Texture2D? Icon(ClientContent? content, string resref)
    {
        if (content == null || resref.Length == 0) return null;
        var cache = CacheFor(content);
        if (cache.TryGetValue("icon:" + resref, out var hit)) return hit;
        Texture2D? result = null;
        if (content.Textures.Get(resref) is { } tex)
        {
            result = tex.Texture;
            if (tex.FlipV && tex.Texture.GetImage() is { } img)
            {
                if (img.IsCompressed()) img.Decompress();
                img.FlipY();
                result = ImageTexture.CreateFromImage(img);
            }
        }
        return cache["icon:" + resref] = result;
    }

    /// <summary>
    /// How a familiar or companion looks (chargen-data "appearance": its level-1 blueprint), for the
    /// 3D preview; null when the module has no blueprint for it.
    /// </summary>
    public static OpenWinter.Net.Appearance? AssociateLook(ChargenEntry? e)
    {
        if (e == null || !e.Raw.TryGetProperty("appearance", out var a) || a.ValueKind != JsonValueKind.Object) return null;
        int I(string n) => a.TryGetProperty(n, out var v) && v.TryGetInt32(out int i) ? i : 0;
        int[] Arr(string n) => a.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.Array ? v.EnumerateArray().Select(x => x.TryGetInt32(out int i) ? i : 0).ToArray() : Array.Empty<int>();
        return new OpenWinter.Net.Appearance
        {
            Type = I("type"), Gender = (byte)I("gender"), Phenotype = (byte)I("phenotype"),
            BodyParts = Arr("bodyParts").Select(x => (ushort)Math.Max(0, x)).ToArray(),
            Colors = Arr("colors").Select(x => (byte)Math.Clamp(x, 0, 255)).ToArray(),
            Tail = I("tail"), Wings = I("wings"),
        };
    }
}

/// <summary>The server's level plan ("chargen-plan" / "levelup-plan"): options for each step and what's still wrong.</summary>
public sealed class LevelPlanView
{
    public bool Ok;
    public List<(string Code, string Message)> Errors = new();
    public int Class = -1, NewLevel, NewClassLevel, HitDie;
    public bool AbilityIncrease, PickDomains, PickSchool, PickFamiliar, PickCompanion;
    public int SkillAvailable, SkillCarried, SkillGained, SkillSpent, SkillLeft;
    public List<(int Id, int Cost, int Max, int Current, bool ClassSkill)> Skills = new();
    public int GeneralSlots, BonusSlots;
    public List<int> Granted = new(), GeneralChoices = new(), BonusChoices = new();
    public List<SpellPickView> Spells = new();
    public int[] Abilities = Array.Empty<int>();
    public int Bab, Fortitude, Reflex, Will;
    public JsonElement? Auto;

    public sealed class SpellPickView
    {
        public int Class, AnyLevel, MaxLevel;
        public Dictionary<int, int> PerLevel = new();
        public Dictionary<int, List<int>> Choices = new(), Granted = new();
        public bool Needed => AnyLevel > 0 || PerLevel.Values.Any(v => v > 0);
    }

    public bool HasSpells => Spells.Any(s => s.Needed);
    public bool HasOptions => PickDomains || PickSchool || PickFamiliar || PickCompanion;
    public IEnumerable<string> ErrorsFor(params string[] codes) => Errors.Where(e => codes.Contains(e.Code)).Select(e => e.Message);

    public static LevelPlanView Parse(JsonElement p)
    {
        var v = new LevelPlanView
        {
            Ok = p.GetProperty("ok").GetBoolean(),
            Class = p.GetProperty("class").GetInt32(),
            NewLevel = p.GetProperty("newLevel").GetInt32(),
            NewClassLevel = p.GetProperty("newClassLevel").GetInt32(),
            HitDie = p.GetProperty("hitDie").GetInt32(),
            AbilityIncrease = p.GetProperty("abilityIncrease").GetBoolean(),
            PickDomains = p.GetProperty("pickDomains").GetBoolean(),
            PickSchool = p.GetProperty("pickSchool").GetBoolean(),
            PickFamiliar = p.GetProperty("pickFamiliar").GetBoolean(),
            PickCompanion = p.GetProperty("pickCompanion").GetBoolean(),
        };
        foreach (var e in p.GetProperty("errors").EnumerateArray()) v.Errors.Add((e.GetProperty("code").GetString() ?? "", e.GetProperty("message").GetString() ?? ""));
        var sp = p.GetProperty("skillPoints");
        v.SkillAvailable = sp.GetProperty("available").GetInt32();
        v.SkillCarried = sp.GetProperty("carried").GetInt32();
        v.SkillGained = sp.GetProperty("gained").GetInt32();
        v.SkillSpent = sp.GetProperty("spent").GetInt32();
        v.SkillLeft = sp.GetProperty("left").GetInt32();
        foreach (var s in p.GetProperty("skills").EnumerateArray())
            v.Skills.Add((s.GetProperty("id").GetInt32(), s.GetProperty("cost").GetInt32(), s.GetProperty("max").GetInt32(), s.GetProperty("current").GetInt32(), s.GetProperty("classSkill").GetBoolean()));
        var f = p.GetProperty("feats");
        v.GeneralSlots = f.GetProperty("general").GetInt32();
        v.BonusSlots = f.GetProperty("bonus").GetInt32();
        v.Granted = Ints(f.GetProperty("granted"));
        v.GeneralChoices = Ints(f.GetProperty("generalChoices"));
        v.BonusChoices = Ints(f.GetProperty("bonusChoices"));
        foreach (var s in p.GetProperty("spells").EnumerateArray())
        {
            var pick = new SpellPickView { Class = s.GetProperty("class").GetInt32(), AnyLevel = s.GetProperty("anyLevel").GetInt32(), MaxLevel = s.GetProperty("maxLevel").GetInt32() };
            foreach (var kv in s.GetProperty("perLevel").EnumerateObject()) pick.PerLevel[int.Parse(kv.Name)] = kv.Value.GetInt32();
            foreach (var kv in s.GetProperty("choices").EnumerateObject()) pick.Choices[int.Parse(kv.Name)] = Ints(kv.Value);
            foreach (var kv in s.GetProperty("granted").EnumerateObject()) pick.Granted[int.Parse(kv.Name)] = Ints(kv.Value);
            v.Spells.Add(pick);
        }
        if (p.TryGetProperty("stats", out var st) && st.ValueKind == JsonValueKind.Object)
        {
            v.Abilities = Ints(st.GetProperty("abilities")).ToArray();
            v.Bab = st.GetProperty("bab").GetInt32();
            v.Fortitude = st.GetProperty("fortitude").GetInt32();
            v.Reflex = st.GetProperty("reflex").GetInt32();
            v.Will = st.GetProperty("will").GetInt32();
        }
        if (p.TryGetProperty("auto", out var auto) && auto.ValueKind == JsonValueKind.Object) v.Auto = auto.Clone();
        return v;
    }

    static List<int> Ints(JsonElement a) => a.EnumerateArray().Select(x => x.GetInt32()).ToList();
}
