using System.Text.Json;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class DoorInteractionTests
{
    [DataFact]
    public void DoorClicksKeepTheOpeningReachAndRecoveryAheadOfFurtherClicks()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Door timing tester");
        var world = f.Server.World; var pc = session.Creature!;
        var door = new Door { GenericType = 0 };
        world.AddToArea(door, pc.Area!, pc.Position + new System.Numerics.Vector3(0, 1, 0), 0);
        void Tick(int count) { for (int i = 0; i < count; i++) f.Server.Tick(1f / 30); }
        void Request(ActionKind kind) => client.Send(new ActionRequestMessage { Action = kind, Target = door.Id }.Write());
        Request(ActionKind.OpenDoor); Tick(15);
        Assert.False(door.IsOpen);
        var opening = Assert.IsType<OpenDoorAction>(pc.Actions[0]);
        Assert.Equal(65535, opening.ActionType);
        Request(ActionKind.Use); Tick(2);
        Assert.Same(opening, pc.Actions[0]); // ordinary left click cannot restart/skip the reach
        Tick(16); Assert.True(door.IsOpen);
        var recovery = Assert.IsType<DoorRecoveryAction>(pc.Actions[0]);
        Request(ActionKind.CloseDoor); Tick(3);
        Assert.Same(recovery, pc.Actions[0]); // radial action obeys the same recovery
        Assert.True(door.IsOpen);
        Tick(90); Assert.False(door.IsOpen);
    }

    [DataFact]
    public void DoorSelfActionsRemainImmediateAndScriptClearingCanCancelAReach()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, _) = f.Join("Script door tester");
        var world = f.Server.World; var pc = session.Creature!;
        var door = new Door { GenericType = 0 };
        world.AddToArea(door, pc.Area!, pc.Position + new System.Numerics.Vector3(0, 1, 0), 0);
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(ActionStatus.Done, new OpenDoorAction { Target = door.Id }.Step(world, door, 0));
            Assert.True(door.IsOpen);
            Assert.Equal(ActionStatus.Done, new CloseDoorAction { Target = door.Id }.Step(world, door, 0));
            Assert.False(door.IsOpen);
        }
        pc.Actions.Add(new OpenDoorAction { Target = door.Id });
        f.Server.Tick(.1f);
        ActionQueues.Clear(world, pc);
        for (int i = 0; i < 60; i++) f.Server.Tick(1f / 30);
        Assert.False(door.IsOpen);
    }

    [DataFact]
    public void TutorialDoorOpenAndUnlockHaveSeparateFeedbackAndBothRunInstalledOneLiner()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Door feedback");
        var world = f.Server.World;
        var pc = session.Creature!;
        using var prelude = OpenWinter.Aurora.Resources.ResMan.ForModule(TestData.Paths,
            Path.Combine(TestData.Paths.GameRoot, "data", "nwm", "Prelude.nwm"));
        var template = prelude.ReadGff("m0q1a", OpenWinter.Aurora.Resources.ResType.Git)!
            .GetList("Door List").Single(d => d.GetString("Tag") == "M1Q0A_M1Q0B");
        Assert.Equal("nw_od_feedback8", template.GetString("OnFailToOpen"));
        var door = new Door { Template = template, Name = template.GetLocString("LocName"), Locked = template.GetInt("Locked") != 0 };
        WorldObjectFields.Load(door, template);
        world.AddToArea(door, pc.Area!, pc.Position, 0);
        var bytes = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile(
            "void main(){SetLocalInt(OBJECT_SELF,\"fails\",GetLocalInt(OBJECT_SELF,\"fails\")+1);"
            + "SetLocalObject(OBJECT_SELF,\"clicker\",GetClickingObject());ExecuteScript(\"nw_od_feedback8\",OBJECT_SELF);}",
            "feedback_fail", world.Scripts!.Definitions, _ => null, out var error);
        Assert.True(bytes != null, error);
        world.Scripts.Register("feedback_fail", new OpenWinter.Game.Scripting.NcsProgram("feedback_fail", bytes!));
        door.Scripts["OnFailToOpen"] = "feedback_fail";
        f.Server.Tick(.01f); ModuleTransitionTests.Fixture.Drain(client);
        string speech = world.Resources.ReadGff("nw_all_feedback", OpenWinter.Aurora.Resources.ResType.Dlg)!
            .GetList("EntryList")[6].GetLocString("Text").Resolve(world.Resources.Talk);
        int count = 0;
        foreach (var (unlock, required, custom) in new[]
            { (false, true, false), (true, true, false), (false, false, false), (true, true, true), (false, true, true) })
        {
            door.KeyRequired = required ? 1 : 0;
            door.Lock.KeyFeedback = custom ? "Fixture key required." : "";
            Assert.False(unlock ? DoorService.Unlock(world, door, pc) : DoorService.Open(world, door, pc));
            f.Server.Tick(.01f);
            var packets = ModuleTransitionTests.Fixture.Drain(client);
            var chats = packets.Where(p => (MessageType)p[0] == MessageType.Chat)
                .Select(p => ChatMessage.Read(new MessageReader(p))).ToList();
            Assert.Equal(2, chats.Count);
            Assert.Equal(ChatChannel.Server, chats[0].Channel);
            Assert.Equal(0u, chats[0].Speaker); // chat only, never a speech bubble
            Assert.Equal(unlock ? custom ? "Fixture key required." : world.Resources.TlkString(7944)
                : world.Resources.TlkString(8296), chats[0].Text);
            Assert.Equal(ChatChannel.Talk, chats[1].Channel);
            Assert.Equal(door.Id, chats[1].Speaker);
            Assert.Equal(door.Name.Resolve(world.Resources.Talk), chats[1].SpeakerName);
            Assert.Equal(speech, chats[1].Text);
            var overhead = packets.Where(p => (MessageType)p[0] == MessageType.Notify)
                .Select(p => NotifyMessage.Read(new MessageReader(p))).Where(n => n.Kind == "lock-feedback").ToList();
            Assert.Equal(unlock ? 0 : 1, overhead.Count);
            if (!unlock)
            {
                Assert.Equal(pc.Id, overhead[0].Subject);
                using var json = JsonDocument.Parse(overhead[0].Json);
                Assert.Equal(world.Resources.TlkString(8307), json.RootElement.GetProperty("feedback").GetString());
            }
            Assert.Equal(++count, door.Locals.Ints["fails"]);
            Assert.Equal(pc.Id, door.Locals.Objects["clicker"]);
            Assert.Equal(0, door.Locals.Ints["NW_L_FEEDBACK"]); // installed queued reset still runs
        }
        // The same default-conversation lookup must also work for placeable one-liners.
        var placeable = new Placeable { Template = template };
        world.AddToArea(placeable, pc.Area!, pc.Position, 0);
        world.Scripts.Run("nw_od_feedback8", placeable.Id);
        var line = Assert.Single(ModuleTransitionTests.Fixture.Drain(client).Where(p => (MessageType)p[0] == MessageType.Chat)
            .Select(p => ChatMessage.Read(new MessageReader(p))));
        Assert.Equal(placeable.Id, line.Speaker);
        Assert.Equal(speech, line.Text);
    }

    [DataFact]
    public void ScriptUnlinkPublishesRemovalOfAuthoredTransition()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Unlink tester");
        var world = f.Server.World;
        var door = new Door { LinkedTo = "old_destination", LinkedToFlags = 1, IsOpen = true };
        world.AddToArea(door, session.Creature!.Area!, session.Creature.Position, 0);
        f.Server.Tick(.01f); ModuleTransitionTests.Fixture.Drain(client);
        var bytes = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile(
            "void main(){SetTransitionTarget(OBJECT_SELF, OBJECT_INVALID);}", "unlink_door",
            world.Scripts!.Definitions, _ => null, out var error);
        Assert.True(bytes != null, error);
        world.Scripts.Register("unlink_door", new OpenWinter.Game.Scripting.NcsProgram("unlink_door", bytes!));
        world.Scripts.Run("unlink_door", door.Id);
        f.Server.Tick(.01f);
        Assert.False(door.IsTransition);
        Assert.Null(TransitionService.ResolveTarget(world, door));
        var update = ModuleTransitionTests.Fixture.Drain(client).Where(p => (MessageType)p[0] == MessageType.Update)
            .Select(p => UpdateMessage.Read(new MessageReader(p))).Single(p => p.ObjectId == door.Id);
        Assert.Equal(ObjectFlags.None, update.Flags & ObjectFlags.Transition);
        Assert.NotEqual(ObjectFlags.None, update.Flags & ObjectFlags.Open);
    }

    [DataFact]
    public void MissingLinkTargetIsLookedUpAgainOnceAScriptCreatesIt()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Late link tester");
        var world = f.Server.World;
        var pc = session.Creature!;
        var area = pc.Area!;
        var door = new Door { GenericType = 0, LinkedTo = "late_destination", LinkedToFlags = 2 };
        world.AddToArea(door, area, pc.Position + new System.Numerics.Vector3(0, 1, 0), 0);
        void Tick(int count = 90) { for (int i = 0; i < count; i++) f.Server.Tick(1f / 30); }
        Tick(1); ModuleTransitionTests.Fixture.Drain(client);
        // Opening syncs the linked door, which looks the link up before its destination exists.
        Assert.True(DoorService.Open(world, door, pc));
        Assert.Null(TransitionService.ResolveTarget(world, door));
        Assert.True(door.IsTransition);
        Tick(1);
        var update = ModuleTransitionTests.Fixture.Drain(client).Where(p => (MessageType)p[0] == MessageType.Update)
            .Select(p => UpdateMessage.Read(new MessageReader(p))).Last(p => p.ObjectId == door.Id);
        Assert.Equal(ObjectFlags.Open | ObjectFlags.Transition, update.Flags & (ObjectFlags.Open | ObjectFlags.Transition));
        var destination = new Waypoint { Tag = "late_destination" };
        world.AddToArea(destination, area, pc.Position + new System.Numerics.Vector3(6, 0, 0), 0);
        Assert.Same(destination, TransitionService.ResolveTarget(world, door));
        Assert.True(door.IsTransition);
        client.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = door.Id }.Write());
        Tick();
        Assert.InRange(System.Numerics.Vector3.Distance(pc.Position, destination.Position), 0, .05f);
    }

    [DataFact]
    public void SetEventScriptOnClickPublishesTheDoorTransitionFlag()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Click script tester");
        var world = f.Server.World;
        var door = new Door { GenericType = 0, IsOpen = true };
        world.AddToArea(door, session.Creature!.Area!, session.Creature.Position, 0);
        f.Server.Tick(.01f); ModuleTransitionTests.Fixture.Drain(client);
        void Run(string name, string body)
        {
            var bytes = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile("void main(){" + body + "}", name,
                world.Scripts!.Definitions, _ => null, out var error);
            Assert.True(bytes != null, error);
            world.Scripts.Register(name, new OpenWinter.Game.Scripting.NcsProgram(name, bytes!));
            world.Scripts.Run(name, door.Id);
            f.Server.Tick(.01f);
        }
        ObjectFlags Published() => ModuleTransitionTests.Fixture.Drain(client).Where(p => (MessageType)p[0] == MessageType.Update)
            .Select(p => UpdateMessage.Read(new MessageReader(p))).Last(p => p.ObjectId == door.Id).Flags;
        Run("add_click", "SetEventScript(OBJECT_SELF, 10012 /* EVENT_SCRIPT_DOOR_ON_CLICK */, \"door_click\");");
        Assert.Equal("door_click", door.Script("OnClick"));
        Assert.NotEqual(ObjectFlags.None, Published() & ObjectFlags.Transition);
        Run("remove_click", "SetEventScript(OBJECT_SELF, 10012 /* EVENT_SCRIPT_DOOR_ON_CLICK */, \"\");");
        Assert.Equal("", door.Script("OnClick"));
        Assert.Equal(ObjectFlags.None, Published() & ObjectFlags.Transition);
    }

    [DataFact]
    public void DoorMenuClosesLinkedDoorWhileUseTransitionsAndReplicatesFlags()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Door tester");
        var world = f.Server.World;
        var pc = session.Creature!;
        var area = pc.Area!;
        var destination = new Waypoint { Tag = "destination" };
        world.AddToArea(destination, area, pc.Position + new System.Numerics.Vector3(6, 0, 0), 0);
        var door = new Door { GenericType = 0, IsOpen = true, OpenedSide = 2, LinkedTo = destination.Tag, LinkedToFlags = 2 };
        world.AddToArea(door, area, pc.Position + new System.Numerics.Vector3(0, 1, 0), 0);
        void Tick(int count = 90) { for (int i = 0; i < count; i++) f.Server.Tick(1f / 30); }
        void Request(ActionKind kind) => client.Send(new ActionRequestMessage { Action = kind, Target = door.Id }.Write());
        Tick(1);
        var spawn = ModuleTransitionTests.Fixture.Drain(client).Where(p => (MessageType)p[0] == MessageType.Spawn)
            .Select(p => SpawnMessage.Read(new MessageReader(p))).Single(p => p.ObjectId == door.Id);
        Assert.Equal(ObjectFlags.Open | ObjectFlags.Transition | ObjectFlags.OpenSide2,
            spawn.Flags & (ObjectFlags.Open | ObjectFlags.Transition | ObjectFlags.OpenSide2));
        Request(ActionKind.CloseDoor); Tick();
        Assert.False(door.IsOpen);
        Assert.True(System.Numerics.Vector3.Distance(pc.Position, destination.Position) > 3);
        Request(ActionKind.Use); Tick();
        Assert.True(door.IsOpen); // first click opens, without transitioning
        Assert.True(System.Numerics.Vector3.Distance(pc.Position, destination.Position) > 3);
        Request(ActionKind.Use); Tick();
        Assert.True(door.IsOpen);
        Assert.InRange(System.Numerics.Vector3.Distance(pc.Position, destination.Position), 0, .05f);
        var sounds = ModuleTransitionTests.Fixture.Drain(client).Where(p => (MessageType)p[0] == MessageType.Notify)
            .Select(p => NotifyMessage.Read(new MessageReader(p))).Where(n => n.Kind == "play-sound")
            .Select(n => JsonDocument.Parse(n.Json).RootElement.GetProperty("resref").GetString()).ToList();
        Assert.Equal(new[] { "as_dr_woodmedcl1", "as_dr_woodmedop1" }, sounds);
    }

    [DataFact]
    public void SoundAppearancesCoverGenericTilesetLockedNoopAndDestruction()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Door sounds");
        var world = f.Server.World;
        var pc = session.Creature!;
        foreach (var (appearance, generic, prefix) in new[] { (0, 0, "woodmed"), (0, 5, "metlmed"), (1, 0, "woodlg") })
        {
            var door = new Door { Appearance = appearance, GenericType = generic, Locked = true };
            world.AddToArea(door, pc.Area!, pc.Position, 0);
            f.Server.Tick(.01f); ModuleTransitionTests.Fixture.Drain(client);
            Assert.False(DoorService.Open(world, door, pc));
            door.Locked = false;
            Assert.True(DoorService.Open(world, door, pc));
            Assert.True(DoorService.Open(world, door, pc)); // no duplicate sound
            Assert.True(DoorService.Close(world, door, pc));
            Assert.True(DoorService.Close(world, door, pc));
            OpenWinter.Game.Rules.HitPoints.Kill(world, door, pc.Id);
            var notes = ModuleTransitionTests.Fixture.Drain(client).Where(p => (MessageType)p[0] == MessageType.Notify)
                .Select(p => NotifyMessage.Read(new MessageReader(p))).Where(n => n.Kind == "play-sound")
                .Select(n => JsonDocument.Parse(n.Json).RootElement.Clone()).ToList();
            Assert.Equal(4, notes.Count);
            Assert.Equal(generic == 5 ? "as_dr_locked3" : "as_dr_locked1", notes[0].GetProperty("resref").GetString());
            Assert.Equal("as_dr_" + prefix + "op1", notes[1].GetProperty("resref").GetString());
            Assert.Equal("as_dr_" + prefix + "cl1", notes[2].GetProperty("resref").GetString());
            Assert.Equal(generic == 5 ? "cb_bu_metallrg" : "cb_bu_woodlrg", notes[3].GetProperty("resref").GetString());
            Assert.True(notes[3].GetProperty("detached").GetBoolean());
            // A killed door without a transition stands broken for 2 s before it goes (parity suite door_bashed).
            f.Server.Tick(.01f);
            Assert.False(door.IsDestroyed);
            for (int i = 0; i < 70; i++) f.Server.Tick(1f / 30);
            Assert.True(door.IsDestroyed);
        }
    }
}
