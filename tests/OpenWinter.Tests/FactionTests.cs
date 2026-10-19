using OpenWinter.Game;
using Xunit;

namespace OpenWinter.Tests;

public class FactionTests
{
    /// <summary>The standard factions: Hostile hates players, Commoners are neutral, Defenders like them.</summary>
    [DataFact]
    public void StandardFactionsFeelAsExpectedAboutPlayers()
    {
        var f = new FactionTable();
        f.Load(TestData.Module);
        Assert.True(f.FactionReputation(FactionTable.Hostile, FactionTable.PlayerFaction) <= 10);
        Assert.InRange(f.FactionReputation(FactionTable.Commoner, FactionTable.PlayerFaction), 11, 100);
        Assert.True(f.FactionReputation(FactionTable.Hostile, FactionTable.Commoner) <= 10);
    }
}
