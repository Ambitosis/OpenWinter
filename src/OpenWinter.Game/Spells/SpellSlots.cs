using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Spells;

/// <summary>Where a cast spell comes from.</summary>
public enum SlotKind { Memorized, Known, Ability }

/// <summary>A spell slot (memorised, spontaneous or spell-like ability) chosen for a cast.</summary>
public sealed class SpellSlot
{
    public SlotKind Kind;
    /// <summary>Index into Creature.Classes, or -1 for spell-like abilities.</summary>
    public int ClassIndex = -1;
    /// <summary>CLASS_TYPE_* cast as (255 for spell-like abilities).</summary>
    public int Class = NwRules.ClassInvalid;
    /// <summary>Level of the slot spent (includes metamagic adjustment).</summary>
    public int SlotLevel;
    /// <summary>Index in the memorised list (memorised slots only).</summary>
    public int SlotIndex = -1;
    /// <summary>The spell's own level as cast (GetLastSpellLevel).</summary>
    public int SpellLevel;
    public int Meta;
    public bool Spontaneous, Domain;
    public SpellAbility? Ability;
    public int CasterLevel;
}

/// <summary>
/// Picks and spends the slot a spell is cast from, following ActionCastSpellAtObject's rules:
/// a specific class (or the first class that has it, then spell-like abilities), METAMAGIC_ANY or an exact
/// metamagic, domain slots, and spontaneous conversion (clerics' cure/inflict). Reuses the memorisation
/// state <see cref="CreatureRules"/> keeps (CreatureSpellbook).
/// </summary>
public static class SpellSlots
{
    public const int MetaAny = 255;

    /// <summary>FEAT_* each metamagic needs.</summary>
    public static int MetaFeat(int meta) => meta switch
    {
        NwRules.MetaEmpower => 11,
        NwRules.MetaExtend => 12,
        NwRules.MetaMaximize => 25,
        NwRules.MetaQuicken => 29,
        NwRules.MetaSilent => 33,
        NwRules.MetaStill => 37,
        _ => -1,
    };

    static bool CanSpontaneouslyConvert(World w, int cls)
    {
        var raw = w.Resources.TwoDA("classes").Get(cls, "CanCastSpontaneously");
        return raw.Length > 0 ? raw != "0" : cls == NwRules.ClassCleric;
    }

    /// <summary>Caster level of a class including prestige progression and the class's CLMultiplier.</summary>
    public static int ClassCasterLevel(World w, Creature c, int cls)
    {
        int cl = CreatureRules.CasterLevel(w, c, cls);
        float mult = w.Resources.TwoDA("classes").GetFloat(cls, "CLMultiplier", 1f);
        if (mult > 0 && Math.Abs(mult - 1f) > 1e-4f) cl = Math.Max(1, (int)(cl * mult));
        return cl;
    }

    /// <summary>
    /// The slot to cast <paramref name="spell"/> from, or null when the creature can't cast it.
    /// <paramref name="cls"/>: -1 any class then spell-like abilities, 255 spell-like abilities only, else that class.
    /// <paramref name="anyMetaInClass"/>: a player's own cast, where a class with METAMAGIC_ANY takes any of that class's slots.
    /// </summary>
    public static SpellSlot? Find(World w, Creature c, int spell, int meta, int cls, int domainLevel, bool spontaneous, bool anyMetaInClass = false)
    {
        CreatureRules.EnsureInit(w, c);
        if (cls == NwRules.ClassInvalid) return FindAbility(c, spell);
        // A script naming a class needs a specific metamagic: with METAMAGIC_ANY the original finds no slot.
        if (cls >= 0 && meta == MetaAny && !spontaneous && !anyMetaInClass) return null;
        for (int ci = 0; ci < c.Classes.Count; ci++)
        {
            var cc = c.Classes[ci];
            if (cls >= 0 && cc.Class != cls) continue;
            if (!CreatureRules.IsSpellCaster(w, cc.Class)) continue;
            var slot = CreatureRules.MemorizesSpells(w, cc.Class)
                ? FindMemorized(w, c, ci, spell, meta, domainLevel, spontaneous)
                : FindKnown(w, c, ci, spell, meta);
            if (slot != null)
            {
                slot.CasterLevel = ClassCasterLevel(w, c, cc.Class);
                return slot;
            }
        }
        return cls < 0 ? FindAbility(c, spell) : null;
    }

