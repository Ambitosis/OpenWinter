using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>The spellbook payload and preparing spells ("spell-memorize") with the module's player character.</summary>
public class SpellbookTests
{
    const int SpellBless = 6, SpellMageArmor = 102, SpellMagicMissile = 107;
    const int ClassCleric = 2, ClassWizard = 10;

    static (World, GameServer, IConnection, Creature) Login()
    {
        var world = new World(TestData.Module);
        world.LoadModule();
        var server = new GameServer(world, new ServerOptions { StartArea = "ss_town", DefaultCharacter = "freddiepharkasf1" });
        var (client, serverEnd) = LoopbackConnection.CreatePair();
        server.Accept(serverEnd);
        client.Send(new HelloMessage { PlayerName = "Tester" }.Write());
        server.Tick(1f / 30);
        uint me = 0;
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            if (r.Type == MessageType.ControlObject) me = ControlObjectMessage.Read(r).ObjectId;
        }
        var pc = world.Get<Creature>(me)!;
        CreatureRules.EnsureInit(world, pc);
        for (int i = 0; i < 6; i++) pc.Abilities[i] = 18;
        return (world, server, client, pc);
    }

    static CreatureClass AddClass(Creature c, int cls, int level)
    {
        var cc = new CreatureClass { Class = cls, Level = level };
        c.Classes.Add(cc);
        return cc;
    }

    static JsonElement Book(World w, Creature c) => JsonSerializer.SerializeToElement(SpellbookRequests.Describe(w, c));

    static JsonElement ClassOf(JsonElement book, int cls) =>
        book.GetProperty("classes").EnumerateArray().Single(x => x.GetProperty("class").GetInt32() == cls);

    static JsonElement LevelOf(JsonElement cls, int level) =>
        cls.GetProperty("levels").EnumerateArray().Single(x => x.GetProperty("level").GetInt32() == level);

    [DataFact]
    public void QuickbarCountsFollowPreparationConsumptionRestAndRemovalIncludingDomainCopies()
    {
        var (world, _, _, pc) = Login();
        var cleric = AddClass(pc, ClassCleric, 3);
        OpenWinter.Client.UI.QuickbarSpellCounts Count()
        {
            var entries = ClassOf(Book(world, pc), ClassCleric).GetProperty("levels").EnumerateArray()
                .SelectMany(level => level.GetProperty("spells").EnumerateArray().Select(spell =>
                    new OpenWinter.Client.UI.BookSpell(spell.GetProperty("spell").GetInt32(), ClassCleric,
                        level.GetProperty("level").GetInt32(), spell.GetProperty("meta").GetInt32(),
                        spell.GetProperty("ready").GetInt32(), spell.GetProperty("total").GetInt32())));
            return OpenWinter.Client.UI.QuickbarSpellCounts.Read(entries, true, SpellBless, ClassCleric, 0);
        }
        Assert.Equal(0, Count().Badge);
        Assert.Null(SpellbookRequests.Memorize(world, pc, ClassCleric, 1, 0, SpellBless, 0));
        Assert.Equal(0, Count().Badge); // Selected for preparation, but not yet rested.
        CreatureRules.RestoreSpells(world, pc);
        Assert.Equal(1, Count().Badge);
        cleric.Memorized[1][0] = (SpellBless, 0, false);
        Assert.Equal(0, Count().Badge);
        Assert.False(Count().Usable);
        CreatureRules.RestoreSpells(world, pc);
        Assert.Equal(1, Count().Badge);
        // The server groups real copies, including domain slots, without counting available choices.
        cleric.Memorized[1].Add((SpellBless, 0, true));
        pc.DomainSlots.Add((pc.Classes.IndexOf(cleric), 1, cleric.Memorized[1].Count - 1));
        Assert.Equal(2, Count().Badge);
        cleric.Memorized[1].Clear();
        Assert.Equal(0, Count().Badge);
        Assert.False(Count().Usable);
    }

    [DataFact]
    public void AuthenticatedSpellbookUsesLoadedDomainsWithoutGrantingThemToWizard()
    {
        foreach (int cls in new[] { ClassCleric, ClassWizard })
        {
            var (w, server, client, pc) = Login();
            var entry = new GffStruct(2).SetInt("Class", cls).SetShort("ClassLevel", 20);
            if (cls == ClassWizard) entry.SetByte("Domain1", 13).SetByte("Domain2", 18);
            var loaded = w.Creatures.FromTemplate(new GffStruct().SetList("ClassList", [entry]));
            pc.Classes.Clear();
            pc.Classes.AddRange(loaded.Classes);
            pc.DomainSlots.Clear();
            pc.DomainSlots.Add((0, 2, 0)); // Even a forced marker cannot grant a wizard domains.
            while (client.TryReceive(out _)) { }
            client.Send(new ClientRequestMessage { Kind = "spellbook", Json = "{}" }.Write());
            server.Tick(1f / 30);
            JsonElement book = default;
            while (client.TryReceive(out var packet))
            {
                var reader = new MessageReader(packet);
                if (reader.Type != MessageType.Notify) continue;
                var notice = NotifyMessage.Read(reader);
                if (notice.Kind == "spellbook") book = JsonSerializer.Deserialize<JsonElement>(notice.Json);
            }
            Assert.NotEqual(JsonValueKind.Undefined, book.ValueKind);
            var available = LevelOf(ClassOf(book, cls), 2).GetProperty("available").EnumerateArray().Select(x => x.GetInt32());
            Assert.Equal(cls == ClassCleric, available.Contains(13)); // Animal-domain Cat's Grace.
            client.Send(new ClientRequestMessage
            {
                Kind = "spell-memorize", Json = JsonSerializer.Serialize(
                new { @class = cls, level = 2, slot = 0, spell = 13, meta = 0 })
            }.Write());
            server.Tick(1f / 30);
            Assert.Equal(cls == ClassCleric, pc.Classes[0].Memorized[2][0].Spell == 13);
        }
    }

    [DataFact]
    public void InnateAbilitiesExposeRemainingCopiesAndTargetingToTheRadial()
    {
        var (w, _, _, pc) = Login();
        pc.SpellAbilities.Clear();
        pc.SpellAbilities.Add(new SpellAbility { Spell = SpellBless, CasterLevel = 4 });
        pc.SpellAbilities.Add(new SpellAbility { Spell = SpellBless, CasterLevel = 4, Ready = false });
        var book = Book(w, pc);
        var ability = Assert.Single(book.GetProperty("abilities").EnumerateArray());
        Assert.Equal(SpellBless, ability.GetProperty("spell").GetInt32());
        Assert.Equal(1, ability.GetProperty("ready").GetInt32());
        Assert.Equal(2, ability.GetProperty("total").GetInt32());
        Assert.Contains(book.GetProperty("info").EnumerateArray(), e => e.GetProperty("spell").GetInt32() == SpellBless && e.GetProperty("targetType").GetInt32() != 0);
        var slot = OpenWinter.Game.Spells.SpellSlots.Find(w, pc, SpellBless, 0, 255, 0, false);
        Assert.NotNull(slot);
        Assert.Equal(OpenWinter.Game.Spells.SlotKind.Ability, slot.Kind);
        slot.Ability!.Ready = false;
        Assert.Equal(0, Assert.Single(Book(w, pc).GetProperty("abilities").EnumerateArray()).GetProperty("ready").GetInt32());
    }

    [DataFact]
    public void WizardPreparesOnlySpellsFromItsSpellbook()
    {
        var (w, _, _, pc) = Login();
        var wiz = AddClass(pc, ClassWizard, 3);
        wiz.KnownSpells[1].Add(SpellMagicMissile);
        int slots = CreatureRules.SpellSlots(w, pc, ClassWizard, 1);
        Assert.True(slots > 0);

        var level1 = LevelOf(ClassOf(Book(w, pc), ClassWizard), 1);
        Assert.Equal(slots, level1.GetProperty("slots").GetArrayLength());
        Assert.All(level1.GetProperty("slots").EnumerateArray(), s => Assert.Equal(-1, s.GetProperty("spell").GetInt32()));
        Assert.Contains(level1.GetProperty("available").EnumerateArray(), s => s.GetInt32() == SpellMagicMissile);

        Assert.Null(SpellbookRequests.Memorize(w, pc, ClassWizard, 1, 0, SpellMagicMissile, 0));
        // Prepared now, ready after the next rest.
        Assert.Equal((SpellMagicMissile, 0, false), wiz.Memorized[1][0]);
        Assert.Equal(0, CreatureRules.SpellCount(w, pc, SpellMagicMissile));
        CreatureRules.RestoreSpells(w, pc);
        Assert.Equal(1, CreatureRules.SpellCount(w, pc, SpellMagicMissile));

        // Bless isn't in the spellbook (nor a wizard spell); slots beyond the count don't exist.
        Assert.NotNull(SpellbookRequests.Memorize(w, pc, ClassWizard, 1, 1, SpellBless, 0));
        Assert.NotNull(SpellbookRequests.Memorize(w, pc, ClassWizard, 1, slots, SpellMagicMissile, 0));
        // A metamagic the character lacks the feat for.
        Assert.NotNull(SpellbookRequests.Memorize(w, pc, ClassWizard, 2, 0, SpellMagicMissile, NwRules.MetaExtend));

        // Emptying the slot.
        Assert.Null(SpellbookRequests.Memorize(w, pc, ClassWizard, 1, 0, -1, 0));
        Assert.Equal(-1, wiz.Memorized[1][0].Spell);
        Assert.Equal(0, CreatureRules.SpellCount(w, pc, SpellMagicMissile));
    }

    [DataFact]
    public void ClericPreparesFromTheWholeClassList()
    {
        var (w, _, _, pc) = Login();
        AddClass(pc, ClassCleric, 3);
        var level1 = LevelOf(ClassOf(Book(w, pc), ClassCleric), 1);
        Assert.Contains(level1.GetProperty("available").EnumerateArray(), s => s.GetInt32() == SpellBless);
        Assert.Null(SpellbookRequests.Memorize(w, pc, ClassCleric, 1, 0, SpellBless, 0));
        Assert.NotNull(SpellbookRequests.Memorize(w, pc, ClassCleric, 1, 1, SpellMagicMissile, 0));
        // The prepared spell is listed in its slot and in the grouped list, with names for the client.
        var book = Book(w, pc);
        var slot = LevelOf(ClassOf(book, ClassCleric), 1).GetProperty("slots")[0];
        Assert.Equal(SpellBless, slot.GetProperty("spell").GetInt32());
        Assert.False(slot.GetProperty("ready").GetBoolean());
        var info = book.GetProperty("info").EnumerateArray().Single(i => i.GetProperty("spell").GetInt32() == SpellBless);
        Assert.False(string.IsNullOrEmpty(info.GetProperty("name").GetString()));
        Assert.False(string.IsNullOrEmpty(info.GetProperty("school").GetString()));
    }

    [DataFact]
    public void MemorizeRequestAnswersWithTheSpellbook()
    {
        var (w, server, client, pc) = Login();
        AddClass(pc, ClassCleric, 1);
        while (client.TryReceive(out _)) { }
        client.Send(new ClientRequestMessage { Kind = "spell-memorize", Json = JsonSerializer.Serialize(new { @class = ClassCleric, level = 1, slot = 0, spell = SpellBless, meta = 0 }) }.Write());
        server.Tick(1f / 30);
        var kinds = new List<string>();
        while (client.TryReceive(out var m))
        {
            var r = new MessageReader(m);
            if (r.Type == MessageType.Notify) kinds.Add(NotifyMessage.Read(r).Kind);
        }
        Assert.Contains("spellbook", kinds);
        Assert.DoesNotContain("spellbook-error", kinds);
        Assert.Equal(SpellBless, pc.Classes.Single(c => c.Class == ClassCleric).Memorized[1][0].Spell);
    }

    [DataTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void FirstSpellbookRequestKeepsEarlyScriptChangesLive(bool requestAbilitiesAfterMutation)
    {
        var (world, server, client, pc) = Login();
        var wizard = AddClass(pc, ClassWizard, 3);
        wizard.KnownSpells[1].AddRange([SpellMagicMissile, 544]);
        world.EnableScripts();
        var host = world.Scripts!;
        var program = new NcsProgram("feed", System.Text.Encoding.ASCII.GetBytes("NCS V1.0B\0\0\0\0").Concat(new byte[] { 0x20, 0 }).ToArray());
        object? Call(string name, params object?[] args) => ((IScriptEngine)host).Call(
            new ScriptThread(host.VM, program, pc.Id), host.Definitions.ByName[name], args);
        void Send(string kind)
        {
            client.Send(new ClientRequestMessage { Kind = kind, Json = "{}" }.Write());
            server.Tick(1f / 30);
        }
        List<JsonElement> Books()
        {
            var books = new List<JsonElement>();
            while (client.TryReceive(out var packet))
            {
                var reader = new MessageReader(packet);
                if (reader.Type != MessageType.Notify) continue;
                var message = NotifyMessage.Read(reader);
                if (message.Kind != "spellbook") continue;
                Assert.Equal(pc.Id, message.Subject);
                using var document = JsonDocument.Parse(message.Json);
                books.Add(document.RootElement.Clone());
            }
            return books;
        }
        void Tick() { for (int i = 0; i < 40; i++) server.Tick(1f / 30); }
        JsonElement Slot(JsonElement book) => LevelOf(ClassOf(book, ClassWizard), 1).GetProperty("slots")[0];

        Call("SetMemorizedSpell", pc.Id, ClassWizard, 1, 0, SpellMagicMissile, 1, 0, 0);
        Send("spellbook");
        Assert.Equal(SpellMagicMissile, Slot(Assert.Single(Books())).GetProperty("spell").GetInt32());
        // This mutation used to disappear into the first feed baseline, leaving the client stale forever.
        Call("SetMemorizedSpell", pc.Id, ClassWizard, 1, 0, 544, 1, 0, 0);
        Assert.Equal(544, Call("GetMemorizedSpellId", pc.Id, ClassWizard, 1, 0));
        if (requestAbilitiesAfterMutation) Send("abilities");
        Tick();
        var changed = Slot(Assert.Single(Books()));
        Assert.Equal(544, changed.GetProperty("spell").GetInt32());
        Assert.True(changed.GetProperty("ready").GetBoolean());
        Tick();
        Assert.Empty(Books()); // An unchanged book is not resent every poll.
        Call("SetMemorizedSpellReady", pc.Id, ClassWizard, 1, 0, 0);
        Tick();
        Assert.False(Slot(Assert.Single(Books())).GetProperty("ready").GetBoolean());
        Send("spellbook");
        Assert.False(Slot(Assert.Single(Books())).GetProperty("ready").GetBoolean());
        Tick();
        Assert.Empty(Books()); // Explicit refresh records exactly the payload delivered.
    }

    [DataFact]
    public void AuthenticatedHotbarBlessCastsAgainAfterActualRest()
    {
        foreach (int meta in new[] { 255, 0 })
        {
            var (w, server, client, pc) = Login();
            w.EnableScripts();
            w.Module.Scripts["Mod_OnPlrRest"] = ""; // Exclude the test PW module's custom rest veto.
            pc.Scripts["ScriptRested"] = "";
            var cleric = AddClass(pc, ClassCleric, 1);
            Assert.Null(SpellbookRequests.Memorize(w, pc, ClassCleric, 1, 0, SpellBless, 0));
            CreatureRules.RestoreSpells(w, pc);
            void Tick(float seconds) { for (int i = 0; i < (int)(seconds * 30) + 1; i++) server.Tick(1f / 30); }
            void Cast() => client.Send(new ClientRequestMessage
            {
                Kind = "cast-spell", Json = JsonSerializer.Serialize(
                new { spell = SpellBless, @class = ClassCleric, meta, target = pc.Id })
            }.Write());
            CreatureRules.SyncHitPoints(w, pc); // Settle the fixture class/ability changes before concentration.
            Tick(1);
            Cast(); Tick(4);
            var diagnostics = new List<string>();
            while (client.TryReceive(out var packet))
            {
                var reader = new MessageReader(packet);
                if (reader.Type == MessageType.Chat) diagnostics.Add(ChatMessage.Read(reader).Text);
                else if (reader.Type == MessageType.Notify)
                {
                    var notice = NotifyMessage.Read(reader);
                    if (notice.Kind.StartsWith("spell")) diagnostics.Add(notice.Kind + ":" + notice.Json);
                }
            }
            Assert.True(pc.LastSpell != null, string.Join("\n", diagnostics));
            Assert.Equal(SpellBless, pc.LastSpell!.SpellId);
            Assert.False(cleric.Memorized[1][0].Ready);
            Assert.True(OpenWinter.Game.Spells.RestAction.CanRest(w, pc, false, out var why), why);
            client.Send(new ClientRequestMessage { Kind = "rest", Json = "{}" }.Write());
            Tick(.2f);
            Assert.True(pc.IsResting);
            Tick(OpenWinter.Game.Spells.RestAction.Duration(w, pc) + 1);
            Assert.False(pc.IsResting);
            Assert.True(cleric.Memorized[1][0].Ready);
            pc.LastSpell = null;
            Cast(); Tick(4);
            Assert.Equal(SpellBless, pc.LastSpell!.SpellId);
            Assert.False(cleric.Memorized[1][0].Ready);
        }
    }

    [DataFact]
    public void PlayerCastsAPreparedSpellFromItsClass()
    {
        // 255: a quickbar slot or right-click from the spellbook (METAMAGIC_ANY with the class); 0: a prepared slot (METAMAGIC_NONE).
        foreach (int meta in new[] { 255, 0 })
        {
            var (w, server, client, pc) = Login();
            Assert.Equal("Mage_Armor", w.Resources.TwoDA("spells").Get(SpellMageArmor, "Label"));
            var wiz = AddClass(pc, ClassWizard, 3);
            wiz.KnownSpells[1].Add(SpellMageArmor);
            Assert.Null(SpellbookRequests.Memorize(w, pc, ClassWizard, 1, 0, SpellMageArmor, 0));
            CreatureRules.RestoreSpells(w, pc);

            // Unlike a script naming a class with METAMAGIC_ANY, the player's own cast finds the slot.
            client.Send(new ClientRequestMessage { Kind = "cast-spell", Json = JsonSerializer.Serialize(new { spell = SpellMageArmor, @class = ClassWizard, meta, target = pc.Id }) }.Write());
            for (int i = 0; i < 30 * 4; i++) server.Tick(1f / 30);
            Assert.False(wiz.Memorized[1][0].Ready);
        }
    }

    [DataFact]
    public void CharacterSheetCarriesBreakdownsAndDescriptions()
    {
        var (w, _, _, pc) = Login();
        var sheet = JsonSerializer.SerializeToElement(CharacterRequests.Sheet(w, pc));
        Assert.True(sheet.GetProperty("xpLevel").GetInt32() <= sheet.GetProperty("xp").GetInt32());
        Assert.True(sheet.GetProperty("carryNormal").GetInt32() > 0);
        var skills = sheet.GetProperty("skills").EnumerateArray().ToList();
        Assert.Contains(skills, s => s.GetProperty("ability").GetString() == "DEX" && s.GetProperty("armourCheck").GetBoolean());
        Assert.All(skills, s => Assert.False(string.IsNullOrEmpty(s.GetProperty("description").GetString())));
        var feats = sheet.GetProperty("feats").EnumerateArray().ToList();
        Assert.NotEmpty(feats);
        Assert.All(feats, f => Assert.InRange(f.GetProperty("category").GetInt32(), 0, 10));
        var saves = sheet.GetProperty("baseSaves");
        Assert.True(saves.GetProperty("fortitude").GetInt32() >= 0);
    }
}
