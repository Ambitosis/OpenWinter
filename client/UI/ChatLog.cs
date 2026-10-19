using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The chat and message log (bottom left): tabs for everything, conversation, combat rolls and
/// system messages. The panel fades back when idle so the world shows through, and comes forward
/// on new messages or when the mouse is over it.
/// </summary>
public partial class ChatLog : PanelContainer
{
    public enum Category { Chat, Combat, System }

    public ClientSession? Session;
    RichTextLabel _log = null!;
    readonly List<(Category Cat, string Bbcode)> _lines = new();
    Category? _filter;
    readonly List<Button> _tabs = new();
    double _lastActivity = -100;
    bool _hover;

    public override void _Ready()
    {
        AnchorTop = 1; AnchorBottom = 1;
        OffsetLeft = 16; OffsetRight = 424; OffsetTop = -300; OffsetBottom = -56; // the chat entry line sits below
        Ui.Glass(this, Styles.Hud(8, Palette.Radius), 0.8f);
        MouseFilter = MouseFilterEnum.Pass;

        var col = Ui.VBox(4);
        col.MouseFilter = MouseFilterEnum.Pass;
        AddChild(col);
        var tabs = Ui.HBox(2);
        tabs.MouseFilter = MouseFilterEnum.Pass;
        col.AddChild(tabs);
        foreach (var (name, cat) in new (string, Category?)[] { ("All", null), ("Chat", Category.Chat), ("Combat", Category.Combat), ("System", Category.System) })
        {
            var b = Ui.Ghost(name);
            b.ToggleMode = true;
            b.ButtonPressed = cat == null;
            b.AddThemeFontSizeOverride("font_size", 12);
            b.AddThemeColorOverride("font_pressed_color", Palette.AccentBright);
            var c = cat;
            b.Pressed += () => SetFilter(c);
            tabs.AddChild(b);
            _tabs.Add(b);
        }
        col.AddChild(new Divider { Color = Palette.LineStrong, Fade = Divider.FadeMode.End, CustomMinimumSize = new Vector2(0, 1) });

        _log = new RichTextLabel { BbcodeEnabled = true, ScrollFollowing = true, SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Pass };
        _log.AddThemeFontSizeOverride("normal_font_size", 14);
        _log.AddThemeFontSizeOverride("bold_font_size", 14);
        _log.AddThemeConstantOverride("line_separation", 3);
        _log.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        _log.AddThemeConstantOverride("outline_size", 3);
        col.AddChild(_log);
    }

    void SetFilter(Category? cat)
    {
        _filter = cat;
        for (int i = 0; i < _tabs.Count; i++) _tabs[i].SetPressedNoSignal(i == (cat == null ? 0 : (int)cat + 1));
        _log.Clear();
        foreach (var (c, line) in _lines)
            if (_filter == null || c == _filter) _log.AppendText(line + "\n");
    }

    void Append(Category cat, string bbcode)
    {
        _lines.Add((cat, bbcode));
        if (_lines.Count > 400) { _lines.RemoveRange(0, 100); SetFilter(_filter); }
        else if (_filter == null || _filter == cat) _log.AppendText(bbcode + "\n");
        _lastActivity = Time.GetTicksMsec() / 1000.0;
    }

    /// <summary>The latest messages and feedback lines as they arrived (an interface switched to in play shows them too).</summary>
    public readonly List<(ChatMessage? Message, string Text, string Color)> Recent = new();

    void Remember(ChatMessage? m, string text, string color)
    {
        Recent.Add((m, text, color));
        if (Recent.Count > 200) Recent.RemoveRange(0, 50);
    }

