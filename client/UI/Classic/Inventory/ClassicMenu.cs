using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// A small popup list in the original's look (galahad text on a dark box with a brass edge), used for an item's
/// right-click options. A click outside, Esc or picking an entry closes it.
/// </summary>
public partial class ClassicMenu : Control
{
    public static readonly Color Edge = new(0.62f, 0.48f, 0.26f);
    static readonly Color Back = new(0, 0, 0, 0.88f);

    public ClassicAssets Assets = null!;
    readonly List<(string Text, Action? Act)> _rows = new();
    string _title = "";
    Vector2 _at;
    int _hover = -1;
    const float Pad = 6;

    GuiFont Font => Assets.Font(GuiFont.DefaultName);

    /// <summary>Opens a menu at a screen point over <paramref name="parent"/> (the HUD); entries with a null action are greyed.</summary>
    public static ClassicMenu Show(Node parent, ClassicAssets assets, Vector2 at, string title, IEnumerable<(string Text, Action? Act)> rows)
    {
        foreach (var old in parent.GetChildren().OfType<ClassicMenu>()) old.QueueFree();
        var m = new ClassicMenu { Assets = assets, _title = title, _at = at, Name = "ClassicMenu" };
        m._rows.AddRange(rows);
        m.MouseFilter = MouseFilterEnum.Stop;
        parent.AddChild(m);
        m.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return m;
    }

    float RowHeight => Font.LineHeight + 4;
    float HeadHeight => _title.Length > 0 ? RowHeight + 2 : 0;

    Rect2 Box
    {
        get
        {
            float w = Math.Max(Font.Measure(_title), _rows.Select(r => Font.Measure(r.Text)).DefaultIfEmpty(0).Max()) + Pad * 2 + 8;
            float h = HeadHeight + _rows.Count * RowHeight + Pad;
            var view = Size;
            var p = new Vector2(Math.Min(_at.X, view.X - w - 2), Math.Min(_at.Y, view.Y - h - 2));
            return new Rect2(p.Floor(), new Vector2(MathF.Ceiling(w), MathF.Ceiling(h)));
        }
    }

    public IReadOnlyList<(string Text, Action? Act)> Rows => _rows;

    public override void _Draw()
    {
        var box = Box;
        DrawRect(box, Back);
        DrawRect(box, Edge, false, 1);
        float y = box.Position.Y + Pad / 2;
        if (_title.Length > 0)
        {
            Font.Draw(this, new Vector2(box.Position.X + Pad, y + 2), _title, GuiDraw.Selected);
            y += RowHeight;
            DrawLine(new Vector2(box.Position.X + 3, y), new Vector2(box.End.X - 3, y), Edge with { A = 0.6f });
            y += 2;
        }
        for (int i = 0; i < _rows.Count; i++)
        {
            var (text, act) = _rows[i];
            var c = act == null ? GuiDraw.DisabledText : i == _hover ? GuiDraw.Selected : Colors.White;
            if (i == _hover && act != null) DrawRect(new Rect2(box.Position.X + 2, y, box.Size.X - 4, RowHeight), new Color(1, 1, 1, 0.08f));
            Font.Draw(this, new Vector2(box.Position.X + Pad, y + 2), text, c);
            y += RowHeight;
        }
    }

    int RowAt(Vector2 p)
    {
        var box = Box;
        if (!box.HasPoint(p)) return -1;
        int i = (int)MathF.Floor((p.Y - box.Position.Y - Pad / 2 - HeadHeight) / RowHeight);
        return i >= 0 && i < _rows.Count ? i : -1;
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion mm)
        {
            int h = RowAt(mm.Position);
            if (h != _hover) { _hover = h; QueueRedraw(); }
        }
        else if (e is InputEventMouseButton { Pressed: true } mb)
        {
            AcceptEvent();
            int i = RowAt(mb.Position);
            if (mb.ButtonIndex == MouseButton.Left && i >= 0)
            {
                if (_rows[i].Act is not { } act) return;
                if (ClassicGame.Current is { } game) ClassicSounds.Play(game.Assets, ClassicSounds.Button);
                QueueFree();
                act();
                return;
            }
            if (!Box.HasPoint(mb.Position)) QueueFree();
        }
    }

    /// <summary>Picks an entry by its text (captures).</summary>
    public bool Pick(string text)
    {
        var row = _rows.FirstOrDefault(r => r.Text == text);
        if (row.Act == null) return false;
        QueueFree();
        row.Act();
        return true;
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            QueueFree();
            GetViewport().SetInputAsHandled();
        }
    }
}
