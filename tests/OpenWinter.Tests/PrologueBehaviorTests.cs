using System.Numerics;
using System.Text.Json;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Functions;
using OpenWinter.Game.Spells;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class PrologueBehaviorTests
{
    static List<byte[]> Drain(LoopbackConnection c) => ModuleTransitionTests.Fixture.Drain(c);
    static uint Token(LoopbackConnection c) => Drain(c).Where(p => (MessageType)p[0] == MessageType.LoadArea)
        .Select(p => LoadAreaMessage.Read(new MessageReader(p)).LoadToken).Last();
    static void Ready(LoopbackConnection c, uint token) => c.Send(new ClientRequestMessage { Kind = "area-ready", Json = JsonSerializer.Serialize(new { token }) }.Write());

    [DataFact]
    public void LoadingPlayerIsPrivateAndInvulnerableUntilMatchingReady()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (observer, other) = f.Join("Observer");
        var (client, wire) = LoopbackConnection.CreatePair();
        var session = f.Server.Accept(wire);
        client.Send(new HelloMessage { PlayerName = "Loading", AreaReadyHandshake = true }.Write());
        f.Server.Tick(.01f);
        var pc = session.Creature!; var area = session.Area!; var world = f.Server.World;
        uint token = Token(client);
        Assert.NotEqual(0u, token);
        Assert.True(session.IsAreaLoading);
        Assert.Null(pc.Area);
        Assert.DoesNotContain(pc, area.Objects);
        Assert.DoesNotContain(pc.Id, observer.Known);
        int hp = pc.CurrentHitPoints;
        HitPoints.Damage(world, pc, observer.Creature!.Id, 999, 1);
        HitPoints.Kill(world, pc, observer.Creature.Id);
        Assert.Equal(hp, pc.CurrentHitPoints);
        Assert.False(pc.IsDead);
        client.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = new Vector3(10, 10, 0) }.Write());
        client.Send(new ClientRequestMessage { Kind = "rest" }.Write());
        client.Send(new ClientRequestMessage { Kind = "area-ready", Json = "{" }.Write());
        client.Send(new ClientRequestMessage { Kind = "area-ready", Json = "[]" }.Write());
        client.Send(new ClientRequestMessage { Kind = "area-ready", Json = "{\"token\":\"wrong-type\"}" }.Write());
        f.Server.Tick(.1f); Assert.Empty(pc.Actions);
        Assert.False(CastSpellAction.CanAct(world, pc));
        Assert.False(OpenWinter.Game.Combat.CombatSystem.CanAttack(pc));
        Ready(client, token + 1); f.Server.Tick(.01f); Assert.Null(pc.Area);
        Ready(client, token); f.Server.Tick(.01f);
        Assert.False(session.IsAreaLoading);
        Assert.Same(area, pc.Area);
        Assert.Contains(pc.Id, observer.Known);
        Ready(client, token); f.Server.Tick(.01f);
        Assert.Single(area.Objects, o => o == pc);
        // A second load supersedes the previous token even when loading the same area.
        var party = OpenWinter.Game.Society.PartyService.Of(world);
        Assert.True(party.Join(pc, observer.Creature!));
        int faction = pc.Faction;
        session.AreaReadyHandshake = true;
        f.Server.EnterArea(session, area, pc.Position, pc.Facing);
        uint next = Token(client);
        Assert.Contains(pc, party.Members(faction));
        Ready(client, token); f.Server.Tick(.01f); Assert.Null(pc.Area);
        Ready(client, next); f.Server.Tick(.01f); Assert.Same(area, pc.Area);
        Assert.Contains(pc, party.Members(faction));
        Assert.Equal(faction, pc.Faction);
    }

    [DataFact]
    public void AlreadyPlacedCreatureStaysInTheWorldAndCanStillPossess()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Loaded");
        var world = f.Server.World; var pc = session.Creature!; var area = pc.Area!;
        var familiar = world.Creatures.FromBlueprint("nw_fm_bat01")!;
        familiar.Scripts.Clear(); world.AddToArea(familiar, area, pc.Position, 0);
        OpenWinter.Game.Society.AssociateService.Add(world, pc, familiar, 3);
        session.AreaReadyHandshake = true;
        // Save loads and ending possession re-show an area the creature is already standing in.
        f.Server.EnterArea(session, area, pc.Position, pc.Facing, alreadyPlaced: true);
        Assert.Equal(0u, Token(client));
        Assert.False(session.IsAreaLoading);
        Assert.Same(area, pc.Area);
        Assert.True(world.System<OpenWinter.Game.Players.FamiliarPossession>().Possess(session, familiar));
    }

    [DataFact]
    public void ReleasingAFamiliarMidLoadPlacesItAndReturnsControl()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Master");
        var world = f.Server.World; var master = session.Creature!; var area = master.Area!;
        var familiar = world.Creatures.FromBlueprint("nw_fm_bat01")!;
        familiar.Scripts.Clear(); world.AddToArea(familiar, area, master.Position + Vector3.UnitX, 0);
        OpenWinter.Game.Society.AssociateService.Add(world, master, familiar, 3);
        var possession = world.System<OpenWinter.Game.Players.FamiliarPossession>();
        Assert.True(possession.Possess(session, familiar));
        session.AreaReadyHandshake = true;
        f.Server.EnterArea(session, area, familiar.Position, 0);
        uint token = Token(client);
        Assert.True(session.IsAreaLoading); Assert.Null(familiar.Area);
        possession.Unpossess(familiar); // damage to the master or a disconnect does the same
        Assert.Same(master, session.Creature);
        Assert.False(session.IsAreaLoading);
        Assert.Same(area, familiar.Area);
        Ready(client, token); f.Server.Tick(.01f);
        Assert.Single(area.Objects, o => o == familiar);
        Assert.Same(area, master.Area);
    }

    [DataFact]
    public void ArrivingAgainInTheAreaBeingLoadedKeepsOneLoad()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Returning");
        var world = f.Server.World; var pc = session.Creature!; var area = pc.Area!;
        session.AreaReadyHandshake = true;
        f.Server.EnterArea(session, area, pc.Position, pc.Facing);
        uint token = Token(client);
        var moved = pc.Position + new Vector3(2, 0, 0);
        world.AddToArea(pc, area, moved, 0, exact: true); // a DM avatar returning to its area does this
        Assert.DoesNotContain(Drain(client), p => (MessageType)p[0] == MessageType.LoadArea);
        Ready(client, token); f.Server.Tick(.01f);
        Assert.Same(area, pc.Area);
        Assert.Equal(moved, pc.Position);
    }

    [DataFact]
    public void LockedDoorFailsBeforeReachButUnlockedDoorUsesSuccessfulOpenTiming()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Door"); var w = f.Server.World; var pc = session.Creature!;
        var door = new Door { Locked = true };
        w.AddToArea(door, pc.Area!, pc.Position, 0); Drain(client);
        var failed = new OpenDoorAction { Target = door.Id };
        Assert.Equal(ActionStatus.Failed, failed.Step(w, pc, .01f));
        Assert.DoesNotContain(Drain(client), p => (MessageType)p[0] == MessageType.Notify && NotifyMessage.Read(new MessageReader(p)).Kind == "object-use");
        door.Locked = false;
        Assert.Equal(ActionStatus.InProgress, new OpenDoorAction { Target = door.Id }.Step(w, pc, .01f));
        Assert.False(door.IsOpen);
    }

    [DataFact]
    public void DazeForcesWalkingAndRejectsAbilitiesBeforeSpendingUses()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, _) = f.Join("Dazed"); var w = f.Server.World; var pc = session.Creature!;
        pc.Effects.Add(new Effect { Type = 28, DurationType = 2 });
        pc.WalkRequested = false;
        Assert.Equal(pc.WalkRate, Movement.MaxSpeed(w, pc, out bool walking));
        Assert.True(walking);
        Assert.False(OpenWinter.Game.Combat.CombatSystem.CanAttack(pc));
        Assert.False(CastSpellAction.CanAct(w, pc));
        pc.Feats.Add(10); pc.FeatUses[10] = 3;
        Assert.Equal(ActionStatus.Failed, new UseFeatAction { Feat = 10 }.Step(w, pc, 0));
        Assert.Equal(3, pc.FeatUses[10]);
    }

    [DataFact]
    public void ScriptStyleGrantsAndDestructionReportOnceWhileRearrangingDoesNot()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Items"); var w = f.Server.World; var pc = session.Creature!;
        var item = w.Inventory.CreateOn("nw_it_mpotion001", pc, 1, "reward")!;
        var messages = Drain(client);
        Assert.Single(messages, p => (MessageType)p[0] == MessageType.Notify && NotifyMessage.Read(new MessageReader(p)).Kind == "item-acquired");
        string acquired = w.Resources.TlkString(10468).Replace("<CUSTOM0>", item.Name.Resolve(w.Resources.Talk));
        Assert.Single(messages, p => (MessageType)p[0] == MessageType.Chat && ChatMessage.Read(new MessageReader(p)).Text == acquired);
        w.Inventory.MoveTo(item, pc);
        Assert.DoesNotContain(Drain(client), p => (MessageType)p[0] == MessageType.Notify && NotifyMessage.Read(new MessageReader(p)).Kind == "item-acquired");
        string lost = w.Resources.TlkString(10469).Replace("<CUSTOM0>", item.Name.Resolve(w.Resources.Talk));
        w.Destroy(item);
        Assert.Single(Drain(client), p => (MessageType)p[0] == MessageType.Chat && ChatMessage.Read(new MessageReader(p)).Text == lost);
    }
    [DataFact]
    public void LootableCorpsesStayAndExposeLootOnlyAfterFalling()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Loot"); var w = f.Server.World; var pc = session.Creature!;
        var corpse = w.Creatures.FromBlueprint("nw_bandit001")!;
        corpse.Scripts.Clear(); corpse.Lootable = true; corpse.IsDestroyable = false;
        w.AddToArea(corpse, pc.Area!, pc.Position + new Vector3(2, 0, 0), 0);
        foreach (var old in w.Inventory.AllItems(corpse).ToArray()) w.Destroy(old);
        var item = w.Inventory.CreateOn("nw_it_mpotion001", corpse, 1, "loot")!; item.Droppable = true;
        HitPoints.Kill(w, corpse, pc.Id);
        var system = w.System<CorpseSystem>();
        Assert.Null(system.LootFor(corpse));
        for (int i = 0; i < 24; i++) f.Server.Tick(.25f);
        Assert.False(corpse.IsDestroyed);
        var bag = Assert.IsType<Placeable>(system.LootFor(corpse));
        Assert.Equal(157, bag.Appearance);
        Assert.Contains(item, w.Inventory.Items(bag));
        Assert.DoesNotContain(item, corpse.Inventory);
        // The body exposes the invisible inventory; it is no longer selectable once emptied.
        Assert.Contains(Drain(client), raw => (MessageType)raw[0] == MessageType.Update
            && UpdateMessage.Read(new MessageReader(raw)) is var update && update.ObjectId == corpse.Id
            && (update.Flags & (ObjectFlags.Dead | ObjectFlags.Useable | ObjectFlags.Selectable)) == (ObjectFlags.Dead | ObjectFlags.Useable | ObjectFlags.Selectable));
        w.Inventory.MoveTo(item, pc); system.Closed(bag);
        Assert.Null(system.LootFor(corpse)); Assert.True(bag.IsDestroyed);
        for (int i = 0; i < 24; i++) f.Server.Tick(.25f);
        Assert.False(corpse.IsDestroyed);
        Assert.Empty(w.ByTag(CorpseSystem.BodyBagTag));
    }

}
