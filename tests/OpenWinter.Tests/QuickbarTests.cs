using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Gameplay;
using OpenWinter.Content;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using Xunit;

namespace OpenWinter.Tests;

public class QuickbarTests
{
    static (World World, Creature Character) Load(string name)
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        var bic = Gff.Read(File.ReadAllBytes(Path.Combine(TestData.Config!.GameRoot, "data", "lcv", name + ".bic")));
        var character = world.Creatures.FromTemplate(bic);
        world.Register(character);
        return (world, character);
    }

    [DataFact]
    public void AluvianMatchesTheOriginalQuickbarAcrossAllThreePages()
    {
        var (world, pc) = Load("aluviandarkstar");
        var warnings = new List<string>();
        var slots = QuickbarRequests.Read(world, pc, warnings.Add)!;
        Assert.Empty(warnings);
        Assert.Equal(36, slots.Length);
        Assert.Equal("attack", slots[0]!.Arg);
        Assert.Equal("talk", slots[1]!.Arg);
        Assert.Equal(("command", "examine"), (slots[2]!.Kind, slots[2]!.Arg));
        Assert.Equal(("mode", 1), (slots[3]!.Kind, slots[3]!.Id));
        AssertItem(4, 4, -1, "Light Crossbow");
        AssertItem(5, 0, -1, "Dagger");
        AssertItem(6, 7, 0, "Potion of Cure Light Wounds");
        Assert.Equal(("feat", 303), (slots[7]!.Kind, slots[7]!.Id)); // Summon Familiar
        Assert.Equal(new[] { 102, 174, 37, 100, 144 }, slots.Skip(8).Take(5).Select(s => s!.Id));
        Assert.All(slots.Skip(8).Take(5), s =>
        {
            Assert.Equal("spell", s!.Kind);
            Assert.Equal(10, s.Class); // QBMultiClass 0 is the first ClassList entry: wizard, not barbarian.
            Assert.Equal(0, s.Meta);
        });
        Assert.All(slots.Skip(13), s => Assert.Null(s));

        // The notify's JSON is also the slot shape the client's local save consumes.
        var restored = JsonSerializer.Deserialize<QuickbarSlot?[]>(JsonSerializer.Serialize(slots))!;
        Assert.Equal(slots[6]!.Item, restored[6]!.Item);
        Assert.Equal(0, restored[8]!.Meta);

        void AssertItem(int slot, int x, int prop, string name)
        {
            var item = pc.Inventory.Single(i => i.Template.GetInt("Repos_PosX", -1) == x && i.Template.GetInt("Repos_Posy", -1) == 0);
            Assert.Equal(("item", item.Id, prop, name), (slots[slot]!.Kind, slots[slot]!.Item, slots[slot]!.Prop, slots[slot]!.Name));
        }
    }

    [DataFact]
    public void ItemBindingSurvivesMovementBeforeTheClientRequestsItsBar()
    {
        var (world, pc) = Load("aluviandarkstar");
        var dagger = pc.Inventory.Single(i => i.Template.GetInt("Repos_PosX", -1) == 0 && i.Template.GetInt("Repos_Posy", -1) == 0);
        pc.Inventory.Remove(dagger);
        pc.Equipment[EquipSlot.RightHand] = dagger;
        dagger.InvX = dagger.InvY = -1;
        var warnings = new List<string>();
        Assert.Equal(dagger.Id, QuickbarRequests.Read(world, pc, warnings.Add)![5]!.Item);
        Assert.Empty(warnings);
        world.Destroy(dagger);
        Assert.Null(QuickbarRequests.Read(world, pc, warnings.Add)![5]);
        Assert.Contains(warnings, s => s.Contains("slot 6: type 1"));
    }

    [DataFact]
    public void EquippedItemReferencesAreSlotBitsAndSkillsCanBeModes()
    {
        var (world, pc) = Load("ranger");
        var warnings = new List<string>();
        var slots = QuickbarRequests.Read(world, pc, warnings.Add)!;
        Assert.Equal(pc.Equipment[EquipSlot.RightHand].Id, slots[3]!.Item);

        pc.Template.SetList("QBList", [Entry(3, 10), Entry(3, 18), Entry(10, 1), Entry(4, 28)]);
        slots = QuickbarRequests.Read(world, pc, warnings.Add)!;
        Assert.Equal(("mode", 2), (slots[0]!.Kind, slots[0]!.Id));
        Assert.Equal(("skill", 18), (slots[1]!.Kind, slots[1]!.Id));
        Assert.Equal(("mode", 1), (slots[2]!.Kind, slots[2]!.Id));
        Assert.Equal(("feat", 28), (slots[3]!.Kind, slots[3]!.Id));
    }

    [DataFact]
    public void MissingEmptyAndUnsupportedBarsStayDistinct()
    {
        var (world, pc) = Load("aluviandarkstar");
        var warnings = new List<string>();
        pc.Template.Remove("QBList");
        Assert.Null(QuickbarRequests.Read(world, pc, warnings.Add));
        pc.Template.SetList("QBList", []);
        Assert.All(QuickbarRequests.Read(world, pc, warnings.Add)!, s => Assert.Null(s));
        pc.Template.SetList("QBList", [Entry(99), Entry(0), Entry(6), Entry(4, 66060626), Entry(3, 22)]);
        var slots = QuickbarRequests.Read(world, pc, warnings.Add)!;
        Assert.Null(slots[0]);
        Assert.Null(slots[1]);
        Assert.Equal("talk", slots[2]!.Arg);
        Assert.Null(slots[3]);
        Assert.Null(slots[4]);
        Assert.Equal(3, warnings.Count);
        Assert.Contains("slot 1: type 99", warnings[0]);
        Assert.Contains("quickbar", ClientRequests.Kinds);
    }

    [Fact]
    public void LocalSavesIncludingEmptyAndLegacyLayoutsWinOverCharacterDefaults()
    {
        Assert.Null(QuickbarSave<QuickbarSlot>.Read(null, null));
        Assert.Null(QuickbarSave<QuickbarSlot>.Read("broken json", null));
        var empty = QuickbarSave<QuickbarSlot>.Read("{\"Page\":2,\"Bars\":[[null],[],[]]}", "[[102,9]]")!;
        Assert.Equal(2, empty.Page);
        Assert.Null(empty.Bars[0][0]);
        Assert.NotNull(QuickbarSave<QuickbarSlot>.Read(null, "[]"));
        var legacy = QuickbarSave<QuickbarSlot>.Read(null, "[null,[102,9]]")!;
        Assert.Null(legacy.Bars[0][0]);
        Assert.Equal(("spell", 102, 9), (legacy.Bars[0][1]!.Kind, legacy.Bars[0][1]!.Id, legacy.Bars[0][1]!.Class));
        var current = QuickbarSave<QuickbarSlot>.Read("{\"Bars\":[[{\"Kind\":\"command\",\"Arg\":\"rest\"}]]}", "[[102,9]]")!;
        Assert.Equal("rest", current.Bars[0][0]!.Arg);
    }

    static GffStruct Entry(int type, int parameter = -1) => new GffStruct().SetByte("QBObjectType", type).SetInt("QBINTParam1", parameter);
}
