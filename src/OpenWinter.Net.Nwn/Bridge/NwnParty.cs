using System.Text.Json.Nodes;
using OpenWinter.Net.Nwn.Messages;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net.Nwn.Wire;
using static OpenWinter.Net.Nwn.Messages.ClientSideMessageFeedbackMessages;
using static OpenWinter.Net.Nwn.Messages.GameObjUpdateUpdateMessages;
using static OpenWinter.Net.Nwn.Messages.PartyMessages;
using static OpenWinter.Net.Nwn.Messages.PlayerListMessages;
using static OpenWinter.Net.Nwn.Messages.PvpMessages;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// The player list and parties.
/// <list type="bullet">
/// <item>The server's player list (PlayerListFull / Add / Delete) becomes the "player-list" notify, with the other
/// players by character name and Dungeon Masters left out, and the join and leave lines the original prints itself
/// ("probe has joined as a player..", once for the two Adds a login brings).</item>
/// <item>The party (PartyList / Add / Remove: the other members), each player's party status in object updates
/// (alone, leading, invited by whom) and the like / dislike changes (PVPAttitudeChange) fill in the list, the
/// "party" notify, the party bar and the radial's party facts.</item>
/// <item>Party feedback becomes the original's chat lines; the invitation window opens with the server's popup and
/// closes when the invitation is answered or expires.</item>
/// <item>The client's "party-invite", "party-answer", "party-leave", "party-kick", "party-leader" and "pc-like"
/// requests go to the server as the party and PvP attitude messages.</item>
/// </list>
/// </summary>
public sealed class NwnParty(INwnBridgeData data, NwnWorldMirror world, Action<NwnMessage> send, Action<byte[]> emit, Func<BnxrPacket?> settings)
{
    // dialog.tlk lines the original shows for each case (measured on nwmain, docs/NETWORKING.md).
    const uint Joined = 6610, JoinedDm = 6611, Left = 6612, LeftDm = 6613;
    const uint LeftParty = 6453, JoinedParty = 6454, NowLeader = 6455, AlreadyConsidering = 6456, InNegotiations = 6457,
        IgnoringYou = 6458, ReceivedInvitation = 6460, SentInvitation = 6462, YouIgnore = 6464, YouRejected = 6465,
        YouWereKicked = 6466, WasKicked = 6467, Expired = 6468, RejectedYours = 6469, YouLeft = 10427,
        OnePartyCantLeave = 66735, OnePartyCantKick = 66736, LeaderOnly = 66858, AlreadyInParty = 66891,
        InvitationText = 66776, DislikesYou = 7992;
    const uint InvalidObject = GameObjUpdateMessages.InvalidObjectId;

    /// <summary>A player on the server: her creature and character name.</summary>
    public sealed record Player(uint PlayerId, uint ObjectId, string Name, string PlayerName, bool IsDm, string Portrait);

    /// <summary>A player's party status from object updates: alone (a party of one), leading, invited by whom.</summary>
    readonly record struct Status(bool Alone, bool Leader, uint InvitedBy);

    readonly Dictionary<uint, Player> _players = [];
    readonly List<uint> _members = [];
    readonly Dictionary<uint, Status> _status = [];
    /// <summary>Like (true) or dislike, by the other player's id: mine toward her, and hers toward me.</summary>
    readonly Dictionary<uint, bool> _attitude = [], _reaction = [];
    uint _self = uint.MaxValue, _invitedBy = InvalidObject;
    int _areaPvp = 3;
    bool _listening;
    string _lastList = "", _lastParty = "";

    /// <summary>The other players in the player's party, in the order the server lists them.</summary>
    public IReadOnlyList<uint> Members => _members;

    /// <summary>The party changed: its other players (creature, name, portrait), for the party bar.</summary>
    public Action<IReadOnlyList<(uint Id, string Name, string Portrait)>>? PartyChanged { get; set; }

