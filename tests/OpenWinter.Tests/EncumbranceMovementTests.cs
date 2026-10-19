using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// A player's load slows her and tells her so (docs/ENCUMBRANCE.md; visual scenes encumberspeed and encumberdrive,
/// parity suite encumbrance_speed). Strength 10: Normal 860, Heavy 1100 tenths of a pound.
/// </summary>
public class EncumbranceMovementTests
{
    sealed class Output : IWorldOutput
    {
        public readonly List<string> Messages = [];
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null) { }
        public void Speak(GameObject speaker, string text, int volume) { }
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        // Only the load lines ("Acquired Item: ..." and the like are left out).
        public void SendMessage(Creature pc, string text) { if (text.StartsWith("You are encumbered", StringComparison.Ordinal)) Messages.Add(text); }
        public void FloatingText(Creature over, string text, bool toFaction) { }
    }

    static (World World, Creature Creature, Output Output) Make(bool player = true)
    {
        var world = new World(ResMan.ForClient(TestData.Paths, [], ""));
        var output = new Output(); world.Output = output;
        var area = new Area(); world.Register(area); world.Areas.Add(area);
        var creature = world.Creatures.FromTemplate(new GffStruct());
        creature.Abilities[0] = 10;
        creature.WalkRate = 2f; creature.RunRate = 4f;
        if (player)
        {
            var (_, connection) = LoopbackConnection.CreatePair();
            creature.Player = new PlayerSession { Connection = connection };
        }
        world.AddToArea(creature, area, default, 0);
        world.System<EncumbranceSystem>().Update(creature);
        return (world, creature, output);
    }

    static Item Load(World world, Creature creature, int tenths)
    {
        Item? last = null;
        while (tenths > 0)
        {
            int stack = Math.Min(tenths, 50);
            last = world.Inventory.CreateOn("nw_wthdt001", creature, stack, "ENCLOAD");
            Assert.NotNull(last);
            tenths -= stack;
        }
        return last!;
    }

    static float Speed(World world, Creature c) => Movement.MaxSpeed(world, c, out _);

    string Encumbered(World w) => w.Resources.TlkString(EncumbranceSystem.EncumberedLine);
    string Overloaded(World w) => w.Resources.TlkString(EncumbranceSystem.OverloadedLine);

    [DataFact]
    public void EachLimitCostsAStageAndALine()
    {
        var (world, pc, output) = Make();
        Assert.Equal("You are encumbered and cannot run.", Encumbered(world));
        Assert.Equal("You are encumbered and cannot walk at normal speed.", Overloaded(world));
        Load(world, pc, 860);
        Assert.Equal(4f, Speed(world, pc));
        Assert.Empty(output.Messages);
        Load(world, pc, 1);
        Assert.Equal([Encumbered(world)], output.Messages);
        Assert.Equal(2f, Speed(world, pc));
        Assert.True(Movement.MaxSpeed(world, pc, out bool walking) > 0 && walking);
        var over = Load(world, pc, 1100 - 861 + 1);
        Assert.Equal([Encumbered(world), Overloaded(world)], output.Messages);
        Assert.Equal(1f, Speed(world, pc));
        // Asking to walk changes nothing once overloaded (encumberdrive: scripted walk at 1 m/s).
        pc.WalkRequested = true;
        Assert.Equal(1f, Speed(world, pc));
        pc.WalkRequested = false;
        // Stealth's stage adds: encumbered in stealth 1, overloaded in stealth 0.5 (stealth.w861, stealth.w1101).
        pc.StealthMode = true;
        Assert.Equal(0.5f, Speed(world, pc));
        // Overloaded back to encumbered is announced again; back to normal is not.
        world.Destroy(over);
        Assert.Equal(1, world.Inventory.Rules.Encumbrance(pc));
        Assert.Equal([Encumbered(world), Overloaded(world), Encumbered(world)], output.Messages);
        Assert.Equal(1f, Speed(world, pc));
        pc.StealthMode = false;
        Assert.Equal(2f, Speed(world, pc));
        foreach (var item in pc.Inventory.ToArray()) world.Destroy(item);
        Assert.Equal(3, output.Messages.Count);
        Assert.Equal(4f, Speed(world, pc));
    }

    [DataFact]
    public void DetectModeCostsAStageOfItsOwnBesideStealth()
    {
        // Visual scene encmodes, a player without Keen Sense: detect or stealth alone 2 m/s, both 1;
        // encumbered with both 0.5; overloaded with both 0.25.
        var (world, pc, _) = Make();
        pc.DetectMode = true;
        Assert.Equal(2f, Speed(world, pc));
        pc.StealthMode = true;
        Assert.Equal(1f, Speed(world, pc));
        Load(world, pc, 861);
        Assert.Equal(0.5f, Speed(world, pc));
        Load(world, pc, 240);
        Assert.Equal(0.25f, Speed(world, pc));
        pc.DetectMode = false;
        Assert.Equal(0.5f, Speed(world, pc));
    }

    [DataFact]
    public void OnePickUpPastBothLimitsGivesBothLines()
    {
        var (world, pc, output) = Make();
        var bag = world.Items.FromBlueprint("nw_it_contain001")!;
        world.Register(bag);
        for (int i = 0; i < 3; i++) Assert.NotNull(world.Inventory.CreateOn("nw_ashto001", bag, 1, ""));
        Assert.Equal(bag, world.Inventory.MoveTo(bag, pc));
        Assert.Equal(2, world.Inventory.Rules.Encumbrance(pc));
        Assert.Equal([Encumbered(world), Overloaded(world)], output.Messages);
        Assert.Equal(1f, Speed(world, pc));
    }

    [DataFact]
    public void OtherCreaturesAreNeverSlowed()
    {
        var (world, npc, output) = Make(player: false);
        Load(world, npc, 5500);
        world.System<EncumbranceSystem>().Update(npc);
        world.System<EncumbranceSystem>().Step(0.1f);
        Assert.Equal(4f, Speed(world, npc));
        Assert.Empty(output.Messages);
    }

    [DataFact]
    public void StrengthEffectsMoveTheLimitsAndEffectsScaleTheSpeed()
    {
        var (world, pc, output) = Make();
        Load(world, pc, 1000);
        Assert.Equal(1, pc.LoadState);
        var strength = new Effect { Type = NwRules.EffectAbilityIncrease, DurationType = 1 };
        strength.Ints[0] = NwRules.AbilityStr; strength.Ints[1] = 4;
        pc.Effects.Add(strength);
        world.System<EncumbranceSystem>().Step(0.1f);
        Assert.Equal(0, pc.LoadState);
        Assert.Equal(4f, Speed(world, pc));
        pc.Effects.Remove(strength);
        Load(world, pc, 101);
        world.System<EncumbranceSystem>().Step(0.1f);
        Assert.Equal(2, pc.LoadState);
        // Hasted and overloaded: 1.5 m/s (haste.w1101).
        pc.Effects.Add(new Effect { Type = NwRules.EffectHaste, DurationType = 1 });
        Assert.Equal(1.5f, Speed(world, pc), 3);
    }

    [DataFact]
    public void APlotPlayerIsToldButNotSlowedUntilTheNextChange()
    {
        var (world, pc, output) = Make();
        pc.Plot = true;
        Load(world, pc, 1101);
        Assert.Equal([Encumbered(world), Overloaded(world)], output.Messages);
        Assert.Equal(4f, Speed(world, pc));
        // Made plot while already slowed, she stays slowed (plot.w1101).
        foreach (var item in pc.Inventory.ToArray()) world.Destroy(item);
        pc.Plot = false;
        Load(world, pc, 1101);
        pc.Plot = true;
        world.System<EncumbranceSystem>().Step(0.1f);
        Assert.Equal(1f, Speed(world, pc));
    }
}
