using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>The original's chat look: its TrueType font (fnt_default) and channel colours.</summary>
public static class ClassicChatStyle
{
    public static ClassicText Text(ClassicAssets assets) => ClassicText.For(assets, FontName, Size);
    public static Font Font(ClassicAssets assets) => Text(assets).Font;

    /// <summary>The chat font (a TrueType font, which GuiFont loads too).</summary>
    public static string FontName => ClassicText.DefaultTextFont;

    /// <summary>Text size in pixels.</summary>
    public static int Size => Settings.Current.Interface.HighResolutionFont ? 14 : 11;
    /// <summary>The window's height per visible line at 1x (65 px for the original's four lines).</summary>
    public const float Line = 16.25f;
    /// <summary>Distance between text lines (and one scroll step).</summary>
    public static float LinePitch => Settings.Current.Interface.HighResolutionFont ? 16 : 15;

    // The original's channel colours (its defaults, as the reference captures show them).
    public static readonly Color Talk = Rgb(240, 240, 240), Shout = Rgb(255, 239, 80), Whisper = Rgb(128, 128, 128), Tell = Rgb(32, 255, 32),
        Server = Rgb(176, 176, 176), Message = Rgb(255, 255, 0), Party = Rgb(255, 102, 1), Dm = Rgb(16, 223, 255), Prompt = Rgb(166, 166, 166);

    /// <summary>The speaker's name before a spoken or conversation line (measured: chatsplit, convoend).</summary>
    public static readonly Color Name = Rgb(143, 127, 255);
    /// <summary>Combat and roll lines: orange, the acting creature's name first in cyan (the player's own character)
    /// or pink (anyone else; "Someone" stays orange), each damage amount in its damagetypegroups.2da colour (chatsplit2).
    /// The kill line too: "Aluvian Darkstar" cyan or "Bandit" pink, then " killed Goblin" orange (killmsg).</summary>
    public static readonly Color Combat = Rgb(255, 102, 1), CombatSelf = Rgb(153, 255, 255), CombatOther = Rgb(204, 153, 204);
    /// <summary>Saving throws are light blue and skill checks blue after the name (chatsplit native shot).</summary>
    public static readonly Color Save = Rgb(102, 204, 255), SkillCheck = Rgb(1, 102, 255);

    static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

    public static Color Of(ChatChannel c) => c switch
    {
        ChatChannel.Shout => Shout, ChatChannel.Whisper => Whisper, ChatChannel.Tell => Tell, ChatChannel.Server => Message,
        ChatChannel.Party => Party, ChatChannel.Dm => Dm, _ => Talk,
    };

    /// <summary>The channel tag the original puts before the text ("[Shout]"); none for talk and server lines.</summary>
    public static string Tag(ChatChannel c) => c switch
    {
        ChatChannel.Shout => "[Shout] ", ChatChannel.Whisper => "[Whisper] ", ChatChannel.Tell => "[Tell] ", ChatChannel.Party => "[Party] ",
        ChatChannel.Dm => "[DM] ", _ => "",
    };
}

/// <summary>
/// The original's chat window, bottom left above the talk line: a thin black drag bar (drag it to show more or fewer
/// lines), the lines in fnt_default with the original's channel colours over a faint backing, and a scroll bar at its
/// right. Spoken and conversation lines carry the speaker's tiny portrait in the margin (chatsplit, convoend).
/// Right-clicking a bar opens the original's chat menu (its game strings and ir_* icons, in the places its captures
/// show): leave out normal chat, shouts, party chat, Dungeon Master messages, server messages or combat information,
/// split the window in two or combine it again; dialogue, dice rolls and the player list sit in a submenu. Split, the
/// two windows stand side by side, each half the width with its own bar and height: speech on the left, server
/// messages, combat information and dice rolls on the right (chatmenuheld). Shows everything the modern chat log does.
/// </summary>
public partial class ClassicChat : Control
{
    public ClassicContext Ctx = null!;

    /// <summary>The kinds of line the chat menu can leave out (Talk is its "normal chat": talk, whispers and tells).</summary>
    public enum Kind { Talk, Shouts, Server, Party, Dice, Dialog, Combat, Dm }

    sealed record Line(Kind Kind, string Bbcode, Texture2D? Portrait);

    readonly List<Line> _all = new();
    readonly Dictionary<string, Texture2D?> _portraits = new();
    Pane _primary = null!, _secondary = null!;
    Vector2 _view;
    float _bottom;
    const float DragHeight = 12, Width = 720, BarWidth = 16;
    static readonly Color FeedbackDefault = new("e8d080");

