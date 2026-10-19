using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Progression;

/// <summary>
/// The data the character wizard and the level-up window show, resolved through the module's haks:
/// names, descriptions and icons of races, classes, skills, feats, spells, domains, schools,
/// packages, voice sets and associates, plus the point-buy and alignment rules. Long texts
/// (feat and spell descriptions) are sent on demand ("chargen-describe").
/// </summary>
public static class ChargenData
{
    static string Tlk(World w, string table, int row, string column) =>
        w.Resources.TwoDA(table).GetInt(row, column, -1) is var strref and >= 0 ? ProgressionRules.StripColors(w.Resources.TlkString(unchecked((uint)strref))) : "";

    static readonly (int LawChaos, int GoodEvil, string Name)[] Alignments =
    {
        (85, 85, "Lawful Good"), (50, 85, "Neutral Good"), (15, 85, "Chaotic Good"),
        (85, 50, "Lawful Neutral"), (50, 50, "True Neutral"), (15, 50, "Chaotic Neutral"),
        (85, 15, "Lawful Evil"), (50, 15, "Neutral Evil"), (15, 15, "Chaotic Evil"),
    };

    public static object Static(World w)
    {
        var res = w.Resources;
        var races = res.TwoDA("racialtypes");
        var classes = res.TwoDA("classes");
        var skills = res.TwoDA("skills");
        var feats = res.TwoDA("feat");
        var packages = res.TwoDA("packages");
        var domains = res.TwoDA("domains");
        var schools = res.TwoDA("spellschools");
        var spells = res.TwoDA("spells");
        int min = ProgressionRules.AbilityMin(w), max = ProgressionRules.AbilityMax(w);
        int mask = Ruleset.Int(w, "CHARGEN_ENABLED_PHENOTYPES", 5);
        var phenotypes = res.TwoDA("phenotype");
        var soundsets = res.TwoDA("soundset");
        return new
        {
            pointBuy = new
            {
                min, max,
                primaryMin = Ruleset.Int(w, "CHARGEN_BASE_ABILITY_MIN_PRIMARY", min),
                cost = Enumerable.Range(min, max - min + 1).Select(s => ProgressionRules.PointBuyCost(w, s)).ToArray(),
            },
            races = Enumerable.Range(0, races.RowCount).Where(r => races.GetInt(r, "PlayerRace") == 1 && races.Get(r, "Name").Length > 0).Select(r => new
            {
                id = r,
                name = Tlk(w, "racialtypes", r, "Name"),
                description = Tlk(w, "racialtypes", r, "Description"),
                adjust = Enumerable.Range(0, 6).Select(a => ProgressionRules.RacialAdjustment(w, r, a)).ToArray(),
                budget = ProgressionRules.PointBuyBudget(w, r),
                favored = races.GetInt(r, "Favored", -1),
                appearance = races.GetInt(r, "Appearance"),
                age = races.GetInt(r, "AGE"),
                // The help panel's title (racialtypes NamePlural), the default description (Biography) and the name
                // generator's letter tables (NameGenTableA, NameGenTableB: + "m"/"f" for first names, "l" for last).
                plural = Tlk(w, "racialtypes", r, "NamePlural"),
                biography = Tlk(w, "racialtypes", r, "Biography"),
                nameGen = new[] { races.Get(r, "NameGenTableA"), races.Get(r, "NameGenTableB") }.Where(x => x.Length > 0).Select(x => x.ToLowerInvariant()).ToArray(),
            }).ToArray(),
            classes = Enumerable.Range(0, classes.RowCount).Where(c => ProgressionRules.ClassExists(w, c)).Select(c => new
            {
                id = c,
                name = ProgressionRules.ClassName(w, c),
                description = Tlk(w, "classes", c, "Description"),
                icon = classes.Get(c, "Icon").ToLowerInvariant(),
                hitDie = ProgressionRules.HitDie(w, c),
                skillPoints = classes.GetInt(c, "SkillPointBase"),
                primary = classes.Get(c, "PrimaryAbil"),
                // The class's suggested starting scores (classes.2da Str..Cha), STR DEX CON INT WIS CHA.
                recommended = new[] { "Str", "Dex", "Con", "Int", "Wis", "Cha" }.Select(col => classes.GetInt(c, col, 10)).ToArray(),
                player = ProgressionRules.IsPlayerClass(w, c),
                prestige = ProgressionRules.IsPrestige(w, c),
                caster = CreatureRules.IsSpellCaster(w, c),
                // Allowed alignments, in the order of "alignments" below.
                alignments = Alignments.Select(a => ProgressionRules.AlignmentAllowed(w, c, a.LawChaos, a.GoodEvil)).ToArray(),
                packages = Enumerable.Range(0, packages.RowCount).Where(p => packages.GetInt(p, "ClassID", -1) == c && packages.GetInt(p, "PlayerClass") == 1).ToArray(),
                defaultPackage = PackageBuilder.DefaultPackage(w, c),
            }).ToArray(),
            alignments = Alignments.Select(a => new { lawChaos = a.LawChaos, goodEvil = a.GoodEvil, name = a.Name }).ToArray(),
            skills = Enumerable.Range(0, skills.RowCount).Where(s => ProgressionRules.SkillExists(w, s)).Select(s => new
            {
                id = s,
                name = ProgressionRules.SkillName(w, s),
                icon = skills.Get(s, "Icon").ToLowerInvariant(),
                ability = skills.Get(s, "KeyAbility"),
                untrained = skills.GetInt(s, "Untrained") == 1,
                hidden = PackageBuilder.IsHiddenSkill(w, s),
            }).ToArray(),
            feats = Enumerable.Range(0, feats.RowCount).Where(f => ProgressionRules.FeatExists(w, f)).Select(f => new
            {
                id = f,
                name = ProgressionRules.FeatName(w, f),
                icon = feats.Get(f, "ICON").ToLowerInvariant(),
                category = feats.Get(f, "TOOLSCATEGORIES"),
                master = feats.GetInt(f, "MASTERFEAT", -1),
            }).ToArray(),
            // Master feats group their sub-feats in the classic feat list (Weapon Focus: one row, its weapons in a popup).
            masterfeats = Enumerable.Range(0, res.TwoDA("masterfeats").RowCount).Where(m => res.TwoDA("masterfeats").Get(m, "STRREF").Length > 0).Select(m => new
            {
                id = m,
                name = Tlk(w, "masterfeats", m, "STRREF"),
                icon = res.TwoDA("masterfeats").Get(m, "ICON").ToLowerInvariant(),
                description = "",
            }).ToArray(),
            spells = Enumerable.Range(0, spells.RowCount).Where(s => spells.Get(s, "Label").Length > 0 && spells.Get(s, "Name").Length > 0).Select(s => new
            {
                id = s,
                name = ProgressionRules.SpellName(w, s),
                icon = spells.Get(s, "IconResRef").ToLowerInvariant(),
                school = spells.Get(s, "School"),
            }).ToArray(),
            packages = Enumerable.Range(0, packages.RowCount).Where(p => packages.GetInt(p, "PlayerClass") == 1 && packages.Get(p, "Name").Length > 0).Select(p => new
            {
                id = p,
                name = Tlk(w, "packages", p, "Name"),
                description = Tlk(w, "packages", p, "Description"),
                @class = packages.GetInt(p, "ClassID", -1),
            }).ToArray(),
            domains = Enumerable.Range(0, domains.RowCount).Where(d => domains.Get(d, "Label").Length > 0 && domains.Get(d, "Name").Length > 0).Select(d => new
            {
                id = d,
                name = Tlk(w, "domains", d, "Name"),
                description = Tlk(w, "domains", d, "Description"),
                icon = domains.Get(d, "Icon").ToLowerInvariant(),
                feat = domains.GetInt(d, "GrantedFeat", -1),
            }).ToArray(),
            schools = Enumerable.Range(0, schools.RowCount).Where(s => schools.Get(s, "Label").Length > 0).Select(s => new
            {
                id = s,
                name = Tlk(w, "spellschools", s, "StringRef"),
                description = Tlk(w, "spellschools", s, "Description"),
            }).ToArray(),
            familiars = Associates(w, "hen_familiar"),
            companions = Associates(w, "hen_companion"),
            phenotypes = Enumerable.Range(0, Math.Min(31, phenotypes.RowCount)).Where(p => (mask & (1 << p)) != 0)
                .Select(p => new { id = p, name = Tlk(w, "phenotype", p, "Name") is { Length: > 0 } n ? n : phenotypes.Get(p, "Label") }).ToArray(),
            soundsets = Enumerable.Range(0, soundsets.RowCount).Where(s => soundsets.GetInt(s, "TYPE", -1) == 0 && soundsets.Get(s, "RESREF").Length > 0)
                .Select(s => new { id = s, name = Tlk(w, "soundset", s, "STRREF") is { Length: > 0 } n ? n : soundsets.Get(s, "LABEL"), gender = soundsets.GetInt(s, "GENDER") }).ToArray(),
            customPortraits = Ruleset.Int(w, "CHARGEN_ALLOW_CUSTOM_PORTRAITS", 1) != 0,
            recommended = Ruleset.Int(w, "CHARGEN_ENABLE_RECOMMENDED_BUTTON", 1) != 0,
            // The clothes every new character starts in, for the classic appearance screen's model (it is shown dressed: c42).
            clothing = ClothingVisual(w),
            wings = w.Resources.TwoDA("wingmodel").RowCount,
            tails = w.Resources.TwoDA("tailmodel").RowCount,
        };
    }

