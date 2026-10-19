using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;

namespace OpenWinter.Game.Inventory;

public sealed partial class InventorySystem
{
    readonly HashSet<uint> _changedInventories = new();

    /// <summary>Publish the final contents after a held item or inventory changes. Multiple mutations in one step coalesce.</summary>
    public void Changed(GameObject? holderOrItem)
    {
        var owner = holderOrItem is Item item ? Owner(item) : holderOrItem;
        if (owner is Creature { IsPC: true } or Placeable or Store)
            _changedInventories.Add(owner.Id);
    }

    void FlushChanges()
    {
        if (_changedInventories.Count == 0) return;
        var changed = _changedInventories.ToArray();
        _changedInventories.Clear();
        foreach (uint id in changed)
        {
            if (_world.Get(id) is Creature { IsPC: true, IsDestroyed: false } pc)
            {
                _world.Output?.Notify("inventory-open", pc, InventoryView(pc), pc);
                _world.System<AbilitiesFeed>().Soon(pc);
                // The open store's offers for the player's items follow the backpack (items a script gives).
                if (_storeSessions.ContainsKey(pc.Id) && SessionOf(pc) is { } session)
                    _world.Output?.Notify("store-open", session.Store, StoreView(session.Store, pc), pc);
            }
            else if (_world.Get(id) is Placeable { IsDestroyed: false } container)
                _world.System<ContainerService>().RefreshViewers(container);
            // Wares a script creates in or takes from an open store show at once (storestock, storeprices).
            else if (_world.Get(id) is Store { IsDestroyed: false } store)
                StoreChanged(store);
        }
    }
}
