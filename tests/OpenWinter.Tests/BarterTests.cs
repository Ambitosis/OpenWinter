using System.Numerics;
using System.Text.Json;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Requests;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Player barter (docs/BARTER.md): asking, offers, locking, accepting, the trade, cancelling.</summary>
public sealed class BarterTests
{
    sealed class Fixture : IDisposable
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly LoopbackConnection ClientA, ClientB;
        public readonly Creature A, B;
        public readonly List<NotifyMessage> NotesA = new(), NotesB = new();
        public readonly List<string> ChatA = new(), ChatB = new();

        public Fixture()
        {
            var resources = new ResMan(TestData.Paths); resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources); World.Register(World.Module); World.EnableScripts();
            var area = new Area { ResRef = "mp", Width = 8, Height = 8 };
            World.Register(area); World.Areas.Add(area);
            area.Walkmesh.Add(new(0, 0, 0), new(80, 0, 0), new(80, 80, 0), 4, true);
            area.Walkmesh.Add(new(0, 0, 0), new(80, 80, 0), new(0, 80, 0), 4, true);
            Server = new GameServer(World, new ServerOptions { StartArea = "mp", Vault = VaultMode.Local });
            (ClientA, A) = Join("Player A");
            (ClientB, B) = Join("Player B");
            A.Position = new Vector3(10, 10, 0);
            B.Position = new Vector3(12, 10, 0);
            foreach (var pc in new[] { A, B })
                foreach (var item in World.Inventory.Items(pc).ToList()) World.Destroy(item);
            A.Gold = 100;
            B.Gold = 50;
            Tick();
            Drain();
        }

        (LoopbackConnection, Creature) Join(string name)
        {
            var pair = LoopbackConnection.CreatePair();
            Server.Accept(pair.Server);
            pair.Client.Send(new HelloMessage { PlayerName = name }.Write());
            Server.Tick(.01f);
            return (pair.Client, Server.Sessions.Single(s => s.PlayerName == name).Creature!);
        }

        public void Tick(int n = 1) { for (int i = 0; i < n; i++) Server.Tick(1f / 30); }

        public void Send(LoopbackConnection c, string kind, object args)
        {
            c.Send(new ClientRequestMessage { Kind = kind, Json = JsonSerializer.Serialize(args) }.Write());
            Tick();
            Drain();
        }

        public void Drain()
        {
            foreach (var (client, notes, chat) in new[] { (ClientA, NotesA, ChatA), (ClientB, NotesB, ChatB) })
                while (client.TryReceive(out var data))
                {
                    var reader = new MessageReader(data);
                    if (reader.Type == MessageType.Notify) notes.Add(NotifyMessage.Read(reader));
                    else if (reader.Type == MessageType.Chat) chat.Add(ChatMessage.Read(new MessageReader(data)).Text);
                }
        }

        public JsonElement LastState(List<NotifyMessage> notes) =>
            JsonDocument.Parse(notes.Last(n => n.Kind == "barter-state").Json).RootElement;

        public string Tlk(uint strref) => World.Resources.TlkString(strref);

        public void Open()
        {
            Send(ClientA, "barter-request", new { target = B.Id });
            Assert.Contains(NotesB, n => n.Kind == "barter-prompt");
            Send(ClientB, "barter-answer", new { from = A.Id, accept = true });
            Assert.Contains(NotesA, n => n.Kind == "barter-state");
            Assert.Contains(NotesB, n => n.Kind == "barter-state");
        }

        public void Dispose()
        {
            World.System<OpenWinter.Game.Persistence.PersistenceSystem>().Dispose();
            foreach (var source in World.Resources.Sources.OfType<IDisposable>().Distinct()) source.Dispose();
        }
    }

    [DataFact]
    public void BothOffersChangeHandsOnlyWhenBothLockedAndAccepted()
    {
        using var f = new Fixture();
        var sword = f.World.Inventory.CreateOn("nw_wswls001", f.A, 1, "BARTER_SWORD")!;
        var potions = f.World.Inventory.CreateOn("nw_it_mpotion001", f.B, 3, "BARTER_POTION")!;
        f.Open();
        f.Send(f.ClientA, "barter-offer", new { item = sword.Id });
        f.Send(f.ClientA, "barter-gold", new { gold = 30 });
        f.Send(f.ClientB, "barter-offer", new { item = potions.Id });
        var seenByB = f.LastState(f.NotesB);
        Assert.Equal(sword.Id, seenByB.GetProperty("theirs").GetProperty("items")[0].GetProperty("id").GetUInt32());
        Assert.Equal(30, seenByB.GetProperty("theirs").GetProperty("gold").GetInt32());

        // Accepting before both offers are locked does nothing.
        f.Send(f.ClientA, "barter-accept", new { });
        Assert.False(f.LastState(f.NotesA).GetProperty("mine").GetProperty("accepted").GetBoolean());
        f.Send(f.ClientA, "barter-lock", new { });
        // A locked offer can't change.
        f.Send(f.ClientA, "barter-gold", new { gold = 60 });
        Assert.Equal(30, f.LastState(f.NotesA).GetProperty("mine").GetProperty("gold").GetInt32());
        f.Send(f.ClientB, "barter-lock", new { });
        Assert.True(f.LastState(f.NotesB).GetProperty("theirs").GetProperty("offered").GetBoolean());
        f.Send(f.ClientA, "barter-accept", new { });
        Assert.Equal(f.World.Inventory.Owner(sword), f.A);
        f.Send(f.ClientB, "barter-accept", new { });

        Assert.Equal(f.B, f.World.Inventory.Owner(sword));
        Assert.Equal(f.A, f.World.Inventory.Owner(potions));
        Assert.Equal(70, f.A.Gold);
        Assert.Equal(80, f.B.Gold);
        Assert.Contains(f.NotesA, n => n.Kind == "barter-close" && n.Json.Contains("false"));
        Assert.Contains(f.NotesB, n => n.Kind == "barter-close");
        Assert.DoesNotContain(f.Tlk(66221), f.ChatA);
        Assert.Null(f.World.System<BarterService>().SessionOf(f.A));
    }

    [DataFact]
    public void ChangingAnOfferTakesBackAcceptance()
    {
        using var f = new Fixture();
        var sword = f.World.Inventory.CreateOn("nw_wswls001", f.A, 1, "BARTER_SWORD")!;
        var dagger = f.World.Inventory.CreateOn("nw_wswdg001", f.B, 1, "BARTER_DAGGER")!;
        f.Open();
        f.Send(f.ClientA, "barter-offer", new { item = sword.Id });
        f.Send(f.ClientA, "barter-lock", new { });
        f.Send(f.ClientB, "barter-lock", new { });
        f.Send(f.ClientA, "barter-accept", new { });
        Assert.True(f.LastState(f.NotesB).GetProperty("theirs").GetProperty("accepted").GetBoolean());
        // B's offer is locked, so it can't change; a dropped item leaves A's offer and takes back acceptance.
        f.Send(f.ClientB, "barter-offer", new { item = dagger.Id });
        Assert.Empty(f.LastState(f.NotesB).GetProperty("mine").GetProperty("items").EnumerateArray());
        f.World.Inventory.Drop(sword, new Location(f.A.Area!.Id, f.A.Position, 0));
        f.Tick(); f.Drain();
        var state = f.LastState(f.NotesA);
        Assert.Empty(state.GetProperty("mine").GetProperty("items").EnumerateArray());
        Assert.False(state.GetProperty("mine").GetProperty("accepted").GetBoolean());
    }

    [DataFact]
    public void RefusingCancellingAndBusyPlayersGetTheGamesLines()
    {
        using var f = new Fixture();
        f.Send(f.ClientA, "barter-request", new { target = f.B.Id });
        f.Send(f.ClientB, "barter-answer", new { from = f.A.Id, accept = false });
        Assert.Contains(f.Tlk(6797).Replace("<FirstName>", f.B.DisplayName(f.World.Resources.Talk)), f.ChatA);
        Assert.Null(f.World.System<BarterService>().SessionOf(f.A));

        f.Open();
        // Already bartering: asking again is refused.
        f.Send(f.ClientA, "barter-request", new { target = f.B.Id });
        Assert.Contains(f.Tlk(6796), f.ChatA);
        f.Send(f.ClientB, "barter-cancel", new { });
        Assert.Contains(f.Tlk(66221), f.ChatA);
        Assert.Contains(f.Tlk(66221), f.ChatB);
        Assert.Contains(f.NotesA, n => n.Kind == "barter-close" && n.Json.Contains("true"));

        // A creature that isn't a player can't be bartered with.
        var npc = new Creature { Name = new OpenWinter.Aurora.Formats.LocString("Merchant") };
        f.World.AddToArea(npc, f.A.Area!, new Vector3(11, 11, 0), 0);
        f.Send(f.ClientA, "barter-request", new { target = npc.Id });
        Assert.Contains(f.Tlk(6794).Replace("<FirstName>", "Merchant"), f.ChatA);
    }

    [DataFact]
    public void WalkingFarApartCancels()
    {
        using var f = new Fixture();
        f.Open();
        f.B.Position = new Vector3(10 + BarterService.MaxDistance + 1, 10, 0);
        f.Tick(); f.Drain();
        Assert.Contains(f.Tlk(66221), f.ChatA);
        Assert.Null(f.World.System<BarterService>().SessionOf(f.B));
    }

    [DataFact]
    public void DamageEndsABarterAtAnyDistance()
    {
        // Taking damage, a jump or starting a conversation ends it.
        using var f = new Fixture();
        f.Open();
        OpenWinter.Game.Rules.HitPoints.Damage(f.World, f.B, OpenWinter.Game.Objects.GameObject.InvalidId, 1, 8);
        f.Tick(); f.Drain();
        Assert.Contains(f.Tlk(66221), f.ChatA);
        Assert.Null(f.World.System<BarterService>().SessionOf(f.A));
    }
}
