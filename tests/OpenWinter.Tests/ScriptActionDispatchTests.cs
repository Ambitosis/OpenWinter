using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Skills;
using OpenWinter.Game.Rules;
using Xunit;

namespace OpenWinter.Tests;

public class ScriptActionDispatchTests
{
    static (World World, Creature Actor, Area Area) Setup()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame();
        var world = new World(resources);
        world.EnableScripts(); world.Register(world.Module); world.Factions.Load(resources);
        var area = new Area { ResRef = "dispatch", Width = 4, Height = 4 };
        world.Register(area); world.Areas.Add(area);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
        var actor = new Creature { Race = 6, CurrentHitPoints = 20, MaxHitPoints = 20 };
        world.AddToArea(actor, area, new(10, 10, 0), 0);
        return (world, actor, area);
    }

    static object? Call(World world, GameObject self, string name, params object?[] args)
    {
        var host = world.Scripts!;
        var program = new NcsProgram("dispatch", System.Text.Encoding.ASCII.GetBytes("NCS V1.0B\0\0\0\0")
            .Concat(new byte[] { 0x20, 0 }).ToArray());
        return ((IScriptEngine)host).Call(new ScriptThread(host.VM, program, self.Id), host.Definitions.ByName[name], args);
    }

    [DataFact]
    public void BashReplacesWaitingAndStartsTheExistingAttackAction()
    {
        foreach (bool door in new[] { true, false })
        {
            var (world, actor, area) = Setup();
            GameObject target = door ? new Door { CurrentHitPoints = 100, HitPoints = 100 }
                : new Placeable { CurrentHitPoints = 100, HitPoints = 100 };
            world.AddToArea(target, area, new Vector3(11, 10, 0), 0);
            actor.Actions.Add(new WaitAction { Remaining = 20 });
            Call(world, actor, door ? "DoDoorAction" : "DoPlaceableObjectAction", target.Id, 2);
            var attack = Assert.IsType<AttackAction>(Assert.Single(actor.Actions));
            Assert.Equal(target.Id, attack.Target);
            world.Step(.1f);
            Assert.Equal(target.Id, world.System<CombatSystem>().TargetOf(actor));
            ActionQueues.Clear(world, actor);
            Assert.False(world.System<CombatSystem>().IsInCombat(actor));
        }
    }

    [DataFact]
    public void BashAcceptsOpenStaticAndPlotTargetsButHonorsCommandability()
    {
        var (world, actor, area) = Setup();
        var door = new Door { IsOpen = true };
        var placeable = new Placeable { Static = true };
        world.AddToArea(door, area, new Vector3(11, 10, 0), 0);
        world.AddToArea(placeable, area, new Vector3(12, 10, 0), 0);
        foreach (GameObject target in new GameObject[] { door, placeable })
        {
            string action = target is Door ? "DoDoorAction" : "DoPlaceableObjectAction";
            string possible = target is Door ? "GetIsDoorActionPossible" : "GetIsPlaceableObjectActionPossible";
            Assert.Equal(1, Call(world, actor, possible, target.Id, 2));
            if (target is Door d) d.Plot = true; else ((Placeable)target).Plot = true;
            Assert.Equal(0, Call(world, actor, possible, target.Id, 2));
            actor.Actions.Clear();
            Call(world, actor, action, target.Id, 2);
            Assert.IsType<AttackAction>(Assert.Single(actor.Actions));
            actor.Actions.Clear(); actor.Commandable = false;
            var wait = new WaitAction { Remaining = 20 }; actor.Actions.Add(wait);
            Call(world, actor, action, target.Id, 2);
            Assert.Same(wait, Assert.Single(actor.Actions));
            actor.Commandable = true;
        }
        actor.Actions.Clear();
        Call(world, actor, "DoDoorAction", placeable.Id, 2);
        Call(world, actor, "DoPlaceableObjectAction", door.Id, 2);
        Assert.Empty(actor.Actions);
    }

    [DataFact]
    public void ObjectSkillTalentsApplyModesImmediatelyAndQueuePickpocket()
    {
        foreach (int skill in new[] { SkillIds.Hide, SkillIds.MoveSilently, SkillIds.Search, SkillIds.PickPocket })
        {
            var (world, actor, _) = Setup();
            var wait = new WaitAction { Remaining = 1 }; actor.Actions.Add(wait);
            var talent = new Talent { Type = NwRules.TalentSkill, Id = skill };
            Call(world, actor, "ActionUseTalentOnObject", talent, actor.Id);
            Assert.Same(wait, actor.Actions[0]);
            if (skill == SkillIds.PickPocket)
            {
                var action = Assert.IsType<UseSkillAction>(actor.Actions[1]);
                Assert.Equal(skill, action.Skill); Assert.Equal(actor.Id, action.Target);
                continue; // Theft outcomes are covered by the existing SkillTests and native fixture.
            }
            Assert.Same(wait, Assert.Single(actor.Actions));
            Assert.Equal(skill != SkillIds.Search, actor.StealthMode);
            Assert.Equal(skill == SkillIds.Search, actor.DetectMode);
            Call(world, actor, "ActionUseTalentOnObject", talent, actor.Id);
            Assert.False(actor.StealthMode); Assert.False(actor.DetectMode);
            Call(world, actor, "ActionUseSkill", skill, actor.Id, 0, GameObject.InvalidId);
            Assert.Equal(skill != SkillIds.Search, actor.StealthMode);
            Assert.Equal(skill == SkillIds.Search, actor.DetectMode);
            Assert.Same(wait, Assert.Single(actor.Actions));
        }
    }

    [DataFact]
    public void SkillTalentsHonorCommandabilityTargetsAndMeasuredNoOpRoutes()
    {
        var (world, actor, area) = Setup();
        foreach (int skill in new[] { SkillIds.Hide, SkillIds.MoveSilently, SkillIds.Search, SkillIds.PickPocket })
        {
            var talent = new Talent { Type = NwRules.TalentSkill, Id = skill };
            Call(world, actor, "ActionUseTalentAtLocation", talent, actor.Location);
            Call(world, actor, "ActionUseTalentOnObject", talent, GameObject.InvalidId);
            actor.Commandable = false;
            Call(world, actor, "ActionUseTalentOnObject", talent, actor.Id);
            actor.Commandable = true;
            Assert.Empty(actor.Actions);
        }
        var chest = new Placeable { Locked = true };
        world.AddToArea(chest, area, actor.Position, 0);
        foreach (int skill in new[] { SkillIds.OpenLock, SkillIds.Heal, SkillIds.Spot, SkillIds.Listen, 999 })
            Call(world, actor, "ActionUseTalentOnObject", new Talent { Type = NwRules.TalentSkill, Id = skill }, skill == SkillIds.OpenLock ? chest.Id : actor.Id);
        Assert.Empty(actor.Actions);
        Assert.True(chest.Locked); Assert.False(actor.DetectMode);
    }

    [DataFact]
    public void ScriptSpotListenAndHealingWithoutAnExplicitKitDoNotAct()
    {
        var (world, actor, area) = Setup();
        var patient = new Creature { Race = 6, CurrentHitPoints = 1, MaxHitPoints = 20 };
        world.AddToArea(patient, area, actor.Position, 0);
        foreach (int skill in new[] { SkillIds.Spot, SkillIds.Listen, SkillIds.Heal })
            Call(world, actor, "ActionUseSkill", skill, patient.Id, 0, GameObject.InvalidId);
        Assert.Empty(actor.Actions); Assert.False(actor.DetectMode);
        Assert.Equal(1, patient.CurrentHitPoints);
    }
}
