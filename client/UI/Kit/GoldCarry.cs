using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Classic;
using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>The amount dialog attaches gold to the pointer; only dropping it on the world spends it.</summary>
public partial class GoldCarry : Control
{
    ClientSession _session = null!;
    int _count;
    uint _area, _owner;
    Views.AreaView _sourceArea = null!;
    Func<Vector2, System.Numerics.Vector3?> _pickGround = null!;
    bool _active, _cancelled;

    public static void Start(Control owner, ClientSession session, int count, Func<Vector2, System.Numerics.Vector3?> pickGround)
    {
        if (count <= 0 || session.Content is not { } content || session.Area is not { } area) return;
        var table = content.Resources.TwoDA("baseitems");
        var coins = new ItemEntry(0, "Gold", 76, count, [table.Get(76, "DefaultIcon").ToLowerInvariant()])
        {
            Info = new ItemInfo { Width = table.GetInt(76, "InvSlotWidth", 2), Height = table.GetInt(76, "InvSlotHeight", 2) },
        };
        var carry = new GoldCarry
        {
            Name = "GoldCarry", MouseFilter = MouseFilterEnum.Ignore,
            _session = session, _count = count, _active = true, _area = area.Info.AreaId,
            _sourceArea = area, _owner = session.ControlledId, _pickGround = pickGround
        };
        PointerCarry.Add(owner, carry);
        var assets = ClassicAssets.For(content);
        var preview = ClassicItemArt.DragPreview(assets, coins, ClassicItemArt.IconSize(assets, coins) / 2);
        carry.ForceDrag(new RefCounted(), preview);
    }

    public override void _Input(InputEvent e)
    {
        ItemDragging.Observe(e);
        if (!_active) return;
        if (e is InputEventKey { Keycode: Key.Escape, Pressed: true } || e is InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true })
        {
            _cancelled = true;
            GetViewport().GuiCancelDrag();
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Notification(int what)
    {
        if (what != NotificationDragEnd || !_active) return;
        _active = false;
        if (!_cancelled && _session.Area == _sourceArea && _session.ControlledId == _owner && !GetViewport().GuiIsDragSuccessful()
            && ItemDragging.OverWorld(this) && _pickGround(ItemDragging.Pointer(this)) is { } point)
        {
            _session.Request("drop-gold", new { count = _count, area = _area, position = new[] { point.X, point.Y, point.Z } });
        }
        QueueFree();
    }
}
