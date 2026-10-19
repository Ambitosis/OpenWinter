using System.Numerics;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;
using OpenWinter.Game.SaveGames;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// How long corpses last and what they drop, as the original did in nwserver corpse_drops and the nwmain scenes
/// pavelroom, pavelarrive and pcdropflag (docs/DEATH_AND_DYING.md, "Corpses and what they drop").
/// </summary>
public sealed class CorpseDropTests
{
    sealed class Game
    {
        public readonly World World;
        public readonly Area Area;

        public Game()
        {
            var resources = new ResMan(TestData.Paths);
            resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources);
            World.Register(World.Module);
            World.Factions.Load(resources);
            Area = new Area { ResRef = "corpses", Width = 8, Height = 8 };
            World.Register(Area); World.Areas.Add(Area);
        }

        /// <summary>A creature from a UTC-shaped struct (a blueprint, or an area instance with embedded items).</summary>
        public Creature Spawn(GffStruct utc, float x)
        {
            var c = World.Creatures.FromTemplate(utc);
            c.Classes.Add(new CreatureClass { Class = 4, Level = 1 });
            World.AddToArea(c, Area, new Vector3(x, 20, 0), 0, exact: true);
            CreatureRules.EnsureInit(World, c);
            c.MaxHitPoints = c.CurrentHitPoints = 10;
            return c;
        }

        public void Run(float seconds)
        {
            for (float t = 0; t < seconds; t += 0.25f) World.Step(0.25f);
        }

