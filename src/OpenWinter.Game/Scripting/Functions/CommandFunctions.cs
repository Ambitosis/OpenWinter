using System.Numerics;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Delays, assignment, action queues, script execution and events.</summary>
static class CommandFunctions
{
    // Event types (EVENT_* in nwscript.nss).
    public const int EventHeartbeat = 1001, EventPerceive = 1002, EventEndCombatRound = 1003, EventDialogue = 1004,
        EventAttacked = 1005, EventDamaged = 1006, EventDisturbed = 1008, EventSpawn = 1009, EventSpellCastAt = 1011, EventActivateItem = 1012, EventUserDefined = 1111;

    static void Enqueue(Call c, GameObject? o, GameAction a)
    {
        if (o == null || !ActionQueues.Accepts(o)) return;
        o.Actions.Add(a);
    }

    [NwFunc("DelayCommand")]
    static object? Delay(Call c)
    {
        if (c.A(1) is { } s) c.Host.Schedule(c.F(0), s, c.SelfId);
        return null;
    }
    [NwFunc("AssignCommand")]
    static object? Assign(Call c)
    {
        // Waypoints and sound objects never run assigned commands (audio_scripts).
        if (c.A(1) is { } s && c.World.Get(c.O(0)) is { } o and not (Waypoint or Sound)) c.Host.Schedule(0, s, o.Id);
        return null;
    }
    [NwFunc("ActionDoCommand")]
    static object? DoCommand(Call c)
    {
        if (c.A(0) is { } s) Enqueue(c, c.Self, new DoCommandAction { Situation = s });
        return null;
    }
    [NwFunc("ExecuteScript")]
    static object? Execute(Call c)
    {
        // An invalid target still runs the script, with OBJECT_SELF invalid.
        var target = c.World.Get(c.O(1));
        var ctx = c.Event.Clone().Set("DirectEventScriptType", 0);
        // Parameters go to the next script only, not to the scripts it runs in turn.
        foreach (var key in ctx.Values.Keys.Where(k => k.StartsWith("param:", StringComparison.Ordinal)).ToList()) ctx.Values.Remove(key);
        foreach (var kv in c.Host.PendingParams) ctx.Set("param:" + kv.Key, kv.Value);
        c.Host.PendingParams.Clear();
        try { c.Host.Run(c.S(0), target?.Id ?? GameObject.InvalidId, ctx); }
        finally { c.Event.CopyPayloadFrom(ctx); }
        return null;
    }
    /// <summary>
    /// Compiles and runs a chunk like ExecuteScript (as the script "!Chunk"). Returns "" on success, the compile
    /// error as the original words it, or "ERROR: FILE NOT OPENED: " when the chunk stopped on a script error.
    /// </summary>
    [NwFunc("ExecuteScriptChunk")]
    static object? Chunk(Call c)
    {
        string chunk = c.S(0);
        string source = c.I(2, 1) != 0 ? "void main() {" + chunk + "}" : chunk;
        var ncs = Compiler.NssCompiler.Compile(source, "Chunk", c.Host.Definitions, c.Host.ScriptSource, out var error, mainOnly: true);
        if (ncs == null) return "ERROR: FILE NOT COMPILED SUCCESSFULLY: " + error + "\n"; // the original ends it with a newline
        var program = new NcsProgram("!Chunk", ncs);
        ScriptChunks.Attach(program, chunk);
        var target = c.World.Get(c.O(1));
        var ctx = c.Event.Clone().Set("DirectEventScriptType", 0);
        foreach (var key in ctx.Values.Keys.Where(k => k.StartsWith("param:", StringComparison.Ordinal)).ToList()) ctx.Values.Remove(key);
        foreach (var kv in c.Host.PendingParams) ctx.Set("param:" + kv.Key, kv.Value);
        c.Host.PendingParams.Clear();
        try { c.Host.Run(program, "!Chunk", target?.Id ?? GameObject.InvalidId, ctx); }
        finally { c.Event.CopyPayloadFrom(ctx); }
        return c.Host.LastRunFailed ? "ERROR: FILE NOT OPENED: " : "";
    }
    [NwFunc("GetScriptParam")] static object? GetParam(Call c) => c.Event.Str("param:" + c.S(0));
    [NwFunc("SetScriptParam")] static object? SetParam(Call c) { c.Host.PendingParams[c.S(0)] = c.S(1); return null; }
    [NwFunc("GetCurrentlyRunningEvent")]
    static object? RunningEvent(Call c) =>
        c.I(0, 1) != 0 || !c.Event.Values.ContainsKey("DirectEventScriptType")
            ? c.Event.Int("EventScriptType") : c.Event.Int("DirectEventScriptType");

