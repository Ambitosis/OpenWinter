using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Progression;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// The familiar's record on the level-up familiar step, as the original showed it for a wizard reaching level 2
/// (menudrive): unarmed, AC 9 and saves -1 whatever the creature, the blueprint's hit points, attack
/// from base attack and Strength (Dexterity with Weapon Finesse), damage 1d3 plus Strength.
/// </summary>
public class FamiliarRecordTests
{
    static JsonElement Record(World w, int id, int level)
    {
        var request = JsonDocument.Parse($"{{\"table\":\"hen_familiar\",\"id\":{id},\"level\":{level}}}").RootElement;
        var json = JsonSerializer.Serialize(ChargenRequests.AssociateSheet(w, request));
        return JsonDocument.Parse(json).RootElement.GetProperty("record").GetProperty("sheet").Clone();
    }

    [DataTheory]
    [InlineData(0, 9, 1, 3, -4)]  // bat: Animal (2), Str 3, Dex 15, Weapon Finesse: "+3", "1-3 - 4"
    [InlineData(1, 13, 2, 6, 3)]  // panther: Rogue (3), Str 16, Dex 19: "+6", "1-3 + 3"
    [InlineData(2, 13, 3, 4, 1)]  // hell hound: Outsider (3), Str 13: "+4", "1-3 + 1"
    [InlineData(3, 7, 2, 5, 0)]   // imp: Outsider (2), Dex 17, Weapon Finesse: "+5", "1-3"
    [InlineData(4, 7, 2, 2, 0)]   // fire mephit: Outsider (2), Str 10, Dex 13 (no finesse): "+2", Base Attack 2
    public void Matches_the_originals_familiar_step(int id, int hp, int bab, int attack, int flat)
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var w = new World(rm);
        var s = Record(w, id, 2);
        Assert.Equal(9, s.GetProperty("ac").GetInt32());
        Assert.Equal(hp, s.GetProperty("maxHp").GetInt32());
        Assert.Equal(hp, s.GetProperty("hp").GetInt32());
        Assert.Equal(bab, s.GetProperty("bab").GetInt32());
        foreach (var save in new[] { "fortitude", "reflex", "will" })
            Assert.Equal(-1, s.GetProperty("saves").GetProperty(save).GetInt32());
        Assert.Equal(0, s.GetProperty("spellResistance").GetInt32());
        var main = s.GetProperty("weapons").GetProperty("main");
        Assert.Equal("", main.GetProperty("name").GetString());
        Assert.Equal(attack, main.GetProperty("attack").GetInt32());
        Assert.Equal(1, main.GetProperty("diceMin").GetInt32());
        Assert.Equal(3, main.GetProperty("diceMax").GetInt32());
        Assert.Equal(flat, main.GetProperty("flat").GetInt32());
    }
}
