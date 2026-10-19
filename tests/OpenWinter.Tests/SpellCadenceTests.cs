using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Spells;
using Xunit;

namespace OpenWinter.Tests;

public class SpellCadenceTests
{
    static (World world, Creature caster) Setup()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        var area = new Area { ResRef = "cadence", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        var caster = new Creature { MaxHitPoints = 100, CurrentHitPoints = 100 };
        world.AddToArea(caster, area, new Vector3(10, 10, 0), 0);
        CreatureRules.EnsureInit(world, caster);
        return (world, caster);
    }

    static void Tick(World world, float seconds)
    {
        for (int i = 0; i < (int)MathF.Round(seconds * 100); i++) world.Step(.01f);
    }

    static CastSpellAction Cast(Creature c, bool instant = false) =>
        new() { Spell = 102, Cheat = true, Target = c.Id, Instant = instant };

    [DataFact]
    public void RecoveryDoesNotHoldWaitsAndInstantDoesNotResetNextCastBoundary()
    {
        var (world, c) = Setup();
        var first = Cast(c);
        var wait = new WaitAction { Remaining = .5f };
        var instant = Cast(c, true);
        var second = Cast(c);
        c.Actions.AddRange([first, wait, instant, second]);
        Tick(world, 3.2f);
        Assert.Same(wait, c.Actions[0]);
        Tick(world, .5f);
        Assert.Same(second, c.Actions[0]);
        Assert.Null(second.Info);
        Assert.Same(instant.Info, c.LastSpell);
        Tick(world, 2.2f);
        Assert.Null(second.Info);
        Tick(world, .2f);
        Assert.NotNull(second.Info);
        Assert.Same(instant.Info, c.LastSpell);
        Tick(world, 2f);
        Assert.Same(second.Info, c.LastSpell);
    }

    [DataFact]
    public void EarlyClearRetainsEligibilityWithoutSpendingOrReleasingFirstCast()
    {
        var (world, c) = Setup();
        c.Actions.Add(Cast(c));
        Tick(world, .75f);
        ActionQueues.Clear(world, c);
        var replacement = Cast(c);
        c.Actions.Add(replacement);
        Tick(world, 5.1f);
        Assert.Null(replacement.Info);
        Assert.Null(c.LastSpell);
        Tick(world, .3f);
        Assert.NotNull(replacement.Info);
        Tick(world, 2f);
        Assert.Same(replacement.Info, c.LastSpell);
    }

    [DataFact]
    public void LiveHasteMovesReleaseAndRecoveryButNotCapturedRoundDeadline()
    {
        var (world, c) = Setup();
        var cast = Cast(c);
        c.Actions.Add(cast);
        Tick(world, 1.7f);
        Assert.Null(c.LastSpell);
        var haste = new Effect { Type = NwRules.EffectHaste, DurationType = 2 };
        c.Effects.Add(haste);
        Tick(world, .02f);
        Assert.Same(cast.Info, c.LastSpell); // New 1.25-second release boundary already passed.
        Assert.InRange(world.System<CombatSystem>().State(c).SpellRoundEnd, 6, 6.02);
        c.Effects.Remove(haste);
        Tick(world, 1f);
        Assert.Contains(cast, c.Actions); // Removing Haste restores the three-second end.
        Tick(world, .4f);
        Assert.DoesNotContain(cast, c.Actions);
    }

