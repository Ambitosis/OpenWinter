using System.Globalization;
using OpenWinter.Game.Nwnx.Plugins;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Society;

/// <summary>
/// PC parties. Every PC is always in a party (alone at first); each party is its own faction id from
/// <see cref="FactionTable.NewPartyFaction"/>, shared by the PCs in it and by all their associates.
/// The first PC in a party's member list is its leader.
/// <para>Party management (docs/PARTY.md): invitations (invite, accept, reject, ignore, expiry), leaving, kicking and
/// passing the leadership, with the original's talk-table lines to the players concerned, the server's One Party Only
/// setting, and NWNX_ON_PARTY_* events for modules that subscribe to them. The engine decides all of it; no stock
/// script takes part.</para>
/// </summary>
public sealed class PartyService : IWorldSystem
{
    /// <summary>
    /// Seconds a party invitation waits for an answer (TLK 6468 "The Party Invitation from X has already expired."):
    /// 30; the original drops it at the inviter's next heartbeat, 30 to about 37 s after it was
    /// sent (the heartbeat's lag and the invited player's feedback line at the lapse are not modelled). Not measured: two
    /// original clients can't share a server (docs/MULTIPLAYER_HARNESS.md).
    /// </summary>
    public const float InvitationLifetime = 30f;

    /// <summary>Seconds "Ignore Invitations from" blocks that player's invitations (TLK 6464 "for a short time"; not measured).</summary>
    public const float IgnoreTime = 300f;

    // dialog.tlk lines (docs/PARTY.md).
    public const int LeftParty = 6453, JoinedParty = 6454, NowLeader = 6455, AlreadyConsidering = 6456, InNegotiations = 6457,
        IgnoringYou = 6458, ReceivedInvitation = 6460, SentInvitation = 6462, YouIgnore = 6464, YouRejected = 6465,
        YouWereKicked = 6466, WasKicked = 6467, Expired = 6468, RejectedYours = 6469, YouLeft = 10427,
        OnePartyCantLeave = 66735, OnePartyCantKick = 66736, LeaderOnly = 66858, AlreadyInParty = 66891,
        InvitationText = 66776, DislikesYou = 7992;

    readonly World _world;
    /// <summary>Party faction → PC members in join order (leader first).</summary>
    readonly Dictionary<int, List<uint>> _parties = new();
    /// <summary>Pending invitations: invited player → (inviter, when it expires).</summary>
    readonly Dictionary<uint, (uint From, double Expires)> _invitations = new();
    /// <summary>(ignoring player, ignored inviter) → until when.</summary>
    readonly Dictionary<(uint, uint), double> _ignored = new();

    public PartyService(World world)
    {
        _world = world;
        world.ObjectAdded += o => { if (o is Creature { IsPC: true } c) Added(c); };
        world.ObjectRemoved += o => { if (o is Creature c) Removed(c); };
    }

    public void Step(float dt)
    {
        if (_invitations.Count == 0) return;
        foreach (var (to, inv) in _invitations.Where(kv => kv.Value.Expires <= _world.Time).ToList())
        {
            _invitations.Remove(to);
            if (_world.Get<Creature>(to) is { } invited) TellInvitation(invited, inv.From, open: false);
            PlayerListChanged();
        }
    }

    public static PartyService Of(World world) => world.System<PartyService>();

    /// <summary>Makes sure a PC is in a party (creating a party of one if needed) and returns the party faction.</summary>
    public int Ensure(Creature pc)
    {
        if (pc.Player?.FamiliarMaster is { } master && master != pc) return Ensure(master);
        if (!pc.IsPC) return pc.Faction;
        if (FactionTable.IsPartyFaction(pc.Faction) && _parties.TryGetValue(pc.Faction, out var list) && list.Contains(pc.Id))
            return pc.Faction;
        foreach (var other in _parties.Values) other.Remove(pc.Id);
        int f = _world.Factions.NewPartyFaction();
        _parties[f] = new List<uint> { pc.Id };
        MoveWithAssociates(pc, f);
        return f;
    }

    /// <summary>PCs in a party faction (leader first); empty for NPC factions.</summary>
    public IReadOnlyList<Creature> Members(int faction) =>
        _parties.TryGetValue(faction, out var list)
            ? list.Select(id => _world.Get<Creature>(id)).Where(c => c != null && !c.IsDestroyed).Cast<Creature>().ToList()
            : Array.Empty<Creature>();

