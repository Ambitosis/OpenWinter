using OpenWinter.Client.UI.Nui.Model;
using Godot;

namespace OpenWinter.Client.UI.Nui;

/// <summary>
/// A clipped viewport onto <see cref="Content"/> with NUI-skinned scrollbars on the
/// right / bottom (they take room from the viewport). Used by groups, NuiText and lists.
/// </summary>
public partial class NuiScrollArea : Control
{
    public NuiSkin Skin = null!;
    public readonly Control Content = new() { MouseFilter = MouseFilterEnum.Pass };
    readonly Control _clip = new() { ClipContents = true, MouseFilter = MouseFilterEnum.Pass };
    Vector2 _contentSize;
    bool _showX, _showY;
    Vector2 _scroll;
    int _drag = -1; // 0 = x thumb, 1 = y thumb
    float _dragOffset;

    public event Action? Scrolled;
    public Vector2 ScrollOffset => _scroll;
    public Vector2 ViewportSize => new(Math.Max(0, Size.X - (_showY ? NuiStyle.ScrollbarSize : 0)), Math.Max(0, Size.Y - (_showX ? NuiStyle.ScrollbarSize : 0)));

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        _clip.AddChild(Content);
        AddChild(_clip);
    }

    /// <summary>Sets the scrolled content's size and which bars show.</summary>
    public void SetContent(Vector2 size, bool showX, bool showY)
    {
        _contentSize = size;
        _showX = showX;
        _showY = showY;
        _clip.Position = Vector2.Zero;
        _clip.Size = ViewportSize;
        Content.Size = size;
        Clamp();
        QueueRedraw();
    }

    Vector2 MaxScroll => new(Math.Max(0, _contentSize.X - ViewportSize.X), Math.Max(0, _contentSize.Y - ViewportSize.Y));

    void Clamp()
    {
        var max = MaxScroll;
        _scroll = new Vector2(Math.Clamp(_scroll.X, 0, max.X), Math.Clamp(_scroll.Y, 0, max.Y));
        Content.Position = -_scroll;
    }

    public void ScrollTo(Vector2 offset)
    {
        var old = _scroll;
        _scroll = offset;
        Clamp();
        if (old != _scroll)
        {
            QueueRedraw();
            Scrolled?.Invoke();
        }
    }

    /// <summary>Wheel scrolling (dy &gt; 0 scrolls up); false when already at the end.</summary>
    public bool ScrollBy(float dx, float dy)
    {
        var old = _scroll;
        float step = 3 * (NuiStyle.RowHeight + 4);
        if (dy != 0 && MaxScroll.Y > 0) ScrollTo(_scroll - new Vector2(0, dy * step));
        else if (MaxScroll.X > 0) ScrollTo(_scroll - new Vector2((dx != 0 ? -dx : dy) * step, 0));
        return old != _scroll;
    }

    Rect2 Track(bool vertical)
    {
        var v = ViewportSize;
        return vertical
            ? new Rect2(v.X, 0, NuiStyle.ScrollbarSize, v.Y)
            : new Rect2(0, v.Y, v.X, NuiStyle.ScrollbarSize);
    }

    Rect2 Thumb(bool vertical)
    {
        var track = Track(vertical);
        float view = vertical ? ViewportSize.Y : ViewportSize.X;
        float content = Math.Max(view, vertical ? _contentSize.Y : _contentSize.X);
        float len = vertical ? track.Size.Y : track.Size.X;
        float thumb = content <= 0 ? len : Math.Max(16, len * view / content);
        float max = vertical ? MaxScroll.Y : MaxScroll.X;
        float t = max > 0 ? (vertical ? _scroll.Y : _scroll.X) / max : 0;
        float pos = (len - thumb) * t;
        return vertical
            ? new Rect2(track.Position.X + 2, track.Position.Y + pos, track.Size.X - 4, thumb)
            : new Rect2(track.Position.X + pos, track.Position.Y + 2, thumb, track.Size.Y - 4);
    }

    public override void _Draw()
    {
        for (int i = 0; i < 2; i++)
        {
            bool vertical = i == 1;
            if (vertical ? !_showY : !_showX) continue;
            var track = Track(vertical);
            DrawRect(track, new Color(0, 0, 0, 0.55f));
            DrawRect(track, new Color(NuiSkin.Bronze, 0.5f), false, 1);
            if ((vertical ? MaxScroll.Y : MaxScroll.X) <= 0) continue;
            var thumb = Thumb(vertical);
            if (Skin.Texture(vertical ? "nui_scrollv" : "nui_scrollh") is { } tex)
                DrawTextureRect(tex, thumb, false);
            else
                DrawRect(thumb, NuiSkin.Accent);
        }
    }

    public override void _GuiInput(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } wheel:
                if (ScrollBy(0, wheel.ButtonIndex == MouseButton.WheelUp ? 1 : -1)) AcceptEvent();
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                if (!mb.Pressed) { _drag = -1; break; }
                for (int i = 0; i < 2; i++)
                {
                    bool vertical = i == 1;
                    if (vertical ? !_showY : !_showX) continue;
                    if (!Track(vertical).HasPoint(mb.Position)) continue;
                    var thumb = Thumb(vertical);
                    if (thumb.HasPoint(mb.Position))
                    {
                        _drag = i;
                        _dragOffset = vertical ? mb.Position.Y - thumb.Position.Y : mb.Position.X - thumb.Position.X;
                    }
                    else
                    {
                        // Page towards the click.
                        var page = vertical ? new Vector2(0, ViewportSize.Y) : new Vector2(ViewportSize.X, 0);
                        bool before = vertical ? mb.Position.Y < thumb.Position.Y : mb.Position.X < thumb.Position.X;
                        ScrollTo(_scroll + (before ? -page : page));
                    }
                    AcceptEvent();
                }
                break;
            case InputEventMouseMotion mm when _drag >= 0:
            {
                bool vertical = _drag == 1;
                var track = Track(vertical);
                var thumb = Thumb(vertical);
                float len = vertical ? track.Size.Y - thumb.Size.Y : track.Size.X - thumb.Size.X;
                float p = (vertical ? mm.Position.Y - track.Position.Y : mm.Position.X - track.Position.X) - _dragOffset;
                float t = len > 0 ? Math.Clamp(p / len, 0, 1) : 0;
                var max = MaxScroll;
                ScrollTo(vertical ? new Vector2(_scroll.X, t * max.Y) : new Vector2(t * max.X, _scroll.Y));
                AcceptEvent();
                break;
            }
        }
    }
}