    /// <summary>The players on the server, by player id.</summary>
    public IReadOnlyDictionary<uint, Player> Players => _players;

    // -- client to server ---------------------------------------------------

    /// <summary>Handles a client request that belongs here; false for every other.</summary>
    public bool FromClient(string kind, string json)
    {
        if (!kind.StartsWith("party-", StringComparison.Ordinal) && kind is not ("player-list" or "pc-like")) return false;
        // A request without data comes with no JSON at all ("player-list", "party-leave").
        var d = (string.IsNullOrEmpty(json) ? null : JsonNode.Parse(json)) as JsonObject ?? [];
        uint Id(string key) => d[key] is JsonValue v && v.TryGetValue<uint>(out var i) ? i : 0;
        switch (kind)
        {
            case "player-list":
                // The list is kept up to date from here on, as OpenWinter's server does after this request.
                _listening = true;
                _lastList = "";
                SendList();
                break;
            case "party-invite": send(new PartyInvite { Creature = Id("other") }); break;
            case "party-kick": send(new PartyKick { Creature = Id("other") }); break;
            case "party-leader": send(new PartyTransferLeadership { Creature = Id("other") }); break;
            // Leaving names the player's own creature.
            case "party-leave": send(new PartyLeave { Creature = Me?.ObjectId ?? world.Controlled }); break;
            case "party-answer":
                uint from = Id("from");
                send((d["answer"] is JsonValue a && a.TryGetValue<string>(out var answer) ? answer : "") switch
                {
                    "accept" => new PartyAcceptInvitation { Creature = from },
                    "ignore" => new PartyIgnoreInvitation { Creature = from },
                    _ => new PartyRejectInvitation { Creature = from },
                });
                break;
            case "pc-like":
                send(new PVPAttitudeChangeC2S { TargetOid = Id("other"), Attitude = d["like"] is JsonValue l && l.TryGetValue<bool>(out var like) && like });
                break;
        }
        return true;
    }

    // -- server to client ---------------------------------------------------

    /// <summary>Handles a server message that belongs here; false for everything else.</summary>
    public bool ToClient(NwnMessage message)
    {
        switch (message)
        {
            case PlayerListFull m:
                // The whole list, the player included; no lines: the original prints them for Adds only.
                _players.Clear();
                foreach (var p in m.Players) _players[p.PlayerId] = Read(p);
                break;
            case PlayerListAdd m:
                // The first Add is the player's own. A login brings two Adds; the line is printed for the first.
                if (_self == uint.MaxValue) _self = m.Player.PlayerId;
                var added = Read(m.Player);
                if (!_players.ContainsKey(added.PlayerId) && m.DisplayDmMsg)
                    Say(data.Text(added.IsDm ? JoinedDm : Joined, 0).Replace("<PlayerName>", added.PlayerName) + ".");
                _players[added.PlayerId] = added;
                break;
            case PlayerListDelete m:
                if (_players.Remove(m.PlayerId, out var gone))
                {
                    if (m.DisplayDmMsg) Say(data.Text(gone.IsDm ? LeftDm : Left, 0).Replace("<PlayerName>", gone.PlayerName) + ".");
                    _status.Remove(gone.ObjectId);
                    _members.Remove(gone.ObjectId);
                }
                _attitude.Remove(m.PlayerId);
                _reaction.Remove(m.PlayerId);
                break;
            case PartyList m:
                // Joining another's party: the status update that says so comes later, and a joiner never leads.
                if (_members.Count == 0 && m.Members.Count > 0) Restate(leader: false);
                _members.Clear();
                _members.AddRange(m.Members);
                break;
            case PartyListAdd m:
                if (_members.Count == 0) Restate(leader: true);
                foreach (var id in m.Members) if (!_members.Contains(id)) _members.Add(id);
                break;
            case PartyListRemove m:
                foreach (var id in m.Members) _members.Remove(id);
                break;
            case PVPAttitudeChangeS2C m:
                // The player's own attitude toward the other (is_source) or the other's toward her.
                (m.IsSource ? _attitude : _reaction)[m.OtherPlayerId] = m.Attitude;
                if (m.IsSource && _players.TryGetValue(m.OtherPlayerId, out var other))
                    Notify("pc-like", new JsonObject { ["other"] = other.ObjectId, ["like"] = m.Attitude });
                break;
            case PopupGuiPanelMessages.PopUpGUIPanelPopUpPartyInvitationReceivedPanel m:
                // The popup names the inviter only; her creature came with the feedback just before it.
                Invitation(_invitedBy, m.Inviter, true);
                return true;
            default:
                return false;
        }
        Changed();
        return true;
    }