    [NwFunc("ClearAllActions")]
    static object? Clear(Call c)
    {
        if (c.Self is { } o) ActionQueues.Clear(c.World, o);
        // bClearCombatState: out of combat at once (nwscript.nss; not measurable in a player-less parity area).
        if (c.I(0) != 0 && c.Self is Creature cr) cr.InCombat = false;
        return null;
    }
    [NwFunc("SetCommandable")]
    static object? SetCommandable(Call c)
    {
        if ((c.World.Get(c.O(1)) ?? c.Self) is { } o) o.Commandable = c.I(0) != 0;
        return null;
    }
    [NwFunc("GetCommandable")] static object? GetCommandable(Call c) => (c.World.Get(c.O(0)) ?? c.Self)?.Commandable == true ? 1 : 0;
    [NwFunc("GetCurrentAction")]
    static object? CurrentAction(Call c) =>
        (c.World.Get(c.O(0)) ?? c.Self) is { Actions.Count: > 0 } o ? o.Actions[0].ActionType : 65535;
    [NwFunc("GetNumActions")] static object? NumActions(Call c) => (c.World.Get(c.O(0)) ?? c.Self)?.Actions.Count ?? 0;

    [NwFunc("ActionWait")] static object? Wait(Call c) { Enqueue(c, c.Self, new WaitAction { Remaining = c.F(0) }); return null; }
    [NwFunc("ActionSpeakString")] static object? ActionSpeak(Call c) { Enqueue(c, c.Self, new SpeakAction { Text = c.S(0), Volume = c.I(1) }); return null; }
    [NwFunc("ActionSpeakStringByStrRef")]
    static object? ActionSpeakRef(Call c)
    {
        Enqueue(c, c.Self, new SpeakAction { Text = c.World.Resources.TlkString(unchecked((uint)c.I(0))), Volume = c.I(1) });
        return null;
    }
    [NwFunc("SpeakString")] static object? Speak(Call c) { if (c.Self is { } o) c.World.Speak(o, c.S(0), c.I(1)); return null; }
    [NwFunc("SpeakStringByStrRef")]
    static object? SpeakRef(Call c)
    {
        if (c.Self is { } o) c.World.Speak(o, c.World.Resources.TlkString(unchecked((uint)c.I(0))), c.I(1));
        return null;
    }
    [NwFunc("ActionPlayAnimation")]
    static object? ActionAnim(Call c)
    {
        Enqueue(c, c.Self, new AnimationAction { Animation = c.I(0), Speed = c.F(1, 1), Duration = c.F(2) });
        return null;
    }
    [NwFunc("PlayAnimation")]
    static object? Anim(Call c)
    {
        if (c.Self is { } o) c.World.Output?.PlayAnimation(o, c.I(0), c.F(1, 1), c.F(2));
        return null;
    }
    [NwFunc("ActionMoveToObject")]
    static object? MoveToObject(Call c)
    {
        // A target in another area: the route through doors and transition triggers (docs/INTER_AREA_MOVEMENT.md).
        if (OtherArea(c, c.Obj(0)))
            Enqueue(c, c.Self, new WorldObjects.InterAreaMoveAction { TargetObject = c.O(0), Run = c.I(1) != 0, Range = c.F(2, 1) });
        else Enqueue(c, c.Self, new MoveAction { TargetObject = c.O(0), Run = c.I(1) != 0, Range = c.F(2, 1), PassClosedDoors = true, TakeDoorTransition = true });
        return null;
    }

