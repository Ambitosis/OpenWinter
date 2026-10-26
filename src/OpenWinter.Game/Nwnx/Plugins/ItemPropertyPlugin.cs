using System.Globalization;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_ItemProperty: item properties as plain fields and back (parity suite nwnx_itemprop).</summary>
static class ItemPropertyPlugin
{
    const string P = "ItemProperty";

    static ItemProperty PopIp(NwnxCall n) => n.Args.Pop()?.Value as ItemProperty ?? new ItemProperty();

    /// <summary>
    /// A property as the original holds it: one on an item in its stored form (parameters 255 when unused, its
    /// uses per day, an id); a script's own value with -1 for what is unset and id 0 (nwnx_item).
    /// </summary>
    static ItemProperty View(ItemProperty ip)
    {
        if (!ip.Valid) return new ItemProperty { Type = 0, ChanceToAppear = 0, Useable = false, UsesPerDay = 0, Id = 0 };
        if (!ip.OnItem)
        {
            var own = ip.Clone();
            if (own.UsesPerDay == 255) own.UsesPerDay = -1;
            own.Id = 0;
            return own;
        }
        var stored = InventorySystem.AsStored(ip);
        if (stored.UsesPerDay == 255 && InventorySystem.MaxUsesPerDay(stored) is > 0 and var max) stored.UsesPerDay = max;
        return stored;
    }

    [NwnxFunc(P, "UnpackIP")]
    static void Unpack(NwnxCall n)
    {
        var raw = PopIp(n);
        var ip = View(raw);
        n.Push(ip.Tag);
        n.PushObject(ip.Creator);
        n.Push(ip.SpellId);
        n.Push(ip.Useable);
        n.Push(ip.ChanceToAppear);
        n.Push(ip.UsesPerDay);
        n.Push(ip.Param1Value);
        n.Push(ip.Param1);
        n.Push(ip.CostTableValue);
        n.Push(ip.CostTable);
        n.Push(ip.SubType);
        n.Push(ip.Type);
        n.Push(ip.Id.ToString(CultureInfo.InvariantCulture));
    }

    [NwnxFunc(P, "PackIP")]
    static void Pack(NwnxCall n)
    {
        var ip = new ItemProperty
        {
            Type = n.PopInt(),
            SubType = n.PopInt(),
            CostTable = n.PopInt(),
            CostTableValue = n.PopInt(),
            Param1 = n.PopInt(),
            Param1Value = n.PopInt(),
            UsesPerDay = n.PopInt(),
            ChanceToAppear = n.PopInt(),
            Useable = n.PopInt() != 0,
            SpellId = n.PopInt(),
            Creator = n.PopObjectId(),
            Tag = n.PopString(),
        };
        if (ip.UsesPerDay < 0) ip.UsesPerDay = 255;
        n.Push(new NwnxValue(NwnxType.ItemProperty, ip));
    }

    /// <summary>
    /// The nth of the item's activated properties (its cast-spell properties, the index item events report), in
    /// their stored form, without creator and spell (nwnx_item).
    /// </summary>
    [NwnxFunc(P, "GetActiveProperty")]
    static void Active(NwnxCall n)
    {
        var item = n.PopObject<Item>();
        int index = n.PopInt();
        var active = item?.Properties.Where(p => p.Type == ItemRules.PropCastSpell).ToList() ?? [];
        var ip = View(index >= 0 && index < active.Count ? active[index] : new ItemProperty());
        n.Push(ip.Tag);
        n.Push(ip.Useable);
        n.Push(ip.ChanceToAppear);
        n.Push(ip.UsesPerDay);
        n.Push(ip.Param1Value);
        n.Push(ip.Param1);
        n.Push(ip.CostTableValue);
        n.Push(ip.CostTable);
        n.Push(ip.SubType);
        n.Push(ip.Type);
    }
}
