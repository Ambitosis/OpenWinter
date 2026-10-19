using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.Classic.Sheets;

/// <summary>
/// Small pieces the classic sheets share: the original's scroll bar (gui_spl_btn_up / down arrows and the ridged
/// gui_scrollbar thumb on a black trough), a clipped scrolling area, text drawn in the original's fonts, and the
/// thin row boxes of the spellbook and journal lists.
/// </summary>
public static class SheetKit
{
    /// <summary>Label text (white) and the grey of captions such as "Main Weapon:".</summary>
    public static readonly Color White = Colors.White, Caption = new(0.667f, 0.667f, 0.667f);
    /// <summary>The hairline of list rows (a light grey over the panel's dark glass).</summary>
    public static readonly Color RowLine = new(0.78f, 0.78f, 0.66f, 0.32f);
    /// <summary>The original's long-text font (TrueType) and its label font (bitmap).</summary>
    public const string Body = "fnt_default", Label = "fnt_galahad14";

    /// <summary>A rectangle between two nodes of a model view, in the view's parent coordinates (null when a node is missing).</summary>
    public static Rect2? NodeBox(GuiModelView view, string a, string b)
    {
        if (view.Node(a) is not { } p || view.Node(b) is not { } q) return null;
        return new Rect2(p + view.Position, Vector2.Zero).Expand(q + view.Position);
    }

    /// <summary>A text label in a box of its parent.</summary>
    public static GuiLabel Text(Control parent, ClassicAssets assets, Rect2 box, string text = "", string font = Label, Color? color = null, float alignH = 0, float alignV = 0.5f)
    {
        var l = GuiLabel.Create(assets, text, font, color ?? White, alignH, alignV);
        l.Position = box.Position;
        l.Size = box.Size;
        parent.AddChild(l);
        return l;
    }

    /// <summary>A row box: a hairline frame (optionally filled).</summary>
    public static void RowBox(CanvasItem ci, Rect2 r, Color? fill = null)
    {
        if (fill is { } f) ci.DrawRect(r, f);
        Frame(ci, r, RowLine);
    }

    /// <summary>A one-pixel outline inside a rectangle, each pixel drawn once (so a translucent colour stays even).</summary>
    public static void Frame(CanvasItem ci, Rect2 r, Color line, bool right = true, bool bottom = true)
    {
        float h = r.Size.Y - (bottom ? 2 : 1);
        ci.DrawRect(new Rect2(r.Position, new Vector2(r.Size.X - (right ? 0 : 1), 1)), line);
        if (bottom) ci.DrawRect(new Rect2(r.Position.X, r.End.Y - 1, r.Size.X - (right ? 0 : 1), 1), line);
        ci.DrawRect(new Rect2(r.Position.X, r.Position.Y + 1, 1, h), line);
        if (right) ci.DrawRect(new Rect2(r.End.X - 1, r.Position.Y + 1, 1, h), line);
    }

    /// <summary>Signed number as the original shows bonuses ("+2", "-1", "0").</summary>
    public static string Signed(int v) => v > 0 ? $"+{v}" : v.ToString();
}

/// <summary>
/// The original's scroll bar: arrow buttons (gui_spl_btn_up / gui_spl_btn_down) at the ends of a black trough and
/// a ridged thumb (gui_scrollbar, repeated) sized to the visible share. Drives a <see cref="SheetScroll"/>.
/// </summary>
public partial class SheetScrollBar : Control
{
    public ClassicAssets Assets = null!;
    public float Page = 1, Total = 1, Offset;
    public Action<float>? Scrolled;
    /// <summary>Line step for the arrow buttons.</summary>
    public float Step = 20;
    bool _drag;
    float _grab;
    public const float Width = 14;

    public SheetScrollBar() { MouseFilter = MouseFilterEnum.Stop; TextureRepeat = TextureRepeatEnum.Enabled; }

    float Arrow => Size.X;
    float Track => Math.Max(1, Size.Y - 2 * Arrow);
    float ThumbLength => Total <= Page ? Track : Math.Max(Arrow, Track * Page / Total);
    float ThumbTop => Arrow + (Total <= Page ? 0 : (Track - ThumbLength) * Offset / Math.Max(1, Total - Page));

    public override void _Draw()
    {
        float w = Size.X;
        DrawRect(new Rect2(0, 0, w, Size.Y), Colors.Black);
        // The arrow buttons are the framed arrows of ctl_cg_btn_skup / skdn (16 px), drawn at the bar's width.
        Button("ctl_cg_btn_skup", 0);
        Button("ctl_cg_btn_skdn", Size.Y - Arrow);
        if (Total <= Page) return;
        // The thumb: gui_scrollbar's ridges run along u; the original lays u along the bar.
        var thumb = new Rect2(1, ThumbTop, w - 2, ThumbLength);
        if (Assets.Texture("gui_scrollbar") is { } tex) GuiDraw.ScrollThumb(this, tex, thumb);
        else DrawRect(thumb, new Color(0.6f, 0.45f, 0.25f));
    }

