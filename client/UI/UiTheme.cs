using OpenWinter.Client.UI.Kit;
using OpenWinter.Client.Views;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The game-window layer of the UI kit (<see cref="Kit"/>): window frames, item icons and slots.
/// Colour names here are kept for older panels and map onto <see cref="Palette"/>.
/// </summary>
public static partial class UiTheme
{
    public static Color Background => Palette.Bg1;
    public static Color Border => Palette.LineStrong;
    public static Color Title => Palette.AccentBright;
    public static Color Text => Palette.Text;
    public static Color Muted => Palette.TextSecondary;
    public static Color Slot => Palette.Bg0;
    public static Color SlotHover => Palette.Bg3;
    public static Color Gold => Palette.AccentBright;

    public static StyleBoxFlat Frame(Color? bg = null, int margin = 14) =>
        bg is { } c ? Styles.Box(c, Palette.RadiusSmall, Palette.Line, 1, margin, Math.Max(0, margin - 2)) : Styles.Card(margin);

    /// <summary>A label in the interface face; large sizes in the brass title colour use the display face.</summary>
    public static Label Label(string text, int size = 15, Color? color = null)
    {
        var l = new Label { Text = text };
        bool title = color is { } c && c == Palette.AccentBright && size >= 17;
        l.AddThemeFontOverride("font", title ? Fonts.Display : size >= 17 ? Fonts.UiMedium : Fonts.Ui);
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", color ?? Palette.Text);
        return l;
    }

