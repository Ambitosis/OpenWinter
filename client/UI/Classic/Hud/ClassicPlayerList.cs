using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's player list (ingame_pvp.gui on pnl_ig_pvp), opened by the party bar's player list button and P:
/// the title "Player List", the PvP setting where the player is (PvPSettingLabel), the Tell, Leave, Invite, Transfer
/// Leadership, Boot and Ban icon buttons (ctl_ig_btn_icon; the original's icons are ir_chat, ir_leave, ir_invite and
/// ir_transleader, with Boot and Ban empty for a player), and the list the engine fills under the Player / Attitude /
/// Reaction headings, with a scroll bar; the window opens at the top left (measured with one client, mp_partyui). Clicking a name selects it (GUIEVENT_PLAYERLIST_PLAYER_CLICK); clicking an Attitude switches Like and
/// Dislike. Party invitations open party_invitation.gui (Accept, Reject, Cancel, "Never show this again.").
/// The list holds the other players only (empty with nobody else on the server). What a row shows is inferred from
/// the strings and the layout, not measured (docs/PARTY.md). The state and the requests are the modern <see cref="PlayerListPanel"/>'s.
/// </summary>
public partial class ClassicPlayerList : ClassicWindow
{
    PlayerListPanel Model => Ctx.Players;
    Rows _rows = null!;
    ClassicScrollBar _scroll = null!;
    uint _selected;

    public static ClassicPlayerList? Current { get; private set; }

    public static void Attach(ClassicContext ctx)
    {
        if (ctx.Assets.Layout("ingame_pvp") == null) return;
        // The original opens it at the screen's top left (mp_partyui).
        var list = new ClassicPlayerList { Ctx = ctx, Name = "ClassicPlayerList", Anchor = LayoutPreset.TopLeft };
        ctx.Hud.AddChild(list);
        Current = list;
        ctx.Players.ToggleOverride = list.Toggle;
        ctx.Players.InvitationOverride = list.Invitation;
        ctx.Players.StateChanged += list.Changed;
        if (ctx.Players.Visible) ctx.Players.Close();
    }

    public static void Detach(ClassicContext ctx)
    {
        ctx.Players.ToggleOverride = null;
        ctx.Players.InvitationOverride = null;
        if (Current is { } list && IsInstanceValid(list))
        {
            ctx.Players.StateChanged -= list.Changed;
            list.QueueFree();
        }
        Current = null;
    }

    protected override GuiPanel Build()
    {
        var panel = GuiPanel.FromLayout(Assets, "ingame_pvp")!;
        Icon(panel, "TellButton", "ir_chat", 66779, () => { if (Selected is { Self: false } p) Model.GuiEvent(28, p.Id); });
        Icon(panel, "LeaveButton", "ir_leave", 1071, () => { if (Model.State.CanLeave) Model.Leave(); });
        Icon(panel, "inviteButton", "ir_invite", 1067, () => { if (Selected is { Party: false } p) Model.Invite(p.Id); });
        Icon(panel, "TransferButton", "ir_transleader", 6383, () => { if (Selected is { Party: true } p) Model.Transfer(p.Id); });
        // Boot and Ban are the Dungeon Master's (dialog.tlk 7998 / 7997): a player sees their frames empty (mp_partyui).
        // OpenWinter's DMs boot and ban from their own tools, so the slots stay empty here.
        foreach (var tag in new[] { "BootButton", "BanButton" })
            if (panel.Get<GuiButton>(tag) is { } b) { b.Disabled = true; b.MouseFilter = MouseFilterEnum.Ignore; }
        // The list box: under the heading boxes (y -1.28) down to the model's playerlist node, the three columns and a
        // scroll bar in the right-hand strip (x 3.42 to 3.6), as the original draws it.
        float top = panel.At(null, 0, -1.28f).Y, bottom = panel.Node("playerlist")?.Y ?? panel.At(null, 0, -4.56f).Y;
        float left = panel.At(null, 0, 0).X, width = 3.42f * GuiModel.PixelsPerUnit;
        _rows = new Rows { List = this, Position = new Vector2(left, top + 2), Size = new Vector2(width, bottom - top - 2) };
        panel.AddChild(_rows);
        _scroll = new ClassicScrollBar { Assets = Assets, Name = "ListScroll", Step = 1, Position = new Vector2(left + width + 1, top + 2), Size = new Vector2(16, bottom - top - 2) };
        _scroll.Scrolled = offset => { _rows.First = (int)MathF.Round(offset); _rows.QueueRedraw(); };
        panel.AddChild(_scroll);
        return panel;
    }

    void Icon(GuiPanel panel, string tag, string icon, uint tip, Action pressed)
    {
        if (panel.Get<GuiButton>(tag) is not { } b) return;
        b.Icon = Assets.Texture(icon);
        b.IconSize = new Vector2(32, 32);
        b.SetMeta(ClassicTooltip.Meta, Assets.Tlk(tip));
        b.Pressed += pressed;
    }

