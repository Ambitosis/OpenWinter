using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>One player on the player list, as the server's "player-list" notify gives her (docs/PARTY.md).</summary>
public sealed record PlayerListEntry(uint Id, string Name, bool Self, bool Party, bool Leader, bool Dislike, bool DislikedBy, bool InvitedMe, bool InvitedThem, int PartySize)
{
    /// <summary>Leads a party of more than one.</summary>
    public bool LeadsParty => Leader && PartySize > 1;
}

/// <summary>The player list's state: every player, the asker's standing and the PvP setting where she is.</summary>
public sealed record PlayerListState(IReadOnlyList<PlayerListEntry> Players, bool Leader, bool InParty, bool OneParty, uint InvitedBy, bool Dm, int Pvp, string PvpText)
{
    public static readonly PlayerListState Empty = new(Array.Empty<PlayerListEntry>(), true, false, false, 0, false, 1, "");

    public static PlayerListState Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var d = doc.RootElement;
        static bool B(JsonElement e, string n) => e.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.True;
        var players = d.GetProperty("players").EnumerateArray().Select(p => new PlayerListEntry(
            p.GetProperty("id").GetUInt32(), NwnText.Strip(p.GetProperty("name").GetString() ?? ""), B(p, "self"), B(p, "party"), B(p, "leader"),
            B(p, "dislike"), B(p, "dislikedBy"), B(p, "invitedMe"), B(p, "invitedThem"),
            p.TryGetProperty("partySize", out var size) ? size.GetInt32() : 1)).ToList();
        return new PlayerListState(players, B(d, "leader"), B(d, "inParty"), B(d, "oneParty"),
            d.TryGetProperty("invitedBy", out var ib) ? ib.GetUInt32() : 0, B(d, "dm"),
            d.TryGetProperty("pvp", out var pvp) ? pvp.GetInt32() : 1, d.TryGetProperty("pvpText", out var t) ? t.GetString() ?? "" : "");
    }

    // What the buttons may do with a selected player (the server checks again and explains a refusal).
    public bool CanInvite(PlayerListEntry? p) => p is { Self: false, Party: false, InvitedThem: false } && Leader && !OneParty;
    public bool CanKick(PlayerListEntry? p) => p is { Self: false, Party: true } && Leader && !OneParty;
    public bool CanTransfer(PlayerListEntry? p) => p is { Self: false, Party: true } && Leader;
    public bool CanLeave => InParty && !OneParty;
}

/// <summary>
/// The player list (P, the party bar's player list button): every player on the server with her party and leader,
/// your Attitude toward her (Like / Dislike, the original's PvP choice) and her Reaction to you, and the original's
/// party buttons for the selected player: Invite Into Party, Transfer Leadership, Remove From Party, Leave Party and
/// Tell (GUIEVENT_PLAYERLIST_PLAYER_TELL). Also shows party invitations ("You have been invited by X to join a party.",
/// dialog.tlk 66776) with Accept, Reject and "Never show this again." The classic interface draws its own list over
/// this one's state (ClassicPlayerList). Requests: "player-list", "pc-like", "party-*" (docs/PARTY.md).
/// </summary>
public partial class PlayerListPanel : UiTheme.Window
{
    public ClientSession Session = null!;
    VBoxContainer _rows = null!;
    Label _pvp = null!;
    HBoxContainer _buttons = null!;
    uint _selected;
    Control? _invitation;

    /// <summary>The player whose invitation is open (0 when none).</summary>
    public uint OpenInvitation { get; private set; }

    /// <summary>The last list the server sent.</summary>
    public PlayerListState State { get; private set; } = PlayerListState.Empty;
    /// <summary>A new list arrived (the classic list redraws).</summary>
    public event Action? StateChanged;
    /// <summary>An invitation arrived or closed: (from, name, text, open). Set by the classic interface to draw its own window.</summary>
    public Action<uint, string, string, bool>? InvitationOverride;
    /// <summary>Opens or closes another look's list instead of this window (the classic interface).</summary>
    public Action? ToggleOverride;

    public static PlayerListPanel Create(ClientSession session)
    {
        var p = Create<PlayerListPanel>("Players", new Vector2(520, 420), "party");
        p.Session = session;
        p.AnchorLeft = p.AnchorRight = 0.5f;
        p.AnchorTop = p.AnchorBottom = 0.5f;
        p.OffsetLeft = -260; p.OffsetRight = 260; p.OffsetTop = -210; p.OffsetBottom = 210;
        return p;
    }

