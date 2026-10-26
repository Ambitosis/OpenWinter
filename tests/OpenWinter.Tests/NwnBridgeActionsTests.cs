using System.Text.Json;
using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using Xunit;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateAppearanceMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateGuiMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdatePlayerinfoMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateUpdateMessages;
using static OpenWinter.Net.Nwn.Messages.GuiQuickbarMessages;

namespace OpenWinter.Tests;

/// <summary>
/// The player's abilities over the NWN bridge: the spellbook, quickbar, modes, feats and character sheet the server
/// describes become the client's notifies, and casting, using and editing go back as the input the server accepted.
/// </summary>
public class NwnBridgeActionsTests
{
    const uint Player = 0x7FFFFFFF;

    /// <summary>A cleric (classes.2da row 2) who knows Bless (spells.2da row 6) and Turn Undead (feat 294).</summary>
    sealed class Data : INwnBridgeData
    {
        static readonly Dictionary<(string, int, string), string> Cells = new()
        {
            [("classes", 2, "Name")] = "1", [("classes", 2, "SpellCaster")] = "1", [("classes", 2, "MemorizesSpells")] = "1",
            [("classes", 2, "SpellTableColumn")] = "Cleric", [("classes", 2, "Icon")] = "IR_CLERIC", [("classes", 2, "SpellcastingAbil")] = "WIS",
            [("spells", 6, "Label")] = "Bless", [("spells", 6, "Name")] = "2", [("spells", 6, "Cleric")] = "1", [("spells", 6, "TargetType")] = "0x2F",
            [("spells", 6, "IconResRef")] = "IS_BLESS",
            [("spells", 32, "Label")] = "Cure_Light_Wounds", [("spells", 32, "Cleric")] = "1", [("spells", 32, "TargetType")] = "0x2B",
            [("feat", 294, "FEAT")] = "3", [("feat", 294, "SPELLID")] = "308", [("feat", 294, "ICON")] = "IFE_TURN",
            [("feat", 28, "FEAT")] = "4", [("feat", 28, "ICON")] = "IFE_POWERATT",
            [("iprp_spells", 68, "SpellIndex")] = "33", [("iprp_spells", 68, "Name")] = "5",
        };

        public int TwoDARowCount(string table) => table == "spells" ? 40 : 0;
        public string TwoDA(string table, int row, string column) => Cells.GetValueOrDefault((table, row, column), "");
        public string Text(uint strRef, byte gender) => strRef switch
        {
            1 => "Cleric",
            2 => "Bless",
            3 => "Turn Undead",
            4 => "Power Attack",
            5 => "Cure Minor Wounds",
            _ => "",
        };
        public void UseContent(IReadOnlyList<string> haks, string customTlk) { }
    }

    sealed class Bridge
    {
        public readonly List<NotifyMessage> Notes = [];
        public readonly List<NwnMessage> Sent = [];
        public readonly NwnAbilities Abilities;

        public Bridge()
        {
            Abilities = new NwnAbilities(new Data(), b => Notes.Add(NotifyMessage.Read(new MessageReader(b))), Sent.Add);
            Abilities.Player = Player;
        }

        public JsonElement Last(string kind) => JsonDocument.Parse(Notes.Last(n => n.Kind == kind).Json).RootElement;
    }

    static GameObjUpdateMessages.GameObjUpdateObjList List(params object[] entries)
    {
        var list = new GameObjUpdateMessages.GameObjUpdateObjList();
        list.Entries.AddRange(entries);
        return list;
    }

