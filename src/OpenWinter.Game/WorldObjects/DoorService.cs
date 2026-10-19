using System.Numerics;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.WorldObjects;

/// <summary>
/// Opening, closing, locking, unlocking and using doors and placeables, with
/// the events the original raises (OnOpen/OnClosed/OnFailToOpen/OnLock/OnUnlock/OnUsed).
/// </summary>
public static class DoorService
{
    /// <summary>
    /// Open Lock skill check: (world, creature, door or placeable, DC) → success. Defaults to the
    /// skills package (<see cref="Skills.SkillSystem.OpenLockCheck"/>: take 20 out of combat, thieves' tools).
    /// </summary>
    public static Func<World, Creature, GameObject, int, bool> OpenLockCheck =
        (world, c, target, dc) => world.System<Skills.SkillSystem>().OpenLockCheck(c, target, dc);

    /// <summary>A key item with <paramref name="tag"/> carried by <paramref name="c"/> (inventory, equipment, or inside containers).</summary>
    public static Item? FindKey(Creature c, string tag)
    {
        if (string.IsNullOrEmpty(tag)) return null;
        foreach (var item in c.Inventory.Concat(c.Equipment.Values))
            if (Find(item, tag) is { } found) return found;
        return null;

        static Item? Find(Item item, string tag)
        {
            if (!item.IsDestroyed && item.Tag.Equals(tag, StringComparison.OrdinalIgnoreCase)) return item;
            foreach (var inner in item.Inventory)
                if (Find(inner, tag) is { } f) return f;
            return null;
        }
    }

    static void Feedback(World world, GameObject? by, string text)
    {
        if (by is Creature { IsPC: true } pc) world.Output?.FloatingText(pc, text, false);
    }

    /// <summary>
    /// Which way a door swings for an opener: 1 = ANIMATION_DOOR_OPEN1, 2 = OPEN2. "Open 1" when the opener stands on
    /// the side the door faces (the model's +y, what GetFacing reports as the bearing plus 90 degrees), so the leaf
    /// swings away from it; "open 2" from behind, exactly in the door's plane, or with no opening creature (a door told
    /// to open itself, one that opens itself for a walker). Measured on nwserver by parity suite door_leafside (doors at
    /// three bearings, openers at four spots, walkers from either side).
    /// </summary>
    public static int OpenSide(Door d, GameObject? by)
    {
        if (by is not Creature) return 2;
        var rel = by.Position - d.Position;
        var normal = new Vector2(-MathF.Sin(d.Facing), MathF.Cos(d.Facing));
        return Vector2.Dot(new Vector2(rel.X, rel.Y), normal) > 0 ? 1 : 2;
    }

    /// <summary>Check a locked attempt on arrival, before the successful-open reach animation.</summary>
    public static bool RefuseLockedOpen(World world, GameObject target, GameObject? by)
    {
        if (IsOpen(target) || target is not ILockable { IsLocked: true } locked || by == target
            || by is Creature c && FindKey(c, locked.LockKeyTag) != null) return false;
        FailToOpen(world, target, by);
        return true;
    }

    /// <summary>Opens a door or container. A locked one opens only for a creature carrying its key. Returns whether it is open afterwards.</summary>
    public static bool Open(World world, GameObject target, GameObject? by)
    {
        if (target is not (Door or Placeable) || target.IsDestroyed) return false;
        if (IsOpen(target)) return true;
        var lockable = (ILockable)target;
        // A door told to open itself opens even when locked.
        if (lockable.IsLocked && by != target)
        {
            if (by is Creature c && FindKey(c, lockable.LockKeyTag) is { } key) UnlockWithKey(world, target, c, key);
            else
            {
                FailToOpen(world, target, by);
                return false;
            }
        }
        // A trapped door goes off for whoever opens it and stays shut (trap_trigger T_DOOR*: commoners and hostiles,
        // on doors of either faction).
        if (by is Creature trapped && TrapSystem.TrySpring(world, target, trapped) && target is Door) return false;
        if (target.IsDestroyed) return false;
        by ??= target;
        switch (target)
        {
            case Door d:
                d.IsOpen = true;
                d.OpenedSide = OpenSide(d, by);
                if (d.Area != null) SyncWalkmesh(d.Area.Walkmesh, d);
                // A broken door keeps its broken pose.
                if (!d.Broken) world.Output?.PlayAnimation(d, d.OpenedSide == 1 ? 205 : 206, 1, 0);
                ObjectSound(world, d, "Opened");
                break;
            case Placeable p:
                p.IsOpen = true;
                world.Output?.PlayAnimation(p, 202, 1, 0);
                ContainerSound(world, p, "Opened");
                break;
        }
        world.MarkChanged(target);
        if (target is Door opened) SyncLinkedDoor(world, opened, by);
        world.RunEvent(target, "OnOpen", new EventContext().Set("OpenedBy", by.Id));
        return true;
    }

