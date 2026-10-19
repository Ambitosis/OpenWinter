using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Preferences;
using OpenWinter.Client.UI.FrontEnd;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// Save / Load (O, or the game menu), with quick save and quick load keys (Ctrl+F5 / Ctrl+F9 by default).
/// Saves are cards with a screenshot thumbnail, name, character and area, play time and date. Save mode
/// starts with a "New save" card and a name field; picking a manual save overwrites it. Saving sends a
/// screenshot taken before the window (or the game menu) opened, stored as the save's screen.tga.
/// Single player only: the server ignores these requests unless save games are enabled.
/// </summary>
public partial class SaveLoadPanel : UiTheme.Window
{
    public enum Mode { Save, Load }

    public ClientSession Session = null!;
    /// <summary>Movement keys are typed into the name field rather than walking while it has focus.</summary>
    public PlayerInput? Input_;
    /// <summary>Result lines ("Game saved", errors) for the chat log.</summary>
    public Action<string>? Feedback;

    readonly List<SaveEntry> _saves = new();
    readonly Dictionary<string, Texture2D?> _thumbs = new();
    SaveGrid _grid = null!;
    LineEdit _name = null!;
    Label _status = null!;
    Button _action = null!, _saveTab = null!, _loadTab = null!;
    Control _nameRow = null!;
    Mode _mode = Mode.Save;
    byte[]? _shot;
    PanelContainer? _newCard;

    public static SaveLoadPanel Create(ClientSession session)
    {
        var p = Create<SaveLoadPanel>("Save / Load Game", new Vector2(1100, 700), "save");
        p.Theme = GameTheme.Theme;
        p.Session = session;
        p.AnchorLeft = p.AnchorRight = p.AnchorTop = p.AnchorBottom = 0.5f;
        p.OffsetLeft = -550; p.OffsetRight = 550; p.OffsetTop = -350; p.OffsetBottom = 350;
        return p;
    }

    public override void _Ready()
    {
        var group = new ButtonGroup();
        _saveTab = Tab("Save", group, () => SetMode(Mode.Save));
        _loadTab = Tab("Load", group, () => SetMode(Mode.Load));
        HeaderExtras.AddChild(_saveTab);
        HeaderExtras.AddChild(_loadTab);
        HeaderExtras.AddChild(new Control { CustomMinimumSize = new Vector2(12, 0) });

        _grid = new SaveGrid();
        Body.AddChild(_grid);
        _grid.SelectionChanged = OnSelection;
        _grid.Activated = s => { if (_mode == Mode.Load) Load(s); else { _name.Text = s.Name; Save(); } };

        Body.AddChild(new Divider { Color = Palette.LineStrong, Fade = Divider.FadeMode.None, CustomMinimumSize = new Vector2(0, 1) });
        var bar = Ui.HBox(Palette.Space3);
        Body.AddChild(bar);
        var hints = FrontEndUi.KeyHints((InputBindings.KeyText(ActionCatalog.QuickSave).Replace("+", " "), "Quick save"), (InputBindings.KeyText(ActionCatalog.QuickLoad).Replace("+", " "), "Quick load"));
        bar.AddChild(hints);
        _status = Ui.Text("", Ui.TextStyle.Caption, Palette.TextSecondary);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.HorizontalAlignment = HorizontalAlignment.Right;
        _status.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(_status);
        var nameRow = Ui.HBox(Palette.Space2);
        _nameRow = nameRow;
        _name = new LineEdit { PlaceholderText = "Name this save", CustomMinimumSize = new Vector2(280, 42), MaxLength = 40 };
        _name.TextSubmitted += _ => Save();
        _name.TextChanged += _ => UpdateAction();
        _name.FocusEntered += () => { if (Input_ != null) Input_.Scripted = (Vector2.Zero, false); };
        _name.FocusExited += () => { if (Input_ != null) Input_.Scripted = null; };
        nameRow.AddChild(_name);
        bar.AddChild(nameRow);
        _action = Ui.Primary("Save", () => { if (_mode == Mode.Save) Save(); else if (_grid.Selected is { } s) Load(s); });
        _action.CustomMinimumSize = new Vector2(150, 42);
        _action.AddThemeFontSizeOverride("font_size", Palette.FontLarge);
        bar.AddChild(_action);

        Closed += () => _name.ReleaseFocus();
        Session.NotifyReceived += OnNotify;
        var args = OS.GetCmdlineUserArgs();
        int at = Array.IndexOf(args, "--capture-saves");
        if (at >= 0 && at + 1 < args.Length) _ = CaptureDemo(args[at + 1]);
    }