    PlayerListEntry? Selected => Model.State.Players.FirstOrDefault(p => p.Id == _selected);

    public override void Open()
    {
        base.Open();
        Model.Refresh();
    }

    public override void Refresh() => Changed();

    void Changed()
    {
        if (Panel == null) return;
        var s = Model.State;
        // "PvP Zone: Party Protected" (dialog.tlk 8017 and 8018-8020; mp_partyui).
        if (Panel.Get<GuiLabel>("PvPSettingLabel") is { } pvp) pvp.Text = s.PvpText.Length > 0 ? $"{Assets.Tlk(8017)}: {s.PvpText}" : "";
        // The four buttons stay as they are whatever is selected: the original's did nothing, and said nothing, when
        // pressed with nobody selected or alone (mp_partyui); the server refuses what doesn't apply.
        _scroll.Page = (int)(_rows.Size.Y / Rows.RowHeight);
        _scroll.Total = s.Players.Count;
        _scroll.Set(_scroll.Offset);
        _rows.QueueRedraw();
    }

    // ------------------------------------------------------------------ invitations (party_invitation.gui)

    ClassicPopup? _invitation;

    void Invitation(uint from, string name, string text, bool open)
    {
        if (_invitation != null && IsInstanceValid(_invitation)) _invitation.QueueFree();
        _invitation = null;
        if (!open || !PlayerListPanel.InvitationPopups) return;
        var popup = ClassicPopup.Show(Ctx.Hud, Assets, "party_invitation", text,
            ("OkButton", () => Model.Answer(from, "accept")), ("RejectButton", () => Model.Answer(from, "reject")), ("CancelButton", null));
        if (popup == null) return;
        if (popup.Panel.Get<GuiButton>("OkButton") is { } ok) ok.Caption = Assets.Tlk(66774);
        if (popup.Panel.Get<GuiButton>("RejectButton") is { } reject) reject.Caption = Assets.Tlk(66775);
        if (popup.Panel.Get<GuiButton>("DisablePartyInviteCheck") is { } never)
            never.Toggled += on => { if (on) PlayerListPanel.NeverShowInvitations(); };
        _invitation = popup;
    }

    // ------------------------------------------------------------------ the rows

    partial class Rows : Control
    {
        public ClassicPlayerList List = null!;
        public const float RowHeight = 20;
        public int First;
        // Column starts, in pixels from the list's left: the heading boxes' (0, 1.74, 2.59 units) plus the captions' inset.
        static readonly float[] Columns = { 7, 189, 284 };

        public override void _Ready() => MouseFilter = MouseFilterEnum.Stop;

        public override void _Draw()
        {
            var assets = List.Assets;
            var font = assets.Font("fnt_galahad14");
            var state = List.Model.State;
            for (int i = First; i < state.Players.Count && (i - First + 1) * RowHeight <= Size.Y; i++)
            {
                var p = state.Players[i];
                var row = new Rect2(0, (i - First) * RowHeight, Size.X, RowHeight);
                if (p.Id == List._selected) DrawRect(row, new Color(1, 1, 1, 0.12f));
                // Party members in the party colour, the asker as herself (colours inferred, not measured).
                var color = p.Self ? new Color(1, 1, 0.6f) : p.Party ? new Color(0.55f, 1, 0.55f) : Colors.White;
                string name = p.Name + (p.LeadsParty ? $" ({assets.Tlk(66852)})" : "")
                    + (p.InvitedMe ? " *" : "");
                font.DrawBox(this, new Rect2(Columns[0], row.Position.Y, Columns[1] - Columns[0] - 4, RowHeight), name, color, 0, 0.5f);
                if (p.Self) continue;
                font.DrawBox(this, new Rect2(Columns[1], row.Position.Y, Columns[2] - Columns[1] - 4, RowHeight),
                    assets.Tlk(p.Dislike ? 8000u : 7999u), p.Dislike ? new Color(1, 0.4f, 0.4f) : Colors.White, 0, 0.5f);
                font.DrawBox(this, new Rect2(Columns[2], row.Position.Y, Size.X - Columns[2], RowHeight),
                    assets.Tlk(p.DislikedBy ? 8000u : 7999u), p.DislikedBy ? new Color(1, 0.4f, 0.4f) : Colors.White, 0, 0.5f);
            }
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } mb) return;
            var players = List.Model.State.Players;
            int i = First + (int)(mb.Position.Y / RowHeight);
            if (i < 0 || i >= players.Count) return;
            var p = players[i];
            List._selected = p.Id;
            List.Model.GuiEvent(12, p.Id);
            if (!p.Self && mb.Position.X >= Columns[1] && mb.Position.X < Columns[2]) List.Model.SetLike(p.Id, p.Dislike);
            List.Changed();
            AcceptEvent();
        }
    }
}
