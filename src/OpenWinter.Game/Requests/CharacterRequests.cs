using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Requests;

/// <summary>The character sheet: everything the client shows about the player's own creature.</summary>
public static class CharacterRequests
{
    static readonly string[] AbilityNames = ["STR", "DEX", "CON", "INT", "WIS", "CHA"];

    [ClientRequest("character")]
    static void Character(Request r)
    {
        // Old clients and explicit opens omit the token and always receive a sheet.
        // Polls carrying the current token never build the expensive payload.
        string version = CharacterSheetVersion.Read(r.World, r.Player);
        if (r.Str("version") == version) return;
        r.World.Output?.Notify("character-sheet", r.Player, Sheet(r.World, r.Player), r.Player);
    }

    [ClientRequest("journal")]
    static void Journal(Request r) => r.World.System<Players.JournalService>().SendAll(r.Player);

    /// <summary>Builds the "character-sheet" payload from the creature rules.</summary>
    public static object Sheet(World w, Creature c)
    {
        var res = w.Resources;
        string Tlk(string table, int row, string column) =>
            res.TwoDA(table).GetInt(row, column, -1) is var strref and >= 0 ? res.TlkString(unchecked((uint)strref)) : "";
        var ac = CreatureRules.ArmorClass(w, c);
        int level = c.TotalLevel;
        var skillTable = res.TwoDA("skills");
        var featTable = res.TwoDA("feat");
        var enc = res.TwoDA("encumbrance");
        int str = Math.Clamp(CreatureRules.AbilityScore(w, c, NwRules.AbilityStr), 0, Math.Max(0, enc.RowCount - 1));
        return new
        {
            creature = c.Id,
            version = CharacterSheetVersion.Read(w, c),
            name = c.Name.Resolve(res.Talk),
            portrait = Portraits.Display(w, c),
            race = c.Subrace.Length > 0 ? c.Subrace : Tlk("racialtypes", c.Race, "Name"),
            baseRace = Tlk("racialtypes", c.Race, "Name"),
            subrace = c.Subrace,
            gender = c.Gender == 1 ? "Female" : "Male",
            deity = c.Deity,
            age = c.Age,
            description = c.Description,
            alignment = Alignment(c),
            lawChaos = c.LawfulChaotic,
            goodEvil = c.GoodEvil,
            level,
            xp = c.Experience,
            // XP the current level started at, so the bar can show progress through this level.
            xpLevel = CreatureRules.XPForLevel(w, level),
            xpNext = level < 40 ? CreatureRules.XPForLevel(w, level + 1) : c.Experience,
            classes = c.Classes.Select(cl => new { id = cl.Class, name = Tlk("classes", cl.Class, "Name"), level = cl.Level, icon = res.TwoDA("classes").Get(cl.Class, "Icon").ToLowerInvariant() }).ToArray(),
            abilities = Enumerable.Range(0, 6).Select(i => new
            {
                name = AbilityNames[i],
                @base = CreatureRules.AbilityScore(w, c, i, baseOnly: true),
                score = CreatureRules.AbilityScore(w, c, i),
                mod = CreatureRules.AbilityModifier(w, c, i),
            }).ToArray(),
            hp = c.CurrentHitPoints,
            maxHp = CreatureRules.MaxHitPoints(w, c),
            tempHp = CreatureRules.TemporaryHitPoints(c),
            ac = ac.Total,
            acParts = new
            {
                armour = ac.Armour + ac.ArmourEnchant, shield = ac.Shield + ac.ShieldEnchant, dex = ac.Dex, size = ac.Size,
                natural = ac.Natural, deflection = ac.Deflection, dodge = ac.Dodge, penalty = ac.Penalty,
            },
            bab = CreatureRules.BaseAttackBonus(w, c),
            attacks = CreatureRules.AttacksPerRound(w, c),
            saves = new
            {
                fortitude = CreatureRules.SavingThrow(w, c, 1),
                reflex = CreatureRules.SavingThrow(w, c, 2),
                will = CreatureRules.SavingThrow(w, c, 3),
            },
            // Class base saves, for the breakdown (total = base + ability + everything else).
            baseSaves = new
            {
                fortitude = CreatureRules.BaseSave(w, c, 1),
                reflex = CreatureRules.BaseSave(w, c, 2),
                will = CreatureRules.BaseSave(w, c, 3),
            },
            spellResistance = CreatureRules.SpellResistance(w, c),
            arcaneFailure = CreatureRules.ArcaneSpellFailure(w, c),
            armourCheckPenalty = CreatureRules.ArmourCheckPenalty(w, c),
            weight = CreatureRules.CarriedWeight(w, c),
            // Carrying capacity in tenths of a pound (encumbrance.2da by Strength).
            carryNormal = enc.GetInt(str, "Normal", 0),
            carryHeavy = enc.GetInt(str, "Heavy", 0),
            // The weapons' attack bonus, damage range and critical (the classic character record's combat summary).
            weapons = Weapons(w, c),
            walkSpeed = c.WalkRate,
            runSpeed = c.RunRate,
            skills = Enumerable.Range(0, skillTable.RowCount)
                .Where(s => skillTable.Get(s, "Label").Length > 0 && !skillTable.Get(s, "Name").StartsWith("****") && CreatureRules.HasSkill(w, c, s))
                .Select(s => new
                {
                    id = s,
                    name = Tlk("skills", s, "Name"),
                    ranks = c.Skills.Length > s ? c.Skills[s] : 0,
                    total = CreatureRules.SkillRank(w, c, s),
                    icon = skillTable.Get(s, "Icon").ToLowerInvariant(),
                    ability = skillTable.Get(s, "KeyAbility").ToUpperInvariant(),
                    armourCheck = skillTable.GetInt(s, "ArmorCheckPenalty") != 0,
                    untrained = skillTable.GetInt(s, "Untrained", 1) != 0,
                    description = Tlk("skills", s, "Description"),
                })
                // Placeholder rows ("{Not Used}") are hidden, as the original sheet does.
                .Where(s => s.name.Length > 0 && !s.name.StartsWith('{')).OrderBy(s => s.name).ToArray(),
            feats = CreatureRules.AllFeats(w, c).Distinct()
                .Select(f => new
                {
                    id = f,
                    name = Tlk("feat", f, "FEAT"),
                    icon = featTable.Get(f, "ICON").ToLowerInvariant(),
                    // feat.2da TOOLSCATEGORIES: 1 combat, 2 active combat, 3 defensive, 4 magical, 5 class/racial, 6 other.
                    category = featTable.GetInt(f, "TOOLSCATEGORIES", 6),
                    description = Tlk("feat", f, "DESCRIPTION"),
                })
                .Where(f => f.name.Length > 0).OrderBy(f => f.name).ToArray(),
        };
    }

