using OpenWinter.Game.Objects;
using OpenWinter.Game.Society;

namespace OpenWinter.Game.Requests;

/// <summary>
/// The pause key and the player list (P): "pause" toggles the player pause of a single-player game;
/// "player-list" answers with a "player-list" notify of the players; "pc-like" {other, like} likes or
/// dislikes one of them (the original's Attitude buttons); "party-invite", "party-answer", "party-leave", "party-kick"
/// and "party-leader" manage parties (docs/PARTY.md). See docs/notify/player.md.
/// </summary>
static class PlayerListRequests
{
    /// <summary>
    /// The pause key toggles the player pause for everyone: always in single player (the local game, which keeps save
    /// games), for Dungeon Masters, and for players when the server allows it (server.allow-player-pausing). With
    /// "state" 1 or 0 it sets or clears it (the classic level-up window pauses single player while open, as the
    /// original does: "PAUSED" / "UNPAUSED", menu capture l00 / l10).
    /// </summary>
    [ClientRequest("pause")]
    static void Pause(Request r)
    {
        if (r.World.Output is not GameServer server) return;
        if (!server.Options.SaveGames && !server.Options.AllowPlayerPausing && r.Player.Player?.IsDM != true && !r.Player.IsDMAvatar)
        {
            server.SendMessage(r.Player, "Only a Dungeon Master can pause this game.");
            return;
        }
        int before = r.World.PauseState;
        int wanted = r.Int("state", -1);
        int next = wanted == 1 ? before | 2 : wanted == 0 ? before & ~2 : (before & 2) != 0 ? before & ~2 : before | 2;
        // NWNX_ON_INPUT_TOGGLE_PAUSE; a skip leaves the game as it is.
        var ev = Nwnx.NwnxEvent.Begin(r.World, "NWNX_ON_INPUT_TOGGLE_PAUSE", r.Player)?.Add("PAUSE_STATE", (next & 2) != 0);
        if (ev?.Before() == true) { ev.After(); return; }
        r.World.PauseState = next;
        ev?.After();
        if (r.World.PauseState != before || wanted < 0) r.World.NotifyAllPlayers("pause", new { state = r.World.PauseState });
    }

    [ClientRequest("player-list")]
    static void List(Request r)
    {
        PartyService.Of(r.World).ListenToPlayerList(r.Player);
        Send(r.World, r.Player);
    }

    [ClientRequest("pc-like")]
    static void Like(Request r)
    {
        if (r.Obj<Creature>("other") is not { IsPC: true } other || other == r.Player || other.IsDMAvatar || r.Player.IsDMAvatar) return;
        // NWNX_ON_PVP_ATTITUDE_CHANGE (ATTITUDE 1 like, 0 dislike); a skip keeps the attitude.
        var ev = Nwnx.NwnxEvent.Begin(r.World, "NWNX_ON_PVP_ATTITUDE_CHANGE", r.Player)?.Add("TARGET_OBJECT_ID", other).Add("ATTITUDE", r.Bool("like"));
        if (ev?.Before() == true) { ev.After(); return; }
        PlayerRelations.SetLike(r.World, r.Player, other, r.Bool("like"));
        ev?.After();
        PartyService.Of(r.World).ListenToPlayerList(r.Player);
        PartyService.Of(r.World).PlayerListChanged();
    }

    // ---- party management (docs/PARTY.md): the player list's buttons and the radial's Party ring

    [ClientRequest("party-invite")]
    static void Invite(Request r)
    {
        if (r.Obj<Creature>("other") is { IsPC: true } other) PartyService.Of(r.World).Invite(r.Player, other);
    }

    /// <summary>{from, answer = "accept" | "reject" | "ignore"}: the invitation window or the radial's Accept / Reject / Ignore.</summary>
    [ClientRequest("party-answer")]
    static void Answer(Request r)
    {
        if (r.Obj<Creature>("from") is { IsPC: true } from) PartyService.Of(r.World).Answer(r.Player, from, r.Str("answer"));
    }

    [ClientRequest("party-leave")]
    static void Leave(Request r) => PartyService.Of(r.World).LeaveByPlayer(r.Player);

    [ClientRequest("party-kick")]
    static void Kick(Request r)
    {
        if (r.Obj<Creature>("other") is { IsPC: true } other) PartyService.Of(r.World).Kick(r.Player, other);
    }

    [ClientRequest("party-leader")]
    static void Leader(Request r)
    {
        if (r.Obj<Creature>("other") is { IsPC: true } other) PartyService.Of(r.World).TransferLeadership(r.Player, other);
    }

    /// <summary>
    /// The "player-list" notify: the other players on the server, with their party, the leaders,
    /// each side's like or dislike (Attitude and Reaction) and open invitations, plus the asker's own standing and the
    /// PvP setting where she is (the classic list's PvPSettingLabel: dialog.tlk 8018 Party Protected, 8019 Full PvP,
    /// 8020 No PvP).
    /// </summary>
    public static void Send(World world, Creature player)
    {
        var talk = world.Resources.Talk;
        var party = PartyService.Of(world);
        var me = player.Player?.FamiliarMaster ?? player;
        uint invitedBy = party.InvitedBy(me);
        var mine = party.InvitationsFrom(me);
        // The other players: the original's list is empty with nobody else on the server (mp_partyui).
        var players = world.ConnectedPCs.Where(p => p != me && !p.IsDMAvatar && !p.IsDestroyed && p.Player?.FamiliarMaster == null).Select(p => new
        {
            id = p.Id,
            name = p.Name.Resolve(talk),
            party = party.SameParty(p, me),
            leader = party.Leader(party.Ensure(p))?.Id == p.Id,
            partySize = party.PartyOf(p).Count,
            dislike = me.Dislikes.Contains(p.Id),
            dislikedBy = p.Dislikes.Contains(me.Id),
            invitedMe = invitedBy == p.Id,
            invitedThem = mine.Contains(p.Id),
        }).OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase).ToList();
        var pvp = world.AreaPvp(me.Area);
        world.Output?.Notify("player-list", player, new
        {
            players,
            leader = party.IsLeader(me),
            inParty = party.PartyOf(me).Count > 1,
            oneParty = world.OneParty,
            invitedBy,
            dm = player.Player?.IsDM == true || player.IsDMAvatar,
            pvp = (int)pvp,
            pvpText = world.TlkStringOverridden(pvp switch { PvpSetting.Full => 8019, PvpSetting.None => 8020, _ => 8018 }),
        }, player);
    }
}
