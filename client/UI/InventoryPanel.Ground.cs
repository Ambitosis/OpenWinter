using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI;

public partial class InventoryPanel
{
    uint _groundRequested;
    Views.AreaView? _groundArea;
    uint _groundOwner;

    bool ItemInventoryOpen() => Classic.ClassicInventory.Current is { } classic
        ? classic.Inventory.IsOpen : IsVisibleInTree();

    /// <summary>Open inventory chooses a destination first; closed inventory uses the ordinary world action.</summary>
    public bool CarryGroundItem(uint id)
    {
        if (!ItemInventoryOpen()) return false;
        _groundRequested = id;
        _groundArea = Session.Area;
        _groundOwner = Session.ControlledId;
        Session.Request("ground-item-carry", new { item = id });
        return true;
    }

    void ReceiveGroundItem(OpenWinter.Net.NotifyMessage message)
    {
        if (message.Subject != _groundRequested || !ItemInventoryOpen() || Session.Area != _groundArea
            || Session.ControlledId != _groundOwner) return;
        _groundRequested = 0;
        using var data = System.Text.Json.JsonDocument.Parse(message.Json);
        if (Session.Area?.Info.AreaId != data.RootElement.GetProperty("area").GetUInt32()) return;
        GroundItemCarry.Start(this, Session, ItemEntry.From(data.RootElement), ItemInventoryOpen);
    }

    /// <summary>Capture terrain before an optional valuable-item confirmation can move the pointer.</summary>
    public void DropOnWorld(ItemEntry item)
    {
        if (Session.Area is not { } area || PickGround(ItemDragging.Pointer(this)) is not { } point) return;
        Drop(item, new { item = item.Id, area = area.Info.AreaId, position = new[] { point.X, point.Y, point.Z } });
    }
}