    /// <summary>Whether a move target is in an area other than the mover's.</summary>
    static bool OtherArea(Call c, GameObject? target) => target?.Area != null && c.Self is Creature { Area: { } here } && target.Area != here;
    /// <summary>
    /// ActionForceFollowObject: keep fFollowDistance from the object until the action queue is cleared (it never
    /// finishes on its own); waits while the leader is in another area. Like the original, a follower that has
    /// caught up waits until the leader is 3 m beyond the follow distance, runs when the leader runs, and comes to
    /// rest exactly at the follow distance.
    /// </summary>
    sealed class FollowAction : GameAction
    {
        public uint Leader;
        public float Distance;
        MoveAction? _move;
        bool _caughtUp;
        public override int ActionType => 35; // ACTION_FOLLOW
        public override ActionStatus Step(World world, GameObject owner, float dt)
        {
            if (world.Get(Leader) is not { } leader || leader.IsDestroyed) return ActionStatus.Failed;
            if (leader.Area != owner.Area || owner is not Creature me) return ActionStatus.InProgress;
            float d = System.Numerics.Vector2.Distance(new(leader.Position.X, leader.Position.Y), new(owner.Position.X, owner.Position.Y));
            if (_move == null)
            {
                if (d <= Distance + (_caughtUp ? 3 : 0.05f)) { _caughtUp = true; return ActionStatus.InProgress; }
                float near = leader is Creature lc ? MoveAction.PersonalSpace(world, me) + MoveAction.PersonalSpace(world, lc) + 0.1f : 0.3f;
                _move = new MoveAction { TargetObject = Leader, Range = Distance, MinRange = near, Timeout = float.MaxValue };
            }
            // Keep pace: run while the leader runs, and keep running to catch up once it stops.
            if (leader is Creature { Mode: not Net.MovementMode.Stand } moving) _move.Run = moving.Mode is Net.MovementMode.Run or Net.MovementMode.RunForwardLeft or Net.MovementMode.RunForwardRight or Net.MovementMode.MouseRun;
            if (_move.Step(world, owner, dt) != ActionStatus.InProgress) { _move = null; _caughtUp = true; }
            return ActionStatus.InProgress;
        }
        public override void Cancel(World world, GameObject owner) => _move?.Cancel(world, owner);
    }

    [NwFunc("ActionForceFollowObject")]
    static object? ForceFollow(Call c)
    {
        Enqueue(c, c.Self, new FollowAction { Leader = c.O(0), Distance = Math.Max(0, c.F(1)) });
        return null;
    }

