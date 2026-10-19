using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Who is told a line when two players share an area: speech by volume and distance, floating text with and
/// without bBroadcastToFaction, attack lines for players nearby. Measured on the original with one client
/// (parity/visual/scenes/mp_delivery, docs/MULTIPLAYER_HARNESS.md).
/// </summary>
public sealed class MultiplayerDeliveryTests
{
    sealed class Fixture : IDisposable
    {
        public readonly World World;
        public readonly GameServer Server;
        public readonly LoopbackConnection ClientA, ClientB;
        public readonly Creature A, B;

        public Fixture()
        {
            var resources = new ResMan(TestData.Paths); resources.AddBaseGame(includeUserOverride: false);
            World = new World(resources); World.Register(World.Module); World.EnableScripts();
            var area = new Area { ResRef = "mp", Width = 8, Height = 8 };
            World.Register(area); World.Areas.Add(area);
            Server = new GameServer(World, new ServerOptions { StartArea = "mp", Vault = VaultMode.Local });
            (ClientA, A) = Join("Player A");
            (ClientB, B) = Join("Player B");
            Drain(ClientA); Drain(ClientB);
        }

        (LoopbackConnection, Creature) Join(string name)
        {
            var pair = LoopbackConnection.CreatePair();
            Server.Accept(pair.Server);
            pair.Client.Send(new HelloMessage { PlayerName = name }.Write());
            Server.Tick(.01f);
            return (pair.Client, Server.Sessions.Single(s => s.PlayerName == name).Creature!);
        }

        /// <summary>Puts A at (10, 10) and B <paramref name="distance"/> metres east of her.</summary>
        public void Apart(float distance)
        {
            A.Position = new Vector3(10, 10, 0);
            B.Position = new Vector3(10 + distance, 10, 0);
        }

        public static List<string> Drain(LoopbackConnection client)
        {
            var lines = new List<string>();
            while (client.TryReceive(out var data))
                if ((MessageType)data[0] == MessageType.Chat) lines.Add(ChatMessage.Read(new MessageReader(data)).Text);
            return lines;
        }

        public void Dispose()
        {
            World.System<OpenWinter.Game.Persistence.PersistenceSystem>().Dispose();
            foreach (var source in World.Resources.Sources.OfType<IDisposable>().Distinct()) source.Dispose();
        }
    }

    [DataFact]
    public void SpeechReachesPlayersByVolumeAndDistance()
    {
        using var f = new Fixture();
        foreach (var (volume, distance, heard) in new[] { (0, 20f, true), (0, 25f, false), (1, 2.5f, true), (1, 3.5f, false), (2, 65f, true) })
        {
            f.Apart(distance);
            Fixture.Drain(f.ClientA); Fixture.Drain(f.ClientB);
            f.Server.Speak(f.A, "line", volume);
            Assert.Contains("line", Fixture.Drain(f.ClientA));
            Assert.Equal(heard, Fixture.Drain(f.ClientB).Contains("line"));
        }
    }

    [DataFact]
    public void PlayerTalkIsRangeLimitedLikeScriptSpeech()
    {
        using var f = new Fixture();
        f.Apart(25);
        f.ClientA.Send(new ChatSendMessage { Channel = ChatChannel.Talk, Text = "far" }.Write());
        f.Server.Tick(.01f);
        Assert.Contains("far", Fixture.Drain(f.ClientA));
        Assert.DoesNotContain("far", Fixture.Drain(f.ClientB));
        f.Apart(10);
        f.ClientA.Send(new ChatSendMessage { Channel = ChatChannel.Talk, Text = "near" }.Write());
        f.Server.Tick(.01f);
        Assert.Contains("near", Fixture.Drain(f.ClientB));
    }

    [DataFact]
    public void FloatingTextReachesTheFactionWithinThirtyMetres()
    {
        using var f = new Fixture();
        f.Apart(5);
        Fixture.Drain(f.ClientA); Fixture.Drain(f.ClientB);
        // Not in her party: only A sees her own text, whether or not it is broadcast to her faction.
        f.Server.FloatingText(f.A, "faction", true);
        f.Server.FloatingText(f.A, "only", false);
        Assert.Equal(["faction", "only"], Fixture.Drain(f.ClientA));
        Assert.Empty(Fixture.Drain(f.ClientB));

        OpenWinter.Game.Society.PartyService.Of(f.World).Join(f.B, f.A);
        foreach (var (distance, seen) in new[] { (25f, true), (35f, false) })
        {
            f.Apart(distance);
            Fixture.Drain(f.ClientA); Fixture.Drain(f.ClientB);
            f.Server.FloatingText(f.A, "party", true);
            f.Server.FloatingText(f.A, "mine", false);
            Assert.Equal(seen ? ["party"] : [], Fixture.Drain(f.ClientB).Where(l => l is "party" or "mine").ToList());
        }
    }

    [DataFact]
    public void AttackLinesSkipBystandersButReachAnAssociatesMaster()
    {
        using var f = new Fixture();
        var area = f.A.Area!;
        var attacker = new Creature { Tag = "bandit" };
        var target = new Creature { Tag = "goblin" };
        f.World.Register(attacker); f.World.Register(target);
        f.World.AddToArea(attacker, area, new Vector3(10, 10, 0), 0, exact: true, enterEvent: false);
        f.World.AddToArea(target, area, new Vector3(11.5f, 10, 0), 0, exact: true, enterEvent: false);
        f.A.Position = new Vector3(10, 14, 0);    // 4 m from the fight (combatlines fight_4: nothing printed)
        f.B.Position = new Vector3(10, 22, 0);    // 12 m
        var told = OpenWinter.Game.Society.AssociateService.AttackFeedbackPlayers(f.World, attacker, target);
        Assert.Empty(told);
        // A's henchman attacking the goblin, and the goblin attacking it (combatlines hench_attacks_12, hench_attacked_12).
        attacker.Master = f.A.Id;
        Assert.Equal([f.A], OpenWinter.Game.Society.AssociateService.AttackFeedbackPlayers(f.World, attacker, target));
        Assert.Equal([f.A], OpenWinter.Game.Society.AssociateService.AttackFeedbackPlayers(f.World, target, attacker));
    }
}
