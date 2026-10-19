using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Gameplay;
using Xunit;

namespace OpenWinter.Tests;

public sealed class FootstepTests
{
    [DataFact]
    public void InstalledTerrainAndFootstepTypesSelectDifferentExistingSounds()
    {
        var rm = new ResMan(TestData.Paths); rm.AddBaseGame();
        var surfaces = rm.TwoDA("surfacemat"); var sounds = rm.TwoDA("footstepsounds");
        string Sound(int surface, int type) => sounds.Get(type, FootstepSounds.Column(surfaces.Get(surface, "Sound"), surface) + "0");
        Assert.Equal("fs_stone_hard1", Sound(4, 0));
        Assert.Equal("fs_wood_hard1", Sound(5, 0));
        Assert.Equal("fs_stone_soft1", Sound(4, 3));
        Assert.Equal("fs_stone_hoof1", Sound(4, 4));
        Assert.True(Sound(4, 12) is "" or "****");
        foreach (int surface in new[] { 1, 3, 4, 5, 6, 9, 10, 11, 14, 19, 20, 22 })
            Assert.NotNull(rm.Read(Sound(surface, 0), ResType.Wav));
    }

    [DataFact]
    public void InstalledMovementClipsCarryFootContactsIncludingTurningAndStrafing()
    {
        var rm = new ResMan(TestData.Paths); rm.AddBaseGame(); var cache = new ModelCache(rm);
        var clips = cache.AllAnimations(cache.Get("a_ba")!).ToDictionary(a => a.Item1, a => a.Item2);
        foreach (string clip in new[] { "walk", "run", "pauseturn", "pausewalkl", "pausewalkr" })
            Assert.Equal(2, clips[clip].Events.Where(e => e.Name == "snd_footstep").Select(e => e.Time).Distinct().Count());
        Assert.Equal(new[] { 0.2f, 0.4f }, clips["pauseturn"].Events.Where(e => e.Name == "snd_footstep").Select(e => e.Time).Distinct().Order().ToArray());
    }
}