    /// <summary>Main and off-hand weapon summaries (name, attack bonus, damage range, critical); null where there is none.</summary>
    static object Weapons(World w, Creature c)
    {
        try
        {
            if (w.System<CombatSystem>().Rules is not CreatureCombatRules rules) return new { };
            var (main, off, _) = rules.Weapons(c);
            object Summary(Item? weapon, bool offHand)
            {
                // A context against nobody in particular (no situation: flanking, unseen ...), as the sheet shows it.
                var s = rules.Stats(weapon);
                AttackContext Context(bool highRolls) => new()
                {
                    World = w, Attacker = c, Target = c, Dice = new FixedDice(highRolls), Weapon = weapon, OffHand = offHand,
                    Ranged = s.Ranged, Thrown = s.Thrown, CreatureWeapon = s.Creature,
                };
                var low = Context(false);
                var high = Context(true);
                var (threat, mult) = rules.Critical(low);
                // The original's sheet (visual scene rulesfollowpc): "Damage: 1-6 + 3 (Critical: 20 / x2)" - the weapon's
                // dice, then what is added to them (Strength and the like) - and a line for each damage property of the
                // weapon, "+1d10 Fire Damage", "+5 Bludgeoning Damage", in its damage type's colour.
                var roll = rules.Damage(low, false);
                int baseType = rules.PhysicalType(low, s);
                var source = rules.DamageSource(low);
                int diceLow = rules.WeaponDice(low, s), diceHigh = rules.WeaponDice(high, s);
                // The enhancement bonus has a line of its own in the weapon's damage type ("+5 Bludgeoning Damage" for
                // the Rakshasa's +5 staff), and the Strength part reads as the plain modifier ("+ 3" at Strength 17,
                // also on that two-handed staff).
                int enhancement = s.Ranged && !s.Thrown ? 0 : Math.Max(0, rules.Enhancement(low));
                int strength = rules.StrengthDamage(low, s), strMod = CreatureRules.AbilityModifier(w, c, NwRules.AbilityStr);
                int flat = roll.ByType.GetValueOrDefault(baseType) - rules.ItemDamageBonuses(source, low, baseType).GetValueOrDefault(baseType)
                    - diceLow - enhancement - (strength > strMod && strMod > 0 ? strength - strMod : 0);
                // A hit deals at least 1, which hides a Strength penalty in the low roll: the original reads "1-3 - 4" for
                // the bat familiar's Strength 3 (level-up's familiar step). Take it from the high roll, or
                // from Strength when that is held at 1 too.
                if (roll.Total <= 1 && strength < 0)
                {
                    var hi = rules.Damage(high, false);
                    flat = hi.Total > 1
                        ? hi.ByType.GetValueOrDefault(baseType) - rules.ItemDamageBonuses(source, high, baseType).GetValueOrDefault(baseType) - diceHigh - enhancement
                        : Math.Min(flat, strength);
                }
                return new
                {
                    name = weapon?.Name.Resolve(w.Resources.Talk) ?? "",
                    attack = rules.AttackBonus(low),
                    min = roll.Total,
                    max = rules.Damage(high, false).Total,
                    diceMin = diceLow,
                    diceMax = diceHigh,
                    flat,
                    bonuses = DamageProperties(w, source, enhancement, baseType),
                    threat,
                    mult,
                };
            }
            return new { main = Summary(main, false), off = off != null ? Summary(off, true) : null };
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return new { };
        }
    }