    [NwFunc("ActionMoveToLocation")]
    static object? MoveToLocation(Call c)
    {
        var loc = c.L(0);
        // The original takes a move order even from a creature that isn't commandable (SetCommandable(FALSE), charmed,
        // frightened, knocked down...): parity suite condition_actions, uncommandable.walk and charm/fear/... .move.
        // A dying player does not (dying scene: walk_ordered left it where it lay).
        if (c.Self?.Area?.Id == loc.Area)
        {
            if (c.Self is Creature { IsDead: false, IsDying: false } mover) mover.Actions.Add(new MoveAction { Target = loc.Position, Run = c.I(1) != 0, PassClosedDoors = true });
            else Enqueue(c, c.Self, new MoveAction { Target = loc.Position, Run = c.I(1) != 0, PassClosedDoors = true });
        }
        // Another area: through the first transition of the route, then (measured) only as far as the next one.
        else if (c.World.Get(loc.Area) is Area && c.Self is Creature)
            Enqueue(c, c.Self, new WorldObjects.InterAreaMoveAction { Kind = WorldObjects.InterAreaMoveAction.Goal.Location, TargetLocation = loc, Run = c.I(1) != 0 });
        return null;
    }
    [NwFunc("ActionMoveAwayFromObject")]
    static object? MoveAway(Call c)
    {
        Enqueue(c, c.Self, new MoveAction { TargetObject = c.O(0), Run = c.I(1) != 0, Range = c.F(2, 40), Away = true });
        return null;
    }
    [NwFunc("ActionMoveAwayFromLocation")]
    static object? MoveAwayLoc(Call c)
    {
        Enqueue(c, c.Self, new MoveAction { Target = c.L(0).Position, Run = c.I(1) != 0, Range = c.F(2, 40), Away = true });
        return null;
    }
    [NwFunc("ActionForceMoveToObject")]
    static object? ForceMoveObj(Call c)
    {
        if (OtherArea(c, c.Obj(0)))
            Enqueue(c, c.Self, new WorldObjects.InterAreaMoveAction { TargetObject = c.O(0), Run = c.I(1) != 0, Range = c.F(2, 1), Timeout = c.F(3, 30), JumpOnTimeout = true });
        else Enqueue(c, c.Self, new MoveAction { TargetObject = c.O(0), Run = c.I(1) != 0, Range = c.F(2, 1), Timeout = c.F(3, 30), JumpOnTimeout = true });
        return null;
    }
    [NwFunc("ActionForceMoveToLocation")]
    static object? ForceMoveLoc(Call c)
    {
        var loc = c.L(0);
        if (c.Self is Creature { Area: { } here } && loc.Area != here.Id && c.World.Get(loc.Area) is Area)
            Enqueue(c, c.Self, new WorldObjects.InterAreaMoveAction { Kind = WorldObjects.InterAreaMoveAction.Goal.Location, TargetLocation = loc, Run = c.I(1) != 0, Timeout = c.F(2, 30), JumpOnTimeout = true });
        else Enqueue(c, c.Self, new MoveAction { Target = loc.Position, Run = c.I(1) != 0, Timeout = c.F(2, 30), JumpOnTimeout = true });
        return null;
    }
    [NwFunc("ActionRandomWalk")] static object? RandomWalk(Call c) { Enqueue(c, c.Self, new RandomWalkAction()); return null; }
    [NwFunc("ActionJumpToLocation")] static object? ActionJumpLoc(Call c) { Enqueue(c, c.Self, new JumpAction { Destination = c.L(0) }); return null; }
    [NwFunc("ActionJumpToObject")]
    static object? ActionJumpObj(Call c)
    {
        if (c.Obj(0) is { } t) Enqueue(c, c.Self, new JumpAction { Destination = c.Self is { } self ? JumpTarget(c.World, self, t) : t.Location });
        return null;
    }
    [NwFunc("JumpToLocation")]
    static object? JumpLoc(Call c)
    {
        if (c.Self is { } o) { ActionQueues.Clear(c.World, o); c.World.Jump(o, c.L(0)); }
        return null;
    }
    [NwFunc("JumpToObject")]
    static object? JumpObj(Call c)
    {
        if (c.Self is { } o && c.Obj(0) is { } t) { ActionQueues.Clear(c.World, o); c.World.Jump(o, JumpTarget(c.World, o, t)); }
        return null;
    }

    /// <summary>
    /// Where a jump to an object lands: its location, or for a door the spot a creature coming through it lands on,
    /// broken or not. A creature lands where its route through the door would put it (nwserver, door_bashed JPJ,
    /// JPR); a player where walking through the doorway puts a player (nwmain, scene transitfollow: JumpToObject to
    /// the Prelude's Training Halls doorway landed the player exactly where walking through it did).
    /// </summary>
    static Location JumpTarget(World world, GameObject self, GameObject t) => t is Door d
        ? self is Creature { IsPC: true } ? WorldObjects.TransitionService.Destination(world, d) : WorldObjects.TransitionService.NpcDestination(world, d)
        : t.Location;

