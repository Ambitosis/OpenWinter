using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The action bar: twelve slots (F1–F12) on three bars (hold Shift for bar 2, Ctrl for bar 3, or click the page
/// pips), holding spells, items, feats, skills, modes, emotes and commands. Shows uses left, lights up modes that
/// are on, and takes drops from the spellbook, inventory, abilities window and radial menu. Right-click or drag a
/// slot off the bar to clear it. Saved locally (user://quickbar/&lt;character&gt;.json) and with the character.
/// <para>Self-sized: the HUD places it (bottom centre). Set <see cref="Session"/> and <see cref="Casting"/> before adding it.</para>
/// </summary>
public partial class ActionBar : Control
{
    public const int Slots = 12, Pages = 3;
    public ClientSession Session = null!;
    public SpellCasting Casting = null!;
    public AbilityBook Book { get; private set; } = null!;
    public AbilityUser User { get; private set; } = null!;

    readonly BarSlot?[][] _bars = Enumerable.Range(0, Pages).Select(_ => new BarSlot?[Slots]).ToArray();
    readonly ActionSlotView[] _views = new ActionSlotView[Slots];
    PageIndicator _pager = null!;
    PanelContainer _panel = null!;
    Label _hint = null!;
    int _basePage;
    string _owner = "";
    uint _ownerId;
    bool _loaded;
    bool _importing, _fillDefaults;
    bool _awaitingServer, _localSaved, _localCache;
    readonly QuickbarSync _sync = new();

    /// <summary>The twelve slot controls (screen positions for tests and tutorials).</summary>
    public IReadOnlyList<ActionSlotView> SlotViews => _views;

    /// <summary>The bar the keys and slots show now: Shift held → 2, Ctrl held → 3, else the chosen one.</summary>
    public int Page { get; private set; }

    public override void _Ready()
    {
        (Book, User) = AbilityServices.For(Session, Casting);
        MouseFilter = MouseFilterEnum.Ignore;
        _panel = Ui.Glass(new PanelContainer(), Styles.Hud(6, Palette.Radius), 0.8f);
        AddChild(_panel);
        var row = Ui.HBox(4);
        row.MouseFilter = MouseFilterEnum.Ignore;
        _panel.AddChild(row);
        _pager = new PageIndicator { Bar = this };
        row.AddChild(_pager);
        for (int i = 0; i < Slots; i++)
        {
            if (i is 4 or 8) row.AddChild(new Control { CustomMinimumSize = new Vector2(6, 0), MouseFilter = MouseFilterEnum.Ignore });
            _views[i] = new ActionSlotView { Bar = this, Index = i };
            row.AddChild(_views[i]);
        }
        var more = IconButton.Create("feat", "Abilities (K): feats, skills, items, emotes · drag them onto the bar", 30, 16, () => User.ToggleAbilitiesWindow?.Invoke());
        more.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(more);

        _hint = Ui.Text("", Ui.TextStyle.Strong, Palette.AccentBright, Palette.FontSmall);
        _hint.AddThemeStyleboxOverride("normal", Styles.Box(Palette.Bg0 with { A = 0.85f }, 12, Palette.AccentDim, 1, 14, 4, shadow: 8));
        _hint.Visible = false;
        AddChild(_hint);

        _panel.Resized += Fit;
        Fit();
        // Added straight to a canvas layer (development): sit at the bottom centre. A HUD container places it otherwise.
        if (GetParent() is not Container)
        {
            AnchorLeft = AnchorRight = 0.5f;
            AnchorTop = AnchorBottom = 1;
            GrowHorizontal = GrowDirection.Both;
            GrowVertical = GrowDirection.Begin;
            OffsetLeft = -CustomMinimumSize.X / 2; OffsetRight = CustomMinimumSize.X / 2;
            OffsetTop = -CustomMinimumSize.Y - 12; OffsetBottom = -12;
        }
        User.Feedback ??= Notice;
        Book.Changed += OnBookChanged;
        User.Used += OnUsed;
        Session.NotifyReceived += OnNotify;
        Session.ControlledChanged += ResetControl;
        TreeExiting += () =>
        {
            Session.NotifyReceived -= OnNotify;
            Session.ControlledChanged -= ResetControl;
            Book.Changed -= OnBookChanged;
            User.Used -= OnUsed;
        };
    }

    void ResetControl(uint controlled)
    {
        _owner = ""; _ownerId = 0;
        _loaded = _importing = _fillDefaults = _awaitingServer = _localCache = _localSaved = false;
        _sync.Reset();
        foreach (var bar in _bars) Array.Clear(bar);
        _basePage = Page = 0;
        Redraw();
    }

    void Fit()
    {
        var size = _panel.GetCombinedMinimumSize();
        CustomMinimumSize = size;
        _panel.Size = size;

    }

    public override void _Process(double delta)
    {
        // Load the bar once we know which character we are.
        var name = Session.Controlled?.DisplayName ?? "";
        if (name.Length > 0 && (name != _owner || Session.ControlledId != _ownerId))
        {
            _owner = name;
            _ownerId = Session.ControlledId;
            Load();
            Book.Refresh();
            Session.Request("spellbook"); // spell names, icons and uses
        }
        int page = Input.IsKeyPressed(Key.Ctrl) ? 2 : Input.IsKeyPressed(Key.Shift) ? 1 : _basePage;
        if (page != Page) { Page = page; Redraw(); }
        bool targeting = User.TargetingFor != null && Casting.Interaction.Targeting != null;
        if (_noticeFor > 0) _noticeFor -= delta;
        string? text = targeting ? $"Choose a target for {User.TargetingFor}  ·  right-click to cancel" : _noticeFor > 0 ? _notice : null;
        _hint.Visible = text != null;
        if (text != null)
        {
            _hint.Text = text;
            _hint.AddThemeColorOverride("font_color", targeting ? Palette.AccentBright : Palette.Warning);
            _hint.Size = _hint.GetMinimumSize();
            _hint.Position = new Vector2((Size.X - _hint.Size.X) / 2, -_hint.Size.Y - 10);
            _hint.Modulate = new Color(1, 1, 1, targeting ? 1 : (float)Math.Clamp(_noticeFor / 0.4, 0, 1));
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k || k.AltPressed || UI.DM.ChatInput.Typing) return;
        // Slot keys come from Options → Controls (F1–F12 by default); Shift and Ctrl pick the second and third bar.
        string key = (k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode).ToString();
        var bindings = Settings.Bindings;
        for (int i = 0; i < Slots; i++)
        for (int j = 0; j < 2; j++)
        {
            if (bindings.Get(Preferences.ActionCatalog.Quickbar(i), j) is not { IsEmpty: false, Alt: false } b || b.Key != key) continue;
            int page = k.CtrlPressed ? 2 : k.ShiftPressed ? 1 : _basePage;
            UseSlot(page, i);
            GetViewport().SetInputAsHandled();
            return;
        }
    }

    // ------------------------------------------------------------------ public API

    /// <summary>Puts a spell on the first free slot of the current bar (the spellbook's right-click).</summary>
    public void Add(int spell, int cls) => Add(new BarSlot { Kind = "spell", Id = spell, Class = cls });

    public void Add(BarSlot slot)
    {
        var bar = _bars[Page];
        int free = Array.FindIndex(bar, s => s == null);
        if (free < 0) return;
        Set(Page, free, slot);
    }

    public BarSlot? Get(int page, int index) => _bars[page][index];

    /// <summary>Dropping an off-hand item onto a main-hand shortcut makes the measured native compound binding.</summary>
    public BarSlot CombineEquipmentDrop(BarSlot? current, BarSlot incoming, Variant data)
    {
        // Inventory entries already contain current authoritative slot eligibility. Do not wait for a name/icon lookup.
        if (ItemDragging.From(data) is { } source) Book.RememberInventoryItem(source.Item);
        if (current is not { Kind: "item", Prop: -1 } || incoming is not { Kind: "item", Prop: -1, Paired: false }
            || current.Item == incoming.Item || incoming.Item == 0) return incoming;
        int offSlots = Book.EquipableSlots(incoming.Item);
        if ((Book.EquipableSlots(current.Item) & 16) == 0 || (offSlots & 32) == 0) return incoming;
        var pair = current.Clone();
        pair.Paired = true;
        pair.SecondaryItem = incoming.Item;
        return pair;
    }

    /// <summary>
    /// A right-click on a slot (both interfaces): the radial for assigning to it or emptying it, opened at a screen
    /// point (docs/QUICKBAR_RADIAL.md).
    /// </summary>
    public void OpenSlotMenu(int page, int index, Vector2 at)
    {
        if ((uint)page >= Pages || (uint)index >= Slots) return;
        User.Interaction.QuickbarMenu?.Invoke(at, Get(page, index), slot => Set(page, index, slot));
    }

    /// <summary>A slot's rectangle on screen (captures).</summary>
    public Rect2 SlotRect(int index) => _views[index].GetGlobalRect();

    public void Set(int page, int index, BarSlot? slot)
    {
        // A manual edit wins over a character-file reply still in flight.
        _importing = _fillDefaults = false;
        _loaded = true;
        if (slot?.Kind == "item" && slot.Name.Length == 0 && Book.DescribeItem(slot.Item) is { } d) slot.Name = d.Name;
        if (slot is { Kind: "item", Item: > 0 }) Book.EquipableSlots(slot.Item);
        if (slot != null) Book.RememberItem(slot);
        _bars[page][index] = slot;
        Save(page * Slots + index);
        Redraw();
    }

    public void SetBasePage(int page)
    {
        _basePage = Math.Clamp(page, 0, Pages - 1);
        Page = _basePage;
        Save();
        Redraw();
    }

    public void UseSlot(int page, int index)
    {
        if (_ownerId != Session.ControlledId || !_sync.Ready || _bars[page][index] is not { } s) return;
        User.Use(s);
    }

    /// <summary>A sensible first bar for a character with none saved: attack, active feats, modes, potions, spells, rest.</summary>
    public void AutoFill()
    {
        var bar = _bars[0];
        var picks = new List<BarSlot> { new() { Kind = "command", Arg = "attack" } };
        picks.AddRange(Book.Feats.Where(f => f.Feat < 1089 || f.Feat > 1125).Take(3).Select(f => new BarSlot { Kind = "feat", Id = f.Feat }));
        picks.AddRange(Book.Modes.Where(m => m.Mode > 1).Take(2).Select(m => new BarSlot { Kind = "mode", Id = m.Mode }));
        picks.AddRange(Book.Items.Where(i => i.Usable && i.Powers.Length > 0).GroupBy(i => i.Name).Take(3).Select(g => new BarSlot { Kind = "item", Item = g.First().Item, Name = g.Key }));
        picks.AddRange(Book.Spells.Where(s => s.Ready > 0).GroupBy(s => s.Spell).Select(g => g.First()).Take(3).Select(s => new BarSlot { Kind = "spell", Id = s.Spell, Class = s.Class }));
        picks.Add(new BarSlot { Kind = "mode", Id = 1 });
        picks.Add(new BarSlot { Kind = "mode", Id = 0 });
        int i = 0;
        foreach (var p in picks)
        {
            while (i < Slots - 1 && bar[i] != null) i++;
            if (i >= Slots - 1) break;
            if (bar.Any(b => b != null && b.Kind == p.Kind && b.Id == p.Id && b.Arg == p.Arg && b.Item == p.Item)) continue;
            bar[i] = p;
        }
        bar[Slots - 1] ??= new BarSlot { Kind = "command", Arg = "rest" };
        Save();
        Redraw();
    }

    // ------------------------------------------------------------------ internals

    string _notice = "";
    double _noticeFor;

    /// <summary>A short message above the bar ("No uses of Rage left.").</summary>
    public void Notice(string text) { _notice = text; _noticeFor = 2.5; }

    void OnUsed(BarSlot s)
    {
        for (int i = 0; i < Slots; i++)
            if (_bars[Page][i] == s) _views[i].Flash();
    }

    void OnBookChanged()
    {
        if (_ownerId != Session.ControlledId) return;
        // Book raises Changed after parsing. Never depend on the order of NotifyReceived subscribers,
        // or fill from the previous creature's abilities while switching characters.
        if (_fillDefaults && Book.Loaded && Book.LoadedFor == _ownerId && _ownerId == Session.ControlledId)
        {
            _fillDefaults = false;
            AutoFill();
        }
        // Keep the requested dim/zero presentation after exhaustion, without binding another stack.
        bool changed = false;
        foreach (var bar in _bars)
            foreach (var s in bar)
            {
                if (s is not { Kind: "item" } || Book.LoadedFor != Session.ControlledId) continue;
                changed |= Book.RememberItem(s);
                if (s.ItemHasPower && Book.Loaded && Book.ResolveItem(s) == null && s.Item != 0)
                { s.Item = 0; changed = true; }
            }
        if (changed) Save();
        Redraw();
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Subject != _ownerId || _ownerId != Session.ControlledId) return;
        if (m.Kind == "quickbar-pair-lost")
        {
            using var lost = JsonDocument.Parse(m.Json);
            uint item = lost.RootElement.GetProperty("item").GetUInt32();
            for (int page = 0; page < Pages; page++)
                for (int index = 0; index < Slots; index++)
                    if (_bars[page][index] is { Paired: true } pair && (pair.Item == item || pair.SecondaryItem == item)) Set(page, index, null);
            return;
        }
        if (m.Kind == "spellbook") Redraw();
        if (m.Kind != "quickbar" || !_awaitingServer) return;
        using var doc = JsonDocument.Parse(m.Json);
        var data = doc.RootElement;
        var slots = data.GetProperty("slots").ValueKind == JsonValueKind.Array
            ? data.GetProperty("slots").Deserialize<BarSlot?[]>() ?? [] : [];
        _localCache = !data.TryGetProperty("localCache", out var cacheScope) || cacheScope.GetBoolean();
        if (_importing && _localCache) ReadLocalCache();
        _sync.Receive(slots);
        _awaitingServer = false;
        bool useServer = _importing && (!_localSaved || data.TryGetProperty("authoritative", out var authority) && authority.GetBoolean());
        _importing = false;
        _loaded = true;
        if (!useServer)
        {
            // Migrate old local layouts once; a character layout saved by OpenWinter wins on later loads in every vault mode.
            Save();
            Redraw();
            return;
        }
        foreach (var bar in _bars) Array.Clear(bar);
        if (data.GetProperty("present").GetBoolean())
        {
            for (int i = 0; i < Math.Min(slots.Length, Pages * Slots); i++)
                _bars[i / Slots][i % Slots] = slots[i];
            GD.Print($"[quickbar] Imported QBList for {_owner}: {slots.Count(s => s != null)} slots");
            Save();
            Redraw();
        }
        else
        {
            // Only characters without QBList get the generated bar; even an empty QBList is authoritative.
            _fillDefaults = true;
            OnBookChanged(); // the abilities may already have arrived before the quickbar reply
            if (_fillDefaults) Book.Refresh();
        }
    }

    void Redraw()
    {
        foreach (var v in _views) v?.QueueRedraw();
        _pager?.QueueRedraw();
    }

    static string Dir => "user://quickbar";
    string FilePath => $"{Dir}/{string.Concat(_owner.Where(char.IsLetterOrDigit))}.json";
    string LegacyPath => "user://quickbar_" + string.Concat(_owner.Where(char.IsLetterOrDigit)) + ".json";

    void Save(int changedSlot = -1)
    {
        if (_owner.Length == 0 || !_loaded || _ownerId != Session.ControlledId) return;
        foreach (var (index, slot) in _sync.Changes(_bars.SelectMany(bar => bar), changedSlot))
            Session.Request("quickbar-set", new { creature = _ownerId, index, slot });
        if (!_sync.Ready || !_localCache) return;
        DirAccess.MakeDirRecursiveAbsolute(Dir);
        using var f = Godot.FileAccess.Open(FilePath, Godot.FileAccess.ModeFlags.Write);
        f?.StoreString(JsonSerializer.Serialize(new QuickbarSave<BarSlot> { Page = _basePage, Bars = _bars }));
    }

    void Load()
    {
        _loaded = _importing = _fillDefaults = _localSaved = _localCache = false;
        _awaitingServer = false;
        _sync.Reset();
        foreach (var bar in _bars) Array.Clear(bar);
        _basePage = Page = 0;
        _awaitingServer = _importing = true;
        Session.Request("quickbar");
        Redraw();
    }

    void ReadLocalCache()
    {
        var file = QuickbarSave<BarSlot>.Read(
            Godot.FileAccess.FileExists(FilePath) ? Godot.FileAccess.GetFileAsString(FilePath) : null,
            Godot.FileAccess.FileExists(LegacyPath) ? Godot.FileAccess.GetFileAsString(LegacyPath) : null);
        _localSaved = file != null;
        if (file == null) return;
        _basePage = Page = Math.Clamp(file.Page, 0, Pages - 1);
        for (int p = 0; p < Math.Min(Pages, file.Bars?.Length ?? 0); p++)
            for (int i = 0; i < Math.Min(Slots, file.Bars![p]?.Length ?? 0); i++)
                if (file.Bars[p][i] is { Kind.Length: > 0 } s) _bars[p][i] = s;
    }

    /// <summary>The three pips at the left: which bar is showing; click one to switch.</summary>
    sealed partial class PageIndicator : Control
    {
        public ActionBar Bar = null!;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(18, 50);
            TooltipText = "bars";
            MouseFilter = MouseFilterEnum.Stop;
        }

        float PipY(int i) => Size.Y / 2 + 6 + (i - 1) * 11;

        public override void _Draw()
        {
            var font = Fonts.UiBold;
            // The bar's number above the pips.
            var num = (Bar.Page + 1).ToString();
            var ns = font.GetStringSize(num, HorizontalAlignment.Left, -1, 12);
            DrawString(font, new Vector2((Size.X - ns.X) / 2, PipY(0) - 9), num, HorizontalAlignment.Left, -1, 12, Bar.Page == Bar._basePage ? Palette.TextSecondary : Palette.AccentBright);
            for (int i = 0; i < Pages; i++)
            {
                bool on = i == Bar.Page;
                var c = new Vector2(Size.X / 2, PipY(i));
                if (on) DrawCircle(c, 5.5f, Palette.Accent with { A = 0.25f });
                DrawCircle(c, on ? 3.5f : 2.5f, on ? Palette.AccentBright : Palette.TextMuted);
                if (i == Bar._basePage && !on) DrawArc(c, 5, 0, MathF.Tau, 16, Palette.AccentDim, 1, true);
            }
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
            {
                int best = 0;
                for (int i = 1; i < Pages; i++) if (Math.Abs(mb.Position.Y - PipY(i)) < Math.Abs(mb.Position.Y - PipY(best))) best = i;
                Bar.SetBasePage(best);
                AcceptEvent();
            }
            else if (e is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown, Pressed: true } wheel)
            {
                Bar.SetBasePage((Bar._basePage + (wheel.ButtonIndex == MouseButton.WheelDown ? 1 : Pages - 1)) % Pages);
                AcceptEvent();
            }
        }

        public override GodotObject _MakeCustomTooltip(string forText)
        {
            var box = Ui.VBox(4);
            box.AddChild(Ui.Text($"Action bar {Bar.Page + 1} of {Pages}", Ui.TextStyle.Strong, Palette.AccentBright));
            var row = Ui.HBox(6);
            row.AddChild(KeyCap.Create("Shift"));
            row.AddChild(Ui.Text("hold for bar 2", Ui.TextStyle.Small));
            box.AddChild(row);
            var row2 = Ui.HBox(6);
            row2.AddChild(KeyCap.Create("Ctrl"));
            row2.AddChild(Ui.Text("hold for bar 3", Ui.TextStyle.Small));
            box.AddChild(row2);
            box.AddChild(Ui.Text("Click a pip or scroll to switch bars", Ui.TextStyle.Caption, Palette.TextMuted));
            return box;
        }
    }

    /// <summary>One slot: icon, key cap, uses badge, mode glow, use flash; drag and drop; right-click opens its radial.</summary>
    public sealed partial class ActionSlotView : Control
    {
        public ActionBar Bar = null!;
        public int Index;
        const float Size_ = 50;
        bool _hover, _dragSource;
        double _flash, _sweep;

        public override void _Ready()
        {
            CustomMinimumSize = new Vector2(Size_, Size_);
            MouseFilter = MouseFilterEnum.Stop;
            TooltipText = "slot";
            MouseEntered += () => { _hover = true; QueueRedraw(); };
            MouseExited += () => { _hover = false; QueueRedraw(); };
        }

        BarSlot? Slot => Bar._bars[Bar.Page][Index];
        string Key => Preferences.InputBindings.KeyText(Preferences.ActionCatalog.Quickbar(Index)) is { Length: > 0 } text ? text : $"F{Index + 1}";

        public void Flash() { _flash = 0.25; _sweep = 1; QueueRedraw(); }

        public override void _Process(double delta)
        {
            bool animate = _flash > 0 || _sweep > 0 || (Slot != null && Bar.User.Look(Slot).Toggled) || GetViewport().GuiIsDragging();
            if (_flash > 0) _flash -= delta;
            if (_sweep > 0) _sweep -= delta; // one second of "activated" sweep
            if (animate) QueueRedraw();
        }

        public override void _Draw()
        {
            var rect = new Rect2(Vector2.Zero, Size);
            var slot = Slot;
            var look = slot != null ? Bar.User.Look(slot) : null;
            bool dragging = GetViewport().GuiIsDragging();
            float pulse = 0.5f + 0.5f * MathF.Sin((float)Time.GetTicksMsec() / 260f);

            // Glow behind a mode that's on.
            if (look?.Toggled == true)
            {
                var glow = Styles.Box(Palette.Accent with { A = 0.0f }, Palette.RadiusSmall + 2, null, 0, 0);
                glow.ShadowColor = Palette.Accent with { A = 0.35f + 0.25f * pulse };
                glow.ShadowSize = 9;
                DrawStyleBox(glow, rect);
            }
            var bg = Styles.Box(new Color(0.03f, 0.035f, 0.05f, slot == null ? 0.55f : 0.9f), Palette.RadiusSmall,
                look?.Toggled == true ? Palette.AccentBright : _hover ? Palette.Accent : dragging ? Palette.AccentDim : Palette.LineStrong,
                look?.Toggled == true ? 2 : 1, 0);
            DrawStyleBox(bg, rect);

            if (look != null)
            {
                var iconRect = rect.Grow(-3);
                float press = _flash > 0 ? (float)(_flash / 0.25) : 0;
                if (press > 0) iconRect = iconRect.Grow(-2 * press);
                SlotArt.DrawIcon(this, Bar.Session.Content, look, iconRect, look.Usable ? Colors.White : new Color(0.45f, 0.42f, 0.4f));
                if (!look.Usable) DrawStyleBox(Styles.Box(new Color(0, 0, 0, 0.35f), Palette.RadiusSmall - 2, null, 0, 0), iconRect);
                SlotArt.DrawSweep(this, iconRect, (float)Math.Clamp(_sweep, 0, 1), new Color(0, 0, 0, 0.45f));
                if (press > 0) DrawStyleBox(Styles.Box(Palette.AccentBright with { A = 0.25f * press }, Palette.RadiusSmall, null, 0, 0), rect);
                if (look.Uses >= 0) SlotArt.DrawBadge(this, look.Uses.ToString(), rect, look.Uses == 0 ? Palette.Danger : Palette.Text);
                // Item bar slots show a little marker so they read as items, not spells.
                if (slot!.Kind == "item" && look.Toggled) DrawCircle(rect.End - new Vector2(6, 6), 2.5f, Palette.Success);
                // A text macro shows its label along the bottom (the classic bar draws it where counts go).
                if (slot.Kind == "macro" && slot.Name.Length > 0)
                {
                    var labelFont = Fonts.UiBold;
                    float width = Math.Min(labelFont.GetStringSize(slot.Name, HorizontalAlignment.Left, -1, 10).X, Size.X - 4);
                    DrawString(labelFont, new Vector2(Math.Max(2, (Size.X - width) / 2), Size.Y - 4), slot.Name, HorizontalAlignment.Left, Size.X - 4, 10, Palette.Text);
                }
            }
            else if (_hover || dragging)
            {
                DrawLine(rect.GetCenter() - new Vector2(6, 0), rect.GetCenter() + new Vector2(6, 0), Palette.TextMuted, 1.5f, true);
                DrawLine(rect.GetCenter() - new Vector2(0, 6), rect.GetCenter() + new Vector2(0, 6), Palette.TextMuted, 1.5f, true);
            }

            // Key cap, top left.
            var font = Fonts.UiBold;
            const int fs = 10;
            var ts = font.GetStringSize(Key, HorizontalAlignment.Left, -1, fs);
            var cap = new Rect2(new Vector2(2, 2), new Vector2(ts.X + 6, 13));
            DrawStyleBox(Styles.Box(new Color(0, 0, 0, 0.62f), 3, null, 0, 0), cap);
            DrawString(font, cap.Position + new Vector2(3, 10.5f), Key, HorizontalAlignment.Left, -1, fs, look != null ? Palette.TextSecondary : Palette.TextMuted);
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is not InputEventMouseButton mb) return;
            // Right-click: the slot's radial (assign something, use it, or empty it), opened on the press so a held
            // button can be let go over a choice; its release comes back here and goes no further.
            if (mb.ButtonIndex == MouseButton.Right) { if (mb.Pressed) Bar.OpenSlotMenu(Bar.Page, Index, mb.GlobalPosition); AcceptEvent(); return; }
            if (mb.Pressed) return;
            if (mb.ButtonIndex == MouseButton.Left && new Rect2(Vector2.Zero, Size).HasPoint(mb.Position)) { Bar.UseSlot(Bar.Page, Index); AcceptEvent(); }
        }

        public override Variant _GetDragData(Vector2 atPosition)
        {
            if (Slot is not { } s) return default;
            var data = s.ToDrag();
            data["fromBar"] = Bar.Page;
            data["fromSlot"] = Index;
            _dragSource = true;
            SetDragPreview(SlotPreview.Centered(Bar.User.Look(s), Bar.Session.Content));
            return data;
        }

        public override bool _CanDropData(Vector2 atPosition, Variant data) => BarSlot.FromDrag(data) != null;

        public override void _DropData(Vector2 atPosition, Variant data)
        {
            if (BarSlot.FromDrag(data) is not { } incoming) return;
            var d = data.VariantType == Variant.Type.Dictionary ? data.AsGodotDictionary() : new Godot.Collections.Dictionary();
            if (d.ContainsKey("fromBar") && d.ContainsKey("fromSlot"))
            {
                // Moving within the bar swaps with whatever was here.
                int fromPage = (int)d["fromBar"].AsInt64(), fromSlot = (int)d["fromSlot"].AsInt64();
                var here = Slot;
                Bar._bars[fromPage][fromSlot] = here;
                Bar._dropHandled = true;
            }
            else incoming = Bar.CombineEquipmentDrop(Slot, incoming, data);
            Bar.Set(Bar.Page, Index, incoming);
        }

        public override void _Notification(int what)
        {
            if (what != NotificationDragEnd || !_dragSource) return;
            _dragSource = false;
            // Dragged off the bar (not onto another slot): clear it.
            if (!GetViewport().GuiIsDragSuccessful() && !Bar._dropHandled) Bar.Set(Bar.Page, Index, null);
            Bar._dropHandled = false;
            QueueRedraw();
        }

        public override GodotObject? _MakeCustomTooltip(string forText)
        {
            if (Slot is not { } s) return SlotArt.Tooltip(new SlotLook("Empty slot", [], "", -1, false, true, "", ""), Key,
                "Drag spells, items, feats and skills here, or right-click to choose", "K opens your abilities");
            return SlotArt.Tooltip(Bar.User.Look(s), Key, "Right-click to change or empty it, or drag it off the bar");
        }
    }

    bool _dropHandled;
}

/// <summary>The icon that follows the mouse while dragging something to the bar (centred on the cursor).</summary>
public partial class SlotPreview : Control
{
    public SlotLook Look = null!;
    public Views.ClientContent? Content;

    public static Control Centered(SlotLook look, Views.ClientContent? content)
    {
        var holder = new Control { MouseFilter = MouseFilterEnum.Ignore };
        holder.AddChild(new SlotPreview { Look = look, Content = content, Position = new Vector2(-23, -23), Size = new Vector2(46, 46) });
        return holder;
    }

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Modulate = new Color(1, 1, 1, 0.92f);
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        DrawStyleBox(Styles.Box(Palette.Bg0, Palette.RadiusSmall, Palette.Accent, 1, 0, shadow: 10), rect);
        SlotArt.DrawIcon(this, Content, Look, rect.Grow(-3), Colors.White);
    }
}
