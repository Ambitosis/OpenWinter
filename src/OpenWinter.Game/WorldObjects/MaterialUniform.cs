namespace OpenWinter.Game.WorldObjects;

/// <summary>A SetMaterialShaderUniformInt / Vec4 override: "int" (exact) or "vec4".</summary>
public sealed record MaterialUniform(string Type, float[] Value, int? Exact)
{
    /// <summary>The "material-uniform" notify payload.</summary>
    public object Data(string material, string param) => Exact is { } i
        ? new { material, param, type = Type, value = Value, @int = i }
        : new { material, param, type = Type, value = Value };
}

/// <summary>Material uniform keys: the material (an MTR name) case-insensitive, the parameter exact.</summary>
public sealed class MaterialUniformKeyComparer : IEqualityComparer<(string Material, string Param)>
{
    public static readonly MaterialUniformKeyComparer Instance = new();

    public bool Equals((string Material, string Param) a, (string Material, string Param) b) =>
        string.Equals(a.Material, b.Material, StringComparison.OrdinalIgnoreCase) && string.Equals(a.Param, b.Param, StringComparison.Ordinal);

    public int GetHashCode((string Material, string Param) k) =>
        HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(k.Material), StringComparer.Ordinal.GetHashCode(k.Param));
}
