using System.Text.Json;
using OpenWinter.Aurora.Formats;
using OpenWinter.Content;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Persistence;
using OpenWinter.Game.Players;
using OpenWinter.Game.Requests;
using OpenWinter.Game.Society;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public class QuickbarPossessionTests
{
    [DataFact]
    public void PossessedBarIsEmptyAuthoritativeAndEditsNeverReachOriginalCharacter()
    {
        using var f = new ModuleTransitionTests.Fixture();
        var (session, client) = f.Join("Quickbar owner");
        var w = f.Server.World;
        var master = session.Creature!;
        void Request(string kind, object? data = null)
        {
            client.Send(new ClientRequestMessage { Kind = kind, Json = JsonSerializer.Serialize(data ?? new { }) }.Write());
            f.Server.Tick(.01f);
        }
        JsonElement Reply(string kind)
        {
            ModuleTransitionTests.Fixture.Drain(client);
            Request(kind);
            var notices = ModuleTransitionTests.Fixture.Drain(client).Where(p => (MessageType)p[0] == MessageType.Notify)
                .Select(p => NotifyMessage.Read(new MessageReader(p))).Where(n => n.Kind == kind).ToList();
            var notice = Assert.Single(notices);
            Assert.Equal(session.Creature!.Id, notice.Subject);
            return JsonSerializer.Deserialize<JsonElement>(notice.Json);
        }
        Request("quickbar-set", new { creature = master.Id, index = 0, slot = new QuickbarSlot { Kind = "command", Arg = "rest" } });
        Request("quickbar-set", new { creature = master.Id, index = 7, slot = (QuickbarSlot?)null });
        string original = JsonSerializer.Serialize(QuickbarRequests.Read(w, master, _ => { }));
        var before = ObjectSerializer.CreatureGff(w, master, uint.MaxValue, true);
        var familiar = w.Creatures.FromBlueprint("nw_fm_bat01")!;
        familiar.Scripts.Clear(); w.AddToArea(familiar, master.Area!, master.Position, 0);
        AssociateService.Add(w, master, familiar, 3);
        Assert.Null(QuickbarRequests.Read(w, familiar, _ => { }));
        Assert.True(w.System<FamiliarPossession>().Possess(session, familiar));
        var bar = Reply("quickbar");
        Assert.True(bar.GetProperty("present").GetBoolean());
        Assert.True(bar.GetProperty("authoritative").GetBoolean());
        Assert.False(bar.GetProperty("localCache").GetBoolean());
        Assert.Equal(36, bar.GetProperty("slots").GetArrayLength());
        Assert.All(bar.GetProperty("slots").EnumerateArray(), s => Assert.Equal(JsonValueKind.Null, s.ValueKind));
        Request("quickbar-set", new { creature = master.Id, index = 0, slot = new QuickbarSlot { Kind = "command", Arg = "attack" } });
        Assert.Empty(familiar.QuickbarEdits); // queued old-owner update is rejected
        Request("quickbar-set", new { creature = familiar.Id, index = 2, slot = new QuickbarSlot { Kind = "mode", Id = 0 } });
        Assert.Equal("mode", familiar.QuickbarEdits[2]!.Kind);
        Assert.Equal(original, JsonSerializer.Serialize(QuickbarRequests.Read(w, master, _ => { })));
        Reply("abilities"); // request/response is addressed to the controlled creature
        Request("action-mode", new { mode = 0, on = true });
        Assert.True(familiar.DetectMode);
        Assert.False(master.DetectMode);
        var exported = Gff.Read(File.ReadAllBytes(ServerVault.Export(w, familiar)));
        Assert.Equal(JsonSerializer.Serialize(BicQuickbar.Read(before)), JsonSerializer.Serialize(BicQuickbar.Read(exported)));
        w.System<FamiliarPossession>().Unpossess(familiar);
        bar = Reply("quickbar");
        Assert.True(bar.GetProperty("localCache").GetBoolean());
        Assert.True(bar.GetProperty("authoritative").GetBoolean());
        Assert.Equal(original, JsonSerializer.Serialize(bar.GetProperty("slots").Deserialize<QuickbarSlot?[]>()));
        Assert.Null(master.QuickbarEdits[7]);
    }
}
