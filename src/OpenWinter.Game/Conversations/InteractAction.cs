using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Conversations;

/// <summary>
/// What clicking an object does once the player has walked up to it:
/// creatures get their OnConversation event (whose default script calls
/// BeginConversation), placeables with a dialogue start it, other placeables
/// get OnUsed.
/// </summary>
public sealed class InteractAction : GameAction
{
    public required uint Target;
    public bool Talk;
    public int Type = 15;
    /// <summary>Mouse-use presentation only; scripts and the /loot helper retain their existing behavior.</summary>
    public bool Clicked;
    float _useRemaining;
    public override int ActionType => Type; // ACTION_DIALOGOBJECT (7) or ACTION_USEOBJECT (15)

    public override ActionStatus Step(World world, GameObject owner, float dt)
    {
        if (owner is not Creature pc || world.Get(Target) is not { } target) return ActionStatus.Failed;
        if (_useRemaining > 0)
        {
            _useRemaining -= dt;
            return _useRemaining > 1e-5f ? ActionStatus.InProgress : ActionStatus.Done;
        }
        var convo = world.System<ConversationService>();
        if (Talk && target is Door or Placeable)
        {
            if (!convo.RunObjectDialog(target, pc)) convo.Start(target, pc, "");
            return ActionStatus.Done;
        }
        switch (target)
        {
            case Creature npc when npc != pc && !npc.IsDead && world.Factions.IsEnemy(npc, pc):
                // The attack follows this action on the queue (see Combat.AttackAction).
                pc.Actions.Insert(Math.Min(1, pc.Actions.Count), new Combat.AttackAction { Target = npc.Id });
                break;
            case Creature npc when npc != pc:
                // A creature in combat is busy; a dead one says nothing (ConversationService.Refuses).
                if (convo.Refuses(pc, npc)) break;
                if (npc.Script("ScriptDialogue").Length > 0)
                    world.RunEvent(npc, "ScriptDialogue", new EventContext().Set("Speaker", pc.Id).Set("PCSpeaker", pc.Id).Set("ListenPatternNumber", -1));
                else convo.Start(npc, pc, npc.Conversation);
                break;
            // Containers and locked placeables: unlock, traps, then open (and show the loot window).
            case Placeable p when p.HasInventory || p.Locked:
                if (WorldObjects.DoorService.Use(world, p, pc) && p.HasInventory && Clicked)
                {
                    var direction = p.Position - pc.Position;
                    if (direction.X * direction.X + direction.Y * direction.Y > 1e-6f)
                        pc.Facing = MathF.Atan2(direction.Y, direction.X);
                    world.MarkChanged(pc);
                    // The short engine reach is getmid, not the looping NWScript getmidlp.
                    world.Output?.Notify("object-use", pc, new { target = p.Id, animation = "getmid" });
                    // A native click stays in ACTION_USEOBJECT during the half-second reach.
                    _useRemaining = 0.5f;
                    return ActionStatus.InProgress;
                }
                break;
            case Placeable p:
                var dlg = p.Template.GetString("Conversation");
                if (dlg.Length > 0 && Talk) convo.Start(p, pc, dlg);
                else if (p.Useable)
                {
                    world.RunEvent(p, "OnUsed", new EventContext().Set("UsedBy", pc.Id));
                    if (dlg.Length > 0 && !convo.IsInConversation(pc)) convo.Start(p, pc, dlg);
                }
                break;
            case Door d:
                world.RunEvent(d, "OnClick", new EventContext().Set("Clicking", pc.Id));
                break;
        }
        return ActionStatus.Done;
    }
}
