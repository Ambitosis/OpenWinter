using System.Text.Json;
using OpenWinter.Game;
using OpenWinter.Game.Inventory;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Skills;
using OpenWinter.Net;
using Xunit;
using Xunit.Abstractions;

namespace OpenWinter.Tests;

/// <summary>The action bar and radial menu requests: abilities, use-feat, use-skill, use-item, action-mode, attack.</summary>
public class AbilityRequestTests
{
    readonly ITestOutputHelper _out;
    public AbilityRequestTests(ITestOutputHelper output) { _out = output; }

    sealed record Game(World World, GameServer Server, IConnection Client, Creature Pc)
    {
        public void Send(string kind, object? data = null)
        {
            Client.Send(new ClientRequestMessage { Kind = kind, Json = data == null ? "" : JsonSerializer.Serialize(data) }.Write());
            Server.Tick(1f / 30);
        }

        public void Tick(int frames) { for (int i = 0; i < frames; i++) Server.Tick(1f / 30); }

        public List<(string Kind, JsonElement Data)> Notifies()
        {
            var list = new List<(string, JsonElement)>();
            while (Client.TryReceive(out var m))
            {
                var r = new MessageReader(m);
                if (r.Type != MessageType.Notify) continue;
                var n = NotifyMessage.Read(r);
                list.Add((n.Kind, JsonDocument.Parse(n.Json).RootElement));
            }
            return list;
        }

        /// <summary>A commoner some metres from the player (or in another area).</summary>
        public Creature Other(bool elsewhere = false, float distance = 2)
        {
            var area = elsewhere ? World.Areas.First(a => a != Pc.Area) : Pc.Area!;
            var at = elsewhere ? area.Objects.First().Position : Pc.Position + new System.Numerics.Vector3(distance, 0, 0);
            var c = new Creature { Tag = "commoner", Faction = 2, Race = 6, CurrentHitPoints = 10, MaxHitPoints = 20, Name = new OpenWinter.Aurora.Formats.LocString("Commoner") };
            World.AddToArea(c, area, at, 0);
            OpenWinter.Game.Rules.CreatureRules.EnsureInit(World, c);
            return c;
        }
    }

