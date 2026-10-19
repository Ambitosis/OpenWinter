using OpenWinter.Game.Combat;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// The game difficulty's weapon-dice formulas (docs/DIFFICULTY.md), as exact distributions over every roll and every
/// random draw, against what the visual scene diffscale measured on the original.
/// </summary>
public class DifficultyScalingTests
{
    sealed class Fixed : IDice
    {
        public int Value;
        public int Roll(int sides) => Value;
    }

    /// <summary>The chance of each scaled value of one die (or, with count, of dice totalled) over all rolls and draws.</summary>
    static Dictionary<int, double> Distribution(int count, int die, Func<int, int, IDice, int> scale, int draws)
    {
        var rolls = new Dictionary<int, double> { [0] = 1 };
        for (int i = 0; i < count; i++)
        {
            var next = new Dictionary<int, double>();
            foreach (var (s, p) in rolls)
                for (int f = 1; f <= die; f++) next[s + f] = next.GetValueOrDefault(s + f) + p / die;
            rolls = next;
        }
        var result = new Dictionary<int, double>();
        var dice = new Fixed();
        foreach (var (r, p) in rolls)
            for (int k = 1; k <= Math.Max(1, draws); k++)
            {
                dice.Value = k;
                int v = scale(r, count * die, dice);
                result[v] = result.GetValueOrDefault(v) + p / Math.Max(1, draws);
            }
        return result;
    }

    [Fact]
    public void EasyPlayerBlowsAreToppedUpToTheirHighest()
    {
        // Unarmed 1d3: not the highest 17 times in 150 (the original: 13 of 122 in diffscale, 3 of 28 in difficultypc).
        var fist = Distribution(1, 3, (r, m, d) => Difficulty.PlayerDice(r, m, 50, d), 50);
        Assert.Equal(17.0 / 150, fist[2], 9);
        Assert.False(fist.ContainsKey(1));
        // Greatsword 2d6: 12 in 90.5% (the original: 39 of 43), never below 8; longsword 1d8: 8 in 81% (36 of 44).
        var sword = Distribution(2, 6, (r, m, d) => Difficulty.PlayerDice(r, m, 50, d), 50);
        Assert.Equal(0.905, sword[12], 3);
        Assert.Equal(8, sword.Keys.Min());
        Assert.Equal(0.81, Distribution(1, 8, (r, m, d) => Difficulty.PlayerDice(r, m, 50, d), 50)[8], 3);
        // 100% (Very Easy's column) is always the highest; 0% leaves the roll.
        Assert.Equal(1.0, Distribution(1, 8, (r, m, d) => Difficulty.PlayerDice(r, m, 100, d), 0)[8], 9);
        Assert.Equal(0.125, Distribution(1, 8, (r, m, d) => Difficulty.PlayerDice(r, m, 0, d), 0)[1], 9);
    }

    [Fact]
    public void NpcBlowsOnPlayersAreCutOrRaised()
    {
        // Easy (50): 1d8 loses 4, at least 1 left: 1 five times in eight; 1d4 loses 2: 1 three times in four.
        var easy8 = Distribution(1, 8, (r, m, d) => Difficulty.NpcDice(r, m, 50, d), 0);
        Assert.Equal(0.625, easy8[1], 9);
        Assert.Equal(0.125, easy8[4], 9);
        Assert.Equal(4, easy8.Keys.Max());
        Assert.Equal(0.75, Distribution(1, 4, (r, m, d) => Difficulty.NpcDice(r, m, 50, d), 0)[1], 9);
        // Very Difficult (200): 0-99% of the highest added, with no cap: 1d8 gives 1..15, mean 7.98.
        var hard8 = Distribution(1, 8, (r, m, d) => Difficulty.NpcDice(r, m, 200, d), 100);
        Assert.Equal(1, hard8.Keys.Min());
        Assert.Equal(15, hard8.Keys.Max());
        Assert.Equal(7.98, hard8.Sum(kv => kv.Key * kv.Value), 9);
        // 100 leaves the roll.
        Assert.Equal(0.125, Distribution(1, 8, (r, m, d) => Difficulty.NpcDice(r, m, 100, d), 0)[1], 9);
    }
}
