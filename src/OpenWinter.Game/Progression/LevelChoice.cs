using System.Text.Json;

namespace OpenWinter.Game.Progression;

/// <summary>
/// What a player picks for one level: the class, the ability increase (every fourth level),
/// skill ranks, feats, new spells and the first-level class options (domains, school,
/// familiar or companion). Hit points are not chosen; the rules compute them.
/// </summary>
public sealed class LevelChoice
{
    public int Class = -1;
    /// <summary>ABILITY_* raised this level (every fourth character level), or -1.</summary>
    public int Ability = -1;
    /// <summary>Ranks added per skill (skills.2da row → ranks).</summary>
    public Dictionary<int, int> Skills = new();
    /// <summary>Chosen feats (general and class bonus feats together; granted feats are added by the rules).</summary>
    public List<int> Feats = new();
    /// <summary>New known spells by spell level (spontaneous casters' picks, wizard spellbook additions).</summary>
    public Dictionary<int, List<int>> Spells = new();
    public int Domain1 = -1, Domain2 = -1, School = -1;
    public int Familiar = -1, Companion = -1;
    public string FamiliarName = "", CompanionName = "";
    /// <summary>packages.2da row the build follows (recorded as StartingPackage at creation), or -1.</summary>
    public int Package = -1;

    public int SkillRanks(int skill) => Skills.GetValueOrDefault(skill);

    public static LevelChoice FromJson(JsonElement e)
    {
        var c = new LevelChoice();
        if (e.ValueKind != JsonValueKind.Object) return c;
        c.Class = Int(e, "class", -1);
        c.Ability = Int(e, "ability", -1);
        c.Domain1 = Int(e, "domain1", -1);
        c.Domain2 = Int(e, "domain2", -1);
        c.School = Int(e, "school", -1);
        c.Familiar = Int(e, "familiar", -1);
        c.Companion = Int(e, "companion", -1);
        c.Package = Int(e, "package", -1);
        c.FamiliarName = Str(e, "familiarName");
        c.CompanionName = Str(e, "companionName");
        if (e.TryGetProperty("skills", out var skills))
        {
            if (skills.ValueKind == JsonValueKind.Array)
            {
                int i = 0;
                foreach (var v in skills.EnumerateArray()) { if (v.TryGetInt32(out int n) && n != 0) c.Skills[i] = n; i++; }
            }
            else if (skills.ValueKind == JsonValueKind.Object)
                foreach (var p in skills.EnumerateObject())
                    if (int.TryParse(p.Name, out int s) && p.Value.TryGetInt32(out int n) && n != 0) c.Skills[s] = n;
        }
        if (e.TryGetProperty("feats", out var feats) && feats.ValueKind == JsonValueKind.Array)
            foreach (var v in feats.EnumerateArray()) if (v.TryGetInt32(out int f)) c.Feats.Add(f);
        if (e.TryGetProperty("spells", out var spells))
        {
            if (spells.ValueKind == JsonValueKind.Array)
            {
                int lvl = 0;
                foreach (var list in spells.EnumerateArray()) { AddSpells(c, lvl++, list); }
            }
            else if (spells.ValueKind == JsonValueKind.Object)
                foreach (var p in spells.EnumerateObject())
                    if (int.TryParse(p.Name, out int lvl)) AddSpells(c, lvl, p.Value);
        }
        return c;
    }

    static void AddSpells(LevelChoice c, int level, JsonElement list)
    {
        if (list.ValueKind != JsonValueKind.Array || level < 0 || level > 9) return;
        var l = new List<int>();
        foreach (var v in list.EnumerateArray()) if (v.TryGetInt32(out int s)) l.Add(s);
        if (l.Count > 0) c.Spells[level] = l;
    }

    internal static int Int(JsonElement e, string name, int fallback) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.TryGetInt32(out int i) ? i : fallback;
    internal static string Str(JsonElement e, string name) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    /// <summary>The JSON shape <see cref="FromJson"/> reads (clients send this).</summary>
    public object ToJson() => new
    {
        @class = Class, ability = Ability,
        skills = Skills.ToDictionary(k => k.Key.ToString(), k => k.Value),
        feats = Feats,
        spells = Spells.ToDictionary(k => k.Key.ToString(), k => k.Value),
        domain1 = Domain1, domain2 = Domain2, school = School,
        familiar = Familiar, familiarName = FamiliarName, companion = Companion, companionName = CompanionName,
        package = Package,
    };
}