    /// <summary>Closes a door or container.</summary>
    public static bool Close(World world, GameObject target, GameObject? by)
    {
        if (target is not (Door or Placeable) || target.IsDestroyed) return false;
        if (!IsOpen(target)) return true;
        by ??= target;
        switch (target)
        {
            case Door d:
                d.IsOpen = false;
                if (d.Area != null) SyncWalkmesh(d.Area.Walkmesh, d);
                if (!d.Broken) world.Output?.PlayAnimation(d, 204, 1, 0);
                ObjectSound(world, d, "Closed");
                break;
            case Placeable p:
                p.IsOpen = false;
                world.Output?.PlayAnimation(p, 203, 1, 0);
                ContainerSound(world, p, "Closed");
                break;
        }
        world.MarkChanged(target);
        if (target is Door closed) SyncLinkedDoor(world, closed, by);
        world.RunEvent(target, "OnClosed", new EventContext().Set("ClosedBy", by.Id));
        return true;
    }

    public static bool IsOpen(GameObject o) => o switch { Door d => d.IsOpen, Placeable p => p.IsOpen, _ => false };

    // Native updates the destination door before the source's hook, without unlocking it,
    // firing its hooks/traps (door_links parity control).
    static void SyncLinkedDoor(World world, Door source, GameObject by)
    {
        if (TransitionService.ResolveTarget(world, source) is not Door target || target == source
            || target.IsDestroyed || target.IsOpen == source.IsOpen) return;
        target.IsOpen = source.IsOpen;
        // The linked door takes the same side as the door that was opened (door_leafside LK1/LK2).
        if (target.IsOpen) target.OpenedSide = source.OpenedSide;
        if (target.Area != null) SyncWalkmesh(target.Area.Walkmesh, target);
        if (!target.Broken) world.Output?.PlayAnimation(target, target.IsOpen ? target.OpenedSide == 1 ? 205 : 206 : 204, 1, 0);
        ObjectSound(world, target, target.IsOpen ? "Opened" : "Closed");
        world.MarkChanged(target);
    }

    /// <summary>How long a killed door without a transition stays before it is removed (nwserver, door_bashed: 2.0 to 2.1 s).</summary>
    public const float BrokenDoorRemoval = 2f;

    /// <summary>
    /// A door's death, measured on nwserver (parity suite door_bashed; docs/INTER_AREA_MOVEMENT.md, "Killed doors"):
    /// <list type="bullet">
    /// <item>it stays the same object where it stood, its hit points and lock as they were, and opens at once (no
    /// OnOpen). Its walkmesh goes: neither the closed slab nor a leaf is in the way any more, even if a creature closes
    /// it again;</item>
    /// <item>the door its transition leads to breaks and opens with it (no OnOpen), keeps its hit points and runs its
    /// own OnDeath first;</item>
    /// <item>a door with a transition stays for good and is still a transition both ways (routing, pursuit, jumps,
    /// OnClick); any other door is removed <see cref="BrokenDoorRemoval"/> seconds after its death.</item>
    /// </list>
    /// </summary>
    public static void Break(World world, Door d, uint killer)
    {
        if (d.Broken || d.IsDestroyed) return;
        var target = TransitionService.ResolveTarget(world, d);
        var linked = target is Door l && l != d && !l.IsDestroyed && !l.Broken ? l : null;
        Shatter(world, d);
        // The linked door breaks with it, keeping its hit points (the original draws both gone: scene doorbash).
        if (linked != null) Shatter(world, linked);
        var ctx = new EventContext().Set("Killer", killer);
        if (linked != null) world.After(0, () => world.RunEvent(linked, "OnDeath", ctx));
        world.After(0, () => world.RunEvent(d, "OnDeath", ctx));
        if (target == null) world.After(BrokenDoorRemoval, () => { if (!d.IsDestroyed) world.Destroy(d); });
    }

