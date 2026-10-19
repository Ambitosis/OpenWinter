using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The abilities window (K): everything the character can use besides spells (feats, modes, skills, usable items,
/// emotes and basic actions) as tiles. Click a tile to use it; drag it onto the action bar to keep it there.
/// </summary>
public partial class AbilitiesPanel : UiTheme.Window
{
    public ClientSession Session = null!;
    public SpellCasting Casting = null!;
    AbilityBook _book = null!;
    AbilityUser _user = null!;
    VBoxContainer _list = null!;
    LineEdit _filter = null!;
    bool _dirty = true;

    public static AbilitiesPanel Create(ClientSession session, SpellCasting casting)
    {
        var p = Create<AbilitiesPanel>("Abilities", new Vector2(620, 580), "feat");
        p.Session = session;
        p.Casting = casting;
        p.AnchorLeft = p.AnchorRight = 0.5f;
        p.AnchorTop = p.AnchorBottom = 0.5f;
        p.OffsetLeft = -310; p.OffsetRight = 310; p.OffsetTop = -330; p.OffsetBottom = 250;
        return p;
    }

    public override void _Ready()
    {
        (_book, _user) = AbilityServices.For(Session, Casting);
        _user.ToggleAbilitiesWindow = Toggle;
        _filter = new LineEdit { PlaceholderText = "Search abilities", ClearButtonEnabled = true, CustomMinimumSize = new Vector2(180, 0) };
        _filter.TextChanged += _ => Build();
        HeaderExtras.AddChild(_filter);
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _list = Ui.VBox(Palette.Space3);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(_list);
        Body.AddChild(scroll);
        var foot = Ui.HBox(6);
        foot.AddChild(Ui.Text("Click to use · drag onto the action bar · spells are in the spellbook", Ui.TextStyle.Caption, Palette.TextMuted));
        Body.AddChild(foot);
        _book.Changed += () => { _dirty = true; };
    }

    public void Toggle()
    {
        if (Visible) { Close(); return; }
        Visible = true;
        _book.Refresh();
        Build();
    }

    public override void _Process(double delta)
    {
        if (Visible && _dirty) Build();
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.K, CtrlPressed: false, AltPressed: false } && !UI.DM.ChatInput.Typing)
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    void Build()
    {
        _dirty = false;
        foreach (var c in _list.GetChildren()) c.QueueFree();
        string f = _filter.Text.Trim();
        bool Match(string name) => f.Length == 0 || name.Contains(f, StringComparison.OrdinalIgnoreCase);
        void Section(string title, IEnumerable<BarSlot> slots)
        {
            var tiles = slots.Select(s => (s, look: _user.Look(s))).Where(x => Match(x.look.Name)).ToList();
            if (tiles.Count == 0) return;
            _list.AddChild(Ui.Section(title));
            var flow = new HFlowContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            flow.AddThemeConstantOverride("h_separation", 6);
            flow.AddThemeConstantOverride("v_separation", 8);
            foreach (var (s, _) in tiles) flow.AddChild(new AbilityTile { User = _user, Slot = s, Content = Session.Content });
            _list.AddChild(flow);
        }
        Section("Modes", _book.Modes.Select(m => new BarSlot { Kind = "mode", Id = m.Mode }));
        var feats = new List<BarSlot>();
        foreach (var feat in _book.Feats)
        {
            if (feat.Subs.Length > 0 && feat.Feat != 5) // called shot keeps one tile; wild shapes etc. get one per form
                feats.AddRange(feat.Subs.Select(s => new BarSlot { Kind = "feat", Id = feat.Feat, Sub = s.Id }));
            else feats.Add(new BarSlot { Kind = "feat", Id = feat.Feat });
        }
        Section("Feats", feats);
        var skills = new List<BarSlot>();
        foreach (var s in _book.Skills)
        {
            if (s.Subs.Length > 0) skills.AddRange(s.Subs.Select(sub => new BarSlot { Kind = "skill", Id = s.Skill, Sub = sub.Id }));
            else skills.Add(new BarSlot { Kind = "skill", Id = s.Skill });
        }
        Section("Skills", skills);
        Section("Items", _book.Items.GroupBy(i => i.Name).Select(g => g.First()).SelectMany(i => i.Powers.Length == 1
            ? [new BarSlot { Kind = "item", Item = i.Item, Prop = i.Powers[0].Index, Name = i.Name }]
            : i.Powers.Select(p => new BarSlot { Kind = "item", Item = i.Item, Prop = p.Index, Name = i.Name })));
        Section("Actions", BarCatalog.Commands.Select(c => new BarSlot { Kind = "command", Arg = c.Command }));
        Section("Emotes", OpenWinter.Content.Emotes.All.Select(e => new BarSlot { Kind = "emote", Arg = e.Key, Name = e.Name }));
        if (_list.GetChildCount() == 0)
            _list.AddChild(Ui.Text(_book.Loaded ? "Nothing matches." : "Loading…", Ui.TextStyle.Secondary));
    }

    /// <summary>One draggable ability: icon slot with its name underneath.</summary>
    sealed partial class AbilityTile : Control
    {
        public AbilityUser User = null!;
        public BarSlot Slot = null!;
        public Views.ClientContent? Content;
        bool _hover;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(84, 92);
            MouseFilter = MouseFilterEnum.Stop;
            TooltipText = "tile";
            MouseEntered += () => { _hover = true; QueueRedraw(); };
            MouseExited += () => { _hover = false; QueueRedraw(); };
        }

        public override void _Draw()
        {
            var look = User.Look(Slot);
            var slot = new Rect2(new Vector2((Size.X - 52) / 2, 2), new Vector2(52, 52));
            if (look.Toggled)
            {
                var glow = Styles.Box(Colors.Transparent, Palette.RadiusSmall + 2, null, 0, 0);
                glow.ShadowColor = Palette.Accent with { A = 0.45f };
                glow.ShadowSize = 8;
                DrawStyleBox(glow, slot);
            }
            DrawStyleBox(Styles.Box(new Color(0.03f, 0.035f, 0.05f, 0.9f), Palette.RadiusSmall,
                look.Toggled ? Palette.AccentBright : _hover ? Palette.Accent : Palette.LineStrong, look.Toggled ? 2 : 1, 0), slot);
            SlotArt.DrawIcon(this, Content, look, slot.Grow(-3), look.Usable ? Colors.White : new Color(0.45f, 0.42f, 0.4f));
            if (look.Uses >= 0) SlotArt.DrawBadge(this, look.Uses.ToString(), slot, look.Uses == 0 ? Palette.Danger : Palette.Text);
            // Name, up to two lines.
            var font = Fonts.Ui;
            const int fs = 11;
            var words = look.Name.Split(' ');
            var lines = new List<string>();
            var cur = "";
            foreach (var w in words)
            {
                var next = cur.Length == 0 ? w : cur + " " + w;
                if (font.GetStringSize(next, HorizontalAlignment.Left, -1, fs).X > Size.X - 2 && cur.Length > 0) { lines.Add(cur); cur = w; }
                else cur = next;
            }
            if (cur.Length > 0) lines.Add(cur);
            for (int i = 0; i < Math.Min(2, lines.Count); i++)
            {
                var text = i == 1 && lines.Count > 2 ? lines[i] + "…" : lines[i];
                var ts = font.GetStringSize(text, HorizontalAlignment.Left, -1, fs);
                DrawString(font, new Vector2(Math.Max(0, (Size.X - ts.X) / 2), 68 + i * 13), text, HorizontalAlignment.Left, Size.X, fs,
                    _hover ? Palette.AccentBright : look.Usable ? Palette.TextSecondary : Palette.TextMuted);
            }
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } mb && new Rect2(Vector2.Zero, Size).HasPoint(mb.Position))
            {
                User.Use(Slot);
                AcceptEvent();
            }
        }

        public override Variant _GetDragData(Vector2 atPosition)
        {
            SetDragPreview(SlotPreview.Centered(User.Look(Slot), Content));
            return Slot.ToDrag();
        }

        public override GodotObject? _MakeCustomTooltip(string forText) =>
            SlotArt.Tooltip(User.Look(Slot), null, "Click to use · drag onto the action bar");
    }
}