    public override void _Ready()
    {
        _pvp = Ui.Text("", Ui.TextStyle.Caption, Palette.TextMuted);
        Body.AddChild(_pvp);
        var frame = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        frame.AddThemeStyleboxOverride("panel", Styles.Inset(6));
        Body.AddChild(frame);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        frame.AddChild(scroll);
        _rows = Ui.VBox(2);
        _rows.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_rows);
        _buttons = Ui.HBox(Palette.Space2);
        Body.AddChild(_buttons);
        Session.NotifyReceived += OnNotify;
        Fill();
    }

    public override void _ExitTree() => Session.NotifyReceived -= OnNotify;

    public void Toggle()
    {
        if (ToggleOverride != null) { ToggleOverride(); return; }
        if (Visible) { Close(); return; }
        Visible = true;
        Session.Request("player-list");
    }

    /// <summary>Asks the server for the list again.</summary>
    public void Refresh() => Session.Request("player-list");

    // ------------------------------------------------------------------ actions (shared with the classic list and the radial)

    public void Invite(uint id) => Session.Request("party-invite", new { other = id });
    public void Kick(uint id) => Session.Request("party-kick", new { other = id });
    public void Transfer(uint id) => Session.Request("party-leader", new { other = id });
    public void Leave() => Session.Request("party-leave");
    public void SetLike(uint id, bool like) => Session.Request("pc-like", new { other = id, like });
    public void Answer(uint from, string answer) => Session.Request("party-answer", new { from, answer });
    /// <summary>GUIEVENT_PLAYERLIST_PLAYER_CLICK (12) / _TELL (28) to the module's OnPlayerGuiEvent.</summary>
    public void GuiEvent(int type, uint id) => Session.Request("gui-event", new { type, @object = id });

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind == "player-list")
        {
            State = PlayerListState.Parse(m.Json);
            Fill();
            StateChanged?.Invoke();
        }
        else if (m.Kind == "party-invitation")
        {
            using var doc = JsonDocument.Parse(m.Json);
            var d = doc.RootElement;
            uint from = d.GetProperty("from").GetUInt32();
            string name = NwnText.Strip(d.GetProperty("name").GetString() ?? "");
            string text = d.GetProperty("text").GetString() ?? "";
            bool open = d.GetProperty("open").GetBoolean();
            OpenInvitation = open ? from : OpenInvitation == from ? 0 : OpenInvitation;
            // Keep the list current from now on (the server sends it again whenever it changes).
            if (open) Refresh();
            if (InvitationOverride != null) InvitationOverride(from, name, text, open);
            else ShowInvitation(from, text, open);
        }
    }

    /// <summary>Whether invitations open a window (the original's ui.party.invite-popup.enabled; "Never show this again.").</summary>
    public static bool InvitationPopups => Settings.Current.Gameplay.PartyInvitePopup;

    public static void NeverShowInvitations() => Settings.Update(s => s.Gameplay.PartyInvitePopup = false);

    void ShowInvitation(uint from, string text, bool open)
    {
        _invitation?.QueueFree();
        _invitation = null;
        if (!open || !InvitationPopups) return;
        var panel = new PanelContainer { Theme = GameTheme.Theme, ZIndex = 90, CustomMinimumSize = new Vector2(420, 0), Name = "PartyInvitation" };
        panel.AddThemeStyleboxOverride("panel", Styles.Window(20));
        Styles.ApplyGlass(panel, 0.92f);
        panel.AnchorLeft = panel.AnchorRight = 0.5f;
        panel.OffsetLeft = -210; panel.OffsetRight = 210; panel.OffsetTop = 120;
        var col = Ui.VBox(Palette.Space3);
        panel.AddChild(col);
        col.AddChild(Ui.Text(Tlk(6382, "Party"), Ui.TextStyle.Title));
        var body = Ui.Text(NwnText.Strip(text), Ui.TextStyle.Secondary);
        body.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        body.CustomMinimumSize = new Vector2(380, 0);
        col.AddChild(body);
        var never = new CheckBox { Text = Tlk(66777, "Never show this again."), FocusMode = FocusModeEnum.None };
        col.AddChild(never);
        var row = Ui.HBox(Palette.Space2);
        row.Alignment = BoxContainer.AlignmentMode.End;
        col.AddChild(row);
        void Done(string? answer)
        {
            if (never.ButtonPressed) NeverShowInvitations();
            if (answer != null) Answer(from, answer);
            panel.QueueFree();
            if (_invitation == panel) _invitation = null;
        }
        row.AddChild(Ui.Button(Tlk(66775, "Reject"), () => Done("reject")));
        row.AddChild(Ui.Button("Later", () => Done(null)));
        row.AddChild(Ui.Primary(Tlk(66774, "Accept"), () => Done("accept")));
        GetParent().AddChild(panel);
        _invitation = panel;
    }

    /// <summary>Answers the invitation window shown now, as its buttons do (captures, the multiplayer harness).</summary>
    public bool AnswerOpenInvitation(string answer)
    {
        if (OpenInvitation == 0) return false;
        Answer(OpenInvitation, answer);
        _invitation?.QueueFree();
        _invitation = null;
        return true;
    }

    string Tlk(int strref, string fallback) => Session.Content?.Resources.TlkString((uint)strref) is { Length: > 0 } s ? s : fallback;

    // ------------------------------------------------------------------ the window

    PlayerListEntry? Selected => State.Players.FirstOrDefault(p => p.Id == _selected);

    void Fill()
    {
        if (_rows == null) return;
        _pvp.Text = State.PvpText.Length > 0 ? $"{Tlk(8017, "PvP Zone")}: {State.PvpText}" : "";
        foreach (var c in _rows.GetChildren()) c.QueueFree();
        if (State.Players.All(p => p.Self))
        {
            var empty = Ui.Text("No other players are here.", Ui.TextStyle.Body, Palette.TextMuted);
            empty.HorizontalAlignment = HorizontalAlignment.Center;
            _rows.AddChild(Ui.Pad(empty, 12, 24, 12, 24));
        }
        foreach (var p in State.Players) _rows.AddChild(Row(p));
        FillButtons();
    }

    Control Row(PlayerListEntry p)
    {
        var bg = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        bg.AddThemeStyleboxOverride("panel", p.Id == _selected ? Styles.Box(Palette.Accent with { A = 0.16f }, 4, null, 0, 0, 0) : new StyleBoxEmpty());
        bg.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            _selected = p.Id;
            GuiEvent(12, p.Id);
            Fill();
        };
        var row = Ui.HBox(Palette.Space2);
        row.MouseFilter = MouseFilterEnum.Ignore;
        var name = Ui.Text(p.Self ? $"{p.Name} (you)" : p.Name, Ui.TextStyle.Body, p.Dislike || p.DislikedBy ? Palette.Hostile : Palette.Text);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        name.ClipText = true;
        name.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(name);
        if (p.LeadsParty) row.AddChild(Chip.Create(Tlk(66852, "Party Leader"), p.Party || p.Self ? Palette.Friendly : Palette.Neutral));
        else if (p.Party && !p.Self) row.AddChild(Chip.Create(Tlk(6382, "Party"), Palette.Friendly));
        if (p.InvitedMe) row.AddChild(Chip.Create("Invited you", Palette.Accent));
        if (p.InvitedThem) row.AddChild(Chip.Create("Invited", Palette.Neutral));
        if (!p.Self)
        {
            // Attitude: yours toward her; Reaction: hers toward you (dialog.tlk 7995 / 7996, 7999 Like / 8000 Dislike).
            var attitude = Ui.Button(p.Dislike ? Tlk(8000, "Dislike") : Tlk(7999, "Like"), () => SetLike(p.Id, p.Dislike), p.Dislike ? "" : "GhostButton");
            attitude.FocusMode = FocusModeEnum.None;
            attitude.TooltipText = $"{Tlk(7995, "Attitude")}: click to " + (p.Dislike ? "make peace" : "allow fighting where the PvP setting permits");
            attitude.CustomMinimumSize = new Vector2(84, 0);
            row.AddChild(attitude);
            var reaction = Ui.Text(p.DislikedBy ? Tlk(8000, "Dislike") : Tlk(7999, "Like"), Ui.TextStyle.Caption, p.DislikedBy ? Palette.Hostile : Palette.TextMuted);
            reaction.TooltipText = Tlk(7996, "Reaction");
            reaction.MouseFilter = MouseFilterEnum.Pass;
            reaction.CustomMinimumSize = new Vector2(60, 0);
            row.AddChild(reaction);
        }
        bg.AddChild(Ui.Pad(row, 8, 4, 8, 4));
        return bg;
    }

    void FillButtons()
    {
        foreach (var c in _buttons.GetChildren()) c.QueueFree();
        var s = Selected;
        void Add(string label, bool enabled, Action act)
        {
            var b = Ui.Button(label, act, "GhostButton");
            b.FocusMode = FocusModeEnum.None;
            b.Disabled = !enabled;
            _buttons.AddChild(b);
        }
        Add(Tlk(1067, "Invite Into Party"), State.CanInvite(s), () => { if (Selected is { } p) Invite(p.Id); });
        Add(Tlk(6383, "Transfer Leadership"), State.CanTransfer(s), () => { if (Selected is { } p) Transfer(p.Id); });
        Add(Tlk(1068, "Remove From Party"), State.CanKick(s), () => { if (Selected is { } p) Kick(p.Id); });
        Add(Tlk(1071, "Leave Party"), State.CanLeave, Leave);
        Add(Tlk(66779, "Tell Player"), s is { Self: false }, () => { if (Selected is { } p) GuiEvent(28, p.Id); });
    }
}
