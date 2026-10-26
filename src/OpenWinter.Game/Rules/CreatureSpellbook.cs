using OpenWinter.Game.Objects;
using CastSlots = OpenWinter.Game.Spells.SpellSlots;

namespace OpenWinter.Game.Rules;

/// <summary>Spells known, memorised slots, spontaneous slots and spell-like abilities.</summary>
public static partial class CreatureRules
{
    /// <summary>Index into Creature.Classes of a class, or -1.</summary>
    public static int ClassIndex(Creature c, int cls) => c.Classes.FindIndex(x => x.Class == cls);

    public static bool MemorizesSpells(World w, int cls)
    {
        var raw = w.Resources.TwoDA("classes").Get(cls, "MemorizesSpells");
        if (raw.Length > 0) return raw != "0";
        return cls is NwRules.ClassCleric or NwRules.ClassDruid or NwRules.ClassPaladin or NwRules.ClassRanger or NwRules.ClassWizard;
    }
    public static bool IsSpellCaster(World w, int cls) => w.Resources.TwoDA("classes").GetInt(cls, "SpellCaster") != 0;

    /// <summary>spells.2da column holding a class's spell levels (classes.2da SpellTableColumn, or the classic names).</summary>
    public static string SpellColumn(World w, int cls)
    {
        var col = w.Resources.TwoDA("classes").Get(cls, "SpellTableColumn");
        if (col.Length > 0) return col;
        return cls switch
        {
            NwRules.ClassBard => "Bard",
            NwRules.ClassCleric => "Cleric",
            NwRules.ClassDruid => "Druid",
            NwRules.ClassPaladin => "Paladin",
            NwRules.ClassRanger => "Ranger",
            NwRules.ClassWizard or NwRules.ClassSorcerer => "Wiz_Sorc",
            _ => "",
        };
    }

    /// <summary>GetSpellLevelByClass: the level a class casts a spell at, or -1.</summary>
    public static int SpellLevelForClass(World w, int cls, int spell)
    {
        var col = SpellColumn(w, cls);
        if (col.Length == 0) return -1;
        var v = w.Resources.TwoDA("spells").Get(spell, col);
        return int.TryParse(v, out int lvl) ? lvl : -1;
    }

    /// <summary>Caster level of a class for spell progression: class level plus prestige class levels that advance it.</summary>
    public static int CasterLevel(World w, Creature c, int cls)
    {
        int lvl = ClassLevel(c, cls);
        if (lvl <= 0) return 0;
        var classes = w.Resources.TwoDA("classes");
        bool arcane = classes.GetInt(cls, "Arcane") != 0;
        // Prestige classes add to the first spellcasting class of the matching kind.
        var first = c.Classes.FirstOrDefault(x => IsSpellCaster(w, x.Class) && (classes.GetInt(x.Class, "Arcane") != 0) == arcane);
        if (first == null || first.Class != cls) return lvl;
        foreach (var other in c.Classes)
        {
            int mod = classes.GetInt(other.Class, arcane ? "ArcSpellLvlMod" : "DivSpellLvlMod");
            if (mod > 0) lvl += (other.Level + mod - 1) / mod;
        }
        return lvl;
    }

    /// <summary>Spell slots per day at a spell level: spell gain table + bonus spells from the casting ability + item bonus slots.</summary>
    public static int SpellSlots(World w, Creature c, int cls, int level)
    {
        if (level < 0 || level > 9 || ClassIndex(c, cls) < 0) return 0;
        var table = ClassTable(w, cls, "SpellGainTable");
        if (table.RowCount == 0) return 0;
        int cl = Math.Min(CasterLevel(w, c, cls), table.RowCount);
        if (cl <= 0) return 0;
        var raw = table.Get(cl - 1, $"SpellLevel{level}");
        if (raw.Length == 0 || !int.TryParse(raw, out int slots)) return 0;
        int ability = NwRules.AbilityFromLabel(w.Resources.TwoDA("classes").Get(cls, "SpellcastingAbil"));
        if (ability >= 0)
        {
            int score = AbilityScore(w, c, ability);
            if (score < 10 + level) return 0;
            int mod = Modifier(score);
            if (level > 0 && mod >= level) slots += (mod - level) / 4 + 1;
        }
        foreach (var (_, _, p) in EquippedProperties(c))
            if (p.Type == NwRules.IpBonusSpellSlot && p.SubType == cls && p.CostTableValue == level) slots++;
        if (c.Overrides is { } o) slots += o.BonusSpells.GetValueOrDefault((ClassIndex(c, cls), level));
        return slots;
    }

