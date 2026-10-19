using OpenWinter.Aurora.Formats;

namespace OpenWinter.Client.Views.WorldFx.Logic;

/// <summary>Model selection from visualeffects.2da, including the original's size fallback.</summary>
public static class VisualEffectModel
{
    public static string Root(TwoDA table, int row, string phase, string size)
    {
        string model = table.Get(row, $"{phase}_Root_{size}_Node");
        return model.Length > 0 || size == "M" ? model : table.Get(row, $"{phase}_Root_M_Node");
    }
}
