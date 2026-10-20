using OpenWinter.Game.Conversations;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>Conversation starting, tokens and state.</summary>
static class ConversationFunctions
{
    static ConversationService Convo(Call c) => c.World.System<ConversationService>();

    /// <summary>
    /// Starts a conversation as an action (ActionStartConversation). As in the original (parity/visual scene talk):
    /// the caller first runs up to the other party, to 2.2 m of a creature (a placeable or door at its use point),
    /// reporting ACTION_DIALOGOBJECT all along; a player talking to a creature with no dialogue named runs the
    /// creature's OnConversation script, as clicking it does. A creature asking another creature runs that one's
    /// OnConversation. Once there, a partner in combat or dead refuses (<see cref="ConversationService.Refuses"/>;
    /// convobusy2 far_npc_attacking: the refusal came when the player arrived).
    /// </summary>
    sealed class StartConversationAction : GameAction
    {
        public required uint Target;
        public required string ResRef;
        public bool Private;
        MoveAction? _approach;
        bool _approached;
        public override int ActionType => 7; // ACTION_DIALOGOBJECT
        const float Reach = 2.2f;

        public override ActionStatus Step(World world, GameObject owner, float dt)
        {
            var target = world.Get(Target);
            if (target != null && target != owner && owner is Creature mover && target.Area == owner.Area)
            {
                if (_approach == null)
                {
                    var d = target.Position - owner.Position;
                    _approach = new MoveAction { TargetObject = Target, Run = true, Range = Reach };
                    _approached = target is Creature && d.X * d.X + d.Y * d.Y <= Reach * Reach; // already close enough
                }
                // A move that fails (blocked) still ends in the conversation.
                if (!_approached && _approach.Step(world, mover, dt) == ActionStatus.InProgress) return ActionStatus.InProgress;
                _approached = true;
            }
            var convo = world.System<ConversationService>();
            // A partner in combat or dead refuses, with the original's message to the player (ConversationService.Refuses).
            if (target == null || convo.Refuses(owner, target)) return ActionStatus.Done;
            // Either side can be the player: the non-player owns the dialogue.
            if (owner is Creature { IsPC: true } pc)
            {
                if (convo.RunObjectDialog(target, pc, ResRef)) return ActionStatus.Done;
                if (ResRef.Length == 0 && target is Creature npc && npc != pc && npc.Script("ScriptDialogue").Length > 0)
                    world.RunEvent(npc, "ScriptDialogue", new EventContext().Set("Speaker", pc.Id).Set("PCSpeaker", pc.Id).Set("ListenPatternNumber", -1));
                else convo.Start(target, pc, ResRef, Private);
            }
            else if (target is Creature { IsPC: true } tpc) convo.Start(owner, tpc, ResRef, Private);
            // Between two creatures the original only asks the partner: its OnConversation runs with the talker as the
            // last speaker, whatever dialog was named (parity suite convo_busy). The conversation the partner's script
            // may then begin between two creatures is not implemented (docs/CONVERSATION_ENDINGS.md).
            else if (owner is Creature && target is Creature npc && npc != owner)
                world.RunEvent(npc, "ScriptDialogue", new EventContext().Set("Speaker", owner.Id).Set("ListenPatternNumber", -1));
            return ActionStatus.Done;
        }

        public override void Cancel(World world, GameObject owner) => _approach?.Cancel(world, owner);
    }

    // void ActionStartConversation(object oObjectToConverseWith, string sDialogResRef="", int bPrivateConversation=FALSE, int bPlayHello=TRUE)
    [NwFunc("ActionStartConversation")]
    static object? ActionStart(Call c)
    {
        if (c.Self is { Commandable: true } self)
            self.Actions.Add(new StartConversationAction { Target = c.O(0), ResRef = c.S(1), Private = c.I(2) != 0 });
        return null;
    }

    // int BeginConversation(string sResRef="", object oObjectToDialog=OBJECT_INVALID)
    [NwFunc("BeginConversation")]
    static object? Begin(Call c)
    {
        var self = c.Self;
        var other = c.Obj(1) ?? c.World.Get(c.Event.Obj("DialogInitiator")) ?? c.World.Get(c.Event.Obj("Speaker")) ?? c.World.Get(c.Event.Obj("PCSpeaker"));
        if (self == null || other is not Creature { IsPC: true } pc) return 0;
        string resref = c.S(0);
        if (resref.Length == 0) resref = c.Event.Str("DialogResRef");
        return Convo(c).Start(self, pc, resref) ? 1 : 0;
    }

    // void SpeakOneLinerConversation(string sDialogResRef="", object oTokenTarget=OBJECT_TYPE_INVALID)
    [NwFunc("SpeakOneLinerConversation")]
    static object? OneLiner(Call c)
    {
        if (c.Self is { } self) Convo(c).OneLiner(self, c.S(0), c.Obj<Creature>(1));
        return null;
    }

    // void SetCustomToken(int nCustomTokenNumber, string sTokenValue)
    [NwFunc("SetCustomToken")] static object? CustomToken(Call c) { Convo(c).CustomTokens[c.I(0)] = c.S(1); return null; }

    // int IsInConversation(object oObject)
    [NwFunc("IsInConversation")] static object? InConversation(Call c) => c.Obj(0) is { } o && Convo(c).IsInConversation(o) ? 1 : 0;

    [NwFunc("ActionPauseConversation")]
    static object? Pause(Call c)
    {
        if (Speaker(c) is { } pc) Convo(c).Pause(pc, true);
        return null;
    }

    [NwFunc("ActionResumeConversation")]
    static object? Resume(Call c)
    {
        if (Speaker(c) is { } pc) Convo(c).Pause(pc, false);
        return null;
    }

    /// <summary>
    /// The player in the conversation the caller is part of: the event's PC speaker, or (in a DelayCommand, where
    /// the dialogue's event is gone) whoever the caller is talking with. Scripts pause a conversation, act, then
    /// resume it from a delayed command.
    /// </summary>
    internal static Creature? Speaker(Call c) =>
        c.World.Get(c.Event.Obj("PCSpeaker")) as Creature ?? (c.Self is { } self && Convo(c).SessionWith(self) is { NoSpeaker: false } s ? s.Pc : null);
}