    /// <summary>The PCs in a player's party (leader first), the player herself included.</summary>
    public IReadOnlyList<Creature> PartyOf(Creature pc) => Members(Ensure(pc));

    /// <summary>The PC leading a party faction, or null for NPC factions.</summary>
    public Creature? Leader(int faction) => Members(faction).FirstOrDefault();

    /// <summary>Whether a player leads her party (a player alone leads her party of one).</summary>
    public bool IsLeader(Creature pc) => Leader(Ensure(pc)) == Real(pc);

    /// <summary>Whether two players are in the same party.</summary>
    public bool SameParty(Creature a, Creature b) => Ensure(a) == Ensure(b);

    /// <summary>Who invited this player, while the invitation lasts (OBJECT_INVALID when nobody).</summary>
    public uint InvitedBy(Creature pc) =>
        _invitations.TryGetValue(Real(pc).Id, out var inv) && inv.Expires > _world.Time ? inv.From : GameObject.InvalidId;

    /// <summary>The players a player has invited whose invitations are still open.</summary>
    public IReadOnlyList<uint> InvitationsFrom(Creature pc)
    {
        uint id = Real(pc).Id;
        return _invitations.Where(kv => kv.Value.From == id && kv.Value.Expires > _world.Time).Select(kv => kv.Key).ToList();
    }

    // ------------------------------------------------------------------ membership (scripts and the actions below)

    /// <summary>AddToParty: <paramref name="pc"/> leaves its party and joins <paramref name="leaderOrMember"/>'s.</summary>
    public bool Join(Creature pc, Creature leaderOrMember)
    {
        pc = Real(pc);
        leaderOrMember = Real(leaderOrMember);
        if (!pc.IsPC || !leaderOrMember.IsPC || pc == leaderOrMember || pc.IsDMAvatar || leaderOrMember.IsDMAvatar) return false;
        int target = Ensure(leaderOrMember);
        int old = Ensure(pc);
        if (old == target) return false;
        bool wasLeader = _parties[old].Count > 1 && _parties[old][0] == pc.Id;
        RemoveMember(old, pc.Id);
        _parties[target].Add(pc.Id);
        MoveWithAssociates(pc, target);
        _invitations.Remove(pc.Id);
        Announce(old);
        Announce(target);
        // Leaving a party of several to join another: the old party is told as for a leaving member.
        var rest = Members(old);
        foreach (var member in rest) Tell(member, LeftParty, pc);
        if (wasLeader && rest.Count > 1) foreach (var member in rest) Tell(member, NowLeader, rest[0]);
        // "X has joined the party." to everyone in it, the newcomer too.
        foreach (var member in Members(target)) Tell(member, JoinedParty, pc);
        PlayerListChanged();
        return true;
    }

    /// <summary>RemoveFromParty and Leave Party: <paramref name="pc"/> leaves its party and starts a party of its own.</summary>
    public bool Leave(Creature pc) => Depart(Real(pc), kicked: false);

    /// <summary>Makes <paramref name="pc"/> the leader of its party (Transfer Leadership; integrations).</summary>
    public void SetLeader(Creature pc)
    {
        pc = Real(pc);
        int f = Ensure(pc);
        var list = _parties[f];
        if (list.Count > 0 && list[0] == pc.Id) return;
        list.Remove(pc.Id);
        list.Insert(0, pc.Id);
        Announce(f);
        if (list.Count > 1)
            foreach (var member in Members(f)) Tell(member, NowLeader, pc);
        PlayerListChanged();
    }

    bool Depart(Creature pc, bool kicked)
    {
        if (!pc.IsPC) return false;
        int old = Ensure(pc);
        if (_parties[old].Count <= 1) return false;
        bool wasLeader = _parties[old][0] == pc.Id;
        RemoveMember(old, pc.Id);
        int f = _world.Factions.NewPartyFaction();
        _parties[f] = new List<uint> { pc.Id };
        MoveWithAssociates(pc, f);
        Announce(old);
        Announce(f);
        var rest = Members(old);
        Tell(pc, kicked ? YouWereKicked : YouLeft, null);
        foreach (var member in rest) Tell(member, kicked ? WasKicked : LeftParty, pc);
        // The leadership passes to the member who joined first (not measured).
        if (wasLeader && rest.Count > 1)
            foreach (var member in rest) Tell(member, NowLeader, rest[0]);
        PlayerListChanged();
        return true;
    }