    /// <summary>The players' party status in an object update.</summary>
    public void ObjectList(GameObjUpdateMessages.GameObjUpdateObjList list)
    {
        bool changed = false;
        foreach (var entry in list.Entries)
        {
            if (entry is not GameObjUpdateObjListUpdateCreature { PcPartyStatus: { } s } u) continue;
            // in_party is set for a player alone (a party of one) and clear in a party of several.
            var status = new Status(s.InParty, s.IsLeader, s.Invited && s.InvitedByValue is { } by ? by.InviterId : InvalidObject);
            if (_status.TryGetValue(u.ObjectId, out var was) && was == status) continue;
            _status[u.ObjectId] = status;
            changed = true;
            if (u.ObjectId == Me?.ObjectId && status.InvitedBy == InvalidObject && _invitedBy != InvalidObject)
                Invitation(_invitedBy, "", false); // answered, expired or withdrawn
        }
        if (changed) Changed();
    }

    /// <summary>The area's PvP setting (the player list's PvP zone).</summary>
    public void Area(AreaMessages.AreaClientArea area)
    {
        _areaPvp = area.Pvp;
        Changed();
    }

    /// <summary>Party and PvP feedback as the original's chat lines; false for other feedback.</summary>
    public bool Feedback(ClientSideMessageFeedback feedback)
    {
        string Line(uint strRef, uint about = InvalidObject) => data.Text(strRef, 0).Replace("<CUSTOM0>", NameOf(about));
        string? text = feedback.Payload.Value switch
        {
            FeedbackPartyNewLeader m => Line(NowLeader, m.Leader),
            FeedbackPartyMemberKicked m => Line(WasKicked, m.Member),
            FeedbackPartyKickedYou => Line(YouWereKicked),
            FeedbackPartyAlreadyConsidering m => Line(AlreadyConsidering, m.Target),
            FeedbackPartyAlreadyInvolved m => Line(InNegotiations, m.Target),
            FeedbackPartySentInvitation m => Line(SentInvitation, m.Invitee),
            FeedbackPartyReceivedInvitation m => Received(m.Inviter),
            FeedbackPartyJoined m => Line(JoinedParty, m.Member),
            FeedbackPartyInvitationIgnored m => Line(IgnoringYou, m.Invitee),
            FeedbackPartyYouIgnoredInvitation m => Answered(YouIgnore, m.Inviter),
            FeedbackPartyInvitationRejected m => Line(RejectedYours, m.Invitee),
            FeedbackPartyYouRejectedInvitation m => Answered(YouRejected, m.Inviter),
            // Sent to the invited player when the invitation runs out, naming the inviter; the window closes.
            FeedbackPartyInvitationExpired m => Answered(Expired, m.Invitee),
            FeedbackPartyLeftParty m => Line(LeftParty, m.Member),
            FeedbackPartyYouLeft => Line(YouLeft),
            FeedbackPartyCannotLeaveTheOneParty => Line(OnePartyCantLeave),
            FeedbackPartyCannotKickFromTheOneParty => Line(OnePartyCantKick),
            FeedbackPartyYouInvitedNonSingleton m => Line(AlreadyInParty, m.Target),
            FeedbackGuiOnlyPartyLeaderMayClick => Line(LeaderOnly),
            // The talk table's line ends in a line break, which the original's chat shows as an empty line.
            FeedbackPvpReactionDislikesYou m => Line(DislikesYou, m.Creature),
            _ => null,
        };
        if (text == null) return false;
        Say(text);
        return true;

        string Received(uint inviter)
        {
            _invitedBy = inviter;
            return Line(ReceivedInvitation, inviter);
        }

        string Answered(uint line, uint inviter)
        {
            Invitation(inviter, "", false);
            return Line(line, inviter);
        }
    }