    /// <summary>Spontaneous caster's uses left at a spell level.</summary>
    public static int SlotsLeft(World w, Creature c, int classIndex, int level)
    {
        EnsureInit(w, c);
        int max = SpellSlots(w, c, c.Classes[classIndex].Class, level);
        return c.SpellSlotsLeft.TryGetValue((classIndex, level), out int left) ? Math.Clamp(left, 0, max) : max;
    }

    static void SetSlotsLeft(World w, Creature c, int classIndex, int level, int value)
    {
        int max = SpellSlots(w, c, c.Classes[classIndex].Class, level);
        c.SpellSlotsLeft[(classIndex, level)] = Math.Clamp(value, 0, max);
    }

    /// <summary>Number of memorisation slots at a level (at least the slots already in the list).</summary>
    public static int MemorizedSlotCount(World w, Creature c, int cls, int level)
    {
        int ci = ClassIndex(c, cls);
        if (ci < 0 || level < 0 || level > 9 || !MemorizesSpells(w, cls)) return 0;
        return Math.Max(SpellSlots(w, c, cls, level), c.Classes[ci].Memorized[level].Count);
    }

    /// <summary>Grows a memorised list so a slot index exists.</summary>
    public static bool EnsureSlot(World w, Creature c, int cls, int level, int index)
    {
        int ci = ClassIndex(c, cls);
        if (ci < 0 || level < 0 || level > 9 || index < 0 || index >= MemorizedSlotCount(w, c, cls, level)) return false;
        var list = c.Classes[ci].Memorized[level];
        while (list.Count <= index) list.Add((-1, 0, false));
        return true;
    }

    /// <summary>Spell level adjustment of a metamagic feat (ruleset.2da SPELL_METAMAGIC_*_COST).</summary>
    public static int MetaMagicCost(World w, int meta) => meta switch
    {
        NwRules.MetaEmpower => Ruleset.Int(w, "SPELL_METAMAGIC_EMPOWER_COST", 2),
        NwRules.MetaExtend => Ruleset.Int(w, "SPELL_METAMAGIC_EXTEND_COST", 1),
        NwRules.MetaMaximize => Ruleset.Int(w, "SPELL_METAMAGIC_MAXIMIZE_COST", 3),
        NwRules.MetaQuicken => Ruleset.Int(w, "SPELL_METAMAGIC_QUICKEN_COST", 4),
        NwRules.MetaSilent => Ruleset.Int(w, "SPELL_METAMAGIC_SILENT_COST", 1),
        NwRules.MetaStill => Ruleset.Int(w, "SPELL_METAMAGIC_STILL_COST", 1),
        _ => 0,
    };

    /// <summary>Uses left of a spell for one class (GetSpellUsesLeft).</summary>
    public static int SpellUsesLeft(World w, Creature c, int cls, int spell, int meta = NwRules.MetaNone, int domainLevel = 0)
    {
        EnsureInit(w, c);
        int ci = ClassIndex(c, cls);
        if (ci < 0) return 0;
        var cc = c.Classes[ci];
        if (MemorizesSpells(w, cls))
        {
            int baseLevel = SpellLevelForClass(w, cls, spell);
            if (domainLevel > 0)
            {
                if (domainLevel > 9) return 0;
                var domains = w.Resources.TwoDA("domains");
                if (!new[] { cc.Domain1, cc.Domain2 }.Any(d => d >= 0
                    && domains.GetInt(d, $"Level_{domainLevel}", -1) == spell)) return 0;
                baseLevel = domainLevel;
            }
            if (baseLevel < 0 || baseLevel > 9) return 0;
            // This reporting API checks the requested metamagic feat and slot level,
            // but not the stored metamagic/SpellDomain marker or the spell's mask.
            // Actual casting remains stricter in SpellSlots.FindMemorized.
            int requestedMeta = meta == CastSlots.MetaAny ? NwRules.MetaNone : meta;
            if (requestedMeta != NwRules.MetaNone
                && !HasFeat(w, c, CastSlots.MetaFeat(requestedMeta), ignoreUses: true)) return 0;
            int preparedLevel = baseLevel + MetaMagicCost(w, requestedMeta);
            if (preparedLevel > 9) return 0;
            return cc.Memorized[preparedLevel].Count(m => m.Spell == spell && m.Ready);
        }
        int level = SpellLevelForClass(w, cls, spell);
        if (level < 0 || level > 9 || !cc.KnownSpells[level].Contains(spell)) return 0;
        int slotLevel = level + MetaMagicCost(w, meta);
        return slotLevel > 9 ? 0 : SlotsLeft(w, c, ci, slotLevel);
    }

