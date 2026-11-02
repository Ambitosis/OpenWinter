using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Players;
using OpenWinter.Game.Rules;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Weapon sounds and melee combat steps, as measured on the original (scenes combatsound, combatarmor, combatsteps, pcsteps).</summary>
public class CombatPresentationTests
{
    /// <summary>Every roll a 2: every swing misses, so the fight goes on.</summary>
    sealed class LowDice : IDice { public int Roll(int sides) => Math.Min(2, sides); }

    static (World World, Creature Attacker, Creature Target) Fight(float distance)
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        world.Register(world.Module);
        var area = new Area { ResRef = "combat_presentation", Width = 4, Height = 4 };
        area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 3, true);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 3, true);
        world.Register(area); world.Areas.Add(area);
        Creature Actor(Vector3 at)
        {
            var c = world.Creatures.FromTemplate(new GffStruct());
            c.Classes.Add(new CreatureClass { Class = 4, Level = 1 });
            c.Race = 6; c.AppearanceType = 6;
            for (int i = 0; i < c.Abilities.Length; i++) c.Abilities[i] = 10;
            c.MaxHitPoints = c.CurrentHitPoints = 1000;
            world.Register(c); CreatureRules.EnsureInit(world, c);
            world.AddToArea(c, area, at, 0, exact: true);
            return c;
        }
        return (world, Actor(new(20 + distance, 20, 0)), Actor(new(20, 20, 0)));
    }

    static Item Armour(World world, int ac)
    {
        var parts = world.Resources.TwoDA("parts_chest");
        int torso = Enumerable.Range(0, parts.RowCount).First(r => parts.Get(r, "ACBONUS") is { Length: > 0 } v && v != "****" && (int)float.Parse(v, System.Globalization.CultureInfo.InvariantCulture) == ac);
        return world.Items.FromTemplate(new GffStruct().SetInt("BaseItem", 16).SetByte("ArmorPart_Torso", (byte)torso));
    }

    [DataFact]
    public void HitsPlayTheWeaponsSoundsForTheArmourAndMissesOneSwing()
    {
        var (world, attacker, target) = Fight(1.6f);
        var combat = world.System<CombatSystem>();
        AttackOutcome Outcome(bool hit, bool critical = false) => new() { Context = new AttackContext { World = world, Attacker = attacker, Target = target, Dice = combat.Dice, Weapon = attacker.Equipment.GetValueOrDefault(EquipSlot.RightHand) }, Hit = hit, Critical = critical, Animation = "1hslashl" };
        Assert.Equal(new[] { "cb_ht_fleshleth1", "cb_ht_fleshleth2" }, combat.AttackSounds(Outcome(true)));
        attacker.Equipment[EquipSlot.RightHand] = world.Items.FromTemplate(new GffStruct().SetInt("BaseItem", 1)); // longsword: Blade
        target.Equipment[EquipSlot.Chest] = Armour(world, 5);
        Assert.Equal(new[] { "cb_ht_bladechan1", "cb_ht_bladechan2" }, combat.AttackSounds(Outcome(true)));
        target.Equipment[EquipSlot.Chest] = Armour(world, 6);
        Assert.Equal(new[] { "cb_ht_bladeplat1", "cb_ht_bladeplat2", "cb_ht_critical" }, combat.AttackSounds(Outcome(true, critical: true)));
        target.Equipment[EquipSlot.Chest] = Armour(world, 3);
        Assert.Equal(new[] { "cb_ht_bladeleth1", "cb_ht_bladeleth2" }, combat.AttackSounds(Outcome(true)));
        var miss = Assert.Single(combat.AttackSounds(Outcome(false)));
        Assert.Contains(miss, new[] { "cb_sw_blade1", "cb_sw_blade2" });
    }

    /// <summary>Where an unarmed human fights another from: appearance.2da PREFATCKDIST 1.7 + HITDIST 0.3.</summary>
    const float Stand = 2.0f;

    /// <summary>The attacker's moves over a stretch of fighting a passive target (each one a combat step).</summary>
    static List<(Vector3 From, Vector3 To, float Facing)> Steps(World world, Creature attacker, Creature target, float seconds)
    {
        var combat = world.System<CombatSystem>();
        combat.Dice = new LowDice();
        Assert.Equal(Stand, combat.MeleeDistance(attacker, target), 2);
        combat.Attack(attacker, target);
        var steps = new List<(Vector3 From, Vector3 To, float Facing)>();
        var last = attacker.Position;
        for (int i = 0; i < 30 * seconds; i++)
        {
            world.Step(1f / 30);
            if (Vector3.Distance(attacker.Position, last) > 0.05f) steps.Add((last, attacker.Position, attacker.Facing));
            last = attacker.Position;
        }
        return steps;
    }

    static float Range(Vector3 at, Creature target) => Vector2.Distance(new(at.X, at.Y), new(target.Position.X, target.Position.Y));

    static void AssertRoundTheTarget((Vector3 From, Vector3 To, float Facing) step, Creature target)
    {
        Assert.Equal(CombatSystem.CombatStep, Vector3.Distance(step.From, step.To), 2);
        // It faces its target from where each step ends (it keeps facing it through the step).
        float toTarget = MathF.Atan2(target.Position.Y - step.To.Y, target.Position.X - step.To.X);
        Assert.InRange(MathF.Abs(MathF.IEEERemainder(step.Facing - toTarget, 2 * MathF.PI)), 0, 0.02f);
        Assert.Equal(Range(step.From, target), Range(step.To, target), 2);
    }

    [DataFact]
    public void MeleeAttackersStepRoundTheirTarget()
    {
        // An NPC with one attack a round: two steps in each 5 s round, one half way through what its swing leaves and
        // one at a random moment (pcsteps): four or five in 12 s.
        var (world, attacker, target) = Fight(Stand);
        var steps = Steps(world, attacker, target, 12);
        Assert.InRange(steps.Count, 4, 5);
        foreach (var step in steps) AssertRoundTheTarget(step, target);
    }

    [DataFact]
    public void PlayersCharactersStepOnceARoundWithOneAttackAndNeverWithMore()
    {
        // A player's character takes only the step half way through what its swing leaves: 3.4 s into each 5.5 s
        // round, so two in 12 s (pcsteps).
        var (world, attacker, target) = Fight(Stand);
        attacker.Player = new PlayerSession { Connection = Net.LoopbackConnection.CreatePair().Item1, Creature = attacker };
        var steps = Steps(world, attacker, target, 12);
        Assert.Equal(2, steps.Count);
        foreach (var step in steps) AssertRoundTheTarget(step, target);

        // With two attacks a round, none.
        (world, attacker, target) = Fight(Stand);
        attacker.Classes[0].Level = 6;
        CreatureRules.EnsureInit(world, attacker);
        attacker.Player = new PlayerSession { Connection = Net.LoopbackConnection.CreatePair().Item1, Creature = attacker };
        Assert.Equal(2, world.System<CombatSystem>().Rules.PlanRound(attacker).Count);
        Assert.Empty(Steps(world, attacker, target, 12));
    }

    [DataFact]
    public void AttackersOutOfPlaceStepStraightToWhereTheyFight()
    {
        // Too close (1 m, where it fights from 2 m): as the round opens it steps straight back, 0.8 m at most (pcsteps:
        // from 1.1 m with a dagger straight back to 1.4 m).
        var (world, attacker, target) = Fight(1.0f);
        var first = Steps(world, attacker, target, 0.5f).First();
        Assert.Equal(1.8f, Range(first.To, target), 2);
        Assert.Equal(0f, MathF.Abs(first.To.Y - first.From.Y), 2);

        // A little too far but in reach: straight in to where it fights from.
        (world, attacker, target) = Fight(Stand + 0.4f);
        first = Steps(world, attacker, target, 0.5f).First();
        Assert.Equal(Stand, Range(first.To, target), 2);
        Assert.Equal(0f, MathF.Abs(first.To.Y - first.From.Y), 2);
    }

    [DataFact]
    public void PlayersSeeAttackersTurnBackToTheirTarget()
    {
        var (world, attacker, target) = Fight(Stand);
        var combat = world.System<CombatSystem>();
        combat.Dice = new LowDice();
        combat.Attack(attacker, target);
        // What players see: the facing as of the attacker's last change notice (the server sends nothing else).
        float sent = attacker.Facing;
        world.ObjectChanged += o => { if (o == attacker) sent = attacker.Facing; };
        int stepped = 0;
        var last = attacker.Position;
        for (int i = 0; i < 30 * 12; i++)
        {
            world.Step(1f / 30);
            if (Vector3.Distance(attacker.Position, last) > 0.05f) stepped++;
            last = attacker.Position;
            Assert.InRange(MathF.Abs(MathF.IEEERemainder(sent - attacker.Facing, 2 * MathF.PI)), 0, 0.02f);
        }
        Assert.True(stepped > 0);
    }
}