    /// <summary>What the radial menu needs to know about another player (its party ring); null for anything else.</summary>
    public JsonObject? Facts(uint target)
    {
        if (PlayerOf(target) is not { IsDm: false } other || Me is not { } me || other.PlayerId == me.PlayerId) return null;
        bool same = _members.Contains(target), leader = Leading(me.ObjectId), oneParty = OneParty;
        return new JsonObject
        {
            ["player"] = true, ["sameParty"] = same, ["leader"] = leader, ["theirLeader"] = Leading(target),
            ["invitedMe"] = _invitedBy == target, ["invitedThem"] = InvitedBy(target) == me.ObjectId,
            ["canInvite"] = !same && leader && !oneParty, ["canKick"] = same && leader && !oneParty, ["canTransfer"] = same && leader,
            ["inParty"] = _members.Count > 0, ["oneParty"] = oneParty,
            ["dislike"] = _attitude.TryGetValue(other.PlayerId, out var like) && !like,
        };
    }

    // -- state ----------------------------------------------------------------

    Player? Me => _players.TryGetValue(_self, out var me) ? me : _players.Values.FirstOrDefault(p => p.ObjectId == world.Controlled);

    Player? PlayerOf(uint creature) => _players.Values.FirstOrDefault(p => p.ObjectId == creature);

    bool OneParty => settings()?.OnePartyOnly ?? false;

    /// <summary>
    /// Leads her party; a player alone leads her party of one. The party lists arrive before the status updates, so
    /// they decide who is alone: the player without other members, another player outside the player's party.
    /// </summary>
    bool Leading(uint creature)
    {
        bool mine = creature == Me?.ObjectId;
        if (mine && _members.Count == 0) return true;
        bool withMe = mine || _members.Contains(creature);
        return _status.TryGetValue(creature, out var s) ? s.Leader || s.Alone && !withMe : mine || !withMe;
    }

    /// <summary>The player's own status once she is no longer alone, ahead of the server's update.</summary>
    void Restate(bool leader)
    {
        if (Me is { } me && _status.TryGetValue(me.ObjectId, out var s)) _status[me.ObjectId] = s with { Alone = false, Leader = leader };
    }

    uint InvitedBy(uint creature) => _status.TryGetValue(creature, out var s) ? s.InvitedBy : InvalidObject;

    /// <summary>The PvP setting where the player is: the area's, never more than the server's (0 none, 1 party, 2 full).</summary>
    int Pvp
    {
        get
        {
            int server = settings()?.PvpSetting ?? 1;
            return _areaPvp is >= 0 and <= 2 ? Math.Min(_areaPvp, server) : server;
        }
    }

    string NameOf(uint creature) => PlayerOf(creature)?.Name is { Length: > 0 } name ? name : world.NameOf(creature);

    Player Read(PlayerListMessages.Player p)
    {
        string Text(NwnLocString s) => s.IsStrRef ? data.Text(s.StrRef, s.Gender) : s.Text;
        var pc = p.PrimaryPc;
        string name = pc == null ? "" : $"{Text(pc.FirstName)} {Text(pc.LastName)}".Trim();
        return new Player(p.PlayerId, p.ObjectId, name.Length > 0 ? name : p.PlayerName, p.PlayerName, p.IsDm, pc?.PortraitResref ?? "");
    }

