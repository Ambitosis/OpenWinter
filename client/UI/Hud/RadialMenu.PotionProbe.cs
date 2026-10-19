using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

public partial class RadialMenu
{
    /// <summary>Exercises the inventory power callback, including stale/non-self power metadata.</summary>
    public static int VerifyPotionTargeting(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        var (client, server) = LoopbackConnection.CreatePair();
        var session = new ClientSession { Connection = client };
        var interaction = new Interaction { Session = session };
        var casting = new SpellCasting { Session = session, Interaction = interaction };
        var book = new AbilityBook(session);
        var menu = new RadialMenu { Session = session, Casting = casting, _book = book,
            _user = new AbilityUser { Session = session, Casting = casting, Book = book } };
        var other = new ObjectView { ObjectId = 99, Kind = ObjectKind.Creature };
        void Check(bool pass, string name)
        {
            if (!pass) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        try
        {
            server.Send(new ControlObjectMessage { ObjectId = 7 }.Write()); session._Process(0);
            Check(session.ControlledId == 7, "controlled player received through session");
            foreach (int baseItem in new[] { 49, 104, 44 })
            {
                book.Items.Clear();
                book.Items.Add(new ItemAbility(42, "Power", [], 1, 1, true, false, true,
                    [new ItemPower(3, "Healing", 1, "", AbilityUser.TargetCreature, false, 1, true, "power")], "item"));
                menu._inventoryItem = new ItemEntry(42, "Power", baseItem, 1, []);
                var command = menu.InventoryRoot()[3];
                Check(!command.Disabled && command.Act != null, $"base {baseItem} has a usable power");
                command.Act!();
                if (baseItem == 44)
                {
                    Check(interaction.Targeting != null && !server.TryReceive(out _), "non-potion retains target selection");
                    interaction.Targeting!(other, null);
                }
                else Check(interaction.Targeting == null, $"potion {baseItem} skips targeting");
                Check(server.TryReceive(out var bytes), $"base {baseItem} sends activation");
                var request = ClientRequestMessage.Read(new MessageReader(bytes));
                using var args = JsonDocument.Parse(request.Json);
                Check(request.Kind == "use-item" && args.RootElement.GetProperty("item").GetUInt32() == 42
                    && args.RootElement.GetProperty("prop").GetInt32() == 3
                    && args.RootElement.GetProperty("target").GetUInt32() == (baseItem == 44 ? 99u : 7u),
                    $"base {baseItem} sends exact item, property and intended target");
            }
            File.WriteAllText(System.IO.Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[potion-targeting] {checks.Count} checks passed"); return 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(System.IO.Path.Combine(directory, "failure.txt"), e.ToString()); GD.PrintErr(e); return 1;
        }
        finally { other.Free(); menu.Free(); interaction.Free(); session.Free(); client.Disconnect(); }
    }
}
