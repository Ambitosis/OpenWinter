using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using OpenWinter.Game.Spells;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class RestEffectTimingTests
{
    sealed class Game
    {
        public readonly World World;
        public readonly Creature Actor;
        public readonly List<string> Log = [];
        public Game()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
            World.EnableScripts();
            World.Scripts!.Log = Log.Add;
            var area = new Area { ResRef = "rest_effects" };
            World.Register(area); World.Areas.Add(area);
            Actor = World.Creatures.FromTemplate(new GffStruct());
            Actor.Classes.Add(new CreatureClass { Class = 4, Level = 1 });
            Actor.Race = 6;
            for (int i = 0; i < Actor.Abilities.Length; i++) Actor.Abilities[i] = 10;
            World.AddToArea(Actor, area, new Vector3(20, 20, 0), 0, exact: true);
            CreatureRules.EnsureInit(World, Actor);
            Actor.BaseHitPoints = 100; Actor.HitPointOffset = 0;
            Actor.MaxHitPoints = 100; Actor.CurrentHitPoints = 50;
            Script("rest_observe", """
                void main()
                {
                    SetLocalInt(OBJECT_SELF,"calls",GetLocalInt(OBJECT_SELF,"calls")+1);
                    SetLocalInt(OBJECT_SELF,"resting",GetIsResting(OBJECT_SELF));
                    SetLocalInt(OBJECT_SELF,"action",GetCurrentAction(OBJECT_SELF));
                    SetLocalInt(OBJECT_SELF,"hp",GetCurrentHitPoints(OBJECT_SELF));
                    int count=0;effect e=GetFirstEffect(OBJECT_SELF);
                    while(GetIsEffectValid(e)){count++;e=GetNextEffect(OBJECT_SELF);}
                    SetLocalInt(OBJECT_SELF,"effects",count);
                    ApplyEffectToObject(DURATION_TYPE_TEMPORARY,TagEffect(EffectSkillIncrease(SKILL_SPOT,1),"hook"),OBJECT_SELF,300.0);
                }
                """);
            Actor.Scripts["ScriptRested"] = "rest_observe";
        }

        public void Script(string name, string source)
        {
            var bytes = NssCompiler.Compile(source, name, World.Scripts!.Definitions, _ => null, out var error);
            Assert.True(bytes != null, error);
            World.Scripts.Register(name, new NcsProgram(name, bytes!));
        }

        public void Kit(int spell = -1, uint creator = GameObject.InvalidId)
        {
            var k = EffectConstants.Of(World);
            foreach (var (name, subtype) in new[] { ("m", k.Magical), ("s", k.Supernatural), ("e", k.Extraordinary), ("u", k.Unyielding) })
                foreach (bool temporary in new[] { true, false })
                    World.System<EffectSystem>().Apply(new Effect
                    {
                        Type = k["EFFECT_TYPE_SKILL_INCREASE"], SubType = subtype,
                        Tag = (temporary ? "t" : "p") + name, SpellId = spell, Creator = creator, Ints = { [0] = 17, [1] = 1 },
                    }, temporary ? k.Temporary : k.Permanent, Actor, 300);
            Assert.Equal(8, Actor.Effects.Count);
            var permanent = Assert.Single(Actor.Effects, e => e.Tag == "ps");
            Assert.Equal(k.Supernatural, permanent.SubType);
            Assert.Equal(k.Permanent, permanent.DurationType);
        }

        public void Survivors() => Assert.Equal(new[] { "ps", "pu", "tu" }, Actor.Effects.Select(e => e.Tag).OrderBy(t => t).ToArray());

        public void Player(bool veto = false)
        {
            Actor.Player = new PlayerSession { Connection = LoopbackConnection.CreatePair().Server, Creature = Actor };
            Script("rest_module", """
                void main()
                {
                    object pc=GetLastPCRested();int kind=GetLastRestEventType();
                    SetLocalString(pc,"events",GetLocalString(pc,"events")+IntToString(kind));
                    int count=0;effect e=GetFirstEffect(pc);
                    while(GetIsEffectValid(e)){count++;e=GetNextEffect(pc);}
                    SetLocalInt(pc,"effects"+IntToString(kind),count);
                    SetLocalInt(pc,"resting"+IntToString(kind),GetIsResting(pc));
                    SetLocalInt(pc,"completed"+IntToString(kind),GetLocalInt(pc,"completed"));
                    if(kind==REST_EVENTTYPE_REST_STARTED&&GetLocalInt(pc,"veto"))AssignCommand(pc,ClearAllActions(TRUE));
                }
                """);
            World.Module.Scripts["Mod_OnPlrRest"] = "rest_module";
            Actor.Locals.Ints["veto"] = veto ? 1 : 0;
        }

        public void QueueRest()
        {
            Script("queue_rest", "void main(){ActionRest();ActionDoCommand(SetLocalInt(OBJECT_SELF,\"completed\",1));}");
            World.Scripts!.Run("queue_rest", Actor.Id);
            Assert.False(World.Scripts.LastRunFailed);
        }
    }

    [DataFact]
    public void NpcRestCompletesImmediatelyAndRestedHookObservesEffectsBeforeRestBenefits()
    {
        var g = new Game();
        g.Kit();
        Assert.False(g.Actor.IsPC);
        Assert.True(RestAction.Duration(g.World, g.Actor) > 1);
        g.Actor.Actions.Add(new RestAction());
        double before = g.World.Time;
        ActionQueues.Step(g.World, g.Actor, 1f / 30);
        Assert.Equal(before, g.World.Time);
        Assert.Empty(g.Actor.Actions);
        Assert.False(g.Actor.IsResting);
        Assert.Equal(1, g.Actor.Locals.Ints["calls"]);
        Assert.Equal(1, g.Actor.Locals.Ints["resting"]);
        Assert.Equal(65535, g.Actor.Locals.Ints["action"]);
        Assert.Equal(50, g.Actor.Locals.Ints["hp"]);
        Assert.Equal(8, g.Actor.Locals.Ints["effects"]);
        Assert.Equal(g.Actor.MaxHitPoints, g.Actor.CurrentHitPoints);
        g.Survivors(); // Also proves the callback's temporary effect is removed.
        Assert.False(g.World.Scripts!.LastRunFailed);
    }

    [DataFact]
    public void ForceRestKeepsTheMeasuredSubtypeExceptionsWithoutFiringRestedHook()
    {
        var g = new Game();
        g.Kit();
        g.Script("force_rest", "void main(){ForceRest(OBJECT_SELF);}");
        g.World.Scripts!.Run("force_rest", g.Actor.Id);
        Assert.False(g.World.Scripts.LastRunFailed);
        Assert.False(g.Actor.IsResting);
        Assert.Empty(g.Actor.Actions);
        Assert.False(g.Actor.Locals.Ints.ContainsKey("calls"));
        Assert.Equal(g.Actor.MaxHitPoints, g.Actor.CurrentHitPoints);
        g.Survivors();
    }

    [DataFact]
    public void PlayerSpellTaggedScriptEffectsSurviveStartAndModuleFinishFollowsQueuedSuccessor()
    {
        foreach (int spell in new[] { -1, 450 })
        {
            var g = new Game(); g.Player(); g.Kit(spell); g.QueueRest();
            ActionQueues.Step(g.World, g.Actor, .1f);
            Assert.True(g.Actor.IsResting);
            Assert.Equal("1", g.Actor.Locals.Strings["events"]);
            Assert.Equal(8, g.Actor.Locals.Ints["effects1"]);
            Assert.Equal(1, g.Actor.Locals.Ints["resting1"]);
            ActionQueues.Step(g.World, g.Actor, 1f);
            Assert.Equal(8, g.Actor.Effects.Count);
            ActionQueues.Step(g.World, g.Actor, RestAction.Duration(g.World, g.Actor));
            Assert.False(g.Actor.IsResting);
            Assert.Empty(g.Actor.Actions);
            Assert.True(g.Actor.Locals.Ints.GetValueOrDefault("completed") == 1, string.Join("\n", g.Log));
            Assert.Equal("1", g.Actor.Locals.Strings["events"]);
            Assert.False(g.Actor.Locals.Ints.ContainsKey("calls")); // PC does not run creature OnRested.
            g.Survivors();
            g.World.Step(.01f);
            Assert.Equal("12", g.Actor.Locals.Strings["events"]);
            Assert.Equal(1, g.Actor.Locals.Ints["completed2"]);
            Assert.Equal(3, g.Actor.Locals.Ints["effects2"]);
            Assert.Equal(0, g.Actor.Locals.Ints["resting2"]);
        }
    }

    [DataFact]
    public void PlayerRestStartHookCanVetoWithoutRemovingEffectsOrRunningSuccessor()
    {
        var g = new Game(); g.Player(veto: true); g.Kit(450, g.Actor.Id); g.QueueRest();
        ActionQueues.Step(g.World, g.Actor, .1f);
        g.World.Step(.01f);
        Assert.False(g.Actor.IsResting);
        Assert.Empty(g.Actor.Actions);
        Assert.Equal("13", g.Actor.Locals.Strings["events"]);
        Assert.Equal(8, g.Actor.Locals.Ints["effects1"]);
        Assert.Equal(8, g.Actor.Locals.Ints["effects3"]);
        Assert.Equal(8, g.Actor.Effects.Count);
        Assert.Equal(0, g.Actor.Locals.Ints["completed3"]);
        Assert.False(g.Actor.Locals.Ints.ContainsKey("calls"));
    }

    [DataFact]
    public void NoRestAreaRejectionPreservesEffectsWithoutRestHooks()
    {
        var g = new Game(); g.Player(); g.Kit(450, g.Actor.Id);
        OpenWinter.Game.WorldObjects.AreaSettings.Of(g.World, g.Actor.Area!).NoRest = true;
        g.QueueRest();
        ActionQueues.Step(g.World, g.Actor, .1f);
        g.World.Step(.01f);
        Assert.False(g.Actor.IsResting);
        Assert.Equal(8, g.Actor.Effects.Count);
        Assert.Equal(1, g.Actor.Locals.Ints["completed"]);
        Assert.False(g.Actor.Locals.Strings.ContainsKey("events"));
        Assert.False(g.Actor.Locals.Ints.ContainsKey("calls"));
    }

    [DataFact]
    public void AcceptedPlayerRestRemovesOnlyItsMagicalEffectsFromAllRecipientsAfterTheStartHook()
    {
        var g = new Game(); g.Player();
        var other = g.World.Creatures.FromTemplate(new GffStruct());
        other.Faction = g.Actor.Faction;
        g.World.AddToArea(other, g.Actor.Area!, new Vector3(22, 20, 0), 0, exact: true);
        var k = EffectConstants.Of(g.World);
        foreach (var target in new[] { g.Actor, other })
            foreach (uint creator in new[] { g.Actor.Id, other.Id, g.World.Module.Id })
                foreach (int spell in new[] { -1, 450 })
                    foreach (int duration in new[] { k.Temporary, k.Permanent })
                        foreach (int subtype in new[] { k.Magical, k.Supernatural, k.Extraordinary, k.Unyielding })
                            g.World.System<EffectSystem>().Apply(new Effect
                            {
                                Type = k["EFFECT_TYPE_SKILL_INCREASE"], Creator = creator, SpellId = spell,
                                SubType = subtype, Ints = { [0] = 17, [1] = 1 },
                            }, duration, target, 300);
        Assert.Equal(48, g.Actor.Effects.Count);
        Assert.Equal(48, other.Effects.Count);
        g.QueueRest();
        ActionQueues.Step(g.World, g.Actor, .1f);
        Assert.True(g.Actor.IsResting);
        Assert.Equal(48, g.Actor.Locals.Ints["effects1"]);
        g.World.Step(.01f); // Flush post-hook commands, then accepted-start cleanup.
        foreach (var target in new[] { g.Actor, other })
        {
            Assert.Equal(44, target.Effects.Count);
            Assert.DoesNotContain(target.Effects, e => e.Creator == g.Actor.Id && e.SubType == k.Magical);
            Assert.Equal(12, target.Effects.Count(e => e.Creator == g.Actor.Id));
            Assert.Equal(16, target.Effects.Count(e => e.Creator == other.Id));
            Assert.Equal(16, target.Effects.Count(e => e.Creator == g.World.Module.Id));
        }
        // Cancellation after accepted start cannot restore the removed effects.
        ActionQueues.Clear(g.World, g.Actor);
        Assert.Equal(44, g.Actor.Effects.Count);
        Assert.Equal(44, other.Effects.Count);
    }

    [DataFact]
    public void RemovingOwnHitPointBuffAtStartDoesNotCancelRestButLaterDamageDoes()
    {
        foreach (bool constitution in new[] { false, true })
        {
            var g = new Game(); g.Player();
            var k = EffectConstants.Of(g.World);
            g.World.System<EffectSystem>().Apply(new Effect
            {
                Type = constitution ? k["EFFECT_TYPE_ABILITY_INCREASE"] : k.TemporaryHitpoints,
                Creator = g.Actor.Id, SubType = k.Magical,
                Ints = { [0] = constitution ? NwRules.AbilityCon : 10, [1] = 4 },
            }, k.Temporary, g.Actor, 300);
            Assert.True(constitution ? HitPoints.Current(g.Actor) > 50 : CreatureRules.TemporaryHitPoints(g.Actor) == 10);
            g.QueueRest();
            ActionQueues.Step(g.World, g.Actor, .1f);
            g.World.Step(.01f);
            Assert.Empty(g.Actor.Effects);
            Assert.Equal(50, HitPoints.Current(g.Actor));
            Assert.True(g.Actor.IsResting);
            g.Actor.CurrentHitPoints--;
            ActionQueues.Step(g.World, g.Actor, .1f);
            Assert.False(g.Actor.IsResting);
        }
    }
}