    /// <summary>The starting clothes' look (CharacterFactory.StartingClothes), read from the blueprint; null when missing.</summary>
    static object? ClothingVisual(World w)
    {
        var t = w.Resources.ReadGff(CharacterFactory.StartingClothes, OpenWinter.Aurora.Resources.ResType.Uti);
        if (t == null) return null;
        return new
        {
            slot = 2,
            baseItem = (int)t.GetInt("BaseItem"),
            modelParts = new[] { "ModelPart1", "ModelPart2", "ModelPart3" }.Select(f => (int)CreatureFactory.Wide(t, f)).ToArray(),
            colors = ItemFactory.ColorFields.Select(f => (int)t.GetInt(f)).ToArray(),
            armorParts = ItemFactory.ArmorPartFields.Select(f => f.Length == 0 ? 0 : (int)CreatureFactory.Wide(t, f)).ToArray(),
        };
    }

    static object[] Associates(World w, string table)
    {
        var t = w.Resources.TwoDA(table);
        return Enumerable.Range(0, t.RowCount).Where(r => t.Get(r, "BASERESREF").Length > 0)
            .Select(r => (object)new
            {
                id = r,
                name = Tlk(w, table, r, "STRREF") is { Length: > 0 } n ? n : t.Get(r, "NAME"),
                description = Tlk(w, table, r, "DESCRIPTION"),
                appearance = AssociateAppearance(w, t.Get(r, "BASERESREF")),
            }).ToArray();
    }