    /// <summary>The cleric as the server describes her on entering: class, slots, prepared spells, feats and state.</summary>
    static GameObjUpdateMessages.GameObjUpdateObjList Cleric() => List(
        new GameObjUpdateObjListUpdateCreature
        {
            ObjectId = Player,
            PrimaryAttributes = new PrimaryAttributes { Race = 6, Classes = [new CreatureClassEntry { ClassId = 2, Level = 2 }] },
        },
        new GameObjUpdateObjListPlayerInfo
        {
            ObjectId = Player,
            Flags = UpdatePlayerCreatureFlags.State | UpdatePlayerCreatureFlags.Guinumbermemorizedspells
                | UpdatePlayerCreatureFlags.Guimemorizedspell | UpdatePlayerCreatureFlags.Feat | UpdatePlayerCreatureFlags.Guiskill,
            PlayerState = 0x800,
            GuiSkillUseable = 1u << 4,
            NumMemorizedSpellSlots = [new SpellSlotClassUpdate { ClassIndex = 0, LevelFlag = 3, Level0 = 4, Level1 = 4 }],
            MemorizedSpells =
            [
                new MemorizedSpellClassUpdate
                {
                    SpellsToAdd =
                    [
                        new MemorizedSpellAdd { SpellId = 6, SpellSlot = 0, SpellLevel = 1, Readied = true },
                        new MemorizedSpellAdd { SpellId = 6, SpellSlot = 1, SpellLevel = 1, Readied = false },
                    ],
                },
            ],
            Feats = new FeatUpdate
            {
                Changes = [new FeatChange { ChangeType = (sbyte)'A', FeatId = 294 }, new FeatChange { ChangeType = (sbyte)'A', FeatId = 28 }],
                Uses = [new FeatUse { FeatId = 294, UsesRemaining = 2 }],
            },
        });

    static string Hex(NwnMessage m)
    {
        var w = new NwnWsWriter(new NwnSessionContext(_ => 0, (_, _, _) => ""));
        m.Write(w);
        return Convert.ToHexString(w.ToArray());
    }

