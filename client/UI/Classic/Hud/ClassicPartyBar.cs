using OpenWinter.Client.UI.HudParts;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's party bar, top right (pnl_party_bar): the player's portrait (pb_portrait) with the red health
/// bar at its left (ctl_playr_health), the eight buttons under it (map, inventory, journal, character, rest,
/// spellbook, options, player list) and the party's portraits below (pb_pnl_member each, with its own health bar,
/// direction arrow and name), against the bar's left edge with the list's scroll bar at their right.
/// The buttons call the HUD's actions, so whichever interface owns a window opens it.
/// </summary>
public partial class ClassicPartyBar : Control
{
    public ClassicContext Ctx = null!;
    GuiModelView _frame = null!;
    PortraitView _portrait = null!;
    HealthBar _health = null!;
    GuiButton _char = null!, _jour = null!;
    readonly List<Member> _members = new();
    ClassicScrollBar _scroll = null!;
    int _first;
    string _layout = "";
    HBoxContainer _effects = null!;

    HudState State => Ctx.State;
    void SelectOrTarget(uint id)
    {
        if (id != 0 && !Ctx.Casting.Interaction.TargetPortrait(id)) State.Select(id);
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var assets = Ctx.Assets;
        _frame = GuiModelView.Create(assets, "pnl_party_bar");
        _frame.MouseFilter = MouseFilterEnum.Stop;
        AddChild(_frame);
        Size = _frame.Size;

        _portrait = new PortraitView { Ctx = Ctx, Model = assets.Model("pb_portrait"), LevelUpModel = assets.Model("ctl_btn_pb_lvlup"), Large = true };
        // While a level waits the portrait is the original's level-up button: clicking it opens the level-up.
        _portrait.Clicked = () => { if (State.CanLevelUp) Ctx.OpenLevelUp(); else SelectOrTarget(Ctx.Session.ControlledId); };
        _portrait.RightClicked = at => OpenRadial(Ctx.Session.ControlledId, at);
        AddChild(_portrait);
        _portrait.PlaceOrigin(Node("Portrait"));
        _health = new HealthBar { Assets = assets, Model = assets.Model("ctl_playr_health") };
        AddChild(_health);
        _health.PlaceOrigin(Node("Health"));

        Button("MapButton", "pb_but_map", "Map", () => Ctx.Hud.OpenMap?.Invoke());
        Button("InvButton", "pb_but_inv", "Inventory", () => Ctx.Hud.OpenInventory?.Invoke());
        _jour = Button("JourButton", "pb_but_jour", "Journal", () => Ctx.Hud.OpenJournal?.Invoke());
        Ctx.Journal.AttentionChanged += Refresh;
        TreeExiting += () => Ctx.Journal.AttentionChanged -= Refresh;
        _char = Button("CharButton", "pb_but_char", "Character Record", () => Ctx.Hud.OpenCharacter?.Invoke());
        Button("RestButton", "pb_but_rest", "Rest", () => Ctx.Hud.Rest?.Invoke());
        Button("SpellButton", "pb_but_spell", "Spellbook", () => Ctx.Hud.OpenSpellbook?.Invoke());
        Button("OptsButton", "pb_but_opts", "Options", () => Ctx.Hud.OpenMenu?.Invoke());
        _scroll = new ClassicScrollBar { Assets = assets, Name = "PartyScroll", Visible = false, Step = 1 };
        _scroll.Scrolled = offset => { int first = (int)MathF.Round(offset); if (first != _first) { _first = first; LayoutMembers(); } };
        AddChild(_scroll);
        var pvp = Button("PvpButton", "pb_but_pvp", "Player List", () => Ctx.Input.OpenPlayerList?.Invoke());
        // As the original: greyed in single player (no other players, no PvP settings), drawn from the texture's
        // greyed picture (GuiButton.DisabledUvShift).
        pvp.Disabled = Ctx.Input.SinglePlayer;
        pvp.DisabledUvShift = new Vector2(0, 0.5625f);

        _effects = new HBoxContainer { Position = new Vector2(-480, 3), Size = new Vector2(476, 40), Alignment = BoxContainer.AlignmentMode.End, MouseFilter = MouseFilterEnum.Ignore };
        _effects.AddThemeConstantOverride("separation", 3);
        _effects.SetMeta("classic_effects", true);
        AddChild(_effects);
        var fx = Ctx.Main.GetChildren().OfType<PlayerFx>().FirstOrDefault();
        fx?.SetEffectsHost(_effects);
        State.Changed += Refresh;
        TreeExiting += () => State.Changed -= Refresh;
        Refresh();
    }

