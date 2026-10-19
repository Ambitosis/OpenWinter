using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Inventory;

/// <summary>Shared walking-into-range for item actions.</summary>
public abstract class ItemAction : GameAction
{
    public const float ReachRange = 2f;
    MoveAction? _approach;

    /// <summary>Walks a creature into range of <paramref name="target"/>; Done when in range, Failed if unreachable.</summary>
    protected ActionStatus Approach(World world, GameObject owner, GameObject target, float dt, float range = ReachRange, bool run = false)
    {
        if (owner is not Creature c) return ActionStatus.Done; // placeables and the like act at any distance
        if (target.Area != owner.Area || target.Area == null) return ActionStatus.Failed;
        if (Vector2.Distance(new(c.Position.X, c.Position.Y), new(target.Position.X, target.Position.Y)) <= range)
        {
            _approach?.Cancel(world, owner);
            _approach = null;
            return ActionStatus.Done;
        }
        _approach ??= new MoveAction { TargetObject = target.Id, Range = range * 0.75f, Run = run, Timeout = 20 };
        var status = _approach.Step(world, owner, dt);
        if (status == ActionStatus.Done) _approach = null;
        return status;
    }

    public override void Cancel(World world, GameObject owner) => _approach?.Cancel(world, owner);

    protected static ActionStatus Fail(World world, string name)
    {
        world.Scripts?.Log($"[script] {name} failed.");
        return ActionStatus.Failed;
    }
}

public sealed class EquipItemAction : EquipmentAction
{
    public uint ItemId;
    public int Slot;
    /// <summary>The inventory radial's offhand command prefers an empty main hand; explicit script slots do not.</summary>
    public bool OffhandCommand;
    protected override ActionStatus Execute(World world, Creature c)
    {
        if (world.Get<Item>(ItemId) is not { } item) return Fail(world, "ActionEquipItem");
        int slot = Slot;
        if (OffhandCommand)
        {
            if (world.Inventory.Owner(item) != c) return ActionStatus.Failed;
            int mask = world.Inventory.Rules.EquipableSlots(item.BaseItem);
            if ((mask & EquipSlot.LeftHand) == 0) return ActionStatus.Failed;
            slot = (mask & EquipSlot.RightHand) != 0 && !c.Equipment.ContainsKey(EquipSlot.RightHand) ? 4 : 5;
        }
        return world.Inventory.Equip(c, item, slot) ? ActionStatus.Done : Fail(world, "ActionEquipItem");
    }
}

public sealed class UnequipItemAction : EquipmentAction
{
    public uint ItemId;
    public (int X, int Y)? Cell;
    protected override ActionStatus Execute(World world, Creature c)
    {
        if (world.Get<Item>(ItemId) is not { } item) return ActionStatus.Failed;
        // An explicit drag destination must still be free when this command executes.
        if (Cell is { } cell && !world.Inventory.FitsAt(c, item, cell.X, cell.Y)) return ActionStatus.Failed;
        return world.Inventory.Unequip(c, item, Cell) ? ActionStatus.Done : ActionStatus.Failed;
    }
}

/// <summary>ActionPickUpItem: a creature runs up to the item, then takes 1.3 s to pick it up, like the original.</summary>
public sealed class PickUpItemAction : ItemAction, IGroundReach
{
    public const float Duration = 1.3f, Reach = 1f;
    public const float PlayerDuration = 1.5f;
    public uint ItemId;
    /// <summary>A backpack cell selected before approaching the ground item.</summary>
    public (int X, int Y)? Cell;
    /// <summary>The surviving inventory stack, which may differ from ItemId after a merge.</summary>
    public uint ResultItemId = GameObject.InvalidId;
    float _elapsed = -1;
    readonly GroundReachNotice _notice = new();
    public override int ActionType => 1; // ACTION_PICKUPITEM

