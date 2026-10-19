using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Weapon sounds and melee sidesteps, as measured on the original (scenes combatsound, combatarmor, combatsteps).</summary>
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

    [DataFact]
    public void MeleeAttackersStepRoundTheirTargetBetweenRounds()
    {
        var (world, attacker, target) = Fight(1.0f);
        var combat = world.System<CombatSystem>();
        combat.Dice = new LowDice();
        combat.Attack(attacker, target);
        var steps = new List<(Vector3 From, Vector3 To, float Facing)>();
        var last = attacker.Position;
        for (int i = 0; i < 30 * 12; i++)
        {
            world.Step(1f / 30);
            if (Vector3.Distance(attacker.Position, last) > 0.05f) steps.Add((last, attacker.Position, attacker.Facing));
            last = attacker.Position;
        }
        var st = combat.State(attacker);
        // About two steps a round with one attack; each 0.8 m along the circle, keeping the distance.
        Assert.True(steps.Count >= 2, $"steps={steps.Count} plan={st.Plan.Count} next={st.NextAttack} planned={st.StepsPlanned} times={string.Join(",", st.StepTimes)} taken={st.StepsTaken} round={st.RoundStart} target={st.Target} t={world.Time}");
        Assert.InRange(steps.Count, 2, 6);
        foreach (var (from, to, facing) in steps)
        {
            Assert.Equal(CombatSystem.CombatStep, Vector3.Distance(from, to), 2);
            // It faces its target from where each step ends (it keeps facing it through the step).
            float toTarget = MathF.Atan2(target.Position.Y - to.Y, target.Position.X - to.X);
            Assert.InRange(MathF.Abs(MathF.IEEERemainder(facing - toTarget, 2 * MathF.PI)), 0, 0.02f);
            Assert.Equal(1.0f, Vector2.Distance(new(to.X, to.Y), new(target.Position.X, target.Position.Y)), 2);
        }
    }
    [DataFact]
    public void PlayersSeeAttackersTurnBackToTheirTarget()
    {
        var (world, attacker, target) = Fight(1.0f);
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