    [NwFunc("EventUserDefined")] static object? UserEvent(Call c) => new ScriptEvent { Type = EventUserDefined, UserDefinedNumber = c.I(0) };
    [NwFunc("EventSpellCastAt")] static object? SpellCastAtEvent(Call c) => new ScriptEvent { Type = EventSpellCastAt, Caster = c.O(0), SpellId = c.I(1), Harmful = c.I(2) != 0 };
    [NwFunc("EventConversation")] static object? ConversationEvent(Call c) => new ScriptEvent { Type = EventDialogue };
    [NwFunc("EventActivateItem")] static object? ActivateItemEvent(Call c) => new ScriptEvent { Type = EventActivateItem, Caster = c.O(0), Position = c.L(1).Position, Location = c.L(1), Target = c.O(2) };
    [NwFunc("GetUserDefinedEventNumber")] static object? UserEventNumber(Call c) => c.Event.Int("UserDefined");

    [NwFunc("SignalEvent")]
    static object? Signal(Call c)
    {
        var target = c.Obj(0);
        var ev = c.Ev(1);
        if (target == null) return null;
        var ctx = new EventContext();
        string field;
        switch (ev.Type)
        {
            case EventUserDefined:
                ctx.Set("UserDefined", ev.UserDefinedNumber);
                field = target switch
                {
                    Module => "Mod_OnUsrDefined",
                    Area => "OnUserDefined",
                    Creature => "ScriptUserDefine",
                    Trigger => "ScriptUserDefine",
                    _ => "OnUserDefined",
                };
                break;
            case EventSpellCastAt:
                // An area of effect's event comes from its creator (nwserver ai_talent_use: an aura's heartbeat signals
                // with OBJECT_SELF and GetLastSpellCaster is the aura's owner).
                if (c.World.Get(ev.Caster) is AreaOfEffect signallingAoe)
                    ev = new ScriptEvent { Type = ev.Type, Caster = Society.SocietyService.AreaOfEffectCreator(signallingAoe), SpellId = ev.SpellId, Harmful = ev.Harmful };
                ctx.Set("SpellCaster", ev.Caster).Set("SpellId", ev.SpellId).Set("SpellHarmful", ev.Harmful ? 1 : 0);
                field = target is Creature ? "ScriptSpellAt" : "OnSpellCastAt";
                break;
            case EventDialogue:
                field = target is Creature ? "ScriptDialogue" : target is Door or Placeable ? "OnDialog" : "";
                break;
            // The module's OnActivateItem, with whoever signalled it as the activator (the Unique Power spell
            // script does this for the item's user).
            case EventActivateItem when target is Module:
                ctx.Set("ActivatedItem", ev.Caster).Set("Activator", c.Self?.Id ?? GameObject.InvalidId)
                    .Set("ActivatedTarget", ev.Target).Set("ActivatedLocation", ev.Location);
                field = "Mod_OnActvtItem";
                break;
            default:
                field = "";
                break;
        }
        // Events run after the current script, like the original's event queue.
        if (field.Length > 0)
        {
            double requestedAt = c.World.Time;
            var caster = c.World.Get(ev.Caster);
            bool harmfulSpell = ev.Type == EventSpellCastAt && ev.Harmful;
            c.World.After(0, () =>
            {
                if (ev.Type == EventSpellCastAt && !target.IsDestroyed)
                {
                    // The object keeps the event's caster and spell, and a harmful one makes the caster its last
                    // hostile actor, with or without an OnSpellCastAt script (nwserver ai_queries).
                    target.LastSpellCaster = ev.Caster;
                    target.LastSpellCastAt = ev.SpellId;
                    target.LastSpellHarmful = ev.Harmful;
                    if (ev.Harmful) Society.SocietyService.SetLastHostileActor(c.World, target, c.World.Get(ev.Caster));
                }
                c.World.RunEvent(target, field, ctx);
                // The native harmful-event callback still observes the prior combat flags.
                if (harmfulSpell) c.World.System<CombatSystem>().RecordScriptHostility(caster, target, requestedAt);
            });
        }
        return null;
    }