    public bool ReachesWhereItStands(World world, GameObject owner) =>
        world.Get<Item>(ItemId) is { Area: not null } item && item.Area == owner.Area
            && Vector2.Distance(new(item.Position.X, item.Position.Y), new(owner.Position.X, owner.Position.Y)) <= Reach;

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (world.Get<Item>(ItemId) is not { Area: not null } item || item.Possessor != GameObject.InvalidId || !item.UseableOnGround
            || owner is Creature && owner.Area != item.Area)
        {
            Cancel(world, owner);
            return Fail(world, "ActionPickUpItem");
        }
        if (_elapsed < 0)
        {
            var reach = Approach(world, owner, item, dt, Reach, run: true);
            if (reach != ActionStatus.Done) return reach == ActionStatus.Failed ? Fail(world, "ActionPickUpItem") : reach;
            _elapsed = 0;
            if (owner is Creature pc)
            {
                var delta = item.Position - pc.Position;
                if (delta.X * delta.X + delta.Y * delta.Y > 1e-6f) pc.Facing = MathF.Atan2(delta.Y, delta.X);
                world.MarkChanged(pc);
            }
        }
        if (owner is Creature creature)
        {
            float duration = creature.IsPC ? PlayerDuration : Duration;
            _notice.Update(world, creature, item.Id, duration - _elapsed);
            if ((_elapsed += dt) < duration) return ActionStatus.InProgress;
        }
        if (Cell is { } cell)
        {
            bool moved = world.Inventory.MoveToCell(item, owner, cell.X, cell.Y, actor: owner);
            if (!moved && !item.IsDestroyed) return Fail(world, "ActionPickUpItem");
            ResultItemId = item.IsDestroyed
                ? world.Inventory.Items(owner).FirstOrDefault(i => i.InvX == cell.X && i.InvY == cell.Y)?.Id ?? GameObject.InvalidId
                : item.Id;
            return ActionStatus.Done;
        }
        var result = world.Inventory.MoveTo(item, owner, actor: owner);
        ResultItemId = result?.Id ?? GameObject.InvalidId;
        return result != null || item.IsDestroyed ? ActionStatus.Done : Fail(world, "ActionPickUpItem");
    }

    public override void Cancel(World world, GameObject owner)
    {
        _notice.Cancel(world, owner);
        base.Cancel(world, owner);
    }
}

/// <summary>ActionPutDownItem: a creature takes 1.2 s to put the item down (ACTION_DROPITEM meanwhile), like the original.</summary>
public sealed class PutDownItemAction : ItemAction, IGroundReach
{
    public const float Duration = 1.2f;
    public const float PlayerDuration = 1.5f;
    public uint ItemId;
    /// <summary>Terrain selected by a player drag; scripts retain the current-position behavior.</summary>
    public Location? Destination;
    MoveAction? _walk;
    bool _started;
    float _elapsed;
    readonly GroundReachNotice _notice = new();
    public override int ActionType => 2; // ACTION_DROPITEM

