using System.Text.Json;
using System.Text.Json.Nodes;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Rules;
using OpenWinter.Game.Scripting;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class CharacterSheetRefreshTests
{
    [DataFact]
    public void VersionCheckAllocatesSubstantiallyLessThanBuildingTheSheet()
    {
        var g = new Game();
        _ = CharacterSheetVersion.Read(g.World, g.Pc);
        _ = CharacterRequests.Sheet(g.World, g.Pc);
        long Measure(Action action)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 20; i++) action();
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }
        long check = Measure(() => CharacterSheetVersion.Read(g.World, g.Pc));
        long full = Measure(() => CharacterRequests.Sheet(g.World, g.Pc));
        Assert.True(check < full / 4, $"20 version checks allocated {check} bytes versus {full} for 20 full builds.");
    }

    sealed class Game
    {
        public readonly World World;
        public readonly Creature Pc;
        public readonly IConnection Client;
        public Game()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
            _ = new GameServer(World);
            Pc = World.Creatures.FromTemplate(new GffStruct());
            Pc.Classes.Add(new CreatureClass { Class = 4, Level = 1 });
            CreatureRules.EnsureInit(World, Pc);
            var pair = LoopbackConnection.CreatePair();
            Client = pair.Client;
            Pc.Player = new PlayerSession { Connection = pair.Server, Creature = Pc };
        }
        public JsonElement? Poll(string? version = null)
        {
            Assert.True(ClientRequests.Handle(World, Pc, "character", JsonSerializer.Serialize(new { version })));
            JsonElement? sheet = null;
            while (Client.TryReceive(out var bytes))
            {
                var reader = new MessageReader(bytes);
                if (reader.Type != MessageType.Notify) continue;
                var note = NotifyMessage.Read(reader);
                if (note.Kind == "character-sheet")
                {
                    Assert.Equal(Pc.Id, note.Subject);
                    Assert.Null(sheet);
                    sheet = JsonSerializer.Deserialize<JsonElement>(note.Json);
                }
            }
            return sheet;
        }
    }

    [DataFact]
    public void UnchangedConditionalRequestsAreSilentButExplicitOpensAlwaysGetASheet()
    {
        var g = new Game();
        var first = g.Poll()!.Value;
        string version = first.GetProperty("version").GetString()!;
        for (int i = 0; i < 10; i++) Assert.Null(g.Poll(version));
        // Walking and advancing time are not sheet inputs.
        g.Pc.Position = new(4, 5, 0);
        g.World.Step(.5f);
        Assert.Null(g.Poll(version));
        Assert.Equal(version, g.Poll()!.Value.GetProperty("version").GetString());
        Assert.NotNull(g.Poll("unknown-old-server-token"));
    }

    [DataFact]
    public void EffectsAndEquipmentRebuildTheLiveSheetAndTheirRemovalRevertsIt()
    {
        var g = new Game();
        var first = g.Poll()!.Value;
        string version = first.GetProperty("version").GetString()!;
        int strength = first.GetProperty("abilities")[0].GetProperty("score").GetInt32();
        var k = EffectConstants.Of(g.World);
        g.World.System<EffectSystem>().Apply(new Effect
        {
            Type = k["EFFECT_TYPE_ABILITY_INCREASE"], SubType = k.Magical, Ints = { [0] = 0, [1] = 4 },
        }, k.Temporary, g.Pc, .1f);
        var buffed = g.Poll(version)!.Value;
        Assert.Equal(strength + 4, buffed.GetProperty("abilities")[0].GetProperty("score").GetInt32());
        version = buffed.GetProperty("version").GetString()!;
        g.World.Step(.2f);
        var expired = g.Poll(version)!.Value;
        Assert.Equal(strength, expired.GetProperty("abilities")[0].GetProperty("score").GetInt32());
        version = expired.GetProperty("version").GetString()!;
        var armor = g.World.Items.FromBlueprint("nw_aarcl004")!;
        g.Pc.Equipment[EquipSlot.Chest] = armor;
        var equipped = g.Poll(version)!.Value;
        Assert.True(equipped.GetProperty("ac").GetInt32() > expired.GetProperty("ac").GetInt32());
        version = equipped.GetProperty("version").GetString()!;
        g.Pc.Equipment.Clear();
        Assert.Equal(expired.GetProperty("ac").GetInt32(), g.Poll(version)!.Value.GetProperty("ac").GetInt32());
    }

    [DataFact]
    public void DirectStatBiographyLevelItemAndGlobalLimitChangesInvalidateWithoutDirtyEvents()
    {
        var g = new Game();
        var item = g.World.Items.FromBlueprint("nw_wswls001")!;
        g.Pc.Inventory.Add(item);
        var property = new ItemProperty { Type = 6, CostTableValue = 1 };
        item.Properties.Add(property);
        var changes = new Action[]
        {
            () => g.Pc.CurrentHitPoints++,
            () => g.Pc.Abilities[0]++,
            () => g.Pc.Skills[0]++,
            () => g.Pc.Feats.Add(0),
            () => g.Pc.Classes[0].Level++,
            () => g.Pc.Experience++,
            () => g.Pc.Age++,
            () => g.Pc.Deity = "Changed deity",
            () => g.Pc.Description = "Changed description",
            () => g.Pc.PortraitId++,
            () => item.StackSize++,
            () => property.CostTableValue++,
            () => g.World.Limits.AbilityBonus++,
            () => g.Pc.ActionModes.Add(5),
        };
        foreach (var change in changes)
        {
            string version = g.Poll()!.Value.GetProperty("version").GetString()!;
            change();
            var changed = g.Poll(version)!.Value;
            string next = changed.GetProperty("version").GetString()!;
            Assert.NotEqual(version, next);
            Assert.Null(g.Poll(next));
        }
    }

    /// <summary>
    /// The invariant behind conditional polls: whenever the sheet payload (apart from its own
    /// version field) changes, the cheap fingerprint must change too, or the client keeps a stale sheet.
    /// </summary>
    [DataFact]
    public void EverySheetChangeAlsoChangesTheVersion()
    {
        var g = new Game();
        var w = g.World;
        var pc = g.Pc;
        var k = EffectConstants.Of(w);
        var effects = w.System<EffectSystem>();
        Item Blueprint(string resref) => w.Items.FromBlueprint(resref) ?? throw new InvalidOperationException(resref);
        var sword = Blueprint("nw_wswls001");
        var armor = Blueprint("nw_aarcl004");
        var loot = Blueprint("nw_wswls001");
        var property = new ItemProperty { Type = 6, CostTableValue = 1 };
        void Effect(string type, params int[] ints)
        {
            var e = new Effect { Type = k[type], SubType = k.Magical };
            for (int i = 0; i < ints.Length; i++) e.Ints[i] = ints[i];
            effects.Apply(e, k.Temporary, pc, 1000f);
        }
        var mutations = new (string Name, Action Apply)[]
        {
            ("strength", () => pc.Abilities[0] += 6),
            ("dexterity", () => pc.Abilities[1] += 4),
            ("constitution", () => pc.Abilities[2] += 4),
            ("intelligence", () => pc.Abilities[3] += 2),
            ("wisdom", () => pc.Abilities[4] += 2),
            ("charisma", () => pc.Abilities[5] += 2),
            ("skill ranks", () => { for (int i = 0; i < pc.Skills.Length; i++) pc.Skills[i] += 2; }),
            ("feat", () => pc.Feats.Add(0)),
            ("second feat", () => pc.Feats.Add(41)),
            ("current hp", () => pc.CurrentHitPoints -= 1),
            ("base hp", () => pc.BaseHitPoints += 5),
            ("max hp", () => pc.MaxHitPoints += 5),
            ("hp offset", () => pc.HitPointOffset += 3),
            ("class level", () => pc.Classes[0].Level++),
            ("new class", () => pc.Classes.Add(new CreatureClass { Class = 0, Level = 1 })),
            ("experience", () => pc.Experience += 5000),
            ("age", () => pc.Age++),
            ("gender", () => pc.Gender ^= 1),
            ("race", () => pc.Race = (byte)(pc.Race == 6 ? 0 : 6)),
            ("subrace", () => pc.Subrace = "Drow"),
            ("deity", () => pc.Deity = "Changed deity"),
            ("description", () => pc.Description = "Changed description"),
            ("name", () => pc.Name = new LocString("Renamed")),
            ("portrait", () => pc.Portrait = "po_hu_m_01_"),
            ("law/chaos", () => pc.LawfulChaotic = 10),
            ("good/evil", () => pc.GoodEvil = 90),
            ("natural ac", () => pc.NaturalAC += 2),
            ("size", () => pc.Size = 2),
            ("fortitude bonus", () => pc.FortBonus += 1),
            ("reflex bonus", () => pc.RefBonus += 1),
            ("will bonus", () => pc.WillBonus += 1),
            ("walk rate", () => pc.WalkRate += 1),
            ("run rate", () => pc.RunRate += 1),
            ("ability effect", () => Effect("EFFECT_TYPE_ABILITY_INCREASE", 1, 4)),
            ("ac effect", () => Effect("EFFECT_TYPE_AC_INCREASE", 0, 2)),
            ("skill effect", () => Effect("EFFECT_TYPE_SKILL_INCREASE", 0, 5)),
            ("save effect", () => Effect("EFFECT_TYPE_SAVING_THROW_INCREASE", 0, 2)),
            ("attack effect", () => Effect("EFFECT_TYPE_ATTACK_INCREASE", 2)),
            ("temporary hp effect", () => Effect("EFFECT_TYPE_TEMPORARY_HITPOINTS", 7)),
            ("spell resistance effect", () => Effect("EFFECT_TYPE_SPELL_RESISTANCE_INCREASE", 15)),
            ("time passes", () => w.Step(.5f)),
            ("equip weapon", () => pc.Equipment[EquipSlot.RightHand] = sword),
            ("equip armor", () => pc.Equipment[EquipSlot.Chest] = armor),
            ("weapon property", () => sword.Properties.Add(property)),
            ("property value", () => property.CostTableValue += 2),
            ("inventory weight", () => pc.Inventory.Add(loot)),
            ("stack size", () => loot.StackSize += 4),
            ("unequip weapon", () => pc.Equipment.Remove(EquipSlot.RightHand)),
            ("action mode", () => pc.ActionModes.Add(5)),
            ("global limit", () => w.Limits.AbilityBonus++),
            ("unequip armor", () => pc.Equipment.Clear()),
        };
        string Payload() =>
            JsonNode.Parse(JsonSerializer.Serialize(CharacterRequests.Sheet(w, pc)))!.AsObject() is var o && o.Remove("version") ? o.ToJsonString() : "";
        int observed = 0;
        foreach (var (name, apply) in mutations)
        {
            string sheet = Payload(), version = CharacterSheetVersion.Read(w, pc);
            apply();
            string nextSheet = Payload(), nextVersion = CharacterSheetVersion.Read(w, pc);
            if (nextSheet == sheet) continue;
            observed++;
            Assert.True(version != nextVersion, $"'{name}' changed the sheet but not its version.");
        }
        Assert.True(observed >= mutations.Length / 2, $"Only {observed} of {mutations.Length} mutations changed the sheet.");
    }
}