    static Preferences.InterfaceSettings Prefs => Settings.Current.Interface;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        _secondary = new Pane { Chat = this, Secondary = true, Lines = Prefs.ChatSecondaryLines };
        _primary = new Pane { Chat = this, Lines = Prefs.ChatLines };
        AddChild(_secondary);
        AddChild(_primary);
        // What was said before the switch to this interface.
        foreach (var (m, text, color) in Ctx.Chat.Recent)
            if (m != null) OnChat(m); else OnFeedback(text, color);
        Ctx.Session.ChatReceived += OnChat;
        Ctx.Chat.FeedbackAdded += OnFeedback;
        TreeExiting += () => { Ctx.Session.ChatReceived -= OnChat; Ctx.Chat.FeedbackAdded -= OnFeedback; };
    }

    /// <summary>Bottom left, the windows' bottoms on the talk line's top.</summary>
    public void Place(Vector2 view, float bottom)
    {
        _view = view;
        _bottom = bottom;
        Position = Vector2.Zero;
        Size = view;
        Layout();
    }

    /// <summary>
    /// One window across the chat's width, or split: two side by side, each half the width (chatmenuheld: at 1280 x 720
    /// the left one spans x 0-359 and the right one 360-719, each with its own bar, grip and scroll bar).
    /// </summary>
    void Layout()
    {
        float w = Math.Min(Width, _view.X - 100);
        _secondary.Visible = Prefs.ChatSplit;
        if (!Prefs.ChatSplit) { _primary.Place(_bottom, 0, w); return; }
        float half = MathF.Floor(w / 2);
        _primary.Place(_bottom, 0, half);
        _secondary.Place(_bottom, half, w - half);
    }

    /// <summary>A window's lines after a drag, down to none (just its bar, as the original allows) and clear of the top of the screen.</summary>
    void SetLines(Pane pane, int lines)
    {
        int room = Math.Max(0, (int)((_bottom - 120) / ClassicChatStyle.Line));
        lines = Math.Clamp(lines, 0, room);
        if (lines == pane.Lines) return;
        pane.Lines = lines;
        Layout();
    }

    /// <summary>A window's drag bar on screen (captures): the left one, or the right one of a split.</summary>
    public Rect2 BarRect(bool secondary)
    {
        var pane = secondary ? _secondary : _primary;
        return new Rect2(pane.GlobalPosition, new Vector2(pane.Size.X, DragHeight));
    }

    /// <summary>Takes the chat settings again (captures that change them).</summary>
    public void Refresh()
    {
        _primary.Lines = Prefs.ChatLines;
        _secondary.Lines = Prefs.ChatSecondaryLines;
        Refill();
    }

    void SaveLines() => Settings.Update(s => { s.Interface.ChatLines = _primary.Lines; s.Interface.ChatSecondaryLines = _secondary.Lines; });

    // ------------------------------------------------------------------ lines

    static bool Shown(Kind k) => !Prefs.ChatExcluded.Contains(k.ToString());
    /// <summary>Split, speech stays in the left window; server messages, combat information and dice rolls go to the right (chatmenuheld).</summary>
    Pane PaneFor(Kind k) => Prefs.ChatSplit && k is Kind.Server or Kind.Combat or Kind.Dice ? _secondary : _primary;

    void Add(Kind kind, string bbcode, Texture2D? portrait)
    {
        _all.Add(new Line(kind, bbcode, portrait));
        if (_all.Count > 600) _all.RemoveRange(0, 100);
        if (Shown(kind)) PaneFor(kind).Add(bbcode, portrait);
    }

    /// <summary>After a menu change: every kept line again, into the window it now belongs to.</summary>
    void Refill()
    {
        _primary.Clear();
        _secondary.Clear();
        foreach (var l in _all)
            if (Shown(l.Kind)) PaneFor(l.Kind).Add(l.Bbcode, l.Portrait);
        Layout();
    }

    void OnChat(ChatMessage m)
    {
        var text = NwnText.Strip(m.Text);
        var kind = m.Channel switch
        {
            ChatChannel.Shout => Kind.Shouts,
            ChatChannel.Party => Kind.Party,
            ChatChannel.Dialog => Kind.Dialog,
            ChatChannel.Dm => Kind.Dm,
            ChatChannel.Server => m.SpeakerName.Length == 0 ? Classify(text) : Kind.Server,
            _ => Kind.Talk,
        };
        var color = ClassicChatStyle.Of(m.Channel);
        // "Bandit: line", but "Bandit : [Shout] line" (chatsplit).
        string who = m.SpeakerName.Length > 0
            ? $"[color={Hex(ClassicChatStyle.Name)}]{Escape(NwnText.Strip(m.SpeakerName))}{(m.Channel == ChatChannel.Shout ? " : " : ": ")}[/color]"
            : "";
        if (who.Length == 0 && kind is Kind.Combat or Kind.Dice) { Add(kind, CombatBbcode(text), null); return; }
        Add(kind, $"{who}{NwnText.ToBbcode(ClassicChatStyle.Tag(m.Channel) + m.Text, Hex(color))}",
            m.Channel is ChatChannel.Server or ChatChannel.Dm ? null : Portrait(m.Speaker));
    }

    static readonly System.Text.RegularExpressions.Regex Actor = new(@"^(.+?)(?= attacks | damages | : | killed )", System.Text.RegularExpressions.RegexOptions.Compiled);
    static readonly System.Text.RegularExpressions.Regex DamagePart = new(@"(\d+) ([A-Za-z]+)", System.Text.RegularExpressions.RegexOptions.Compiled);
    Dictionary<string, Color>? _damageColors;

    /// <summary>A combat or roll line in the original's colours (see <see cref="ClassicChatStyle.Combat"/>).</summary>
    string CombatBbcode(string text)
    {
        string Span(Color c, string t) => $"[color={Hex(c)}]{Escape(t)}[/color]";
        var sb = new System.Text.StringBuilder();
        int at = 0;
        if (Actor.Match(text) is { Success: true } actor && actor.Value != "Someone")
        {
            bool self = Ctx.Session.Controlled is { } me && me.DisplayName == actor.Value; // the creature's name (Node.Name is the node's)
            sb.Append(Span(self ? ClassicChatStyle.CombatSelf : ClassicChatStyle.CombatOther, actor.Value));
            at = actor.Length;
        }
        var rest = text[at..];
        int open = rest.LastIndexOf('(');
        if (rest.Contains(" damages ") && open >= 0 && rest.EndsWith(')'))
        {
            var colors = _damageColors ??= DamageColors();
            sb.Append(Span(ClassicChatStyle.Combat, rest[..(open + 1)]));
            var inner = rest[(open + 1)..^1];
            int last = 0;
            foreach (System.Text.RegularExpressions.Match part in DamagePart.Matches(inner))
            {
                sb.Append(Span(ClassicChatStyle.Combat, inner[last..part.Index]));
                sb.Append(Span(colors.GetValueOrDefault(part.Groups[2].Value, ClassicChatStyle.Combat), part.Value));
                last = part.Index + part.Length;
            }
            sb.Append(Span(ClassicChatStyle.Combat, inner[last..] + ")"));
        }
        else sb.Append(Span(rest.Contains(" Save") || rest.Contains("Saving Throw") ? ClassicChatStyle.Save
            : rest.Contains("vs. DC") ? ClassicChatStyle.SkillCheck : ClassicChatStyle.Combat, rest));
        return sb.ToString();
    }

    /// <summary>Damage type name (its feedback string) to colour, from damagetypegroups.2da.</summary>
    Dictionary<string, Color> DamageColors()
    {
        var map = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        if (Ctx.Session.Content?.Resources is not { } rm) return map;
        var t = rm.TwoDA("damagetypegroups");
        for (int row = 0; row < t.RowCount; row++)
        {
            // The feedback string is "<CUSTOM0> Magical": the amount, then the name.
            var name = rm.TlkString(unchecked((uint)t.GetInt(row, "FeedbackStrref", -1))).Replace("<CUSTOM0>", "").Trim();
            if (name.Length == 0) name = t.Get(row, "Label");
            map[name] = new Color(t.GetInt(row, "ColorR", 255) / 255f, t.GetInt(row, "ColorG", 102) / 255f, t.GetInt(row, "ColorB", 1) / 255f);
        }
        return map;
    }

    void OnFeedback(string text, string color)
    {
        // The modern default feedback colour is the original's server grey; other colours are kept.
        var c = Color.FromString(color, FeedbackDefault).IsEqualApprox(FeedbackDefault) ? ClassicChatStyle.Server : Color.FromString(color, ClassicChatStyle.Server);
        var kind = Classify(NwnText.Strip(text));
        Add(kind, kind is Kind.Combat or Kind.Dice ? CombatBbcode(NwnText.Strip(text)) : NwnText.ToBbcode(text, Hex(c)), null);
    }

    static readonly System.Text.RegularExpressions.Regex Roll = new(@"\(\d+ [+-] \d+ = \d+", System.Text.RegularExpressions.RegexOptions.Compiled);
    static readonly System.Text.RegularExpressions.Regex Attack = new(@"( attacks | damages |\*hit\*|\*miss\*|\*critical hit\*|\*parried\*)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// A line with no speaker: attacks and damage are combat information, other rolls (initiative, saves, skill
    /// checks) dice rolls, the rest of the engine's combat feedback combat information, anything else a server message.
    /// </summary>
    public static Kind Classify(string text) =>
        Attack.IsMatch(text) ? Kind.Combat : Roll.IsMatch(text) ? Kind.Dice : ChatLog.IsCombatLine(text) ? Kind.Combat : Kind.Server;

    /// <summary>The speaker's tiny portrait (16 x 25, the picture part of its "t" texture), if it has one.</summary>
    Texture2D? Portrait(uint speaker)
    {
        if (Ctx.Session.Objects.GetValueOrDefault(speaker) is not { Portrait.Length: > 0 } view || Ctx.Session.Content is not { } content) return null;
        if (_portraits.TryGetValue(view.Portrait, out var cached)) return cached;
        Texture2D? result = null;
        foreach (var size in new[] { "t", "s", "m" })
        {
            if (content.Textures.Get(view.Portrait + size) is not { } tex) continue;
            var s = tex.Texture.GetSize();
            float h = s.Y * 25f / 32f;
            result = new AtlasTexture { Atlas = tex.Texture, Region = new Rect2(0, tex.FlipV ? s.Y - h : 0, s.X, h) };
            break;
        }
        return _portraits[view.Portrait] = result;
    }

    static string Hex(Color c) => "#" + c.ToHtml(false);
    static string Escape(string s) => s.Replace("[", "[lb]");

    // ------------------------------------------------------------------ the chat menu

    string Tlk(uint strref) => Ctx.Session.Content?.Resources.TlkString(strref) ?? "";

    /// <summary>Opens the chat menu: the ring centres on ir_chat, as the original's does.</summary>
    void OpenMenu(Vector2 at) =>
        Ctx.Interaction.OpenMenu?.Invoke(RadialMenu.Icon(new RadialEntry { Label = Tlk(2392) }, "ir_chat"), at, Menu);

    /// <summary>
    /// The original's chat menu (chatradial, chatmenuheld): split / combine at the top, then clockwise party chat
    /// (ir_partychat), server messages (ir_servermsg), Dungeon Master messages (ir_dmchat), normal chat (ir_chat,
    /// "Exclude Normal Chat" measured), combat information (ir_cmbtinfo) and shouts (ir_shouts) on the left; the
    /// upper left is empty on the original. Its labels are the game's own strings (1394-1408, 67572-67575). The original
    /// shows no dialogue, dice roll or player list options on this ring; OpenWinter keeps them in a submenu in the
    /// empty place (ir_chatmenu).
    /// </summary>
    List<RadialEntry> Menu()
    {
        RadialEntry Toggle(Kind kind, uint include, uint exclude, string icon)
        {
            bool shown = Shown(kind);
            return RadialMenu.Icon(new RadialEntry { Label = Tlk(shown ? exclude : include), Act = () => SetShown(kind, !shown) }, icon);
        }
        bool split = Prefs.ChatSplit;
        return RadialMenu.FixedRing(
            (0, RadialMenu.Icon(new RadialEntry { Label = Tlk(split ? 1406u : 1405u), Act = ToggleSplit }, split ? "ir_chatcombine" : "ir_chatsplit")),
            (1, Toggle(Kind.Party, 1399, 1400, "ir_partychat")),
            (2, Toggle(Kind.Server, 1397, 1398, "ir_servermsg")),
            (3, Toggle(Kind.Dm, 67572, 67573, "ir_dmchat")),
            (4, Toggle(Kind.Talk, 67574, 67575, "ir_chat")),
            (5, Toggle(Kind.Combat, 1407, 1408, "ir_cmbtinfo")),
            (6, Toggle(Kind.Shouts, 1394, 1395, "ir_shouts")),
            (7, RadialMenu.Icon(new RadialEntry
            {
                Label = Tlk(201),
                Open = () => RadialMenu.FixedRing(
                    (0, Toggle(Kind.Dialog, 1403, 1404, "ir_dialog")),
                    (2, Toggle(Kind.Dice, 1401, 1402, "ir_dice")),
                    (4, RadialMenu.Icon(new RadialEntry { Label = Tlk(1396), Act = () => Ctx.Input.OpenPlayerList?.Invoke() }, "ir_lstplay"))),
            }, "ir_chatmenu")));
    }

    void SetShown(Kind kind, bool shown)
    {
        Settings.Update(s =>
        {
            s.Interface.ChatExcluded.Remove(kind.ToString());
            if (!shown) s.Interface.ChatExcluded.Add(kind.ToString());
        });
        Refill();
    }

    void ToggleSplit()
    {
        Settings.Update(s => s.Interface.ChatSplit = !s.Interface.ChatSplit);
        if (Prefs.ChatSplit) SetLines(_secondary, _secondary.Lines);
        Refill();
    }

    // ------------------------------------------------------------------ one window

    /// <summary>One chat window: its bar, lines, portraits and scroll bar.</summary>
    sealed partial class Pane : Control
    {
        public ClassicChat Chat = null!;
        public bool Secondary;
        public int Lines = 4;
        /// <summary>Where the text starts: past the portrait margin (30 px); the right-hand window of a split starts its
        /// text 25 px in (chatmenuheld).</summary>
        float TextLeft => Secondary ? 25 : 30;
        RichTextLabel _log = null!;
        ClassicScrollBar _bar = null!;
        DragBar _drag = null!;
        bool _highResolution;
        readonly List<Texture2D?> _portraits = new();
        int _count;
        /// <summary>A line with a portrait stands 35 px from the next (27 px of portrait and frame, then the gap), its text
        /// level with the portrait's top (chatlines).</summary>
        const int PortraitPitch = 35;
        int _spacer;
        static readonly Color Frame = new(99 / 255f, 101 / 255f, 99 / 255f);
        readonly ImageTexture Spacer = ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgba8));

        /// <summary>The window's height for the lines shown: the drag bar plus the lines.</summary>
        public float Height => DragHeight + Lines * ClassicChatStyle.Line;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            _highResolution = Settings.Current.Interface.HighResolutionFont;
            var font = ClassicChatStyle.Font(Chat.Ctx.Assets);
            _log = new RichTextLabel
            {
                BbcodeEnabled = true, ScrollActive = true, ScrollFollowing = true, MouseFilter = MouseFilterEnum.Pass,
                SelectionEnabled = false, FocusMode = FocusModeEnum.None,
            };
            _log.AddThemeFontOverride("normal_font", font);
            _log.AddThemeFontOverride("bold_font", font);
            _log.AddThemeFontSizeOverride("normal_font_size", ClassicChatStyle.Size);
            _log.AddThemeFontSizeOverride("bold_font_size", ClassicChatStyle.Size);
            int separation = (int)MathF.Round(ClassicChatStyle.LinePitch - font.GetHeight(ClassicChatStyle.Size));
            _log.AddThemeConstantOverride("line_separation", separation);
            // The spacer hangs below the text's baseline: ascent + spacer + separation = the portrait pitch.
            _spacer = Math.Max(1, PortraitPitch - (int)MathF.Round(font.GetAscent(ClassicChatStyle.Size)) - separation);
            _log.AddThemeColorOverride("default_color", ClassicChatStyle.Server);
            var textStyle = ClassicChatStyle.Text(Chat.Ctx.Assets);
            _log.Material = textStyle.Material;
            _log.AddThemeConstantOverride("outline_size", 2);
            _log.AddThemeColorOverride("font_outline_color", new Color(textStyle.OutlineBrightness, textStyle.OutlineBrightness, textStyle.OutlineBrightness, textStyle.OutlineAlpha));
            _log.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
            _log.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            AddChild(_log);
            // The label's own scroll bar stays hidden: the original's drives it.
            var own = _log.GetVScrollBar();
            own.Modulate = Colors.Transparent;
            own.MouseFilter = MouseFilterEnum.Ignore;
            own.Changed += SyncBar;
            own.ValueChanged += _ => SyncBar();
            _bar = new ClassicScrollBar { Assets = Chat.Ctx.Assets, Step = ClassicChatStyle.LinePitch, Scrolled = o => own.Value = o };
            AddChild(_bar);
            _drag = new DragBar { Pane = this };
            AddChild(_drag);
        }

        void SyncBar()
        {
            var own = _log.GetVScrollBar();
            _bar.Total = (float)own.MaxValue;
            _bar.Page = (float)own.Page;
            _bar.Offset = (float)own.Value;
            _bar.QueueRedraw();
            QueueRedraw();
        }

        public void Place(float bottom, float x, float w)
        {
            Size = new Vector2(w, Height);
            Position = new Vector2(x, bottom - Height);
            _drag.Position = Vector2.Zero;
            _drag.Size = new Vector2(w, DragHeight);
            float inset = _highResolution ? -3 : 3;
            _log.Position = new Vector2(TextLeft, DragHeight + inset);
            _log.Size = new Vector2(w - TextLeft - BarWidth - 6, Math.Max(0, Height - DragHeight - inset));
            // No lines: just the bar.
            _log.Visible = _bar.Visible = Lines > 0;
            _bar.Position = new Vector2(w - BarWidth - 1, DragHeight);
            _bar.Size = new Vector2(BarWidth, Height - DragHeight);
            SyncBar();
        }

        public void Add(string bbcode, Texture2D? portrait)
        {
            if (_count > 300) { _log.RemoveParagraph(0); _portraits.RemoveAt(0); _count--; }
            if (_count > 0) _log.Newline();
            // A portrait line is as tall as the portrait: an invisible inline image holds the room.
            if (portrait != null) _log.AddImage(Spacer, 1, _spacer, inlineAlign: InlineAlignment.TopTo | InlineAlignment.ToBaseline);
            _log.AppendText(bbcode);
            _portraits.Add(portrait);
            _count++;
            QueueRedraw();
        }

        public void Clear()
        {
            _log.Clear();
            _portraits.Clear();
            _count = 0;
            QueueRedraw();
        }

        /// <summary>The faint black backing the original puts behind the whole chat window, so text reads over the world.</summary>
        static readonly Color Backing = new(0, 0, 0, 0.3f);

        public override void _Draw()
        {
            if (Size.Y <= DragHeight) return;
            DrawRect(new Rect2(0, DragHeight, Size.X, Size.Y - DragHeight), Backing);
            // Portraits in the margin, beside their lines (a 1-pixel grey frame round the 16 x 25 picture).
            float scroll = (float)_log.GetVScrollBar().Value;
            for (int i = 0; i < _portraits.Count; i++)
            {
                if (_portraits[i] is not { } tex) continue;
                float y = _log.Position.Y + _log.GetParagraphOffset(i) - scroll;
                if (y < DragHeight || y + 27 > Size.Y) continue;
                DrawRect(new Rect2(6.5f, y + 0.5f, 17, 26), Frame, false, 1);
                DrawTextureRect(tex, new Rect2(7, y + 1, 16, 25), false);
            }
        }

        /// <summary>The black bar along the top: drag it up or down to show more or fewer lines, right-click it for the chat menu.</summary>
        sealed partial class DragBar : Control
        {
            public Pane Pane = null!;
            bool _dragging;
            float _grabY;
            int _grabLines;
            /// <summary>The bar's grey rim and the line under it (chatmenuheld: black inside, rounded at both ends).</summary>
            static readonly Color Rim = new(55 / 255f, 57 / 255f, 59 / 255f), Bottom = new(46 / 255f, 47 / 255f, 48 / 255f);
            static readonly Color Brass = new(239 / 255f, 207 / 255f, 148 / 255f), BrassDark = new(147 / 255f, 118 / 255f, 55 / 255f);

            public override void _Ready()
            {
                MouseFilter = MouseFilterEnum.Stop;
                MouseDefaultCursorShape = CursorShape.Vsize;
            }

            public override void _Draw()
            {
                // Black, its top corners cut 2 px a row for four rows at both ends, inside a 1-pixel grey rim.
                float w = Size.X, h = Size.Y;
                DrawColoredPolygon([new(8, 1), new(w - 8, 1), new(w - 1, 4.5f), new(w - 1, h - 1), new(1, h - 1), new(1, 4.5f)], Colors.Black);
                DrawPolyline([new(0.5f, h - 1), new(0.5f, 4.5f), new(7.5f, 0.5f), new(w - 7.5f, 0.5f), new(w - 0.5f, 4.5f), new(w - 0.5f, h - 1)], Rim);
                DrawLine(new Vector2(0, h - 0.5f), new Vector2(w, h - 0.5f), Bottom);
                // The grip in the middle: a small brass arrowhead.
                var c = new Vector2(MathF.Round(Size.X / 2), 2);
                DrawColoredPolygon([c + new Vector2(0, 0), c + new Vector2(5, 7), c + new Vector2(-5, 7)], Brass);
                DrawLine(c + new Vector2(-5, 7), c + new Vector2(5, 7), BrassDark, 2);
            }

            public override void _GuiInput(InputEvent e)
            {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
                {
                    if (!mb.Pressed && _dragging) Pane.Chat.SaveLines();
                    _dragging = mb.Pressed;
                    _grabY = mb.GlobalPosition.Y;
                    _grabLines = Pane.Lines;
                    AcceptEvent();
                }
                else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Right } right)
                {
                    // Pressed opens the menu (hold, move onto an option and let go, or click); the release that
                    // follows comes back here (the bar keeps the mouse until it sees it) and goes no further.
                    if (right.Pressed) Pane.Chat.OpenMenu(right.GlobalPosition);
                    AcceptEvent();
                }
                else if (e is InputEventMouseMotion mm && _dragging)
                {
                    Pane.Chat.SetLines(Pane, _grabLines + (int)MathF.Round((_grabY - mm.GlobalPosition.Y) / ClassicChatStyle.Line));
                    AcceptEvent();
                }
            }
        }
    }
}

