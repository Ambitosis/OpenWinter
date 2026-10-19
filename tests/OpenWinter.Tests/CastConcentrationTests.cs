using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Spells;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Concentration and the casting round length, as measured by the parity suites cast_concentration and
/// queue_cadence.</summary>
public class CastConcentrationTests
{
    static (World world, Creature caster) Setup(string? spells = null)
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        if (spells != null)
        {
            var source = new OpenWinter.Game.Persistence.MemorySource("authored cast timing");
            source.Set(ResKey.Of("spells", ResType.TwoDA), System.Text.Encoding.UTF8.GetBytes(spells));
            world.Resources.AddFirst(source);
        }
        var area = new Area { ResRef = "concentration", Width = 4, Height = 4 };
        world.Register(area);
        world.Areas.Add(area);
        var caster = new Creature { MaxHitPoints = 1000, CurrentHitPoints = 1000 };
        world.AddToArea(caster, area, new Vector3(10, 10, 0), 0);
        CreatureRules.EnsureInit(world, caster);
        return (world, caster);
    }

    static void Tick(World world, float seconds)
    {
        for (int i = 0; i < (int)MathF.Round(seconds * 100); i++) world.Step(.01f);
    }

    // Mage Armor: ConjTime 1500, released at 2.0 s.
    static CastSpellAction Cast(Creature c) => new() { Spell = 102, Cheat = true, Target = c.Id };

    [DataFact]
    public void AHitWhileConjuringStopsTheCast()
    {
        var (world, c) = Setup();
        var cast = Cast(c);
        c.Actions.Add(cast);
        Tick(world, 1f);
        HitPoints.Damage(world, c, GameObject.InvalidId, 200, 8);
        Tick(world, 2f);
        Assert.Empty(c.Actions);
        Assert.NotSame(cast.Info, c.LastSpell);
    }

    [DataFact]
    public void AHitAfterConjuringEndsDoesNotStopTheCast()
    {
        var (world, c) = Setup();
        var cast = Cast(c);
        c.Actions.Add(cast);
        Tick(world, 1.7f);
        HitPoints.Damage(world, c, GameObject.InvalidId, 200, 8);
        Tick(world, .5f);
        Assert.Same(cast.Info, c.LastSpell);
    }

    [DataFact]
    public void ACastLongerThanItsRoundStretchesTheRound()
    {
        var (world, c) = Setup("2DA V2.0\n\nLabel ConjTime CastTime CastAnim ConjAnim Range Innate ImpactScript\n102 Long 1500 5000 self hand P 1 ****\n");
        c.Actions.Add(Cast(c));
        Tick(world, .05f);
        var state = world.System<CombatSystem>().State(c);
        Assert.InRange(state.SpellRoundEnd - world.Time, 6.9, 7.0);
    }
}