    /// <summary>
    /// How a familiar or companion looks, for the wizard's 3D preview: its first-level blueprint
    /// (BASERESREF + "01", as summoning resolves it, or the bare resref) read without creating it.
    /// Null when the module has no such blueprint.
    /// </summary>
    public static object? AssociateAppearance(World w, string baseRef)
    {
        baseRef = baseRef.ToLowerInvariant();
        string resref = baseRef + "01";
        var utc = w.Resources.ReadGff(resref, OpenWinter.Aurora.Resources.ResType.Utc);
        if (utc == null) { resref = baseRef; utc = w.Resources.ReadGff(resref, OpenWinter.Aurora.Resources.ResType.Utc); }
        if (utc == null) return null;
        return new
        {
            resref,
            type = (int)utc.GetInt("Appearance_Type"),
            gender = (int)utc.GetInt("Gender"),
            phenotype = (int)utc.GetInt("Phenotype"),
            bodyParts = CreatureFactory.BodyPartFields.Select(f => (int)CreatureFactory.Wide(utc, f, 1)).ToArray(),
            colors = new[] { "Color_Skin", "Color_Hair", "Color_Tattoo1", "Color_Tattoo2" }.Select(f => (int)utc.GetInt(f)).ToArray(),
            tail = (int)utc.GetInt("Tail_New", utc.GetInt("Tail")),
            wings = (int)utc.GetInt("Wings_New", utc.GetInt("Wings")),
        };
    }

    /// <summary>
    /// Portraits for a race and gender (portraits.2da Race/Sex, not plot or inanimate): the race's own first, in row
    /// order, then the other playable races' of the same gender. The original's list for a female elf starts with
    /// the 19 installed elf portraits and goes on with others (menu capture); that the rest are the
    /// other playable races' in row order is inferred from the next row's faces.
    /// </summary>
    public static object Portraits(World w, int race, int gender)
    {
        var t = w.Resources.TwoDA("portraits");
        var races = w.Resources.TwoDA("racialtypes");
        var own = new List<object>();
        var others = new List<object>();
        for (int r = 0; r < t.RowCount; r++)
        {
            var baseRes = t.Get(r, "BaseResRef");
            int pr = t.GetInt(r, "Race", -1);
            if (baseRes.Length == 0 || pr < 0 || t.GetInt(r, "Sex", -1) != gender) continue;
            if (t.GetInt(r, "Plot") == 1 || t.Get(r, "InanimateType").Length > 0) continue;
            var entry = new { id = r, resref = "po_" + baseRes.ToLowerInvariant() };
            if (pr == race) own.Add(entry);
            else if (pr < races.RowCount && races.GetInt(pr, "PlayerRace") == 1) others.Add(entry);
        }
        return new { race, gender, portraits = own.Concat(others).ToList() };
    }