    public bool ReachesWhereItStands(World world, GameObject owner) => GroundReachNotice.Near(owner, Destination);

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (world.Get<Item>(ItemId) is not { } item || world.Inventory.Owner(item) != owner || owner.Area == null
            || Destination is { } destination && destination.Area != owner.Area.Id)
        {
            Cancel(world, owner);
            return Fail(world, "ActionPutDownItem");
        }
        if (!_started)
        {
            if (owner is Creature pc)
            {
                if (Destination is { } target)
                {
                    var delta = new Vector2(target.Position.X - pc.Position.X, target.Position.Y - pc.Position.Y);
                    if (delta.Length() > 1.001f)
                    {
                        _walk ??= new MoveAction { Target = target.Position - new Vector3(Vector2.Normalize(delta), 0), Run = true };
                        var status = _walk.Step(world, pc, dt);
                        if (status != ActionStatus.Done) return status;
                    }
                    _walk?.Cancel(world, pc);
                    delta = new Vector2(target.Position.X - pc.Position.X, target.Position.Y - pc.Position.Y);
                    if (delta.Length() > 1.01f) return ActionStatus.Failed;
                    if (delta.LengthSquared() > 1e-6f) pc.Facing = MathF.Atan2(delta.Y, delta.X);
                    world.MarkChanged(pc);
                }
            }
            _started = true;
        }
        if (owner is Creature creature)
        {
            float duration = creature.IsPC ? PlayerDuration : Duration;
            _notice.Update(world, creature, item.Id, duration - _elapsed);
            if ((_elapsed += dt) < duration) return ActionStatus.InProgress;
        }
        // The original lays a put-down item at facing 0 (east), whichever way its dropper faces (parity/visual,
        // scene grounddrop: dropped facing north, GetFacing 0 and the blade pointing east).
        if (!world.Inventory.Drop(item, Destination ?? owner.Location with { FacingDegrees = 0 }, owner)) return Fail(world, "ActionPutDownItem");
        if (owner is Creature { IsPC: true } player)
        {
            world.Output?.Notify("item-placed", player, new { item = item.Id, sound = world.Inventory.Rules.InventorySound(item) }, player);
            world.Output?.Notify("inventory-open", player, world.Inventory.InventoryView(player), player);
        }
        return ActionStatus.Done;
    }

    public override void Cancel(World world, GameObject owner)
    {
        _walk?.Cancel(world, owner);
        _notice.Cancel(world, owner);
        base.Cancel(world, owner);
    }
}

/// <summary>
/// ActionGiveItem (owner gives its item to Other) and ActionTakeItem (owner takes Other's item), as the original
/// does them. Giving happens at once whatever the distance, into another area too, to the dead as well, but only
/// with an item the giver holds itself (equipped or in its backpack, not in a bag). A creature taking first runs
/// up to the other object (ACTION_MOVETOPOINT), or stands a moment (ACTION_WAIT) when it is in another area, and
/// then takes the item even from a bag. Placeables give and take at any distance.
/// </summary>
public sealed class TransferItemAction : ItemAction
{
    /// <summary>How long a creature stands before taking from something in another area.</summary>
    public const float OtherAreaWait = 0.32f;
    public uint ItemId, Other;
    public bool Take;
    MoveAction? _walk;
    float _waited = -1;
    bool _reached;

    public override int ActionType => _walk != null ? 0 : _waited >= 0 && !_reached ? 36 : 65535;

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        var item = world.Get<Item>(ItemId);
        var other = world.Get(Other);
        if (item == null || other == null || other == owner) return ActionStatus.Failed;
        var inv = world.Inventory;
        if (!Take)
        {
            if (inv.Holder(item) != owner) return ActionStatus.Failed;
            return inv.MoveTo(item, other, actor: owner) != null || item.IsDestroyed ? ActionStatus.Done : ActionStatus.Failed;
        }
        if (inv.Owner(item) != other) return ActionStatus.Failed;
        if (owner is Creature c && !_reached)
        {
            // Within 2 m of a placeable is close enough (the original stops at its use point, about that far).
            bool near = other is not Creature && other.Area == c.Area && Vector2.Distance(new(c.Position.X, c.Position.Y), new(other.Position.X, other.Position.Y)) <= ReachRange;
            if (other.Area != null && other.Area == c.Area && !near)
            {
                // Running up to the other object (1 m from a creature; 1 m from a placeable's use point, as
                // ActionMoveToObject's default range: approach_use TK_H); the item is taken however that ends.
                _walk ??= new MoveAction { TargetObject = other.Id, Range = other is Creature ? 0f : 1f, MinRange = other is Creature ? 1f : null, Run = true, Timeout = 30f };
                if (_walk.Step(world, owner, dt) == ActionStatus.InProgress) return ActionStatus.InProgress;
                _walk = null;
            }
            else if (!near)
            {
                if (_waited < 0) _waited = 0;
                if ((_waited += dt) < OtherAreaWait) return ActionStatus.InProgress;
            }
            _reached = true;
        }
        return inv.MoveTo(item, owner, actor: owner) != null || item.IsDestroyed ? ActionStatus.Done : ActionStatus.Failed;
    }

    public override void Cancel(World world, GameObject owner)
    {
        _walk?.Cancel(world, owner);
        base.Cancel(world, owner);
    }
}

