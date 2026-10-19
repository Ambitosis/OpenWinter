using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Inventory;

/// <summary>Icons of items built from palette textures: body armour (from its parts), helmets and cloaks.</summary>
public sealed partial class InventorySystem
{
    /// <summary>Body armour: its part icons for the viewer's gender (<see cref="ArmourIcon"/>), else the base item's icon.</summary>
    string[] ArmorIcon(Item item, Creature? viewer)
    {
        var res = _world.Resources;
        var robes = res.TwoDA("parts_robe");
        int robe = GetAppearance(item, ApprArmorModel, ArmourIcon.Robe);
        var layers = ArmourIcon.Layers(viewer?.Gender == 1,
            part => GetAppearance(item, ApprArmorModel, part),
            index => GetAppearance(item, ApprArmorColor, index),
            resref => res.Contains(resref, ResType.Plt),
            column => robes.GetInt(robe, column, 0) == 1);
        return layers.Length > 0 ? layers : [res.TwoDA("baseitems").Get(item.BaseItem, "DefaultIcon").ToLowerInvariant()];
    }

    /// <summary>
    /// Helmets and cloaks: one i&lt;class&gt;_&lt;model&gt; icon, tinted with the item's colours when it is a PLT. Cloak icons
    /// are installed as icloak_m_&lt;model&gt; only; the original shows them to female players too (armouriconsf).
    /// </summary>
    string[] LayeredIcon(Item item, string resref)
    {
        var res = _world.Resources;
        if (!res.Contains(resref, ResType.Plt) && !res.Contains(resref, ResType.Tga) && !res.Contains(resref, ResType.Dds)
            && resref.LastIndexOf('_') is var cut and > 0 && resref.Insert(cut + 1, "m_") is var male && res.Contains(male, ResType.Plt))
            resref = male;
        if (!res.Contains(resref, ResType.Plt)) return [resref];
        byte[] colors = new byte[10];
        int[] channels = [6, 7, 4, 5, 2, 3];
        for (int channel = 0; channel < channels.Length; channel++)
            colors[channels[channel]] = (byte)Math.Clamp(GetAppearance(item, ApprArmorColor, channel), 0, 175);
        return [$"iconplt:{resref}:{Convert.ToHexString(colors)}"];
    }
}
