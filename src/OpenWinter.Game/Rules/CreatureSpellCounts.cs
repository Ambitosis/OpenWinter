using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

public static partial class CreatureRules
{
    // Native GetHasSpell has legacy counting rules distinct from both
    // GetSpellUsesLeft and actual slot selection.
    static int PreparedSpellCount(World w, Creature c, int classIndex, int spell)
    {
        var cc = c.Classes[classIndex];
        var spells = w.Resources.TwoDA("spells");
        int level = SpellLevelForClass(w, cc.Class, spell);
        if (level < 0) level = spells.GetInt(spell, "Innate", -1);
        if (level is < 0 or > 9) return 0;

        bool converts = w.Resources.TwoDA("classes").GetInt(cc.Class,
            "CanCastSpontaneously", cc.Class == NwRules.ClassCleric ? 1 : 0) != 0;
        if (converts && spells.GetInt(spell, "SpontaneouslyCast") == 1)
            return cc.Memorized[level].Where((slot, index) => slot.Ready
                && !c.DomainSlots.Contains((classIndex, level, index))).Count();

        int Count(int preparedLevel, int? exactMeta = null, bool modified = false) =>
            preparedLevel is < 0 or > 9 ? 0 : cc.Memorized[preparedLevel].Count(m =>
                m.Ready && m.Spell == spell && (!modified || m.Meta != 0)
                && (exactMeta == null || m.Meta == exactMeta));

        int n = Count(level);
        // Each normal metamagic contributes its adjusted level, counting any
        // nonzero stored meta. Thus three +1 variants count one slot three times.
        foreach (int meta in new[] { 1, 2, 4, 8, 16, 32 })
            n += Count(level + MetaMagicCost(w, meta), modified: true);

        var domains = w.Resources.TwoDA("domains");
        for (int domainLevel = 1; domainLevel <= 9; domainLevel++)
        {
            if (domainLevel == level || !(cc.Domain1 >= 0 && domains.GetInt(cc.Domain1, $"Level_{domainLevel}", -1) == spell
                || cc.Domain2 >= 0 && domains.GetInt(cc.Domain2, $"Level_{domainLevel}", -1) == spell)) continue;
            // Duplicate selections of the same level contribute once. Native's
            // exact-domain subcounts include these raw values, even 3/6/7;
            // the complete byte matrix proves that this is not the normal mask.
            n += Count(domainLevel);
            foreach (int meta in new[] { 0, 1, 2, 3, 6, 7 })
                n += Count(domainLevel + MetaMagicCost(w, meta), exactMeta: meta);
        }
        return n;
    }
}