    static uint Ev(Call c, string key) => c.World.Get(c.Event.Obj(key)) != null ? c.Event.Obj(key) : GameObject.InvalidId;
    [NwFunc("GetEnteringObject")] static object? Entering(Call c) => Ev(c, "Entering");
    [NwFunc("GetExitingObject")] static object? Exiting(Call c) => Ev(c, "Exiting");
    [NwFunc("GetLastUsedBy")] static object? UsedBy(Call c) => Ev(c, "UsedBy");
    [NwFunc("GetClickingObject")] static object? Clicking(Call c) => Ev(c, "Clicking");
    [NwFunc("GetLastSpeaker")] static object? Speaker(Call c) => Ev(c, "Speaker");
    [NwFunc("GetPCSpeaker")] static object? PcSpeaker(Call c) => ConversationFunctions.Speaker(c)?.Id ?? GameObject.InvalidId;
    [NwFunc("GetLastOpenedBy")] static object? OpenedBy(Call c) => Ev(c, "OpenedBy");
    [NwFunc("GetLastClosedBy")] static object? ClosedBy(Call c) => Ev(c, "ClosedBy");
    [NwFunc("GetLastLocked")] static object? Locked(Call c) => Ev(c, "LockedBy");
    [NwFunc("GetLastUnlocked")] static object? Unlocked(Call c) => Ev(c, "UnlockedBy");
    [NwFunc("GetLastDisturbed")] static object? Disturbed(Call c) => Ev(c, "Disturber");
    [NwFunc("GetInventoryDisturbItem")] static object? DisturbItem(Call c) => Ev(c, "DisturbItem");
    [NwFunc("GetInventoryDisturbType")] static object? DisturbType(Call c) => c.Event.Int("DisturbType");
    [NwFunc("GetLastPCToCancelCutscene")]
    static object? CancelCutscene(Call c) =>
        c.World.Get(c.World.Module.LastCutsceneCanceller)?.Id ?? GameObject.InvalidId;
    [NwFunc("GetPlaceableLastClickedBy")] static object? PlcClicked(Call c) => Ev(c, "Clicking");
    // Inside OnSpellCastAt the event's values; elsewhere what the object last received (nwserver ai_queries).
    static bool InSpellCastAt(Call c) => c.Event.Values.ContainsKey("SpellHarmful");
    [NwFunc("GetLastSpellCaster")]
    static object? SpellCaster(Call c) =>
        InSpellCastAt(c) ? Ev(c, "SpellCaster") : c.Self is { } o && c.World.Get(o.LastSpellCaster) != null ? o.LastSpellCaster : GameObject.InvalidId;
    [NwFunc("GetLastSpell")] static object? LastSpell(Call c) => InSpellCastAt(c) ? c.Event.Int("SpellId") : c.Self?.LastSpellCastAt ?? -1;
    [NwFunc("GetLastSpellHarmful")]
    static object? SpellHarmful(Call c) =>
        InSpellCastAt(c) ? c.Event.Int("SpellHarmful") : c.Self?.LastSpellHarmful == true ? 1 : 0;
    [NwFunc("GetModuleItemAcquired")] static object? Acquired(Call c) => Ev(c, "AcquiredItem");
    [NwFunc("GetModuleItemAcquiredFrom")] static object? AcquiredFrom(Call c) => Ev(c, "AcquiredFrom");
    [NwFunc("GetModuleItemAcquiredBy")] static object? AcquiredBy(Call c) => Ev(c, "AcquiredBy");
    [NwFunc("GetModuleItemAcquiredStackSize")] static object? AcquiredStack(Call c) => c.Event.Int("AcquiredStack");
    [NwFunc("GetModuleItemLost")] static object? Lost(Call c) => Ev(c, "LostItem");
    [NwFunc("GetModuleItemLostBy")] static object? LostBy(Call c) => Ev(c, "LostBy");
    [NwFunc("GetItemActivated")] static object? Activated(Call c) => Ev(c, "ActivatedItem");
    [NwFunc("GetItemActivator")] static object? Activator(Call c) => Ev(c, "Activator");
    [NwFunc("GetItemActivatedTarget")] static object? ActivatedTarget(Call c) => Ev(c, "ActivatedTarget");
    [NwFunc("GetItemActivatedTargetLocation")]
    static object? ActivatedLocation(Call c) =>
        c.Event.Values.TryGetValue("ActivatedLocation", out var l) && l is Location loc ? loc : Location.Invalid;
    [NwFunc("GetPCLevellingUp")] static object? LevellingUp(Call c) => Ev(c, "LevellingUp");
    [NwFunc("GetLastPlayerDied")] static object? PlayerDied(Call c) => Ev(c, "PlayerDied");
    [NwFunc("GetLastPlayerDying")] static object? PlayerDying(Call c) => Ev(c, "PlayerDying");
    [NwFunc("GetLastRespawnButtonPresser")] static object? Respawner(Call c) => Ev(c, "Respawner");
    [NwFunc("GetLastPCRested")] static object? Rested(Call c) => Ev(c, "Rester");
    [NwFunc("GetLastRestEventType")] static object? RestType(Call c) => c.Event.Int("RestEventType");
    [NwFunc("GetPCChatSpeaker")] static object? ChatSpeaker(Call c) => Ev(c, "ChatSpeaker");
    [NwFunc("GetPCChatMessage")] static object? ChatMessage(Call c) => c.Event.Str("ChatMessage");
    [NwFunc("GetPCChatVolume")] static object? ChatVolume(Call c) => c.Event.Int("ChatVolume");
    [NwFunc("SetPCChatMessage")] static object? SetChat(Call c) { c.Event.Set("ChatMessage", c.S(0)); c.Event.Set("ChatChanged", 1); return null; }
    [NwFunc("SetPCChatVolume")] static object? SetChatVolume(Call c) { c.Event.Set("ChatVolume", c.I(0)); return null; }