    sealed class PresentationOutput(World world) : IWorldOutput
    {
        public readonly List<(string Kind, double At, System.Text.Json.JsonElement Data)> Events = new();
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) =>
            Events.Add((kind, world.Time, System.Text.Json.JsonSerializer.SerializeToElement(data)));
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
    }

    [DataFact]
    public void ReleasePresentationFollowsLiveHasteBoundaryExactlyOnce()
    {
        foreach (var sample in new[] { (false, 0f, 2f), (true, 0f, 1.25f),
            (false, .5f, 1.25f), (false, 1.7f, 1.7f), (true, .5f, 2f), (true, 1.6f, 1.25f) })
        {
            var (world, c) = Setup();
            var output = new PresentationOutput(world);
            world.Output = output;
            var haste = new Effect { Type = NwRules.EffectHaste, DurationType = 2 };
            if (sample.Item1) c.Effects.Add(haste);
            c.Actions.Add(Cast(c));
            if (sample.Item2 > 0)
            {
                Tick(world, sample.Item2);
                if (sample.Item1) c.Effects.Remove(haste); else c.Effects.Add(haste);
            }
            Tick(world, 3.5f - sample.Item2);
            var conjure = Assert.Single(output.Events, e => e.Kind == "spell-conjure");
            var cast = Assert.Single(output.Events, e => e.Kind == "spell-cast");
            var release = Assert.Single(output.Events, e => e.Kind == "spell-release");
            long token = conjure.Data.GetProperty("presentation").GetInt64();
            Assert.True(token > 0);
            Assert.Equal(token, cast.Data.GetProperty("presentation").GetInt64());
            Assert.Equal(token, release.Data.GetProperty("presentation").GetInt64());
            Assert.InRange(release.At - conjure.At, sample.Item3 - .03, sample.Item3 + .03);
            Assert.True(release.At >= cast.At);
            Assert.Equal(102, release.Data.GetProperty("spell").GetInt32());
            Tick(world, 3f);
            Assert.Single(output.Events, e => e.Kind == "spell-release");
        }
    }

    [DataFact]
    public void LiveConjureTimingUpdatesOnlyTheStillPreparingPresentation()
    {
        foreach (bool initial in new[] { false, true })
        {
            var (world, c) = Setup();
            var output = new PresentationOutput(world);
            world.Output = output;
            var haste = new Effect { Type = NwRules.EffectHaste, DurationType = 2 };
            if (initial) c.Effects.Add(haste);
            c.Actions.Add(Cast(c));
            Tick(world, .5f);
            if (initial) c.Effects.Remove(haste); else c.Effects.Add(haste);
            Tick(world, .02f);
            var timing = Assert.Single(output.Events, e => e.Kind == "spell-conjure-timing");
            var begin = Assert.Single(output.Events, e => e.Kind == "spell-conjure");
            Assert.Equal(begin.Data.GetProperty("presentation").GetInt64(), timing.Data.GetProperty("presentation").GetInt64());
            Assert.InRange(timing.Data.GetProperty("remaining").GetSingle(), initial ? .98f : .23f, initial ? 1.01f : .26f);
            Tick(world, 1.6f);
            if (initial) c.Effects.Add(haste); else c.Effects.Remove(haste);
            Tick(world, 2f);
            Assert.Single(output.Events, e => e.Kind == "spell-conjure-timing");
            Assert.Single(output.Events, e => e.Kind == "spell-conjure");
            Assert.Single(output.Events, e => e.Kind == "spell-cast");
            Assert.Single(output.Events, e => e.Kind == "spell-release");
        }
    }

    [DataFact]
    public void SelfConjureLifetimeHasInitialCapButCanExtendAgainBeforeExpiry()
    {
        foreach (bool initial in new[] { false, true })
        foreach (bool authored in new[] { false, true })
        {
            var (world, c) = Setup();
            if (authored)
            {
                var source = new OpenWinter.Game.Persistence.MemorySource("authored long self cast");
                source.Set(ResKey.Of("spells", ResType.TwoDA), System.Text.Encoding.UTF8.GetBytes(
                    "2DA V2.0\n\nLabel ConjTime CastTime CastAnim ConjAnim Range Innate\n102 LongSelf 4000 5000 SeLf hand P 1\n"));
                world.Resources.AddFirst(source);
            }
            var output = new PresentationOutput(world);
            world.Output = output;
            var haste = new Effect { Type = NwRules.EffectHaste, DurationType = 2 };
            if (initial) c.Effects.Add(haste);
            c.Actions.Add(Cast(c));
            Tick(world, .5f);
            var begin = Assert.Single(output.Events, e => e.Kind == "spell-conjure");
            float normal = authored ? 9.5f : 3f, fast = authored ? 7.5f : 2.25f;
            Assert.Equal(initial ? fast : normal, begin.Data.GetProperty("conjureLifetime").GetSingle());
            for (int flip = 0; flip < 3; flip++)
            {
                if (c.Effects.Contains(haste)) c.Effects.Remove(haste); else c.Effects.Add(haste);
                Tick(world, .02f);
                var update = output.Events.Last(e => e.Kind == "spell-conjure-lifetime");
                float expectedEnd = initial || c.Effects.Contains(haste) ? fast : normal;
                Assert.InRange(update.At - begin.At + update.Data.GetProperty("remaining").GetSingle(), expectedEnd - .025, expectedEnd + .025);
                Assert.Equal(begin.Data.GetProperty("presentation").GetInt64(), update.Data.GetProperty("presentation").GetInt64());
                Tick(world, .48f);
            }
            Assert.Equal(3, output.Events.Count(e => e.Kind == "spell-conjure-lifetime"));
            Assert.Single(output.Events, e => e.Kind == "spell-conjure");
        }
    }

    [DataFact]
    public void SelfConjureLifetimeCanEndImmediatelyDuringRecoveryWithoutReleasingTwice()
    {
        var (world, c) = Setup();
        var output = new PresentationOutput(world);
        world.Output = output;
        c.Actions.Add(Cast(c));
        Tick(world, 2.6f);
        c.Effects.Add(new Effect { Type = NwRules.EffectHaste, DurationType = 2 });
        Tick(world, .02f);
        var update = Assert.Single(output.Events, e => e.Kind == "spell-conjure-lifetime");
        Assert.Equal(0, update.Data.GetProperty("remaining").GetSingle());
        Assert.Single(output.Events, e => e.Kind == "spell-release");
        Assert.Empty(c.Actions);
    }

    [DataFact]
    public void SelfCastBodyDeadlineMovesBothWaysWithoutReplayingPresentation()
    {
        foreach (bool initial in new[] { false, true })
        {
            var (world, c) = Setup();
            var output = new PresentationOutput(world);
            world.Output = output;
            var haste = new Effect { Type = NwRules.EffectHaste, DurationType = 2 };
            if (initial) c.Effects.Add(haste);
            c.Actions.Add(Cast(c));
            Tick(world, initial ? 1.8f : 1.7f);
            if (initial) c.Effects.Remove(haste); else c.Effects.Add(haste);
            Tick(world, .02f);
            var timing = Assert.Single(output.Events, e => e.Kind == "spell-cast-timing");
            var begin = Assert.Single(output.Events, e => e.Kind == "spell-conjure");
            var cast = Assert.Single(output.Events, e => e.Kind == "spell-cast");
            Assert.Equal(cast.Data.GetProperty("presentation").GetInt64(), timing.Data.GetProperty("presentation").GetInt64());
            double end = timing.At - begin.At + timing.Data.GetProperty("remaining").GetSingle();
            Assert.InRange(end, initial ? 2.98 : 2.23, initial ? 3.02 : 2.27);
            Tick(world, 3f);
            Assert.Single(output.Events, e => e.Kind == "spell-cast");
            Assert.Single(output.Events, e => e.Kind == "spell-release");
        }
    }

    [DataFact]
    public void RepeatedRecoveryFlipsRetainBodyExtensionBeyondInitialHandCap()
    {
        var (world, c) = Setup();
        var output = new PresentationOutput(world);
        world.Output = output;
        var haste = new Effect { Type = NwRules.EffectHaste, DurationType = 2 };
        c.Effects.Add(haste);
        c.Actions.Add(Cast(c));
        Tick(world, 1.5f);
        var begin = Assert.Single(output.Events, e => e.Kind == "spell-conjure");
        for (int flip = 0; flip < 3; flip++)
        {
            if (c.Effects.Contains(haste)) c.Effects.Remove(haste); else c.Effects.Add(haste);
            Tick(world, .02f);
            var body = output.Events.Last(e => e.Kind == "spell-cast-timing");
            var hands = output.Events.Last(e => e.Kind == "spell-conjure-lifetime");
            double bodyEnd = body.At - begin.At + body.Data.GetProperty("remaining").GetSingle();
            double handEnd = hands.At - begin.At + hands.Data.GetProperty("remaining").GetSingle();
            Assert.InRange(bodyEnd, c.Effects.Contains(haste) ? 2.23 : 2.98, c.Effects.Contains(haste) ? 2.27 : 3.02);
            Assert.InRange(handEnd, 2.23, 2.27);
            Tick(world, .18f);
        }
        Assert.Equal(3, output.Events.Count(e => e.Kind == "spell-cast-timing"));
        Assert.Single(output.Events, e => e.Kind == "spell-cast");
        Assert.Single(output.Events, e => e.Kind == "spell-release");
    }

    [DataFact]
    public void LateHasteEndsSelfCastBodyImmediatelyWithoutAnotherRelease()
    {
        var (world, c) = Setup();
        var output = new PresentationOutput(world);
        world.Output = output;
        c.Actions.Add(Cast(c));
        Tick(world, 2.6f);
        c.Effects.Add(new Effect { Type = NwRules.EffectHaste, DurationType = 2 });
        Tick(world, .02f);
        var timing = Assert.Single(output.Events, e => e.Kind == "spell-cast-timing");
        Assert.Equal(0, timing.Data.GetProperty("remaining").GetSingle());
        Assert.Single(output.Events, e => e.Kind == "spell-release");
        Assert.Empty(c.Actions);
    }

    [DataFact]
    public void HandLifetimeExtensionDoesNotChangeUnmeasuredCastStylesOrFeatPaths()
    {
        foreach (var (style, feat) in new[] { ("touch", -1), ("out", -1), ("self", 303) })
        {
            var (world, c) = Setup();
            var source = new OpenWinter.Game.Persistence.MemorySource("cast style boundary");
            source.Set(ResKey.Of("spells", ResType.TwoDA), System.Text.Encoding.UTF8.GetBytes(
                $"2DA V2.0\n\nLabel ConjTime CastTime CastAnim ConjAnim Range Innate\n102 Boundary 1500 1000 {style} hand P 1\n"));
            world.Resources.AddFirst(source);
            var output = new PresentationOutput(world);
            world.Output = output;
            var cast = Cast(c); cast.FeatId = feat;
            c.Actions.Add(cast);
            Tick(world, .5f);
            c.Effects.Add(new Effect { Type = NwRules.EffectHaste, DurationType = 2 });
            Tick(world, 3f);
            var begin = Assert.Single(output.Events, e => e.Kind == "spell-conjure");
            Assert.Equal(System.Text.Json.JsonValueKind.Null, begin.Data.GetProperty("conjureLifetime").ValueKind);
            Assert.DoesNotContain(output.Events, e => e.Kind == "spell-conjure-lifetime");
            Assert.DoesNotContain(output.Events, e => e.Kind == "spell-cast-timing");
        }
    }

    [DataFact]
    public void ReleasePresentationRespectsEarlyAndCommittedQueueClearing()
    {
        foreach (bool committed in new[] { false, true })
        {
            var (world, c) = Setup();
            var output = new PresentationOutput(world);
            world.Output = output;
            c.Actions.Add(Cast(c));
            Tick(world, committed ? 1.7f : 1f);
            ActionQueues.Clear(world, c);
            c.Effects.Add(new Effect { Type = NwRules.EffectHaste, DurationType = 2 });
            Tick(world, 3f);
            if (committed)
            {
                var begin = Assert.Single(output.Events, e => e.Kind == "spell-conjure");
                var release = Assert.Single(output.Events, e => e.Kind == "spell-release");
                Assert.InRange(release.At - begin.At, 1.68, 1.73);
                Assert.NotNull(c.LastSpell);
            }
            else
            {
                Assert.DoesNotContain(output.Events, e => e.Kind == "spell-release");
                Assert.Null(c.LastSpell);
            }
        }
    }

    [DataFact]
    public void PreparedQuickenRetainsReleaseRecoveryAndThreeSecondEligibility()
    {
        var (world, c) = Setup();
        c.Abilities[NwRules.AbilityInt] = 20;
        c.Feats.Add(29);
        var cc = new CreatureClass { Class = NwRules.ClassWizard, Level = 20 };
        cc.Memorized[5].Add((102, NwRules.MetaQuicken, true));
        c.Classes.Add(cc);
        var cast = new CastSpellAction { Spell = 102, Class = NwRules.ClassWizard,
            MetaMagic = NwRules.MetaQuicken, Target = c.Id };
        c.Actions.Add(cast);
        Tick(world, .4f);
        Assert.True(cc.Memorized[5][0].Ready);
        Assert.Null(c.LastSpell);
        Tick(world, .2f);
        Assert.False(cc.Memorized[5][0].Ready);
        Assert.Null(c.LastSpell);
        Tick(world, .5f);
        Assert.Same(cast.Info, c.LastSpell);
        Assert.Contains(cast, c.Actions);
        Assert.InRange(world.System<CombatSystem>().State(c).SpellRoundEnd, 3, 3.02);
        Tick(world, 1f);
        Assert.DoesNotContain(cast, c.Actions);
    }

    [DataFact]
    public void SpellHandoffUsesExistingPlayerAndNpcAttackRoundLengths()
    {
        foreach (bool pc in new[] { false, true })
        foreach (int attacks in new[] { 1, 2, 6 })
        {
            var (world, c) = Setup();
            if (pc) c.Player = new PlayerSession { Connection = OpenWinter.Net.LoopbackConnection.CreatePair().Item1 };
            var combat = world.System<CombatSystem>();
            var state = combat.State(c);
            // The attack scheduler owns these measured values; spell waiting must not replace them with six.
            state.RoundStart = 0;
            state.RoundLength = CombatSystem.RoundLength(attacks) + (pc ? .5f : 0);
            var cast = Cast(c);
            Assert.Equal(ActionStatus.InProgress, cast.Step(world, c, 0));
            Assert.Null(cast.Info);
            Tick(world, state.RoundLength - .1f);
            Assert.Equal(ActionStatus.InProgress, cast.Step(world, c, 0));
            Assert.Null(cast.Info);
            Tick(world, .2f);
            Assert.Equal(ActionStatus.InProgress, cast.Step(world, c, 0));
            Assert.NotNull(cast.Info);
        }
    }
    [DataFact]
    public void SaveAfterRecoveryPreservesWaitingSpellEligibility()
    {
        var (world, c) = Setup();
        c.Actions.Add(Cast(c));
        var second = Cast(c);
        c.Actions.Add(second);
        Tick(world, 3.4f);
        Assert.Null(second.Info);
        var snapshot = OpenWinter.Game.SaveGames.WorldSnapshot.Capture(world, out var warnings);
        Assert.Empty(warnings);
        var (restored, unused) = Setup();
        var result = OpenWinter.Game.SaveGames.WorldSnapshot.Apply(restored, snapshot);
        Assert.Empty(result.Warnings);
        var copy = restored.Get<Creature>(c.Id)!;
        var waiting = Assert.IsType<CastSpellAction>(Assert.Single(copy.Actions));
        Assert.Equal(world.System<CombatSystem>().State(c).SpellRoundEnd,
            restored.System<CombatSystem>().State(copy).SpellRoundEnd);
        Tick(restored, 2.4f);
        Assert.Null(waiting.Info);
        Tick(restored, .3f);
        Assert.NotNull(waiting.Info);
        Tick(restored, 2f);
        Assert.Same(waiting.Info, copy.LastSpell);
    }

}
