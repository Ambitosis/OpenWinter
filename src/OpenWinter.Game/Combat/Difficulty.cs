using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Combat;

/// <summary>
/// What the game difficulty (<see cref="World.GameDifficulty"/>, GAME_DIFFICULTY_*) does in the engine, read from
/// diffsettings.2da (rows NoCriticalOnPC ... MassiveDamage, one column per difficulty: DMEasy, Easy, Normal, Hardcore,
/// DMPlayers for 0-4; **** counts as 0). Measured on the original (visual scene diffscale at difficulties 1-4, earlier
/// difficulty, difficultyhits and difficultypc; parity suites difficulty_npc1/4; docs/DIFFICULTY.md):
/// <list type="bullet">
/// <item>Only a weapon's own dice change (base item, creature weapon or unarmed dice; each critical roll on its own),
/// and only between a player and a creature that isn't one. A player hitting such a creature: MinPCDamagePercent
/// (P) tops the roll R of dice whose highest total is M up by ((P + random(100 - P)) x M) / 100, capped at M
/// (P 100: always M; P 0: unchanged). Such a creature hitting a player: MaxNPCDamagePercent below 100 takes
/// (M x (100 - P)) / 100 off, leaving at least 1; above 100 adds (M x random(P - 100)) / 100.</item>
/// <item>NoCriticalOnPC: a confirmed critical hit on a player by a creature that isn't one is an ordinary hit.</item>
/// <item>Henchmen, summons and other associates count as creatures that aren't players, on either side.</item>
/// </list>
/// Scripted and spell damage don't change. The rest of what the difficulty descriptions list is done by the stock
/// scripts through GetGameDifficulty (friendly fire in spellsIsTarget, shorter durations and dazes in
/// GetScaledDuration / GetScaledEffect, maximised cure spells).
/// </summary>
public static class Difficulty
{
    public const int VeryEasy = 0, Easy = 1, Normal = 2, CoreRules = 3, Difficult = 4;

    static readonly string[] Columns = { "DMEasy", "Easy", "Normal", "Hardcore", "DMPlayers" };

    /// <summary>One diffsettings.2da switch (by its Name column) at the current difficulty; **** and missing are 0.</summary>
    public static int Setting(World world, string name)
    {
        var t = world.Resources.TwoDA("diffsettings");
        int d = Math.Clamp(world.GameDifficulty, 0, Columns.Length - 1);
        for (int r = 0; r < t.RowCount; r++)
            if (string.Equals(t.Get(r, "Name"), name, StringComparison.OrdinalIgnoreCase))
                return t.GetInt(r, Columns[d], 0);
        return 0;
    }

    static bool IsPlayer(GameObject o) => o is Creature { IsPC: true };

    /// <summary>
    /// The weapon dice roll <paramref name="roll"/> (of dice whose highest total is <paramref name="max"/>) as the
    /// difficulty has it for this attacker and target.
    /// </summary>
    public static int ScaleWeaponDice(World world, Creature attacker, GameObject target, int roll, int max, IDice dice)
    {
        if (max <= 0) return roll;
        if (attacker.IsPC && target is Creature && !IsPlayer(target))
            return PlayerDice(roll, max, Setting(world, "MinPCDamagePercent"), dice);
        if (!attacker.IsPC && IsPlayer(target))
            return NpcDice(roll, max, Setting(world, "MaxNPCDamagePercent"), dice);
        return roll;
    }

    /// <summary>MinPCDamagePercent: a random top-up of P% to 99% of the highest roll, capped at it.</summary>
    public static int PlayerDice(int roll, int max, int percent, IDice dice)
    {
        if (percent <= 0) return roll;
        if (percent * max / 100 == max) return max;
        if (percent >= 100) return roll;
        int random = dice.Roll(100 - percent) - 1;
        return Math.Min(max, roll + (percent + random) * max / 100);
    }

    /// <summary>MaxNPCDamagePercent: below 100 a flat cut (at least 1 left), above 100 a random bonus of up to P - 101 %.</summary>
    public static int NpcDice(int roll, int max, int percent, IDice dice)
    {
        if (percent <= 0 || percent == 100) return roll;
        if (percent < 100) return Math.Max(1, roll - max * (100 - percent) / 100);
        int random = dice.Roll(percent - 100) - 1;
        return roll + max * random / 100;
    }

    /// <summary>NoCriticalOnPC: a confirmed critical hit on a player by a creature that isn't one is an ordinary hit.</summary>
    public static bool SparesCritical(World world, Creature attacker, GameObject target) =>
        IsPlayer(target) && !attacker.IsPC && Setting(world, "NoCriticalOnPC") != 0;
}