    static void Shatter(World world, Door d)
    {
        d.Broken = true;
        d.IsOpen = true;
        if (d.Area != null) SyncWalkmesh(d.Area.Walkmesh, d);
        ObjectSound(world, d, "Destroyed");
        world.MarkChanged(d);
    }

    /// <summary>
    /// A bash on a broken door that a creature has closed again knocks it open. The original does nothing (the attack
    /// never starts), so a broken transition door closed and then locked by script traps whoever needs it; OpenWinter
    /// deliberately doesn't (docs/INTER_AREA_MOVEMENT.md).
    /// </summary>
    public static void KnockOpen(World world, Door d, GameObject by)
    {
        if (!d.Broken || d.IsOpen || d.IsDestroyed) return;
        d.IsOpen = true;
        if (d.Area != null) SyncWalkmesh(d.Area.Walkmesh, d);
        ObjectSound(world, d, "Opened");
        world.MarkChanged(d);
        SyncLinkedDoor(world, d, by);
        world.RunEvent(d, "OnOpen", new EventContext().Set("OpenedBy", by.Id));
    }

    /// <summary>
    /// DestroyObject on a door: one with a transition is not removed but opens (no OnOpen, its linked door unchanged);
    /// any other door goes as usual (nwserver, door_bashed: DS_0, UD). Returns whether the door stays.
    /// </summary>
    public static bool KeepOnDestroy(World world, Door d)
    {
        if (d.IsDestroyed || TransitionService.ResolveTarget(world, d) == null) return false;
        if (!d.IsOpen)
        {
            d.IsOpen = true;
            d.OpenedSide = 1;
            if (d.Area != null) SyncWalkmesh(d.Area.Walkmesh, d);
            if (!d.Broken) world.Output?.PlayAnimation(d, 205, 1, 0);
            world.MarkChanged(d);
        }
        return true;
    }

    static void ContainerSound(World world, Placeable p, string state)
        => ObjectSound(world, p, state);

    /// <summary>Door and placeable sound appearances share placeableobjsnds.2da.</summary>
    public static void ObjectSound(World world, GameObject target, string state)
    {
        var (table, appearance) = target switch
        {
            Door { Appearance: > 0 } d => ("doortypes", d.Appearance),
            Door d => ("genericdoors", d.GenericType),
            Placeable p => ("placeables", p.Appearance),
            _ => ("", -1),
        };
        if (appearance < 0 || !int.TryParse(world.Resources.TwoDA(table).Get(appearance, "SoundAppType"), out int row)) return;
        string sound = world.Resources.TwoDA("placeableobjsnds").Get(row, state);
        if (sound.Length == 0 || sound == "****") return;
        // Destruction removes the object immediately; let its final cue outlive its model.
        if (state == "Destroyed") world.Output?.Notify("play-sound", target,
            new { resref = sound, position = SoundService.Vec(target.Position), detached = true });
        else SoundService.Play(world, target, sound);
    }

