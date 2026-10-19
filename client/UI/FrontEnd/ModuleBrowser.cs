using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.FrontEnd;

/// <summary>
/// New Game: the playable modules as cards (loading-screen art, name, where it comes from), and the
/// selected one in detail: large art, facts (haks, size, areas), its description and Play.
/// </summary>
public partial class ModuleBrowser : VBoxContainer
{
    public required GamePathsHolder Paths;
    public Action<FrontEndContent.ModuleInfo>? Play;

    readonly List<FrontEndContent.ModuleInfo> _modules = new();
    readonly List<(FrontEndContent.ModuleInfo Module, PanelContainer Card, TextureRect Thumb, Label Name, Label Caption)> _cards = new();
    FrontEndContent.ModuleInfo? _selected;
    VBoxContainer _list = null!;
    Control _hero = null!;
    TextureRect _heroImage = null!;
    Label _title = null!, _description = null!;
    HBoxContainer _chips = null!;
    Button _play = null!;
    LineEdit _filter = null!;
    readonly Dictionary<string, Texture2D?> _art = new(StringComparer.OrdinalIgnoreCase);

    public override void _Ready()
    {
        AddThemeConstantOverride("separation", Palette.Space4);
        SizeFlagsVertical = SizeFlags.ExpandFill;
        var head = Ui.HBox(Palette.Space4);
        var title = FrontEndUi.Header("New game", "Choose an adventure", "Modules from your modules folder and the official campaign");
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        _filter = new LineEdit { PlaceholderText = "Search modules", CustomMinimumSize = new Vector2(240, 36), SizeFlagsVertical = SizeFlags.ShrinkEnd, ClearButtonEnabled = true };
        _filter.RightIcon = Icons.Get("search", 18);
        _filter.TextChanged += _ => Filter();
        head.AddChild(_filter);
        AddChild(head);

        var split = Ui.HBox(Palette.Space5);
        split.SizeFlagsVertical = SizeFlags.ExpandFill;
        AddChild(split);

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(330, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 0.85f, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _list = Ui.VBox(Palette.Space2);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var listPad = Ui.Pad(_list, 0, 0, 10, 0);
        listPad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(listPad);
        split.AddChild(scroll);

        // Detail pane.
        var detail = Ui.VBox(Palette.Space3);
        detail.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        detail.SizeFlagsStretchRatio = 1.15f;
        split.AddChild(detail);
        var heroFrame = new PanelContainer { CustomMinimumSize = new Vector2(0, 250), ClipContents = true };
        heroFrame.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg0, Palette.Radius, Palette.LineStrong, 1, 0));
        _hero = FrontEndUi.CoverImage(null, 0.9f);
        heroFrame.AddChild(_hero);
        _heroImage = _hero.GetNode<TextureRect>("Image");
        var heroText = Ui.VBox(4);
        heroText.AnchorTop = 1; heroText.AnchorBottom = 1; heroText.AnchorRight = 1;
        heroText.OffsetLeft = 20; heroText.OffsetRight = -20; heroText.OffsetTop = -86; heroText.OffsetBottom = -16;
        heroText.Alignment = AlignmentMode.End;
        heroText.MouseFilter = MouseFilterEnum.Ignore;
        _hero.AddChild(heroText);
        _title = Ui.Text("", Ui.TextStyle.Heading, Palette.Text, 30);
        _title.ClipText = true;
        _title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        _title.AddThemeConstantOverride("shadow_outline_size", 6);
        heroText.AddChild(_title);
        _chips = Ui.HBox(6);
        heroText.AddChild(_chips);
        detail.AddChild(heroFrame);

        var descScroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _description = Ui.Text("", Ui.TextStyle.Reading, Palette.TextSecondary, 18);
        _description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _description.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var descPad = Ui.Pad(_description, 2, 0, 12, 0);
        descPad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        descScroll.AddChild(descPad);
        detail.AddChild(descScroll);

        var actions = Ui.HBox(Palette.Space2);
        actions.Alignment = AlignmentMode.End;
        detail.AddChild(actions);
        _play = Ui.Primary("Play", () => { if (_selected != null) Play?.Invoke(_selected); });
        _play.CustomMinimumSize = new Vector2(200, 46);
        _play.AddThemeFontSizeOverride("font_size", Palette.FontLarge);
        _play.FocusMode = FocusModeEnum.All;
        actions.AddChild(_play);