    Vector2 Node(string name) => _frame.Node(name) ?? Vector2.Zero;

    /// <summary>The player's portrait (captures hover it).</summary>
    public Control Portrait => _portrait;

    GuiButton Button(string node, string model, string tip, Action pressed)
    {
        var b = GuiButton.Create(Ctx.Assets, model);
        b.Name = node;
        b.SetMeta(ClassicTooltip.Meta, tip);
        b.Pressed += pressed;
        AddChild(b);
        b.PlaceOrigin(Node(node));
        return b;
    }

    /// <summary>
    /// Top right. The original draws the bar one column further left than its 79-pixel texture would put it, with
    /// the panel's dark outer texel in the screen's last column (partyframes, 1280x720 at 1x and 1600x900 at 1.2x).
    /// </summary>
    public void Place(Vector2 view) => Position = new Vector2(view.X - _frame.Size.X - 1, 0);

    /// <summary>
    /// Members shown at once: as many whole rows as fit between PartyMembers and the bottom of the scroll bar at
    /// its longest (scrollmax plus the down arrow). Not measured past four members.
    /// </summary>
    int VisibleRows(float pitch)
    {
        float top = Node("PartyMembers").Y, bottom = (_frame.Node("scrollmax") ?? new Vector2(0, 538)).Y + 16;
        return Math.Max(1, (int)((bottom - top) / pitch));
    }

    void Refresh()
    {
        if (State.Self is { } me)
        {
            _portrait.Portrait = me.Portrait;
            // While paused the original's portrait shows po_pause, an hourglass (pausegame).
            _portrait.Override = State.Paused ? "po_pause" : null;
            _portrait.Dead = me.Dead;
            _portrait.Tip = Tip(me);
            _portrait.LevelUp = State.CanLevelUp;
            _health.Fraction = me.MaxHp > 0 ? Math.Clamp(me.Hp / (float)me.MaxHp, 0, 1) : 1;
        }
        // The character button pulses when a level is waiting, as the original's.
        _char.ForcedState = State.CanLevelUp ? "encouraged" : null;
        if (!State.CanLevelUp) _char.QueueRedraw();
        // The journal button pulses after a journal update until the journal is opened (scene journalpc).
        _jour.ForcedState = Ctx.Journal.Attention ? "encouraged" : null;
        if (!Ctx.Journal.Attention) _jour.QueueRedraw();

        var layout = string.Join(",", State.Party.Select(p => p.Id));
        if (layout != _layout)
        {
            _layout = layout;
            foreach (var m in _members) m.QueueFree();
            _members.Clear();
            foreach (var p in State.Party)
            {
                var m = new Member { Bar = this, Id = p.Id };
                AddChild(m);
                _members.Add(m);
            }
            LayoutMembers();
        }
        foreach (var m in _members)
            if (State.Party.FirstOrDefault(p => p.Id == m.Id) is { } v) m.Show(v, State.Selected == v.Id, State.Leader == v.Id);
    }