    /// <summary>Legacy GetHasSpell aggregate; this is not a cast-authorization check.</summary>
    public static int SpellCount(World w, Creature c, int spell)
    {
        EnsureInit(w, c);
        int n = 0;
        // A sub-radial spell counts its master's slots (nwserver ai_talent_ties: Protection from Evil with
        // Protection from Alignment memorised).
        int master = w.Resources.TwoDA("spells").GetInt(spell, "Master", -1);
        if (master >= 0 && master != spell) n += SpellCount(w, c, master);
        for (int ci = 0; ci < c.Classes.Count; ci++)
        {
            var cc = c.Classes[ci];
            if (MemorizesSpells(w, cc.Class))
            {
                n += PreparedSpellCount(w, c, ci, spell);
            }
            else
            {
                int level = SpellLevelForClass(w, cc.Class, spell);
                if (level >= 0 && level <= 9 && cc.KnownSpells[level].Contains(spell)) n += SlotsLeft(w, c, ci, level);
            }
        }
        // Special abilities count once however many uses are left (parity suite hasspell_abilities: the imp's three
        // Invisibility entries read 1 until the last is spent; a wizard with two memorised Mage Armors and three as
        // abilities reads 3, then 2, then 1 until all are gone).
        if (c.SpellAbilities.Any(a => a.Spell == spell && a.Ready)) n++;
        return n;
    }

    /// <summary>DecrementRemainingSpellUses: spends one use from a memorised slot, spontaneous slot or spell-like ability.</summary>
    public static void SpendSpell(World w, Creature c, int spell)
    {
        EnsureInit(w, c);
        for (int ci = 0; ci < c.Classes.Count; ci++)
        {
            var cc = c.Classes[ci];
            if (MemorizesSpells(w, cc.Class))
            {
                for (int lvl = 0; lvl < 10; lvl++)
                {
                    var list = cc.Memorized[lvl];
                    int i = list.FindIndex(m => m.Spell == spell && m.Ready);
                    if (i >= 0) { list[i] = (list[i].Spell, list[i].Meta, false); return; }
                }
            }
            else
            {
                int level = SpellLevelForClass(w, cc.Class, spell);
                if (level >= 0 && level <= 9 && cc.KnownSpells[level].Contains(spell) && SlotsLeft(w, c, ci, level) > 0)
                {
                    SetSlotsLeft(w, c, ci, level, SlotsLeft(w, c, ci, level) - 1);
                    return;
                }
            }
        }
        var ability = c.SpellAbilities.FirstOrDefault(a => a.Spell == spell && a.Ready);
        if (ability != null) ability.Ready = false;
    }

    /// <summary>ReadySpellLevel: 255 readies everything, 0 clears, positive readies that many more, negative un-readies.</summary>
    public static void ReadySpellLevel(World w, Creature c, int level, int cls, int count)
    {
        EnsureInit(w, c);
        if (level < 0 || level > 9) return;
        for (int ci = 0; ci < c.Classes.Count; ci++)
        {
            var cc = c.Classes[ci];
            if (cls != NwRules.ClassInvalid && cc.Class != cls) continue;
            if (!IsSpellCaster(w, cc.Class)) continue;
            if (MemorizesSpells(w, cc.Class))
            {
                var list = cc.Memorized[level];
                if (count == 255) { for (int i = 0; i < list.Count; i++) if (list[i].Spell >= 0) list[i] = (list[i].Spell, list[i].Meta, true); }
                else if (count == 0) { for (int i = 0; i < list.Count; i++) list[i] = (list[i].Spell, list[i].Meta, false); }
                else if (count > 0)
                {
                    for (int i = 0, n = count; i < list.Count && n > 0; i++)
                        if (list[i].Spell >= 0 && !list[i].Ready) { list[i] = (list[i].Spell, list[i].Meta, true); n--; }
                }
                else
                {
                    for (int i = list.Count - 1, n = -count; i >= 0 && n > 0; i--)
                        if (list[i].Ready) { list[i] = (list[i].Spell, list[i].Meta, false); n--; }
                }
            }
            else
            {
                int left = SlotsLeft(w, c, ci, level);
                SetSlotsLeft(w, c, ci, level, count == 255 ? int.MaxValue : count == 0 ? 0 : left + count);
            }
        }
    }

    /// <summary>Readies every spell slot, spontaneous use and spell-like ability (resting).</summary>
    public static void RestoreSpells(World w, Creature c)
    {
        EnsureInit(w, c);
        for (int lvl = 0; lvl < 10; lvl++) ReadySpellLevel(w, c, lvl, NwRules.ClassInvalid, 255);
        c.SpellSlotsLeft.Clear();
        foreach (var a in c.SpellAbilities) a.Ready = true;
    }
}
