using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game;

public sealed partial class World
{
    InventorySystem? _inventory;

    /// <summary>Item possession, stacking, equipment, gold and stores.</summary>
    public InventorySystem Inventory => _inventory ??= System<InventorySystem>();

    /// <summary>
    /// Takes an object out of its area without destroying it (an item picked up from the ground).
    /// Clients are told it is gone, as if removed.
    /// </summary>
    public void LiftFromArea(GameObject o)
    {
        if (o.Area == null) return;
        RemoveFromArea(o);
        ObjectRemoved?.Invoke(o);
    }
}
