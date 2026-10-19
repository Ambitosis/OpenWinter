using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Progression;

/// <summary>Result of building a new character: the BIC (when legal), the first level's plan and every broken rule.</summary>
public sealed record CharacterBuildResult(GffStruct? Bic, LevelPlan Plan, List<RuleError> Errors)
{
    public bool Ok => Errors.Count == 0 && Bic != null;
}

/// <summary>
/// Builds new characters: checks identity, looks and point buy, plans and applies the first level
/// with the same rules as a level-up, and writes a BIC laid out like the original's (fields in
/// the same order, level history, package gold and equipment as blueprint references).
/// </summary>
public static class CharacterFactory
{
    /// <summary>Clothing every new character wears (the original equips it in the chest slot).</summary>
    public const string StartingClothes = "nw_cloth001";
    const int ChestSlot = 0x2;

    public static List<RuleError> IdentityErrors(World w, NewCharacter n)
    {
        var errors = new List<RuleError>();
        var races = w.Resources.TwoDA("racialtypes");
        if (n.Race < 0 || n.Race >= races.RowCount || races.GetInt(n.Race, "PlayerRace") != 1) errors.Add(new("race", "Choose a playable race."));
        if (n.Gender is not (0 or 1)) errors.Add(new("gender", "Choose a gender."));
        if (n.FirstName.Length == 0) errors.Add(new("name", "Enter a first name."));
        if (n.FirstName.Length > 40 || n.LastName.Length > 40 || n.FullName.Any(char.IsControl)) errors.Add(new("name", "That name is not allowed."));
        if (n.Deity.Length > 64 || n.Subrace.Length > 64) errors.Add(new("deity", "Deity and subrace are limited to 64 characters."));
        if (n.LawChaos is < 0 or > 100 || n.GoodEvil is < 0 or > 100) errors.Add(new("alignment", "Choose an alignment."));
        errors.AddRange(AbilityErrors(w, n));
        if (PortraitResRef(w, n) is not { Length: > 0 }) errors.Add(new("portrait", "Choose a portrait."));
        int mask = Ruleset.Int(w, "CHARGEN_ENABLED_PHENOTYPES", 5);
        if (n.Phenotype is < 0 or > 30 || (mask & (1 << n.Phenotype)) == 0) errors.Add(new("phenotype", "That body type is not available."));
        if (n.Colors.Any(c => c is < 0 or > 255)) errors.Add(new("colors", "Invalid colour."));
        if (n.BodyParts.Any(p => p is < 0 or > 65535) || (n.BodyParts.Length > 0 && n.BodyParts[0] < 1)) errors.Add(new("appearance", "Invalid body part."));
        if (n.Wings < 0 || n.Wings >= Math.Max(1, w.Resources.TwoDA("wingmodel").RowCount)) errors.Add(new("appearance", "Invalid wings."));
        if (n.Tail < 0 || n.Tail >= Math.Max(1, w.Resources.TwoDA("tailmodel").RowCount)) errors.Add(new("appearance", "Invalid tail."));
        if (n.SoundSet >= w.Resources.TwoDA("soundset").RowCount) errors.Add(new("soundset", "Invalid voice set."));
        var packages = w.Resources.TwoDA("packages");
        if (n.Level.Package >= 0 && (n.Level.Package >= packages.RowCount || packages.GetInt(n.Level.Package, "ClassID", -1) != n.Level.Class))
            errors.Add(new("package", "That package is not for the chosen class."));
        return errors;
    }