    /// <summary>A weapon's damage bonus properties as the sheet lists them: amount ("1d10", "5"), type name and colour.</summary>
    static object[] DamageProperties(World w, Item? item, int enhancement, int baseType)
    {
        var costs = w.Resources.TwoDA("iprp_damagecost");
        var types = w.Resources.TwoDA("damagetypes");
        var groups = w.Resources.TwoDA("damagetypegroups");
        var list = new List<object>();
        var lines = new List<(string Amount, int Bits)>();
        if (item != null)
            foreach (var p in item.Properties)
                if (p.Type == CombatK.IpDamageBonus) lines.Add((costs.Get(p.CostTableValue, "Label"), ItemPropertyRules.DamageTypeBits(p.SubType)));
        if (enhancement > 0) lines.Add((enhancement.ToString(), baseType));
        foreach (var (amount, bits) in lines)
        {
            if (bits == 0) continue;
            // damagetypes.2da rows follow the DAMAGE_TYPE_* bits; CharsheetStrref is "Fire Damage" and so on.
            int row = bits == 7 ? 12 : System.Numerics.BitOperations.Log2((uint)bits);
            int strref = types.GetInt(row, "CharsheetStrref", -1);
            string name = strref >= 0 ? w.Resources.TlkString(unchecked((uint)strref)) : "";
            if (name.Length == 0) name = types.Get(row, "Label");
            int group = types.GetInt(row, "DamageTypeGroup", -1);
            string color = group < 0 ? "" : $"{groups.GetInt(group, "ColorR", 255):x2}{groups.GetInt(group, "ColorG", 255):x2}{groups.GetInt(group, "ColorB", 255):x2}";
            list.Add(new { amount, type = name, color });
        }
        return list.ToArray();
    }

    /// <summary>Dice that always roll their lowest or highest face (damage ranges).</summary>
    sealed class FixedDice(bool high) : IDice
    {
        public int Roll(int sides) => high ? Math.Max(1, sides) : 1;
    }

    static string Alignment(Creature c)
    {
        string lc = c.LawfulChaotic >= 70 ? "Lawful" : c.LawfulChaotic <= 30 ? "Chaotic" : "Neutral";
        string ge = c.GoodEvil >= 70 ? "Good" : c.GoodEvil <= 30 ? "Evil" : "Neutral";
        return lc == "Neutral" && ge == "Neutral" ? "True Neutral" : $"{lc} {ge}";
    }
}