        public List<Placeable> BagsNear(Vector3 at) => Area.Objects.OfType<Placeable>()
            .Where(p => p.Tag == CorpseSystem.BodyBagTag && Vector3.Distance(p.Position, at) < 1.5f).ToList();
    }

    static GffStruct Ref(uint type, string label, string resref, int? dropable = null, int? pick = null)
    {
        var s = new GffStruct(type).SetResRef(label, resref);
        if (dropable is int d) s.SetByte("Dropable", d);
        if (pick is int p) s.SetByte("Pickpocketable", p);
        return s;
    }

    [Fact]
    public void ACorpseLastsItsDecayTimeButAtLeastFiveSeconds()
    {
        Assert.Equal(5, CorpseSystem.Lifetime(new Creature { DecayTime = 0 }));
        Assert.Equal(5, CorpseSystem.Lifetime(new Creature { DecayTime = 1000 }));
        Assert.Equal(5, CorpseSystem.Lifetime(new Creature { DecayTime = 5000 }));
        Assert.Equal(20, CorpseSystem.Lifetime(new Creature { DecayTime = 20000 }));
        Assert.Equal(3, CorpseSystem.Lifetime(new Creature { DecayTime = 0, SpectacularDeath = true }));
    }

    [Fact]
    public void ACreatureSavedAtMinusElevenLoadsAsACorpse()
    {
        var corpse = new Creature { CurrentHitPoints = -11 };
        corpse.Actions.Add(new WaitAction { Remaining = 1 });
        NativeSaveGame.RestoreDeath(corpse);
        Assert.True(corpse.IsDead);
        Assert.Empty(corpse.Actions);
        var dying = new Creature { CurrentHitPoints = -10 };
        NativeSaveGame.RestoreDeath(dying);
        Assert.False(dying.IsDead);
    }

    [DataFact]
    public void BlueprintItemsDropOnlyWhenTheToolsetSaysSo()
    {
        var g = new Game();
        var utc = new GffStruct()
            .SetList("Equip_ItemList", [Ref(0x2, "EquippedRes", "nw_aarcl009"), Ref(0x10, "EquippedRes", "nw_wswls001", dropable: 1)])
            .SetList("ItemList", [Ref(0, "InventoryRes", "nw_it_mpotion001"), Ref(1, "InventoryRes", "nw_it_mpotion002", dropable: 1, pick: 1)]);
        var thug = g.Spawn(utc, 10);
        var armour = thug.Equipment[0x2];
        var sword = thug.Equipment[0x10];
        var plain = thug.Inventory.Single(i => i.ResRef == "nw_it_mpotion001");
        var ticked = thug.Inventory.Single(i => i.ResRef == "nw_it_mpotion002");
        Assert.False(armour.Droppable); Assert.False(armour.Pickpocketable);
        Assert.True(sword.Droppable); Assert.False(sword.Pickpocketable);
        Assert.False(plain.Droppable);
        Assert.True(ticked.Droppable); Assert.True(ticked.Pickpocketable);

        // An area instance carries its items whole; without the fields they are neither.
        var embedded = g.World.Resources.ReadGff("nw_wblcl001", ResType.Uti)!;
        embedded.StructType = 0x10;
        var placed = g.Spawn(new GffStruct().SetList("Equip_ItemList", [embedded]), 30);
        Assert.False(placed.Equipment[0x10].Droppable);

        var at = thug.Position;
        HitPoints.Kill(g.World, thug, GameObject.InvalidId);
        g.Run(4.5f);
        Assert.False(thug.IsDestroyed);
        g.Run(1.5f);
        Assert.True(thug.IsDestroyed);
        var bag = Assert.Single(g.BagsNear(at));
        Assert.Equal(new[] { "nw_it_mpotion002", "nw_wswls001" }, g.World.Inventory.Items(bag).Select(i => i.ResRef).Order());

        // Nothing droppable: the corpse goes and leaves no bag.
        var placedAt = placed.Position;
        HitPoints.Kill(g.World, placed, GameObject.InvalidId);
        g.Run(6);
        Assert.True(placed.IsDestroyed);
        Assert.Empty(g.BagsNear(placedAt));
    }

    [DataFact]
    public void ABlowToMinusElevenEndsTheBodyInThreeSeconds()
    {
        var g = new Game();
        var just = g.Spawn(new GffStruct(), 10);
        var over = g.Spawn(new GffStruct(), 20);
        just.CurrentHitPoints = -10;
        HitPoints.CheckDeath(g.World, just, GameObject.InvalidId, 10);
        over.CurrentHitPoints = -11;
        HitPoints.CheckDeath(g.World, over, GameObject.InvalidId, 10);
        Assert.True(just.IsDead && over.IsDead);
        g.Run(3.5f);
        Assert.True(over.IsDestroyed);
        Assert.False(just.IsDestroyed);
        g.Run(2.5f);
        Assert.True(just.IsDestroyed);
    }

    [DataFact]
    public void ScriptsAndPlayersSetTheFlags()
    {
        var g = new Game();
        var npc = g.Spawn(new GffStruct(), 10);
        var created = g.World.Inventory.CreateOn("nw_it_mpotion001", npc, 1, "")!;
        Assert.True(created.Droppable); Assert.False(created.Pickpocketable);
        var ground = g.World.Items.FromBlueprint("nw_it_gem001")!;
        Assert.False(ground.Droppable); Assert.False(ground.Pickpocketable);
        g.World.Register(ground);
        g.World.AddToArea(ground, g.Area, new Vector3(12, 20, 0), 0);
        var copy = g.World.Inventory.CopyItem(ground, npc, false)!;
        Assert.True(copy.Droppable); Assert.True(copy.Pickpocketable);

        // A creature that isn't a player keeps the flags; a player taking the item makes it both.
        var other = g.Spawn(new GffStruct(), 14);
        created.Droppable = false;
        g.World.Inventory.MoveTo(created, other);
        Assert.False(created.Droppable);
        var pc = g.Spawn(new GffStruct(), 16);
        pc.Player = new PlayerSession { Connection = LoopbackConnection.CreatePair().Server, Creature = pc };
        g.World.Inventory.MoveTo(ground, pc);
        Assert.True(ground.Droppable); Assert.True(ground.Pickpocketable);
        ground.Droppable = false;
        g.World.Inventory.MoveTo(ground, pc);
        Assert.False(ground.Droppable);
    }

    [DataFact]
    public void SavesWriteBothFlagsAndOlderPlayerFilesStayDroppable()
    {
        var g = new Game();
        var holder = g.Spawn(new GffStruct(), 12);
        holder.Player = new PlayerSession { Connection = LoopbackConnection.CreatePair().Server, Creature = holder };
        var item = g.World.Inventory.CreateOn("nw_it_mpotion001", holder, 1, "")!;
        item.Droppable = true; item.Pickpocketable = false;
        var saved = OpenWinter.Game.Persistence.ObjectSerializer.ItemGff(g.World, item, 0);
        Assert.Equal(1, saved.GetInt("Dropable", -1));
        Assert.Equal(0, saved.GetInt("Pickpocketable", -1));
        item.Droppable = false;
        var undroppable = OpenWinter.Game.Persistence.ObjectSerializer.ItemGff(g.World, item, 0);
        Assert.Equal(0, undroppable.GetInt("Dropable", -1));

        // A player's item without the flags (an OpenWinter file from before they were always written) is droppable;
        // one saved undroppable stays so, at login and when a save game loads.
        var pc = g.Spawn(new GffStruct().SetList("ItemList", [Ref(0, "InventoryRes", "nw_it_gem001"), undroppable]), 16);
        pc.Player = new PlayerSession { Connection = LoopbackConnection.CreatePair().Server, Creature = pc };
        var items = g.World.Inventory.Items(pc);
        Assert.All(items, i => Assert.False(i.Droppable));
        g.World.Inventory.KeepPlayerItemFlags(pc);
        Assert.True(items.Single(i => i.ResRef == "nw_it_gem001").Droppable);
        Assert.False(items.Single(i => i.ResRef == "nw_it_mpotion001").Droppable);
    }

    [DataFact]
    public void ALootableBodyWithNothingToDropDecays()
    {
        var g = new Game();
        var empty = g.Spawn(new GffStruct().SetByte("Lootable", 1), 10);
        Assert.True(empty.SelectableWhenDead);
        Assert.False(empty.IsRaiseable);
        var full = g.Spawn(new GffStruct().SetByte("Lootable", 1)
            .SetList("ItemList", [Ref(0, "InventoryRes", "nw_it_mpotion001", dropable: 1)]), 20);
        HitPoints.Kill(g.World, empty, GameObject.InvalidId);
        HitPoints.Kill(g.World, full, GameObject.InvalidId);
        g.Run(4);
        Assert.False(empty.IsDestroyed);
        g.Run(2);
        Assert.True(empty.IsDestroyed);
        Assert.False(full.IsDestroyed);
        Assert.NotNull(g.World.System<CorpseSystem>().LootFor(full));
    }
}