    /// <summary>
    /// The original's member list (partyframes): frames hang from PartyMembers against the bar's left edge, one
    /// NextLink (66 px) apart; the scroll bar (pb_scrl_up, the ridged thumb, pb_scrl_down) runs at scrollmin's x from
    /// the first frame's top to the last row's bottom, its track starting at scrollmin.
    /// </summary>
    void LayoutMembers()
    {
        var at = Node("PartyMembers");
        float pitch = _members.Count > 0 ? _members[0].Pitch : 66;
        int rows = VisibleRows(pitch);
        _first = Math.Clamp(_first, 0, Math.Max(0, _members.Count - rows));
        for (int i = 0; i < _members.Count; i++)
        {
            var m = _members[i];
            int row = i - _first;
            m.Visible = row >= 0 && row < rows;
            m.Position = new Vector2(at.X, at.Y + row * pitch);
        }
        _scroll.Visible = _members.Count > 0;
        if (_members.Count == 0) return;
        var min = _frame.Node("scrollmin") ?? new Vector2(63, 199);
        float arrow = 16;
        _scroll.Position = new Vector2(min.X, min.Y - arrow);
        _scroll.Size = new Vector2(_frame.Size.X - min.X, at.Y + Math.Min(rows, _members.Count) * pitch - _scroll.Position.Y);
        _scroll.Page = rows;
        _scroll.Total = _members.Count;
        _scroll.Offset = _first;
        _scroll.QueueRedraw();
    }

    /// <summary>The original's portrait hover text: the creature's name, then the area it is in.</summary>
    static string Tip(Vitals v) => v.Area.Length > 0 ? $"{v.Name}\n{v.Area}" : v.Name;

    void OpenRadial(uint id, Vector2 at)
    {
        if (id != 0 && Ctx.Session.Objects.TryGetValue(id, out var view)) Ctx.Interaction.OpenRadial?.Invoke(view, at);
    }

    /// <summary>A portrait model (pb_portrait / pb_mbr_pic) with the creature's portrait on its plane.</summary>
    sealed partial class PortraitView : Control, IClassicHover
    {
        public ClassicContext Ctx = null!;
        public GuiModel? Model;
        /// <summary>ctl_btn_pb_lvlup: shown instead of the portrait while a level waits (po_levelup on its PortraitPlane05).</summary>
        public GuiModel? LevelUpModel;
        public bool Large, Dead, LevelUp;
        public string Portrait = "", Tip = "";
        /// <summary>A whole texture shown instead of the portrait (po_pause), or null.</summary>
        public string? Override;
        public Action? Clicked;
        public Action<Vector2>? RightClicked;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            var b = Model?.Bounds ?? new Rect2(0, -100, 64, 100);
            Size = b.Size;
        }

        Vector2 Origin => Model == null ? new Vector2(0, Size.Y) : -Model.Bounds.Position;
        public void PlaceOrigin(Vector2 p) => Position = p - Origin;

        public override void _Process(double delta) => QueueRedraw();

        public override void _Draw()
        {
            var assets = Ctx.Assets;
            var rect = new Rect2(Vector2.Zero, Size);
            // The plane's texture (gui_replace01) stands for the creature's portrait: "m" (64x128, top 100 rows
            // shown) on the party bar, "s" (32x64, top 50) for party members.
            // A level waiting: the original swaps the player's portrait for po_levelup until the level is taken
            // (levelup scene: from the moment the experience arrives, back when it no longer allows a level).
            // The pause hourglass (Override) goes over it (their order was not measured).
            var levelPlane = Override == null && LevelUp ? LevelUpModel?.Rest.Meshes.FirstOrDefault(m => m.Node.StartsWith("PortraitPlane", StringComparison.OrdinalIgnoreCase)) : null;
            var tex = Override != null ? assets.Texture(Override)
                : levelPlane != null ? assets.Texture(levelPlane.Texture)
                : Portrait.Length > 0 ? assets.Texture(Portrait + (Large ? "m" : "s")) : null;
            if (tex == null && Model != null) { GuiDraw.Model(this, assets, Model.Rest, Origin, Colors.White); return; }
            if (tex == null) return;
            var plane = levelPlane ?? Model?.Rest.Meshes.FirstOrDefault();
            float u1 = plane?.Uvs.Max(u => u.X) ?? 1, v1 = plane?.Uvs.Max(u => u.Y) ?? 0.78f;
            var src = new Rect2(0, 0, tex.GetWidth() * Math.Min(1, u1), tex.GetHeight() * Math.Min(1, v1));
            var mod = Dead ? new Color(0.45f, 0.45f, 0.45f) : IsHovered() ? GuiDraw.Hilite : Colors.White;
            DrawTextureRectRegion(tex, rect, src, mod);
        }

