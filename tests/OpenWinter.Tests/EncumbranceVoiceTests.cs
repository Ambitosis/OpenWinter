using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.WorldObjects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class EncumbranceVoiceTests
{
    static (World World, Creature Creature, Output Output) Make(bool player = true)
    {
        var world = new World(ResMan.ForClient(TestData.Paths, [], ""));
        var output = new Output(); world.Output = output;
        var area = new Area(); world.Register(area); world.Areas.Add(area);
        var creature = world.Creatures.FromTemplate(new GffStruct());
        creature.Abilities[0] = 10;
        if (player)
        {
            var (_, connection) = LoopbackConnection.CreatePair();
            creature.Player = new PlayerSession { Connection = connection };
        }
        creature.SoundSet = Enumerable.Range(0, world.Resources.TwoDA("soundset").RowCount)
            .First(row => VoiceService.Resolve(world.Resources, row, 32).Sound.Length > 0);
        world.AddToArea(creature, area, default, 0);
        return (world, creature, output);
    }

    static Item Load(World world, Creature creature, int tenths)
    {
        Item? last = null;
        while (tenths > 0)
        {
            int stack = Math.Min(tenths, 50);
            last = world.Inventory.CreateOn("nw_wthdt001", creature, stack, "VOICELOAD");
            Assert.NotNull(last);
            tenths -= stack;
        }
        return last!;
    }

    [DataFact]
    public void ExactThresholdAndRepeatedStatesDoNotDuplicateWarnings()
    {
        var (world, pc, output) = Make();
        var table = world.Resources.TwoDA("encumbrance");
        int normal = table.GetInt(10, "Normal"), heavy = table.GetInt(10, "Heavy");
        Assert.True(heavy > normal && normal > 0);
        Load(world, pc, normal);
        Assert.Equal(normal, world.Inventory.Rules.CarriedTenths(pc));
        Assert.Empty(output.Voices);
        Load(world, pc, 1);
        Assert.Single(output.Voices);
        Load(world, pc, 10);
        Assert.Single(output.Voices);
        var last = Load(world, pc, heavy - normal - 10);
        Assert.Equal(2, world.Inventory.Rules.Encumbrance(pc));
        Assert.Equal(2, output.Voices.Count);
        world.Destroy(last);
        Assert.Equal(1, world.Inventory.Rules.Encumbrance(pc));
        Assert.Equal(3, output.Voices.Count);
        foreach (var item in pc.Inventory.ToArray()) world.Destroy(item);
        Assert.Equal(0, world.Inventory.Rules.Encumbrance(pc));
        Assert.Equal(3, output.Voices.Count);
        // UI reads must not cause another warning.
        world.Inventory.InventoryView(pc);
        Assert.Equal(3, output.Voices.Count);
        Assert.All(output.Voices, note =>
        {
            Assert.Equal(32, note.GetProperty("id").GetInt32());
            Assert.Equal(VoiceService.Resolve(world.Resources, pc.SoundSet, 32).Sound,
                note.GetProperty("sound").GetString());
        });
        Assert.Empty(output.Speech);
    }

    [DataFact]
    public void FilledContainerPickupAndTopLevelRemovalMatchMeasuredStates()
    {
        var (world, pc, output) = Make();
        var bag = world.Items.FromBlueprint("nw_it_contain001")!;
        world.Register(bag);
        Assert.NotNull(world.Inventory.CreateOn("nw_ashto001", bag, 1, ""));
        Assert.NotNull(world.Inventory.CreateOn("nw_ashto001", bag, 1, ""));
        Assert.Empty(output.Voices);
        Assert.Equal(bag, world.Inventory.MoveTo(bag, pc));
        Assert.Equal(1, world.Inventory.Rules.Encumbrance(pc));
        Assert.Single(output.Voices);
        var shield = world.Inventory.CreateOn("nw_ashto001", pc, 1, "")!;
        Assert.Equal(2, world.Inventory.Rules.Encumbrance(pc));
        Assert.Equal(2, output.Voices.Count);
        Assert.True(world.Inventory.Drop(shield, new Location(pc.Area!.Id, pc.Position, 0), pc));
        Assert.Equal(1, world.Inventory.Rules.Encumbrance(pc));
        Assert.Equal(3, output.Voices.Count);
        world.Destroy(bag);
        Assert.Equal(0, world.Inventory.Rules.Encumbrance(pc));
        Assert.Equal(3, output.Voices.Count);
        Assert.Empty(output.Speech);
    }

    [DataFact]
    public void UnmeasuredNpcAndNestedMutationRoutesDoNotGainAutomaticVoices()
    {
        var (world, npc, output) = Make(player: false);
        Load(world, npc, world.Resources.TwoDA("encumbrance").GetInt(10, "Heavy") + 1);
        foreach (var item in npc.Inventory.ToArray()) world.Destroy(item);
        Assert.Empty(output.Voices);
        var (pcWorld, pc, pcOutput) = Make();
        var bag = pcWorld.Inventory.CreateOn("nw_it_contain001", pc, 1, "")!;
        for (int i = 0; i < 3; i++) pcWorld.Inventory.CreateOn("nw_ashto001", bag, 1, "");
        Assert.Equal(2, pcWorld.Inventory.Rules.Encumbrance(pc));
        foreach (var item in bag.Inventory.ToArray()) pcWorld.Destroy(item);
        Assert.Empty(pcOutput.Voices);
    }

    [DataFact]
    public void ProtectedPolymorphCleanupDoesNotGainAutomaticWarnings()
    {
        var (world, pc, output) = Make();
        Load(world, pc, 900);
        var stored = world.Inventory.CreateOn("nw_ashto001", pc, 1, "")!;
        pc.Inventory.Remove(stored);
        var form = world.Items.FromBlueprint("nw_wswls001")!;
        Assert.NotNull(form);
        world.Register(form);
        form.Possessor = pc.Id;
        pc.Equipment[EquipSlot.RightHand] = form;
        var polymorph = new PolymorphState();
        polymorph.Stored[EquipSlot.LeftHand] = stored;
        polymorph.Form.Add(form);
        pc.PolymorphForm = polymorph;
        Assert.Equal(2, world.Inventory.Rules.Encumbrance(pc));
        output.Voices.Clear();
        world.Destroy(form);
        Assert.Equal(2, world.Inventory.Rules.Encumbrance(pc));
        world.Destroy(stored);
        Assert.Equal(1, world.Inventory.Rules.Encumbrance(pc));
        Assert.Empty(output.Voices);
    }

    sealed class Output : IWorldOutput
    {
        public readonly List<JsonElement> Voices = [];
        public readonly List<string> Speech = [];
        public void Notify(string kind, GameObject? subject, object data, Creature? player = null)
        { if (kind == "voice-chat") Voices.Add(JsonSerializer.SerializeToElement(data)); }
        public void Speak(GameObject speaker, string text, int volume) => Speech.Add(text);
        public void PlayAnimation(GameObject o, int animation, float speed, float duration) { }
        public void SendMessage(Creature pc, string text) { }
        public void FloatingText(Creature over, string text, bool toFaction) { }
    }
}
