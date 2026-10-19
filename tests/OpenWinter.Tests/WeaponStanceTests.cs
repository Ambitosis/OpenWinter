using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Combat;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Which ready stance, swings and arm clips each held weapon gives, as measured on the original with public
/// ReplaceObjectAnimation (parity/visual scenes weaponstance1..3 and weaponready1..6).
/// </summary>
public class WeaponStanceTests
{
    const int Small = 2, Medium = 3;
    const int Shortsword = 0, Longsword = 1, LightFlail = 4, HeavyCrossbow = 6, LightCrossbow = 7, Longbow = 8, LightMace = 9,
        Halberd = 10, TwoBladed = 12, Greatsword = 13, SmallShield = 14, Torch = 15, Greataxe = 18, Dagger = 22, Club = 28,
        DireMace = 32, HeavyFlail = 35, MagicStaff = 45, Morningstar = 47, Quarterstaff = 50, Scythe = 55, ShortSpear = 58,
        Sling = 61, ThrowingAxe = 63, BastardSword = 3, Trident = 95;

    static OpenWinter.Aurora.Formats.TwoDA BaseItems()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        return resources.TwoDA("baseitems");
    }

    static WeaponStances.Stance For(int size, int right, int left = -1, bool chain = false) =>
        WeaponStances.For(BaseItems(), size, right, left, _ => chain);

    [DataTheory]
    [InlineData(Dagger)]
    [InlineData(Shortsword)]
    [InlineData(Longsword)]
    [InlineData(LightMace)]
    [InlineData(Morningstar)]
    [InlineData(LightFlail)]
    [InlineData(Club)]
    [InlineData(BastardSword)]
    public void OneHandersStandIn1hReadyAndCarryTheArmOut(int weapon)
    {
        var s = For(Medium, weapon);
        Assert.Equal("1h", s.Prefix);
        Assert.Equal("1hreadyr", s.Ready);
        Assert.Empty(s.Idle);
        Assert.Equal(new[] { "walk_swordr" }, s.Walk);
        Assert.Equal(new[] { "run_swordr" }, s.Run);
    }

    [DataTheory]
    [InlineData(Halberd)]
    [InlineData(Scythe)]
    [InlineData(ShortSpear)]
    [InlineData(Quarterstaff)]
    [InlineData(MagicStaff)]
    [InlineData(TwoBladed)]
    [InlineData(DireMace)]
    public void PolearmsStavesAndDoubleWeaponsUseThePolearmSet(int weapon)
    {
        var s = For(Medium, weapon);
        Assert.Equal("pl", s.Prefix);
        Assert.Equal("plreadyr", s.Ready);
        Assert.Equal(new[] { "plpause1" }, s.Idle);
        Assert.Equal(new[] { "plpause1" }, s.Walk);
    }

    [DataTheory]
    [InlineData(Greatsword)]
    [InlineData(Greataxe)]
    [InlineData(Trident)]
    public void WeaponsLargerThanTheCreatureAreTwoHanded(int weapon)
    {
        var s = For(Medium, weapon);
        Assert.Equal("2h", s.Prefix);
        Assert.Equal(new[] { "plpause1" }, s.Idle);
    }

    [DataFact]
    public void SizeDecidesForSmallCreaturesAndFlailsKeepTheirOwnChain()
    {
        Assert.Equal("1h", For(Small, Shortsword).Prefix);
        Assert.Equal("2h", For(Small, Club).Prefix);
        Assert.Equal(new[] { "plpause1" }, For(Small, Club).Idle);
        var star = For(Small, Morningstar, chain: true);
        Assert.Equal("2h", star.Prefix);
        Assert.Empty(star.Idle);
        Assert.Empty(star.Walk);
        var flail = For(Medium, HeavyFlail, chain: true);
        Assert.Equal("2hreadyr", flail.Ready);
        Assert.Empty(flail.Idle);
    }

    [DataFact]
    public void OffHandItemsAndUnarmed()
    {
        var dual = For(Medium, Shortsword, Dagger);
        Assert.Equal("2w", dual.Prefix);
        Assert.Equal(new[] { "walk_swordl" }, dual.Walk.Where(c => c.EndsWith('l')).ToArray());
        var shield = For(Medium, Longsword, SmallShield);
        Assert.Equal("1h", shield.Prefix);
        Assert.Contains("walk_shieldl", shield.Walk);
        Assert.Contains("walk_swordr", shield.Walk);
        var torch = For(Medium, -1, Torch);
        Assert.Equal("nw", torch.Prefix);
        Assert.Equal("nwreadyr", torch.Ready);
        Assert.Empty(torch.Walk);
    }

    [DataFact]
    public void RangedWeapons()
    {
        var bow = For(Medium, Longbow);
        Assert.Equal(("bowrdy", "bowshot"), (bow.Ready, bow.Shot));
        Assert.Equal(new[] { "walk_bowl" }, bow.Walk);
        foreach (int xbow in new[] { LightCrossbow, HeavyCrossbow })
        {
            var s = For(Medium, xbow);
            Assert.Equal(("xbowrdy", "xbowshot"), (s.Ready, s.Shot));
            Assert.Equal(new[] { "xbowr" }, s.Idle);
        }
        foreach (int thrown in new[] { Sling, ThrowingAxe })
            Assert.Equal(("1hreadyr", "throwr"), (For(Medium, thrown).Ready, For(Medium, thrown).Shot));
    }

    [DataFact]
    public void SwingsFollowTheStanceAndTheSlashWeights()
    {
        var items = BaseItems();
        var halberd = For(Medium, Halberd);
        // halberd: %AnimSlashL 35, R 35, S 65
        Assert.Equal("plslashl", WeaponStances.Swing(halberd, items, Halberd, 0));
        Assert.Equal("plslashr", WeaponStances.Swing(halberd, items, Halberd, 35));
        Assert.Equal("plstab", WeaponStances.Swing(halberd, items, Halberd, 70));
        // maces never stab (100/100/0)
        var mace = For(Medium, LightMace);
        for (int roll = 0; roll < 200; roll++) Assert.NotEqual("1hstab", WeaponStances.Swing(mace, items, LightMace, roll));
    }
}