/// <summary>Everything chosen when creating a character: identity, looks, starting abilities and the first level.</summary>
public sealed class NewCharacter
{
    public string FirstName = "", LastName = "";
    public int Gender, Race = -1;
    public string Subrace = "", Deity = "", Description = "";
    public int Age = -1;
    /// <summary>portraits.2da row; <see cref="Portrait"/> is the resref (po_…) when a custom one is used.</summary>
    public int PortraitId = -1;
    public string Portrait = "";
    public int LawChaos = 50, GoodEvil = 50;
    /// <summary>Point-buy scores before racial adjustments (STR, DEX, CON, INT, WIS, CHA).</summary>
    public int[] Abilities = { 8, 8, 8, 8, 8, 8 };
    /// <summary>appearance.2da row; -1 uses the race's (racialtypes.2da Appearance).</summary>
    public int Appearance = -1;
    public int Phenotype;
    /// <summary>Body part numbers in CreatureFactory.BodyPartFields order (head first); empty = defaults.</summary>
    public int[] BodyParts = Array.Empty<int>();
    /// <summary>Skin, hair, tattoo 1, tattoo 2 palette indices.</summary>
    public int[] Colors = { 0, 0, 0, 0 };
    public int Wings, Tail;
    public int SoundSet = -1;
    public LevelChoice Level = new();

    public static NewCharacter FromJson(JsonElement e)
    {
        var n = new NewCharacter();
        if (e.ValueKind != JsonValueKind.Object) return n;
        n.FirstName = LevelChoice.Str(e, "firstName").Trim();
        n.LastName = LevelChoice.Str(e, "lastName").Trim();
        n.Gender = LevelChoice.Int(e, "gender", 0);
        n.Race = LevelChoice.Int(e, "race", -1);
        n.Subrace = LevelChoice.Str(e, "subrace");
        n.Deity = LevelChoice.Str(e, "deity");
        n.Description = LevelChoice.Str(e, "description");
        n.Age = LevelChoice.Int(e, "age", -1);
        n.PortraitId = LevelChoice.Int(e, "portraitId", -1);
        n.Portrait = LevelChoice.Str(e, "portrait");
        n.LawChaos = LevelChoice.Int(e, "lawChaos", 50);
        n.GoodEvil = LevelChoice.Int(e, "goodEvil", 50);
        n.Appearance = LevelChoice.Int(e, "appearance", -1);
        n.Phenotype = LevelChoice.Int(e, "phenotype", 0);
        n.Wings = LevelChoice.Int(e, "wings", 0);
        n.Tail = LevelChoice.Int(e, "tail", 0);
        n.SoundSet = LevelChoice.Int(e, "soundSet", -1);
        if (Ints(e, "abilities") is { Length: 6 } ab) n.Abilities = ab;
        if (Ints(e, "bodyParts") is { } parts) n.BodyParts = parts;
        if (Ints(e, "colors") is { Length: 4 } col) n.Colors = col;
        if (e.TryGetProperty("level", out var lvl)) n.Level = LevelChoice.FromJson(lvl);
        return n;
    }

    static int[]? Ints(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array) return null;
        return v.EnumerateArray().Select(x => x.TryGetInt32(out int i) ? i : 0).ToArray();
    }

    public string FullName => string.Join(" ", new[] { FirstName, LastName }.Where(s => s.Length > 0));

    /// <summary>The JSON shape <see cref="FromJson"/> reads (clients send this).</summary>
    public object ToJson() => new
    {
        firstName = FirstName, lastName = LastName, gender = Gender, race = Race, subrace = Subrace, deity = Deity,
        description = Description, age = Age, portraitId = PortraitId, portrait = Portrait, lawChaos = LawChaos, goodEvil = GoodEvil,
        abilities = Abilities, appearance = Appearance, phenotype = Phenotype, bodyParts = BodyParts, colors = Colors,
        wings = Wings, tail = Tail, soundSet = SoundSet, level = Level.ToJson(),
    };
}

/// <summary>
/// One broken rule: a short code clients can key on and a message for the player, and where known the character
/// level, feat, skill or spell it concerns.
/// </summary>
public readonly record struct RuleError(string Code, string Message, int Level = -1, int Feat = -1, int Skill = -1, int Spell = -1)
{
    public override string ToString() => $"{Code}: {Message}";
}