    [Fact]
    public void PreparedSpellsAndSlotsMakeTheSpellbook()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        var cls = Assert.Single(b.Last("spellbook").GetProperty("classes").EnumerateArray());
        Assert.Equal((2, "Cleric", "ir_cleric", true), (cls.GetProperty("class").GetInt32(), cls.GetProperty("name").GetString(),
            cls.GetProperty("icon").GetString(), cls.GetProperty("memorizes").GetBoolean()));
        var level1 = cls.GetProperty("levels").EnumerateArray().Single(l => l.GetProperty("level").GetInt32() == 1);
        // Two copies of Bless, one ready; four slots, the other two empty.
        var bless = Assert.Single(level1.GetProperty("spells").EnumerateArray());
        Assert.Equal((6, 1, 2), (bless.GetProperty("spell").GetInt32(), bless.GetProperty("ready").GetInt32(), bless.GetProperty("total").GetInt32()));
        Assert.Equal((4, 1), (level1.GetProperty("max").GetInt32(), level1.GetProperty("left").GetInt32()));
        Assert.Equal([6, 6, -1, -1], level1.GetProperty("slots").EnumerateArray().Select(s => s.GetProperty("spell").GetInt32()));
        Assert.Contains(32, level1.GetProperty("available").EnumerateArray().Select(s => s.GetInt32()));
    }

    [Fact]
    public void CastingNamesTheClassByItsPlaceAndTheSpellsTargetType()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        Assert.True(b.Abilities.FromClient("cast-spell", """{"spell":6,"class":2,"meta":255}"""));
        var cast = Assert.IsType<InputMessages.InputCastSpell>(b.Sent.Single());
        // As recorded and accepted: Bless on herself, class 0, TargetType 0x2F (any other target type is refused).
        Assert.Equal("0706000000030003000300030111FFFFFF7F052F00", Hex(cast));
        b.Sent.Clear();
        b.Abilities.FromClient("cast-spell", """{"spell":32,"class":2,"meta":255,"spontaneous":true,"pos":[22,21,0]}""");
        var cure = Assert.IsType<InputMessages.InputCastSpell>(b.Sent.Single());
        Assert.Equal(InputMessages.CastSpellFlags.Spontaneous | InputMessages.CastSpellFlags.HasPosition, cure.Flags);
        Assert.Equal(((float, float, float)?)(22f, 21f, 0f), cure.Position);
    }

    [Fact]
    public void ModesFollowThePlayerStateBits()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        Assert.DoesNotContain(b.Notes, n => n.Kind == "action-mode");
        // Detect mode on (measured: 0x81 over the resting 0x800), then power attack (0x8) in its place.
        b.Abilities.ObjectList(List(new GameObjUpdateObjListPlayerInfo { ObjectId = Player, Flags = UpdatePlayerCreatureFlags.State, PlayerState = 0x881 }));
        b.Abilities.ObjectList(List(new GameObjUpdateObjListPlayerInfo { ObjectId = Player, Flags = UpdatePlayerCreatureFlags.State, PlayerState = 0x808 }));
        Assert.Equal([(0, true), (0, false), (3, true)], b.Notes.Where(n => n.Kind == "action-mode")
            .Select(n => JsonDocument.Parse(n.Json).RootElement).Select(j => (j.GetProperty("mode").GetInt32(), j.GetProperty("on").GetBoolean())));
        // Asking for a mode that is already on sends nothing; the power attack feat toggles its mode.
        b.Abilities.FromClient("action-mode", """{"mode":3,"on":true}""");
        Assert.Empty(b.Sent);
        b.Abilities.FromClient("use-feat", """{"feat":28,"sub":0}""");
        Assert.Equal(InputMessages.ToggleMode.PowerAttack, Assert.IsType<InputMessages.InputToggleMode>(b.Sent.Single()).Mode);
    }

    [Fact]
    public void FeatsWithUsesAreListedAndUsedOnTheirTarget()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        b.Abilities.FromClient("abilities", "{}");
        var abilities = b.Last("abilities");
        var turn = Assert.Single(abilities.GetProperty("feats").EnumerateArray());
        Assert.Equal((294, "Turn Undead", 2, 2), (turn.GetProperty("feat").GetInt32(), turn.GetProperty("name").GetString(),
            turn.GetProperty("uses").GetInt32(), turn.GetProperty("max").GetInt32()));
        Assert.Contains(abilities.GetProperty("modes").EnumerateArray(), m => m.GetProperty("mode").GetInt32() == 3);
        Assert.Equal(4, Assert.Single(abilities.GetProperty("skills").EnumerateArray()).GetProperty("skill").GetInt32());
        b.Abilities.FromClient("use-feat", """{"feat":294,"sub":0}""");
        var use = Assert.IsType<InputMessages.InputUseFeat>(b.Sent.Single());
        Assert.Equal((294, 0, Player), (use.Feat, use.SubFeat, use.Target));
    }

    [Fact]
    public void TheServersQuickbarReplacesTheClientsAndEditsGoBackOneButtonAtATime()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        var dust = new ItemAppearanceAndProps
        {
            BaseItemId = 24, Identified = true, StackSize = 1, Name = new NwnTagged(1, new OriginalItemName { Name = NwnLocString.FromText("Lesser Dust") }),
            Properties = [new ItemPropertyEntry { Name = 15, Subtype = 68, CostTableValue = 1 }],
            UsesLeft = new ItemUsesLeft { UseableProperties = 1, Uses = [1] },
        };
        var all = new GuiQuickbarSetAllButtons();
        all.Slots.Add(new NwnTagged((long)ButtonObjectType.ModeToggle, new ButtonModeToggle { Mode = 1 }));
        all.Slots.Add(new NwnTagged((long)ButtonObjectType.Item, new ButtonItem { Primary = new ButtonItem.PrimaryItem { ObjectId = 5316, PropertyId = 0xFF0000, Item = dust } }));
        all.Slots.Add(new NwnTagged((long)ButtonObjectType.Spell, new ButtonSpell { MultiClass = 0, SpellId = 6 }));
        all.Slots.Add(new NwnTagged((long)ButtonObjectType.Feat, new ButtonFeat { FeatId = 294 }));
        b.Abilities.Quickbar(all);
        b.Abilities.FromClient("quickbar", "{}");
        var bar = b.Last("quickbar");
        Assert.True(bar.GetProperty("authoritative").GetBoolean());
        var slots = bar.GetProperty("slots").EnumerateArray().ToArray();
        Assert.Equal(36, slots.Length);
        Assert.Equal(("mode", 1), (slots[0].GetProperty("Kind").GetString(), slots[0].GetProperty("Id").GetInt32()));
        Assert.Equal(("item", 5316u, 0, "Lesser Dust"), (slots[1].GetProperty("Kind").GetString(), slots[1].GetProperty("Item").GetUInt32(),
            slots[1].GetProperty("Prop").GetInt32(), slots[1].GetProperty("Name").GetString()));
        Assert.Equal(("spell", 6, 2), (slots[2].GetProperty("Kind").GetString(), slots[2].GetProperty("Id").GetInt32(), slots[2].GetProperty("Class").GetInt32()));
        Assert.Equal(JsonValueKind.Null, slots[4].ValueKind);

        // An item is set by its id and property field alone (the server stored it as its own); other buttons in full.
        b.Abilities.FromClient("quickbar-set", """{"creature":2147483647,"index":11,"slot":{"Kind":"item","Item":5316,"Prop":0}}""");
        Assert.Equal("030B0301" + "11C4140000" + "080000FF00", Hex(b.Sent[^1]));
        b.Abilities.FromClient("quickbar-set", """{"index":10,"slot":{"Kind":"mode","Id":0}}""");
        var mode = Assert.IsType<GuiQuickbarSetButton>(b.Sent[^1]);
        Assert.Equal((10, 0), (mode.Button, Assert.IsType<ButtonModeToggle>(mode.Slot.Value).Mode));
        b.Abilities.FromClient("quickbar-set", """{"index":10,"slot":null}""");
        Assert.IsType<ButtonEmpty>(Assert.IsType<GuiQuickbarSetButton>(b.Sent[^1]).Slot.Value);

        // The quickbar's item is the player's to use: its property's place among the usable ones, on herself.
        b.Abilities.FromClient("use-item", """{"item":5316,"prop":0}""");
        Assert.Equal("11C4140000" + "0300" + "0200" + "020111FFFFFF7F" + "0200", Hex(b.Sent[^1]));
    }

    [Fact]
    public void TheSheetAsksForTheMainPanelThenSkills()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        b.Abilities.FromClient("character", "{}");
        Assert.Equal(0, Assert.IsType<GuiCharacterSheetMessages.GuiCharacterSheetStatus>(b.Sent.Single()).ActivePanel);
        b.Abilities.ObjectList(List(new GameObjUpdateMessages.ObjListGuiEntry('S', '\0', new GuiCharacterSheet
        {
            ObjectId = Player,
            Flags = CharacterSheetFlags.Abilities | CharacterSheetFlags.Fortsave | CharacterSheetFlags.Currentxp | CharacterSheetFlags.Baseattack,
            Abilities = new CharSheetAbilities { StrStat = 9, DexStat = 8, ConStat = 16, IntStat = 14, WisStat = 17, ChaStat = 8 },
            FortSave = 6, Experience = 1161, BaseAttack = new CharSheetBaseAttack { BaseAttack = 1 },
        })));
        Assert.Equal(1, Assert.IsType<GuiCharacterSheetMessages.GuiCharacterSheetStatus>(b.Sent[^1]).ActivePanel);
        var sheet = b.Last("character-sheet");
        Assert.Equal((1161, 1, 6), (sheet.GetProperty("xp").GetInt32(), sheet.GetProperty("bab").GetInt32(), sheet.GetProperty("saves").GetProperty("fortitude").GetInt32()));
        Assert.Equal(3, sheet.GetProperty("abilities")[4].GetProperty("mod").GetInt32());
        // A poll with the current version gets nothing new.
        int count = b.Notes.Count;
        b.Abilities.FromClient("character", $$"""{"version":"{{sheet.GetProperty("version").GetString()}}"}""");
        Assert.Equal(count, b.Notes.Count);
    }

    [Fact]
    public void PreparingAndCancellingGoBackAsTheirInput()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        b.Abilities.FromClient("spell-memorize", """{"class":2,"level":1,"slot":0,"spell":-1,"meta":0}""");
        var forget = Assert.IsType<InputMessages.InputUnMemorizeSpell>(b.Sent[^1]);
        Assert.Equal((0, 1, 0), (forget.MultiClass, forget.SpellLevel, forget.SpellSlot));
        b.Abilities.FromClient("spell-memorize", """{"class":2,"level":1,"slot":0,"spell":6,"meta":0}""");
        Assert.Equal(6u, Assert.IsType<InputMessages.InputMemorizeSpell>(b.Sent[^1]).SpellId);
        // A queued cast is cancelled by its action number and group, as the queue named it.
        b.Abilities.ObjectList(List(new GameObjUpdateObjListPlayerInfo
        {
            ObjectId = Player, Flags = UpdatePlayerCreatureFlags.ActionQueue,
            ActionQueue = new ActionQueueChanges { Entries = [new NwnTagged('A', new ActionQueueAdd { ActionId = 4, GroupId = 2, TargetObjectId = 96, SpellId = 450 })] },
        }));
        b.Abilities.FromClient("clear-actions", """{"index":-1}""");
        var cancel = Assert.IsType<InputMessages.InputCancelAction>(b.Sent[^1]);
        Assert.Equal((4, 2), (cancel.ActionId, cancel.GroupId));
        b.Abilities.FromClient("rest", "{}");
        Assert.IsType<InputMessages.InputRest>(b.Sent[^1]);
        Assert.False(b.Abilities.FromClient("area-ready", "{}"));
    }
}
