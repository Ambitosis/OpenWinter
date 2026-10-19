using System.Globalization;
using System.Numerics;
using OpenWinter.Game.Nwnx.Plugins;
using OpenWinter.Game.Objects;
using OpenWinter.Net;

namespace OpenWinter.Game.DungeonMaster;

/// <summary>
/// Dungeon Masters: DM login, the DM avatar (undetectable by players and creatures until it
/// "appears"), possession of other creatures, limbo, muted players and creatures whose AI a
/// DM switched off. DM tool actions are in <see cref="DMActions"/>, the creator in
/// <see cref="DMCreator"/>, and client requests in <see cref="DMRequests"/>.
///
/// <para>Script view (nwscript.nss): GetIsDM is TRUE for the avatar (and player-DMs), FALSE for a
/// DM-possessed creature; GetIsDMPossessed is TRUE for the possessed creature; GetIsPC is TRUE for
/// both, because a player controls them. DMs log in through the module's OnClientEnter like players.</para>
///
/// <para>NWNX: DM actions raise NWNX_Events-style <c>NWNX_ON_DM_*_BEFORE/_AFTER</c> events with
/// OBJECT_SELF = the DM avatar, so PW scripts subscribed through NWNX_Events can log or veto them
/// (SkipEvent on a BEFORE event cancels the action).</para>
/// </summary>
public sealed class DungeonMasterSystem : IWorldSystem
{
    readonly World _world;
    readonly HashSet<PlayerSession> _muted = new();
    readonly List<uint> _limbo = new();

    public Palettes Palettes { get; }
    public int Order => 50;

    public DungeonMasterSystem(World world)
    {
        _world = world;
        Palettes = new Palettes(world);
        world.ObjectRemoved += OnRemoved;
    }

    GameServer? Server => _world.Output as GameServer;

    /// <summary>Connected DM sessions.</summary>
    public IEnumerable<PlayerSession> DMSessions => Server?.Sessions.Where(s => s.IsDM) ?? Enumerable.Empty<PlayerSession>();

    /// <summary>
    /// DM login check. <paramref name="serverPassword"/> null: the server takes no DMs;
    /// empty: any DM may log in (local games); otherwise the passwords must match.
    /// </summary>
    public static bool PasswordAccepted(string? serverPassword, string given) =>
        serverPassword != null && (serverPassword.Length == 0 || string.Equals(serverPassword, given, StringComparison.Ordinal));

    // ------------------------------------------------------------------ avatar

    /// <summary>Turns a DM session's character into its DM avatar (before OnClientEnter runs).</summary>
    public void MakeAvatar(PlayerSession s, Creature c)
    {
        c.DMOwner = s;
        c.DMHidden = true;
        // DM avatars can't be harmed.
        c.Plot = true;
        c.IsImmortal = true;
        if (c.Template.Fields.Count == 0)
        {
            // No character file: a plain human figure.
            c.AppearanceType = 6;
            for (int i = 0; i < c.BodyParts.Length; i++) c.BodyParts[i] = 1;
            if (c.Name.FirstString.Length == 0) c.Name = new Aurora.Formats.LocString("Dungeon Master");
        }
    }

    /// <summary>The DM's own avatar (also while possessing another creature).</summary>
    public Creature? Avatar(PlayerSession s) =>
        s.Creature is { } c && c.DMOwner == s ? c
        : s.Creature is { DMPossessor: var id } && id != GameObject.InvalidId ? _world.Get<Creature>(id)
        : null;

    /// <summary>"Appear" / "Disappear": whether players can see and perceive the DM avatar.</summary>
    public void SetHidden(PlayerSession s, bool hidden)
    {
        if (Avatar(s) is not { } avatar || avatar.DMHidden == hidden) return;
        string evt = hidden ? "DISAPPEAR" : "APPEAR";
        if (Before(avatar, evt, new())) return;
        avatar.DMHidden = hidden;
        _world.MarkChanged(avatar);
        After(avatar, evt, new());
        NotifyState(s);
    }

    /// <summary>Tells a DM client its DM state (avatar, possessed creature, hidden).</summary>
    public void NotifyState(PlayerSession s)
    {
        if (!s.IsDM || s.Creature == null) return;
        var avatar = Avatar(s);
        var possessed = s.Creature != avatar ? s.Creature : null;
        _world.Output?.Notify("dm-state", s.Creature, new
        {
            dm = true,
            avatar = avatar?.Id ?? GameObject.InvalidId,
            possessed = possessed?.Id ?? GameObject.InvalidId,
            possessedName = possessed?.Name.Resolve(_world.Resources.Talk) ?? "",
            hidden = avatar?.DMHidden ?? true,
        }, s.Creature);
    }

    // ------------------------------------------------------------------ possession

    /// <summary>
    /// Possesses a creature: the DM's input now drives it, it speaks with the DM's chat, and the
    /// client's camera follows it. Only creatures nobody else controls, in the DM's area.
    /// </summary>
    public bool Possess(PlayerSession s, Creature target)
    {
        if (!s.IsDM || Avatar(s) is not { } avatar) return false;
        if (target == avatar) { Unpossess(s); return true; }
        if (target.IsDestroyed || target.Player != null || target.IsDMPossessed || target.DMOwner != null || target.Area == null) return false;
        if (target.Area != s.Creature?.Area) return false;
        if (Before(avatar, "POSSESS", Targets(target))) return false;
        if (s.Creature != avatar) Release(s, avatar, jumpBack: false);

        avatar.Player = null;
        avatar.MoveDirection = Vector3.Zero;
        ActionQueues.Clear(_world, avatar);
        ActionQueues.Clear(_world, target);
        target.MoveDirection = Vector3.Zero;
        target.DMPossessor = avatar.Id;
        target.Player = s;
        s.Creature = target;
        s.Send(new ControlObjectMessage { ObjectId = target.Id }.Write());
        _world.MarkChanged(target);
        After(avatar, "POSSESS", Targets(target));
        NotifyState(s);
        return true;
    }

