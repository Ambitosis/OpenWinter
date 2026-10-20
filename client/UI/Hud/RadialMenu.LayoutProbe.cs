using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

public partial class RadialMenu
{
    /// <summary>Headless Godot regression using the real DM extension and root menu builders.</summary>
    public static int VerifyDmDirections(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        var session = new ClientSession();
        var interaction = new Interaction { Session = session };
        var casting = new SpellCasting { Session = session, Interaction = interaction };
        var book = new AbilityBook(session);
        var menu = new RadialMenu
        {
            Session = session, Casting = casting, _book = book,
            _user = new AbilityUser { Session = session, Casting = casting, Book = book }
        };
        var target = new ObjectView { ObjectId = 42 };
        long selected = -1;
        interaction.MenuChosen = (id, obj) => { if (obj == target) selected = id; };
        void Check(bool value, string label)
        {
            if (!value) throw new InvalidOperationException(label);
            checks.Add(label);
        }
        try
        {
            foreach (var (kind, self, full) in new[] {
                (ObjectKind.Creature, true, false), (ObjectKind.Creature, true, true),
                (ObjectKind.Creature, false, false), (ObjectKind.Door, false, false),
                (ObjectKind.Placeable, false, false), (ObjectKind.Item, false, false) })
            {
                target.Kind = kind; menu._target = target;
                using var facts = JsonDocument.Parse(full ? "{\"canCancelPolymorph\":true}" : "{}");
                book.TargetFacts[target.ObjectId] = facts.RootElement.Clone();
                interaction.ExtendMenu = null;
                var ordinary = menu.Root(target, self);
                interaction.ExtendMenu = (popup, _) => { popup.AddItem("Heal", 700); popup.AddItem("Disabled", 701); popup.SetItemDisabled(1, true); };
                var dm = menu.Root(target, self);
                string context = $"{kind}/self={self}/full={full}";
                Check(dm.Count == 8, context + " remains one eight-direction ring");
                for (int i = 0; i < 8; i++)
                    if (!ordinary[i].Hidden) Check(dm[i].Label == ordinary[i].Label, context + $" preserves direction {i}");
                var tools = full ? dm[6].Open!().Single(e => e.Label == "DM tools") : dm.Single(e => e.Label == "DM tools");
                var commands = tools.Open!();
                Check(commands.Count == 2 && commands[1].Disabled, context + " preserves DM command states");
                selected = -1; commands[0].Act!();
                Check(selected == 700, context + " dispatches original DM command id and target");
                if (full) Check(dm[6].Open!().Count == 8, context + " keeps the Special Abilities sub-ring at eight directions");
                if (full) Check(dm[4].Act != null && dm[6].Open!().Any(e => e.Label == "Craft"), context + " retains polymorph and existing abilities");
            }
            // Owned items: a bag's ring offers Open; other items don't.
            menu._target = null;
            ItemEntry? opened = null;
            menu._inventoryOpenBag = item => opened = item;
            var bag = new ItemEntry(77, "Bag", 66, 1, []) { Info = new ItemInfo { Category = "container" } };
            menu._inventoryItem = bag;
            var bagRing = menu.InventoryRoot();
            Check(bagRing.Count == 8 && bagRing[0].Label == "Examine" && bagRing[2].Label == "Open", "bag ring has Examine N and Open E");
            bagRing[2].Act!();
            Check(opened == bag, "bag Open opens that bag");
            menu._inventoryItem = new ItemEntry(78, "Gem", 77, 1, []) { Info = new ItemInfo { Category = "misc" } };
            Check(menu.InventoryRoot().All(e => e.Label != "Open"), "non-bag ring has no Open");
            // With a store open (measured, storeradial): Sell at E in place of Equip, no Equip offhand, a bag keeps Open,
            // a worn item keeps Unequip at W, a stack keeps Split at W.
            var sold = new List<ItemEntry>();
            var sword = new ItemEntry(79, "Sword", 1, 1, []) { Info = new ItemInfo { Category = "weapon", Slots = 1 << 4 } };
            menu._inventoryItem = sword;
            Check(menu.InventoryRoot()[2].Label == "Equip", "unworn sword: Equip E without a store");
            menu._inventorySell = sold.Add;
            var swordRing = menu.InventoryRoot();
            Check(swordRing[2].Label == "Sell" && !swordRing[2].Disabled && swordRing[6].Hidden, "store open: sword Sell E, nothing W");
            Check(swordRing[0].Label == "Examine" && swordRing[1].Label == "Cast Spell" && swordRing[4].Label == "Drop", "store open: N NE S kept");
            swordRing[2].Act!();
            Check(sold.SequenceEqual([sword]), "ring Sell sells that item");
            menu._inventoryCanSell = _ => false;
            Check(menu.InventoryRoot()[2].Disabled, "modern ring greys Sell when the store won't buy");
            menu._inventoryCanSell = null;
            menu._inventoryItem = bag;
            Check(menu.InventoryRoot()[2].Label == "Open", "store open: bag keeps Open E (no Sell)");
            menu._inventoryItem = new ItemEntry(80, "Robe", 16, 1, [], Slot: 1) { Info = new ItemInfo { Category = "armor", Slots = 1 << 1 } };
            var worn = menu.InventoryRoot();
            Check(worn[2].Label == "Sell" && worn[6].Label == "Unequip", "store open: worn robe Sell E, Unequip W");
            menu._inventoryItem = new ItemEntry(81, "Arrows", 20, 50, []) { Info = new ItemInfo { Category = "ammo", Slots = 1 << 11 } };
            var stack = menu.InventoryRoot();
            Check(stack[2].Label == "Sell" && stack[6].Label == "Split", "store open: stack Sell E, Split W");
            menu._inventorySell = null;
            Check(menu.InventoryRoot()[6].Label == "Split", "stack Split W without a store");
            menu._inventoryItem = null;
            menu._inventoryOpenBag = null;
            File.WriteAllText(System.IO.Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[dm-radial] {checks.Count} checks passed");
            return 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(System.IO.Path.Combine(directory, "failure.txt"), e.ToString()); GD.PrintErr(e); return 1;
        }
        finally { target.Free(); menu.Free(); interaction.Free(); session.Free(); }
    }
}
