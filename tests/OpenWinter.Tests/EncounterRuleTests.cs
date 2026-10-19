using OpenWinter.Game.WorldObjects;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Encounter wave sizes against what the original spawned (docs/ENCOUNTERS.md): every walk-in of the visual scenes
/// encounterbudget and encounterparty (out/visual/shots/vis_encounter*/nwn/notes.txt), where six encounters with one
/// list entry each (different CRs) went off at once. MaxCreatures was 25 there. They are replayed through the
/// encounter budget (EncounterSystem.ChooseEntries), which they all agree with.
/// </summary>
public class EncounterRuleTests
{
    static readonly int[] Value = { 0, 1, 2, 5, 9 }; // encdifficulty.2da VALUE

    // label, party member levels, difficulty row, then list CR : creatures spawned.
    static readonly string[] Measured =
    {
        "xp45k 10 2 0.1:25 0.25:25 0.5:25 1.0:18 2.0:12 4.0:5",
        "p1h1 1,1 2 0.1:8 0.25:3 0.5:1 1.0:1 2.0:1 4.0:1 0.05:8 0.17:5 0.25:3 0.5:1 1.0:1 2.0:1",
        "p1h11 1,1,1 2 0.1:8 0.25:3 0.5:1 1.0:1 2.0:1 4.0:1",
        "p1h5 1,5 2 0.1:18 0.25:8 0.5:3 1.0:2 2.0:1 4.0:1 0.05:18 0.17:12 0.25:8 0.5:3 1.0:2 2.0:1",
        "p1h10 1,10 2 0.1:25 0.25:25 0.5:25 1.0:18 2.0:12 4.0:5",
        "l1d0 1 0 0.05:2 0.05:2 0.05:2 0.067:2 0.13:2 0.2:1",
        "l1d1 1 1 0.05:3 0.05:3 0.053:3 0.11:3 0.21:1 0.32:1",
        "l1d2 1 2 0.05:5 0.05:5 0.084:5 0.17:3 0.34:1 0.5:1",
        "l1d3 1 3 0.05:18 0.13:18 0.34:5 0.67:3 1.3:2 2.0:1",
        "l1d4 1 4 0.32:25 0.8:18 2.1:8 4.3:3 8.5:1 13.0:1",
        "l1d4b 1 4 1.0:12 1.5:12 2.0:8 3.0:5 4.0:3 6.0:1",
        "l2d0 2 0 0.05:3 0.05:3 0.05:3 0.094:3 0.19:2 0.28:1",
        "l2d1 2 1 0.05:5 0.05:5 0.075:5 0.15:3 0.3:1 0.45:1",
        "l2d2 2 2 0.05:8 0.05:8 0.12:8 0.24:3 0.48:1 0.71:1",
        "l2d3 2 3 0.071:25 0.18:18 0.48:5 0.95:5 1.9:3 2.9:2",
        "l2d4 2 4 0.45:25 1.1:18 3.0:8 6.0:2 12.0:1 18.0:1",
        "l3d0 3 0 0.05:3 0.05:3 0.067:3 0.13:3 0.27:1 0.4:1",
        "l3d1 3 1 0.05:5 0.05:5 0.11:5 0.21:2 0.42:1 0.64:1",
        "l3d2 3 2 0.05:8 0.063:8 0.17:5 0.34:2 0.67:1 1.0:1",
        "l3d3 3 3 0.1:25 0.25:12 0.67:5 1.3:3 2.7:2 4.0:1",
        "l3d4 3 4 0.64:25 1.6:18 4.3:5 8.5:1 17.0:1 26.0:1",
        "l4d0 4 0 0.05:5 0.05:5 0.095:5 0.19:3 0.38:1 0.57:1",
        "l4d1 4 1 0.05:8 0.056:8 0.15:5 0.3:2 0.6:1 0.9:1",
        "l4d2 4 2 0.05:12 0.089:12 0.24:5 0.48:2 0.95:2 1.4:1",
        "l4d3 4 3 0.14:25 0.36:12 0.95:8 1.9:5 3.8:2 5.7:1",
        "l4d4 4 4 0.91:25 2.3:18 6.1:3 12.0:1 24.0:1 36.0:1",
        "l5d0 5 0 0.05:8 0.05:8 0.13:8 0.27:3 0.54:1 0.8:1",
        "l5d1 5 1 0.05:12 0.08:12 0.21:5 0.42:2 0.85:2 1.3:1",
        "l5d2 5 2 0.051:18 0.13:18 0.34:5 0.67:3 1.3:2 2.0:1",
        "l5d3 5 3 0.2:25 0.51:12 1.3:8 2.7:5 5.4:1 8.1:1",
        "l5d4 5 4 1.3:25 3.2:18 8.6:2 17.0:1 34.0:1 51.0:1",
        "l5d4c 5 4 2.0:25 3.0:18 4.0:12 6.0:5 8.0:2 12.0:1",
        "p5h5 5,5 2 1.0:3 1.5:3 2.0:2 3.0:1 4.0:1 6.0:1",
        "l6d0 6 0 0.05:12 0.077:12 0.2:5 0.41:2 0.82:2 1.2:1",
        "l6d1 6 1 0.05:18 0.12:18 0.32:5 0.65:3 1.3:2 1.9:2",
        "l6d2 6 2 0.077:25 0.19:18 0.51:5 1.0:3 2.1:2 3.1:1",
        "l6d3 6 3 0.31:25 0.77:18 2.1:8 4.1:3 8.2:1 12.0:1",
        "l6d4 6 4 2.0:25 4.9:18 13.0:1 26.0:1 52.0:1 78.0:1",
        "l8d0 8 0 0.071:25 0.18:18 0.47:5 0.95:5 1.9:3 2.8:2",
        "l8d1 8 1 0.11:25 0.28:18 0.75:8 1.5:5 3.0:2 4.5:1",
        "l8d2 8 2 0.18:25 0.45:12 1.2:8 2.4:5 4.8:2 7.2:1",
        "l8d3 8 3 0.72:25 1.8:25 4.8:8 9.6:1 19.0:1 29.0:1",
        "l8d4 8 4 4.6:25 11.0:2 30.0:1 61.0:1 120.0:1 180.0:1",
        "l10d0 10 0 0.17:25 0.41:12 1.1:8 2.2:5 4.4:2 6.6:1",
        "l10d1 10 1 0.26:25 0.66:18 1.8:12 3.5:5 7.0:1 11.0:1",
        "l10d2 10 2 0.42:25 1.0:18 2.8:12 5.6:3 11.0:1 17.0:1",
        "l10d3 10 3 1.7:25 4.2:18 11.0:1 22.0:1 45.0:1 67.0:1",
        "l10d4 10 4 11.0:5 26.0:1 71.0:1 140.0:1 280.0:1 420.0:1",
        "l10d2b 10 2 1.0:18 1.5:18 2.0:12 3.0:8 4.0:5 6.0:2",
        "l12d0 12 0 0.38:25 0.96:25 2.6:12 5.1:3 10.0:1 15.0:1",
        "l12d1 12 1 0.61:25 1.5:25 4.1:8 8.1:1 16.0:1 24.0:1",
        "l12d2 12 2 0.97:25 2.4:25 6.4:5 13.0:1 26.0:1 39.0:1",
        "l12d3 12 3 3.9:25 9.7:5 26.0:1 52.0:1 100.0:1 150.0:1",
        "l12d4 12 4 25.0:1 61.0:1 160.0:1 330.0:1 650.0:1 980.0:1",
        "l15d0 15 0 1.4:25 3.4:25 9.0:2 18.0:1 36.0:1 54.0:1",
        "l15d1 15 1 2.1:25 5.4:18 14.0:1 29.0:1 57.0:1 86.0:1",
        "l15d2 15 2 3.4:25 8.5:8 23.0:1 45.0:1 91.0:1 140.0:1",
        "l15d3 15 3 14.0:2 34.0:1 91.0:1 180.0:1 360.0:1 550.0:1",
        "l15d4 15 4 87.0:1 220.0:1 580.0:1 1200.0:1 2300.0:1 3500.0:1",
        "l20d0 20 0 11.0:8 28.0:1 74.0:1 150.0:1 290.0:1 440.0:1",
        "l20d1 20 1 18.0:1 44.0:1 120.0:1 230.0:1 470.0:1 700.0:1",
        "l20d2 20 2 28.0:1 70.0:1 190.0:1 370.0:1 740.0:1 1100.0:1",
        "l20d3 20 3 110.0:1 280.0:1 740.0:1 1500.0:1 3000.0:1 4500.0:1",
        "l20d4 20 4 710.0:1 1800.0:1 4700.0:1 9400.0:1 19000.0:1 28000.0:1",
        "p1 1 2 0.05:5 0.17:3 0.25:2 0.5:1 1.0:1 2.0:1",
        "p1h1_1 1,1,1 2 0.05:8 0.17:5 0.25:3 0.5:1 1.0:1 2.0:1",
        "p1h1_1_1 1,1,1,1 2 0.05:12 0.17:8 0.25:5 0.5:2 1.0:1 2.0:1",
        "p1h2 1,2 2 0.05:8 0.17:5 0.25:3 0.5:1 1.0:1 2.0:1",
        "p1h3 1,3 2 0.05:8 0.17:5 0.25:3 0.5:1 1.0:1 2.0:1",
        "p1h4 1,4 2 0.05:12 0.17:8 0.25:5 0.5:2 1.0:1 2.0:1",
        "p1h6 1,6 2 0.05:25 0.17:18 0.25:12 0.5:5 1.0:3 2.0:2",
        "p1h8 1,8 2 0.05:25 0.17:25 0.25:25 0.5:12 1.0:8 2.0:5",
        "p5 5 4 0.05:25 0.25:25 1.0:25 2.0:25 4.0:12 8.0:2",
        "p5h1 5,1 4 0.05:25 0.25:25 1.0:25 2.0:25 4.0:12 8.0:2",
        "p5h3 5,3 4 0.05:25 0.25:25 1.0:25 2.0:25 4.0:12 8.0:2",
        "p5h4 5,4 4 0.05:25 0.25:25 1.0:25 2.0:25 4.0:18 8.0:3",
        "p5h5 5,5 4 0.05:25 0.25:25 1.0:25 2.0:25 4.0:18 8.0:3",
        "p5h5_5 5,5,5 4 0.05:25 0.25:25 1.0:25 2.0:25 4.0:25 8.0:5",
        "p5h6 5,6 4 0.05:25 0.25:25 1.0:25 2.0:25 4.0:25 8.0:5",
        "p5h10 5,10 4 0.05:25 0.25:25 1.0:25 2.0:25 4.0:25 8.0:18",
        "p10h10 10,10 4 0.05:25 0.25:25 1.0:25 2.0:25 4.0:25 8.0:25",
        "p10h5_5 10,5,5 4 0.05:25 0.25:25 1.0:25 2.0:25 4.0:25 8.0:18",
    };