    /// <summary>Point buy: every score between CHARGEN_BASE_ABILITY_MIN and _MAX, the class's primary ability at least _MIN_PRIMARY, total cost within the race's AbilitiesPointBuyNumber.</summary>
    public static List<RuleError> AbilityErrors(World w, NewCharacter n)
    {
        var errors = new List<RuleError>();
        if (n.Abilities.Length != 6) { errors.Add(new("abilities", "Six ability scores are needed.")); return errors; }
        int min = ProgressionRules.AbilityMin(w), max = ProgressionRules.AbilityMax(w);
        if (n.Abilities.Any(a => a < min || a > max)) errors.Add(new("abilities", $"Ability scores must be between {min} and {max} before racial adjustments."));
        int budget = n.Race >= 0 ? ProgressionRules.PointBuyBudget(w, n.Race) : 30;
        int cost = ProgressionRules.PointBuyTotal(w, n.Abilities);
        if (cost > budget) errors.Add(new("abilities", $"The scores cost {cost} points; {budget} are available."));
        if (ProgressionRules.ClassExists(w, n.Level.Class)
            && NwRules.AbilityFromLabel(w.Resources.TwoDA("classes").Get(n.Level.Class, "PrimaryAbil")) is int primary and >= 0
            && n.Abilities[primary] < Ruleset.Int(w, "CHARGEN_BASE_ABILITY_MIN_PRIMARY", min))
            errors.Add(new("abilities", $"A {ProgressionRules.ClassName(w, n.Level.Class)} needs at least {Ruleset.Int(w, "CHARGEN_BASE_ABILITY_MIN_PRIMARY", min)} in its primary ability."));
        return errors;
    }

    /// <summary>The portrait resref: a portraits.2da row (po_ + BaseResRef) or, when allowed, a custom resref.</summary>
    public static string PortraitResRef(World w, NewCharacter n)
    {
        var portraits = w.Resources.TwoDA("portraits");
        if (n.PortraitId >= 0 && n.PortraitId < portraits.RowCount && portraits.Get(n.PortraitId, "BaseResRef") is { Length: > 0 } baseRes)
            return "po_" + baseRes.ToLowerInvariant();
        var custom = n.Portrait.ToLowerInvariant();
        if (custom.Length is > 0 and <= 16 && custom.All(ch => char.IsAsciiLetterOrDigit(ch) || ch == '_') && Ruleset.Int(w, "CHARGEN_ALLOW_CUSTOM_PORTRAITS", 1) != 0)
            return custom;
        return "";
    }

    /// <summary>Validates a new character (identity, abilities and the first level) without building it.</summary>
    public static CharacterBuildResult Validate(World w, NewCharacter n) => Build(w, n, commit: false);

    /// <summary>Builds the character's BIC, or reports why it isn't legal.</summary>
    public static CharacterBuildResult Build(World w, NewCharacter n, bool commit = true)
    {
        var errors = IdentityErrors(w, n);
        if (n.Race < 0 || n.Race >= w.Resources.TwoDA("racialtypes").RowCount) return new(null, new LevelPlan(), errors);
        var c = w.Creatures.FromTemplate(BlankTemplate(w, n));
        var plan = ProgressionRules.Plan(w, c, n.Level, player: true);
        errors.AddRange(plan.Errors);
        if (!commit || errors.Count > 0) return new(null, plan, errors);

        LevelApplier.Apply(w, c, n.Level, plan);
        c.CurrentHitPoints = c.MaxHitPoints;
        c.StartingPackage = StartingPackage(w, n);
        var t = c.Template;
        t.SetByte("StartingPackage", c.StartingPackage);
        var ac = CreatureRules.ArmorClass(w, c);
        t.SetShort("ArmorClass", ac.Total);
        t.SetByte("BaseAttackBonus", CreatureRules.BaseAttackBonus(w, c));
        t.Set("RefSaveThrow", GffType.Char, (sbyte)CreatureRules.BaseSave(w, c, 2));
        t.Set("WillSaveThrow", GffType.Char, (sbyte)CreatureRules.BaseSave(w, c, 3));
        t.Set("FortSaveThrow", GffType.Char, (sbyte)CreatureRules.BaseSave(w, c, 1));
        PrepareSpells(w, c);

        var bic = ObjectSerializer.CreatureGff(w, c, 0xFFFFFFFF, asCharacter: true);
        // Like the original: stored current hit points exclude bonuses; PregameCurrent holds the real value.
        bic.SetShort("CurrentHitPoints", c.BaseHitPoints);
        bic.SetShort("PregameCurrent", c.MaxHitPoints);
        var packages = w.Resources.TwoDA("packages");
        bic.SetDWord("Gold", (uint)Math.Max(0, packages.GetInt(c.StartingPackage, "Gold")));
        var equip = new List<GffStruct>();
        if (w.Resources.Read(StartingClothes, ResType.Uti) != null)
            equip.Add(new GffStruct(ChestSlot).SetResRef("EquippedRes", StartingClothes));
        bic.SetList("Equip_ItemList", equip);
        var items = new List<GffStruct>();
        var eq = w.Resources.TwoDA(packages.Get(c.StartingPackage, "Equip2DA").ToLowerInvariant());
        for (int r = 0; r < eq.RowCount; r++)
            if (eq.Get(r, "Label") is { Length: > 0 } res && w.Resources.Read(res.ToLowerInvariant(), ResType.Uti) != null)
                items.Add(new GffStruct().SetResRef("InventoryRes", res.ToLowerInvariant()));
        bic.SetList("ItemList", items);
        return new(bic, plan, errors);
    }

