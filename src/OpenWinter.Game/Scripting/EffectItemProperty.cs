namespace OpenWinter.Game.Scripting;

/// <summary>An item property held as an effect (NWNX_Util_ItemPropertyToEffect): a valid effect of no script type.</summary>
public sealed partial class Effect
{
    /// <summary>The effect type of an item property cast to an effect, outside the script's EFFECT_TYPE_* range.</summary>
    public const int ItemPropertyType = 100_000;
    /// <summary>The item property this effect carries, if it is one.</summary>
    public ItemProperty? Property;
}