        foreach (var m in FrontEndContent.FindModules(Paths.Paths)) _modules.Add(m);
        BuildCards();
        if (_modules.Count == 0) _description.Text = "No modules found. Put modules (.mod files or module folders) in your Neverwinter Nights modules folder.";
        _ = DescribeAll();
    }

    void BuildCards()
    {
        string? group = null;
        var (normal, hover, selected) = FrontEndUi.CardStyles(8);
        foreach (var m in _modules)
        {
            if (m.Group != group)
            {
                group = m.Group;
                var gap = new Control { CustomMinimumSize = new Vector2(0, _list.GetChildCount() == 0 ? 0 : 8) };
                _list.AddChild(gap);
                _list.AddChild(Ui.Section(group));
            }
            var card = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand, FocusMode = FocusModeEnum.All };
            card.AddThemeStyleboxOverride("panel", normal);
            var row = Ui.HBox(Palette.Space3);
            row.MouseFilter = MouseFilterEnum.Ignore;
            card.AddChild(row);
            var thumbFrame = new PanelContainer { CustomMinimumSize = new Vector2(104, 60), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
            thumbFrame.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg0, 6, Palette.Line, 1, 0));
            var thumb = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
            // A glyph until the art arrives (the art covers it).
            thumbFrame.AddChild(new CenterContainer { MouseFilter = MouseFilterEnum.Ignore });
            thumbFrame.GetChild(0).AddChild(Icons.Rect(m.Official ? "book" : "map", 22, Palette.TextMuted with { A = 0.6f }));
            thumbFrame.AddChild(thumb);
            row.AddChild(thumbFrame);
            var text = Ui.VBox(2);
            text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            text.Alignment = AlignmentMode.Center;
            text.MouseFilter = MouseFilterEnum.Ignore;
            var name = Ui.Text(m.FileName, Ui.TextStyle.Strong, Palette.Text, Palette.FontBody + 1);
            name.ClipText = true;
            text.AddChild(name);
            var caption = Ui.Text(m.Official ? "Official campaign" : System.IO.Directory.Exists(m.Path) ? "Module folder" : "Module file", Ui.TextStyle.Caption, Palette.TextMuted);
            caption.ClipText = true;
            text.AddChild(caption);
            row.AddChild(text);
            var module = m;
            card.MouseEntered += () => { if (_selected != module) card.AddThemeStyleboxOverride("panel", hover); };
            card.MouseExited += () => { if (_selected != module) card.AddThemeStyleboxOverride("panel", normal); };
            card.GuiInput += e =>
            {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
                {
                    Select(module);
                    if (mb.DoubleClick) Play?.Invoke(module);
                }
                else if (e.IsActionPressed("ui_accept")) Play?.Invoke(module);
            };
            card.FocusEntered += () => Select(module);
            _list.AddChild(card);
            _cards.Add((m, card, thumb, name, caption));
        }
        if (_modules.Count > 0) Select(_modules[0]);
    }

    void Filter()
    {
        var q = _filter.Text.Trim();
        foreach (var (m, card, _, _, _) in _cards)
            card.Visible = q.Length == 0 || m.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || m.FileName.Contains(q, StringComparison.OrdinalIgnoreCase);
    }

    public void Select(FrontEndContent.ModuleInfo m)
    {
        _selected = m;
        var (normal, _, selected) = FrontEndUi.CardStyles(8);
        foreach (var c in _cards) c.Card.AddThemeStyleboxOverride("panel", c.Module == m ? selected : normal);
        _title.Text = m.Title;
        FrontEndUi.Clear(_chips);
        _chips.AddChild(FrontEndUi.Chip(m.Official ? "Official campaign" : "Custom module", m.Official ? Palette.Accent : Palette.Info));
        if (m.Read)
        {
            if (m.SizeBytes > 0) _chips.AddChild(FrontEndUi.Chip(FrontEndContent.FormatSize(m.SizeBytes)));
            if (m.AreaCount > 0) _chips.AddChild(FrontEndUi.Chip($"{m.AreaCount} areas"));
            if (m.Haks.Count > 0) _chips.AddChild(FrontEndUi.Chip(m.Haks.Count == 1 ? "1 hak" : $"{m.Haks.Count} haks", m.MissingHaks.Count > 0 ? Palette.Danger : null));
            var text = m.Description.Length > 0 ? m.Description : "No description.";
            if (m.MissingHaks.Count > 0) text = $"Missing hak files: {string.Join(", ", m.MissingHaks)}. Their content will be absent.\n\n" + text;
            _description.Text = text;
        }
        else _description.Text = "Reading module…";
        _heroImage.Texture = _art.GetValueOrDefault(m.Path);
        FrontEndUi.FadeIn(_heroImage, 0.25);
    }

    async Task DescribeAll()
    {
        var paths = Paths.Paths;
        var talk = await Task.Run(() => FrontEndContent.BaseTalk(paths));
        foreach (var m in _modules)
        {
            await Task.Run(() => FrontEndContent.Describe(m, paths, talk));
            if (!IsInstanceValid(this)) return;
            var card = _cards.FirstOrDefault(c => c.Module == m);
            if (card.Name != null) card.Name.Text = m.Title;
            if (_selected == m) Select(m);
        }
        // Art needs the base game's textures.
        var content = await FrontEndContent.BaseAsync;
        if (content == null || !IsInstanceValid(this)) return;
        foreach (var m in _modules)
        {
            if (!m.Readable) continue;
            string resref = FrontEndContent.LoadScreen(content.Resources, m.LoadScreen, m.Tileset, m.FileName.GetHashCode() & 0x7fffffff);
            var tex = FrontEndContent.LoadScreenShots(content, resref).FirstOrDefault();
            _art[m.Path] = tex;
            var card = _cards.FirstOrDefault(c => c.Module == m);
            if (card.Thumb != null)
            {
                card.Thumb.Texture = tex;
                FrontEndUi.FadeIn(card.Thumb, 0.3);
            }
            if (_selected == m) { _heroImage.Texture = tex; FrontEndUi.FadeIn(_heroImage, 0.35); }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            if (!IsInstanceValid(this)) return;
        }
    }

    public void FocusFirst() => _play.GrabFocus();
}

/// <summary>Passes the game paths to screens built in code (required members need a reference type).</summary>
public sealed class GamePathsHolder
{
    public required OpenWinter.Aurora.Resources.GamePaths Paths;
    public required OpenWinter.Content.LocalConfig Config;
    /// <summary>The saves folder single player writes to (the sandbox in development runs).</summary>
    public string SavesDir => System.IO.Path.Combine(Config.WriteRoot ?? Config.UserRoot, "saves");
}
