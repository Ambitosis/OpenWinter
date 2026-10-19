using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Content;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Requests;
using OpenWinter.Game.SaveGames;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Text macros on the quickbar (docs/QUICKBAR_RADIAL.md): the original saves them as QBObjectType 18 with the label in
/// QBCommandLabel and the line in QBCommandLine, both CExoStrings (visual scene quickbargaps, exported character).
/// </summary>
public class QuickbarMacroTests
{
    static (World World, Creature Character) Load()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        var bic = Gff.Read(File.ReadAllBytes(Path.Combine(TestData.Config!.GameRoot, "data", "lcv", "aluviandarkstar.bic")));
        var character = world.Creatures.FromTemplate(bic);
        world.Register(character);
        world.Inventory.Load(character);
        return (world, character);
    }

    static void Set(World world, Creature pc, int index, QuickbarSlot? slot) =>
        ClientRequests.Handle(world, pc, "quickbar-set", JsonSerializer.Serialize(new { index, slot }));

    [Fact]
    public void MacroIsWrittenAsTheOriginalsTextMacroEntry()
    {
        var entry = BicQuickbar.Write(new QuickbarSlot { Kind = "macro", Name = "Hail", Text = "Hail and well met" });
        Assert.Equal(18, entry.GetInt("QBObjectType"));
        Assert.Equal(GffType.ExoString, entry.Field("QBCommandLabel")!.Type);
        Assert.Equal(GffType.ExoString, entry.Field("QBCommandLine")!.Type);
        Assert.Equal("Hail", entry.GetString("QBCommandLabel"));
        Assert.Equal("Hail and well met", entry.GetString("QBCommandLine"));
    }

    [Fact]
    public void NativeTextMacroEntryReadsAsAMacro()
    {
        var bic = new GffStruct(0).SetList("ClassList", []).SetList("QBList", [
            new GffStruct(0).SetByte("QBObjectType", 18).SetString("QBCommandLabel", "Pty").SetString("QBCommandLine", "/p party macro"),
        ]);
        var entry = BicQuickbar.Read(bic)![0];
        Assert.Equal((18, "Pty", "/p party macro"), (entry.Type, entry.Label, entry.Command));
    }

    [DataFact]
    public void MacroSurvivesTheCharacterFileAndComesBackFromANativeEntry()
    {
        var (world, pc) = Load();
        Set(world, pc, 9, new QuickbarSlot { Kind = "macro", Name = "Hail", Text = "Hail and well met" });
        var slots = QuickbarRequests.Read(world, pc, _ => { })!;
        Assert.Equal(("macro", "Hail", "Hail and well met"), (slots[9]!.Kind, slots[9]!.Name, slots[9]!.Text));

        var bic = ObjectSerializer.CreatureGff(world, pc, uint.MaxValue, true);
        QuickbarPersistence.Write(world, pc, bic);
        var saved = bic.GetList("QBList")[9];
        Assert.Equal((18, "Hail", "Hail and well met"), ((int)saved.GetInt("QBObjectType"), saved.GetString("QBCommandLabel"), saved.GetString("QBCommandLine")));

        // As the original writes it (no OpenWinter extension field), it still reads back as the macro.
        saved.Fields.RemoveAll(f => f.Label == "ElySlot");
        var reloaded = world.Creatures.FromTemplate(Gff.Read(Gff.Write(bic, "BIC ")));
        world.Register(reloaded);
        var again = QuickbarRequests.Read(world, reloaded, _ => { })!;
        Assert.Equal(("macro", "Hail", "Hail and well met"), (again[9]!.Kind, again[9]!.Name, again[9]!.Text));
    }

    [DataFact]
    public void OversizedOrMisplacedMacroTextIsRejected()
    {
        var (world, pc) = Load();
        Set(world, pc, 9, new QuickbarSlot { Kind = "macro", Name = "Long", Text = new string('x', QuickbarRequests.MacroTextLimit + 1) });
        Set(world, pc, 10, new QuickbarSlot { Kind = "command", Arg = "rest", Text = "not a macro" });
        Set(world, pc, 11, new QuickbarSlot { Kind = "macro", Name = new string('x', QuickbarRequests.MacroLabelLimit + 1), Text = "hi" });
        Assert.Empty(pc.QuickbarEdits);
    }
}

/// <summary>The original's emote rings and how its quickbar saves an emote (QBObjectType 8, docs/QUICKBAR_RADIAL.md).</summary>
public class QuickbarEmoteTests
{
    [Fact]
    public void RingsHoldTheMeasuredEmotesInTheirPlaces()
    {
        string Ring(int ring) => string.Join("/", Enumerable.Range(0, 8).Select(d => Emotes.All.FirstOrDefault(e => e.Ring == ring && e.Direction == d)?.Name ?? (d == 7 && ring < 2 ? "More" : "")));
        Assert.Equal("Bow/Threaten/Greet/Laugh/Salute/Cheer/Goodbye/More", Ring(0));
        Assert.Equal("Nod/Tired/Beg/Poisoned/Bored/Look Far/Scratch Head/More", Ring(1));
        Assert.Equal("Fall down forwards//Sit on ground/Meditate/Drink potion/Read/Worship/Fall down backwards", Ring(2));
    }

    [Fact]
    public void EmoteIsSavedAndReadAsTheOriginalsEntry()
    {
        var bow = BicQuickbar.Write(new QuickbarSlot { Kind = "emote", Arg = "bow" });
        Assert.Equal((8, 7), ((int)bow.GetInt("QBObjectType"), (int)bow.GetInt("QBINTParam1")));
        // OpenWinter's earlier name for Greet still saves as the original's Greet.
        var greet = BicQuickbar.Write(new QuickbarSlot { Kind = "emote", Arg = "greeting" });
        Assert.Equal((8, 6), ((int)greet.GetInt("QBObjectType"), (int)greet.GetInt("QBINTParam1")));
        var bic = new GffStruct(0).SetList("ClassList", []).SetList("QBList", [
            new GffStruct(0).SetByte("QBObjectType", 8).SetInt("QBINTParam1", 1),
        ]);
        Assert.Equal((8, 1), (BicQuickbar.Read(bic)![0].Type, BicQuickbar.Read(bic)![0].Parameter));
        Assert.Equal("cheer", Emotes.FromId(1)!.Key);
    }

    [DataFact]
    public void ChosenEmotePlaysAsAnActionThatHoldsForItsMeasuredTime()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        var pc = world.Creatures.FromTemplate(Gff.Read(File.ReadAllBytes(Path.Combine(TestData.Config!.GameRoot, "data", "lcv", "aluviandarkstar.bic"))));
        world.Register(pc);
        ClientRequests.Handle(world, pc, "quickbar-set", JsonSerializer.Serialize(new { index = 3, slot = new QuickbarSlot { Kind = "emote", Arg = "sit" } }));
        ClientRequests.Handle(world, pc, "quickbar-set", JsonSerializer.Serialize(new { index = 4, slot = new QuickbarSlot { Kind = "emote", Arg = "no such emote" } }));
        Assert.Equal("sit", pc.QuickbarEdits[3]!.Arg);
        Assert.False(pc.QuickbarEdits.ContainsKey(4));
        var sit = Emotes.Find("sit")!;
        Assert.Equal((7, 10f), (sit.Animation, AnimationAction.BlockingTime(sit.Animation, 1, sit.Seconds)));
        Assert.Equal(7f, AnimationAction.BlockingTime(Emotes.Find("worship")!.Animation, 1, Emotes.Find("worship")!.Seconds));
    }
}

