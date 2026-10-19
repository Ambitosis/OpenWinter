using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Society;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Spells;

/// <summary>
/// ActionRest (and the player's rest button): refused in combat, near enemies (REST_ENEMY_CHECK_DISTANCE,
/// optionally only enemies in line of sight) or, for players, in no-rest areas. Resting takes
/// restduration.2da (by hit dice) for players; NPCs finish immediately. Being hurt or an enemy coming near cancels it. At the end
/// hit points, spells and feat uses come back. Players fire the module's OnPlayerRest for
/// REST_EVENTTYPE_REST_STARTED / FINISHED / CANCELLED; NPCs fire their OnRested when done.
/// </summary>
public sealed class RestAction : GameAction
{
    public const int RestStarted = 1, RestFinished = 2, RestCancelled = 3;
    // The original's feedback lines (dying scene chat log): "Resting.", "Done resting.", "Cancelled Rest.",
    // "You cannot rest while there are enemies nearby." and the no-rest area's "No resting is allowed in this area.".
    const int StrResting = 7952, StrDone = 7953, StrCancelled = 7957, StrEnemies = 66234, StrNoRest = 10492;
    const int AnimationSitCross = 7, AnimationPause = 0;

    public bool LineOfSightCheck;
    public override int ActionType => _ended ? 65535 : 17; // ACTION_INVALID once completed, otherwise ACTION_REST