/// <summary>ActionUseItemOnObject / ActionUseItemAtLocation: activates one of an item's cast-spell properties.</summary>
public sealed class UseItemAction : ItemAction
{
    public uint ItemId;
    public required ItemProperty Property;
    public uint Target = GameObject.InvalidId;
    public Location TargetLocation = Location.Invalid;
    public int SubPropertyIndex;
    public bool DecrementCharges = true;
    public override int ActionType => 19; // ACTION_ITEMCASTSPELL

    Spells.CastSpellAction? _cast;
    Skills.UseSkillAction? _setTrap;
    bool _endsWhenConsumed;

    public override bool RetainsAfterQueueClear(World world, GameObject owner) =>
        _endsWhenConsumed && (_cast?.RetainsAfterQueueClear(world, owner) ?? false);

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        var inv = world.Inventory;
        // A trap kit is Set Trap: walk there, kneel 4.5 s, then the check (trap_set ITEMCHEST / ITEMLOC).
        if (_setTrap != null) return _setTrap.Step(world, owner, dt);
        if (_cast != null)
        {
            // Native potion_expire/item_use_clock end the current use when its
            // consumed item disappears, even with recovery time remaining.
            if (_endsWhenConsumed && world.Get<Item>(ItemId) == null) { _cast.Cancel(world, owner); return ActionStatus.Done; }
            return _cast.Step(world, owner, dt);
        }
        if (world.Get<Item>(ItemId) is not { } item || inv.Owner(item) != owner) return ActionStatus.Failed;
        var prop = item.Properties.FirstOrDefault(p => ReferenceEquals(p, Property))
            ?? item.Properties.FirstOrDefault(p => p.Type == Property.Type && p.SubType == Property.SubType && p.CostTableValue == Property.CostTableValue);
        if (prop == null || !prop.Useable) return ActionStatus.Failed;
        if (prop.Type == Skills.SkillIds.IpTrap && owner is Creature)
        {
            bool onObject = world.Get(Target) is { } t && t != owner;
            _setTrap = new Skills.UseSkillAction
            {
                Skill = Skills.SkillIds.SetTrap, ItemId = item.Id, Target = onObject ? Target : owner.Id,
                Spot = onObject ? null : TargetLocation.Area != Location.Invalid.Area ? TargetLocation : owner.Location,
                Range = Skills.UseSkillAction.RangeFor(Skills.SkillIds.SetTrap),
            };
            return _setTrap.Step(world, owner, dt);
        }
        int spell = prop.Type == ItemRules.PropCastSpell ? world.Resources.TwoDA("iprp_spells").GetInt(prop.SubType, "SpellIndex", -1) : -1;
        if (spell < 0) return inv.UseItemProperty(owner, item, prop, Target, TargetLocation, DecrementCharges) ? ActionStatus.Done : ActionStatus.Failed;
        if (owner is Creature user && !world.System<Skills.SkillSystem>().CanActivateSpellItem(user, item))
            return ActionStatus.Failed;
        // Cast-spell properties go through spell casting (range, animation, impact); charges are spent at impact.
        _endsWhenConsumed = true;
        _cast = new Spells.CastSpellAction
        {
            Spell = spell, Target = Target, TargetLocation = TargetLocation, ItemId = item.Id, ItemProperty = prop,
            DecrementCharges = DecrementCharges, Type = ActionType,
        };
        return _cast.Step(world, owner, dt);
    }

    public override void Cancel(World world, GameObject owner)
    {
        _cast?.Cancel(world, owner);
        _setTrap?.Cancel(world, owner);
        base.Cancel(world, owner);
    }
}
