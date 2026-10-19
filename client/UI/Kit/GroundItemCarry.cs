using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Classic;
using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>Choosing an inventory destination does not acquire the ground item until the player reaches it.</summary>
public partial class GroundItemCarry : Control
{
    ClientSession _session = null!;
    Views.AreaView _area = null!;
    uint _owner;
    ItemDrag _drag = null!;
    Func<bool> _inventoryOpen = null!;
    bool _active;

    public static void Start(Control host, ClientSession session, ItemEntry item, Func<bool> inventoryOpen)
    {
        if (session.Content is not { } content || session.Area is not { } area || host.GetViewport().GuiIsDragging()) return;
        var carry = new GroundItemCarry
        {
            Name = "GroundItemCarry", MouseFilter = MouseFilterEnum.Ignore, _session = session, _area = area,
            _owner = session.ControlledId, _inventoryOpen = inventoryOpen, _active = true,
            _drag = new ItemDrag { Item = item, Source = "ground", ServerPlacementSound = true },
        };
        PointerCarry.Add(host, carry);
        var assets = ClassicAssets.For(content);
        var preview = ClassicItemArt.DragPreview(assets, item, ClassicItemArt.IconSize(assets, item) / 2);
        ItemDragging.PlayPickupSound(content);
        carry.ForceDrag(carry._drag, preview);
    }

    public override void _Process(double delta)
    {
        if (_active && (_session.Area != _area || _session.ControlledId != _owner || !_inventoryOpen()
            || !_session.Objects.ContainsKey(_drag.Item.Id))) GetViewport().GuiCancelDrag();
    }

    public override void _Input(InputEvent e)
    {
        ItemDragging.Observe(e);
        if (!_active) return;
        if (e is InputEventKey { Keycode: Key.Escape, Pressed: true }
            || e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            GetViewport().GuiCancelDrag();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || !_active) return;
        _active = false;
        QueueFree();
    }
}
