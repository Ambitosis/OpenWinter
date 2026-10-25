using OpenWinter.Client.UI.Kit;
using OpenWinter.Game.SaveGames;
using Godot;

namespace OpenWinter.Client.UI.FrontEnd;

/// <summary>Load Game from the main menu: the single-player saves read straight from disk.</summary>
public partial class LoadGameScreen : VBoxContainer
{
    public required GamePathsHolder Paths;
    /// <summary>Load this save (its module path may be empty for saves from before module paths were stored).</summary>
    public Action<SaveEntry>? Load;
    SaveGrid _grid = null!;
    Button _load = null!;
    Label _detail = null!;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", Palette.Space4);
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(FrontEndUi.Header("Load game", "Continue a journey", "Single-player saves, newest first"));
        _grid = new SaveGrid { EmptyText = "No saved games yet. Start a new game and save with Ctrl+F5 or from the game menu." };
        AddChild(_grid);
        var bar = Ui.HBox(Palette.Space3);
        _detail = Ui.Text("", Ui.TextStyle.Small);
        _detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _detail.ClipText = true;
        _detail.VerticalAlignment = VerticalAlignment.Center;
        bar.AddChild(_detail);
        _load = Ui.Primary("Load", () => { if (_grid.Selected is { } s) Load?.Invoke(s); });
        _load.CustomMinimumSize = new Vector2(180, 46);
        _load.AddThemeFontSizeOverride("font_size", Palette.FontLarge);
        bar.AddChild(_load);
        AddChild(bar);
        _grid.SelectionChanged = s =>
        {
            _load.Disabled = s == null;
            _detail.Text = s == null ? "" : $"{s.Module}  ·  saved {s.SavedAt:f}";
        };
        _grid.Activated = s => Load?.Invoke(s);
        Refresh();
    }

    public static List<SaveEntry> ReadSaves(string dir) =>
        SaveGameService.List(dir).Select(s => new SaveEntry(s.Folder, s.Name, s.Kind.ToString().ToLowerInvariant(), s.SavedAt, Kit.NwnText.Strip(s.AreaName),
            Kit.NwnText.Strip(s.PlayerName), s.PlayedSeconds, Kit.NwnText.Strip(s.Module), s.HasScreen, s.ModulePath)).ToList();

    public void Refresh()
    {
        var dir = Paths.SavesDir;
        var saves = ReadSaves(dir);
        _grid.Set(saves);
        _grid.SelectFirst();
        foreach (var s in saves.Where(s => s.HasScreen))
            _grid.SetThumbnail(s.Folder, FrontEndContent.Tga(SaveGameService.ReadScreen(dir, s.Folder)));
    }

    public void FocusFirst() { if (_grid.Cards.Count > 0) _grid.Cards[0].GrabFocus(); }
}

/// <summary>Multiplayer: join a server by address (with the recently joined ones), player name and DM login, or an
/// NWN:EE server that runs WSNetLayer.</summary>
public partial class MultiplayerScreen : VBoxContainer
{
    public Action<Dictionary<string, string>>? Join;
    LineEdit _address = null!, _name = null!, _password = null!;
    CheckButton _dm = null!, _nwn = null!;
    VBoxContainer _recent = null!;

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", Palette.Space5);
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(FrontEndUi.Header("Multiplayer", "Join a server", "Persistent worlds and hosted games"));
        var split = Ui.HBox(Palette.Space5);
        split.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(split);

