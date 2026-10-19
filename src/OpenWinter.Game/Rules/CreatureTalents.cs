using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using CastSlots = OpenWinter.Game.Spells.SpellSlots;

namespace OpenWinter.Game.Rules;

/// <summary>
/// Talents (spells, special abilities, potions, feats and skills a creature can use), as GetCreatureTalentBest /
/// GetCreatureTalentRandom pick them for the stock AI. Measured on nwserver (parity suites ai_talents*, docs/STOCK_AI.md):
/// <list type="bullet">
/// <item>Each usable instance is a talent: every ready memorised slot, every known spell of a spontaneous caster with a
/// slot left at its level, every ready special ability, every potion item (a stack counts once), every usable feat and
/// skill. A spell with sub-radial spells (SubRadSpell1..5) gives one talent per sub-radial spell.</item>
/// <item>A memorised spell with metamagic counts only if the creature has that metamagic feat.</item>
/// <item>Categories are spells.2da / feat.2da Category with 1 and 11 exchanged for spells (category 1 finds the
/// indiscriminate area spells, 11 the discriminate ones); a blank category is 0. A potion's category follows its
/// spell's: healing 4-5 to 17, conditional 6-7 to 18, protection 12-14 to 20, enhancement 8-10 to 21.</item>
/// <item>A spell's rating is (innate level + metamagic level cost) * 2 - 1, half a point less when its level for the
/// class is below its innate level (Resistance). Best = the highest rating not above nCRMax; on a tie the later talent
/// wins, except that sub-radial spells (and anything tying after one) are chosen at random among the ties.</item>
/// <item>Potions ignore nCRMax (the highest-rated potion always wins); feats and skills ignore it too and are chosen at
/// random. nCRMax -1 behaves like GetCreatureTalentRandom; below -1 only potions, feats and skills are found.</item>
/// <item>Random = uniform over the instances in the category.</item>
/// </list>
/// </summary>
public static partial class CreatureRules
{
    /// <summary>
    /// A usable talent instance: TALENT_TYPE_*, id, TALENT_CATEGORY_* and rating. <paramref name="RandomTie"/>: chosen at random
    /// among equal ratings; <paramref name="AnyCR"/>: not limited by nCRMax. <paramref name="Item"/> / <paramref name="Property"/>
    /// for potions, <paramref name="Meta"/> for memorised metamagic.
    /// </summary>
    public readonly record struct TalentInfo(int Type, int Id, int Category, float CR, bool RandomTie = false, bool AnyCR = false,
        uint Item = GameObject.InvalidId, ItemProperty? Property = null, int Meta = 0);

    /// <summary>Talent category of a spells.2da Category value: 1 and 11 are exchanged, blank is 0.</summary>
    static int SpellTalentCategory(int category) => category switch { < 0 => 0, 1 => 11, 11 => 1, _ => category };

    /// <summary>The potion category of a spell's (unswapped) spells.2da Category, or -1.</summary>
    static int PotionCategory(int category) => category switch
    {
        4 or 5 => 17,
        6 or 7 => 18,
        12 or 13 or 14 => 20,
        8 or 9 or 10 => 21,
        _ => -1,
    };

    /// <summary>Every talent instance the creature can use right now, in the order the original considers them.</summary>
    public static IEnumerable<TalentInfo> Talents(World w, Creature c)
    {
        EnsureInit(w, c);
        var spells = w.Resources.TwoDA("spells");

        IEnumerable<TalentInfo> SpellInstances(int spell, int meta, int classLevel, bool fromClass)
        {
            bool any = false;
            for (int i = 1; i <= 5; i++)
            {
                int sub = spells.GetInt(spell, "SubRadSpell" + i, -1);
                if (sub <= 0) continue;
                any = true;
                yield return SpellTalent(sub, meta, classLevel, fromClass) with { RandomTie = true };
            }
            if (!any) yield return SpellTalent(spell, meta, classLevel, fromClass);
        }

        TalentInfo SpellTalent(int spell, int meta, int classLevel, bool fromClass)
        {
            int innate = spells.GetInt(spell, "Innate", 0);
            float cr = (innate + MetaMagicCost(w, meta)) * 2 - 1;
            if (fromClass && classLevel >= 0 && classLevel < innate) cr -= 0.5f;
            return new TalentInfo(NwRules.TalentSpell, spell, SpellTalentCategory(spells.GetInt(spell, "Category", -1)), cr, Meta: meta);
        }

        for (int ci = 0; ci < c.Classes.Count; ci++)
        {
            var cc = c.Classes[ci];
            if (MemorizesSpells(w, cc.Class))
            {
                for (int lvl = 0; lvl < 10; lvl++)
                    foreach (var m in cc.Memorized[lvl])
                    {
                        if (!m.Ready || m.Spell < 0) continue;
                        if (m.Meta != NwRules.MetaNone && !HasFeat(w, c, CastSlots.MetaFeat(m.Meta), ignoreUses: true)) continue;
                        foreach (var t in SpellInstances(m.Spell, m.Meta, SpellLevelForClass(w, cc.Class, m.Spell), true)) yield return t;
                    }
            }
            else
            {
                for (int lvl = 0; lvl < 10; lvl++)
                    if (cc.KnownSpells[lvl].Count > 0 && SlotsLeft(w, c, ci, lvl) > 0)
                        foreach (var s in cc.KnownSpells[lvl])
                            foreach (var t in SpellInstances(s, NwRules.MetaNone, SpellLevelForClass(w, cc.Class, s), true)) yield return t;
            }
        }
        foreach (var a in c.SpellAbilities)
            if (a.Ready)
                foreach (var t in SpellInstances(a.Spell, NwRules.MetaNone, -1, false)) yield return t;

        // Potions in the inventory (not equipped, not inside containers): one talent per item.
        var ipSpells = w.Resources.TwoDA("iprp_spells");
        foreach (var item in c.Inventory.ToList())
        {
            if (item.IsDestroyed || !ItemRules.IsPotion(item.BaseItem)) continue;
            foreach (var p in item.Properties)
            {
                if (p.Type != ItemRules.PropCastSpell || !p.Useable) continue;
                int spell = ipSpells.GetInt(p.SubType, "SpellIndex", -1);
                if (spell < 0) continue;
                int cat = PotionCategory(spells.GetInt(spell, "Category", -1));
                if (cat < 0) continue;
                yield return new TalentInfo(NwRules.TalentSpell, spell, cat, spells.GetInt(spell, "Innate", 0) * 2 - 1,
                    AnyCR: true, Item: item.Id, Property: p);
            }
        }

        var feats = w.Resources.TwoDA("feat");
        foreach (int feat in AllFeats(w, c).ToList())
        {
            if (!HasFeat(w, c, feat)) continue;
            int cat = feats.GetInt(feat, "CATEGORY", -1);
            yield return new TalentInfo(NwRules.TalentFeat, feat, Math.Max(0, cat), 0, RandomTie: true, AnyCR: true);
        }
        var skills = w.Resources.TwoDA("skills");
        for (int s = 0; s < skills.RowCount; s++)
        {
            if (!HasSkill(w, c, s)) continue;
            yield return new TalentInfo(NwRules.TalentSkill, s, Math.Max(0, skills.GetInt(s, "Category", -1)), 0, RandomTie: true, AnyCR: true);
        }
    }