    /// <summary>Logs in the module's barbarian (Barbarian Rage, no spells) in the town.</summary>
    static Game Login()
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
        return new Game(world, server, client, world.Get<Creature>(me)!);
    }

    const int FeatRage = 293, FeatTurnUndead = 294, FeatPowerAttack = 28, FeatExpertise = 389, FeatFlurry = 204;

    [DataFact]
    public void TrapTriggersAreDisarmedByIdAndTheirOutlineGoesOnlyToWhoSeesThem()
    {
        var g = Login();
        g.Pc.Skills[SkillIds.DisableTrap] = 4;
        var at = new Location(g.Pc.Area!.Id, g.Pc.Position + new System.Numerics.Vector3(6, 0, 0), 0);
        var trap = OpenWinter.Game.WorldObjects.TrapSystem.CreateAt(g.World, 0, at, 1f, "T", 1, "", "")!;
        g.Notifies();
        // Not seen yet: nothing sent. Seen: the polygon, its name and the radial's flags go to this player.
        OpenWinter.Game.WorldObjects.TrapSystem.SetDetected(g.World, trap, g.Pc, true);
        g.Tick(1);
        var shown = g.Notifies().Single(n => n.Kind == "trap-detected").Data;
        Assert.True(shown.GetProperty("detected").GetBoolean());
        Assert.False(shown.GetProperty("own").GetBoolean());
        Assert.Equal(4, shown.GetProperty("geometry").GetArrayLength());
        Assert.Equal("Spike Trap, Minor", shown.GetProperty("name").GetString());
        // The trap ring sends the trigger's id; the server takes it like any trapped object.
        g.Send("use-skill", new { skill = SkillIds.DisableTrap, sub = SkillIds.SubSkillExamineTrap, target = trap.Id });
        var action = Assert.Single(g.Pc.Actions.OfType<UseSkillAction>());
        Assert.Equal(trap.Id, action.Target);
        Assert.Equal(SkillIds.SubSkillExamineTrap, action.SubSkill);
    }

    [Fact]
    public void RequestKindsAreRegistered()
    {
        foreach (var kind in new[] { "abilities", "use-feat", "use-skill", "use-item", "item-info", "attack", "action-mode", "cancel-polymorph" })
            Assert.Contains(kind, ClientRequests.Kinds);
    }

    [DataFact]
    public void CancelPolymorphEndsAnUnlockedPolymorphAndGivesTheEquipmentBack()
    {
        var g = Login();
        var k = EffectConstants.Of(g.World);
        var fx = g.World.System<OpenWinter.Game.Rules.EffectSystem>();
        var worn = g.Pc.Equipment.ToList();
        Effect Poly(int locked)
        {
            var e = new Effect { Type = k["EFFECT_TYPE_POLYMORPH"], SubType = k.Magical, ScriptId = fx.NextId() };
            e.Ints[0] = 21; // brown bear
            e.Ints[1] = locked;
            fx.Apply(e, k.Permanent, g.Pc, 0);
            return e;
        }
        Poly(locked: 1);
        Assert.False(SelfFact(g, "canCancelPolymorph")); // the radial menu doesn't offer it
        g.Send("cancel-polymorph");
        g.Tick(30);
        Assert.NotNull(g.Pc.PolymorphForm); // a locked polymorph can't be cancelled
        fx.EndPolymorphNow(g.Pc);
        Assert.Equal(worn, g.Pc.Equipment.ToList());

        Poly(locked: 0);
        Assert.NotEqual(worn, g.Pc.Equipment.ToList());
        Assert.True(SelfFact(g, "canCancelPolymorph"));
        g.Send("cancel-polymorph");
        g.Tick(30);
        Assert.Null(g.Pc.PolymorphForm);
        Assert.Equal(worn, g.Pc.Equipment.ToList());
        Assert.False(SelfFact(g, "canCancelPolymorph"));
    }

    /// <summary>A fact the server gives the client about the player itself (its radial menu).</summary>
    static bool SelfFact(Game g, string name)
    {
        g.Notifies();
        g.Send("abilities", new { target = g.Pc.Id });
        var target = g.Notifies().Last(n => n.Kind == "abilities").Data.GetProperty("target");
        return target.GetProperty(name).GetBoolean();
    }

    [DataFact]
    public void ObjectTargetFactsCarryAuthoredAndScriptedPortraitsOnlyWithinTheArea()
    {
        var g = Login();
        var chest = new Placeable { Template = g.World.Resources.ReadGff("plc_chest1", OpenWinter.Aurora.Resources.ResType.Utp)! };
        g.World.AddToArea(chest, g.Pc.Area!, g.Pc.Position, 0);
        g.World.EnableScripts();
        var host = g.World.Scripts!;
        var thread = new ScriptThread(host.VM, new NcsProgram("portrait_probe",
            System.Text.Encoding.ASCII.GetBytes("NCS V1.0B\0\0\0\0").Concat(new byte[] { 0x20, 0 }).ToArray()), g.Pc.Id);
        object? Script(string name, params object?[] args) =>
            ((IScriptEngine)host).Call(thread, host.Definitions.ByName[name], args);
        string? Portrait(GameObject target)
        {
            g.Notifies();
            g.Send("abilities", new { target = target.Id });
            var facts = g.Notifies().Last(n => n.Kind == "abilities").Data.GetProperty("target");
            return facts.ValueKind == JsonValueKind.Null ? null : facts.GetProperty("portrait").GetString();
        }
        Assert.Equal("po_plc_a08_", Portrait(chest));
        chest.Template.SetResRef("Portrait", "custom_chest");
        Assert.Equal("custom_chest", Portrait(chest));
        chest.Template.SetResRef("Portrait", "").SetWord("PortraitId", 1);
        Assert.Equal("po_" + g.World.Resources.TwoDA("portraits").Get(1, "BaseResRef"), Portrait(chest));
        var door = new Door { Template = g.World.Resources.ReadGff("nw_door_normal", OpenWinter.Aurora.Resources.ResType.Utd)! };
        g.World.AddToArea(door, g.Pc.Area!, g.Pc.Position, 0);
        Assert.Equal("po_door01_", Portrait(door));
        Assert.Equal("po_door01_", Script("GetPortraitResRef", door.Id));
        Assert.Equal(0, Script("GetPortraitId", door.Id));
        door.Template.SetResRef("Portrait", "custom_door");
        Assert.Equal("custom_door", Portrait(door));
        Assert.Equal("custom_door", Script("GetPortraitResRef", door.Id));
        door.Template.SetResRef("Portrait", "").SetWord("PortraitId", 1);
        Assert.Equal("po_" + g.World.Resources.TwoDA("portraits").Get(1, "BaseResRef"), Portrait(door));
        Assert.Equal(Portrait(door), Script("GetPortraitResRef", door.Id));
        Assert.Equal(1, Script("GetPortraitId", door.Id));
        g.World.AddToArea(chest, g.World.Areas.First(a => a != g.Pc.Area), chest.Position, 0);
        Assert.Null(Portrait(chest));
    }

    [DataFact]
    public void ItemTargetIconsUseAppearanceAndViewerPaletteOnlyWithinTheArea()
    {
        var g = Login();
        string[]? Icons(Item item)
        {
            g.Notifies();
            g.Send("abilities", new { target = item.Id });
            var facts = g.Notifies().Last(n => n.Kind == "abilities").Data.GetProperty("target");
            if (facts.ValueKind == JsonValueKind.Null) return null;
            var (width, height) = g.World.Inventory.Footprint(item);
            Assert.Equal(item.BaseItem, facts.GetProperty("itemBase").GetInt32());
            Assert.Equal(item.StackSize, facts.GetProperty("itemStack").GetInt32());
            Assert.Equal(width, facts.GetProperty("itemWidth").GetInt32());
            Assert.Equal(height, facts.GetProperty("itemHeight").GetInt32());
            return facts.GetProperty("itemIcon").EnumerateArray().Select(x => x.GetString()!).ToArray();
        }
        var sword = g.World.Inventory.CreateOn("nw_wswls001", g.Pc, 1, "RADIAL_ICON")!;
        Assert.True(g.World.Inventory.Drop(sword, g.Pc.Location, g.Pc));
        Assert.Equal(g.World.Inventory.Icon(sword, g.Pc), Icons(sword));
        var before = Icons(sword);
        Assert.True(g.World.Inventory.SetAppearance(sword, InventorySystem.ApprWeaponColor, 0, 2));
        Assert.NotEqual(before, Icons(sword));
        Assert.Equal(g.World.Inventory.Icon(sword, g.Pc), Icons(sword));
        var armor = g.World.Inventory.CreateOn("nw_aarcl001", g.Pc, 1, "RADIAL_ARMOR")!;
        Assert.True(g.World.Inventory.Drop(armor, g.Pc.Location, g.Pc));
        Assert.Equal(g.World.Inventory.Icon(armor, g.Pc), Icons(armor));
        Assert.Contains(Icons(armor)!, x => x.StartsWith("iconplt:", StringComparison.Ordinal));
        var arrows = g.World.Inventory.CreateOn("nw_wamar001", g.Pc, 99, "RADIAL_ARROWS")!;
        Assert.True(g.World.Inventory.Drop(arrows, g.Pc.Location, g.Pc));
        Assert.Equal(g.World.Inventory.Icon(arrows, g.Pc), Icons(arrows));
        g.World.AddToArea(sword, g.World.Areas.First(a => a != g.Pc.Area), sword.Position, 0);
        Assert.Null(Icons(sword));
    }

    [DataFact]
    public void RadialLockCommandsUseTheRequestingPlayersKeyAndRejectInvalidContexts()
    {
        var g = Login();
        var chest = new Placeable { Locked = true, KeyRequired = 1, KeyName = "RADIAL_KEY" };
        chest.Lock.Lockable = true;
        // The player (a half-orc) stands at the chest's use point: within PERSPACE + 0.5 m of it (approach_use).
        g.World.AddToArea(chest, g.Pc.Area!, g.Pc.Position + new System.Numerics.Vector3(0, 0.9f, 0), 0);
        g.World.Inventory.CreateOn("nw_it_mpotion001", g.Pc, 1, "RADIAL_KEY");
        g.Send("lock-object", new { target = chest.Id, locked = false });
        g.Tick(2);
        Assert.False(chest.Locked);
        g.Send("lock-object", new { target = chest.Id, locked = true });
        g.Tick(2);
        Assert.True(chest.Locked);

        chest.Lock.Lockable = false;
        chest.Locked = false;
        g.Send("lock-object", new { target = chest.Id, locked = true });
        g.Tick(2);
        Assert.False(chest.Locked);
        chest.Lock.Lockable = true;
        g.Pc.IsDead = true;
        g.Send("lock-object", new { target = chest.Id, locked = true });
        Assert.False(chest.Locked);
        g.Pc.IsDead = false;
        g.World.AddToArea(chest, g.World.Areas.First(a => a != g.Pc.Area), chest.Position, 0);
        g.Send("lock-object", new { target = chest.Id, locked = true });
        g.Tick(2);
        Assert.False(chest.Locked);
        g.Send("lock-object", new { target = g.Pc.Id, locked = true });
        Assert.Empty(g.Pc.Actions);
    }

    [DataTheory]
    [InlineData(false, false, 4)]
    [InlineData(true, false, 4)]
    [InlineData(false, true, 4)]
    [InlineData(true, true, 5)]
    public void GroundEquipWaitsForPickupAndChoosesTheMeasuredHand(bool offhand, bool occupied, int expectedSlot)
    {
        var g = Login();
        foreach (var worn in g.Pc.Equipment.Values.ToArray()) g.World.Inventory.Unequip(g.Pc, worn);
        var held = g.World.Inventory.CreateOn("nw_wswdg001", g.Pc, 1, "HELD")!;
        if (occupied) Assert.True(g.World.Inventory.Equip(g.Pc, held, 4));
        var item = g.World.Inventory.CreateOn("nw_wswls001", g.Pc, 1, "GROUND")!;
        Assert.True(g.World.Inventory.Drop(item, g.Pc.Location, g.Pc));
        g.Send("equip-ground", new { item = item.Id, offhand });
        Assert.Null(g.World.Inventory.Owner(item));
        Assert.DoesNotContain(item, g.Pc.Equipment.Values);
        for (int frame = 0; frame < 55 && g.World.Inventory.Owner(item) == null; frame++) g.Tick(1);
        Assert.Same(g.Pc, g.World.Inventory.Owner(item));
        Assert.DoesNotContain(item, g.Pc.Equipment.Values); // native acquisition callback boundary
        g.Tick(1);
        Assert.Same(item, g.Pc.Equipment[EquipSlot.Bit(expectedSlot)]);
        if (occupied && offhand) Assert.Same(held, g.Pc.Equipment[EquipSlot.RightHand]);
        if (occupied && !offhand) Assert.Contains(held, g.Pc.Inventory);
    }

    [DataTheory]
    [InlineData(false, false, 4)]
    [InlineData(true, false, 4)]
    [InlineData(true, true, 5)]
    public void InventoryOffhandCommandUsesTheMeasuredHand(bool offhand, bool occupied, int expectedSlot)
    {
        var g = Login();
        foreach (var worn in g.Pc.Equipment.Values.ToArray()) g.World.Inventory.Unequip(g.Pc, worn);
        var held = g.World.Inventory.CreateOn("nw_wswdg001", g.Pc, 1, "HELD")!;
        if (occupied) Assert.True(g.World.Inventory.Equip(g.Pc, held, 4));
        var item = g.World.Inventory.CreateOn("nw_wswdg001", g.Pc, 1, "PACK")!;
        g.Send("equip", new { item = item.Id, offhand });
        g.Tick(2);
        Assert.Same(item, g.Pc.Equipment[EquipSlot.Bit(expectedSlot)]);
        if (occupied) Assert.Same(held, g.Pc.Equipment[EquipSlot.RightHand]);
    }

    [DataFact]
    public void InventoryOffhandCommandRejectsForeignItemsAndDoesNotChangeScriptSlots()
    {
        var g = Login();
        foreach (var worn in g.Pc.Equipment.Values.ToArray()) g.World.Inventory.Unequip(g.Pc, worn);
        var item = g.World.Inventory.CreateOn("nw_wswdg001", g.Other(), 1, "FOREIGN")!;
        g.Send("equip", new { item = item.Id, offhand = true });
        g.Tick(2);
        Assert.DoesNotContain(item, g.Pc.Equipment.Values);
        var own = g.World.Inventory.CreateOn("nw_wswdg001", g.Pc, 1, "OWN")!;
        Assert.Equal(ActionStatus.Done, new EquipItemAction { ItemId = own.Id, Slot = 5 }.Step(g.World, g.Pc, .1f));
        Assert.Same(own, g.Pc.Equipment[EquipSlot.LeftHand]);
        Assert.False(g.Pc.Equipment.ContainsKey(EquipSlot.RightHand));
    }

    [DataFact]
    public void GroundEquipRevalidatesOwnershipAfterAcquisitionCallbacks()
    {
        var g = Login();
        var item = g.World.Inventory.CreateOn("nw_wswls001", g.Pc, 1, "GROUND")!;
        Assert.True(g.World.Inventory.Drop(item, g.Pc.Location, g.Pc));
        var other = g.Other();
        g.Send("equip-ground", new { item = item.Id });
        for (int frame = 0; frame < 55 && g.World.Inventory.Owner(item) == null; frame++) g.Tick(1);
        Assert.Same(g.Pc, g.World.Inventory.Owner(item));
        Assert.DoesNotContain(item, g.Pc.Equipment.Values);
        // Acquisition events run in this queue before the next action step.
        g.World.After(0, () => g.World.Inventory.MoveTo(item, other));
        g.Tick(2);
        Assert.Same(other, g.World.Inventory.Owner(item));
        Assert.DoesNotContain(item, g.Pc.Equipment.Values);
        Assert.Empty(g.Pc.Actions.OfType<PickUpAndEquipAction>());
    }

    [DataFact]
    public void GroundEquipRetainsTheSurvivingStackWhenPickupMergesAmmunition()
    {
        var g = Login();
        var arrows = g.World.Inventory.CreateOn("nw_wamar001", g.Pc, 10, "MERGE")!;
        var ground = g.World.Inventory.CreateOn("nw_wamar001", g.Pc, 5, "SEPARATE")!;
        Assert.True(g.World.Inventory.Drop(ground, g.Pc.Location, g.Pc));
        ground.Tag = arrows.Tag;
        g.Send("equip-ground", new { item = ground.Id });
        g.Tick(55);
        Assert.True(ground.IsDestroyed);
        Assert.Equal(15, arrows.StackSize);
        Assert.Contains(arrows, g.Pc.Equipment.Values);
    }

    [DataFact]
    public void GroundEquipCancellationAndInvalidRequestsDoNotTransferItems()
    {
        var g = Login();
        var item = g.World.Inventory.CreateOn("nw_wswls001", g.Pc, 1, "GROUND")!;
        Assert.True(g.World.Inventory.Drop(item, g.Pc.Location, g.Pc));
        g.Send("equip-ground", new { item = item.Id });
        g.Tick(5);
        ActionQueues.Clear(g.World, g.Pc);
        g.Tick(60);
        Assert.Null(g.World.Inventory.Owner(item));
        Assert.Same(g.Pc.Area, item.Area);

        g.Pc.IsDead = true;
        g.Send("equip-ground", new { item = item.Id });
        Assert.Empty(g.Pc.Actions);
        g.Pc.IsDead = false;
        g.Pc.Commandable = false;
        g.Send("equip-ground", new { item = item.Id });
        Assert.Empty(g.Pc.Actions);
        g.Pc.Commandable = true;
        var other = g.Other();
        Assert.Same(item, g.World.Inventory.MoveTo(item, other));
        g.Send("equip-ground", new { item = item.Id });
        Assert.Empty(g.Pc.Actions);
        Assert.Same(other, g.World.Inventory.Owner(item));
        Assert.True(g.World.Inventory.Drop(item, new Location(g.World.Areas.First(a => a != g.Pc.Area).Id, g.Pc.Position, 0), other));
        g.Send("equip-ground", new { item = item.Id });
        Assert.Empty(g.Pc.Actions);
        var potion = g.World.Inventory.CreateOn("nw_it_mpotion001", g.Pc, 1, "POTION")!;
        Assert.True(g.World.Inventory.Drop(potion, g.Pc.Location, g.Pc));
        g.Send("equip-ground", new { item = potion.Id, offhand = true });
        Assert.Empty(g.Pc.Actions);
        Assert.Null(g.World.Inventory.Owner(potion));
    }

    [DataFact]
    public void TrapSubchoicesExposeTheirMeasuredNativeIcons()
    {
        var g = Login();
        g.Pc.Skills[SkillIds.DisableTrap] = 5;
        g.Notifies();
        g.Send("abilities");
        var abilities = g.Notifies().Last(n => n.Kind == "abilities").Data;
        var skill = abilities.GetProperty("skills").EnumerateArray().Single(s => s.GetProperty("skill").GetInt32() == SkillIds.DisableTrap);
        var icons = skill.GetProperty("subs").EnumerateArray().ToDictionary(s => s.GetProperty("id").GetInt32(), s => s.GetProperty("icon").GetString());
        Assert.Equal("ir_dis_trap", icons[0]);
        Assert.Equal("ir_examine", icons[SkillIds.SubSkillExamineTrap]);
        Assert.Equal("ir_flag_trap", icons[SkillIds.SubSkillFlagTrap]);
        Assert.Equal("ir_rec_trap", icons[SkillIds.SubSkillRecoverTrap]);
    }

    [DataFact]
    public void AbilitiesListFeatsModesSkillsAndItemsWithNames()
    {
        var g = Login();
        var potion = g.World.Inventory.CreateOn("nw_wswls001", g.Pc, 1, "")!;
        potion.Properties.Add(new ItemProperty { Type = 15, SubType = 329, CostTable = 3, CostTableValue = 9, DurationType = 2 }); // 2 uses/day
        g.Notifies();
        g.Send("abilities");
        var data = g.Notifies().Last(n => n.Kind == "abilities").Data;
        _out.WriteLine(data.ToString()[..Math.Min(3000, data.ToString().Length)]);

        var rage = data.GetProperty("feats").EnumerateArray().Single(f => f.GetProperty("feat").GetInt32() == FeatRage);
        Assert.False(string.IsNullOrWhiteSpace(rage.GetProperty("name").GetString()));
        Assert.True(rage.GetProperty("uses").GetInt32() > 0);
        Assert.Equal(AbilityRequests.TargetSelf, rage.GetProperty("targetType").GetInt32() & AbilityRequests.TargetSelf);
        // A successor feat (Barbarian Rage 2..7 after Barbarian Rage) never shows separately from the feat it follows.
        var featTable = g.World.Resources.TwoDA("feat");
        var listed = data.GetProperty("feats").EnumerateArray().Select(f => f.GetProperty("feat").GetInt32()).ToList();
        Assert.DoesNotContain(listed, f => listed.Any(other => featTable.GetInt(other, "SUCCESSOR", -1) == f));

        var modes = data.GetProperty("modes").EnumerateArray().ToList();
        Assert.Contains(modes, m => m.GetProperty("mode").GetInt32() == AbilityRequests.ModeDetect);
        Assert.Contains(modes, m => m.GetProperty("mode").GetInt32() == AbilityRequests.ModeStealth);
        Assert.All(modes, m => Assert.False(m.GetProperty("on").GetBoolean()));

        var skills = data.GetProperty("skills").EnumerateArray().ToList();
        Assert.NotEmpty(skills);
        Assert.All(skills, s => Assert.False(string.IsNullOrWhiteSpace(s.GetProperty("name").GetString())));

        var item = data.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("item").GetUInt32() == potion.Id);
        var prop = item.GetProperty("props").EnumerateArray().Single();
        Assert.Equal(potion.Properties.Count - 1, prop.GetProperty("index").GetInt32());
        Assert.Equal(2, prop.GetProperty("uses").GetInt32());
        Assert.NotEmpty(item.GetProperty("icon").EnumerateArray());
    }

    [DataFact]
    public void AbilitiesDescribeTheTargetForTheRadialMenu()
    {
        var g = Login();
        var other = g.Other();
        g.Notifies();
        g.Send("abilities", new { target = other.Id });
        var target = g.Notifies().Last(n => n.Kind == "abilities").Data.GetProperty("target");
        Assert.Equal(other.Id, target.GetProperty("id").GetUInt32());
        Assert.False(target.GetProperty("self").GetBoolean());
        Assert.False(target.GetProperty("trapped").GetBoolean());
    }

    [DataFact]
    public void UseFeatSpendsAUseAndRejectsFeatsThePlayerLacks()
    {
        var g = Login();
        int before = OpenWinter.Game.Rules.CreatureRules.RemainingFeatUses(g.World, g.Pc, FeatRage);
        Assert.True(before > 0);
        // A feat the barbarian doesn't have, and a sub-radial spell the feat doesn't offer, are refused.
        g.Send("use-feat", new { feat = FeatTurnUndead });
        Assert.DoesNotContain(g.Pc.Actions, a => a is OpenWinter.Game.Scripting.Functions.UseFeatAction);
        g.Send("use-feat", new { feat = FeatRage, sub = 100 });
        Assert.DoesNotContain(g.Pc.Actions, a => a is OpenWinter.Game.Scripting.Functions.UseFeatAction);

        g.Send("use-feat", new { feat = FeatRage });
        g.Tick(90);
        Assert.Equal(before - 1, OpenWinter.Game.Rules.CreatureRules.RemainingFeatUses(g.World, g.Pc, FeatRage));

        // With no uses left nothing is queued.
        OpenWinter.Game.Rules.CreatureRules.AdjustFeatUses(g.World, g.Pc, FeatRage, -100);
        g.Pc.Actions.Clear();
        g.Send("use-feat", new { feat = FeatRage });
        Assert.DoesNotContain(g.Pc.Actions, a => a is OpenWinter.Game.Scripting.Functions.UseFeatAction);
    }

    [DataFact]
    public void ModesNeedTheirFeatAndCombatModesExcludeEachOther()
    {
        var g = Login();
        g.Pc.Feats.Remove(FeatFlurry);
        g.Send("action-mode", new { mode = AbilityRequests.ModeFlurry, on = true });
        Assert.DoesNotContain(AbilityRequests.ModeFlurry, g.Pc.ActionModes);

        g.Pc.Feats.Add(FeatPowerAttack);
        g.Pc.Feats.Add(FeatExpertise);
        g.Send("action-mode", new { mode = AbilityRequests.ModePowerAttack, on = true });
        Assert.Contains(AbilityRequests.ModePowerAttack, g.Pc.ActionModes);
        // Using a mode feat toggles its mode; turning on expertise turns power attack off.
        g.Send("use-feat", new { feat = FeatExpertise });
        Assert.Contains(AbilityRequests.ModeExpertise, g.Pc.ActionModes);
        Assert.DoesNotContain(AbilityRequests.ModePowerAttack, g.Pc.ActionModes);
        var notified = g.Notifies().Where(n => n.Kind == "action-mode").Select(n => (n.Data.GetProperty("mode").GetInt32(), n.Data.GetProperty("on").GetBoolean())).ToList();
        Assert.Contains((AbilityRequests.ModePowerAttack, false), notified);
        Assert.Contains((AbilityRequests.ModeExpertise, true), notified);

        // Stealth and detect still work through the same request.
        g.Send("action-mode", new { mode = AbilityRequests.ModeDetect, on = true });
        Assert.True(g.Pc.DetectMode);
    }

    [DataFact]
    public void DefensiveCastingFeedbackNamesTheModeAndLeavesDetectAlone()
    {
        var g = Login();
        g.Pc.Classes.Add(new CreatureClass { Class = 10, Level = 1 });
        g.Send("action-mode", new { mode = AbilityRequests.ModeDefensiveCast, on = true });
        Assert.Contains(AbilityRequests.ModeDefensiveCast, g.Pc.ActionModes);
        Assert.False(g.Pc.DetectMode);
        var enabled = Assert.Single(g.Notifies(), n => n.Kind == "action-mode").Data;
        Assert.Equal(10, enabled.GetProperty("mode").GetInt32());
        Assert.Equal("Defensive Casting", enabled.GetProperty("name").GetString());
        Assert.Equal("Defensive Casting Mode Activated", enabled.GetProperty("feedback").GetString());
        Assert.True(enabled.GetProperty("on").GetBoolean());

        g.Send("action-mode", new { mode = AbilityRequests.ModeDetect, on = true });
        g.Notifies();
        g.Send("action-mode", new { mode = AbilityRequests.ModeDefensiveCast, on = false });
        Assert.True(g.Pc.DetectMode);
        Assert.DoesNotContain(AbilityRequests.ModeDefensiveCast, g.Pc.ActionModes);
        var disabled = Assert.Single(g.Notifies(), n => n.Kind == "action-mode").Data;
        Assert.Equal("Defensive Casting", disabled.GetProperty("name").GetString());
        Assert.Equal("Defensive Casting Mode Deactivated", disabled.GetProperty("feedback").GetString());
        Assert.False(disabled.GetProperty("on").GetBoolean());

        g.Send("action-mode", new { mode = 10, on = true });
        g.Notifies();
        g.Pc.Feats.Add(FeatPowerAttack);
        g.Send("action-mode", new { mode = 3, on = true });
        Assert.DoesNotContain(10, g.Pc.ActionModes);
        Assert.Contains(g.Notifies(), n => n.Kind == "action-mode" && n.Data.GetProperty("mode").GetInt32() == 10
            && n.Data.GetProperty("feedback").GetString() == "Defensive Casting Mode Deactivated");
        g.Send("action-mode", new { mode = 10, on = true });
        Assert.DoesNotContain(3, g.Pc.ActionModes);
        Assert.Equal(new[] { 10, 3 }, g.Notifies().Where(n => n.Kind == "action-mode")
            .Select(n => n.Data.GetProperty("mode").GetInt32()).ToArray());
        Assert.True(g.Pc.DetectMode); // Search is independent of combat modes.
    }

    [DataFact]
    public void KeenSenseProvidesDetectWithoutAToggleOrWalkingPenalty()
    {
        var g = Login();
        g.Pc.Feats.Add(SkillIds.FeatKeenSense);
        Assert.True(SkillSystem.ActiveDetect(g.World, g.Pc));
        g.Notifies();
        foreach (bool on in new[] { true, false }) g.Send("action-mode", new { mode = 0, on });
        Assert.False(g.Pc.DetectMode);
        Assert.False(SkillSystem.ForcesWalk(g.World, g.Pc));
        Assert.DoesNotContain(g.Notifies(), n => n.Kind == "action-mode");
        g.Send("abilities");
        var modes = g.Notifies().Single(n => n.Kind == "abilities").Data.GetProperty("modes");
        Assert.Contains(modes.EnumerateArray(), m => m.GetProperty("mode").GetInt32() == 0);
        // Gaining the feat while manual Detect is selected must also allow running.
        g.Pc.DetectMode = true;
        Assert.False(SkillSystem.ForcesWalk(g.World, g.Pc));
        g.Pc.StealthMode = true;
        Assert.True(SkillSystem.ForcesWalk(g.World, g.Pc));
    }

    [DataFact]
    public void ParryCanBeSelectedAndActivatedWithoutTrainedRanks()
    {
        // The native fixture uses stock skills. Module overrides may make Parry trained-only.
        var resources = new OpenWinter.Aurora.Resources.ResMan(TestData.Paths);
        resources.AddBaseGame(includeUserOverride: false);
        var world = new World(resources);
        var pc = new Creature { Race = 6, CurrentHitPoints = 10, MaxHitPoints = 10 };
        world.Register(pc);
        OpenWinter.Game.Rules.CreatureRules.EnsureInit(world, pc);
        Assert.Equal(0, pc.Skills[SkillIds.Parry]);
        Assert.True(AbilityRequests.SetMode(world, pc, AbilityRequests.ModeParry, true));
        Assert.Contains(AbilityRequests.ModeParry, pc.ActionModes);
        Assert.True(AbilityRequests.SetMode(world, pc, AbilityRequests.ModeParry, false));
        Assert.DoesNotContain(AbilityRequests.ModeParry, pc.ActionModes);
    }

    [DataFact]
    public void EveryToggleUsesItsOwnLocalizedOverheadFeedbackWithoutRepeatingUnchangedState()
    {
        var g = Login();
        g.Pc.Feats.Remove(SkillIds.FeatKeenSense);
        g.Pc.Classes.Add(new CreatureClass { Class = 10, Level = 1 });
        foreach (int feat in new[] { 28, 19, 204, 30, 389, 390, 425 }) g.Pc.Feats.Add(feat);
        g.Pc.Skills[SkillIds.Parry] = 1;
        foreach (int mode in new[] { 0, 1, 2, 3, 4, 6, 7, 8, 9, 10, 11 })
        {
            g.Notifies();
            Assert.True(AbilityRequests.SetMode(g.World, g.Pc, mode, true), $"Mode {mode} should be available");
            var message = Assert.Single(g.Notifies(), n => n.Kind == "action-mode").Data;
            Assert.Contains("Activated", message.GetProperty("feedback").GetString());
            Assert.True(AbilityRequests.SetMode(g.World, g.Pc, mode, true));
            Assert.DoesNotContain(g.Notifies(), n => n.Kind == "action-mode");
            Assert.True(AbilityRequests.SetMode(g.World, g.Pc, mode, false));
            message = Assert.Single(g.Notifies(), n => n.Kind == "action-mode").Data;
            Assert.Contains("Deactivated", message.GetProperty("feedback").GetString());
        }
    }

    [DataFact]
    public void TheFeedPushesChangedAbilities()
    {
        var g = Login();
        g.Pc.Feats.Add(FeatPowerAttack);
        g.Send("abilities");
        g.Tick(40);
        g.Notifies();
        g.Send("action-mode", new { mode = AbilityRequests.ModePowerAttack, on = true });
        g.Tick(40);
        var pushed = g.Notifies().Where(n => n.Kind == "abilities").ToList();
        Assert.NotEmpty(pushed);
        Assert.Contains(pushed.Last().Data.GetProperty("modes").EnumerateArray(),
            m => m.GetProperty("mode").GetInt32() == AbilityRequests.ModePowerAttack && m.GetProperty("on").GetBoolean());
    }

    [DataFact]
    public void UseSkillChecksTheTargetAndQueuesTheSkill()
    {
        var g = Login();
        var other = g.Other(distance: 8); // far enough that the player is still walking over after one tick
        g.Send("use-skill", new { skill = SkillIds.Taunt, target = other.Id });
        var action = Assert.Single(g.Pc.Actions.OfType<UseSkillAction>());
        Assert.Equal(SkillIds.Taunt, action.Skill);
        Assert.Equal(other.Id, action.Target);

        // Not on a creature in another area, not with a made-up sub-skill, not a passive skill.
        g.Pc.Actions.Clear();
        var far = g.Other(elsewhere: true);
        g.Send("use-skill", new { skill = SkillIds.Taunt, target = far.Id });
        g.Send("use-skill", new { skill = SkillIds.DisableTrap, sub = 7, target = other.Id });
        g.Send("use-skill", new { skill = SkillIds.Concentration, target = other.Id });
        Assert.Empty(g.Pc.Actions.OfType<UseSkillAction>());

        // Search toggles detect mode.
        g.Send("use-skill", new { skill = SkillIds.Search });
        Assert.True(g.Pc.DetectMode);
    }

    [DataFact]
    public void UseItemNeedsTheItemAndAUseLeft()
    {
        var g = Login();
        var inv = g.World.Inventory;
        var wand = inv.CreateOn("nw_wswls001", g.Pc, 1, "")!;
        // This authored spell item is a longsword, so its power requires it to be equipped.
        Assert.True(inv.Equip(g.Pc, wand, 4));
        var daily = new ItemProperty { Type = 15, SubType = 329, CostTable = 3, CostTableValue = 8, DurationType = 2 }; // 1 use/day
        wand.Properties.Add(daily);
        int index = wand.Properties.Count - 1;

        g.Send("use-item", new { item = wand.Id, prop = index });
        var use = Assert.Single(g.Pc.Actions.OfType<UseItemAction>());
        Assert.Same(daily, use.Property);
        Assert.Equal(g.Pc.Id, use.Target);
        g.Tick(90);
        Assert.Equal(0, daily.UsesPerDay);

        // No uses left, a property that isn't activatable, somebody else's item: nothing happens.
        g.Pc.Actions.Clear();
        g.Send("use-item", new { item = wand.Id, prop = index });
        g.Send("use-item", new { item = wand.Id, prop = 99 });
        var other = g.World.AllObjects.OfType<Item>().First(i => inv.Owner(i) != g.Pc && i.Properties.Any(p => p.Type == 15));
        g.Send("use-item", new { item = other.Id, prop = other.Properties.FindIndex(p => p.Type == 15) });
        Assert.Empty(g.Pc.Actions.OfType<UseItemAction>());
    }

    [DataFact]
    public void ItemInfoOnlyForThePlayersOwnItems()
    {
        var g = Login();
        var sword = g.World.Inventory.CreateOn("nw_wswls001", g.Pc, 1, "")!;
        var other = g.World.AllObjects.OfType<Item>().First(i => g.World.Inventory.Owner(i) != g.Pc);
        g.Notifies();
        g.Send("item-info", new { item = other.Id });
        Assert.DoesNotContain(g.Notifies(), n => n.Kind == "item-info");
        g.Send("item-info", new { item = sword.Id });
        var info = g.Notifies().Single(n => n.Kind == "item-info").Data;
        Assert.Equal(sword.Id, info.GetProperty("item").GetUInt32());
        Assert.False(string.IsNullOrEmpty(info.GetProperty("name").GetString()));
        Assert.True(info.GetProperty("equippable").GetBoolean());
    }

    [DataFact]
    public void AttackOnlyInTheSameArea()
    {
        var g = Login();
        var far = g.Other(elsewhere: true);
        g.Send("attack", new { target = far.Id });
        Assert.NotEqual(far.Id, g.World.System<OpenWinter.Game.Combat.CombatSystem>().TargetOf(g.Pc));
        var other = g.Other();
        g.Send("attack", new { target = other.Id });
        Assert.Equal(other.Id, g.World.System<OpenWinter.Game.Combat.CombatSystem>().TargetOf(g.Pc));
    }

    [DataFact]
    public void PlayersNextOrderEndsTheirAttack()
    {
        // Found by the playtest autopilot (Prelude tutorial): after punching Bim the player kept swinging at him
        // through conversations and walks elsewhere, and could never rest ("not while in combat").
        var g = Login();
        var combat = g.World.System<OpenWinter.Game.Combat.CombatSystem>();
        var other = g.Other();
        other.Plot = true; // takes the blows without dying (the barbarian kills a commoner in one)
        g.Send("attack", new { target = other.Id });
        g.Tick(3);
        Assert.Equal(other.Id, combat.TargetOf(g.Pc));
        g.Client.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = g.Pc.Position + new System.Numerics.Vector3(-3, 0, 0) }.Write());
        g.Tick(2);
        Assert.Equal(GameObject.InvalidId, combat.TargetOf(g.Pc));
        Assert.False(combat.IsInCombat(g.Pc));

        // And a script's ClearAllActions on the player ends it too.
        g.Send("attack", new { target = other.Id });
        g.Tick(3);
        Assert.Equal(other.Id, combat.TargetOf(g.Pc));
        ActionQueues.Clear(g.World, g.Pc);
        Assert.Equal(GameObject.InvalidId, combat.TargetOf(g.Pc));

        // Left-clicking a hostile creature attacks it the same way.
        var foe = g.Other(distance: 2.5f);
        foe.Faction = OpenWinter.Game.FactionTable.Hostile;
        foe.Plot = true;
        g.Client.Send(new ActionRequestMessage { Action = ActionKind.Talk, Target = foe.Id }.Write());
        g.Tick(3);
        Assert.Equal(foe.Id, combat.TargetOf(g.Pc));
        g.Client.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = g.Pc.Position + new System.Numerics.Vector3(-3, 0, 0) }.Write());
        g.Tick(2);
        Assert.Equal(GameObject.InvalidId, combat.TargetOf(g.Pc));
    }
}