    static Button Tab(string text, ButtonGroup group, Action pressed)
    {
        var b = new Button { Text = text, ToggleMode = true, ButtonGroup = group, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(90, 32) };
        var idle = Styles.Box(Colors.Transparent, Palette.RadiusSmall, Palette.Line, 1, 12, 4);
        var on = Styles.Box(Palette.Accent with { A = 0.16f }, Palette.RadiusSmall, Palette.Accent, 1, 12, 4);
        b.AddThemeStyleboxOverride("normal", idle);
        b.AddThemeStyleboxOverride("hover", Styles.Box(new Color(1, 1, 1, 0.05f), Palette.RadiusSmall, Palette.LineStrong, 1, 12, 4));
        b.AddThemeStyleboxOverride("pressed", on);
        b.AddThemeStyleboxOverride("hover_pressed", on);
        b.AddThemeColorOverride("font_pressed_color", Palette.AccentBright);
        b.Pressed += pressed;
        return b;
    }

    /// <summary>Takes the thumbnail for the next save now, without the interface (call before menus cover the game).</summary>
    public async Task TakeScreenshot() => _shot = await CleanScreenshot();

    /// <summary>Opens the window in a mode; <paramref name="keepScreenshot"/> uses the one taken earlier (opened from the game menu).</summary>
    public void Open(Mode mode, bool keepScreenshot = false) => _ = OpenAsync(mode, keepScreenshot);

    async Task OpenAsync(Mode mode, bool keepScreenshot)
    {
        if (!keepScreenshot || _shot == null) _shot = await CleanScreenshot();
        Visible = true;
        SetMode(mode);
        _status.Text = "";
        Session.Request("list-saves");
    }

    public void Toggle()
    {
        if (Visible) { Close(); return; }
        Open(_mode);
    }

    void SetMode(Mode mode)
    {
        _mode = mode;
        (mode == Mode.Save ? _saveTab : _loadTab).SetPressedNoSignal(true);
        (mode == Mode.Save ? _loadTab : _saveTab).SetPressedNoSignal(false);
        TitleLabel.Text = mode == Mode.Save ? "Save Game" : "Load Game";
        _nameRow.Visible = mode == Mode.Save;
        Rebuild();
    }

    void Rebuild()
    {
        var list = _mode == Mode.Save ? _saves.Where(s => s.Kind == "manual") : _saves;
        _newCard = _mode == Mode.Save ? NewSaveCard() : null;
        _grid.EmptyText = "No saved games yet.";
        _grid.Set(list, _newCard);
        foreach (var (folder, tex) in _thumbs) _grid.SetThumbnail(folder, tex);
        if (_mode == Mode.Load) _grid.SelectFirst();
        else SelectNew();
        UpdateAction();
    }