    static void FailToOpen(World world, GameObject target, GameObject? by)
    {
        var l = (ILockable)target;
        if (target is Door)
        {
            ObjectSound(world, target, "Locked");
            if (by is Creature { IsPC: true } pc)
            {
                // Native Open reports a locked object even when a specific key is required.
                // Its short overhead status is independent of chat and the door's scripted speech.
                world.Output?.SendMessage(pc, world.Resources.TlkString(8296));
                world.Output?.Notify("lock-feedback", pc,
                    new { creature = pc.Id, feedback = world.Resources.TlkString(8307) }, pc);
            }
            world.RunEvent(target, "OnFailToOpen", new EventContext().Set("Clicking", (by ?? target).Id));
        }
        else Feedback(world, by, l.KeyRequiredFlag ? (l.Lock.KeyFeedback.Length > 0 ? l.Lock.KeyFeedback : "A specific key is required to open this lock.") : "Locked");
    }

    static void UnlockWithKey(World world, GameObject target, Creature by, Item key)
    {
        Feedback(world, by, "Unlocked with " + key.Name.Resolve(world.Resources.Talk));
        SetUnlocked(world, target, by);
        if (((ILockable)target).Lock.AutoRemoveKey) world.Destroy(key);
    }

    static void SetUnlocked(World world, GameObject target, GameObject by)
    {
        ((ILockable)target).IsLocked = false;
        world.MarkChanged(target);
        world.RunEvent(target, "OnUnlock", new EventContext().Set("UnlockedBy", by.Id));
    }

    /// <summary>
    /// Unlocks with a key, or (when no key is required) an Open Lock check against the
    /// unlock DC. Returns whether the object is unlocked afterwards.
    /// </summary>
    public static bool Unlock(World world, GameObject target, GameObject? by, bool allowSkill = true)
    {
        if (target is not ILockable l || target.IsDestroyed) return false;
        if (!l.IsLocked) return true;
        by ??= target;
        if (by is not Creature c) { SetUnlocked(world, target, by); return true; }
        TrapSystem.TrySpring(world, target, c);
        if (target.IsDestroyed) return false;
        if (FindKey(c, l.LockKeyTag) is { } key) { UnlockWithKey(world, target, c, key); return true; }
        if (l.KeyRequiredFlag || !allowSkill)
        {
            if (target is Door)
            {
                if (c.IsPC) world.Output?.SendMessage(c,
                    l.Lock.KeyFeedback.Length > 0 ? l.Lock.KeyFeedback : world.Resources.TlkString(7944));
                world.RunEvent(target, "OnFailToOpen", new EventContext().Set("Clicking", c.Id));
            }
            else Feedback(world, c, l.Lock.KeyFeedback.Length > 0 ? l.Lock.KeyFeedback : "A specific key is required to open this lock.");
            return false;
        }
        if (!OpenLockCheck(world, c, target, l.UnlockDC))
        {
            Feedback(world, c, "You failed to pick the lock.");
            return false;
        }
        SetUnlocked(world, target, c);
        return true;
    }

    /// <summary>Locks a door or placeable: needs the key when one is required, otherwise an Open Lock check against the lock DC.</summary>
    public static bool Lock(World world, GameObject target, GameObject? by)
    {
        if (target is not ILockable l || target.IsDestroyed) return false;
        if (l.IsLocked) return true;
        by ??= target;
        if (by is Creature c)
        {
            if (!l.Lock.Lockable) { Feedback(world, c, "This cannot be locked."); return false; }
            bool hasKey = FindKey(c, l.LockKeyTag) != null;
            if (!hasKey && (l.KeyRequiredFlag || !OpenLockCheck(world, c, target, l.LockDC)))
            {
                Feedback(world, c, l.KeyRequiredFlag ? "A specific key is required to lock this." : "You failed to lock it.");
                return false;
            }
        }
        if (IsOpen(target)) Close(world, target, by);
        l.IsLocked = true;
        world.MarkChanged(target);
        world.RunEvent(target, "OnLock", new EventContext().Set("LockedBy", by.Id));
        return true;
    }