    void RemoveMember(int faction, uint id)
    {
        if (!_parties.TryGetValue(faction, out var list)) return;
        list.Remove(id);
        if (list.Count == 0) _parties.Remove(faction);
    }

    /// <summary>
    /// A PC entered the world: a party of her own, or, with One Party Only, the party everyone else is in (the
    /// original's server default, server.one-party-only; how a player is put into it is not measured).
    /// </summary>
    void Added(Creature c)
    {
        int own = Ensure(c);
        if (_world.OneParty && !c.IsDMAvatar && c.Player?.FamiliarMaster == null
            && _world.ConnectedPCs.FirstOrDefault(p => p != c && !p.IsDMAvatar && p.Player?.FamiliarMaster == null && Ensure(p) != own) is { } other)
            Join(c, other);
        else PlayerListChanged();
    }

    void Removed(Creature c)
    {
        _invitations.Remove(c.Id);
        foreach (var key in _invitations.Where(kv => kv.Value.From == c.Id).Select(kv => kv.Key).ToList())
        {
            _invitations.Remove(key);
            if (_world.Get<Creature>(key) is { } invited) TellInvitation(invited, c.Id, open: false);
        }
        if (!FactionTable.IsPartyFaction(c.Faction) || !_parties.TryGetValue(c.Faction, out var list) || !list.Contains(c.Id)) return;
        bool wasLeader = list[0] == c.Id;
        int faction = c.Faction;
        RemoveMember(faction, c.Id);
        Announce(faction);
        // A player leaving the server leaves her party: the others are told, and of a new leader (not measured).
        var rest = Members(faction);
        foreach (var member in rest) Tell(member, LeftParty, c);
        if (wasLeader && rest.Count > 1)
            foreach (var member in rest) Tell(member, NowLeader, rest[0]);
        PlayerListChanged();
    }

    /// <summary>Moves a creature and every creature it is master of (recursively) into a faction.</summary>
    void MoveWithAssociates(Creature master, int faction)
    {
        master.Faction = faction;
        foreach (var a in AssociateService.AllAssociates(_world, master))
            if (a != master) MoveWithAssociates(a, faction);
    }

    /// <summary>Tells each PC in a party who is in it (notify kind "party").</summary>
    void Announce(int faction)
    {
        if (_world.Output == null) return;
        var pcs = Members(faction);
        if (pcs.Count == 0) return;
        var members = _world.AllObjects.OfType<Creature>().Where(c => c.Faction == faction && !c.IsDestroyed)
            .Select(c => new { id = c.Id, pc = c.IsPC, master = c.Master, associateType = c.AssociateType }).ToList();
        var data = new { leader = pcs[0].Id, members };
        foreach (var pc in pcs)
        {
            var recipient = pc.FamiliarController?.Creature ?? pc;
            _world.Output.Notify("party", recipient, data, recipient);
        }
    }

    /// <summary>Re-sends the roster of the party a creature belongs to (after associates join or leave).</summary>
    public void Refresh(Creature anyMember)
    {
        if (FactionTable.IsPartyFaction(anyMember.Faction)) Announce(anyMember.Faction);
    }

    // ------------------------------------------------------------------ players' party actions (player list, radial)