    /// <summary>Opens or closes the invitation window ("party-invitation").</summary>
    void Invitation(uint from, string name, bool open)
    {
        if (!open && (from == InvalidObject || _invitedBy != from)) return;
        _invitedBy = open ? from : InvalidObject;
        if (name.Length == 0) name = NameOf(from);
        Notify("party-invitation", new JsonObject
        {
            ["from"] = from, ["name"] = name, ["open"] = open,
            ["text"] = open ? data.Text(InvitationText, 0).Replace("<CUSTOM0>", name) : "",
        });
        Changed();
    }

    /// <summary>Sends what changed: the "party" notify, the party bar's players and, once asked for, the list.</summary>
    void Changed()
    {
        if (Me is { } me)
        {
            uint leader = Leading(me.ObjectId) ? me.ObjectId : _members.FirstOrDefault(Leading, me.ObjectId);
            var party = new JsonObject
            {
                ["leader"] = leader,
                ["members"] = new JsonArray([.. new[] { me.ObjectId }.Concat(_members).Select(id => (JsonNode)new JsonObject
                {
                    ["id"] = id, ["pc"] = true, ["master"] = InvalidObject, ["associateType"] = 0,
                })]),
            };
            var json = party.ToJsonString();
            if (json != _lastParty)
            {
                _lastParty = json;
                Notify("party", party);
                PartyChanged?.Invoke([.. _members.Select(id => (id, NameOf(id), PlayerOf(id)?.Portrait ?? ""))]);
            }
        }
        if (_listening) SendList();
    }

    void SendList()
    {
        var json = List().ToJsonString();
        if (json == _lastList) return;
        _lastList = json;
        emit(new NotifyMessage { Kind = "player-list", Subject = world.Controlled, Json = json }.Write());
    }

    /// <summary>The "player-list" notify, as OpenWinter's server sends it (docs/notify/player.md).</summary>
    public JsonObject List()
    {
        var me = Me;
        var players = new JsonArray();
        foreach (var p in _players.Values.Where(p => !p.IsDm && p.PlayerId != me?.PlayerId).OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        {
            bool party = _members.Contains(p.ObjectId);
            bool alone = !_status.TryGetValue(p.ObjectId, out var s) || s.Alone;
            players.Add(new JsonObject
            {
                ["id"] = p.ObjectId, ["name"] = p.Name, ["party"] = party, ["leader"] = Leading(p.ObjectId),
                // The server lists the player's own party only: another party counts as two.
                ["partySize"] = party ? _members.Count + 1 : alone ? 1 : 2,
                ["dislike"] = _attitude.TryGetValue(p.PlayerId, out var like) && !like,
                ["dislikedBy"] = _reaction.TryGetValue(p.PlayerId, out var theirs) && !theirs,
                ["invitedMe"] = _invitedBy == p.ObjectId,
                ["invitedThem"] = me != null && InvitedBy(p.ObjectId) == me.ObjectId,
            });
        }
        int pvp = Pvp;
        return new JsonObject
        {
            ["players"] = players,
            ["leader"] = me == null || Leading(me.ObjectId),
            ["inParty"] = _members.Count > 0,
            ["oneParty"] = OneParty,
            ["invitedBy"] = _invitedBy == InvalidObject ? 0 : _invitedBy,
            ["dm"] = me?.IsDm == true,
            ["pvp"] = pvp,
            ["pvpText"] = data.Text(pvp switch { 2 => 8019u, 0 => 8020u, _ => 8018u }, 0),
        };
    }

    void Say(string text) => emit(new ChatMessage { Channel = ChatChannel.Server, Text = text }.Write());

    void Notify(string kind, JsonObject json) =>
        emit(new NotifyMessage { Kind = kind, Subject = world.Controlled, Json = json.ToJsonString() }.Write());
}