/// <summary>
/// The full-width line at the very bottom (pnl_chat_prompt) with the chat mode button ("Talk:") at its left: Enter
/// starts typing, Enter sends, Esc leaves; Up / Down step through what was sent; the button (or /s, /w, /p, /dm)
/// picks the channel. Sends through the modern chat entry's logic.
/// </summary>
public partial class ClassicTalkLine : Control
{
    public ClassicContext Ctx = null!;
    GuiModelView _frame = null!;
    GuiButton _mode = null!;
    GuiLabel _modeText = null!;
    LineEdit _line = null!;
    ModeList _list = null!;
    ChatChannel _channel = ChatChannel.Talk;
    readonly List<string> _history = new();
    int _historyAt;

    /// <summary>The line's top edge on screen (the chat window and compass sit on it).</summary>
    public float Top { get; private set; }

    static readonly (ChatChannel Channel, string Name)[] Modes =
        [(ChatChannel.Talk, "Talk"), (ChatChannel.Shout, "Shout"), (ChatChannel.Whisper, "Whisper"), (ChatChannel.Party, "Party"), (ChatChannel.Dm, "DM")];

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var assets = Ctx.Assets;
        _frame = GuiModelView.Create(assets, "pnl_chat_prompt");
        AddChild(_frame);
        _mode = GuiButton.Create(assets, "ctl_btn_chatmode");
        _mode.SetMeta(ClassicTooltip.Meta, "Chat Mode");
        _mode.Pressed += () => _list.Visible = !_list.Visible;
        AddChild(_mode);
        _modeText = GuiLabel.Create(assets, "Talk:", ClassicChatStyle.FontName, ClassicChatStyle.Prompt, 0, 0.5f);
        _mode.AddChild(_modeText);
        var font = ClassicChatStyle.Font(assets);
        _line = new LineEdit { MaxLength = 1000, FocusMode = FocusModeEnum.Click, ContextMenuEnabled = false, CaretBlink = true };
        _line.AddThemeFontOverride("font", font);
        _line.AddThemeFontSizeOverride("font_size", ClassicChatStyle.Size);
        _line.AddThemeColorOverride("font_color", ClassicChatStyle.Talk);
        _line.AddThemeColorOverride("caret_color", ClassicChatStyle.Talk);
        _line.AddThemeColorOverride("selection_color", new Color(0.4f, 0.35f, 0.2f));
        foreach (var style in new[] { "normal", "focus", "read_only" }) _line.AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        _line.TextSubmitted += Submit;
        _line.FocusEntered += () => DM.ChatInput.Typing = true;
        _line.FocusExited += () => DM.ChatInput.Typing = false;
        _line.GuiInput += LineInput;
        AddChild(_line);
        _list = new ModeList { Talk = this, Visible = false };
        AddChild(_list);
    }

    /// <summary>Full width along the bottom, just above the quickbar.</summary>
    public void Place(Vector2 view)
    {
        Position = Vector2.Zero;
        Size = view;
        // The prompt panel's origin is 36 px above the bottom (its frame runs 3 px below and 20 above it).
        var origin = new Vector2(0, view.Y - 36);
        _frame.PlaceOrigin(origin);
        _mode.PlaceOrigin(origin);
        var text = _mode.Model?.Node("text") ?? new Vector2(6, -8);
        _modeText.Position = new Vector2(text.X, 0);
        _modeText.Size = new Vector2(_mode.Size.X - text.X, _mode.Size.Y);
        Top = origin.Y + (_frame.Model?.Bounds.Position.Y ?? -20);
        float left = _mode.Position.X + _mode.Size.X + 4;
        _line.Position = new Vector2(left, _mode.Position.Y);
        _line.Size = new Vector2(view.X - left - 4, _mode.Size.Y);
        _list.Place(_mode.Position);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!IsVisibleInTree() || _line.HasFocus()) return;
        if (Preferences.InputBindings.Pressed(e, Preferences.ActionCatalog.Chat))
        {
            _line.GrabFocus();
            GetViewport().SetInputAsHandled();
        }
    }

    void LineInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true } k) return;
        if (k.PhysicalKeycode == Key.Escape)
        {
            _line.Text = "";
            _line.ReleaseFocus();
            _list.Visible = false;
            _line.AcceptEvent();
        }
        else if (k.PhysicalKeycode is Key.Up or Key.Down && _history.Count > 0)
        {
            _historyAt = Math.Clamp(_historyAt + (k.PhysicalKeycode == Key.Up ? -1 : 1), 0, _history.Count);
            _line.Text = _historyAt < _history.Count ? _history[_historyAt] : "";
            _line.CaretColumn = _line.Text.Length;
            _line.AcceptEvent();
        }
    }

    void Submit(string text)
    {
        _line.Text = "";
        _line.ReleaseFocus();
        text = text.Trim();
        if (text.Length == 0) return;
        _history.Remove(text);
        _history.Add(text);
        if (_history.Count > 50) _history.RemoveAt(0);
        _historyAt = _history.Count;
        Ctx.ChatInput.SetChannel(_channel);
        Ctx.ChatInput.Send(text);
    }

    void SetChannel(ChatChannel channel)
    {
        _channel = channel;
        _modeText.Text = Modes.First(m => m.Channel == channel).Name + ":";
        _list.Visible = false;
    }

    /// <summary>The chat mode list that opens above the button (ctl_lst_chatmode).</summary>
    sealed partial class ModeList : Control
    {
        public ClassicTalkLine Talk = null!;
        GuiModelView _frame = null!;
        Rect2 _text;
        int _hover = -1;
        GuiFont _font = null!;

        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            _frame = GuiModelView.Create(Talk.Ctx.Assets, "ctl_lst_chatmode");
            AddChild(_frame);
            Size = _frame.Size;
            _text = _frame.Node("text0") is { } a && _frame.Node("text1") is { } b ? new Rect2(a, Vector2.Zero).Expand(b) : new Rect2(new Vector2(6, 6), Size - new Vector2(12, 12));
            _font = Talk.Ctx.Assets.Font(ClassicChatStyle.FontName);
            // The rows draw over the frame (children draw over their parent).
            _rows = new Control { MouseFilter = MouseFilterEnum.Ignore, Size = Size };
            _rows.Draw += DrawRows;
            AddChild(_rows);
            MouseExited += () => { _hover = -1; _rows.QueueRedraw(); };
            VisibilityChanged += () => _rows.QueueRedraw();
        }

        Control _rows = null!;
        float Pitch => Math.Min(_font.LineHeight, _text.Size.Y / Modes.Length);

        /// <summary>Opens upward from the button's top-left.</summary>
        public void Place(Vector2 buttonTopLeft) => Position = buttonTopLeft - new Vector2(0, Size.Y);

        int RowAt(Vector2 p)
        {
            if (!_text.HasPoint(p)) return -1;
            int i = (int)((p.Y - _text.Position.Y) / Pitch);
            return i >= 0 && i < Modes.Length ? i : -1;
        }

        void DrawRows()
        {
            for (int i = 0; i < Modes.Length; i++)
            {
                var at = _text.Position + new Vector2(0, i * Pitch);
                var color = i == _hover ? GuiDraw.Selected : Modes[i].Channel == Talk._channel ? Colors.White : ClassicChatStyle.Prompt;
                _font.Draw(_rows, at, Modes[i].Name, color);
            }
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseMotion mm)
            {
                int h = RowAt(mm.Position);
                if (h != _hover) { _hover = h; _rows.QueueRedraw(); }
            }
            else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } mb)
            {
                int i = RowAt(mb.Position);
                if (i >= 0)
                {
                    ClassicSounds.Play(Talk.Ctx.Assets, ClassicSounds.Button);
                    Talk.SetChannel(Modes[i].Channel);
                }
                AcceptEvent();
            }
        }
    }
}