    static readonly Dictionary<string, string> TitleIcons = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Inventory"] = "inventory", ["Character"] = "character", ["Journal"] = "journal", ["Map"] = "map",
        ["Spellbook"] = "spellbook", ["Store"] = "gold", ["Container"] = "chest", ["Examine"] = "eye",
        ["Save / Load Game"] = "save", ["Level up"] = "levelup", ["New character"] = "character",
        ["Choose a character"] = "party", ["Creator"] = "magic", ["DM Examine"] = "eye", ["Time & Weather"] = "hourglass",
        ["Options"] = "options",
    };

    /// <summary>
    /// A game window: frosted panel, title bar with an icon and close button, a brass hairline, and
    /// a body (<see cref="Window.Body"/>). Drag by the title bar; Esc closes the most recent one;
    /// opening fades it in and brings it to the front.
    /// </summary>
    public partial class Window : PanelContainer
    {
        public VBoxContainer Body = null!;
        public Label TitleLabel = null!;
        /// <summary>Space in the title bar, before the close button, for extra controls (tabs, filters).</summary>
        public HBoxContainer HeaderExtras = null!;
        public Action? Closed;
        /// <summary>Set false for windows that must be answered (death, character selection).</summary>
        public bool Closable = true;
        TextureRect _icon = null!;
        Button _close = null!;
        bool _drag;
        const float HeaderHeight = 54;

        public static T Create<T>(string title, Vector2 size, string? icon = null) where T : Window, new()
        {
            var w = new T { CustomMinimumSize = size, Size = size };
            w.AddThemeStyleboxOverride("panel", Styles.Window());
            Styles.ApplyGlass(w, 0.9f);
            var outer = new VBoxContainer();
            outer.AddThemeConstantOverride("separation", 0);
            w.AddChild(outer);

            var bar = Ui.HBox(10);
            bar.MouseFilter = MouseFilterEnum.Pass;
            var iconName = icon ?? TitleIcons.GetValueOrDefault(title, "");
            w._icon = Icons.Rect(iconName.Length > 0 ? iconName : "book", 22, Palette.Accent);
            w._icon.Visible = iconName.Length > 0;
            bar.AddChild(w._icon);
            w.TitleLabel = Ui.Text(title, Ui.TextStyle.Title);
            w.TitleLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            w.TitleLabel.MouseFilter = MouseFilterEnum.Pass;
            w.TitleLabel.ClipText = true;
            bar.AddChild(w.TitleLabel);
            w.HeaderExtras = Ui.HBox(6);
            bar.AddChild(w.HeaderExtras);
            w._close = IconButton.Create("close", "Close (Esc)", 30, 12, () => w.Close());
            bar.AddChild(w._close);
            var header = Ui.Pad(bar, 18, 12, 10, 10);
            header.MouseFilter = MouseFilterEnum.Pass;
            outer.AddChild(header);
            outer.AddChild(new Divider { Color = Palette.AccentDim, Fade = Divider.FadeMode.Both, CustomMinimumSize = new Vector2(0, 1) });

            w.Body = new VBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
            w.Body.AddThemeConstantOverride("separation", 10);
            var body = Ui.Pad(w.Body, 18, 14, 18, 18);
            body.SizeFlagsVertical = SizeFlags.ExpandFill;
            outer.AddChild(body);
            w.Visible = false;
            w.VisibilityChanged += w.OnVisibility;
            return w;
        }

        public void SetIcon(string icon)
        {
            _icon.Texture = Icons.Get(icon, 44);
            _icon.Visible = true;
        }

        void OnVisibility()
        {
            if (Visible)
            {
                _close.Visible = Closable;
                WindowStack.Activate(this);
                // Next frame, once sized: keep the window on screen, then fade it in.
                Callable.From(() =>
                {
                    if (!IsInsideTree() || !Visible) return;
                    var view = GetViewportRect().Size;
                    var pos = GlobalPosition;
                    var clamped = new Vector2(Math.Clamp(pos.X, 0, Math.Max(0, view.X - Size.X)), Math.Clamp(pos.Y, 0, Math.Max(0, view.Y - Size.Y)));
                    if (clamped != pos) GlobalPosition = clamped;
                    PivotOffset = Size / 2;
                    Scale = new Vector2(0.97f, 0.97f);
                    Modulate = Modulate with { A = 0 };
                    var tw = CreateTween().SetParallel().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
                    tw.TweenProperty(this, "modulate:a", 1f, 0.14);
                    tw.TweenProperty(this, "scale", Vector2.One, 0.16);
                }).CallDeferred();
            }
            else WindowStack.Remove(this);
        }

        public virtual void Close()
        {
            if (!Visible) return;
            Visible = false;
            Closed?.Invoke();
        }

        public override void _GuiInput(InputEvent e)
        {
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
            {
                _drag = mb.Pressed && mb.Position.Y < HeaderHeight;
                if (mb.Pressed) WindowStack.Activate(this);
            }
            else if (e is InputEventMouseMotion mm && _drag) Position += mm.Relative;
        }
    }

    /// <summary>An item icon: layered textures (composite weapons stack three), with a stack count.</summary>
    public static Control Icon(ClientContent content, IEnumerable<string> layers, int stack = 1, int size = 44)
    {
        var root = new Control { CustomMinimumSize = new Vector2(size, size), MouseFilter = Control.MouseFilterEnum.Ignore };
        bool any = false;
        foreach (var layer in layers)
        {
            if (content.Textures.Get(layer) is not { } tex) continue;
            any = true;
            root.AddChild(new TextureRect
            {
                Texture = tex.Texture, FlipV = tex.FlipV, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
                AnchorRight = 1, AnchorBottom = 1,
            });
        }
        if (!any)
        {
            var fallback = Icons.Rect("chest", size / 2, Palette.TextMuted);
            fallback.AnchorRight = fallback.AnchorBottom = 1;
            root.AddChild(fallback);
        }
        if (stack > 1)
        {
            var count = Ui.Overlay(stack.ToString(), 12, Palette.Text);
            count.AnchorLeft = count.AnchorRight = count.AnchorTop = count.AnchorBottom = 1;
            count.OffsetLeft = -26; count.OffsetTop = -17; count.OffsetRight = -1; count.OffsetBottom = 1;
            count.HorizontalAlignment = HorizontalAlignment.Right;
            root.AddChild(count);
        }
        return root;
    }

    /// <summary>A clickable slot holding an icon (or empty), outlined in brass on hover.</summary>
    public static Button SlotButton(Control? icon, string tooltip, int size = 52, string? placeholder = null)
    {
        var b = new Button { CustomMinimumSize = new Vector2(size, size), TooltipText = tooltip, FocusMode = Control.FocusModeEnum.None };
        b.AddThemeStyleboxOverride("normal", Styles.Slot());
        b.AddThemeStyleboxOverride("hover", Styles.Slot(hover: true));
        b.AddThemeStyleboxOverride("pressed", Styles.Slot(hover: true, active: true));
        b.AddThemeStyleboxOverride("hover_pressed", Styles.Slot(hover: true, active: true));
        // Empty equipment slots show a faint silhouette of what goes there.
        if (icon == null && placeholder != null) icon = Icons.Rect(placeholder, size / 2, new Color(1, 1, 1, 0.13f));
        if (icon != null)
        {
            icon.AnchorRight = icon.AnchorBottom = 1;
            icon.OffsetLeft = icon.OffsetTop = 4; icon.OffsetRight = icon.OffsetBottom = -4;
            b.AddChild(icon);
        }
        return b;
    }
}

/// <summary>Open windows, most recently used last: Esc closes the top one; clicking one raises it.</summary>
public static class WindowStack
{
    static readonly List<UiTheme.Window> Open = new();

    public static IReadOnlyList<UiTheme.Window> Windows => Open;

    public static void Activate(UiTheme.Window w)
    {
        Open.Remove(w);
        Open.Add(w);
        if (w.IsInsideTree()) w.MoveToFront();
    }

    public static void Remove(UiTheme.Window w) => Open.Remove(w);

    /// <summary>Dismisses old-module windows, including nonclosable script/state panels.</summary>
    public static void CloseAll()
    {
        foreach (var window in Open.ToArray())
            if (GodotObject.IsInstanceValid(window)) window.Close();
    }

    /// <summary>Closes the most recent closable window; false when none was open.</summary>
    /// <summary>Classic interface windows close first (they keep their own order); true when one closed.</summary>
    public static Func<bool>? CloseClassic;

    public static bool CloseTop()
    {
        if (CloseClassic?.Invoke() == true) return true;
        for (int i = Open.Count - 1; i >= 0; i--)
        {
            var w = Open[i];
            if (!GodotObject.IsInstanceValid(w) || !w.Visible) { Open.RemoveAt(i); continue; }
            if (!w.Closable) continue;
            w.Close();
            return true;
        }
        return false;
    }
}
