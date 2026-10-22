namespace OpenWinter.Net.Nwn.Wire;

/// <summary>One variant of a tagged field: the discriminator it was read with and the variant's value.</summary>
public sealed record NwnTagged(long Tag, object Value);

/// <summary>The protocol's name for a generated field, for dumps and conformance tests.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NwnFieldAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