    /// <summary>Uses a placeable (ActionInteractObject / clicking it): containers open, others fire OnUsed.</summary>
    public static bool Use(World world, Placeable p, Creature by)
    {
        if (p.IsDestroyed || !p.Useable) return false;
        if (p.Locked)
        {
            if (FindKey(by, p.KeyName) is { } key) UnlockWithKey(world, p, by, key);
            else { FailToOpen(world, p, by); return false; }
        }
        // A trapped container a player opens springs in Open. An NPC using one sets the trap off when the
        // container's faction rates it below 90, as for a botched disarm (TrapSystem.NpcCriticalClash), and then
        // doesn't use it (parity suite trap_trigger with Prelude's faction table: a Hostile chest with a Hostile trap,
        // used by a Commoner through ActionInteractObject or DoPlaceableObjectAction, went off and ran no OnUsed; used
        // by a Hostile creature it stayed trapped and ran OnUsed. Without a faction table no NPC set one off.)
        if (!p.HasInventory) TrapSystem.TrySpring(world, p, by);
        else if (!by.IsPC && TrapSystem.TrySpringOnNpcUse(world, p, by)) return false;
        if (p.IsDestroyed) return false;
        // A container opens for a player; anyone else just uses it.
        if (p.HasInventory && by.IsPC)
        {
            // Opening a container shows its contents; closing the loot window closes it again.
            if (!Open(world, p, by)) return false;
            world.System<Requests.ContainerService>().Show(by, p);
            return true;
        }
        world.RunEvent(p, "OnUsed", new EventContext().Set("UsedBy", by.Id));
        return true;
    }

    /// <summary>
    /// SetLocked: sets the lock and queues OnLock / OnUnlock (even when nothing changed) with the calling object as
    /// GetLastLocked / GetLastUnlocked, like the original.
    /// </summary>
    public static void SetLocked(World world, GameObject target, bool locked, GameObject? by)
    {
        if (target is not ILockable l) return;
        if (l.IsLocked != locked) { l.IsLocked = locked; world.MarkChanged(target); }
        uint id = (by ?? target).Id;
        world.After(0, () => world.RunEvent(target, locked ? "OnLock" : "OnUnlock", new EventContext().Set(locked ? "LockedBy" : "UnlockedBy", id)));
    }

    /// <summary>
    /// Where a creature stands to use a door or placeable, and how near it gets: the nearest "use" point of a
    /// placeable's walkmesh (or "closed" point of a door's; while it is open, also the point on the side it didn't
    /// swing to), the creature's PERSPACE plus 0.5 m away for a placeable, plus 0.75 m for a door. Measured on nwserver
    /// by parity suite approach_use (doors: a human and a half-orc 1.05 m from the use point, a halfling 1.00 m; a
    /// chest: 0.80 and 0.75 m; ActionMoveToObject goes to the same use point, stopping at fRange when that is larger).
    /// </summary>
    public static (Vector3 Point, float MinRange)? UsePoint(World world, Creature mover, GameObject target, bool closedOnly = false)
    {
        var walkmesh = target switch
        {
            Placeable p => world.Models.GetWalkmesh(world.Resources.TwoDA("placeables").Get(p.Appearance, "ModelName"), Aurora.Resources.ResType.Pwk),
            Door d when world.Loader != null => world.Models.GetWalkmesh(world.Loader.DoorModel(d), Aurora.Resources.ResType.Dwk),
            _ => null,
        };
        float min = (target is Door ? 0.75f : 0.5f) + Game.PersonalSpace.Radius(world, mover);
        if (walkmesh == null) return (target.Position, min);
        var globals = walkmesh.RestGlobals();
        var placement = Matrix4x4.CreateRotationZ(target.Facing) * Matrix4x4.CreateTranslation(target.Position);
        Vector3? best = null;
        for (int i = 0; i < walkmesh.Parts.Count; i++)
        {
            string name = walkmesh.Parts[i].Name;
            bool use = target is Door door
                ? name.Contains("dp_closed", StringComparison.OrdinalIgnoreCase)
                    || door.IsOpen && !closedOnly && name.Contains("dp_open", StringComparison.OrdinalIgnoreCase)
                : name.Contains("_use", StringComparison.OrdinalIgnoreCase);
            if (!use || walkmesh.Parts[i].Mesh != null) continue;
            var point = Vector3.Transform(globals[i].Translation, placement);
            point.Z = target.Position.Z;
            if (best == null || Vector3.DistanceSquared(point, mover.Position) < Vector3.DistanceSquared(best.Value, mover.Position)) best = point;
        }
        return (best ?? target.Position, min);
    }

