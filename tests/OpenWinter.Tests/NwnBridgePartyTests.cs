using System.Text.Json;
using OpenWinter.Net;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using Xunit;
using static OpenWinter.Net.Nwn.Messages.PartyMessages;

namespace OpenWinter.Tests;

/// <summary>
/// The player list and parties through the NWN bridge, from frames recorded on nwserver with two players (Amber Rose,
/// player "probe", and Ada Covington, "probe2"), and the lines the original printed for them.
/// </summary>
public class NwnBridgePartyTests
{
    const uint Amber = 0x7FFFFFFF, Ada = 0x7FFFFFFE;

    sealed class Data : INwnBridgeData
    {
        public int TwoDARowCount(string table) => 0;
        public string TwoDA(string table, int row, string column) => "";
        public string Text(uint strRef, byte gender) => strRef switch
        {
            6453 => "<CUSTOM0> has left the party.",
            6454 => "<CUSTOM0> has joined the party.",
            6460 => "You have received a Party Invitation from <CUSTOM0>.",
            6465 => "You have rejected the Party Invitation from <CUSTOM0>.",
            6610 => "<PlayerName> has joined as a player.",
            6612 => "<PlayerName> has left as a player.",
            7992 => "<CUSTOM0> now dislikes you!\n",
            8018 => "Party Protected",
            8020 => "No PvP",
            66776 => "You have been invited by <CUSTOM0> to join a party.",
            _ => $"#{strRef}",
        };
        public void UseContent(IReadOnlyList<string> haks, string customTlk) { }
    }

    // Frames recorded from nwserver (after the major / minor bytes).
    const string AmberAdd = "0201070000000011FFFFFF7F02000E0500000070726F6265020103020E0000000011FFFFFF7F120005000000416D626572120004000000526F736505FEFF0D0C706F5F68755F665F3531365F";
    const string AmberFull = "02010301070000000011FFFFFF7F02000E0500000070726F6265020103020E0000000011FFFFFF7F120005000000416D626572120004000000526F736505FEFF0D0C706F5F68755F665F3531365F";
    const string AdaAdd = "0201070100000011FEFFFF7F02000E0600000070726F626532020103020E0000000011FEFFFF7F120003000000416461120009000000436F76696E67746F6E05FEFF0D0B706F5F68755F665F31375F";
    const string AdaDelete = "02010701000000";
    /// <summary>Ada's party status while Amber's invitation is open (the inviter is an object id).</summary>
    const string AdaInvitedByAmber = "0455030511FEFFFF7F07004000000201020102010201020111FFFFFF7F120005000000416D626572120004000000526F736502000200";

    /// <summary>Both players' status once Ada has joined: Amber leads, neither is alone.</summary>
    const string AfterJoin = "0455030511FFFFFF7F070040000002010201020002010200020002000455030511FEFFFF7F0700440000060C00060A00060000060C0002010201020002000200020002000455030511FEFFFF7F076066050005FEFF0D0B706F5F68755F665F31375F05FFFF030005000003010200113800000008140000000814000000060C00060A00060000060C000201120003000000416461120009000000436F76696E67746F6E0201020102000200020002000200110000007F050000020002000200";

    static T Decode<T>(string hex) where T : NwnMessage, new()
    {
        var m = new T();
        m.Read(new NwnWsReader(Convert.FromHexString(hex), new NwnSessionContext(_ => 0, (_, _, _) => "")));
        return m;
    }

    static ClientSideMessageFeedbackMessages.ClientSideMessageFeedback Feedback(string hex) => Decode<ClientSideMessageFeedbackMessages.ClientSideMessageFeedback>(hex);

    sealed class Bridge
    {
        public readonly List<byte[]> Output = [];
        public readonly List<NwnMessage> Sent = [];
        public readonly NwnParty Party;
        public IReadOnlyList<(uint Id, string Name, string Portrait)> Bar = [];

        public Bridge(bool oneParty = false)
        {
            var world = new NwnWorldMirror(new Data(), m => { });
            Party = new NwnParty(new Data(), world, Sent.Add, Output.Add,
                () => new BnxrPacket { PvpSetting = 1, OnePartyOnly = oneParty })
            { PartyChanged = p => Bar = p };
        }

        public void Take(NwnMessage m) => Assert.True(Party.ToClient(m));

        public List<string> Chat() => Output.Select(o => new MessageReader(o)).Where(r => r.Type == MessageType.Chat).Select(r => ChatMessage.Read(r).Text).ToList();

        public List<NotifyMessage> Notes(string kind) => Output.Select(o => new MessageReader(o)).Where(r => r.Type == MessageType.Notify).Select(NotifyMessage.Read).Where(n => n.Kind == kind).ToList();

        public JsonElement List() => JsonDocument.Parse(Notes("player-list").Last().Json).RootElement;

