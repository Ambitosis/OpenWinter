using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.UI.Classic;
using OpenWinter.Net;
using System.Text.Json;
using Godot;

namespace OpenWinter.Client.UI.HudParts;

/// <summary>
/// Spoken and floating text over objects, as in the original: drawn in screen space at a constant,
/// adjustable size (not shrinking with distance), centred just above the speaker's model, wrapped
/// to a column, with colour tokens honoured. A new line from the same speaker replaces the old one.
/// Lives on its own layer so it stays up in cutscene mode, which hides the HUD.
/// </summary>
public partial class SpeechText : Control
{
    public ClientSession Session = null!;
    public Camera3D Camera = null!;
    public Interaction Interaction = null!;

    /// <summary>Column width at size 16; it grows with the text size.</summary>
    const float Column = 340;

    sealed class Line
    {
        public required RichTextLabel Label;
        public double Age, Life;
    }

    readonly Dictionary<uint, Line> _lines = new();
    readonly List<(uint Speaker, Line Line)> _modes = new();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorRight = AnchorBottom = 1;
        Session.NotifyReceived += OnNotify;
        Session.AreaLoading += _ => ClearModes();
    }

    void OnNotify(NotifyMessage message)
    {
        // Mode and lock feedback, and "*Level Up!*" when a level becomes available (xp-changed with feedback).
        if (message.Kind is not ("action-mode" or "lock-feedback" or "xp-changed")) return;
        using var doc = JsonDocument.Parse(message.Json);
        var data = doc.RootElement;
        if (!data.TryGetProperty("feedback", out var f) || string.IsNullOrWhiteSpace(f.GetString())) return;
        if (!data.TryGetProperty("creature", out var who)) return;
        uint speaker = who.GetUInt32();
        if (!Session.Objects.ContainsKey(speaker)) return;
        // Native spaces a burst of messages by about 0.4 s, rather than displaying it all at once.
        var previous = _modes.LastOrDefault(m => m.Speaker == speaker).Line;
        var line = MakeLine("*" + f.GetString() + "*", 1.5, mode: true);
        line.Age = previous == null ? 0 : -Math.Max(0, 0.4 - previous.Age);
        line.Label.Visible = false;
        _modes.Add((speaker, line));
    }

    void ClearModes()
    {
        foreach (var (_, line) in _modes) line.Label.QueueFree();
        _modes.Clear();
    }

    /// <summary>Shows <paramref name="text"/> over the object for a time that grows with its length.</summary>
    public void Say(uint speaker, string text)
    {
        if (!Session.Objects.ContainsKey(speaker) || string.IsNullOrWhiteSpace(NwnText.Strip(text))) return;
        if (_lines.Remove(speaker, out var old) && IsInstanceValid(old.Label)) old.Label.QueueFree();
        _lines[speaker] = MakeLine(text, Math.Clamp(NwnText.Strip(text).Length * 0.08, 3, 9));
        Place(speaker, _lines[speaker]);
    }

    Line MakeLine(string text, double life, bool mode = false)
    {
        int size = Settings.Current.Interface.OverheadTextSize;
        Font font = Fonts.UiMedium;
        ClassicText? native = null;
        if (mode && Session.Content is { } content)
        {
            size = Math.Max(8, (int)MathF.Round(size * 17f / 16));
            native = ClassicText.For(ClassicAssets.For(content), ClassicText.NameFont, size);
            font = native.Font;
        }
        var plain = NwnText.Strip(text);
        float column = Column * size / 16f;
        float width = Math.Min(column, font.GetStringSize(plain, HorizontalAlignment.Left, -1, size).X + 4);
        var label = new RichTextLabel
        {
            BbcodeEnabled = true,
            Text = "[center]" + NwnText.ToBbcode(text) + "[/center]",
            FitContent = true,
            ScrollActive = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(width, 0),
            Size = new Vector2(width, 0),
        };
        label.AddThemeFontOverride("normal_font", font);
        label.AddThemeFontSizeOverride("normal_font_size", size);
        label.AddThemeColorOverride("default_color", new Color(1, 1, 1));
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.9f));
        label.AddThemeConstantOverride("outline_size", Math.Max(4, size / 3));
        if (native != null)
        {
            label.Material = native.Material;
            label.AddThemeColorOverride("font_outline_color", new Color(native.OutlineBrightness, native.OutlineBrightness, native.OutlineBrightness, native.OutlineAlpha));
            label.AddThemeConstantOverride("outline_size", 2);
        }
        AddChild(label);
        return new Line { Label = label, Life = life };
    }

    public override void _Process(double delta)
    {
        foreach (var (id, line) in _lines.ToArray())
        {
            line.Age += delta;
            if (line.Age >= line.Life || !IsInstanceValid(line.Label) || !Session.Objects.ContainsKey(id))
            {
                if (IsInstanceValid(line.Label)) line.Label.QueueFree();
                _lines.Remove(id);
                continue;
            }
            Place(id, line);
            float fade = (float)(line.Life - line.Age);
            line.Label.Modulate = line.Label.Modulate with { A = Math.Clamp(fade / 0.4f, 0, 1) };
        }
        // Mode messages stack (newest nearest the head); speech retains its own lifetime.
        if (_modes.Count == 0) return;
        var heights = new Dictionary<uint, float>();
        for (int i = _modes.Count - 1; i >= 0; i--)
        {
            var (id, line) = _modes[i];
            line.Age += delta;
            if (line.Age >= line.Life || !Session.Objects.ContainsKey(id))
            {
                line.Label.QueueFree();
                _modes.RemoveAt(i);
                continue;
            }
            if (line.Age < 0) { line.Label.Visible = false; continue; }
            float offset = Math.Max(heights.GetValueOrDefault(id), (float)line.Age * 50);
            Place(id, line, offset, mode: true);
            line.Label.Modulate = line.Label.Modulate with { A = (float)Math.Clamp((line.Life - line.Age) / 0.3, 0, 1) };
            heights[id] = offset + line.Label.Size.Y + 2;
        }
    }

    void Place(uint id, Line line, float offset = 0, bool mode = false)
    {
        if (!Session.Objects.TryGetValue(id, out var view)) return;
        var top = mode ? Interaction.HeadTop(view) : Interaction.LabelTop(view);
        if (Camera.IsPositionBehind(top) || mode && !view.IsVisibleInTree()) { line.Label.Visible = false; return; }
        line.Label.Visible = true;
        var screen = Camera.UnprojectPosition(top);
        var size = line.Label.Size;
        // Kept on screen, like the original's, when the speaker is near an edge.
        var area = GetViewportRect().Size;
        var pos = screen - new Vector2(size.X / 2, size.Y + offset);
        pos.X = Math.Clamp(pos.X, 4, Math.Max(4, area.X - size.X - 4));
        pos.Y = Math.Clamp(pos.Y, 4, Math.Max(4, area.Y - size.Y - 4));
        line.Label.Position = pos;
    }
}
