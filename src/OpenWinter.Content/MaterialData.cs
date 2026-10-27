namespace OpenWinter.Content;

/// <summary>
/// A material file (MTR): the shaders, textures and shader parameters a mesh uses instead of its plain bitmap.
/// A model's bitmap (or material name) that names an MTR picks it up.
/// </summary>
public sealed class MaterialData
{
    public const int TextureSlots = 6;

    public string Name = "";
    /// <summary>The custom vertex and fragment shaders (customshaderVS / customshaderFS); empty when not set.</summary>
    public string VertexShader = "";
    public string FragmentShader = "";
    public string GeometryShader = "";
    /// <summary>"renderhint": NormalAndSpecMapped, NormalTangents, ...; empty when not set.</summary>
    public string RenderHint = "";
    /// <summary>"transparency": the mesh blends with what is behind it; null when the file does not say.</summary>
    public bool? Transparency;
    /// <summary>texture0..texture5 (empty: slot not set; "null" in the file also clears a slot).</summary>
    public readonly string[] Textures = Enumerable.Repeat("", TextureSlots).ToArray();
    public readonly List<MaterialParameter> Parameters = new();
    /// <summary>Lines with other keywords, kept for diagnostics.</summary>
    public readonly List<string> Unknown = new();

    public bool HasCustomShader => VertexShader.Length > 0 || FragmentShader.Length > 0;

    public MaterialParameter? Parameter(string name) =>
        Parameters.FindLast(p => string.Equals(p.Name, name, StringComparison.Ordinal));
}

/// <summary>A shader parameter set by a material: "parameter float name 0.5", "parameter int name 3", up to four floats.</summary>
public sealed record MaterialParameter(string Name, string Type, float[] Values, int IntValue)
{
    public bool IsInt => Type == "int";
}
