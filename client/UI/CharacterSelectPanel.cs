using OpenWinter.Aurora.Formats;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.FrontEnd;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// Character selection after connecting, full screen: the characters as cards on the left (portrait,
/// name, classes, level), the chosen one large on the right (portrait, race and alignment, abilities,
/// description), and Play / New Character. The list is the server's for a server-vault server, or the
/// player's own local vault (whose chosen BIC is uploaded) otherwise.
/// </summary>
public partial class CharacterSelectPanel : Control
{
    public ClientSession Session = null!;
    /// <summary>The player's local vault folder (&lt;user&gt;/localvault).</summary>
    public string LocalVault = "";
    /// <summary>More local vault folders listed after it (development: the sandbox new characters are saved to).</summary>
    public readonly List<string> ExtraVaults = new();
    /// <summary>"New character" was pressed (the server allows creation).</summary>
    public Action? NewCharacterRequested;
    /// <summary>Back to the main menu / the options window.</summary>
    public Action? ExitToMenu, OpenOptions;
    /// <summary>Don't show the list when it arrives (a saved game is being loaded instead).</summary>
    public bool Suppressed;

    /// <summary>What the panel knows about a character (more from a local BIC than from a server list).</summary>
    sealed class Hero
    {
        public required CharacterEntry Entry;
        public string Race = "", Gender = "", Alignment = "", Deity = "", Description = "";
        public int[]? Abilities;
        public int Experience;
    }

    readonly List<Hero> _heroes = new();
    readonly List<PanelContainer> _cards = new();
    int _selected = -1;
    VaultMode _mode;
    VBoxContainer _list = null!;
    Label _kicker = null!, _count = null!, _name = null!, _line = null!, _classes = null!, _level = null!, _empty = null!;
    RichTextLabel _description = null!;
    Control _noPortrait = null!;
    TextureRect _portrait = null!;
    HBoxContainer _stats = null!;
    Button _play = null!, _new = null!;
    Control _detail = null!;

    public static CharacterSelectPanel Create(ClientSession session, string localVault) =>
        new() { Session = session, LocalVault = localVault, Visible = false };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Theme = GameTheme.Theme;
        AddChild(new ColorRect { Color = new Color("07080b"), AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });
        AddChild(new Atmosphere { Smoke = 1, LeftShade = 0.5f, Vignette = 0.8f, Embers = 0.8f });

        // Left: header and the list.
        var left = new VBoxContainer { AnchorBottom = 1, OffsetLeft = 88, OffsetRight = 88 + 470, OffsetTop = 64, OffsetBottom = -104 };
        left.AddThemeConstantOverride("separation", Palette.Space2);
        AddChild(left);
        _kicker = Ui.Text("", Ui.TextStyle.Caption, Palette.Accent, 13);
        _kicker.AddThemeFontOverride("font", new FontVariation { BaseFont = Fonts.UiBold, SpacingGlyph = 3 });
        _kicker.ClipText = true;
        left.AddChild(_kicker);
        left.AddChild(Ui.Text("Choose your hero", Ui.TextStyle.Display, new Color("f4dca6"), 44));
        _count = Ui.Text("", Ui.TextStyle.Secondary, Palette.TextSecondary, Palette.FontSmall + 1);
        left.AddChild(_count);
        left.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        left.AddChild(new Divider { Color = Palette.Accent with { A = 0.6f }, Fade = Divider.FadeMode.End, CustomMinimumSize = new Vector2(0, 1) });
        left.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _list = Ui.VBox(Palette.Space2);
        _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var pad = Ui.Pad(_list, 0, 0, 12, 0);
        pad.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(pad);
        left.AddChild(scroll);
        _empty = Ui.Text("No characters yet. Create one to begin.", Ui.TextStyle.Secondary, Palette.TextMuted);
        _empty.Visible = false;
        left.AddChild(_empty);

