using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class CreatureLabelTests
{
    [DataFact]
    public void InjuryCategoriesIncludeTemporaryHitPointsAndDeath()
    {
        var world = new World(TestData.Module);
        var c = new Creature { RulesInitialised = true, MaxHitPoints = 100 };
        foreach (var (hp, category) in new[] { (100, 0), (99, 0), (95, 0), (94, 1), (76, 1), (75, 1), (74, 2), (51, 2), (50, 2), (49, 3), (26, 3), (25, 3), (24, 4), (1, 4), (0, 4) })
        {
            c.CurrentHitPoints = hp;
            Assert.Equal(category, CreatureLabelFeed.DamageLevel(world, c));
        }
        var temp = new Effect { Type = NwRules.EffectTemporaryHitpoints };
        temp.Ints[0] = 100;
        c.Effects.Add(temp);
        Assert.Equal(0, CreatureLabelFeed.DamageLevel(world, c));
        c.IsDead = true;
        Assert.Equal(5, CreatureLabelFeed.DamageLevel(world, c));
    }

    [DataFact]
    public void LabelsAreReliableDeltasScopedToKnownObjectsAndRefreshAfterAreaChanges()
    {
        var world = new World(TestData.Module);
        var area = new Area(); world.Register(area);
        var otherArea = new Area(); world.Register(otherArea);
        var pc = new Creature { RulesInitialised = true, Name = new LocString("Hero") };
        var npc = new Creature { RulesInitialised = true, MaxHitPoints = 100, CurrentHitPoints = 75, Name = new LocString("Guard") };
        world.Register(pc); world.Register(npc);
        world.AddToArea(pc, area, default, 0, exact: true, enterEvent: false);
        world.AddToArea(npc, area, default, 0, exact: true, enterEvent: false);
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        var server = new GameServer(world);
        pc.Player = server.Accept(serverEnd);
        pc.Player.Creature = pc; pc.Player.Area = area; pc.Player.PlayerName = "Account";
        pc.Player.Known.Add(pc.Id);
        var feed = world.System<CreatureLabelFeed>();
        JsonElement Next()
        {
            feed.Step(0.3f);
            Assert.True(client.TryReceive(out var bytes));
            var note = NotifyMessage.Read(new MessageReader(bytes));
            Assert.Equal("creature-labels", note.Kind);
            return JsonDocument.Parse(note.Json).RootElement;
        }
        var initial = Next();
        Assert.True(initial.GetProperty("reset").GetBoolean());
        Assert.False(initial.TryGetProperty(npc.Id.ToString(), out _));
        Assert.Equal("Account", initial.GetProperty(pc.Id.ToString()).GetProperty("playerName").GetString());

        pc.Player.Known.Add(npc.Id);
        var first = Next().GetProperty(npc.Id.ToString());
        Assert.Equal("Guard", first.GetProperty("name").GetString());
        Assert.Equal(1, first.GetProperty("damageLevel").GetInt32());
        Assert.Equal(new[] { "damageLevel", "name", "playerName" }, first.EnumerateObject().Select(p => p.Name).Order().ToArray());
        feed.Step(0.3f); Assert.False(client.TryReceive(out _));
        npc.CurrentHitPoints = 100; npc.Name = new LocString("Healed Guard");
        var healed = Next().GetProperty(npc.Id.ToString());
        Assert.Equal(0, healed.GetProperty("damageLevel").GetInt32());
        Assert.Equal("Healed Guard", healed.GetProperty("name").GetString());

        pc.Player.Known.Remove(npc.Id);
        Assert.Equal(JsonValueKind.Null, Next().GetProperty(npc.Id.ToString()).ValueKind);
        pc.Player.Known.Add(npc.Id);
        Assert.True(Next().TryGetProperty(npc.Id.ToString(), out _));
        world.AddToArea(pc, otherArea, default, 0, exact: true, enterEvent: false);
        Assert.True(Next().GetProperty("reset").GetBoolean());
        world.AddToArea(pc, area, default, 0, exact: true, enterEvent: false);
        var back = Next();
        Assert.True(back.GetProperty("reset").GetBoolean());
        Assert.True(back.TryGetProperty(npc.Id.ToString(), out _));
    }
}