        var form = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkBegin };
        form.AddThemeStyleboxOverride("panel", Styles.Card(22));
        split.AddChild(form);
        var col = Ui.VBox(Palette.Space2);
        form.AddChild(col);
        var profile = Settings.Current.Profile;
        col.AddChild(Ui.Text("Server address", Ui.TextStyle.Caption, Palette.TextSecondary));
        _address = new LineEdit { Text = profile.RecentServers.FirstOrDefault()?.Address ?? "127.0.0.1:5121", PlaceholderText = "host or host:port", CustomMinimumSize = new Vector2(0, 40) };
        _address.TextSubmitted += _ => Connect();
        col.AddChild(_address);
        // NWN:EE servers are reached over WSNetLayer's websocket (ws:// or wss:// addresses).
        _nwn = new CheckButton { Text = "NWN:EE server (WSNetLayer)", ButtonPressed = IsNwnAddress(_address.Text) };
        _address.TextChanged += t => { if (IsNwnAddress(t)) _nwn.ButtonPressed = true; };
        col.AddChild(_nwn);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        col.AddChild(Ui.Text("Player name", Ui.TextStyle.Caption, Palette.TextSecondary));
        _name = new LineEdit { Text = profile.PlayerName.Length > 0 ? profile.PlayerName : System.Environment.UserName, MaxLength = 32, CustomMinimumSize = new Vector2(0, 40) };
        col.AddChild(_name);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
        _dm = new CheckButton { Text = "Log in as Dungeon Master" };
        col.AddChild(_dm);
        _password = new LineEdit { PlaceholderText = "DM password", Secret = true, Editable = false, CustomMinimumSize = new Vector2(0, 40), Modulate = new Color(1, 1, 1, 0.5f) };
        _dm.Toggled += on => { _password.Editable = on; _password.Modulate = new Color(1, 1, 1, on ? 1 : 0.5f); };
        col.AddChild(_password);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        var connect = Ui.Primary("Connect", Connect);
        connect.CustomMinimumSize = new Vector2(0, 46);
        connect.AddThemeFontSizeOverride("font_size", Palette.FontLarge);
        col.AddChild(connect);

        var right = Ui.VBox(Palette.Space2);
        right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        split.AddChild(right);
        right.AddChild(Ui.Section("Recent servers"));
        _recent = Ui.VBox(Palette.Space2);
        right.AddChild(_recent);
        BuildRecent();
    }

    void BuildRecent()
    {
        FrontEndUi.Clear(_recent);
        var list = Settings.Current.Profile.RecentServers;
        if (list.Count == 0)
        {
            var none = Ui.Text("Servers you join appear here.", Ui.TextStyle.Small, Palette.TextMuted);
            _recent.AddChild(none);
            return;
        }
        var (normal, hover, _) = FrontEndUi.CardStyles(12);
        foreach (var r in list)
        {
            var card = new PanelContainer { MouseDefaultCursorShape = CursorShape.PointingHand };
            card.AddThemeStyleboxOverride("panel", normal);
            var row = Ui.HBox(Palette.Space3);
            row.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(Icons.Rect("party", 22, Palette.Accent));
            var text = Ui.VBox(1);
            text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            text.MouseFilter = MouseFilterEnum.Ignore;
            text.AddChild(Ui.Text(r.Address, Ui.TextStyle.Strong));
            text.AddChild(Ui.Text("Last joined " + FrontEndContent.FormatAgo(r.LastJoined), Ui.TextStyle.Caption, Palette.TextMuted));
            row.AddChild(text);
            row.AddChild(KeyCap.Create("Double-click to join", 10));
            card.AddChild(row);
            var address = r.Address;
            card.MouseEntered += () => card.AddThemeStyleboxOverride("panel", hover);
            card.MouseExited += () => card.AddThemeStyleboxOverride("panel", normal);
            card.GuiInput += e =>
            {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
                {
                    _address.Text = address;
                    _nwn.ButtonPressed = IsNwnAddress(address);
                    if (mb.DoubleClick) Connect();
                }
            };
            _recent.AddChild(card);
        }
    }

    void Connect()
    {
        var address = _address.Text.Trim();
        if (address.Length == 0) { _address.GrabFocus(); return; }
        // NWN:EE servers are remembered by their websocket address, so the recent list rejoins them the same way.
        var nwn = _nwn.ButtonPressed || IsNwnAddress(address);
        if (nwn && !IsNwnAddress(address)) address = "ws://" + address;
        var name = _name.Text.Trim();
        Settings.Update(s =>
        {
            s.Profile.PlayerName = name;
            s.Profile.Remember(address, DateTime.Now);
        });
        var args = new Dictionary<string, string> { ["name"] = name.Length > 0 ? name : "Player" };
        if (nwn) args["connect-nwn"] = address;
        else
        {
            args["connect"] = address;
            if (_dm.ButtonPressed) { args["dm"] = "true"; args["dm-password"] = _password.Text; }
        }
        Join?.Invoke(args);
    }

    static bool IsNwnAddress(string address) =>
        address.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) || address.StartsWith("wss://", StringComparison.OrdinalIgnoreCase);

    public void FocusFirst() => _address.GrabFocus();
}
