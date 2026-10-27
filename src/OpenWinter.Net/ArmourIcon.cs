namespace OpenWinter.Net;

/// <summary>
/// How the original builds body armour's inventory icon from the armour's parts (measured on nwmain, visual scenes
/// <c>armouricons</c>, <c>armouriconsf</c> and <c>armouriconsm</c>; docs/CLASSIC_UI.md "Armour icons").
/// <list type="bullet">
/// <item>Layers, bottom first: pelvis, chest, belt, left shoulder (<c>ip?_shol</c>), right shoulder (<c>ip?_shor</c>),
/// robe. Each is a 64x128 palette texture (PLT) covering the whole canvas; the item shows its top 64x96.</item>
/// <item><c>ipm_</c> for a male player, <c>ipf_</c> for a female one (the viewer's gender, also in stores and
/// containers).</item>
/// <item>A part with model 0, or whose icon isn't installed, is left out (nothing stands in for it).</item>
/// <item>A robe hides the parts its parts_robe.2da row hides (HIDEPELVIS, HIDECHEST, HIDEBELT, HIDESHOL, HIDESHOR).</item>
/// <item>Colours are the item's six, each part taking its per-part overrides (EE): pelvis, chest and belt their own,
/// the left-shoulder icon the right shoulder's and the right-shoulder icon the left shoulder's; the robe takes none.
/// Skin, hair and tattoo rows are 0 (not the player's).</item>
/// </list>
/// </summary>
public static class ArmourIcon
{
    /// <summary>One icon part: its icon name, the ITEM_APPR_ARMOR_MODEL_* that picks the model, the one whose per-part
    /// colours it takes and the parts_robe.2da column that hides it.</summary>
    public readonly record struct Part(string Icon, int Model, int Colours, string Hide);

    public const int Pelvis = 6, Chest = 7, Belt = 8, RShoulder = 14, LShoulder = 15, Robe = 18;

    public static readonly Part[] Parts =
    {
        new("pelvis", Pelvis, Pelvis, "HIDEPELVIS"),
        new("chest", Chest, Chest, "HIDECHEST"),
        new("belt", Belt, Belt, "HIDEBELT"),
        new("shol", LShoulder, RShoulder, "HIDESHOL"),
        new("shor", RShoulder, LShoulder, "HIDESHOR"),
    };

    /// <summary>ITEM_APPR_ARMOR_COLOR_* (leather 1, leather 2, cloth 1, cloth 2, metal 1, metal 2) to PLT layers.</summary>
    static readonly int[] ChannelLayers = { 6, 7, 4, 5, 2, 3 };

    /// <summary>
    /// The icon's layers, bottom first, as <c>iconplt:&lt;resref&gt;:&lt;ten palette rows in hex&gt;</c>; empty when no part
    /// has an icon. <paramref name="model"/> answers ITEM_APPR_ARMOR_MODEL_* indices, <paramref name="colour"/>
    /// ITEM_APPR_TYPE_ARMOR_COLOR indices (0-5 the item's colours, 6 + part * 6 + channel the per-part ones, 255 for
    /// none), <paramref name="hasIcon"/> whether a PLT is installed, <paramref name="robeHides"/> a parts_robe.2da
    /// HIDE* column of the robe's row.
    /// </summary>
    public static string[] Layers(bool female, Func<int, int> model, Func<int, int> colour, Func<string, bool> hasIcon, Func<string, bool> robeHides)
    {
        string prefix = female ? "ipf_" : "ipm_";
        int robe = model(Robe);
        var layers = new List<string>();
        foreach (var part in Parts)
        {
            int m = model(part.Model);
            if (m <= 0 || robe > 0 && robeHides(part.Hide)) continue;
            Add($"{prefix}{part.Icon}{m:000}", part.Colours);
        }
        if (robe > 0) Add($"{prefix}robe{robe:000}", -1);
        return layers.ToArray();

        void Add(string resref, int colourPart)
        {
            if (!hasIcon(resref)) return;
            var rows = new byte[10];
            for (int channel = 0; channel < 6; channel++)
            {
                int c = colourPart < 0 ? 255 : colour(6 + colourPart * 6 + channel);
                if (c is < 0 or > 175) c = colour(channel);
                rows[ChannelLayers[channel]] = (byte)Math.Clamp(c, 0, 175);
            }
            layers.Add($"iconplt:{resref}:{Convert.ToHexString(rows)}");
        }
    }
}