    [NwFunc("SendMessageToPC")]
    static object? Message(Call c)
    {
        if (c.Obj<Creature>(0) is { } pc && Players.FamiliarPossession.SessionOf(pc) != null) c.World.Output?.SendMessage(pc, c.S(1));
        return null;
    }
    [NwFunc("SendMessageToPCByStrRef")]
    static object? MessageRef(Call c)
    {
        if (c.Obj<Creature>(0) is { } pc && Players.FamiliarPossession.SessionOf(pc) != null) c.World.Output?.SendMessage(pc, c.World.Resources.TlkString(unchecked((uint)c.I(1))));
        return null;
    }
    [NwFunc("SendMessageToAllDMs")]
    static object? MessageDMs(Call c)
    {
        foreach (var pc in c.World.AllObjects.OfType<Creature>().Where(x => x.Player?.IsDM == true)) c.World.Output?.SendMessage(pc, c.S(0));
        return null;
    }
    [NwFunc("FloatingTextStringOnCreature")]
    static object? Floating(Call c)
    {
        if (c.Obj<Creature>(1) is { } cr) c.World.Output?.FloatingText(cr, c.S(0), c.I(2, 1) != 0);
        return null;
    }
    [NwFunc("FloatingTextStrRefOnCreature")]
    static object? FloatingRef(Call c)
    {
        if (c.Obj<Creature>(1) is { } cr) c.World.Output?.FloatingText(cr, c.World.Resources.TlkString(unchecked((uint)c.I(0))), c.I(2, 1) != 0);
        return null;
    }
}
