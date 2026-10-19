using System.Text.RegularExpressions;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The conversation view: while talking, the bottom of the screen darkens into a soft scrim with the
/// speaker's portrait and name, their line in the reading face (revealed quickly; click or Space to
/// show it all), and the numbered replies (click or press 1-9; Esc ends the conversation).
/// Skill-check tags such as [Persuade] show as coloured tags; [Continue] and [End Dialog] as quiet
/// actions. The camera keeps following the player during dialogue.
/// </summary>
public partial class DialogPanel : Control
{
    public Action<int>? Reply;
    /// <summary>For the speaker's portrait (optional).</summary>
    public ClientSession? Session;
    /// <summary>The game camera; conversations leave its target and orbit unchanged.</summary>
    public OrbitCamera? Camera;

    /// <summary>Whether a script has put the player in cutscene mode.</summary>
    public bool Cutscene
    {
        get => _cutscene;
        set { _cutscene = value; }
    }
    bool _cutscene;

    Control _scrim = null!;
    HBoxContainer _content = null!;
    TextureRect _portrait = null!;
    Label _monogram = null!, _speaker = null!;
    RichTextLabel _text = null!;
    VBoxContainer _replies = null!;
    readonly List<int> _indexes = new();
    Tween? _reveal;
    bool _revealing;
    uint _speakerId;

    const float ContentWidth = 960, BottomGap = 120;
    const int TextSize = 23, ReplySize = 20;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        _scrim = new Control { MouseFilter = MouseFilterEnum.Ignore };
        _scrim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _scrim.Draw += DrawScrim;
        _scrim.Resized += _scrim.QueueRedraw;
        AddChild(_scrim);

        // Portrait | (name, line, replies), centred above the action bar.
        _content = Ui.HBox(28);
        _content.MouseFilter = MouseFilterEnum.Ignore;
        _content.AnchorLeft = _content.AnchorRight = 0.5f;
        _content.AnchorTop = _content.AnchorBottom = 1;
        _content.GrowHorizontal = GrowDirection.Both;
        _content.GrowVertical = GrowDirection.Begin;
        _content.Resized += () => _scrim.QueueRedraw();
        AddChild(_content);

        _content.AddChild(BuildPortrait());