    static int StartingPackage(World w, NewCharacter n)
    {
        if (n.Level.Package >= 0) return n.Level.Package;
        int pkg = w.Resources.TwoDA("classes").GetInt(n.Level.Class, "Package", -1);
        return pkg >= 0 ? pkg : 255;
    }

    /// <summary>A wizard starts with its first spells prepared, like the original's new characters.</summary>
    static void PrepareSpells(World w, Creature c)
    {
        foreach (var cc in c.Classes)
        {
            if (!ProgressionRules.HasSpellbook(w, cc.Class)) continue;
            for (int lvl = 0; lvl < 10; lvl++)
            {
                int slots = CreatureRules.SpellSlots(w, c, cc.Class, lvl);
                var known = cc.KnownSpells[lvl];
                for (int i = 0; i < slots && known.Count > 0; i++) cc.Memorized[lvl].Add((known[i % known.Count], 0, true));
            }
        }
    }

    /// <summary>A level-0 character file (identity and looks, no classes), fields in the original's order.</summary>
    public static GffStruct BlankTemplate(World w, NewCharacter n)
    {
        var races = w.Resources.TwoDA("racialtypes");
        var t = new GffStruct(0xFFFFFFFF);
        t.SetLocString("FirstName", new LocString(n.FirstName));
        t.SetLocString("LastName", new LocString(n.LastName));
        t.SetLocString("Description", new LocString(n.Description));
        t.SetDWord("Gold", 0);
        t.SetResRef("Conversation", "");
        t.SetInt("Age", n.Age >= 0 ? n.Age : races.GetInt(n.Race, "AGE"));
        t.SetByte("Gender", n.Gender);
        t.SetByte("Race", n.Race);
        t.SetString("Subrace", n.Subrace);
        t.SetByte("StartingPackage", 255);
        t.SetString("Deity", n.Deity);
        t.SetShort("ArmorClass", 10);
        t.SetByte("BaseAttackBonus", 0);
        t.Set("RefSaveThrow", GffType.Char, (sbyte)0);
        t.Set("WillSaveThrow", GffType.Char, (sbyte)0);
        t.Set("FortSaveThrow", GffType.Char, (sbyte)0);
        t.SetByte("MClassLevUpIn", 0);
        t.SetWord("SoundSetFile", n.SoundSet >= 0 ? n.SoundSet : DefaultSoundSet(w, n.Gender));
        t.SetList("ClassList", new List<GffStruct>());
        t.SetWord("SkillPoints", 0);
        int skills = w.Resources.TwoDA("skills").RowCount;
        t.SetList("SkillList", Enumerable.Range(0, skills).Select(_ => new GffStruct().SetByte("Rank", 0)).ToList());
        t.SetList("FeatList", new List<GffStruct>());
        string[] ab = { "Str", "Dex", "Con", "Int", "Wis", "Cha" };
        for (int i = 0; i < 6; i++) t.SetByte(ab[i], n.Abilities.Length == 6 ? n.Abilities[i] : 8);
        t.SetShort("CurrentHitPoints", 0);
        t.SetShort("HitPoints", 0);
        t.SetShort("MaxHitPoints", 0);
        t.SetShort("PregameCurrent", 0);
        t.SetList("LvlStatList", new List<GffStruct>());
        t.SetDWord("Experience", 0);
        t.SetResRef("Portrait", PortraitResRef(w, n));
        t.SetByte("GoodEvil", n.GoodEvil);
        t.SetByte("LawfulChaotic", n.LawChaos);
        string[] colors = { "Color_Skin", "Color_Hair", "Color_Tattoo1", "Color_Tattoo2" };
        for (int i = 0; i < 4; i++) t.SetByte(colors[i], n.Colors.Length == 4 ? n.Colors[i] : 0);
        t.SetInt("Phenotype", n.Phenotype);
        int appearance = n.Appearance >= 0 ? n.Appearance : races.GetInt(n.Race, "Appearance");
        t.SetWord("Appearance_Type", appearance);
        var parts = DefaultBodyParts();
        for (int i = 0; i < parts.Length && i < n.BodyParts.Length; i++) parts[i] = n.BodyParts[i];
        SetPart(t, "Appearance_Head", parts[0]);
        t.SetDWord("Tail_New", (uint)n.Tail);
        t.SetDWord("Wings_New", (uint)n.Wings);
        // Body parts in the original's field order; the right foot is stored under the armour label.
        foreach (var (field, index) in new[]
        {
            ("BodyPart_Neck", 1), ("BodyPart_Torso", 2), ("BodyPart_Belt", 4), ("BodyPart_Pelvis", 3),
            ("BodyPart_LShoul", 6), ("BodyPart_LBicep", 8), ("BodyPart_LFArm", 10), ("BodyPart_LHand", 12),
            ("BodyPart_LThigh", 14), ("BodyPart_LShin", 16), ("BodyPart_LFoot", 18),
            ("BodyPart_RShoul", 5), ("BodyPart_RBicep", 7), ("BodyPart_RFArm", 9), ("BodyPart_RHand", 11),
            ("BodyPart_RThigh", 13), ("BodyPart_RShin", 15), ("ArmorPart_RFoot", 17),
        })
            SetPart(t, field, parts[index]);
        return t;
    }

