using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Society;

/// <summary>
/// LevelUpHenchman: gives a creature one more level, chosen from its package, when it has the experience for it.
/// Works on player characters too (Hordes of the Underdark's "Automatic" level-up levels the PC this way).
/// </summary>
public static class HenchmanLevels
{
    public const int ClassInvalid = 255, MaxLevel = 40, MaxClasses = 3;

    /// <summary>Returns the creature's new total level, or 0 on failure.</summary>
    public static int LevelUp(World world, Creature c, int cls, bool readyAllSpells, int package)
    {
        if (c.IsDestroyed) return 0;
        int level = c.TotalLevel;
        if (level >= MaxLevel) return 0;
        var exp = world.Resources.TwoDA("exptable");
        if (exp.RowCount > level)
        {
            int needed = exp.GetInt(level, "XP", -1);
            if (needed >= 0 && c.Experience < needed) return 0;
        }
        if (cls != ClassInvalid && cls >= 0 && c.Classes.All(k => k.Class != cls) && c.Classes.Count >= MaxClasses) return 0;
        // Feats, skills, spells and hit points follow the class package (Progression rules).
        int before = c.TotalLevel;
        int after = Progression.PackageBuilder.LevelUpNpc(world, c, cls, package, readyAllSpells);
        if (after <= before) return 0;
        if (c.IsPC)
        {
            world.Output?.Notify("xp-changed", c, new
            {
                xp = c.Experience,
                level = after,
                nextLevelXp = after < Rules.CreatureRules.MaxLevel ? Rules.CreatureRules.XPForLevel(world, after + 1) : -1,
                canLevelUp = Rules.CreatureRules.CanLevelUp(world, c),
                levelLost = false,
            }, c);
            world.MarkChanged(c);
        }
        return after;
    }
}