    /// <summary>Spells the creature can cast now: ready memorised slots, known spells with uses, ready spell-like abilities.</summary>
    public static IEnumerable<int> CastableSpells(World w, Creature c)
    {
        for (int ci = 0; ci < c.Classes.Count; ci++)
        {
            var cc = c.Classes[ci];
            if (MemorizesSpells(w, cc.Class))
            {
                for (int lvl = 0; lvl < 10; lvl++)
                    foreach (var m in cc.Memorized[lvl])
                        if (m.Ready && m.Spell >= 0) yield return m.Spell;
            }
            else
            {
                for (int lvl = 0; lvl < 10; lvl++)
                    if (cc.KnownSpells[lvl].Count > 0 && SlotsLeft(w, c, ci, lvl) > 0)
                        foreach (var s in cc.KnownSpells[lvl]) yield return s;
            }
        }
        foreach (var a in c.SpellAbilities)
            if (a.Ready) yield return a.Spell;
    }

    /// <summary>
    /// GetCreatureHasTalent. A spell talent needs a spell instance without metamagic (a memorised copy with metamagic
    /// does not count); potions do not count.
    /// </summary>
    public static bool HasTalent(World w, Creature c, Talent t) => t.Type switch
    {
        NwRules.TalentSpell => Talents(w, c).Any(x => x.Type == NwRules.TalentSpell && x.Item == GameObject.InvalidId
            && x.Meta == NwRules.MetaNone && x.Id == t.Id) || HasMasterSpellTalent(w, c, t.Id),
        NwRules.TalentFeat => HasFeat(w, c, t.Id),
        NwRules.TalentSkill => HasSkill(w, c, t.Id),
        _ => false,
    };

    /// <summary>A sub-radial master spell (its sub-radial spells are the talents) counts for itself too.</summary>
    static bool HasMasterSpellTalent(World w, Creature c, int spell)
    {
        var spells = w.Resources.TwoDA("spells");
        if (spells.GetInt(spell, "SubRadSpell1", -1) <= 0) return false;
        return CastableSpells(w, c).Contains(spell);
    }

    static Talent ToTalent(TalentInfo t) => new() { Type = t.Type, Id = t.Id, Category = t.Category, Item = t.Item, Property = t.Property, Meta = t.Meta };
    static Talent NoTalent => new() { Type = -1 };

    /// <summary>GetCreatureTalentRandom: any usable talent instance in the category, uniformly.</summary>
    public static Talent RandomTalent(World w, Creature c, int category, Random? rng = null)
    {
        var list = Talents(w, c).Where(t => t.Category == category).ToList();
        return list.Count == 0 ? NoTalent : ToTalent(list[(rng ?? Random.Shared).Next(list.Count)]);
    }

    /// <summary>GetCreatureTalentBest (rules in the class summary).</summary>
    public static Talent BestTalent(World w, Creature c, int category, int crMax, Random? rng = null)
    {
        if (crMax == -1) return RandomTalent(w, c, category, rng);
        rng ??= Random.Shared;
        TalentInfo? best = null;
        float bestCr = float.NegativeInfinity;
        int ties = 0;
        foreach (var t in Talents(w, c))
        {
            if (t.Category != category || (!t.AnyCR && (t.CR > crMax || crMax < 0))) continue;
            if (t.CR > bestCr)
            {
                best = t;
                bestCr = t.CR;
                ties = t.RandomTie ? 1 : 0;
            }
            else if (t.CR == bestCr)
            {
                if (ties == 0 && !t.RandomTie) best = t;
                else if (rng.Next(++ties) == 0) best = t;
            }
        }
        return best is { } b ? ToTalent(b) : NoTalent;
    }
}