        /// <summary>Amber's login, as recorded: Add and Full twice, then Ada's two Adds.</summary>
        public Bridge AmberWithAda()
        {
            foreach (var _ in new[] { 1, 2 })
            {
                Take(Decode<PlayerListMessages.PlayerListAdd>(AmberAdd));
                Take(Decode<PlayerListMessages.PlayerListFull>(AmberFull));
            }
            Take(Decode<PlayerListMessages.PlayerListAdd>(AdaAdd));
            Take(Decode<PlayerListMessages.PlayerListAdd>(AdaAdd));
            return this;
        }
    }

    [Fact]
    public void JoinAndLeaveLinesAreTheOriginalsOncePerPlayer()
    {
        var b = new Bridge().AmberWithAda();
        b.Take(Decode<PlayerListMessages.PlayerListDelete>(AdaDelete));
        // As nwmain printed them: the talk table's line and a second full stop, one per player for the two Adds.
        Assert.Equal(["probe has joined as a player..", "probe2 has joined as a player..", "probe2 has left as a player.."], b.Chat());
    }

    [Fact]
    public void PlayerListNamesTheOtherPlayersByCharacterAndLeavesOutDungeonMasters()
    {
        var b = new Bridge().AmberWithAda();
        b.Take(new PlayerListMessages.PlayerListAdd
        {
            DisplayDmMsg = false,
            Player = new PlayerListMessages.Player { PlayerId = 2, ObjectId = 0x7FFFFFFD, IsDm = true, PlayerName = "dm" },
        });
        Assert.True(b.Party.FromClient("player-list", ""));
        var list = b.List();
        var player = Assert.Single(list.GetProperty("players").EnumerateArray());
        Assert.Equal(Ada, player.GetProperty("id").GetUInt32());
        Assert.Equal("Ada Covington", player.GetProperty("name").GetString());
        Assert.False(player.GetProperty("party").GetBoolean());
        Assert.True(list.GetProperty("leader").GetBoolean());
        Assert.Equal("Party Protected", list.GetProperty("pvpText").GetString());
        // The hidden join line of a Dungeon Master the server doesn't announce.
        Assert.DoesNotContain(b.Chat(), l => l.Contains("dm "));
        // An area set to No PvP.
        b.Party.Area(new AreaMessages.AreaClientArea { Pvp = 0 });
        Assert.Equal("No PvP", b.List().GetProperty("pvpText").GetString());
    }

    [Fact]
    public void AnInvitationOpensTheWindowAndARejectionClosesIt()
    {
        var b = new Bridge();
        b.Take(Decode<PlayerListMessages.PlayerListAdd>(AdaAdd));
        b.Take(Decode<PlayerListMessages.PlayerListAdd>(AmberAdd));
        // Ada's side: the feedback names Amber's creature, the popup her name.
        Assert.True(b.Party.Feedback(Feedback("05250011FFFFFF7F")));
        b.Take(Decode<PopupGuiPanelMessages.PopUpGUIPanelPopUpPartyInvitationReceivedPanel>("0E0A000000416D62657220526F7365"));
        var open = JsonDocument.Parse(b.Notes("party-invitation").Single().Json).RootElement;
        Assert.Equal(Amber, open.GetProperty("from").GetUInt32());
        Assert.True(open.GetProperty("open").GetBoolean());
        Assert.Equal("You have been invited by Amber Rose to join a party.", open.GetProperty("text").GetString());
        Assert.True(b.Party.Facts(Amber)!["invitedMe"]!.GetValue<bool>());

        Assert.True(b.Party.FromClient("party-answer", $$"""{"from":{{Amber}},"answer":"reject"}"""));
        Assert.Equal(Amber, Assert.IsType<PartyRejectInvitation>(Assert.Single(b.Sent)).Creature);
        Assert.True(b.Party.Feedback(Feedback("052A0011FFFFFF7F")));
        Assert.False(JsonDocument.Parse(b.Notes("party-invitation").Last().Json).RootElement.GetProperty("open").GetBoolean());
        Assert.Equal(["probe2 has joined as a player..", "probe has joined as a player..",
            "You have received a Party Invitation from Amber Rose.", "You have rejected the Party Invitation from Amber Rose."], b.Chat());
    }

