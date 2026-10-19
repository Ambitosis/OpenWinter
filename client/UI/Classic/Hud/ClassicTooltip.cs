using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>A HUD control that shows hover text in the original's look (see <see cref="ClassicTooltip"/>).</summary>
public interface IClassicHover
{
    /// <summary>The lines to show now; null or empty for none.</summary>
    string[]? HoverLines();

    /// <summary>
    /// True when the text shows as soon as the pointer is over the control, as the original's item and portrait
    /// hover text does; false waits <see cref="ClassicTooltip.Delay"/> first.
    /// </summary>
    bool HoverInstant => false;
}

/// <summary>
/// The original's hover text, measured on its inventory (invitemring: "Backpack Dagger", "Five Potions"): white
/// lines in the text face (fnt_default, fnt_default_hr with the large font) centred on a borderless black box at
/// 60% opacity, 20 px high for one line, at least about 112 px wide, its top-left 5 px right of and 40 px below the
/// pointer. The HUD root shows one for the control under the pointer that implements <see cref="IClassicHover"/>
/// or carries a "classic_hover" text meta (models' buttons).
/// </summary>
public partial class ClassicTooltip : Control
{
    ClassicText _font = null!;
    string[] _lines = [];
    public const string Meta = "classic_hover";
    /// <summary>The wait before hover text on controls that are not instant (not measured on the original).</summary>
    public const double Delay = 0.4;
    /// <summary>Measured: the box's top-left from the pointer and its fill; one line's box is 20 px high, at least 112 wide.</summary>
    public static readonly Vector2 PointerOffset = new(5, 40);
    public static readonly Color Fill = new(0, 0, 0, 0.6f);
    public const float LinePad = 6, MinWidth = 112, SidePad = 12;

    Control _text = null!;

    public static ClassicTooltip Create(ClassicAssets assets)
    {
        var t = new ClassicTooltip { _font = ClassicText.For(assets), MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        // The glyphs get the face's coverage contrast; the box keeps its own alpha.
        t._text = t._font.CreateLayer(t, t.DrawText);
        return t;
    }

    public string[] Lines => _lines;

    public void SetLines(string[] lines)
    {
        var clean = lines.SelectMany(l => (l ?? "").Replace("\r", "").Split('\n')).Select(l => Kit.NwnText.Strip(l).Trim())
            .Where(l => l.Length > 0).SelectMany(Wrap).ToArray();
        if (clean.SequenceEqual(_lines)) return;
        _lines = clean;
        float w = _lines.Length == 0 ? 0 : _lines.Max(_font.Measure);
        Size = new Vector2(MathF.Max(MinWidth, MathF.Ceiling(w) + 2 * SidePad), _lines.Length * _font.LineHeight + LinePad);
        QueueRedraw();
        _text.QueueRedraw();
    }

    /// <summary>Not measured: lines wider than this (long map notes) wrap at spaces.</summary>
    public const float MaxTextWidth = 420;

    IEnumerable<string> Wrap(string line)
    {
        if (_font.Measure(line) <= MaxTextWidth) { yield return line; yield break; }
        var current = "";
        foreach (var word in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var next = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && _font.Measure(next) > MaxTextWidth) { yield return current; current = word; }
            else current = next;
        }
        if (current.Length > 0) yield return current;
    }

    /// <summary>Places the box for a pointer at <paramref name="pointer"/>, kept on the screen.</summary>
    public void PlaceAt(Vector2 pointer, Vector2 view)
    {
        var at = pointer + PointerOffset;
        // Not measured: near the bottom edge the box goes above the pointer instead.
        if (at.Y + Size.Y > view.Y) at.Y = pointer.Y - Size.Y - 4;
        at.X = Math.Clamp(at.X, 0, Math.Max(0, view.X - Size.X));
        at.Y = Math.Clamp(at.Y, 0, Math.Max(0, view.Y - Size.Y));
        Position = at.Floor();
    }

    public override void _Draw() => DrawRect(new Rect2(Vector2.Zero, Size), Fill);

    void DrawText(CanvasItem ci)
    {
        // Measured: capitals start 4 px under the box's top edge (one line above the even split).
        float y = LinePad / 2 - 2;
        foreach (var line in _lines)
        {
            float x = MathF.Round((Size.X - _font.Measure(line)) / 2);
            _font.DrawLine(ci, new Vector2(x, y), line, Colors.White);
            y += _font.LineHeight;
        }
    }

    /// <summary>The hover lines a control offers (itself or the nearest parent that offers any), and whether they show at once.</summary>
    public static (Control Owner, string[] Lines, bool Instant)? LinesFor(Control? c)
    {
        for (; c != null; c = c.GetParent() as Control)
        {
            if (c is IClassicHover h) return h.HoverLines() is { Length: > 0 } l ? (c, l, h.HoverInstant) : null;
            if (c.HasMeta(Meta)) return (c, c.GetMeta(Meta).AsString().Split('\n'), false);
        }
        return null;
    }
}
