using System.Numerics;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Inventory;

/// <summary>Put a chosen amount of gold on the ground without reserving or losing it on cancellation.</summary>
public sealed class DropGoldAction : GameAction, IGroundReach
{
    public const float Duration = PutDownItemAction.PlayerDuration;
    public int Count;
    /// <summary>Selected terrain; null retains legacy drop-at-current-position requests.</summary>
    public Location? Destination;
    MoveAction? _approach;
    bool _inRange;
    float _elapsed;
    readonly GroundReachNotice _notice = new();
    public override int ActionType => 2;

    public bool ReachesWhereItStands(World world, GameObject owner) => GroundReachNotice.Near(owner, Destination);
    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (owner is not Creature pc || pc.Area == null || Count <= 0 || Count > pc.Gold
            || Destination is { } destination && destination.Area != pc.Area.Id)
        {
            Cancel(world, owner);
            return ActionStatus.Failed;
        }
        if (!_inRange && Destination is { } target)
        {
            var delta = new Vector2(target.Position.X - pc.Position.X, target.Position.Y - pc.Position.Y);
            if (delta.Length() > 1.001f)
            {
                _approach ??= new MoveAction
                {
                    Target = target.Position - new Vector3(Vector2.Normalize(delta), 0), Run = true,
                };
                var status = _approach.Step(world, pc, dt);
                if (status != ActionStatus.Done) return status;
            }
            _approach?.Cancel(world, pc);
            delta = new Vector2(target.Position.X - pc.Position.X, target.Position.Y - pc.Position.Y);
            if (delta.LengthSquared() > 1e-6f) pc.Facing = MathF.Atan2(delta.Y, delta.X);
            world.MarkChanged(pc);
            _inRange = true;
        }
        // The same bend as putting an item down (PutDownItemAction), once it has turned to the spot.
        _notice.Update(world, pc, GameObject.InvalidId, Duration - _elapsed);
        if ((_elapsed += dt) < Duration) return ActionStatus.InProgress;
        var coins = world.Items.FromBlueprint("nw_it_gold001");
        if (coins == null) return ActionStatus.Failed;
        coins.StackSize = Count;
        world.Register(coins);
        if (!world.Inventory.Drop(coins, Destination ?? pc.Location, pc)) { world.Destroy(coins); return ActionStatus.Failed; }
        world.Inventory.AddGold(pc, -Count);
        world.Output?.Notify("item-placed", pc, new { item = coins.Id, sound = world.Inventory.Rules.InventorySound(coins) }, pc);
        world.Output?.Notify("inventory-open", pc, world.Inventory.InventoryView(pc), pc);
        return ActionStatus.Done;
    }

    public override void Cancel(World world, GameObject owner)
    {
        _approach?.Cancel(world, owner);
        _notice.Cancel(world, owner);
    }
}