    [Fact]
    public void JoiningFillsTheListThePartyBarAndTheRadial()
    {
        var b = new Bridge().AmberWithAda();
        b.Party.FromClient("player-list", "{}");
        var status = Decode<GameObjUpdateMessages.GameObjUpdateObjList>(AdaInvitedByAmber);
        b.Party.ObjectList(status);
        Assert.True(b.List().GetProperty("players")[0].GetProperty("invitedThem").GetBoolean());

        b.Take(Decode<PartyListAdd>("080100000011FEFFFF7F"));
        Assert.True(b.Party.Feedback(Feedback("05260011FEFFFF7F")));
        Assert.Equal("Ada Covington has joined the party.", b.Chat().Last());
        b.Party.ObjectList(Decode<GameObjUpdateMessages.GameObjUpdateObjList>(AfterJoin));
        var ada = b.List().GetProperty("players")[0];
        Assert.True(ada.GetProperty("party").GetBoolean());
        Assert.Equal(2, ada.GetProperty("partySize").GetInt32());
        Assert.Equal((Ada, "Ada Covington", "po_hu_f_17_"), Assert.Single(b.Bar));
        var party = JsonDocument.Parse(b.Notes("party").Last().Json).RootElement;
        Assert.Equal(Amber, party.GetProperty("leader").GetUInt32());
        Assert.Equal(2, party.GetProperty("members").GetArrayLength());
        var facts = b.Party.Facts(Ada)!;
        Assert.True(facts["sameParty"]!.GetValue<bool>());
        Assert.True(facts["canKick"]!.GetValue<bool>());
        Assert.False(facts["canInvite"]!.GetValue<bool>());
        Assert.Null(b.Party.Facts(Amber));

        // Ada leaves: the server lists her out and says so.
        b.Take(Decode<PartyListRemove>("080100000011FEFFFF7F"));
        Assert.True(b.Party.Feedback(Feedback("052C0011FEFFFF7F")));
        Assert.Equal("Ada Covington has left the party.", b.Chat().Last());
        Assert.Empty(b.Bar);
        Assert.False(b.List().GetProperty("inParty").GetBoolean());
    }

    [Fact]
    public void ADislikeIsBothWaysAndALikeOnlyTheLikers()
    {
        var b = new Bridge().AmberWithAda();
        b.Party.FromClient("player-list", "{}");
        // Amber dislikes Ada: her own attitude (is_source), Ada's toward her, and the line about Ada.
        b.Take(Decode<PvpMessages.PVPAttitudeChangeS2C>("0700000000070100000002000201"));
        b.Take(Decode<PvpMessages.PVPAttitudeChangeS2C>("0700000000070100000002000200"));
        Assert.True(b.Party.Feedback(Feedback("05CB0011FEFFFF7F")));
        var ada = b.List().GetProperty("players")[0];
        Assert.True(ada.GetProperty("dislike").GetBoolean());
        Assert.True(ada.GetProperty("dislikedBy").GetBoolean());
        Assert.Equal("Ada Covington now dislikes you!\n", b.Chat().Last());
        var like = JsonDocument.Parse(b.Notes("pc-like").Single().Json).RootElement;
        Assert.Equal(Ada, like.GetProperty("other").GetUInt32());
        Assert.False(like.GetProperty("like").GetBoolean());
        // Amber likes Ada again: only her own side changes.
        b.Take(Decode<PvpMessages.PVPAttitudeChangeS2C>("0700000000070100000002010201"));
        ada = b.List().GetProperty("players")[0];
        Assert.False(ada.GetProperty("dislike").GetBoolean());
        Assert.True(ada.GetProperty("dislikedBy").GetBoolean());
    }

    [Fact]
    public void RequestsBecomeTheMessagesTheServerReads()
    {
        var b = new Bridge().AmberWithAda();
        string Hex(NwnMessage m)
        {
            var w = new NwnWsWriter(new NwnSessionContext(_ => 0, (_, _, _) => ""));
            m.Write(w);
            return $"{m.Major:X2}{m.Minor:X2}{Convert.ToHexString(w.ToArray())}";
        }
        b.Party.FromClient("party-invite", $$"""{"other":{{Ada}}}""");
        b.Party.FromClient("party-answer", $$"""{"from":{{Ada}},"answer":"accept"}""");
        b.Party.FromClient("party-answer", $$"""{"from":{{Ada}},"answer":"ignore"}""");
        b.Party.FromClient("party-kick", $$"""{"other":{{Ada}}}""");
        b.Party.FromClient("party-leader", $$"""{"other":{{Ada}}}""");
        b.Party.FromClient("party-leave", "");
        b.Party.FromClient("pc-like", $$"""{"other":{{Ada}},"like":false}""");
        // As recorded from the bridge and answered by the server (invite, accept, ignore, kick, transfer, leave with the
        // player's own creature, dislike).
        Assert.Equal(["0E0911FEFFFF7F", "0E0B11FEFFFF7F", "0E0A11FEFFFF7F", "0E0711FEFFFF7F", "0E0811FEFFFF7F", "0E0611FFFFFF7F", "290311FEFFFF7F0200"],
            b.Sent.Select(Hex).ToList());
        Assert.False(b.Party.FromClient("area-ready", "{}"));
    }

    [Fact]
    public void OnePartyOnlyTakesAwayInviteAndKick()
    {
        var b = new Bridge(oneParty: true).AmberWithAda();
        var facts = b.Party.Facts(Ada)!;
        Assert.False(facts["canInvite"]!.GetValue<bool>());
        Assert.True(facts["oneParty"]!.GetValue<bool>());
    }
}
