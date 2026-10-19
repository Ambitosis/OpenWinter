using OpenWinter.Aurora.Models;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Views.WorldFx.Logic;
using Xunit;

namespace OpenWinter.Tests;

public class EffectAnimationTests
{
    [Fact]
    public void LastingCessationOnlyModelDoesNotPlayItsEndingAtApplication()
    {
        Assert.True(EffectAnimation.WaitsForCessation("duration", new[] { "cessation" }));
        Assert.True(EffectAnimation.WaitsForCessation("duration", new[] { "CESSATION", "fade" }));
        Assert.False(EffectAnimation.WaitsForCessation("impact", new[] { "cessation" }));
    }

    [Theory]
    [InlineData("impact")]
    [InlineData("duration")]
    [InlineData("default")]
    [InlineData("conjure01")]
    public void AuthoredStartOrFallbackPhaseStillPlays(string phase)
        => Assert.False(EffectAnimation.WaitsForCessation("duration", new[] { phase, "cessation" }));

    [Fact]
    public void ConjuringKeepsItsExistingFallback()
        => Assert.False(EffectAnimation.WaitsForCessation("", new[] { "conjure01", "fade" }));

    [DataFact]
    public void InstalledCessationModelsOnlyHaveEndAnimations()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var models = new ModelCache(resources);
        foreach (var name in new[] { "vce_neutral", "vce_positive", "vce_negative" })
        {
            var model = models.Get(name)!;
            Assert.NotNull(model);
            Assert.True(EffectAnimation.WaitsForCessation("duration", model.Animations.Keys));
            Assert.All(model.Parts.Where(p => p.Emitter != null && !p.Emitter.Is("Explosion")), p => Assert.Equal(0, p.Emitter!.BirthRate));
            if (name != "vce_neutral") Assert.Contains(model.Parts, p => p.Emitter is { BirthRate: > 0 } e && e.Is("Explosion"));
        }
    }
}