    /// <summary>Returns control to the DM avatar.</summary>
    public void Unpossess(PlayerSession s)
    {
        if (Avatar(s) is not { } avatar || s.Creature == avatar) return;
        Release(s, avatar, jumpBack: true);
        NotifyState(s);
    }

    void Release(PlayerSession s, Creature avatar, bool jumpBack)
    {
        if (s.Creature is { } possessed && possessed != avatar)
        {
            possessed.Player = null;
            possessed.DMPossessor = GameObject.InvalidId;
            possessed.MoveDirection = Vector3.Zero;
            ActionQueues.Clear(_world, possessed);
            _world.MarkChanged(possessed);
            // The possessed creature may have walked into another area: the avatar follows.
            if (jumpBack && !possessed.IsDestroyed && possessed.Area != null && possessed.Area != avatar.Area)
            {
                avatar.Player = s;
                s.Creature = avatar;
                _world.Jump(avatar, possessed.Location);
            }
        }
        avatar.Player = s;
        s.Creature = avatar;
        if (avatar.Area == null && s.Area != null) _world.AddToArea(avatar, s.Area, avatar.Position, avatar.Facing);
        s.Send(new ControlObjectMessage { ObjectId = avatar.Id }.Write());
        _world.MarkChanged(avatar);
    }

    /// <summary>A DM leaving: let go of a possessed creature so the avatar, not it, leaves the game.</summary>
    public void BeforeDisconnect(PlayerSession s)
    {
        _muted.Remove(s);
        if (s.IsDM && Avatar(s) is { } avatar && s.Creature != avatar) Release(s, avatar, jumpBack: false);
    }

    void OnRemoved(GameObject o)
    {
        // A possessed creature that dies away or is destroyed hands control back.
        if (o is Creature { DMPossessor: var id } c && id != GameObject.InvalidId && c.Player is { } s && o.IsDestroyed)
            if (_world.Get<Creature>(id) is { } avatar) { Release(s, avatar, jumpBack: false); NotifyState(s); }
    }

    // ------------------------------------------------------------------ limbo

    /// <summary>Creatures a DM put in limbo (out of every area, kept for later).</summary>
    public IReadOnlyList<uint> Limbo => _limbo;

    /// <summary>Takes a creature out of the world into limbo (GetArea becomes OBJECT_INVALID). Not players.</summary>
    public bool SendToLimbo(GameObject o)
    {
        if (o is not Creature c || c.IsPC || c.DMOwner != null || c.Area == null) return false;
        ActionQueues.Clear(_world, c);
        c.MoveDirection = Vector3.Zero;
        _world.LiftFromArea(c);
        if (!_limbo.Contains(c.Id)) _limbo.Add(c.Id);
        return true;
    }

    /// <summary>Brings a creature back from limbo to a location.</summary>
    public bool FromLimbo(uint id, Location to)
    {
        if (!_limbo.Contains(id) || _world.Get<Creature>(id) is not { } c || _world.Get(to.Area) is not Areas.Area area) return false;
        _limbo.Remove(id);
        _world.AddToArea(c, area, to.Position, to.FacingDegrees * MathF.PI / 180f);
        return true;
    }

    // ------------------------------------------------------------------ players

    public bool IsMuted(PlayerSession s) => _muted.Contains(s);
    public void SetMuted(PlayerSession s, bool muted) { if (muted) _muted.Add(s); else _muted.Remove(s); }

    /// <summary>Sends text to every DM (SendMessageToAllDMs, DM channel notices).</summary>
    public void MessageDMs(string text)
    {
        var msg = new ChatMessage { Channel = ChatChannel.Server, Text = text }.Write();
        foreach (var s in DMSessions) s.Send(msg);
    }

    // ------------------------------------------------------------------ AI switched off

    public void Step(float dt)
    {
        foreach (var area in _world.Areas)
            foreach (var o in area.Objects)
                if (o is Creature { DMAIOff: true, Player: null } c && (c.Actions.Count > 0 || c.MoveDirection != Vector3.Zero))
                {
                    ActionQueues.Clear(_world, c);
                    c.MoveDirection = Vector3.Zero;
                }
    }

    // ------------------------------------------------------------------ NWNX DM action events

    /// <summary>Runs NWNX_ON_DM_&lt;evt&gt;_BEFORE; true if a subscriber skipped the action.</summary>
    public bool Before(Creature dm, string evt, Dictionary<string, string> data) =>
        EventsPlugin.Signal(_world, $"NWNX_ON_DM_{evt}_BEFORE", dm, data).Skipped;

    public void After(Creature dm, string evt, Dictionary<string, string> data) =>
        EventsPlugin.Signal(_world, $"NWNX_ON_DM_{evt}_AFTER", dm, data);

    /// <summary>Object ids as NWNX event data writes them (ObjectToString: hex).</summary>
    public static string Hex(uint id) => id.ToString("x", CultureInfo.InvariantCulture);

    /// <summary>Event data for actions on a group of targets: NUM_TARGETS, TARGET_1...</summary>
    public static Dictionary<string, string> Targets(params GameObject[] targets)
    {
        var d = new Dictionary<string, string> { ["NUM_TARGETS"] = targets.Length.ToString(CultureInfo.InvariantCulture) };
        for (int i = 0; i < targets.Length; i++) d["TARGET_" + (i + 1)] = Hex(targets[i].Id);
        return d;
    }
}