    /// <summary>Puts a creature in a seat (ActionSit after reaching it).</summary>
    public static bool Sit(World world, Creature c, Placeable chair)
    {
        if (chair.Area != c.Area) return false;
        if (chair.Sitter != c.Id && world.Get<Creature>(chair.Sitter) is { } other && other.SittingOn == chair.Id) return false;
        chair.Sitter = c.Id;
        c.SittingOn = chair.Id;
        c.MoveDirection = Vector3.Zero;
        c.Position = chair.Position;
        // The sitter faces out of the seat: opposite the chair's facing.
        c.FacingDegrees = chair.FacingDegrees + 180;
        world.MarkChanged(c);
        world.Output?.PlayAnimation(c, 6, 1, 1e6f); // ANIMATION_LOOPING_SIT_CHAIR
        return true;
    }

    public static void StandUp(World world, Creature c)
    {
        if (c.SittingOn == GameObject.InvalidId) return;
        var seat = world.Get<Placeable>(c.SittingOn);
        if (seat != null && seat.Sitter == c.Id) seat.Sitter = GameObject.InvalidId;
        c.SittingOn = GameObject.InvalidId;
        // Standing up steps off the seat: the original puts the creature 0.6 m north of it.
        if (seat != null && Vector2.Distance(new(seat.Position.X, seat.Position.Y), new(c.Position.X, c.Position.Y)) < 0.01f)
        {
            c.Position += new Vector3(0, 0.6f, 0);
            world.MarkChanged(c);
        }
        world.Output?.PlayAnimation(c, 0, 1, 0); // back to ANIMATION_LOOPING_PAUSE
    }

    /// <summary>
    /// Records that a closed door blocked a creature and raises its OnBlocked event
    /// (GetBlockingDoor). Called by movement/pathing when a door is in the way.
    /// </summary>
    public static void ReportBlocked(World world, Creature c, Door door)
    {
        c.LastBlockingDoor = door.Id;
        world.RunEvent(c, "ScriptOnBlocked", new EventContext().Set("BlockingDoor", door.Id));
    }

    /// <summary>Doors' walkmesh as they stand: the closed slab while shut, the leaf on its side while open.</summary>
    public static void SyncWalkmesh(Area area)
    {
        foreach (var d in area.Objects.OfType<Door>()) SyncWalkmesh(area.Walkmesh, d);
    }

    /// <summary>
    /// An open door's leaf blocks the way like any wall (measured: creatures walk round it, e.g. to close the door
    /// from the side it swung to); the doorway itself is clear.
    /// </summary>
    static void SyncWalkmesh(Walkmesh wm, Door d)
    {
        if (d.WalkmeshOwner == 0) return;
        // A broken door has no walkmesh at all, open or closed again (door_bashed: LDS/LDN/KDS/KDN stray, CXX cross).
        wm.SetOwnerEnabled(d.WalkmeshOwner, !d.IsOpen && !d.Broken);
        wm.SetOwnerEnabled(d.LeafOwner(1), d.IsOpen && !d.Broken && d.OpenedSide != 2);
        wm.SetOwnerEnabled(d.LeafOwner(2), d.IsOpen && !d.Broken && d.OpenedSide == 2);
    }
}

/// <summary>Applies initial door states for areas loaded with the module.</summary>
public sealed class DoorSystem : IWorldSystem
{
    readonly World _world;
    readonly HashSet<uint> _synced = new();
    public DoorSystem(World world) { _world = world; }

    public void Step(float dt)
    {
        foreach (var area in _world.Areas)
            if (_synced.Add(area.Id)) DoorService.SyncWalkmesh(area);
    }
}