        bool IsHovered() => GetGlobalRect().HasPoint(GetGlobalMousePosition());

        public override void _GuiInput(InputEvent e)
        {
            if (e is not InputEventMouseButton { Pressed: true } mb) return;
            if (mb.ButtonIndex == MouseButton.Left) { Clicked?.Invoke(); AcceptEvent(); }
            else if (mb.ButtonIndex == MouseButton.Right) { RightClicked?.Invoke(mb.GlobalPosition); AcceptEvent(); }
        }

        public string[]? HoverLines() => Tip.Length > 0 ? Tip.Split('\n') : null;
        public bool HoverInstant => true;
    }

    /// <summary>A vertical health bar model (ctl_playr_health / ctl_party_health) emptied from the top as health drops.</summary>
    sealed partial class HealthBar : Control
    {
        public ClassicAssets Assets = null!;
        GuiModel? _model;
        float _fraction = 1;

        public GuiModel? Model
        {
            get => _model;
            set { _model = value; Size = _model?.Bounds.Size ?? new Vector2(7, 100); }
        }

        public float Fraction
        {
            get => _fraction;
            set { if (Math.Abs(_fraction - value) < 0.001f) return; _fraction = value; QueueRedraw(); }
        }

        public HealthBar() { MouseFilter = MouseFilterEnum.Ignore; ClipContents = true; }

        Vector2 Origin => _model == null ? new Vector2(0, Size.Y) : -_model.Bounds.Position;
        public void PlaceOrigin(Vector2 p) => Position = p - Origin;

        public override void _Draw()
        {
            if (_model == null || _fraction <= 0) return;
            // Draw the bar squashed toward its bottom by the health left.
            float h = Size.Y;
            DrawSetTransform(new Vector2(0, h * (1 - _fraction)), 0, new Vector2(1, _fraction));
            GuiDraw.Model(this, Assets, _model.Rest, Origin, Colors.White);
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
    }

    /// <summary>
    /// One party member under the bar (pb_pnl_member): small portrait, health bar, the arrow pointing to the member
    /// (pb_mbr_pos at Pos) and the name under the frame (Text); click selects, right-click opens the radial menu.
    /// </summary>
    sealed partial class Member : Control
    {
        public ClassicPartyBar Bar = null!;
        public uint Id;
        GuiModelView _frame = null!;
        PortraitView _portrait = null!;
        HealthBar _health = null!;
        GuiModelView? _leader;
        GuiModel? _locator;
        Control _arrow = null!, _name = null!;
        ClassicText _font = null!;
        string _text = "";
        Vector2 _pos;
        float? _angle;
        public float Pitch = 66;
        /// <summary>
        /// The name's colour and face size (partyframes: 204 grey; with the large font fnt_default_hr at 13 px, "Hench A"
        /// 54 px wide with 9 px capitals; with the regular font fnt_default at 11 px, 48 px wide with 8 px capitals).
        /// </summary>
        static readonly Color NameColor = new(0.8f, 0.8f, 0.8f);
        static int NameSize => Settings.Current.Interface.HighResolutionFont ? 13 : 11;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            var assets = Bar.Ctx.Assets;
            _frame = GuiModelView.Create(assets, "pb_pnl_member");
            AddChild(_frame);
            Size = _frame.Size;
            if (_frame.Node("NextLink") is { } next) Pitch = next.Y - _frame.Origin.Y;
            _portrait = new PortraitView { Ctx = Bar.Ctx, Model = assets.Model("pb_mbr_pic") };
            uint id = Id;
            _portrait.Clicked = () => Bar.SelectOrTarget(id);
            _portrait.RightClicked = at => Bar.OpenRadial(id, at);
            AddChild(_portrait);
            _portrait.PlaceOrigin(_frame.Node("PartyMember") ?? new Vector2(6, 53));
            _health = new HealthBar { Assets = assets, Model = assets.Model("ctl_party_health") };
            AddChild(_health);
            _health.PlaceOrigin(_frame.Node("Health") ?? new Vector2(4, 53));
            _locator = assets.Model("pb_mbr_pos");
            _pos = _frame.Node("Pos") ?? new Vector2(49, 15);
            _arrow = new Control { MouseFilter = MouseFilterEnum.Ignore };
            _arrow.Draw += DrawArrow;
            AddChild(_arrow);
            // The name: left at Text, its baseline two pixels above it, no wider than the frame.
            _font = ClassicText.For(assets, ClassicText.DefaultTextFont, NameSize);
            var text = _frame.Node("Text") ?? new Vector2(5, 67);
            _name = _font.CreateLayer(this, DrawName);
            _name.ClipContents = true;
            _name.Position = new Vector2(text.X, text.Y - 2 - _font.Ascent - 2);
            // Cut at the frame's last column (partyframes: the regular font's "Summoned" shows the edge of its 'd' up to it).
            _name.Size = new Vector2(Size.X - text.X - 1, _font.Ascent + 6);
        }

        /// <summary>
        /// The name's characters that start within <see cref="NameRoom"/> of its left (partyframes): the original
        /// draws "Summoned ..."'s 'd' starting 55 px in (regular font, cut at the frame's edge) but not the 'e' that would start
        /// 56 px in after the large font's "Summon". The rest is dropped.
        /// </summary>
        string Fit(string name)
        {
            name = Kit.NwnText.Strip(name ?? "");
            int n = 0;
            while (n < name.Length && _font.MeasureTracked(name[..n]) < NameRoom) n++;
            return name[..n];
        }

        const float NameRoom = 56;

        void DrawName(CanvasItem ci) => _font.DrawLineTracked(ci, new Vector2(0, 2), _text, NameColor);

        public override void _Process(double delta)
        {
            var angle = Angle();
            if (angle == _angle || (angle is { } a && _angle is { } b && MathF.Abs(a - b) < 0.01f)) return;
            _angle = angle;
            _arrow.QueueRedraw();
        }

        /// <summary>
        /// The direction from the controlled creature to the member, on screen: 0 straight up (ahead of the camera),
        /// clockwise. Null when the member is not in view of the client (another area).
        /// </summary>
        float? Angle()
        {
            var session = Bar.Ctx.Session;
            if (session.Controlled is not { } me || !session.Objects.TryGetValue(Id, out var them) || !IsInstanceValid(them)) return null;
            var cam = GetViewport()?.GetCamera3D();
            if (cam == null) return null;
            var d = them.GlobalPosition - me.GlobalPosition;
            var right = cam.GlobalTransform.Basis.X; right.Y = 0;
            var ahead = -cam.GlobalTransform.Basis.Z; ahead.Y = 0;
            if (right.LengthSquared() < 1e-6f || ahead.LengthSquared() < 1e-6f) return null;
            float x = d.Dot(right.Normalized()), y = d.Dot(ahead.Normalized());
            return x * x + y * y < 1e-6f ? 0 : MathF.Atan2(x, y);
        }

        void DrawArrow()
        {
            if (_locator == null || _angle is not { } angle) return;
            _arrow.DrawSetTransform(_pos, angle, Vector2.One);
            GuiDraw.Model(_arrow, Bar.Ctx.Assets, _locator.Rest, Vector2.Zero, Colors.White);
            _arrow.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }

        public void Show(Vitals v, bool selected, bool leader)
        {
            _portrait.Portrait = v.Portrait;
            _portrait.Dead = v.Dead;
            _portrait.Tip = Tip(v);
            _health.Fraction = v.MaxHp > 0 ? Math.Clamp(v.Hp / (float)v.MaxHp, 0, 1) : 1;
            var name = Fit(v.Name);
            if (_text != name) { _text = name; _name.QueueRedraw(); }
            if (leader && _leader == null)
            {
                _leader = GuiModelView.Create(Bar.Ctx.Assets, "ctl_party_leader");
                AddChild(_leader);
                _leader.PlaceOrigin(_frame.Node("Properties") ?? new Vector2(39, 53));
            }
            if (_leader != null) _leader.Visible = leader;
            Modulate = selected ? GuiDraw.Hilite : Colors.White;
        }
    }
}