    /// <summary>
    /// The domain level for a cast that names none (HUD requests, talents): 0 while an ordinary slot holds
    /// the spell, else the lowest domain level with a ready domain slot for it, else 0.
    /// </summary>
    public static int InferDomainLevel(World w, Creature c, int spell, int meta, int cls, bool anyMetaInClass = false)
    {
        if (Find(w, c, spell, meta, cls, 0, false, anyMetaInClass) != null) return 0;
        for (int level = 1; level <= 9; level++)
            if (Find(w, c, spell, meta, cls, level, false, anyMetaInClass) != null) return level;
        return 0;
    }

    static SpellSlot? FindAbility(Creature c, int spell)
    {
        var a = c.SpellAbilities.FirstOrDefault(x => x.Spell == spell && x.Ready);
        return a == null ? null : new SpellSlot { Kind = SlotKind.Ability, Ability = a, CasterLevel = a.CasterLevel, SpellLevel = -1 };
    }

    static bool MetaMatches(int wanted, int have) => wanted == MetaAny || wanted == have;

    // SetMemorizedSpell can store unsupported combinations. The original rejects them when casting.
    static bool AllowsMeta(World w, int spell, int meta) =>
        meta == NwRules.MetaNone || (SpellTable.MetaMagicMask(w, spell) & meta) == meta;

    static SpellSlot? FindMemorized(World w, Creature c, int ci, int spell, int meta, int domainLevel, bool spontaneous)
    {
        var cc = c.Classes[ci];
        int level = CreatureRules.SpellLevelForClass(w, cc.Class, spell);
        if (spontaneous)
        {
            if (!SpellTable.SpontaneouslyCast(w, spell) || !CanSpontaneouslyConvert(w, cc.Class) || level < 0) return null;
            int m = meta == MetaAny ? NwRules.MetaNone : meta;
            if (!AllowsMeta(w, spell, m)) return null;
            if (m != NwRules.MetaNone && !CreatureRules.HasFeat(w, c, MetaFeat(m), ignoreUses: true)) return null;
            int slotLevel = level + CreatureRules.MetaMagicCost(w, m);
            if (slotLevel > 9) return null;
            var list = cc.Memorized[slotLevel];
            for (int i = 0; i < list.Count; i++)
                if (list[i].Spell >= 0 && list[i].Ready && !c.DomainSlots.Contains((ci, slotLevel, i)))
                    return new SpellSlot
                    {
                        Kind = SlotKind.Memorized, ClassIndex = ci, Class = cc.Class, SlotLevel = slotLevel, SlotIndex = i,
                        SpellLevel = level, Meta = m, Spontaneous = true,
                    };
            return null;
        }
        // The native domain argument names the spell's base level in a selected
        // domain. The stored SpellDomain marker does not authorize/restrict a cast.
        bool domain = domainLevel > 0;
        if (domain)
        {
            if (domainLevel > 9) return null;
            var domains = w.Resources.TwoDA("domains");
            if (!new[] { cc.Domain1, cc.Domain2 }.Any(d => d >= 0
                && domains.GetInt(d, $"Level_{domainLevel}", -1) == spell)) return null;
            level = domainLevel;
        }
        if (level < 0) return null;
        for (int lvl = 0; lvl < 10; lvl++)
        {
            var list = cc.Memorized[lvl];
            for (int i = 0; i < list.Count; i++)
            {
                var m = list[i];
                if (m.Spell != spell || !m.Ready || !MetaMatches(meta, m.Meta) || !AllowsMeta(w, spell, m.Meta)) continue;
                if (lvl != level + CreatureRules.MetaMagicCost(w, m.Meta)) continue;
                return new SpellSlot
                {
                    Kind = SlotKind.Memorized, ClassIndex = ci, Class = cc.Class, SlotLevel = lvl, SlotIndex = i,
                    SpellLevel = level, Meta = m.Meta, Domain = domain,
                };
            }
        }
        return null;
    }