    /// <summary>Name and description of one entry ("feat", "spell", "skill", "class", "race", "domain", "school", "package").</summary>
    public static object Describe(World w, string kind, int id)
    {
        var (table, name, desc) = kind switch
        {
            "feat" => ("feat", "FEAT", "DESCRIPTION"),
            "spell" => ("spells", "Name", "SpellDesc"),
            "skill" => ("skills", "Name", "Description"),
            "class" => ("classes", "Name", "Description"),
            "race" => ("racialtypes", "Name", "Description"),
            "domain" => ("domains", "Name", "Description"),
            "school" => ("spellschools", "StringRef", "Description"),
            "package" => ("packages", "Name", "Description"),
            "masterfeat" => ("masterfeats", "STRREF", "DESCRIPTION"),
            _ => ("", "", ""),
        };
        if (table.Length == 0) return new { kind, id, name = "", text = "" };
        return new { kind, id, name = Tlk(w, table, id, name), text = Tlk(w, table, id, desc) };
    }

    /// <summary>The "plan" payload: options for the level, the choice's errors, and the resulting numbers.</summary>
    public static object Plan(World w, LevelPlan p, LevelChoice? auto = null)
    {
        var a = p.After;
        bool hasClass = p.Class >= 0 && ProgressionRules.ClassExists(w, p.Class) && a.TotalLevel > 0;
        return new
        {
            ok = p.Ok,
            errors = p.Errors.Select(e => new { code = e.Code, message = e.Message }).ToArray(),
            @class = p.Class,
            newLevel = p.NewCharacterLevel,
            newClassLevel = p.NewClassLevel,
            hitDie = p.HitDie,
            abilityIncrease = p.AbilityIncrease,
            skillPoints = new { available = p.SkillPointsAvailable, carried = p.SkillPointsCarried, gained = p.SkillPointsGained, spent = p.SkillPointsSpent, left = p.SkillPointsLeft },
            skills = p.Skills.Select(s => new { id = s.Skill, cost = s.Cost, max = s.MaxRanks, current = s.Current, classSkill = s.ClassSkill }).ToArray(),
            feats = new { general = p.GeneralFeatSlots, bonus = p.BonusFeatSlots, granted = p.GrantedFeats, generalChoices = p.GeneralFeatChoices, bonusChoices = p.BonusFeatChoices },
            spells = p.Spells.Select(s => new
            {
                @class = s.Class,
                perLevel = s.PerLevel.ToDictionary(k => k.Key.ToString(), k => k.Value),
                anyLevel = s.AnyLevel,
                maxLevel = s.MaxLevel,
                choices = s.Choices.ToDictionary(k => k.Key.ToString(), k => k.Value),
                granted = s.Granted.ToDictionary(k => k.Key.ToString(), k => k.Value),
            }).ToArray(),
            pickDomains = p.PickDomains, pickSchool = p.PickSchool, pickFamiliar = p.PickFamiliar, pickCompanion = p.PickCompanion,
            stats = hasClass ? new
            {
                abilities = Enumerable.Range(0, 6).Select(i => CreatureRules.AbilityScore(w, a, i, baseOnly: true)).ToArray(),
                bab = CreatureRules.BaseAttackBonus(w, a),
                fortitude = CreatureRules.BaseSave(w, a, 1) + CreatureRules.Modifier(CreatureRules.AbilityScore(w, a, 2, baseOnly: true)),
                reflex = CreatureRules.BaseSave(w, a, 2) + CreatureRules.Modifier(CreatureRules.AbilityScore(w, a, 1, baseOnly: true)),
                will = CreatureRules.BaseSave(w, a, 3) + CreatureRules.Modifier(CreatureRules.AbilityScore(w, a, 4, baseOnly: true)),
            } : null,
            auto = auto?.ToJson(),
        };
    }
}