    void Button(string model, float y)
    {
        if (Assets.Model(model) is not { } m) return;
        float s = Size.X / Math.Max(1, m.Bounds.Size.X);
        DrawSetTransform(new Vector2(0, y), 0, new Vector2(s, s));
        GuiDraw.Model(this, Assets, m.Rest, -m.Bounds.Position, Colors.White);
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed)
            {
                if (mb.Position.Y < Arrow) Set(Offset - Step);
                else if (mb.Position.Y > Size.Y - Arrow) Set(Offset + Step);
                else if (Total > Page && mb.Position.Y >= ThumbTop && mb.Position.Y <= ThumbTop + ThumbLength) { _drag = true; _grab = mb.Position.Y - ThumbTop; }
                else if (Total > Page) Set(Offset + (mb.Position.Y < ThumbTop ? -Page : Page));
            }
            else _drag = false;
            AcceptEvent();
        }
        else if (e is InputEventMouseMotion mm && _drag)
        {
            float f = (mm.Position.Y - _grab - Arrow) / Math.Max(1, Track - ThumbLength);
            Set(f * (Total - Page));
            AcceptEvent();
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } w)
        {
            Set(Offset + (w.ButtonIndex == MouseButton.WheelUp ? -Step : Step));
            AcceptEvent();
        }
    }

    public void Set(float offset)
    {
        Offset = Math.Clamp(offset, 0, Math.Max(0, Total - Page));
        Scrolled?.Invoke(Offset);
        QueueRedraw();
    }
}

/// <summary>
/// A clipped area whose <see cref="Content"/> scrolls vertically under a <see cref="SheetScrollBar"/> (placed by the
/// owner, usually at the model's scrollmin / scrollmax). Content is laid out by the owner: set its height with
/// <see cref="SetContentHeight"/>.
/// </summary>
public partial class SheetScroll : Control
{
    public readonly Control Content = new() { MouseFilter = MouseFilterEnum.Pass };
    public SheetScrollBar Bar = null!;
    float _offset;

    public SheetScroll() { ClipContents = true; MouseFilter = MouseFilterEnum.Pass; }

    public static SheetScroll Create(ClassicAssets assets, Rect2 area, Rect2 bar, Control parent)
    {
        var s = new SheetScroll { Position = area.Position, Size = area.Size };
        s.AddChild(s.Content);
        s.Content.Size = new Vector2(area.Size.X, 0);
        parent.AddChild(s);
        s.Bar = new SheetScrollBar { Assets = assets, Position = bar.Position, Size = bar.Size };
        s.Bar.Scrolled = o => { s._offset = o; s.Content.Position = new Vector2(0, -o); };
        parent.AddChild(s.Bar);
        s.Resized += () => s.SetContentHeight(s.Content.Size.Y);
        return s;
    }

    public void SetContentHeight(float height)
    {
        Content.Size = new Vector2(Size.X, height);
        Bar.Total = height;
        Bar.Page = Size.Y;
        _offset = Math.Clamp(_offset, 0, Math.Max(0, height - Size.Y));
        Bar.Offset = _offset;
        Content.Position = new Vector2(0, -_offset);
        Bar.QueueRedraw();
    }

    public void ScrollTo(float offset) => Bar.Set(offset);

    public void Clear()
    {
        foreach (var c in Content.GetChildren()) { Content.RemoveChild(c); c.QueueFree(); }
        SetContentHeight(0);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } w)
        {
            Bar.Set(_offset + (w.ButtonIndex == MouseButton.WheelUp ? -1 : 1) * Bar.Step * 2);
            AcceptEvent();
        }
    }
}

/// <summary>A line of text drawn in a font, with an optional grey caption before it ("Main Weapon: " + value).</summary>
public partial class SheetLine : Control
{
    public ClassicAssets Assets = null!;
    public string Caption = "", Value = "";
    public string Font = SheetKit.Body;
    public Color CaptionColor = SheetKit.Caption, ValueColor = SheetKit.White;
    public bool Wrap;

    public SheetLine() { MouseFilter = MouseFilterEnum.Pass; }

    public override void _Draw()
    {
        var f = Assets.Font(Font);
        float y = (Size.Y - f.Height) / 2;
        if (Wrap)
        {
            f.DrawBox(this, new Rect2(0, 0, Size.X, Size.Y), NwnText.Strip(Caption + Value), ValueColor, 0, 0, true);
            return;
        }
        f.Draw(this, new Vector2(0, y), Caption, CaptionColor);
        f.Draw(this, new Vector2(f.Measure(Caption), y), NwnText.Strip(Value), ValueColor);
    }
}
