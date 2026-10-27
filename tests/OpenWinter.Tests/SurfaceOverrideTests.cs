using OpenWinter.Client.Views.WorldFx.Logic;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>ReplaceObjectTexture and SetMaterialShaderUniform* on one module-shader surface (ObjectFx).</summary>
public class SurfaceOverrideTests
{
    sealed record M(string Name);

    static readonly M Shared = new("shared");

    static M? Retexture(M m, string r) => new($"{m.Name}+{r}");

    static Func<M, M?> Uniforms(bool any) => m => any ? new M($"{m.Name}#u") : null;

    [Fact]
    public void NothingSetShowsTheSharedMaterial() =>
        Assert.Null(SurfaceOverride.Resolve(Shared, null, Retexture, Uniforms(false)));

    [Fact]
    public void UniformsAreSetOnTheRetexturedMaterial() =>
        Assert.Equal("shared+tex#u", SurfaceOverride.Resolve(Shared, "tex", Retexture, Uniforms(true))!.Name);

    [Fact]
    public void ResettingTheUniformsKeepsTheSwap() =>
        Assert.Equal("shared+tex", SurfaceOverride.Resolve(Shared, "tex", Retexture, Uniforms(false))!.Name);

    [Fact]
    public void RemovingTheSwapKeepsTheUniforms() =>
        Assert.Equal("shared#u", SurfaceOverride.Resolve(Shared, null, Retexture, Uniforms(true))!.Name);

    [Fact]
    public void AReplacementThatCantBeMadeKeepsTheMaterial() =>
        Assert.Equal("shared#u", SurfaceOverride.Resolve(Shared, "missing", (_, _) => null, Uniforms(true))!.Name);
}
