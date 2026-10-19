using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The chat window's scroll bar as the original draws it: brass arrow boxes at the ends (pb_scrl_up /
/// pb_scrl_down) and the ridged thumb between them (gui_scrollbar: a top cap, a stretched middle and a bottom
/// cap, as scrollbar_simple slices it). The thumb fills the track while everything fits.
/// </summary>
public partial class ClassicScrollBar : Control
{
    public ClassicAssets Assets = null!;
    public float Page = 1, Total = 1, Offset, Step = 16;
    public Action<float>? Scrolled;
    GuiModel? _up, _down;
    bool _drag;
    float _grab;
    int _pressed; // -1 up arrow, 1 down arrow

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        _up = Assets.Model("pb_scrl_up");
        _down = Assets.Model("pb_scrl_down");
    }

    float Arrow => _up?.Bounds.Size.Y ?? 16;
    float Track => Math.Max(1, Size.Y - 2 * Arrow);
    float ThumbLength => Total <= Page ? Track : Math.Max(12, Track * Page / Total);
    float ThumbTop => Arrow + (Total <= Page ? 0 : (Track - ThumbLength) * Offset / Math.Max(1, Total - Page));

    public override void _Draw()
    {
        if (_up != null) GuiDraw.Model(this, Assets, _up.Rest, -_up.Bounds.Position + new Vector2(0, _pressed < 0 ? 1 : 0), Colors.White);
        if (_down != null) GuiDraw.Model(this, Assets, _down.Rest, -_down.Bounds.Position + new Vector2(0, Size.Y - Arrow + (_pressed > 0 ? 1 : 0)), Colors.White);
        if (Assets.Texture("gui_scrollbar") is not { } tex) return;
        float w = 12, x = MathF.Round((Size.X - w) / 2);
        GuiDraw.ScrollThumbCapped(this, tex, new Rect2(x, MathF.Round(ThumbTop), w, MathF.Round(ThumbLength)));
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
        {
            if (mb.Pressed)
            {
                if (mb.Position.Y < Arrow) { _pressed = -1; Set(Offset - Step); }
                else if (mb.Position.Y > Size.Y - Arrow) { _pressed = 1; Set(Offset + Step); }
                else if (mb.Position.Y >= ThumbTop && mb.Position.Y <= ThumbTop + ThumbLength) { _drag = true; _grab = mb.Position.Y - ThumbTop; }
                else Set(Offset + (mb.Position.Y < ThumbTop ? -Page : Page));
            }
            else { _drag = false; _pressed = 0; }
            QueueRedraw();
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
