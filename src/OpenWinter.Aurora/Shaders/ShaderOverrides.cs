namespace OpenWinter.Aurora.Shaders;

/// <summary>
/// Functions of the installed shader library that OpenWinter replaces with its own, because Godot does that work
/// itself after the shader has described the surface: lighting from the engine's light list, fog, the keyhole cut
/// (OpenWinter cuts world meshes itself) and the spell targeting highlight (drawn by OpenWinter's interface).
/// Everything else in the library is used as installed. Each entry is the full replacement (every overload).
/// </summary>
public static class ShaderOverrides
{
    public static readonly IReadOnlyDictionary<string, string> Functions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["SetupLights"] = "void SetupLights() {}",
        ["ComputeLighting"] = "void ComputeLighting(inout vec4 ColorIO, vec3 vNormalThis) {}",
        ["ApplyFog"] = "void ApplyFog(inout vec3 ColorIO) {}\nvoid ApplyFog(inout vec4 ColorIO) {}",
        ["ApplyKeyhole"] = "void ApplyKeyhole(inout vec4 ColorIO) {}",
        ["ApplyTargetGroundHighlighting"] = "void ApplyTargetGroundHighlighting(inout vec4 ColorIO) {}",
    };
}
