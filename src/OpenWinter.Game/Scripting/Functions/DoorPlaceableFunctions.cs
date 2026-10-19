using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Doors and placeables: open/close/lock/use actions, lock properties, illumination, hardness.</summary>
static class DoorPlaceableFunctions
{
    // DOOR_ACTION_* / PLACEABLE_ACTION_*
    const int ActOpenOrUse = 0, ActUnlock = 1, ActBash = 2, ActIgnore = 3, ActKnock = 4;

    static void Enqueue(GameObject? o, GameAction a)
    {
        if (o == null || !ActionQueues.Accepts(o)) return;
        o.Actions.Add(a);
    }

    [NwFunc("ActionOpenDoor")] static object? ActionOpen(Call c)
    {
        if (c.Obj(0) is Door or Placeable) Enqueue(c.Self, new OpenDoorAction { Target = c.O(0), Run = c.I(1) != 0 });
        return null;
    }
    [NwFunc("ActionCloseDoor")] static object? ActionClose(Call c)
    {
        if (c.Obj(0) is Door or Placeable) Enqueue(c.Self, new CloseDoorAction { Target = c.O(0), Run = c.I(1) != 0 });
        return null;
    }
    [NwFunc("ActionUnlockObject")] static object? ActionUnlock(Call c)
    {
        if (c.Obj(0) is ILockable) Enqueue(c.Self, new UnlockAction { Target = c.O(0) });
        return null;
    }
    [NwFunc("ActionLockObject")] static object? ActionLock(Call c)
    {
        if (c.Obj(0) is ILockable) Enqueue(c.Self, new LockAction { Target = c.O(0) });
        return null;
    }
    [NwFunc("ActionInteractObject")] static object? ActionInteract(Call c)
    {
        // On a broken door the original does nothing at all (scene doorbash: the player neither moves nor acts).
        if (c.Obj(0) is Placeable or Door { Broken: false }) Enqueue(c.Self, new InteractAction { Target = c.O(0) });
        return null;
    }
    [NwFunc("ActionSit")] static object? ActionSit(Call c)
    {
        // Any placeable will do (the original doesn't ask for a useable one); the sitter runs there.
        if (c.Self is Creature && c.Obj(0) is Placeable) Enqueue(c.Self, new SitAction { Target = c.O(0), Run = true });
        return null;
    }

    [NwFunc("GetIsOpen")] static object? IsOpen(Call c) => DoorService.IsOpen(c.Obj(0)!) ? 1 : 0;

    [NwFunc("SetLocked")] static object? SetLocked(Call c)
    {
        if (c.Obj(0) is { } o) DoorService.SetLocked(c.World, o, c.I(1) != 0, c.Self);
        return null;
    }
    [NwFunc("GetLocked")] static object? GetLocked(Call c) => c.Obj(0) is ILockable { IsLocked: true } ? 1 : 0;

    [NwFunc("GetLockKeyRequired")] static object? KeyRequired(Call c) => c.Obj(0) is ILockable { KeyRequiredFlag: true } ? 1 : 0;
    [NwFunc("SetLockKeyRequired")] static object? SetKeyRequired(Call c)
    {
        if (c.Obj(0) is ILockable l) l.KeyRequiredFlag = c.I(1) != 0;
        return null;
    }
    [NwFunc("GetLockKeyTag")] static object? KeyTag(Call c) => c.Obj(0) switch
    {
        ILockable l => l.LockKeyTag,
        Trigger t => t.KeyName,
        _ => "",
    };
    [NwFunc("SetLockKeyTag")] static object? SetKeyTag(Call c)
    {
        switch (c.Obj(0))
        {
            case ILockable l: l.LockKeyTag = c.S(1); break;
            case Trigger t: t.KeyName = c.S(1); break;
        }
        return null;
    }
    [NwFunc("GetLockLockable")] static object? Lockable(Call c) => c.Obj(0) is ILockable { Lock.Lockable: true } ? 1 : 0;
    [NwFunc("SetLockLockable")] static object? SetLockable(Call c)
    {
        if (c.Obj(0) is ILockable l) l.Lock.Lockable = c.I(1) != 0;
        return null;
    }
    [NwFunc("GetLockUnlockDC")] static object? UnlockDC(Call c) => c.Obj(0) is ILockable l ? l.UnlockDC : 0;
    [NwFunc("SetLockUnlockDC")] static object? SetUnlockDC(Call c)
    {
        if (c.Obj(0) is ILockable l) l.UnlockDC = Math.Clamp(c.I(1), 0, 250);
        return null;
    }
    [NwFunc("GetLockLockDC")] static object? LockDC(Call c) => c.Obj(0) is ILockable l ? l.LockDC : 0;
    [NwFunc("SetLockLockDC")] static object? SetLockDC(Call c)
    {
        if (c.Obj(0) is ILockable l) l.LockDC = Math.Clamp(c.I(1), 0, 250);
        return null;
    }
    [NwFunc("GetKeyRequiredFeedback")] static object? KeyFeedback(Call c) => c.Obj(0) is ILockable l ? l.Lock.KeyFeedback : "";
    [NwFunc("SetKeyRequiredFeedback")] static object? SetKeyFeedback(Call c)
    {
        if (c.Obj(0) is ILockable l) l.Lock.KeyFeedback = c.S(1);
        return null;
    }