        // Right: the selected hero.
        _detail = new HBoxContainer { AnchorLeft = 0.40f, AnchorRight = 1, AnchorBottom = 1, OffsetRight = -88, OffsetTop = 96, OffsetBottom = -120 };
        _detail.AddThemeConstantOverride("separation", Palette.Space6);
        AddChild(_detail);
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(300, 470), SizeFlagsVertical = SizeFlags.ShrinkBegin, ClipContents = true };
        var frameStyle = Styles.Box(Palette.Bg0, Palette.Radius, Palette.Accent with { A = 0.55f }, 1, 0, shadow: 30, shadowAlpha: 0.6f);
        frame.AddThemeStyleboxOverride("panel", frameStyle);
        _noPortrait = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        _noPortrait.AddChild(Icons.Rect("character", 96, Palette.TextMuted with { A = 0.35f }));
        frame.AddChild(_noPortrait);
        _portrait = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered };
        frame.AddChild(_portrait);
        _detail.AddChild(frame);
        var info = Ui.VBox(Palette.Space2);
        info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _detail.AddChild(info);
        _level = Ui.Text("", Ui.TextStyle.Caption, Palette.Accent, 13);
        _level.AddThemeFontOverride("font", new FontVariation { BaseFont = Fonts.UiBold, SpacingGlyph = 2 });
        info.AddChild(_level);
        _name = Ui.Text("", Ui.TextStyle.Display, Palette.Text, 46);
        _name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.AddChild(_name);
        _classes = Ui.Text("", Ui.TextStyle.Title, Palette.AccentBright, 20);
        _classes.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.AddChild(_classes);
        _line = Ui.Text("", Ui.TextStyle.Secondary);
        _line.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.AddChild(_line);
        info.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        _stats = Ui.HBox(Palette.Space2);
        info.AddChild(_stats);
        info.AddChild(new Control { CustomMinimumSize = new Vector2(0, 10) });
        _description = new RichTextLabel { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill, ScrollActive = true, BbcodeEnabled = false, FocusMode = FocusModeEnum.None };
        _description.AddThemeFontOverride("normal_font", Fonts.Reading);
        _description.AddThemeFontSizeOverride("normal_font_size", 18);
        _description.AddThemeColorOverride("default_color", Palette.TextSecondary);
        _description.AddThemeConstantOverride("line_separation", 4);
        info.AddChild(_description);

        // Bottom bar.
        var bar = Ui.HBox(Palette.Space3);
        bar.AnchorTop = bar.AnchorBottom = 1;
        bar.AnchorRight = 1;
        bar.OffsetLeft = 88; bar.OffsetRight = -88; bar.OffsetTop = -84; bar.OffsetBottom = -36;
        AddChild(bar);
        var back = Ui.Ghost("Main Menu", () => ExitToMenu?.Invoke());
        back.Icon = Icons.Get("exit", 32);
        bar.AddChild(back);
        var options = Ui.Ghost("Options", () => OpenOptions?.Invoke());
        options.Icon = Icons.Get("options", 32);
        bar.AddChild(options);
        bar.AddChild(Ui.Spacer());
        bar.AddChild(FrontEndUi.KeyHints(("↑ ↓", "Choose"), ("Enter", "Play")));
        bar.AddChild(new Control { CustomMinimumSize = new Vector2(16, 0) });
        _new = Ui.Button("New Character", () => { Visible = false; NewCharacterRequested?.Invoke(); });
        _new.CustomMinimumSize = new Vector2(180, 48);
        _new.Visible = false;
        bar.AddChild(_new);
        _play = Ui.Primary("Play", Play);
        _play.CustomMinimumSize = new Vector2(200, 48);
        _play.AddThemeFontSizeOverride("font_size", Palette.FontLarge);
        _play.Disabled = true;
        bar.AddChild(_play);

        Session.CharacterListReceived += Show;
    }

    /// <summary>Shows the list again (back from character creation).</summary>
    public void Reopen()
    {
        Visible = true;
        Gameplay.PlayerInput.Blocked = true;
    }

    void Show(CharacterListMessage m)
    {
        _mode = m.Mode;
        _new.Visible = m.AllowNew;
        _heroes.Clear();
        if (m.Mode == VaultMode.Server) _heroes.AddRange(m.Characters.Select(c => new Hero { Entry = c }));
        else _heroes.AddRange(ReadLocalVault());
        _kicker.Text = NwnText.Strip(Session.ModuleName).ToUpperInvariant();
        _count.Text = $"{(m.Mode == VaultMode.Server ? "Server vault" : "Local vault")}  ·  {_heroes.Count} {(_heroes.Count == 1 ? "character" : "characters")}";
        BuildCards();
        _empty.Visible = _heroes.Count == 0;
        _detail.Visible = _heroes.Count > 0;
        _play.Disabled = _heroes.Count == 0;
        if (_heroes.Count > 0) Select(0);
        if (Suppressed) { Suppressed = false; return; }
        Visible = true;
        Gameplay.PlayerInput.Blocked = true;
        FrontEndUi.Reveal(_detail, 16, 0, 0.3);
        Callable.From(() => { if (_cards.Count > 0) _cards[Math.Max(0, _selected)].GrabFocus(); else if (_new.Visible) _new.GrabFocus(); }).CallDeferred();
    }

    void BuildCards()
    {
        FrontEndUi.Clear(_list);
        _cards.Clear();
        var (normal, hover, _) = FrontEndUi.CardStyles(10);
        for (int i = 0; i < _heroes.Count; i++)
        {
            int index = i;
            var h = _heroes[i];
            var card = new PanelContainer { FocusMode = FocusModeEnum.All, MouseDefaultCursorShape = CursorShape.PointingHand };
            card.AddThemeStyleboxOverride("panel", normal);
            var row = Ui.HBox(Palette.Space3);
            row.MouseFilter = MouseFilterEnum.Ignore;
            card.AddChild(row);
            var thumbFrame = new PanelContainer { CustomMinimumSize = new Vector2(46, 64), ClipContents = true, MouseFilter = MouseFilterEnum.Ignore };
            thumbFrame.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg0, 6, Palette.LineStrong, 1, 0));
            var thumb = Portrait(h.Entry.Portrait, "m", "l");
            if (thumb == null)
            {
                var glyph = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
                glyph.AddChild(Icons.Rect("character", 22, Palette.TextMuted with { A = 0.5f }));
                thumbFrame.AddChild(glyph);
            }
            else thumbFrame.AddChild(new TextureRect { Texture = thumb, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore });
            row.AddChild(thumbFrame);
            var text = Ui.VBox(2);
            text.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            text.Alignment = BoxContainer.AlignmentMode.Center;
            text.MouseFilter = MouseFilterEnum.Ignore;
            var name = Ui.Text(h.Entry.Name.Length > 0 ? h.Entry.Name : "Unnamed", Ui.TextStyle.Strong, Palette.Text, Palette.FontLarge);
            name.ClipText = true;
            text.AddChild(name);
            var sub = Ui.Text(string.Join("  ·  ", new[] { h.Race, h.Entry.Classes }.Where(x => x.Length > 0)), Ui.TextStyle.Small);
            sub.ClipText = true;
            text.AddChild(sub);
            row.AddChild(text);
            var level = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(40, 40), MouseFilter = MouseFilterEnum.Ignore };
            level.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.35f), 20, Palette.Accent with { A = 0.5f }, 1, 4, 4));
            var lv = Ui.Text(h.Entry.Level.ToString(), Ui.TextStyle.Number, Palette.AccentBright);
            lv.HorizontalAlignment = HorizontalAlignment.Center;
            lv.VerticalAlignment = VerticalAlignment.Center;
            level.AddChild(lv);
            level.TooltipText = $"Level {h.Entry.Level}";
            row.AddChild(level);
            card.MouseEntered += () => { if (_selected != index) card.AddThemeStyleboxOverride("panel", hover); };
            card.MouseExited += () => { if (_selected != index) card.AddThemeStyleboxOverride("panel", normal); };
            card.FocusEntered += () => Select(index);
            card.GuiInput += e =>
            {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
                {
                    card.GrabFocus();
                    if (mb.DoubleClick) Play();
                }
                else if (e.IsActionPressed("ui_accept")) { Play(); card.AcceptEvent(); }
            };
            _list.AddChild(card);
            _cards.Add(card);
        }
    }

    /// <summary>Characters in the local vault, read the way the server reads them, plus what the preview shows.</summary>
    List<Hero> ReadLocalVault()
    {
        var list = new List<Hero>();
        if (Session.Content is not { } content) return list;
        var rm = content.Resources;
        var classes = rm.TwoDA("classes");
        var races = rm.TwoDA("racialtypes");
        var genders = rm.TwoDA("gender");
        var files = new[] { LocalVault }.Concat(ExtraVaults).Where(System.IO.Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(dir => System.IO.Directory.EnumerateFiles(dir, "*.bic").OrderBy(p => p, StringComparer.OrdinalIgnoreCase));
        foreach (var path in files)
        {
            try
            {
                var bic = Gff.Read(System.IO.File.ReadAllBytes(path));
                var talk = rm.Talk;
                var cls = bic.GetList("ClassList").Select(c => (Class: (int)c.GetInt("Class"), Level: (int)c.GetInt("ClassLevel"))).ToList();
                int race = (int)bic.GetInt("Race");
                int gender = (int)bic.GetInt("Gender");
                list.Add(new Hero
                {
                    Entry = new CharacterEntry
                    {
                        File = path,
                        Name = NwnText.Strip((bic.GetLocString("FirstName").Resolve(talk) + " " + bic.GetLocString("LastName").Resolve(talk)).Trim()),
                        Classes = string.Join(" / ", cls.Select(c => $"{rm.TlkString(unchecked((uint)classes.GetInt(c.Class, "Name", 0)))} {c.Level}")),
                        Level = cls.Sum(c => c.Level),
                        Portrait = ShownPortrait(bic.GetString("Portrait"), gender),
                    },
                    Race = rm.TlkString(unchecked((uint)races.GetInt(race, "Name", 0))),
                    Gender = genders.GetInt(gender, "NAME", 0) is > 0 and var g ? rm.TlkString((uint)g) : gender == 1 ? "Female" : "Male",
                    Alignment = Alignment((int)bic.GetInt("LawfulChaotic"), (int)bic.GetInt("GoodEvil")),
                    Deity = NwnText.Strip(bic.GetString("Deity")),
                    Description = NwnText.Strip(bic.GetLocString("Description").Resolve(talk)).Trim(),
                    Abilities = new[] { "Str", "Dex", "Con", "Int", "Wis", "Cha" }.Select(a => (int)bic.GetInt(a)).ToArray(),
                    Experience = (int)bic.GetInt("Experience"),
                });
            }
            catch (Exception e) when (e is System.IO.IOException or System.IO.InvalidDataException or System.IO.EndOfStreamException) { }
        }
        return list;
    }

    static string Alignment(int lawChaos, int goodEvil)
    {
        string a = lawChaos >= 70 ? "Lawful" : lawChaos <= 30 ? "Chaotic" : "Neutral";
        string b = goodEvil >= 70 ? "Good" : goodEvil <= 30 ? "Evil" : "Neutral";
        return a == "Neutral" && b == "Neutral" ? "True Neutral" : $"{a} {b}";
    }

    /// <summary>The character's portrait, or the original's shadowy default for its gender when it isn't installed.</summary>
    string ShownPortrait(string resref, int gender)
    {
        if (resref.Length > 0 && Session.Content is { } content && new[] { "m", "l", "h" }.Any(size => content.Textures.Get(resref + size) != null)) return resref;
        return gender == 1 ? "po_hu_f_99_" : "po_hu_m_99_";
    }

    Texture2D? Portrait(string resref, params string[] sizes)
    {
        if (Session.Content is not { } content || resref.Length == 0) return null;
        foreach (var size in sizes)
        {
            if (content.Textures.Get(resref + size) is not { } tex) continue;
            // Portraits sit in the top 100/128 of their texture.
            var s = tex.Texture.GetSize();
            float h = s.Y * 100f / 128f;
            var atlas = new AtlasTexture { Atlas = tex.Texture, Region = new Rect2(0, tex.FlipV ? s.Y - h : 0, s.X, h) };
            if (!tex.FlipV) return atlas;
            var image = atlas.GetImage();
            if (image == null) return atlas;
            if (image.IsCompressed()) image.Decompress();
            image.FlipY();
            return ImageTexture.CreateFromImage(image);
        }
        return null;
    }

    void Select(int i)
    {
        if (i < 0 || i >= _heroes.Count) return;
        bool changed = _selected != i;
        _selected = i;
        var (normal, _, selected) = FrontEndUi.CardStyles(10);
        for (int c = 0; c < _cards.Count; c++) _cards[c].AddThemeStyleboxOverride("panel", c == i ? selected : normal);
        var h = _heroes[i];
        _name.Text = h.Entry.Name.Length > 0 ? h.Entry.Name : "Unnamed";
        _classes.Text = h.Entry.Classes;
        _level.Text = $"LEVEL {h.Entry.Level}" + (h.Experience > 0 ? $"  ·  {h.Experience:N0} XP" : "");
        _line.Text = string.Join("  ·  ", new[] { $"{h.Gender} {h.Race}".Trim(), h.Alignment, h.Deity }.Where(x => x.Length > 0));
        _description.Text = h.Description;
        FrontEndUi.Clear(_stats);
        if (h.Abilities is { } ab)
        {
            string[] names = { "STR", "DEX", "CON", "INT", "WIS", "CHA" };
            for (int a = 0; a < 6; a++) _stats.AddChild(StatTile(names[a], ab[a]));
        }
        _portrait.Texture = Portrait(h.Entry.Portrait, "h", "l", "m");
        _noPortrait.Visible = _portrait.Texture == null;
        _play.Disabled = false;
        if (changed) FrontEndUi.FadeIn(_portrait, 0.25);
    }

    static Control StatTile(string name, int value)
    {
        var p = new PanelContainer { CustomMinimumSize = new Vector2(64, 0) };
        p.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.35f), Palette.RadiusSmall, Palette.LineStrong, 1, 8, 6));
        var col = Ui.VBox(0);
        var n = Ui.Text(name, Ui.TextStyle.Caption, Palette.TextMuted);
        n.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(n);
        var v = Ui.Text(value.ToString(), Ui.TextStyle.Number, Palette.Text, 22);
        v.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(v);
        int mod = (int)Math.Floor((value - 10) / 2.0);
        var m = Ui.Text(mod >= 0 ? $"+{mod}" : mod.ToString(), Ui.TextStyle.Caption, mod > 0 ? Palette.Success : mod < 0 ? Palette.Danger : Palette.TextMuted);
        m.HorizontalAlignment = HorizontalAlignment.Center;
        col.AddChild(m);
        p.AddChild(col);
        return p;
    }

    /// <summary>Plays the highlighted character (also used by scripted captures).</summary>
    public void Play()
    {
        if (_selected < 0 || _selected >= _heroes.Count) return;
        var e = _heroes[_selected].Entry;
        Session.Send(_mode == VaultMode.Server
            ? new CharacterChoiceMessage { File = e.File }.Write()
            : new CharacterChoiceMessage { Bic = System.IO.File.ReadAllBytes(e.File) }.Write());
        Visible = false;
        Gameplay.PlayerInput.Blocked = false;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible || e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode is Key.Enter or Key.KpEnter) { Play(); GetViewport().SetInputAsHandled(); }
    }
}