    static void SetPart(GffStruct t, string field, int value)
    {
        t.SetByte(field, Math.Min(value, 255));
        if (value > 255) t.SetWord("x" + field, value);
    }

    /// <summary>A new character's body: everything 1 except belt and shoulders (0), as the original.</summary>
    public static int[] DefaultBodyParts()
    {
        var parts = Enumerable.Repeat(1, CreatureFactory.BodyPartFields.Length).ToArray();
        parts[4] = 0; parts[5] = 0; parts[6] = 0;
        return parts;
    }

    /// <summary>First player voice set (soundset.2da TYPE 0) of the gender.</summary>
    public static int DefaultSoundSet(World w, int gender)
    {
        var t = w.Resources.TwoDA("soundset");
        for (int r = 0; r < t.RowCount; r++)
            if (t.GetInt(r, "TYPE", -1) == 0 && t.GetInt(r, "GENDER", -1) == gender && t.Get(r, "RESREF").Length > 0) return r;
        return 0;
    }

    /// <summary>Writes a new character into the (sandboxed) local vault; returns the file path.</summary>
    public static string SaveToLocalVault(World w, GffStruct bic)
    {
        var dir = Path.Combine(w.System<PersistenceSystem>().UserRoot, "localvault");
        return SaveTo(w, dir, bic);
    }

    internal static string SaveTo(World w, string dir, GffStruct bic)
    {
        Directory.CreateDirectory(dir);
        var talk = w.Resources.Talk;
        string name = new string((bic.GetLocString("FirstName").Resolve(talk) + bic.GetLocString("LastName").Resolve(talk))
            .Where(char.IsAsciiLetterOrDigit).ToArray()).ToLowerInvariant();
        if (name.Length > 16) name = name[..16];
        if (name.Length == 0) name = "character";
        string file = name;
        for (int i = 1; File.Exists(Path.Combine(dir, file + ".bic")) && i < 1000; i++)
            file = name[..Math.Min(name.Length, 16 - i.ToString().Length)] + i;
        var path = Path.Combine(dir, file + ".bic");
        File.WriteAllBytes(path + ".tmp", Gff.Write(bic, "BIC "));
        File.Move(path + ".tmp", path, overwrite: true);
        return path;
    }
}