    [NwFunc("GetHardness")] static object? Hardness(Call c) => c.ObjOrSelf(0) switch
    {
        Door d => d.Hardness,
        Placeable p => p.Hardness,
        _ => -1,
    };
    [NwFunc("SetHardness")] static object? SetHardness(Call c)
    {
        int h = Math.Clamp(c.I(0), 0, 250);
        switch (c.ObjOrSelf(1))
        {
            case Door d: d.Hardness = h; break;
            case Placeable p: p.Hardness = h; break;
        }
        return null;
    }

    [NwFunc("GetBlockingDoor")] static object? BlockingDoor(Call c)
    {
        if (c.Self is not Creature cr) return GameObject.InvalidId;
        // A door, or a creature that the caller walked into (PersonalSpace).
        uint id = c.Event.Obj("BlockingDoor");
        if (c.World.Get(id) is Door or Creature) return id;
        return c.World.Get(cr.LastBlockingDoor) is Door or Creature ? cr.LastBlockingDoor : GameObject.InvalidId;
    }

    [NwFunc("GetIsDoorActionPossible")] static object? DoorActionPossible(Call c)
    {
        if (c.Obj(0) is not Door d) return 0;
        bool ok = c.I(1) switch
        {
            ActOpenOrUse => !d.IsOpen && (!d.Locked || c.Self is Creature cr && DoorService.FindKey(cr, d.KeyName) != null),
            // A broken door's lock can't be worked (door_bashed CC_0 ".locked.can").
            ActUnlock => d.Locked && !d.Broken && (d.KeyRequired == 0 || c.Self is Creature cr2 && DoorService.FindKey(cr2, d.KeyName) != null),
            // A broken door still reads bashable (nwserver, door_bashed: ED_0, CC_0 ".can.bash").
            ActBash => !d.Plot,
            ActIgnore => true,
            ActKnock => d.Locked && !d.Broken && d.KeyRequired == 0,
            _ => false,
        };
        return ok ? 1 : 0;
    }

    [NwFunc("DoDoorAction")] static object? DoDoorAction(Call c)
    {
        if (c.Obj(0) is not Door d) return null;
        switch (c.I(1))
        {
            case ActOpenOrUse: Enqueue(c.Self, new OpenDoorAction { Target = d.Id, NoReachWhenOpen = true }); break;
            case ActUnlock: Enqueue(c.Self, new UnlockAction { Target = d.Id }); break;
            case ActKnock: Enqueue(c.Self, new UnlockAction { Target = d.Id, Magic = true, Range = 20f }); break;
            case ActBash: Bash(c, d); break;
        }
        return null;
    }

    [NwFunc("GetIsPlaceableObjectActionPossible")] static object? PlaceableActionPossible(Call c)
    {
        if (c.Obj(0) is not Placeable p) return 0;
        bool ok = c.I(1) switch
        {
            ActOpenOrUse => p.Useable,
            ActUnlock => p.Locked && (p.KeyRequired == 0 || c.Self is Creature cr && DoorService.FindKey(cr, p.KeyName) != null),
            ActBash => !p.Plot,
            ActKnock => p.Locked && p.KeyRequired == 0,
            _ => false,
        };
        return ok ? 1 : 0;
    }

    [NwFunc("DoPlaceableObjectAction")] static object? DoPlaceableAction(Call c)
    {
        if (c.Obj(0) is not Placeable p) return null;
        switch (c.I(1))
        {
            case ActOpenOrUse: Enqueue(c.Self, new InteractAction { Target = p.Id, Range = 1.5f }); break;
            case ActUnlock: Enqueue(c.Self, new UnlockAction { Target = p.Id }); break;
            case ActKnock: Enqueue(c.Self, new UnlockAction { Target = p.Id, Magic = true, Range = 20f }); break;
            case ActBash: Bash(c, p); break;
        }
        return null;
    }

    // Native Do*Action(BASH) replaces queued waiting/movement, unlike ActionAttack.
    // Plot/open/static targets are still submitted; combat owns target validity.
    static void Bash(Call c, GameObject target)
    {
        if (c.Self is not Creature actor || !ActionQueues.Accepts(actor)) return;
        ActionQueues.Clear(c.World, actor);
        actor.Actions.Add(new Combat.AttackAction { Target = target.Id });
    }

    /// <summary>Placeables with a light (placeables.2da LightColor set).</summary>
    static bool HasLight(Call c, Placeable p)
    {
        var t = c.World.Resources.TwoDA("placeables");
        return !t.IsEmpty(p.Appearance, "LightColor");
    }

    [NwFunc("SetPlaceableIllumination")] static object? SetIllumination(Call c)
    {
        if (c.ObjOrSelf(0) is Placeable p && HasLight(c, p))
        {
            bool on = c.I(1) != 0;
            if (p.Illuminated != on && p.Area != null)
            {
                p.Illuminated = on;
                // Buffered: clients see it after RecomputeStaticLighting.
                AreaSettings.Of(c.World, p.Area).LightingDirty = true;
            }
        }
        return null;
    }
    [NwFunc("GetPlaceableIllumination")] static object? GetIllumination(Call c) =>
        c.ObjOrSelf(0) is Placeable p && HasLight(c, p) && p.Illuminated ? 1 : 0;
}
