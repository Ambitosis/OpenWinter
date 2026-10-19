using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Combat;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Which attack clips non-humanoid creature models (the creature animation set) play, as measured on the original
/// with public ReplaceObjectAnimation markers (parity/visual scenes creatureswing, creatureswing2..7 and
/// creaturereach; docs/WEAPON_STANCES.md "Creature models").
/// </summary>
public class CreatureSwingTests
{
    const int CSlash = 69, CPierce = 70, CBludgeon = 71, CSlashPierce = 72;

    static ResMan Resources()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        return resources;
    }

    [Theory]
    [InlineData("S", true)]
    [InlineData("SWT", true)]
    [InlineData("L", true)]
    [InlineData("lw", true)]
    [InlineData("P", false)]
    [InlineData("F", false)]
    [InlineData("FWT", false)]
    [InlineData("", false)]
    public void SimpleAndLargeModelsUseTheCreatureSet(string modelType, bool creature) =>
        Assert.Equal(creature, WeaponStances.CreatureSet(modelType));

    [DataTheory]
    // Dire badger (PREFATCKDIST 2.6): ccloseh rooted at 2.3, 2.45 and 2.6 m, its own 2.1 m stand-off; ca1 at 2.7,
    // 2.8 and 2.9 m and where it stops closing in on a skeleton (2.8).
    [InlineData(2.6f, 2.1f, true)]
    [InlineData(2.6f, 2.3f, true)]
    [InlineData(2.6f, 2.6f, true)]
    [InlineData(2.6f, 2.7f, false)]
    [InlineData(2.6f, 2.8f, false)]
    // Wolf (2.3): ccloseh at 1.4, 1.8 and 2.1 m, ca1 at 2.5 m.
    [InlineData(2.3f, 1.4f, true)]
    [InlineData(2.3f, 2.1f, true)]
    [InlineData(2.3f, 2.5f, false)]
    public void CloseTargetsGetCcloseh(float preferred, float distance, bool close)
    {
        var items = Resources().TwoDA("baseitems");
        for (int roll = 0; roll < 30; roll++)
        {
            string clip = WeaponStances.CreatureSwing(items, CPierce, roll, distance, preferred);
            if (close) Assert.Equal("ccloseh", clip);
            else Assert.Contains(clip, new[] { "ca1slashl", "ca1slashr", "ca1stab" });
        }
    }

    [DataTheory]
    [InlineData(CSlash)]
    [InlineData(CPierce)]
    [InlineData(CBludgeon)]
    [InlineData(CSlashPierce)]
    [InlineData(-1)]
    public void FartherSwingsAreTheThreeCa1ClipsInEqualShares(int weapon)
    {
        // Measured: each of the left claw, right claw and bite alone, and all three, played ca1slashl, ca1slashr and
        // ca1stab alike; the creature weapon base items weigh %AnimSlashL/R/S 50 / 50 / 50.
        var items = Resources().TwoDA("baseitems");
        var counts = Enumerable.Range(0, 300).Select(r => WeaponStances.CreatureSwing(items, weapon, r, 3f, 2.6f))
            .GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(new[] { "ca1slashl", "ca1slashr", "ca1stab" }, counts.Keys.Order());
        Assert.All(counts.Values, n => Assert.InRange(n, 99, 102));
    }

    [Theory]
    [InlineData("nwslashl", "ca1slashl")]
    [InlineData("1hslashr", "ca1slashr")]
    [InlineData("2hstab", "ca1stab")]
    [InlineData("plcloseh", "ccloseh")]
    [InlineData("2wclosel", "cclosel")]
    [InlineData("nwreach", "creach")]
    [InlineData("ca1slashl", "nwslashl")]
    [InlineData("ca2stab", "nwstab")]
    [InlineData("ccloseh", "nwcloseh")]
    [InlineData("creach", "nwreach")]
    [InlineData("nwslasho", null)]
    [InlineData("cdamages", null)]
    [InlineData("bowshot", null)]
    [InlineData("castout", null)]
    public void SwingsHaveTheirCounterpartInTheOtherSet(string clip, string? alias) =>
        Assert.Equal(alias, WeaponStances.SwingAlias(clip));

    [DataTheory]
    [InlineData("c_direbadg")]
    [InlineData("c_badger")]
    [InlineData("c_wolf")]
    [InlineData("c_spidgiant")]
    [InlineData("c_golstone")]
    public void CreatureModelsHaveTheClipsAndTheirBlowsLandHalfWay(string model)
    {
        // The server's names exist on the installed creature models. The client times the weapon sounds by the
        // clip's "hit" event: about half way in, as on the humanoid swings (0.467 s on the giant spider's 0.967 s
        // clips); the dire badger's swings have no events, and the client keeps the humanoid 0.5 s for them.
        var models = new ModelCache(Resources());
        var anims = models.AllAnimations(models.Get(model)!).ToDictionary(a => a.Name, a => a.Animation, StringComparer.OrdinalIgnoreCase);
        foreach (var clip in new[] { "ca1slashl", "ca1slashr", "ca1stab", "ccloseh" })
        {
            Assert.True(anims.ContainsKey(clip), $"{model} has no {clip}");
            var hit = anims[clip].Events.Where(e => e.Name == "hit").Select(e => e.Time).ToList();
            if (model == "c_direbadg") Assert.Empty(hit);
            else Assert.InRange(hit.Min(), 0.45f, 0.5f);
        }
        Assert.True(anims.ContainsKey("creadyr"));
    }
}