    static SpellSlot? FindKnown(World w, Creature c, int ci, int spell, int meta)
    {
        var cc = c.Classes[ci];
        int level = CreatureRules.SpellLevelForClass(w, cc.Class, spell);
        if (level < 0 || level > 9 || !cc.KnownSpells[level].Contains(spell)) return null;
        int m = meta == MetaAny ? NwRules.MetaNone : meta;
        if (!AllowsMeta(w, spell, m)) return null;
        if (m != NwRules.MetaNone && !CreatureRules.HasFeat(w, c, MetaFeat(m), ignoreUses: true)) return null;
        int slotLevel = level + CreatureRules.MetaMagicCost(w, m);
        if (slotLevel > 9 || CreatureRules.SlotsLeft(w, c, ci, slotLevel) <= 0) return null;
        return new SpellSlot { Kind = SlotKind.Known, ClassIndex = ci, Class = cc.Class, SlotLevel = slotLevel, SpellLevel = level, Meta = m };
    }

    /// <summary>Uses up a slot found by <see cref="Find"/>.</summary>
    public static void Spend(World w, Creature c, SpellSlot slot)
    {
        switch (slot.Kind)
        {
            case SlotKind.Memorized:
                if (slot.ClassIndex < 0 || slot.ClassIndex >= c.Classes.Count) return;
                var list = c.Classes[slot.ClassIndex].Memorized[slot.SlotLevel];
                if (slot.SlotIndex >= 0 && slot.SlotIndex < list.Count)
                    list[slot.SlotIndex] = (list[slot.SlotIndex].Spell, list[slot.SlotIndex].Meta, false);
                break;
            case SlotKind.Known:
                if (slot.ClassIndex < 0 || slot.ClassIndex >= c.Classes.Count) return;
                int left = CreatureRules.SlotsLeft(w, c, slot.ClassIndex, slot.SlotLevel);
                c.SpellSlotsLeft[(slot.ClassIndex, slot.SlotLevel)] = Math.Max(0, left - 1);
                break;
            case SlotKind.Ability:
                if (slot.Ability != null) slot.Ability.Ready = false;
                break;
        }
    }

    /// <summary>Gives back a slot <see cref="Spend"/> used (a spell interrupted by damage keeps its preparation).</summary>
    public static void Restore(World w, Creature c, SpellSlot slot)
    {
        switch (slot.Kind)
        {
            case SlotKind.Memorized:
                if (slot.ClassIndex < 0 || slot.ClassIndex >= c.Classes.Count) return;
                var list = c.Classes[slot.ClassIndex].Memorized[slot.SlotLevel];
                if (slot.SlotIndex >= 0 && slot.SlotIndex < list.Count)
                    list[slot.SlotIndex] = (list[slot.SlotIndex].Spell, list[slot.SlotIndex].Meta, true);
                break;
            case SlotKind.Known:
                if (slot.ClassIndex < 0 || slot.ClassIndex >= c.Classes.Count) return;
                int left = CreatureRules.SlotsLeft(w, c, slot.ClassIndex, slot.SlotLevel);
                c.SpellSlotsLeft[(slot.ClassIndex, slot.SlotLevel)] = left + 1;
                break;
            case SlotKind.Ability:
                if (slot.Ability != null) slot.Ability.Ready = true;
                break;
        }
    }

    /// <summary>
    /// Caster level for a cast without a slot (bCheat, placeables): the best caster level among the
    /// creature's classes that have the spell, else any casting class, else the minimum level that can cast
    /// the spell's innate level.
    /// </summary>
    public static int CheatCasterLevel(World w, GameObject caster, int spell)
    {
        if (caster is Creature c && c.Classes.Count > 0)
        {
            int best = 0, any = 0;
            foreach (var cc in c.Classes)
            {
                if (!CreatureRules.IsSpellCaster(w, cc.Class)) continue;
                int cl = ClassCasterLevel(w, c, cc.Class);
                any = Math.Max(any, cl);
                if (CreatureRules.SpellLevelForClass(w, cc.Class, spell) >= 0) best = Math.Max(best, cl);
            }
            if (best > 0) return best;
            if (any > 0) return any;
        }
        return MinimumCasterLevel(w, spell);
    }

    /// <summary>Lowest caster level that can cast a spell of its innate level (cantrips 1, then 2 x level - 1).</summary>
    public static int MinimumCasterLevel(World w, int spell)
    {
        int innate = SpellTable.Innate(w, spell);
        return innate <= 0 ? 1 : innate * 2 - 1;
    }
}