    static EncounterState State(int max, int rec, params (float Cr, bool Unique)[] list)
    {
        var st = new EncounterState { MaxCreatures = max, RecCreatures = rec };
        foreach (var (cr, unique) in list) st.Creatures.Add(new EncounterState.Entry("c", cr, unique));
        return st;
    }

    /// <summary>The wave for a party at an encounter difficulty row, from an empty pool.</summary>
    static List<int> Wave(EncounterState st, IReadOnlyList<int> levels, int difficultyRow, int seed = 1)
    {
        st.Pool = EncounterSystem.Budget(EncounterSystem.PartyLevel(levels) + Value[difficultyRow]);
        return EncounterSystem.ChooseEntries(st, 0, new Random(seed));
    }

    [Fact]
    public void WaveSizesMatchTheOriginal()
    {
        int checkedCount = 0;
        foreach (var row in Measured)
        {
            var f = row.Split(' ');
            var levels = f[1].Split(',').Select(int.Parse).ToList();
            foreach (var pair in f.Skip(3))
            {
                var p = pair.Split(':');
                float cr = float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture);
                int expected = int.Parse(p[1]);
                int n = Wave(State(25, 1, (cr, false)), levels, int.Parse(f[2])).Count;
                Assert.True(expected == n, $"{f[0]}: CR {cr} gave {expected} in the original, {n} here");
                checkedCount++;
            }
        }
        Assert.True(checkedCount > 450);
    }

    // The party level: four members of level L make L, one makes L - 3 (truncated toward zero: a level 1 alone is -1).
    [Theory]
    [InlineData(new[] { 1 }, -1)]
    [InlineData(new[] { 2 }, 0)]
    [InlineData(new[] { 3 }, 0)]
    [InlineData(new[] { 1, 1 }, 0)]
    [InlineData(new[] { 1, 1, 1, 1 }, 1)]
    [InlineData(new[] { 5, 4 }, 3)]
    [InlineData(new[] { 5, 10 }, 7)]
    [InlineData(new[] { 10 }, 7)]
    [InlineData(new[] { 10, 10 }, 8)]
    [InlineData(new[] { 7, 7, 7, 7 }, 7)]
    public void PartyLevels(int[] levels, int expected) => Assert.Equal(expected, EncounterSystem.PartyLevel(levels));

    [Fact]
    public void PointValues()
    {
        Assert.Equal(2.25f, EncounterSystem.PointValue(2.9f));
        Assert.Equal(1.5f, EncounterSystem.PointValue(1f));
        Assert.Equal(1f, EncounterSystem.PointValue(0.95f));
        Assert.Equal(1f, EncounterSystem.PointValue(0f));
        Assert.Equal(2f / 3f, EncounterSystem.PointValue(0.34f));
        Assert.Equal(4f / 9f, EncounterSystem.PointValue(0.25f));
        Assert.Equal(8f / 27f, EncounterSystem.PointValue(0.17f));
        Assert.Equal(16f / 81f, EncounterSystem.PointValue(0.1f));
        // A level 10 player alone at Normal: party level 7, budget 1.5^8 = 25.6, eighteen CR 1 (17 fit, one in the slack).
        Assert.Equal(18, Wave(State(30, 1, (1f, false)), new[] { 10 }, 2).Count);
    }

    /// <summary>
    /// Mixed lists for a level 1 player at Impossible (budget 1.5^7) as the original spawned them in encountermix: list
    /// CRs (u = unique) and how many of each entry.
    /// </summary>
    [Theory]
    [InlineData("1 2 3", "1 0 5")]
    [InlineData("3 2 1", "5 0 1")]
    [InlineData("6 0.25", "1 16")]
    [InlineData("8 10", "1 0")]
    [InlineData("1u 2 4u", "0 6 1")]
    [InlineData("4u 4u 4u", "1 1 1")]
    [InlineData("0.5 1 2 3 4 5", "0 0 0 1 0 2")]
    [InlineData("1 3", "1 5")]
    [InlineData("2u 2u", "1 1")]
    [InlineData("5 3", "2 1")]
    public void MixedListsMatchTheOriginal(string list, string counts)
    {
        var st = State(25, 1, list.Split(' ').Select(e => (float.Parse(e.TrimEnd('u'), System.Globalization.CultureInfo.InvariantCulture), e.EndsWith('u'))).ToArray());
        var picks = Wave(st, new[] { 1 }, 4);
        var got = Enumerable.Range(0, st.Creatures.Count).Select(i => picks.Count(p => p == i));
        Assert.Equal(counts, string.Join(" ", got));
    }

    [Fact]
    public void EqualEntriesShareTheWaveAndRecFillsWithTheLightest()
    {
        Assert.Equal(12, Wave(State(25, 1, (1f, false), (1f, false)), new[] { 1 }, 4, seed: 3).Count); // the original: 5 and 7
        // c5r3: three CR 5 for a level 1 player at Normal (budget 1); uniq: a unique entry three times.
        Assert.Equal(3, Wave(State(25, 3, (5f, false)), new[] { 1 }, 2).Count);
        Assert.Equal(3, Wave(State(25, 3, (1f, true)), new[] { 1 }, 2).Count);
    }

    /// <summary>
    /// Respawns (encounterwaves): the pool keeps what a wave left, the points in play keep what dead creatures of a lower
    /// real CR left, and the reset clears both after its wave.
    /// </summary>
    [Fact]
    public void PointsInPlayShrinkTheRespawn()
    {
        // List CR 3 for CR 1 creatures, RecCreatures 3, a level 1 player at Impossible: 5, then 3, then 5 again.
        var st = State(50, 3, (3f, false));
        float budget = EncounterSystem.Budget(EncounterSystem.PartyLevel(new[] { 1 }) + Value[4]);
        int Spawn(bool reset)
        {
            st.Pool += budget;
            var picks = EncounterSystem.ChooseEntries(st, 0, new Random(1));
            if (st.Pool < EncounterSystem.PointValue(3f)) st.Pool = 0;
            foreach (var _ in picks)
            {
                st.Pool = Math.Max(0, st.Pool - EncounterSystem.PointValue(3f));
                st.PointsInPlay += EncounterSystem.PointValue(3f);
            }
            // All killed: each takes its own CR 1 out.
            st.PointsInPlay = Math.Max(0, st.PointsInPlay - picks.Count * EncounterSystem.PointValue(1f));
            if (reset) st.Pool = st.PointsInPlay = 0;
            return picks.Count;
        }
        Assert.Equal(5, Spawn(false));
        Assert.Equal(3, Spawn(true));
        Assert.Equal(5, Spawn(true));
    }
}