    PanelContainer NewSaveCard()
    {
        var card = new PanelContainer { CustomMinimumSize = new Vector2(SaveCard.Width, 0), MouseDefaultCursorShape = CursorShape.PointingHand };
        var (normal, hover, selected) = FrontEndUi.CardStyles(10);
        card.AddThemeStyleboxOverride("panel", selected);
        var col = Ui.VBox(Palette.Space2);
        col.MouseFilter = MouseFilterEnum.Ignore;
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(SaveCard.Width - 20, (SaveCard.Width - 20) * 10 / 16), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg0, 6, Palette.Accent with { A = 0.4f }, 1, 0));
        if (_shot != null && FrontEndContent.Tga(_shot) is { } tex)
            frame.AddChild(new TextureRect { Texture = tex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0.55f) });
        var plus = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        plus.AddChild(Icons.Rect("save", 40, Palette.AccentBright));
        frame.AddChild(plus);
        col.AddChild(frame);
        col.AddChild(Ui.Text("New save", Ui.TextStyle.Strong, Palette.AccentBright, Palette.FontBody + 1));
        col.AddChild(Ui.Text(Session.Area?.Info.Name is { Length: > 0 } a ? NwnText.Strip(a) : "Current game", Ui.TextStyle.Small));
        card.AddChild(col);
        card.GuiInput += e =>
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) SelectNew();
        };
        card.SetMeta("styles", new Godot.Collections.Array { normal, hover, selected });
        return card;
    }

    void SelectNew()
    {
        _grid.Select(null);
        if (_newCard != null) _newCard.AddThemeStyleboxOverride("panel", FrontEndUi.CardStyles(10).Selected);
        if (_name.Text.Length == 0 || _saves.Any(s => s.Name == _name.Text)) _name.Text = DefaultName();
        UpdateAction();
    }

    string DefaultName()
    {
        var area = NwnText.Strip(Session.Area?.Info.Name ?? "");
        return area.Length > 0 ? area : "Saved game";
    }

    void OnSelection(SaveEntry? s)
    {
        if (s != null && _newCard != null) _newCard.AddThemeStyleboxOverride("panel", FrontEndUi.CardStyles(10).Normal);
        if (_mode == Mode.Save && s != null) _name.Text = s.Name;
        UpdateAction();
        if (s != null) _status.Text = $"{s.Module}  ·  saved {s.SavedAt:f}";
    }

    void UpdateAction()
    {
        if (_action == null) return;
        if (_mode == Mode.Load)
        {
            _action.Text = "Load";
            _action.Disabled = _grid.Selected == null;
            return;
        }
        bool overwrite = _grid.Selected is { Kind: "manual" } s && s.Name.Equals(_name.Text.Trim(), StringComparison.OrdinalIgnoreCase)
            || _saves.Any(x => x.Kind == "manual" && x.Name.Equals(_name.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        _action.Text = overwrite ? "Overwrite" : "Save";
        _action.Disabled = _name.Text.Trim().Length == 0;
    }

    public override void _Input(InputEvent e)
    {
        // Quick save / load before the quickbar or other windows see the key (players may bind F-keys); never while
        // typing, since the original's quick save key is a letter (G).
        if (e is not InputEventKey || GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return;
        if (InputBindings.Pressed(e, ActionCatalog.QuickSave))
        {
            GetViewport().SetInputAsHandled();
            _ = QuickSave();
        }
        else if (InputBindings.Pressed(e, ActionCatalog.QuickLoad))
        {
            Session.Request("quickload");
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!PlayerInput.Blocked && InputBindings.Pressed(e, ActionCatalog.SaveLoad)) { Toggle(); GetViewport().SetInputAsHandled(); }
    }

    async Task QuickSave()
    {
        var shot = await CleanScreenshot();
        Session.Request("quicksave", new { screenshot = shot == null ? "" : Convert.ToBase64String(shot) });
    }

    /// <summary>
    /// The game view without the interface: every canvas layer is hidden for a frame, the frame is
    /// grabbed, and they come back.
    /// </summary>
    async Task<byte[]?> CleanScreenshot()
    {
        if (!IsInsideTree()) return null;
        var root = GetTree().Root;
        var hidden = root.FindChildren("*", "CanvasLayer", true, false).OfType<CanvasLayer>().Where(l => l.Visible).ToList();
        var topLevel = root.GetChildren().OfType<Control>().Where(c => c.Visible).ToList();
        foreach (var l in hidden) l.Visible = false;
        foreach (var c in topLevel) c.Visible = false;
        try
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            return Screenshot();
        }
        finally
        {
            foreach (var l in hidden) if (IsInstanceValid(l)) l.Visible = true;
            foreach (var c in topLevel) if (IsInstanceValid(c)) c.Visible = true;
        }
    }

    /// <summary>Saves under a name (scripted captures; the window's Save button).</summary>
    public void SaveAs(string name)
    {
        _name.Text = name;
        Save();
    }

    void Save()
    {
        var name = _name.Text.Trim();
        if (name.Length == 0) { _status.Text = "Enter a name for the save."; return; }
        // Saving over a manual save with the same name replaces it.
        var same = _grid.Selected is { Kind: "manual" } sel && sel.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ? sel
            : _saves.FirstOrDefault(s => s.Kind == "manual" && s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        Session.Request("save-game", new { name, folder = same?.Folder ?? "", screenshot = _shot == null ? "" : Convert.ToBase64String(_shot) });
        _status.Text = "Saving…";
    }

    void Load(SaveEntry s)
    {
        Session.Request("load-game", new { folder = s.Folder });
        _status.Text = "Loading…";
        Close();
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Subject != Session.ControlledId) return;
        if (m.Kind == "save-result")
        {
            using var doc = JsonDocument.Parse(m.Json);
            var message = doc.RootElement.GetProperty("message").GetString() ?? "";
            bool ok = doc.RootElement.TryGetProperty("ok", out var okEl) && okEl.ValueKind == JsonValueKind.True;
            _status.Text = message;
            _status.AddThemeColorOverride("font_color", ok ? Palette.Success : Palette.Danger);
            Feedback?.Invoke(message);
            return;
        }
        if (m.Kind == "save-screenshot")
        {
            using var doc = JsonDocument.Parse(m.Json);
            var folder = Str(doc.RootElement, "folder");
            var b64 = Str(doc.RootElement, "tga");
            Texture2D? tex = null;
            if (b64.Length > 0) try { tex = FrontEndContent.Tga(Convert.FromBase64String(b64)); } catch (FormatException) { }
            _thumbs[folder] = tex;
            _grid.SetThumbnail(folder, tex);
            return;
        }
        if (m.Kind != "saves-list") return;
        using var list = JsonDocument.Parse(m.Json);
        _saves.Clear();
        foreach (var s in list.RootElement.GetProperty("saves").EnumerateArray())
        {
            DateTime.TryParse(Str(s, "savedAt"), out var at);
            double played = s.TryGetProperty("played", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDouble() : 0;
            bool shot = s.TryGetProperty("screenshot", out var sh) && sh.ValueKind == JsonValueKind.True;
            _saves.Add(new SaveEntry(Str(s, "folder"), Str(s, "name"), Str(s, "kind"), at, NwnText.Strip(Str(s, "area")), NwnText.Strip(Str(s, "player")), played, NwnText.Strip(Str(s, "module")), shot));
        }
        // Thumbnails: ask for those not fetched yet (a re-saved slot gets a new one).
        foreach (var s in _saves.Where(s => s.HasScreen))
        {
            _thumbs.Remove(s.Folder);
            Session.Request("save-screenshot", new { folder = s.Folder });
        }
        if (Visible) Rebuild();
    }

    /// <summary>
    /// --capture-saves dir: screenshots of saving, walking away, loading the save back and a quick
    /// save / quick load (development check of the whole client flow).
    /// </summary>
    async Task CaptureDemo(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(5);
        Open(Mode.Save);
        await Wait(1);
        await Shot(dir, "30_save_window");
        SaveAs("Capture demo");
        await Wait(2.5);
        await Shot(dir, "31_saved");
        Close();
        var start = Session.Controlled?.Position;
        Session.Send(new MoveInputMessage { Direction = new System.Numerics.Vector3(0, 1, 0) }.Write());
        await Wait(2.5);
        Session.Send(new MoveInputMessage { Direction = System.Numerics.Vector3.Zero }.Write());
        await Wait(0.5);
        GD.Print($"[capture] walked from {start} to {Session.Controlled?.Position}");
        await Shot(dir, "32_walked");
        Open(Mode.Load);
        await Wait(1.5);
        await Shot(dir, "32b_load_window");
        var save = _saves.FirstOrDefault(x => x.Name == "Capture demo");
        if (save != null) Load(save);
        await Wait(5);
        GD.Print($"[capture] after load at {Session.Controlled?.Position}");
        await Shot(dir, "33_loaded");
        Session.Request("quicksave");
        await Wait(2);
        Session.Request("quickload");
        await Wait(5);
        await Shot(dir, "34_quickloaded");
        GetTree().Quit();
    }

    async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    async Task Shot(string dir, string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir, name + ".png"));
        GD.Print($"[capture] {name}: {_status.Text}");
    }

    static string Str(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    /// <summary>The current view as a small uncompressed TGA (screen.tga), or null when there is nothing to grab.</summary>
    byte[]? Screenshot()
    {
        var image = GetViewport()?.GetTexture()?.GetImage();
        if (image == null || image.GetWidth() == 0) return null;
        const int w = 320, h = 180;
        // Crop to 16:9 first so the thumbnail is not squashed.
        int iw = image.GetWidth(), ih = image.GetHeight();
        int cw = Math.Min(iw, ih * 16 / 9), ch = Math.Min(ih, iw * 9 / 16);
        image = image.GetRegion(new Rect2I((iw - cw) / 2, (ih - ch) / 2, cw, ch));
        image.Resize(w, h, Image.Interpolation.Lanczos);
        image.Convert(Image.Format.Rgb8);
        var rgb = image.GetData();
        var tga = new byte[18 + w * h * 3];
        tga[2] = 2; // uncompressed true-colour
        tga[12] = w & 0xFF; tga[13] = w >> 8;
        tga[14] = h & 0xFF; tga[15] = h >> 8;
        tga[16] = 24; // bits per pixel; descriptor 0 = rows bottom-up
        int o = 18;
        for (int y = h - 1; y >= 0; y--)
            for (int x = 0; x < w; x++)
            {
                int p = (y * w + x) * 3;
                tga[o++] = rgb[p + 2];
                tga[o++] = rgb[p + 1];
                tga[o++] = rgb[p];
            }
        return tga;
    }
}
