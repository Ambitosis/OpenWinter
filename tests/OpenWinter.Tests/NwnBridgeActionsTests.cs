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
            [("classes", 2, "SpellTableColumn")] = "Cleric", [("classes", 2, "CanCastSpontaneously")] = "1",
            [("spells", 32, "SpontaneouslyCast")] = "1", [("spells", 107, "Label")] = "Magic_Missile", [("spells", 107, "TargetType")] = "0x02",
            [("spells", 693, "Label")] = "Mind_Blast", [("spells", 693, "TargetType")] = "0x3E", [("classes", 2, "Icon")] = "IR_CLERIC", [("classes", 2, "SpellcastingAbil")] = "WIS",
            [("spells", 6, "Label")] = "Bless", [("spells", 6, "Name")] = "2", [("spells", 6, "Cleric")] = "1", [("spells", 6, "TargetType")] = "0x2F",
            [("spells", 6, "IconResRef")] = "IS_BLESS",
            [("spells", 32, "Label")] = "Cure_Light_Wounds", [("spells", 32, "Cleric")] = "1", [("spells", 32, "TargetType")] = "0x2B",
            [("feat", 294, "FEAT")] = "3", [("feat", 294, "SPELLID")] = "308", [("feat", 294, "ICON")] = "IFE_TURN",
            [("feat", 28, "FEAT")] = "4", [("feat", 28, "ICON")] = "IFE_POWERATT",
            [("iprp_spells", 68, "SpellIndex")] = "33", [("iprp_spells", 68, "Name")] = "5",
            [("spells", 308, "Label")] = "Turn_Undead", [("spells", 308, "ConjTime")] = "1500", [("spells", 308, "ConjAnim")] = "head",
            [("spells", 308, "ConjHeadVisual")] = "vco_mehanholy02", [("spells", 308, "CastAnim")] = "up", [("spells", 308, "CastTime")] = "500",
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
        public double Now;

        public Bridge()
        {
            Abilities = new NwnAbilities(new Data(), b => Notes.Add(NotifyMessage.Read(new MessageReader(b))), Sent.Add, () => Now);
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
        // A domain spell's level goes back as it came; a main/off-hand pair can't be stored, so the button empties.
        b.Abilities.FromClient("quickbar-set", """{"index":12,"slot":{"Kind":"spell","Id":6,"Class":2,"Meta":0,"Sub":1}}""");
        Assert.Equal(1, Assert.IsType<ButtonSpell>(Assert.IsType<GuiQuickbarSetButton>(b.Sent[^1]).Slot.Value).DomainLevel);
        b.Abilities.FromClient("quickbar-set", """{"index":13,"slot":{"Kind":"item","Paired":true,"Item":5316,"SecondaryItem":5317,"Prop":-1}}""");
        Assert.IsType<ButtonEmpty>(Assert.IsType<GuiQuickbarSetButton>(b.Sent[^1]).Slot.Value);
        // The server empties the used-up item's button: the client takes it as an update.
        Assert.False(bar.GetProperty("update").GetBoolean());
        b.Abilities.QuickbarButton(new GuiQuickbarSetButton { Button = 1, Slot = new NwnTagged(0, new ButtonEmpty()) });
        var update = b.Last("quickbar");
        Assert.True(update.GetProperty("update").GetBoolean());
        Assert.Equal(JsonValueKind.Null, update.GetProperty("slots")[1].ValueKind);

        // The quickbar's item is the player's to use: its property's place among the usable ones, on herself.
        b.Abilities.FromClient("use-item", """{"item":5316,"prop":0}""");
        Assert.Equal("11C4140000" + "0300" + "0200" + "020111FFFFFF7F" + "0200", Hex(b.Sent[^1]));
    }

    static GameObjUpdateMessages.GameObjUpdateObjList MainPanel(sbyte fortitude) => List(new GameObjUpdateMessages.ObjListGuiEntry('S', '\0', new GuiCharacterSheet
    {
        ObjectId = Player,
        Flags = CharacterSheetFlags.Abilities | CharacterSheetFlags.Fortsave | CharacterSheetFlags.Currentxp | CharacterSheetFlags.Baseattack,
        Abilities = new CharSheetAbilities { StrStat = 9, DexStat = 8, ConStat = 16, IntStat = 14, WisStat = 17, ChaStat = 8 },
        FortSave = fortitude, Experience = 1161, BaseAttack = new CharSheetBaseAttack { BaseAttack = 1 },
    }));

    static sbyte Panel(NwnMessage m) => Assert.IsType<GuiCharacterSheetMessages.GuiCharacterSheetStatus>(m).ActivePanel;

    [Fact]
    public void TheSheetIsSentOnlyToThePollsOfAnOpenSheetAndSwitchesPanels()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        b.Abilities.FromClient("character", "{}");
        Assert.Equal(0, Panel(b.Sent.Single()));
        // The main panel's figures in: the skills panel next. The sheet itself waits for the client's next poll.
        int notes = b.Notes.Count;
        b.Abilities.ObjectList(MainPanel(6));
        Assert.Equal(1, Panel(b.Sent[^1]));
        Assert.Equal(notes, b.Notes.Count);
        b.Now = 0.5;
        b.Abilities.FromClient("character", """{"version":"1"}""");
        var sheet = b.Last("character-sheet");
        Assert.Equal((1161, 1, 6), (sheet.GetProperty("xp").GetInt32(), sheet.GetProperty("bab").GetInt32(), sheet.GetProperty("saves").GetProperty("fortitude").GetInt32()));
        Assert.Equal(3, sheet.GetProperty("abilities")[4].GetProperty("mod").GetInt32());
        string version = sheet.GetProperty("version").GetString()!;
        // Hit points change in a fight: nothing is sent until a poll, and a poll with the current version gets
        // only what changed.
        notes = b.Notes.Count;
        b.Abilities.ObjectList(List(new GameObjUpdateObjListUpdateCreature { ObjectId = Player, HpCurrent = 5, HpMax = 22 }));
        Assert.Equal(notes, b.Notes.Count);
        b.Now = 1;
        b.Abilities.FromClient("character", $$"""{"version":"{{version}}"}""");
        Assert.Equal(5, b.Last("character-sheet").GetProperty("hp").GetInt32());
        version = b.Last("character-sheet").GetProperty("version").GetString()!;
        notes = b.Notes.Count;
        b.Now = 1.5;
        b.Abilities.FromClient("character", $$"""{"version":"{{version}}"}""");
        Assert.Equal(notes, b.Notes.Count);
        // A few seconds on the skills panel, then back to the main one (the server fills only the active panel).
        b.Now = 3.6;
        b.Abilities.FromClient("character", $$"""{"version":"{{version}}"}""");
        Assert.Equal(0, Panel(b.Sent[^1]));
        // Not polled for a while: the sheet was closed, and opening it again starts on the main panel.
        int sent = b.Sent.Count;
        b.Now = 4.1;
        b.Abilities.FromClient("character", "{}");
        Assert.Equal(sent, b.Sent.Count);
        b.Now = 10;
        b.Abilities.FromClient("character", "{}");
        Assert.Equal(sent, b.Sent.Count); // already on the main panel
        Assert.Equal("character-sheet", b.Notes[^1].Kind);
    }

    [Fact]
    public void ControllingAnotherCreatureForgetsTheLastOne()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        b.Abilities.FromClient("abilities", "{}");
        Assert.NotEmpty(b.Last("abilities").GetProperty("feats").EnumerateArray());
        b.Abilities.Player = 0x7FFFFFFE;
        b.Abilities.FromClient("abilities", "{}");
        Assert.Empty(b.Last("abilities").GetProperty("feats").EnumerateArray());
        Assert.Equal(0x7FFFFFFEu, b.Notes[^1].Subject);
        b.Abilities.FromClient("spellbook", "{}");
        Assert.Empty(b.Last("spellbook").GetProperty("classes").EnumerateArray());
    }

    [Fact]
    public void SpecialAbilitiesAndFormAbilitiesAreCastAsTheServerTookThem()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        b.Abilities.ObjectList(List(new GameObjUpdateObjListPlayerInfo
        {
            ObjectId = Player, Flags = UpdatePlayerCreatureFlags.Spelllikeability | UpdatePlayerCreatureFlags.Polymorphspell,
            SpellLikeAbilities =
            [
                new NwnTagged('S', new SpellLikeAbilityResize { NewSize = 1 }),
                new NwnTagged('U', new SpellLikeAbilityEntryUpdate { Index = 0, Readied = true, CasterLevel = 5, SpellId = 107 }),
            ],
            PolymorphSpells = new PolymorphSpells { SpellId1 = 693, SpellId2 = -1, SpellId3 = -1 },
        }));
        // As measured: a special ability at its caster level; a form's ability as a cast with class 255.
        b.Abilities.FromClient("cast-spell", """{"spell":107,"class":255,"meta":255,"target":19}""");
        var ability = Assert.IsType<InputMessages.InputCastSpellLikeAbility>(b.Sent[^1]);
        Assert.Equal((107u, 5, 19u), (ability.SpellId, ability.CasterLevel, ability.Target!.Value));
        b.Abilities.FromClient("cast-spell", """{"spell":693,"class":255,"meta":255,"form":true,"target":31}""");
        var form = Assert.IsType<InputMessages.InputCastSpell>(b.Sent[^1]);
        Assert.Equal((693u, 255, 0x3E, 31u), (form.SpellId, form.MultiClass, form.SpellTargetType, form.Target!.Value));
    }

    [Fact]
    public void ACureWithNoReadyCopyIsConvertedSpontaneously()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        b.Abilities.FromClient("cast-spell", """{"spell":32,"class":-1,"meta":255}""");
        var cure = Assert.IsType<InputMessages.InputCastSpell>(b.Sent[^1]);
        Assert.Equal((0, InputMessages.CastSpellFlags.Spontaneous | InputMessages.CastSpellFlags.HasTarget), (cure.MultiClass, cure.Flags));
    }

    [Fact]
    public void UnmeasuredModesAreToggledWheneverAskedAndCounterspellNeedsATarget()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        // Rapid shot's state bit is unknown: never shown as on, so every request goes to the server.
        b.Abilities.FromClient("action-mode", """{"mode":7,"on":false}""");
        Assert.Equal(InputMessages.ToggleMode.RapidShot, Assert.IsType<InputMessages.InputToggleMode>(b.Sent.Single()).Mode);
        b.Abilities.FromClient("action-mode", """{"mode":5,"on":true}""");
        b.Abilities.FromClient("counterspell", "{}");
        Assert.Single(b.Sent);
        // Defensive stance and counterspell bits (measured) are announced like the others.
        b.Abilities.ObjectList(List(new GameObjUpdateObjListPlayerInfo { ObjectId = Player, Flags = UpdatePlayerCreatureFlags.State, PlayerState = 0x40800 }));
        Assert.Equal(12, b.Last("action-mode").GetProperty("mode").GetInt32());
        // Bad input is dropped without breaking the session.
        Assert.True(b.Abilities.FromClient("cast-spell", "{not json"));
        Assert.True(b.Abilities.FromClient("cast-spell", """{"spell":6,"pos":["a",1,2]}"""));
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
        // One action by its id (number and group), and none after a new area.
        int sent = b.Sent.Count;
        b.Abilities.FromClient("clear-actions", $$"""{"id":{{NwnAbilities.QueueId((4, 2))}}}""");
        Assert.Equal(sent + 1, b.Sent.Count);
        b.Abilities.AreaChanged();
        b.Abilities.FromClient("clear-actions", "{}");
        Assert.Equal(sent + 1, b.Sent.Count);
        b.Abilities.FromClient("rest", "{}");
        Assert.IsType<InputMessages.InputRest>(b.Sent[^1]);
        Assert.False(b.Abilities.FromClient("area-ready", "{}"));
    }

    [Fact]
    public void RestingShowsItsProgressUntilItFinishes()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        // As recorded: the rest bar (timing event 6, eleven seconds), then the finished feedback and the bar's end.
        b.Abilities.TimingEvent(new GuiTimingEventMessages.GuiTimingEventInfo { Starting = true, Duration = 11000, EventType = 6 });
        b.Abilities.Feedback(new ClientSideMessageFeedbackMessages.ClientSideMessageFeedback
        {
            Payload = new NwnTagged(0, new ClientSideMessageFeedbackMessages.FeedbackRestFinishedRest()),
        });
        b.Abilities.TimingEvent(new GuiTimingEventMessages.GuiTimingEventInfo { Starting = false });
        var rests = b.Notes.Where(n => n.Kind == "rest").Select(n => JsonDocument.Parse(n.Json).RootElement).ToArray();
        Assert.Equal(["started", "finished"], rests.Select(r => r.GetProperty("state").GetString()));
        Assert.Equal(11f, rests[0].GetProperty("duration").GetSingle());
    }

    [Fact]
    public void ADomainSlotStaysOneWhenEmptied()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        var memorized = new MemorizedSpellClassUpdate
        {
            SpellsToAdd = [new MemorizedSpellAdd { SpellId = 6, SpellSlot = 3, SpellLevel = 1, Readied = true, DomainSpell = true }],
        };
        b.Abilities.ObjectList(List(new GameObjUpdateObjListPlayerInfo { ObjectId = Player, Flags = UpdatePlayerCreatureFlags.Guimemorizedspell, MemorizedSpells = [memorized] }));
        var emptied = new MemorizedSpellClassUpdate { SpellsToDelete = [new MemorizedSpellDelete { SpellLevel = 1, SpellSlot = 3 }] };
        b.Abilities.ObjectList(List(new GameObjUpdateObjListPlayerInfo { ObjectId = Player, Flags = UpdatePlayerCreatureFlags.Guimemorizedspell, MemorizedSpells = [emptied] }));
        var slot = b.Last("spellbook").GetProperty("classes")[0].GetProperty("levels").EnumerateArray()
            .Single(l => l.GetProperty("level").GetInt32() == 1).GetProperty("slots")[3];
        Assert.Equal((-1, true), (slot.GetProperty("spell").GetInt32(), slot.GetProperty("domain").GetBoolean()));
        b.Abilities.FromClient("spell-memorize", """{"class":2,"level":1,"slot":3,"spell":6,"meta":0}""");
        Assert.Equal(1, Assert.IsType<InputMessages.InputMemorizeSpell>(b.Sent[^1]).DomainLevel);
    }

    [Fact]
    public void OnlyTheRestsOwnBarEndsARest()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        b.Abilities.TimingEvent(new GuiTimingEventMessages.GuiTimingEventInfo { Starting = true, Duration = 11000, EventType = 6 });
        b.Abilities.TimingEvent(new GuiTimingEventMessages.GuiTimingEventInfo { Starting = true, Duration = 3000, EventType = 2 });
        b.Abilities.TimingEvent(new GuiTimingEventMessages.GuiTimingEventInfo { Starting = false });
        Assert.Equal(["started"], b.Notes.Where(n => n.Kind == "rest").Select(n => JsonDocument.Parse(n.Json).RootElement.GetProperty("state").GetString()));
    }

    [Fact]
    public void ACreaturesConjuringAndCastingShowTheSpellsEffects()
    {
        var b = new Bridge();
        b.Abilities.ObjectList(Cleric());
        // As recorded for Turn Undead: the conjuring animation carries the spell, the casting one its target too.
        b.Abilities.ObjectList(List(new GameObjUpdateObjListUpdateCreature
        {
            ObjectId = Player,
            AnimTypeData = new GameObjUpdateAnimationMessages.AnimTypeData
            {
                AnimType = 16, Spell = new GameObjUpdateAnimationMessages.CastAnimData { SpellId = 308, CastItemId = 0x7F000000 },
            },
        }));
        b.Abilities.ObjectList(List(new GameObjUpdateObjListUpdateCreature
        {
            ObjectId = Player,
            AnimTypeData = new GameObjUpdateAnimationMessages.AnimTypeData
            {
                AnimType = 19, Spell = new GameObjUpdateAnimationMessages.CastAnimData { SpellId = 308, CastItemId = 0x7F000000 },
                CastTarget = new GameObjUpdateAnimationMessages.CastTargetData { TargetType = 1, TargetOid = Player },
            },
        }));
        var conjure = b.Last("spell-conjure");
        Assert.Equal(("head", "vco_mehanholy02", 1.5f), (conjure.GetProperty("animation").GetString(), conjure.GetProperty("headVfx").GetString(),
            conjure.GetProperty("duration").GetSingle()));
        var cast = b.Last("spell-cast");
        Assert.Equal((Player, "up"), (cast.GetProperty("target").GetUInt32(), cast.GetProperty("animation").GetString()));
    }
}
