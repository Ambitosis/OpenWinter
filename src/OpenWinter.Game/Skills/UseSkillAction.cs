using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Skills;

/// <summary>
/// ActionUseSkill: walk to the target, then use the skill on it (see <see cref="SkillSystem"/> for the rules).
/// GetCurrentAction reports the matching ACTION_* (ACTION_OPENLOCK, ACTION_DISABLETRAP, ACTION_PICKPOCKET, ...).
/// <para>
/// The trap skills (Disable Trap's four uses and Set Trap) kneel at the trap for 4.5 s (ruleset SKILL_*_TRAP_DURATION)
/// and resolve at the end (trap_disarm NEAR: 4.68 s from a creature already in reach). Set Trap can aim at a spot on the
/// ground (<see cref="Spot"/>). An untrained creature gives up at once; a trap already there, or one that can't be
/// disarmed, ends the attempt on arrival.
/// </para>
/// </summary>
public sealed partial class UseSkillAction : ApproachAction
{
    public int Skill;
    public int SubSkill;
    public uint ItemId = GameObject.InvalidId;
    /// <summary>Set Trap on the ground: where the trap goes (Target is then the user itself or invalid).</summary>
    public Location? Spot;

    double _resolveAt = double.NaN;
    MoveAction? _walk;
    bool _atSpot;

    public override int ActionType => Skill switch
    {
        SkillIds.OpenLock => SkillIds.ActionOpenLock,
        SkillIds.DisableTrap => SubSkill switch
        {
            SkillIds.SubSkillFlagTrap => SkillIds.ActionFlagTrap,
            SkillIds.SubSkillRecoverTrap => SkillIds.ActionRecoverTrap,
            SkillIds.SubSkillExamineTrap => SkillIds.ActionExamineTrap,
            _ => SkillIds.ActionDisableTrap,
        },
        SkillIds.SetTrap => SkillIds.ActionSetTrap,
        SkillIds.PickPocket => SkillIds.ActionPickPocket,
        SkillIds.Taunt => SkillIds.ActionTaunt,
        SkillIds.Heal => SkillIds.ActionHeal,
        SkillIds.AnimalEmpathy => SkillIds.ActionAnimalEmpathy,
        _ => SkillIds.ActionInvalid,
    };

    bool _pvpChecked;

    /// <summary>
    /// A hostile skill (skills.2da HostileSkill: Animal Empathy, Pick Pocket, Taunt) on a creature the PvP setting protects
    /// is refused at once with the PvP feedback: measured for a player's Taunt on her own summon (associateradial).
    /// </summary>
    bool RefusedForPvp(World world, GameObject owner)
    {
        if (_pvpChecked) return false;
        _pvpChecked = true;
        return owner is Creature c && world.Get(Target) is Creature t && t != c && IsHostile(world, Skill) && world.RefusesPvp(c, t);
    }

    static bool IsHostile(World world, int skill)
    {
        var table = world.Resources.TwoDA("skills");
        return skill >= 0 && skill < table.RowCount && table.GetInt(skill, "HostileSkill", 0) != 0;
    }

    /// <summary>Taunt and Animal Empathy reach a little further than hand-to-hand skills.</summary>
    public static float RangeFor(int skill) => skill is SkillIds.Taunt or SkillIds.AnimalEmpathy ? 3f : 2f;

