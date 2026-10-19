using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Disease after it lands, as measured on the original server (parity suite fx_disease), with loaded dice.</summary>
public class DiseaseTests
{
    const int FilthFever = 4; // Fortitude 12, incubation 1 hour, 1d3 Int and 1d3 Con

    sealed record Setup(World World, Area Area)
    {
        public EffectConstants K => EffectConstants.Of(World);
        public EffectSystem Fx => World.System<EffectSystem>();

        /// <summary>Saves: the next d20 rolls (1 fails, 20 saves); damage dice roll their maximum.</summary>
        public readonly Queue<int> D20 = new();

        public Creature Spawn()
        {
            var c = new Creature { Tag = "t", MaxHitPoints = 20, CurrentHitPoints = 20, Race = 6 };
            for (int i = 0; i < 6; i++) c.Abilities[i] = 14;
            c.Classes.Add(new CreatureClass { Class = NwRules.ClassFighter, Level = 1 });
            World.AddToArea(c, Area, new Vector3(5, 5, 0), 0);
            CreatureRules.EnsureInit(World, c);
            return c;
        }

        public Effect Disease(Creature c, int row)
        {
            var e = new Effect { Type = K["EFFECT_TYPE_DISEASE"], SubType = K.Supernatural, ScriptId = Fx.NextId() };
            e.Ints[0] = row;
            Fx.Apply(e, K.Permanent, c, 0);
            return c.Effects.FirstOrDefault(x => x.Type == e.Type)!;
        }

        public void Hours(double hours)
        {
            double seconds = hours * World.Clock.HourMs / 1000.0;
            for (double t = 0; t < seconds; t += 0.5) World.Step(0.5f);
        }

        public List<Effect> Damage(Creature c) => c.Effects.Where(x => x.Type == K["EFFECT_TYPE_ABILITY_DECREASE"]).ToList();
    }

    static Setup Make()
    {
        var rm = new ResMan(TestData.Paths);
        rm.AddBaseGame();
        var world = new World(rm);
        world.Register(world.Module);
        world.Clock.MinutesPerHour = 1;
        var area = new Area { Tag = "dz_area", ResRef = "dz_area" };
        world.Register(area);
        world.Areas.Add(area);
        var s = new Setup(world, area);
        s.Fx.Dice = n => n == 20 ? (s.D20.Count > 0 ? s.D20.Dequeue() : 1) - 1 : n - 1;
        return s;
    }

    [DataFact]
    public void ItIncubatesThenDamagesOnEveryFailedDailySave()
    {
        var s = Make();
        var c = s.Spawn();
        s.D20.Enqueue(1); // the save when it's applied
        var e = s.Disease(c, FilthFever);
        Assert.NotNull(e);
        Assert.Equal(new[] { FilthFever, 0, 0 }, e.Ints[..3]);
        long due = e.Ints[3] * 24 * s.World.Clock.HourMs + e.Ints[4];
        Assert.Equal(s.World.Clock.TotalMs + s.World.Clock.HourMs, due); // incubation: one game hour

        s.Hours(0.9);
        Assert.Empty(s.Damage(c));
        s.D20.Enqueue(1);
        s.Hours(0.2);
        // Failed at the end of incubation: 1d3 Intelligence and 1d3 Constitution, permanent and supernatural.
        var damage = s.Damage(c);
        Assert.Equal(new[] { (3, 3), (2, 3) }, damage.Select(d => (d.Ints[0], d.Ints[1])).OrderByDescending(x => x.Item1));
        Assert.All(damage, d => { Assert.Equal(s.K.Supernatural, d.SubType); Assert.Equal(s.K.Permanent, d.DurationType); Assert.Equal(2, d.Ints[2]); });
        Assert.Equal(1, e.Ints[2]);
        Assert.Equal(0, e.Ints[1]);

        s.Hours(23);
        Assert.Equal(2, s.Damage(c).Count);
        s.D20.Enqueue(1);
        s.Hours(1.5);
        Assert.Equal(4, s.Damage(c).Count);
    }

    [DataFact]
    public void TheSecondSuccessfulSaveCuresItEvenWithAFailureBetween()
    {
        var s = Make();
        var c = s.Spawn();
        s.D20.Enqueue(1);
        var e = s.Disease(c, FilthFever);
        s.D20.Enqueue(20);
        s.Hours(1.1);      // incubation ends: saved
        Assert.Equal(1, e.Ints[1]);
        Assert.Empty(s.Damage(c));
        s.D20.Enqueue(1);
        s.Hours(24);       // day 1: failed, but the save already made still counts
        Assert.Equal(1, e.Ints[1]);
        Assert.Equal(2, s.Damage(c).Count);
        s.D20.Enqueue(20);
        s.Hours(24);       // day 2: the second save cures it; the damage stays
        Assert.DoesNotContain(e, c.Effects);
        Assert.Equal(2, s.Damage(c).Count);
    }

    [DataFact]
    public void TheModuleClockDecidesWhenACheckIsDue()
    {
        var s = Make();
        var c = s.Spawn();
        s.D20.Enqueue(1);
        var e = s.Disease(c, FilthFever);
        // SetTime to a minute before the end of incubation (a minute is a game hour here; seconds run in real time).
        s.World.Clock.AddMilliseconds(s.World.Clock.HourMs - 1000);
        s.World.Step(0.1f);
        Assert.Equal(0, e.Ints[2]);
        s.D20.Enqueue(1);
        s.Hours(0.05);
        Assert.Equal(1, e.Ints[2]);
        Assert.Equal(2, s.Damage(c).Count);
    }

    [DataFact]
    public void ADiseasedCreatureCatchesNoOtherDisease()
    {
        var s = Make();
        var c = s.Spawn();
        s.D20.Enqueue(1);
        s.Disease(c, FilthFever);
        s.D20.Enqueue(1);
        s.Disease(c, 5); // mindfire
        Assert.Single(c.Effects, x => x.Type == s.K["EFFECT_TYPE_DISEASE"]);
    }
}