    /// <summary>
    /// Invite Into Party: only a party's leader (or a player alone) may invite, and only a player who is alone. The
    /// invited player is told (and shown the invitation window, ui.party.invite-popup), the inviter too.
    /// </summary>
    public bool Invite(Creature inviter, Creature invited)
    {
        inviter = Real(inviter);
        invited = Real(invited);
        if (!Valid(inviter) || !Valid(invited) || inviter == invited || SameParty(inviter, invited)) return false;
        if (!IsLeader(inviter)) { Tell(inviter, LeaderOnly, null); return false; }
        if (PartyOf(invited).Count > 1) { Tell(inviter, AlreadyInParty, invited); return false; }
        if (_ignored.TryGetValue((invited.Id, inviter.Id), out var until) && until > _world.Time) { Tell(inviter, IgnoringYou, invited); return false; }
        uint pending = InvitedBy(invited);
        if (pending != GameObject.InvalidId)
        {
            if (pending != inviter.Id) Tell(inviter, AlreadyConsidering, invited);
            return false;
        }
        // Someone with an open invitation of her own, either way, is busy negotiating.
        if (InvitationsFrom(invited).Count > 0 || InvitedBy(inviter) != GameObject.InvalidId) { Tell(inviter, InNegotiations, invited); return false; }
        if (Before("INVITE", inviter, ("INVITED", invited))) return false;
        _invitations[invited.Id] = (inviter.Id, _world.Time + InvitationLifetime);
        Tell(inviter, SentInvitation, invited);
        Tell(invited, ReceivedInvitation, inviter);
        TellInvitation(invited, inviter.Id, open: true);
        After("INVITE", inviter, ("INVITED", invited));
        PlayerListChanged();
        return true;
    }

    /// <summary>Accept, reject or ignore an invitation ("accept" / "reject" / "ignore").</summary>
    public bool Answer(Creature invited, Creature inviter, string answer)
    {
        invited = Real(invited);
        inviter = Real(inviter);
        if (!Valid(invited)) return false;
        if (!_invitations.TryGetValue(invited.Id, out var inv) || inv.From != inviter.Id || inv.Expires <= _world.Time)
        {
            if (answer == "accept") Tell(invited, Expired, inviter);
            if (inv.From == inviter.Id) _invitations.Remove(invited.Id);
            TellInvitation(invited, inviter.Id, open: false);
            return false;
        }
        string evt = answer switch { "accept" => "ACCEPT_INVITATION", "ignore" => "IGNORE_INVITATION", _ => "REJECT_INVITATION" };
        var data = answer == "accept"
            ? new[] { ("INVITED_BY", inviter), ("PARTY_LEADER", Leader(Ensure(inviter)) ?? inviter) }
            : new[] { ("INVITED_BY", inviter) };
        if (Before(evt, invited, data)) return false;
        _invitations.Remove(invited.Id);
        TellInvitation(invited, inviter.Id, open: false);
        switch (answer)
        {
            case "accept":
                Join(invited, inviter);
                break;
            case "ignore":
                _ignored[(invited.Id, inviter.Id)] = _world.Time + IgnoreTime;
                Tell(invited, YouIgnore, inviter);
                Tell(inviter, IgnoringYou, invited);
                break;
            default:
                Tell(invited, YouRejected, inviter);
                Tell(inviter, RejectedYours, invited);
                break;
        }
        After(evt, invited, data);
        PlayerListChanged();
        return true;
    }

    /// <summary>Leave Party from the player list or radial (refused on a One Party Only server, TLK 66735).</summary>
    public bool LeaveByPlayer(Creature pc)
    {
        pc = Real(pc);
        if (!Valid(pc) || PartyOf(pc).Count <= 1) return false;
        if (_world.OneParty) { Tell(pc, OnePartyCantLeave, null); return false; }
        if (Before("LEAVE", pc, ("LEAVING", pc))) return false;
        bool done = Leave(pc);
        After("LEAVE", pc, ("LEAVING", pc));
        return done;
    }

    /// <summary>Remove From Party: the leader removes a member (TLK 66858 for anyone else, 66736 on One Party Only).</summary>
    public bool Kick(Creature leader, Creature member)
    {
        leader = Real(leader);
        member = Real(member);
        if (!Valid(leader) || !Valid(member) || leader == member || !SameParty(leader, member)) return false;
        if (!IsLeader(leader)) { Tell(leader, LeaderOnly, null); return false; }
        if (_world.OneParty) { Tell(leader, OnePartyCantKick, null); return false; }
        if (Before("KICK", leader, ("KICKED", member))) return false;
        bool done = Depart(member, kicked: true);
        After("KICK", leader, ("KICKED", member));
        return done;
    }

