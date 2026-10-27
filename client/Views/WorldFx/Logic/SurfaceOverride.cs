namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>
/// The material a module-shader surface of an object shows: its shared material, retextured by ReplaceObjectTexture
/// or SetTextureOverride, with the object's SetMaterialShaderUniform* values on its own copy of that. Each layer is
/// worked out from the shared material every time, so neither undoes the other.
/// </summary>
public static class SurfaceOverride
{
    /// <param name="shared">The surface's own material.</param>
    /// <param name="replacement">The texture replacing the surface's bitmap, or null.</param>
    /// <param name="retexture">The material retextured with a replacement (null: it can't be).</param>
    /// <param name="uniforms">The object's copy of a material with its uniforms set (null: it has none for it).</param>
    /// <returns>The material to show over the shared one, or null for the shared material itself.</returns>
    public static T? Resolve<T>(T shared, string? replacement, Func<T, string, T?> retexture, Func<T, T?> uniforms) where T : class
    {
        var swapped = replacement != null ? retexture(shared, replacement) ?? shared : shared;
        var result = uniforms(swapped) ?? swapped;
        return ReferenceEquals(result, shared) ? null : result;
    }
}
