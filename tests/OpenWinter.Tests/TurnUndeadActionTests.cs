using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Functions;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Native turn_queue: round spacing, resource commitment and queue clearing.</summary>
public class TurnUndeadActionTests
{
    sealed class Output(World world) : IWorldOutput
    {
        public readonly List<(string Kind, double At)> Events = new();
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) =>
            Events.Add((kind, world.Time));
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
    }

    static (World World, Creature Caster, Output Output) Setup(bool haste = false)
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        var area = new Area { ResRef = "turn_queue", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        var c = new Creature { Race = 6, MaxHitPoints = 100, CurrentHitPoints = 100 };
        c.Classes.Add(new CreatureClass { Class = NwRules.ClassCleric, Level = 15 });
        c.Abilities[NwRules.AbilityCha] = 16;
        c.Feats.Add(NwRules.FeatTurnUndead);
        world.AddToArea(c, area, new Vector3(10, 10, 0), 0);
        CreatureRules.EnsureInit(world, c);
        if (haste) c.Effects.Add(new Effect { Type = NwRules.EffectHaste, DurationType = 2 });
        var output = new Output(world);
        world.Output = output;
        Assert.Equal(6, CreatureRules.RemainingFeatUses(world, c, NwRules.FeatTurnUndead));
        return (world, c, output);
    }

    static UseFeatAction Turn(Creature c) => new() { Feat = NwRules.FeatTurnUndead, Target = c.Id };
    static int Uses(World world, Creature c) => CreatureRules.RemainingFeatUses(world, c, NwRules.FeatTurnUndead);
    static void Tick(World world, float seconds)
    {
        for (int i = 0; i < (int)MathF.Round(seconds * 100); i++) world.Step(.01f);
    }

    [DataFact]
    public void QueuedUsesWaitSixSecondsOrThreeWithHasteAndSpendAfterPreparation()
    {
        foreach (bool haste in new[] { false, true })
        {
            var (world, c, output) = Setup(haste);
            c.Actions.AddRange([Turn(c), Turn(c)]);
            Tick(world, haste ? .7f : 1.4f);
            Assert.Equal(6, Uses(world, c));
            Tick(world, .2f);
            Assert.Equal(5, Uses(world, c));
            Tick(world, 8f);
            Assert.Empty(c.Actions);
            Assert.Equal(4, Uses(world, c));
            var starts = output.Events.Where(e => e.Kind == "spell-conjure").ToList();
            var releases = output.Events.Where(e => e.Kind == "spell-release").ToList();
            Assert.Equal(2, starts.Count);
            Assert.Equal(2, releases.Count);
            Assert.InRange(starts[1].At - starts[0].At, haste ? 2.98 : 5.98, haste ? 3.03 : 6.03);
            Assert.InRange(releases[0].At - starts[0].At, haste ? 1.23 : 1.98, haste ? 1.28 : 2.03);
        }
    }

    [DataFact]
    public void CancellingPreparationDoesNotSpendOrRelease()
    {
        var (world, c, output) = Setup();
        c.Actions.AddRange([Turn(c), Turn(c)]);
        Tick(world, .5f);
        ActionQueues.Clear(world, c);
        Tick(world, 9f);
        Assert.Equal(6, Uses(world, c));
        Assert.Empty(c.Actions);
        Assert.Null(c.LastSpell);
        Assert.DoesNotContain(output.Events, e => e.Kind == "spell-release");
    }

    [DataFact]
    public void ClearingAfterCommitRetainsFirstImpactAndRecoveryButRemovesSuccessor()
    {
        var (world, c, output) = Setup();
        var first = Turn(c);
        c.Actions.AddRange([first, Turn(c)]);
        Tick(world, 1.7f);
        Assert.Equal(5, Uses(world, c));
        Assert.Null(c.LastSpell);
        ActionQueues.Clear(world, c);
        Assert.Same(first, Assert.Single(c.Actions));
        Tick(world, .4f);
        Assert.Equal(308, c.LastSpell?.SpellId);
        Assert.Same(first, Assert.Single(c.Actions));
        Tick(world, 7f);
        Assert.Empty(c.Actions);
        Assert.Equal(5, Uses(world, c));
        Assert.Single(output.Events, e => e.Kind == "spell-release");
    }

    [DataFact]
    public void QueuedUsesCannotOverdrawLastRemainingCharge()
    {
        var (world, c, output) = Setup();
        CreatureRules.AdjustFeatUses(world, c, NwRules.FeatTurnUndead, -5);
        c.Actions.AddRange([Turn(c), Turn(c)]);
        Tick(world, 9f);
        Assert.Equal(0, Uses(world, c));
        Assert.Empty(c.Actions);
        Assert.Single(output.Events, e => e.Kind == "spell-release");
    }
}