    /// <summary>Transfer Leadership: the leader passes it to another member (TLK 66858 for anyone else).</summary>
    public bool TransferLeadership(Creature leader, Creature member)
    {
        leader = Real(leader);
        member = Real(member);
        if (!Valid(leader) || !Valid(member) || leader == member || !SameParty(leader, member)) return false;
        if (!IsLeader(leader)) { Tell(leader, LeaderOnly, null); return false; }
        if (Before("TRANSFER_LEADERSHIP", leader, ("NEW_LEADER", member))) return false;
        SetLeader(member);
        After("TRANSFER_LEADERSHIP", leader, ("NEW_LEADER", member));
        return true;
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>The player behind a possessed familiar (parties belong to the player's own creature).</summary>
    static Creature Real(Creature c) => c.Player?.FamiliarMaster is { } master ? master : c;

    static bool Valid(Creature c) => c.IsPC && !c.IsDestroyed && !c.IsDMAvatar;

    /// <summary>A talk-table line to one player, &lt;CUSTOM0&gt; being another's name.</summary>
    void Tell(Creature pc, int strref, Creature? about)
    {
        if (_world.Output == null) return;
        string text = _world.TlkStringOverridden(strref);
        if (about != null) text = text.Replace("<CUSTOM0>", Name(about));
        _world.Output.SendMessage(pc, text);
    }

    string Name(Creature c) => c.DisplayName(_world.Resources.Talk) is { Length: > 0 } n ? n : c.Tag;

    void TellInvitation(Creature invited, uint from, bool open)
    {
        var inviter = _world.Get<Creature>(from);
        string name = inviter != null ? Name(inviter) : "";
        _world.Output?.Notify("party-invitation", invited,
            new { from, name, open, text = open ? _world.TlkStringOverridden(InvitationText).Replace("<CUSTOM0>", name) : "" }, invited);
    }

    /// <summary>Players whose clients asked for the player list: they are kept up to date from then on.</summary>
    readonly HashSet<uint> _listListeners = new();

    /// <summary>A client asked for the player list (it is sent again whenever it changes).</summary>
    public void ListenToPlayerList(Creature pc) => _listListeners.Add(pc.Id);

    /// <summary>Every player's player list may have changed: tells each player whose client keeps one.</summary>
    public void PlayerListChanged()
    {
        if (_world.Output == null || _listListeners.Count == 0) return;
        _listListeners.RemoveWhere(id => _world.Get<Creature>(id) is not { IsPC: true });
        foreach (var id in _listListeners.ToList())
            if (_world.Get<Creature>(id) is { } pc) Requests.PlayerListRequests.Send(_world, pc);
    }

    /// <summary>NWNX_ON_PARTY_&lt;evt&gt;_BEFORE: true when a subscriber skipped the action. OBJECT_SELF is the player acting.</summary>
    bool Before(string evt, Creature self, params (string Key, Creature Value)[] data) =>
        EventsPlugin.Signal(_world, $"NWNX_ON_PARTY_{evt}_BEFORE", self, EventData(data)).Skipped;

    void After(string evt, Creature self, params (string Key, Creature Value)[] data) =>
        EventsPlugin.Signal(_world, $"NWNX_ON_PARTY_{evt}_AFTER", self, EventData(data));

    /// <summary>Objects as NWNX_Events writes them (ObjectToString: hex).</summary>
    static Dictionary<string, string> EventData((string Key, Creature Value)[] data) =>
        data.ToDictionary(x => x.Key, x => x.Value.Id.ToString("x", CultureInfo.InvariantCulture));

    /// <summary>What the radial and the player list need to know about another player (target facts "party").</summary>
    public object Facts(Creature viewer, Creature other)
    {
        viewer = Real(viewer);
        other = Real(other);
        bool player = Valid(other) && Valid(viewer) && other != viewer;
        bool same = player && SameParty(viewer, other);
        bool leader = IsLeader(viewer);
        return new
        {
            player,
            sameParty = same,
            leader,
            theirLeader = player && IsLeader(other),
            invitedMe = player && InvitedBy(viewer) == other.Id,
            invitedThem = player && InvitedBy(other) == viewer.Id,
            canInvite = player && !same && leader && !_world.OneParty,
            canKick = same && leader && !_world.OneParty,
            canTransfer = same && leader,
            inParty = PartyOf(viewer).Count > 1,
            oneParty = _world.OneParty,
            dislike = viewer.Dislikes.Contains(other.Id),
        };
    }
}