    bool _started, _ended;
    float _remaining, _nextCheck;
    int _hp;

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (owner is not Creature c || c.IsDead || c.Area == null) return ActionStatus.Failed;
        if (!_started)
        {
            if (!CanRest(world, c, LineOfSightCheck, out var why))
            {
                if (c.IsPC) world.Output?.SendMessage(c, why);
                return ActionStatus.Failed;
            }
            _started = true;
            c.IsResting = true;
            if (!c.IsPC)
            {
                // Native rest_start_effects: NPC ActionRest finishes in this tick.
                // OnRested sees resting=true and the original effects; cleanup also
                // removes temporary effects added by that callback. ForceRest has no hook.
                _ended = true;
                world.RunEvent(c, "ScriptRested", new EventContext().Set("Rester", c.Id).Set("RestEventType", RestFinished));
                CreatureRules.Rest(world, c);
                c.IsResting = false;
                return ActionStatus.Done;
            }
            _hp = HitPoints.Current(c);
            _nextCheck = 1f;
            world.Output?.SendMessage(c, world.TlkStringOverridden(StrResting));
            // The module's rest script runs first: it may cancel the rest (ClearAllActions) or change its
            // length and animation (NWNX_Player_SetRestDuration / SetRestAnimation).
            ModuleEvent(world, c, RestStarted);
            if (_ended) return ActionStatus.Failed;
            // Native restcreatorrules: accepted PC rest removes that PC's magical
            // effects from both self and other recipients, regardless of spell ID
            // or temporary/permanent duration. The STARTED hook may veto first.
            // AssignCommand(ClearAllActions) in that hook is queued, so cleanup must
            // follow those zero-delay commands as well as the hook itself.
            world.After(0, () =>
            {
                if (_ended || world.Get<Creature>(c.Id) != c) return;
                var effects = world.System<EffectSystem>();
                int magical = EffectConstants.Of(world).Magical;
                foreach (var target in world.AllObjects.ToArray())
                    effects.RemoveWhere(target, e => e.Creator == c.Id && e.SubType == magical);
                // Losing one's own Constitution/temporary-HP buff is part of
                // starting rest, not subsequent damage that should cancel it.
                CreatureRules.SyncHitPoints(world, c);
                _hp = HitPoints.Current(c);
            });
            var nwnx = world.System<Nwnx.NwnxRegistry>().ObjectState(c.Id);
            _remaining = nwnx.TryGetValue("restDuration", out var d) && d.Value is int ms && ms > 0 ? ms / 1000f : Duration(world, c);
            int nwnxAnimation = nwnx.TryGetValue("restAnimation", out var a) && a.Value is int an ? an : -1;
            if (nwnxAnimation < 0) world.Output?.PlayAnimation(c, AnimationSitCross, 1, _remaining);
            world.System<SpellSystem>().Notify("rest", c, new { state = "started", duration = _remaining, nwnxAnimation });
            return ActionStatus.InProgress;
        }
        if (_ended) return ActionStatus.Failed;
        _remaining -= dt;
        _nextCheck -= dt;
        int hp = HitPoints.Current(c);
        if (hp < _hp) { Stop(world, c, cancelled: true); return ActionStatus.Failed; }
        _hp = hp;
        if (_nextCheck <= 0)
        {
            _nextCheck = 1f;
            if (EnemyNear(world, c, LineOfSightCheck)) { Stop(world, c, cancelled: true); return ActionStatus.Failed; }
        }
        if (_remaining > 0) return ActionStatus.InProgress;
        Stop(world, c, cancelled: false);
        return ActionStatus.Done;
    }

    public override void Cancel(World world, GameObject owner)
    {
        if (owner is Creature c && _started && !_ended) Stop(world, c, cancelled: true);
    }

    void Stop(World world, Creature c, bool cancelled)
    {
        _ended = true;
        c.IsResting = false;
        world.Output?.PlayAnimation(c, AnimationPause, 1, 0);
        world.System<SpellSystem>().Notify("rest", c, new { state = cancelled ? "cancelled" : "finished", duration = 0f });
        if (c.IsPC) world.Output?.SendMessage(c, world.TlkStringOverridden(cancelled ? StrCancelled : StrDone));
        if (cancelled)
        {
            // Events run after whatever cancelled the rest (often the rest script itself).
            uint id = c.Id;
            world.After(0, () => { if (world.Get<Creature>(id) is { } r) ModuleEvent(world, r, RestCancelled); });
            return;
        }
        CreatureRules.Rest(world, c);
        // Native restspellrules: the PC's queued successor runs before the module's
        // FINISHED event, and the creature OnRested hook does not fire for PCs.
        uint finishedId = c.Id;
        world.After(0, () => { if (world.Get<Creature>(finishedId) is { } rester) ModuleEvent(world, rester, RestFinished); });
    }

    static void ModuleEvent(World world, Creature c, int type)
    {
        if (c.IsPC) world.RunEvent(world.Module, "Mod_OnPlrRest", new EventContext().Set("Rester", c.Id).Set("RestEventType", type));
    }

    /// <summary>Rest time from restduration.2da (milliseconds by hit dice).</summary>
    public static float Duration(World world, Creature c)
    {
        var table = world.Resources.TwoDA("restduration");
        if (table.RowCount == 0) return 10f + 0.5f * c.TotalLevel;
        int row = Math.Clamp(c.TotalLevel, 0, table.RowCount - 1);
        return Math.Max(0, table.GetInt(row, "DURATION", 10000)) / 1000f;
    }

    public static bool CanRest(World world, Creature c, bool lineOfSight, out string reason)
    {
        reason = "";
        // In combat the enemies are near too; the original's own line for it is unmeasured, so it says the same.
        if (world.System<Combat.CombatSystem>().IsInCombat(c)) { reason = world.TlkStringOverridden(StrEnemies); return false; }
        if (c.IsPC && c.Area != null && AreaSettings.Of(world, c.Area).NoRest) { reason = world.TlkStringOverridden(StrNoRest); return false; }
        if (EnemyNear(world, c, lineOfSight)) { reason = world.TlkStringOverridden(StrEnemies); return false; }
        return true;
    }

    static bool EnemyNear(World world, Creature c, bool lineOfSight)
    {
        if (c.Area == null) return false;
        float range = Ruleset.Float(world, "REST_ENEMY_CHECK_DISTANCE", 30f);
        foreach (var o in c.Area.Objects.OfType<Creature>())
        {
            if (o == c || o.IsDead || System.Numerics.Vector3.Distance(o.Position, c.Position) > range) continue;
            if (!world.Factions.IsEnemyOf(o, c) && !world.Factions.IsEnemyOf(c, o)) continue;
            if (lineOfSight && !LineOfSight.Between(world, c, o)) continue;
            return true;
        }
        return false;
    }
}

/// <summary>ActionCounterSpell: waits ready to counter the target's next spell (see <see cref="SpellSystem.TryCounter"/>).</summary>
public sealed class CounterSpellAction : GameAction
{
    public uint Target = GameObject.InvalidId;
    /// <summary>Set when a spell was countered; the action then ends.</summary>
    public bool Countered;
    public override int ActionType => 31; // ACTION_COUNTERSPELL

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (owner is not Creature { IsDead: false } c) return ActionStatus.Failed;
        var t = world.Get(Target);
        if (t == null || t.Area != c.Area || t is Creature { IsDead: true } || Countered) return ActionStatus.Done;
        return ActionStatus.InProgress;
    }
}