    bool TrapSkill => Skill is SkillIds.DisableTrap or SkillIds.SetTrap;

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (RefusedForPvp(world, owner)) return ActionStatus.Failed;
        if (owner is Creature c && TrapSkill && double.IsNaN(_resolveAt) && !world.System<SkillSystem>().CanUse(c, Skill))
            return ActionStatus.Failed; // untrained: nothing happens, not even the walk (trap_set UNTRAINED)
        if (Spot is { } spot && (Target == GameObject.InvalidId || Target == owner.Id))
        {
            if (!_atSpot && owner is Creature walker)
            {
                if (walker.Area?.Id != spot.Area) return ActionStatus.Failed;
                bool first = _walk == null;
                _walk ??= new MoveAction { Target = spot.Position, Range = Range, Run = true };
                var status = _walk.Step(world, owner, dt);
                if (status == ActionStatus.InProgress && first && TrapSkill && TrapBefore(world, walker, null, needsToMove: true))
                {
                    _walk.Cancel(world, owner);
                    return ActionStatus.Failed;
                }
                if (status == ActionStatus.InProgress) return status;
                _atSpot = true;
            }
            return Arrive(world, owner, owner);
        }
        if (TrapSkill && owner is Creature cr && world.Get(Target) is { } trap && trap != owner)
        {
            // Trap skills stop at their own distances: 1.78 m from a chest's centre, just outside a trap trigger's
            // edge (trap_disarm SUCC/FAR/TRIG: one 1 m outside didn't move), close to a door; running (FAR: 7.2 m in 2.6 s).
            if (!_atSpot)
            {
                if (trap.Area != cr.Area) return ActionStatus.Failed;
                _trapStart ??= cr.Position;
                _walk ??= trap switch
                {
                    Trigger t => new MoveAction { Target = OutsideEdge(t, cr.Position), Range = 0.1f, Run = true },
                    Placeable => new MoveAction { TargetObject = Target, TargetOrigin = true, Range = PlaceableReach, Run = true },
                    _ => new MoveAction { TargetObject = Target, Range = DoorReach, Run = true },
                };
                var status = _walk.Step(world, owner, dt);
                // Setting off: the walk has moved the creature.
                if (System.Numerics.Vector3.DistanceSquared(cr.Position, _trapStart.Value) > 0.0025f && TrapBefore(world, cr, trap, needsToMove: true))
                {
                    _walk.Cancel(world, owner);
                    return ActionStatus.Failed;
                }
                if (status == ActionStatus.InProgress) return status;
                _atSpot = true;
            }
            return Arrive(world, owner, trap);
        }
        return base.Step(world, owner, dt);
    }

    /// <summary>
    /// The moment between arriving and the 4.5 s count starting: across trap_disarm runs the original resolved
    /// 0.2-0.4 s later than arrival + 4.5 (4.68 s already in reach; 5.4-5.8 s from 3 m).
    /// </summary>
    public const float LeadIn = 0.2f;

    /// <summary>Where trap skills stop: from a placeable's centre, from a door's use point, outside a trap trigger's edge.</summary>
    public const float PlaceableReach = 1.78f, DoorReach = 1.0f, TriggerReach = 1.0f;

    /// <summary>A point <see cref="TriggerReach"/> outside a trap trigger's nearest edge, or the creature's own spot when it is that close.</summary>
    static System.Numerics.Vector3 OutsideEdge(Trigger t, System.Numerics.Vector3 from)
    {
        if (t.Geometry.Count < 3 || t.Contains(from)) return from;
        System.Numerics.Vector2 p = new(from.X, from.Y), best = p;
        float bestD = float.MaxValue;
        for (int i = 0, j = t.Geometry.Count - 1; i < t.Geometry.Count; j = i++)
        {
            System.Numerics.Vector2 a = new(t.Geometry[j].X, t.Geometry[j].Y), b = new(t.Geometry[i].X, t.Geometry[i].Y), ab = b - a;
            float len = ab.LengthSquared(), k = len < 1e-6f ? 0 : Math.Clamp(System.Numerics.Vector2.Dot(p - a, ab) / len, 0, 1);
            var q = a + ab * k;
            float d = System.Numerics.Vector2.Distance(p, q);
            if (d < bestD) { bestD = d; best = q; }
        }
        if (bestD <= TriggerReach) return from;
        var dir = System.Numerics.Vector2.Normalize(p - best);
        var at = best + dir * TriggerReach;
        return new System.Numerics.Vector3(at.X, at.Y, from.Z);
    }

    protected override ActionStatus Arrive(World world, GameObject owner, GameObject target)
    {
        if (owner is not Creature c || !Spells.CastSpellAction.CanAct(world, c)) return ActionStatus.Failed;
        var skills = world.System<SkillSystem>();
        var item = world.Get<Item>(ItemId);
        if (item != null && world.Inventory.Owner(item) != c) item = null; // must be carried
        if (TrapSkill)
        {
            var on = Spot != null && target == c ? null : target;
            if (double.IsNaN(_resolveAt))
            {
                bool can = Skill == SkillIds.DisableTrap ? skills.CanDisableTrap(c, target, SubSkill)
                    : item != null && skills.CanSetTrap(c, item, on is Door or Placeable ? on : null);
                if (!can) return ActionStatus.Failed;
                if (TrapBefore(world, c, on ?? target, needsToMove: false)) return ActionStatus.Failed;
                Face(world, c, Spot?.Position ?? target.Position);
                float duration = skills.TrapSkillDuration(Skill, SubSkill);
                _resolveAt = world.GameTime + LeadIn + duration;
                world.Output?.PlayAnimation(c, 12, 1, duration); // ANIMATION_LOOPING_GET_LOW
                return ActionStatus.InProgress;
            }
            if (world.GameTime < _resolveAt - 1e-4) return ActionStatus.InProgress;
            var tev = SkillEvent(world, c, target, item);
            if (tev?.Before() == true) { tev.Add("ACTION_RESULT", false).After(); return ActionStatus.Failed; }
            bool done = Skill == SkillIds.DisableTrap
                ? skills.DisableTrap(c, target, SubSkill)
                : item != null && skills.SetTrap(c, item, on, Spot ?? target.Location);
            tev?.Add("ACTION_RESULT", done).After();
            TrapAfter(done);
            return done ? ActionStatus.Done : ActionStatus.Failed;
        }
        var ev = SkillEvent(world, c, target, item);
        if (ev?.Before() == true) { ev.Add("ACTION_RESULT", false).After(); return ActionStatus.Failed; }
        Animate(world, c);
        bool ok = Skill switch
        {
            SkillIds.OpenLock => skills.OpenLock(c, target, item),
            SkillIds.PickPocket => target is Creature victim && skills.PickPocket(c, victim),
            SkillIds.Taunt => target is Creature t && skills.Taunt(c, t),
            SkillIds.Heal => target is Creature patient && skills.Heal(c, patient, item),
            SkillIds.AnimalEmpathy => target is Creature animal && skills.AnimalEmpathy(c, animal),
            SkillIds.Hide or SkillIds.MoveSilently => skills.SetActionMode(c, SkillIds.ModeStealth, !c.StealthMode),
            SkillIds.Search or SkillIds.Spot or SkillIds.Listen => skills.SetActionMode(c, SkillIds.ModeDetect, !c.DetectMode),
            SkillIds.Lore => target is Item i && skills.TryIdentify(c, i),
            _ => false,
        };
        ev?.Add("ACTION_RESULT", ok).After();
        return ok ? ActionStatus.Done : ActionStatus.Failed;
    }

    /// <summary>NWNX_ON_USE_SKILL as the skill is applied (nwnx_events_creature); ACTION_RESULT is added for the _AFTER half.</summary>
    Nwnx.NwnxEvent? SkillEvent(World world, Creature c, GameObject target, Item? item) =>
        Nwnx.NwnxEvent.Begin(world, "NWNX_ON_USE_SKILL", c)?.Add("USED_ITEM_OBJECT_ID", item).Add("TARGET_OBJECT_ID", target == c && Spot != null ? null : target)
            .Add("SKILL_ID", Skill).Add("SUB_SKILL_ID", SubSkill).Position("TARGET_POSITION_", Spot?.Position ?? target.Position);

    static void Face(World world, Creature c, System.Numerics.Vector3 at)
    {
        var d = at - c.Position;
        if (d.X * d.X + d.Y * d.Y < 1e-6f) return;
        c.Facing = MathF.Atan2(d.Y, d.X);
        world.MarkChanged(c);
    }

    public override void Cancel(World world, GameObject owner)
    {
        _walk?.Cancel(world, owner);
        base.Cancel(world, owner);
    }

    void Animate(World world, Creature c)
    {
        int animation = Skill switch
        {
            SkillIds.OpenLock or SkillIds.Heal => 12, // ANIMATION_LOOPING_GET_LOW
            SkillIds.PickPocket => 106, // ANIMATION_FIREFORGET_STEAL
            SkillIds.Taunt => 108, // ANIMATION_FIREFORGET_TAUNT
            _ => -1,
        };
        if (animation >= 0) world.Output?.PlayAnimation(c, animation, 1, animation == 12 ? 1.5f : 0);
    }
}
