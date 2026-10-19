using System.Security.Cryptography;
using System.Text.Json;
using OpenWinter.Content;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Requests;

/// <summary>Remember a power shortcut without retaining ownership of a consumed item.</summary>
public static class QuickbarItemState
{
    // This is an OpenWinter shortcut identity, not a native stacking rule. Remaining
    // charges/stack count are deliberately excluded; different authored powers,
    // tags or flags must not bind merely because names match.
    public static string Key(Item item) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
    {
        item.BaseItem, resref = item.ResRef.ToLowerInvariant(), item.Tag,
        item.Identified, item.Stolen, item.Plot, item.Infinite,
        properties = item.Properties.Select(p => new { p.Type, p.SubType, p.CostTable, p.CostTableValue, p.Param1, p.Param1Value, p.DurationType }).ToArray(),
    })));

    public static string PowerKey(ItemProperty p) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(
        new { p.Type, p.SubType, p.CostTable, p.CostTableValue, p.Param1, p.Param1Value, p.DurationType })));

    public static void Remember(World world, Creature owner, Item item, QuickbarSlot slot)
    {
        if (slot.Prop >= 0)
        {
            if (slot.ItemPowerKey.Length > 0)
            {
                int index = item.Properties.FindIndex(p => p.Valid && p.Useable && AbilityRequests.IsActivatable(p) && PowerKey(p) == slot.ItemPowerKey);
                if (index >= 0) slot.Prop = index;
            }
            else if (slot.Prop < item.Properties.Count) slot.ItemPowerKey = PowerKey(item.Properties[slot.Prop]);
        }
        slot.Name = item.Name.Resolve(world.Resources.Talk);
        slot.ItemIcons = world.Inventory.Icon(item, owner);
        slot.ItemKey = Key(item);
        slot.ItemHasPower = item.Properties.Any(p => p.Valid && p.Useable && AbilityRequests.IsActivatable(p));
    }
}