        var column = Ui.VBox(0);
        column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        column.MouseFilter = MouseFilterEnum.Ignore;
        _content.AddChild(column);
        _speaker = Ui.Text("", Ui.TextStyle.Heading, Palette.AccentBright, 24);
        _speaker.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
        _speaker.AddThemeConstantOverride("shadow_offset_y", 2);
        column.AddChild(_speaker);
        var rule = new Divider { Fade = Divider.FadeMode.End, Color = Palette.Accent with { A = 0.75f }, CustomMinimumSize = new Vector2(0, 1) };
        column.AddChild(Ui.Pad(rule, 0, 6, 160, 14));
        _text = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Pass, SelectionEnabled = false,
        };
        ReadingFonts(_text, TextSize);
        _text.AddThemeColorOverride("default_color", Palette.Text);
        _text.AddThemeConstantOverride("line_separation", 4);
        _text.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.7f));
        _text.AddThemeConstantOverride("shadow_offset_y", 1);
        _text.AddThemeConstantOverride("shadow_outline_size", 4);
        _text.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } && _revealing) FinishReveal(); };
        column.AddChild(_text);
        _replies = Ui.VBox(1);
        _replies.MouseFilter = MouseFilterEnum.Ignore;
        column.AddChild(Ui.Pad(_replies, -12, 18, 0, 0));

        Resized += Layout;
        Layout();
        Visible = false;
    }

    Control BuildPortrait()
    {
        var frame = new PanelContainer { CustomMinimumSize = new Vector2(124, 194), SizeFlagsVertical = SizeFlags.ShrinkBegin, MouseFilter = MouseFilterEnum.Ignore };
        frame.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg2, Palette.Radius, Palette.AccentDim, 1, 4, 4, shadow: 18, shadowAlpha: 0.65f));
        var stack = new Control { MouseFilter = MouseFilterEnum.Ignore, ClipContents = true };
        frame.AddChild(stack);
        var back = new Control { MouseFilter = MouseFilterEnum.Ignore };
        back.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        back.Draw += () =>
        {
            var s = back.Size;
            back.DrawPolygon([new(0, 0), new(s.X, 0), new(s.X, s.Y), new(0, s.Y)], [Palette.Bg3, Palette.Bg3, Palette.Bg1, Palette.Bg1]);
        };
        stack.AddChild(back);
        _portrait = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = MouseFilterEnum.Ignore };
        _portrait.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        stack.AddChild(_portrait);
        _monogram = Ui.Text("", Ui.TextStyle.Display, Palette.AccentDim, 56);
        _monogram.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _monogram.HorizontalAlignment = HorizontalAlignment.Center;
        _monogram.VerticalAlignment = VerticalAlignment.Center;
        stack.AddChild(_monogram);
        // Fade the foot of the picture and edge it with a hairline, like a framed miniature.
        var sheen = new Control { MouseFilter = MouseFilterEnum.Ignore };
        sheen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        sheen.Draw += () =>
        {
            var s = sheen.Size;
            var clear = new Color(0, 0, 0, 0);
            var dark = new Color(0, 0, 0, 0.5f);
            sheen.DrawPolygon([new(0, s.Y * 0.66f), new(s.X, s.Y * 0.66f), new(s.X, s.Y), new(0, s.Y)], [clear, clear, dark, dark]);
            var line = Styles.Box(Colors.Transparent, Palette.RadiusSmall, new Color(1, 1, 1, 0.12f), 1, 0, 0);
            sheen.DrawStyleBox(line, new Rect2(Vector2.Zero, s));
        };
        sheen.Resized += sheen.QueueRedraw;
        stack.AddChild(sheen);
        return frame;
    }

    static void ReadingFonts(RichTextLabel l, int size)
    {
        l.AddThemeFontOverride("normal_font", Fonts.Reading);
        l.AddThemeFontOverride("italics_font", Fonts.ReadingItalic);
        l.AddThemeFontOverride("bold_font", Fonts.ReadingBold);
        foreach (var f in new[] { "normal_font_size", "italics_font_size", "bold_font_size" }) l.AddThemeFontSizeOverride(f, size);
    }

    void Layout()
    {
        float width = Math.Min(ContentWidth, Size.X - 64);
        _content.CustomMinimumSize = new Vector2(width, 0);
        _content.OffsetLeft = -width / 2;
        _content.OffsetRight = width / 2;
        _content.OffsetTop = -BottomGap;
        _content.OffsetBottom = -BottomGap;
        _scrim.QueueRedraw();
    }

    /// <summary>Darkens the bottom of the screen from a little above the conversation, and the top edge slightly.</summary>
    void DrawScrim()
    {
        var s = _scrim.Size;
        float top = Math.Clamp(_content.Position.Y - 120, 0, s.Y);
        var ink = new Color(0.015f, 0.016f, 0.024f);
        var clear = ink with { A = 0 };
        var mid = ink with { A = 0.66f };
        var deep = ink with { A = 0.9f };
        float midY = top + (s.Y - top) * 0.34f;
        _scrim.DrawPolygon([new(0, top), new(s.X, top), new(s.X, midY), new(0, midY)], [clear, clear, mid, mid]);
        _scrim.DrawPolygon([new(0, midY), new(s.X, midY), new(s.X, s.Y), new(0, s.Y)], [mid, mid, deep, deep]);
        // Cinematic: a gentle shade along the top edge.
        var shade = ink with { A = 0.45f };
        _scrim.DrawPolygon([new(0, 0), new(s.X, 0), new(s.X, 96), new(0, 96)], [shade, shade, clear, clear]);
    }

    public void ShowStep(DialogMessage m)
    {
        if (m.Ended) { EndConversation(); return; }
        bool opening = !Visible;
        Visible = true;
        _speaker.Text = m.SpeakerName.Length > 0 ? m.SpeakerName : "—";
        SetPortrait(m);
        _text.Text = Markup(m.Text);
        BuildReplies(m);

        // Reveal the line quickly; the replies fade in once it is shown (at once on a click or Space).
        _reveal?.Kill();
        int chars = Math.Max(1, _text.GetTotalCharacterCount());
        double duration = Math.Clamp(chars / 120.0, 0.15, 1.5);
        _text.VisibleRatio = 0;
        _replies.Modulate = _replies.Modulate with { A = 0 };
        _revealing = true;
        _reveal = CreateTween();
        _reveal.TweenProperty(_text, "visible_ratio", 1f, duration);
        _reveal.TweenCallback(Callable.From(FinishReveal));

        if (opening)
        {
            Modulate = Modulate with { A = 0 };
            var tw = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            tw.TweenProperty(this, "modulate:a", 1f, 0.2);
        }
        _speakerId = m.Speaker;
    }

    void EndConversation()
    {
        if (!Visible) return;
        _reveal?.Kill();
        _revealing = false;
        Visible = false;
        _speakerId = 0;
    }

    void FinishReveal()
    {
        if (!_revealing) return;
        _revealing = false;
        _reveal?.Kill();
        _text.VisibleRatio = 1;
        var tw = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(_replies, "modulate:a", 1f, 0.16);
    }

    void SetPortrait(DialogMessage m)
    {
        Texture2D? tex = null;
        if (Session?.Objects.GetValueOrDefault(m.Speaker) is { Portrait.Length: > 0 } speaker)
            tex = PortraitTexture(Session, speaker.Portrait);
        _portrait.Texture = tex;
        _monogram.Visible = tex == null;
        var name = NwnText.Strip(m.SpeakerName).Trim();
        _monogram.Text = name.Length > 0 ? name[..1].ToUpperInvariant() : "?";
    }

    /// <summary>The largest portrait size available, cropped to the picture (the top 100 of 128 rows).</summary>
    static Texture2D? PortraitTexture(ClientSession session, string resref)
    {
        if (session.Content is not { } content) return null;
        foreach (var size in new[] { "h", "l", "m" })
        {
            if (content.Textures.Get(resref + size) is not { } tex) continue;
            var s = tex.Texture.GetSize();
            float h = s.Y * 100f / 128f;
            return new AtlasTexture { Atlas = tex.Texture, Region = new Rect2(0, tex.FlipV ? s.Y - h : 0, s.X, h) };
        }
        return null;
    }

    void BuildReplies(DialogMessage m)
    {
        foreach (var c in _replies.GetChildren()) { _replies.RemoveChild(c); c.QueueFree(); }
        _indexes.Clear();
        int n = 1;
        foreach (var (index, text) in m.Replies)
        {
            int choice = index;
            _replies.AddChild(new ReplyRow(n, text) { Chosen = () => Reply?.Invoke(choice) });
            _indexes.Add(choice);
            n++;
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Visible || e is not InputEventKey { Pressed: true, Echo: false } k || DM.ChatInput.Typing) return;
        var key = k.PhysicalKeycode;
        if (key is >= Key.Key1 and <= Key.Key9 or >= Key.Kp1 and <= Key.Kp9)
        {
            int i = key >= Key.Kp1 ? (int)(key - Key.Kp1) : (int)(key - Key.Key1);
            if (i < _indexes.Count) { Choose(i); GetViewport().SetInputAsHandled(); }
        }
        else if (key is Key.Space or Key.Enter or Key.KpEnter)
        {
            if (_revealing) FinishReveal();
            else if (_indexes.Count == 1) Choose(0);
            GetViewport().SetInputAsHandled();
        }
        else if (key == Key.Escape) { Reply?.Invoke(-1); GetViewport().SetInputAsHandled(); }
    }

    /// <summary>The middle of reply <paramref name="i"/> on screen, if shown (scripted captures).</summary>
    public Vector2? ReplyPosition(int i) => _replies.GetChildOrNull<Control>(i) is { } c ? c.GetGlobalRect().GetCenter() : null;

    void Choose(int i)
    {
        if (_replies.GetChildOrNull<ReplyRow>(i) is { } row) row.Flash();
        Reply?.Invoke(_indexes[i]);
    }

    // ---- text

    static readonly Regex LeadingTag = new(@"^\s*(?:<[cC][^>]{3}>)?(?:\[\[(?:action|check|highlight)\]\])?\s*\[(?<tag>[A-Za-z][A-Za-z '\-/]{1,28})\]\s*(?:\[\[/\]\])?(?:</[cC]>)?\s*", RegexOptions.Compiled);
    static readonly Regex Spans = new(@"\[\[(action|check|highlight)\]\](.*?)(?:\[\[/\]\]|$)", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>The server marks action/check/highlight spans with [[action]] ... [[/]]; the rest is escaped text.</summary>
    internal static string Markup(string text)
    {
        var sb = new System.Text.StringBuilder();
        int at = 0;
        text = text.Trim();
        foreach (Match m in Spans.Matches(text))
        {
            sb.Append(Escape(text[at..m.Index]));
            var inner = Escape(m.Groups[2].Value);
            sb.Append(m.Groups[1].Value switch
            {
                "action" => $"[i][color={Palette.Hex(Palette.AccentBright)}]{inner}[/color][/i]",
                "check" => $"[color={Palette.Hex(Palette.Danger)}]{inner}[/color]",
                _ => $"[color={Palette.Hex(Palette.Info)}]{inner}[/color]",
            });
            at = m.Index + m.Length;
        }
        sb.Append(Escape(text[at..].Replace("[[/]]", "")));
        return sb.ToString();
    }

    /// <summary>Text to BBCode: '[' escaped and NWN colour tokens (&lt;cRGB&gt;) as colours.</summary>
    static string Escape(string s) => NwnText.ToBbcode(s);

    static string StripMarkup(string text) => text.Replace("[[action]]", "").Replace("[[check]]", "").Replace("[[highlight]]", "").Replace("[[/]]", "");

    internal enum ReplyKind { Normal, Continue, End }

    /// <summary>Sorts a reply into spoken text (with an optional leading [Tag]), Continue or End.</summary>
    internal static (ReplyKind Kind, string? Tag, string Text) ParseReply(string raw)
    {
        var lower = NwnText.Strip(StripMarkup(raw)).Trim().ToLowerInvariant();
        if (lower is "[continue]" or "continue" or "[continue...]" or "[continue.]" or "...") return (ReplyKind.Continue, null, "Continue");
        if (lower is "[end dialog]" or "[end dialogue]" or "[end conversation]" or "[leave]" or "[end]" or "[done]") return (ReplyKind.End, null, "End conversation");
        var m = LeadingTag.Match(raw);
        if (m.Success && m.Length < raw.Length) return (ReplyKind.Normal, m.Groups["tag"].Value.Trim(), raw[m.Length..]);
        return (ReplyKind.Normal, null, raw);
    }

    /// <summary>Colour for a reply tag: social skills brass, abilities and magic blue, violence red, the rest grey.</summary>
    static Color TagColor(string tag)
    {
        var t = tag.ToLowerInvariant();
        bool Has(params string[] words) => words.Any(t.Contains);
        if (Has("persuade", "bluff", "lie", "intimidate", "appraise", "lore", "taunt", "perform", "diplomacy", "sense motive", "seduce", "flirt", "bribe", "gold"))
            return Palette.Accent;
        if (Has("strength", "dexterity", "constitution", "intelligence", "wisdom", "charisma", "spell", "magic", "arcane", "divine")) return Palette.Magic;
        if (Has("attack", "threat", "kill", "fight", "hostile", "draw")) return Palette.Danger;
        return Palette.TextSecondary;
    }

    /// <summary>One reply: number, optional tag, text; a brass bar and warm glow on hover.</summary>
    partial class ReplyRow : PanelContainer
    {
        public Action? Chosen;
        readonly int _number;
        readonly string _raw;
        bool _hover;
        Label _num = null!;
        RichTextLabel _label = null!;
        ReplyKind _kind;
        float _glow;
        Tween? _glowTween;

        public ReplyRow(int number, string raw) { _number = number; _raw = raw; }

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            MouseDefaultCursorShape = CursorShape.PointingHand;
            var (kind, tag, text) = ParseReply(_raw);
            _kind = kind;
            AddThemeStyleboxOverride("panel", Styles.Box(Colors.Transparent, Palette.RadiusSmall, null, 0, 12, kind == ReplyKind.Normal ? 5 : 7));
            var row = Ui.HBox(Palette.Space3);
            row.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(row);

            _num = Ui.Text(_number.ToString(), Ui.TextStyle.Strong, Palette.AccentDim, 14);
            _num.CustomMinimumSize = new Vector2(16, 0);
            _num.HorizontalAlignment = HorizontalAlignment.Right;
            _num.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            row.AddChild(Ui.Pad(_num, 0, kind == ReplyKind.Normal ? 4 : 0, 0, 0));

            if (tag != null)
            {
                var color = TagColor(tag);
                var pill = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkBegin };
                pill.AddThemeStyleboxOverride("panel", Styles.Box(color with { A = 0.13f }, 4, color with { A = 0.5f }, 1, 7, 2));
                var tl = Ui.Text(tag.ToUpperInvariant(), Ui.TextStyle.Caption, color.Lightened(0.2f), 11);
                tl.AddThemeFontOverride("font", Fonts.UiBold);
                pill.AddChild(tl);
                row.AddChild(Ui.Pad(pill, 0, 5, 0, 0));
            }

            _label = new RichTextLabel
            {
                BbcodeEnabled = true, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _label.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.6f));
            _label.AddThemeConstantOverride("shadow_offset_y", 1);
            row.AddChild(_label);
            if (kind == ReplyKind.Normal)
            {
                ReadingFonts(_label, ReplySize);
                _label.Text = Markup(text);
            }
            else
            {
                // Continue / End: quiet spaced capitals, distinct from spoken replies.
                _label.AddThemeFontOverride("normal_font", Fonts.UiBold);
                _label.AddThemeFontSizeOverride("normal_font_size", 12);
                _label.Text = kind == ReplyKind.Continue ? "C O N T I N U E    ›" : "E N D   C O N V E R S A T I O N";
            }
            Recolor();
            MouseEntered += () => { _hover = true; Recolor(); };
            MouseExited += () => { _hover = false; Recolor(); };
        }

        void Recolor()
        {
            var normal = _kind switch
            {
                ReplyKind.Continue => Palette.Accent,
                ReplyKind.End => Palette.TextSecondary,
                _ => new Color("d6cfbf"),
            };
            _label.AddThemeColorOverride("default_color", _hover ? (_kind == ReplyKind.Normal ? new Color("fff6e2") : Palette.AccentBright) : normal);
            _num.AddThemeColorOverride("font_color", _hover ? Palette.AccentBright : Palette.AccentDim.Lightened(0.15f));
            _glowTween?.Kill();
            _glowTween = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
            _glowTween.TweenMethod(Callable.From<float>(g => { _glow = g; QueueRedraw(); }), _glow, _hover ? 1f : 0f, 0.14);
        }

        public void Flash()
        {
            _glowTween?.Kill();
            _glow = 1.5f;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (_glow <= 0.001f) return;
            float g = Math.Min(1, _glow);
            var s = Size;
            var warm = new Color(0.83f, 0.65f, 0.34f, 0.26f * g);
            var none = warm with { A = 0 };
            DrawPolygon([new(0, 0), new(s.X * 0.8f, 0), new(s.X * 0.8f, s.Y), new(0, s.Y)], [warm, none, none, warm]);
            DrawRect(new Rect2(0, 3, 2, s.Y - 6), Palette.Accent with { A = g });
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                Flash();
                Chosen?.Invoke();
                AcceptEvent();
            }
        }
    }

}
