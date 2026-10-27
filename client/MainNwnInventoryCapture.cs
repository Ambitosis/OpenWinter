using System.Text.Json;
using OpenWinter.Client.UI;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --connect-nwn ws://host:port --character resref --capture-nwn-inventory dir: on an NWN:EE server, opens the
    /// inventory and takes a shot, equips a weapon from the backpack, opens the nearest container (named by
    /// --nwn-container, default "Chest") and takes an item from it, opens the nearest store (by using the object named
    /// by --nwn-merchant, default "Barrel"), buys and sells an item and examines one, with a shot after each step.
    /// The real mouse is never used.
    /// </summary>
    async Task CaptureNwnInventory(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        for (int i = 0; i < 600 && (_session.Area == null || _session.Controlled == null); i++) await Wait(0.1);
        if (_session.Area == null || _session.Controlled == null)
        {
            GD.PrintErr("[nwn-inv] no area or controlled character after 60 s");
            GetTree().Quit(1);
            return;
        }
        string containerName = _args.GetValueOrDefault("nwn-container", "Chest"), merchant = _args.GetValueOrDefault("nwn-merchant", "Barrel");
        var notices = new List<NotifyMessage>();
        _session.NotifyReceived += notices.Add;
        await Wait(2);

        _input.ToggleInventory?.Invoke();
        for (int i = 0; i < 100 && Inventory.Carried.Count == 0; i++) await Wait(0.1);
        await Wait(3);
        GD.Print($"[nwn-inv] gold {Inventory.Gold}, {Inventory.Carried.Count} carried, equipped [{string.Join(", ", Inventory.Equipped.Values.Select(e => $"{e.Slot}:{e.Name}"))}]");
        await Shot(dir, "01_inventory");

        // Equip the first one-handed weapon in the backpack that isn't already in hand.
        if (Inventory.Carried.FirstOrDefault(i => i.Bag == 0 && i.Info?.Category == "weapon" && ((i.Info?.Slots ?? 0) & 0x10) != 0) is { } weapon)
        {
            _session.Request("equip", new { item = weapon.Id });
            for (int i = 0; i < 50 && !Inventory.Equipped.Values.Any(e => e.Id == weapon.Id); i++) await Wait(0.1);
            await Wait(2);
            GD.Print($"[nwn-inv] equipped {weapon.Name}: {Inventory.Equipped.Values.Any(e => e.Id == weapon.Id)}");
            await Shot(dir, "02_equipped");
        }

        // A container: walk there and open it, then take its first item.
        if (Nearest(containerName) is { } chest)
        {
            _session.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = chest }.Write());
            await Notice(notices, "container-open", 15);
            await Wait(1.5);
            var opened = notices.LastOrDefault(n => n.Kind == "container-open");
            GD.Print($"[nwn-inv] container {chest}: {opened?.Json.Length ?? 0} bytes");
            await Shot(dir, "03_container");
            if (opened != null && Items(opened).FirstOrDefault() is var (take, takeName) && take != 0)
            {
                _session.Request("container-take", new { item = take });
                for (int i = 0; i < 40 && !Inventory.Carried.Any(c => c.Id == take); i++) await Wait(0.1);
                await Wait(1.5);
                GD.Print($"[nwn-inv] took {takeName}: {Inventory.Carried.Any(c => c.Id == take)}");
                await Shot(dir, "04_taken");
            }
            _session.Request("container-close");
            await Wait(1);
        }

        // A store: use the merchant's object, buy the cheapest item, sell something back and examine an item.
        if (Nearest(merchant) is { } shop)
        {
            _session.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = shop }.Write());
            var store = await Notice(notices, "store-open", 20);
            await Wait(2);
            await Shot(dir, "05_store");
            if (store != null)
            {
                using var doc = JsonDocument.Parse(store.Json);
                var stock = doc.RootElement.GetProperty("items").EnumerateArray()
                    .Select(e => (Id: e.GetProperty("id").GetUInt32(), Name: e.GetProperty("name").GetString(), Price: e.GetProperty("price").GetInt32()))
                    .Where(e => e.Price > 0).OrderBy(e => e.Price).ToList();
                GD.Print($"[nwn-inv] store: {stock.Count} items for sale");
                if (stock.Count > 0)
                {
                    int gold = Inventory.Gold;
                    _session.Request("store-buy", new { item = stock[0].Id });
                    for (int i = 0; i < 50 && Inventory.Gold == gold; i++) await Wait(0.1);
                    await Wait(2.5);
                    GD.Print($"[nwn-inv] bought {stock[0].Name} for {stock[0].Price}: gold {gold} -> {Inventory.Gold}");
                    await Shot(dir, "06_bought");
                }
                if (Inventory.Carried.FirstOrDefault(i => i.Bag == 0 && i.Info?.Category == "consumable") is { } sell)
                {
                    int gold = Inventory.Gold;
                    _session.Request("store-sell", new { item = sell.Id });
                    for (int i = 0; i < 50 && Inventory.Gold == gold; i++) await Wait(0.1);
                    await Wait(2.5);
                    GD.Print($"[nwn-inv] sold {sell.Name}: gold {gold} -> {Inventory.Gold}");
                    await Shot(dir, "07_sold");
                }
            }
        }
        if (Inventory.Carried.FirstOrDefault(i => i.Bag == 0 && (i.Info?.Properties.Count ?? 0) > 0) is { } magic)
        {
            _session.Request("examine", new { @object = magic.Id });
            var examined = await Notice(notices, "examine", 5);
            await Wait(1);
            GD.Print($"[nwn-inv] examined {magic.Name}: {examined != null}");
            await Shot(dir, "08_examine");
        }
        _session.NotifyReceived -= notices.Add;
        GetTree().Quit();
    }

    uint? Nearest(string name)
    {
        var here = _session.Controlled!.NetPosition;
        return _session.Objects.Values.Where(o => o.DisplayName.Contains(name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(o => o.NetPosition.DistanceTo(here)).Select(o => (uint?)o.ObjectId).FirstOrDefault();
    }

    async Task<NotifyMessage?> Notice(List<NotifyMessage> seen, string kind, double seconds)
    {
        int from = seen.Count;
        for (int i = 0; i < seconds * 10; i++)
        {
            if (seen.Skip(from).LastOrDefault(n => n.Kind == kind) is { } found) return found;
            await Wait(0.1);
        }
        return null;
    }

    static IEnumerable<(uint Id, string Name)> Items(NotifyMessage n)
    {
        using var doc = JsonDocument.Parse(n.Json);
        return doc.RootElement.GetProperty("items").EnumerateArray()
            .Select(e => (e.GetProperty("id").GetUInt32(), e.GetProperty("name").GetString() ?? "")).ToList();
    }
}
