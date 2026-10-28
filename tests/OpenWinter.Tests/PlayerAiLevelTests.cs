using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Society;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// AI levels and attacks with a player present (nwmain visual scenes playerai and playerkill).
/// </summary>
public class PlayerAiLevelTests
{
    sealed record Setup(World World, Area A, Area B, Creature Pc)
    {
        public void Step(float seconds) { for (float t = 0; t < seconds; t += 1f / 30) World.Step(1f / 30); }
    }

    static Area Flat(World world, string tag)
    {
        var area = new Area { ResRef = tag, Tag = tag, Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
        return area;
    }

    static Setup Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var world = new World(rm);
        world.Register(world.Module);
        world.Factions.Load(rm);
        var a = Flat(world, "pai_a");
        var b = Flat(world, "pai_b");
        var pc = new Creature { Tag = "pc", Faction = FactionTable.PlayerFaction, CurrentHitPoints = 200, MaxHitPoints = 200, PerceptionRange = 9 };
        pc.Player = new PlayerSession { Connection = LoopbackConnection.CreatePair().Server, PlayerName = "pc" };
        world.AddToArea(pc, a, new Vector3(20, 20, 0), 0);
        return new Setup(world, a, b, pc);
    }

    static Creature Npc(Setup s, Area area, Vector3 at, int faction, string tag)
    {
        var c = new Creature { Tag = tag, Faction = faction, CurrentHitPoints = 200, MaxHitPoints = 200, IsImmortal = true };
        s.World.AddToArea(c, area, at, MathF.PI);
        return c;
    }

    [DataFact]
    public void PlayersAndTheirAssociatesRunHighAndCreaturesDropAtTheirNextPass()
    {
        var s = Make();
        var w = s.World;
        var n1 = Npc(s, s.A, new Vector3(28, 24, 0), FactionTable.Merchant, "n1");
        var hench = Npc(s, s.A, new Vector3(18, 22, 0), FactionTable.Merchant, "hench");
        AssociateService.Add(w, s.Pc, hench, AssociateService.Henchman);
        var nb = Npc(s, s.B, new Vector3(20, 20, 0), FactionTable.Merchant, "nb");
        s.Step(1f);
        // nwmain playerai: she reads AI_LEVEL_VERY_HIGH, her henchman HIGH, an NPC near her LOW, one elsewhere VERY_LOW.
        Assert.Equal(4, w.EffectiveAILevel(s.Pc));
        Assert.Equal(3, w.EffectiveAILevel(hench));
        Assert.Equal(1, w.EffectiveAILevel(n1));
        Assert.Equal(0, w.EffectiveAILevel(nb));
        s.Step(5f); // every NPC has had a pass with her in the area
        w.AddToArea(s.Pc, s.B, new Vector3(22, 20, 0), 0);
        // Arriving raises the new area's creatures at once; the henchman left behind stays HIGH.
        Assert.Equal(1, w.EffectiveAILevel(nb));
        Assert.Equal(3, w.EffectiveAILevel(hench));
        // The creature she left stays LOW until its next perception pass (about 2 s for the NPC she left in playerai).
        Assert.Equal(1, w.EffectiveAILevel(n1));
        s.Step(PerceptionSystem.Interval + 0.2f);
        Assert.Equal(0, w.EffectiveAILevel(n1));
    }

    [DataFact]
    public void APlayersAttackEndsWhenItsTargetLeavesTheArea()
    {
        var s = Make();
        var w = s.World;
        var t1 = Npc(s, s.A, new Vector3(21.5f, 20, 0), 1, "t1");
        s.Step(0.5f);
        s.Pc.Actions.Add(new AttackAction { Target = t1.Id });
        s.Step(1.5f);
        Assert.Equal(t1.Id, w.System<CombatSystem>().State(s.Pc).Target);
        var at = s.Pc.Position;
        w.AddToArea(t1, s.B, new Vector3(30, 30, 0), 0);
        s.Step(0.1f);
        // Alone: the attack action is gone in the next update and she doesn't move (playerai atk1).
        Assert.Empty(s.Pc.Actions);
        s.Step(1f);
        Assert.Equal(at, s.Pc.Position);
    }

    [DataFact]
    public void APlayersAttackTurnsToAnEnemyInReachWhenItsTargetLeaves()
    {
        var s = Make();
        var w = s.World;
        var t3 = Npc(s, s.A, new Vector3(20, 21.8f, 0), 1, "t3");
        var t2 = Npc(s, s.A, new Vector3(21.4f, 20, 0), 1, "t2");
        s.Step(0.5f);
        s.Pc.Actions.Add(new AttackAction { Target = t3.Id });
        s.Step(1.5f);
        w.AddToArea(t3, s.B, new Vector3(30, 30, 0), 0);
        s.Step(0.1f);
        // With an enemy 1.4 m away the attack goes on against it (playerai atk2).
        var attack = Assert.IsType<AttackAction>(Assert.Single(s.Pc.Actions));
        Assert.Equal(t2.Id, attack.Target);
        s.Step(6.5f);
        Assert.Equal(t2.Id, w.System<CombatSystem>().State(s.Pc).Target);
    }

    [DataFact]
    public void APlayersAttackTurnsToTheNearestEnemyInReachWhenItsTargetDies()
    {
        var s = Make();
        var w = s.World;
        var t = Npc(s, s.A, new Vector3(20, 21.8f, 0), 1, "t");
        t.IsImmortal = false;
        var near = Npc(s, s.A, new Vector3(21.4f, 20, 0), 1, "near");
        Npc(s, s.A, new Vector3(14, 20, 0), 1, "far");
        s.Step(0.5f);
        s.Pc.Actions.Add(new AttackAction { Target = t.Id });
        s.Step(1.5f);
        OpenWinter.Game.Rules.HitPoints.Kill(w, t, s.Pc.Id);
        s.Step(0.1f);
        // The nearer enemy, not the one 6 m away, taken up at her next swing (playerkill both).
        var attack = Assert.IsType<AttackAction>(Assert.Single(s.Pc.Actions));
        Assert.Equal(near.Id, attack.Target);
        s.Step(6.5f);
        Assert.Equal(near.Id, w.System<CombatSystem>().State(s.Pc).Target);
    }

    [DataFact]
    public void AttacksEndWhenTheTargetDiesWithNoEnemyInReachOrForAnNpc()
    {
        var s = Make();
        var w = s.World;
        // Her attack, with only an enemy 6 m away (playerkill far).
        var t = Npc(s, s.A, new Vector3(20, 21.8f, 0), 1, "t");
        t.IsImmortal = false;
        Npc(s, s.A, new Vector3(14, 20, 0), 1, "far");
        // An NPC's attack, with an enemy of it 1.4 m away (playerkill npc).
        var a = Npc(s, s.A, new Vector3(30, 20, 0), 1, "a");
        var m = Npc(s, s.A, new Vector3(30, 21.8f, 0), FactionTable.Merchant, "m");
        m.IsImmortal = false;
        Npc(s, s.A, new Vector3(31.4f, 20, 0), FactionTable.Merchant, "n");
        s.Step(0.5f);
        s.Pc.Actions.Add(new AttackAction { Target = t.Id });
        a.Actions.Add(new AttackAction { Target = m.Id });
        s.Step(1.5f);
        OpenWinter.Game.Rules.HitPoints.Kill(w, t, s.Pc.Id);
        OpenWinter.Game.Rules.HitPoints.Kill(w, m, a.Id);
        s.Step(0.1f);
        Assert.DoesNotContain(s.Pc.Actions, x => x is AttackAction);
        Assert.DoesNotContain(a.Actions, x => x is AttackAction);
    }
}