    public void Add(ChatMessage m)
    {
        Remember(m, "", "");
        var color = m.Channel switch
        {
            ChatChannel.Shout => Palette.ChatShout,
            ChatChannel.Whisper => Palette.ChatWhisper,
            ChatChannel.Server => Palette.ChatServer,
            ChatChannel.Party => Palette.ChatParty,
            ChatChannel.Dm => Palette.ChatDm,
            ChatChannel.Tell => Palette.ChatTell,
            _ => Palette.ChatTalk,
        };
        string tag = m.Channel switch
        {
            ChatChannel.Dm => Tag("DM", Palette.ChatDm),
            ChatChannel.Party => Tag("Party", Palette.ChatParty),
            ChatChannel.Shout => Tag("Shout", Palette.ChatShout),
            ChatChannel.Whisper => Tag("Whisper", Palette.ChatWhisper),
            ChatChannel.Tell => Tag("Tell", Palette.ChatTell),
            _ => "",
        };
        string who = m.SpeakerName.Length > 0 ? $"[b][color={Palette.Hex(Palette.AccentBright)}]{Escape(NwnText.Strip(m.SpeakerName))}[/color][/b]: " : "";
        if (m.Channel == ChatChannel.Server && m.SpeakerName.Length == 0 && CombatLine.IsMatch(NwnText.Strip(m.Text))) { Append(Category.Combat, Combat(m.Text)); return; }
        var cat = m.Channel == ChatChannel.Server && m.SpeakerName.Length == 0 ? Category.System : Category.Chat;
        Append(cat, $"{tag}{who}{NwnText.ToBbcode(m.Text, Palette.Hex(color))}");
    }

    static string Tag(string name, Color c) => $"[color={Palette.Hex(c)}][lb]{name}][/color] ";

    /// <summary>Engine feedback (journal updates, skill rolls, saves) in its own colour.</summary>
    public void Feedback(string text, string color = "#e8d080", Category category = Category.System)
    {
        Append(category, NwnText.ToBbcode(text, color));
        Remember(null, text, color);
        FeedbackAdded?.Invoke(text, color);
    }

    /// <summary>Fires with each feedback line and its colour (the classic interface's chat window shows them too).</summary>
    public event Action<string, string>? FeedbackAdded;

    /// <summary>The line is the engine's combat or roll feedback (see <see cref="CombatLine"/>).</summary>
    public static bool IsCombatLine(string text) => CombatLine.IsMatch(text) || KillLine.IsMatch(text);

    /// <summary>The kill line, dialog.tlk 10474 "&lt;CUSTOM0&gt; killed &lt;CUSTOM1&gt;" (killmsg): no colon, no closing full stop.</summary>
    public static readonly System.Text.RegularExpressions.Regex KillLine = new(@"^[^:]+ killed [^:]*[^:.!?]$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>The engine's combat and roll feedback ("X attacks Y : *hit* : (13 + 5 = 18)", saves, damage, spell resistance).</summary>
    static readonly System.Text.RegularExpressions.Regex CombatLine = new(
        @"( attacks | damages |\*hit\*|\*miss\*|\*critical hit\*|\*parried\*|Saving Throw|Spell Resistance|Spell Immunity| casts | : Immune|\*success\*|\*failure\*|\*resisted\*)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>Colours the result tokens of a combat line.</summary>
    static string Combat(string text)
    {
        var t = NwnText.ToBbcode(text);
        t = t.Replace("*critical hit*", $"[color={Palette.Hex(new Color("ff9a3c"))}]*critical hit*[/color]")
            .Replace("*hit*", $"[color={Palette.Hex(Palette.Success)}]*hit*[/color]")
            .Replace("*miss*", $"[color={Palette.Hex(Palette.TextMuted)}]*miss*[/color]")
            .Replace("*success*", $"[color={Palette.Hex(Palette.Success)}]*success*[/color]")
            .Replace("*failure*", $"[color={Palette.Hex(Palette.Danger)}]*failure*[/color]")
            .Replace("*resisted*", $"[color={Palette.Hex(Palette.Info)}]*resisted*[/color]");
        return $"[color={Palette.Hex(Palette.TextSecondary)}]{t}[/color]";
    }

    public override void _Process(double delta)
    {
        // Fade the panel back when nothing is happening.
        double idle = Time.GetTicksMsec() / 1000.0 - _lastActivity;
        _hover = GetGlobalRect().HasPoint(GetGlobalMousePosition());
        bool active = _hover || UI.DM.ChatInput.Typing || idle < 8;
        float a = Mathf.MoveToward(SelfModulate.A, active ? 1f : 0.3f, (float)delta * 2.5f);
        if (Math.Abs(a - SelfModulate.A) > 0.001f) SelfModulate = SelfModulate with { A = a };
        _log.Modulate = _log.Modulate with { A = Mathf.MoveToward(_log.Modulate.A, active ? 1f : 0.75f, (float)delta * 2f) };
    }

    static string Escape(string s) => s.Replace("[", "[lb]");
}
