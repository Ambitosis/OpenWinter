using Godot;

namespace OpenWinter.Client.UI.Kit;

/// <summary>
/// The Godot <see cref="Theme"/> for the whole client, set on the root window so every stock
/// control (buttons, fields, lists, tabs, sliders, scroll bars, popups, tooltips) shares the kit's look.
/// Variations: "PrimaryButton", "GhostButton", "TitleLabel", "HeadingLabel", "CaptionLabel", "ReadingLabel".
/// </summary>
public static class GameTheme
{
    static Theme? _theme;
    static bool _installed;

    public static Theme Theme => _theme ??= Build();

    /// <summary>
    /// Installs the theme on the root window and on every top-level control as it's added: controls under a
    /// CanvasLayer (the HUD, menus) don't inherit the window's theme, so each subtree's root gets it directly.
    /// </summary>
    public static void Install(SceneTree tree)
    {
        tree.Root.Theme = Theme;
        // Exit to Main Menu reloads the scene and runs this again; hook the tree only once.
        if (_installed) return;
        _installed = true;
        tree.NodeAdded += node =>
        {
            if (node is Control c && c.GetParent() is not Control && c.Theme == null) c.Theme = Theme;
            // Popups (menus, tooltips, dialogs) are windows of their own and don't inherit either.
            else if (node is Window w && w.Theme == null) w.Theme = Theme;
        };
    }

    static Theme Build()
    {
        var t = new Theme { DefaultFont = Fonts.Ui, DefaultFontSize = Palette.FontBody };
        var text = Palette.Text;

        // ---- labels
        t.SetColor("font_color", "Label", text);
        t.SetColor("font_shadow_color", "Label", Colors.Transparent);
        Variation(t, "TitleLabel", "Label", Fonts.Display, Palette.FontTitle, Palette.AccentBright);
        Variation(t, "HeadingLabel", "Label", Fonts.Display, Palette.FontHeading, Palette.AccentBright);
        Variation(t, "CaptionLabel", "Label", Fonts.UiMedium, Palette.FontCaption, Palette.TextMuted);
        Variation(t, "ReadingLabel", "Label", Fonts.Reading, 19, text);

        // ---- rich text
        t.SetFont("normal_font", "RichTextLabel", Fonts.Ui);
        t.SetFont("bold_font", "RichTextLabel", Fonts.UiBold);
        t.SetFont("italics_font", "RichTextLabel", Fonts.ReadingItalic);
        t.SetFont("bold_italics_font", "RichTextLabel", Fonts.UiBold);
        t.SetColor("default_color", "RichTextLabel", text);
        t.SetStylebox("normal", "RichTextLabel", new StyleBoxEmpty());
        t.SetStylebox("focus", "RichTextLabel", new StyleBoxEmpty());

        // ---- buttons
        ButtonStyles(t, "Button",
            Styles.Box(Palette.Bg3, Palette.RadiusSmall, Palette.LineStrong, 1, 14, 7),
            Styles.Box(Palette.Bg4, Palette.RadiusSmall, Palette.AccentDim, 1, 14, 7),
            Styles.Box(Palette.Bg2, Palette.RadiusSmall, Palette.Accent, 1, 14, 7),
            Styles.Box(Palette.Bg2 with { A = 0.6f }, Palette.RadiusSmall, Palette.Line, 1, 14, 7),
            text, Palette.AccentBright);
        t.SetFont("font", "Button", Fonts.UiMedium);
        t.SetConstant("h_separation", "Button", 8);
        t.SetConstant("icon_max_width", "Button", 22);

        t.SetTypeVariation("PrimaryButton", "Button");
        ButtonStyles(t, "PrimaryButton",
            Styles.Box(Palette.Accent, Palette.RadiusSmall, Palette.AccentBright with { A = 0.6f }, 1, 18, 8),
            Styles.Box(Palette.AccentBright, Palette.RadiusSmall, Palette.AccentBright, 1, 18, 8),
            Styles.Box(Palette.AccentDim, Palette.RadiusSmall, Palette.Accent, 1, 18, 8),
            Styles.Box(Palette.AccentDim with { A = 0.4f }, Palette.RadiusSmall, null, 1, 18, 8),
            Palette.TextOnAccent, Palette.TextOnAccent);
        t.SetFont("font", "PrimaryButton", Fonts.UiBold);

        t.SetTypeVariation("GhostButton", "Button");
        ButtonStyles(t, "GhostButton",
            Styles.Box(Colors.Transparent, Palette.RadiusSmall, null, 0, 8, 5),
            Styles.Box(new Color(1, 1, 1, 0.06f), Palette.RadiusSmall, null, 0, 8, 5),
            Styles.Box(new Color(1, 1, 1, 0.1f), Palette.RadiusSmall, null, 0, 8, 5),
            Styles.Box(Colors.Transparent, Palette.RadiusSmall, null, 0, 8, 5),
            Palette.TextSecondary, Palette.AccentBright);

        // Menu rows (main menu, game menu): large, left aligned, highlighted bar on hover.
        t.SetTypeVariation("MenuRow", "Button");
        var menuHover = Styles.Box(new Color(0.83f, 0.65f, 0.34f, 0.12f), Palette.RadiusSmall, null, 0, 18, 10);
        menuHover.BorderColor = Palette.Accent;
        menuHover.BorderWidthLeft = 3;
        ButtonStyles(t, "MenuRow",
            Styles.Box(Colors.Transparent, Palette.RadiusSmall, null, 0, 18, 10), menuHover, menuHover,
            Styles.Box(Colors.Transparent, Palette.RadiusSmall, null, 0, 18, 10),
            Palette.Text, Palette.AccentBright);
        t.SetFont("font", "MenuRow", Fonts.Display);
        t.SetFontSize("font_size", "MenuRow", 19);

        // ---- text fields
        var field = Styles.Box(new Color(0, 0, 0, 0.35f), Palette.RadiusSmall, Palette.LineStrong, 1, 10, 7);
        var fieldFocus = Styles.Box(new Color(0, 0, 0, 0.45f), Palette.RadiusSmall, Palette.Accent, 1, 10, 7);
        foreach (var type in new[] { "LineEdit", "TextEdit" })
        {
            t.SetStylebox("normal", type, field);
            t.SetStylebox("focus", type, fieldFocus);
            t.SetStylebox("read_only", type, Styles.Box(new Color(0, 0, 0, 0.2f), Palette.RadiusSmall, Palette.Line, 1, 10, 7));
            t.SetColor("font_color", type, text);
            t.SetColor("font_placeholder_color", type, Palette.TextMuted);
            t.SetColor("caret_color", type, Palette.AccentBright);
            t.SetColor("selection_color", type, Palette.Accent with { A = 0.35f });
        }

        // ---- panels
        t.SetStylebox("panel", "PanelContainer", Styles.Card());
        t.SetStylebox("panel", "Panel", Styles.Card());
        t.SetTypeVariation("InsetPanel", "PanelContainer");
        t.SetStylebox("panel", "InsetPanel", Styles.Inset());
        t.SetTypeVariation("CardPanel", "PanelContainer");
        t.SetStylebox("panel", "CardPanel", Styles.Card());

        // ---- separators
        var sep = new StyleBoxLine { Color = Palette.Line, Thickness = 1 };
        t.SetStylebox("separator", "HSeparator", sep);
        t.SetConstant("separation", "HSeparator", 9);
        t.SetStylebox("separator", "VSeparator", new StyleBoxLine { Color = Palette.Line, Thickness = 1, Vertical = true });
        t.SetConstant("separation", "VSeparator", 9);

        // ---- scroll bars: thin, rounded, brighter on hover
        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            t.SetStylebox("scroll", type, Styles.Box(new Color(1, 1, 1, 0.03f), 4, null, 0, 3, 3));
            t.SetStylebox("scroll_focus", type, Styles.Box(new Color(1, 1, 1, 0.03f), 4, null, 0, 3, 3));
            t.SetStylebox("grabber", type, Styles.Box(new Color(1, 1, 1, 0.16f), 4, null, 0, 3, 3));
            t.SetStylebox("grabber_highlight", type, Styles.Box(Palette.AccentDim, 4, null, 0, 3, 3));
            t.SetStylebox("grabber_pressed", type, Styles.Box(Palette.Accent, 4, null, 0, 3, 3));
            foreach (var icon in new[] { "increment", "decrement", "increment_highlight", "decrement_highlight", "increment_pressed", "decrement_pressed" })
                t.SetIcon(icon, type, new ImageTexture());
        }

        // ---- tabs: text tabs, underline on the selected one
        var tabSel = Styles.Box(Colors.Transparent, 0, null, 0, 14, 8);
        tabSel.BorderColor = Palette.Accent;
        tabSel.BorderWidthBottom = 2;
        var tabIdle = Styles.Box(Colors.Transparent, 0, null, 0, 14, 8);
        tabIdle.BorderColor = Palette.Line;
        tabIdle.BorderWidthBottom = 1;
        var tabHover = Styles.Box(new Color(1, 1, 1, 0.04f), 0, null, 0, 14, 8);
        tabHover.BorderColor = Palette.AccentDim;
        tabHover.BorderWidthBottom = 1;
        foreach (var type in new[] { "TabContainer", "TabBar" })
        {
            t.SetStylebox("tab_selected", type, tabSel);
            t.SetStylebox("tab_unselected", type, tabIdle);
            t.SetStylebox("tab_hovered", type, tabHover);
            t.SetStylebox("tab_disabled", type, tabIdle);
            t.SetStylebox("tab_focus", type, new StyleBoxEmpty());
            t.SetColor("font_selected_color", type, Palette.AccentBright);
            t.SetColor("font_unselected_color", type, Palette.TextSecondary);
            t.SetColor("font_hovered_color", type, Palette.Text);
            t.SetColor("font_disabled_color", type, Palette.TextMuted);
            t.SetFont("font", type, Fonts.UiMedium);
        }
        t.SetStylebox("panel", "TabContainer", Styles.Box(Colors.Transparent, 0, null, 0, 0, 10));
        t.SetStylebox("tabbar_background", "TabContainer", new StyleBoxEmpty());

        // ---- popups and dropdowns
        var popup = Styles.Box(new Color("10121af8"), 8, Palette.LineStrong, 1, 6, 6, shadow: 16);
        t.SetStylebox("panel", "PopupMenu", popup);
        t.SetStylebox("hover", "PopupMenu", Styles.Box(Palette.Accent with { A = 0.16f }, 5, null, 0, 8, 4));
        t.SetColor("font_color", "PopupMenu", text);
        t.SetColor("font_hover_color", "PopupMenu", Palette.AccentBright);
        t.SetColor("font_disabled_color", "PopupMenu", Palette.TextMuted);
        t.SetColor("font_separator_color", "PopupMenu", Palette.AccentBright);
        t.SetFont("font_separator", "PopupMenu", Fonts.Display);
        t.SetStylebox("separator", "PopupMenu", new StyleBoxLine { Color = Palette.Line, GrowBegin = -6, GrowEnd = -6 });
        t.SetStylebox("labeled_separator_left", "PopupMenu", new StyleBoxLine { Color = Palette.Line });
        t.SetStylebox("labeled_separator_right", "PopupMenu", new StyleBoxLine { Color = Palette.Line });
        t.SetConstant("v_separation", "PopupMenu", 8);
        t.SetConstant("item_start_padding", "PopupMenu", 10);
        t.SetConstant("item_end_padding", "PopupMenu", 14);
        t.SetStylebox("panel", "PopupPanel", popup);
        t.SetStylebox("embedded_border", "Window", popup);
        t.SetStylebox("embedded_unfocused_border", "Window", popup);
        t.SetColor("title_color", "Window", Palette.AccentBright);
        t.SetFont("title_font", "Window", Fonts.Display);
        t.SetStylebox("panel", "AcceptDialog", Styles.Box(Palette.Bg1, 0, null, 0, 14));

        foreach (var type in new[] { "OptionButton", "MenuButton" })
        {
            ButtonStyles(t, type,
                Styles.Box(new Color(0, 0, 0, 0.35f), Palette.RadiusSmall, Palette.LineStrong, 1, 10, 7),
                Styles.Box(new Color(0, 0, 0, 0.45f), Palette.RadiusSmall, Palette.AccentDim, 1, 10, 7),
                Styles.Box(new Color(0, 0, 0, 0.45f), Palette.RadiusSmall, Palette.Accent, 1, 10, 7),
                Styles.Box(new Color(0, 0, 0, 0.2f), Palette.RadiusSmall, Palette.Line, 1, 10, 7),
                text, Palette.AccentBright);
        }
        t.SetIcon("arrow", "OptionButton", Svg(Chevron, 14, Palette.TextSecondary));

        // ---- tooltips
        t.SetStylebox("panel", "TooltipPanel", Styles.Tooltip());
        t.SetColor("font_color", "TooltipLabel", text);
        t.SetFontSize("font_size", "TooltipLabel", Palette.FontSmall);

        // ---- check boxes, toggles, sliders, progress
        foreach (var type in new[] { "CheckBox", "CheckButton" })
        {
            ButtonStyles(t, type, new StyleBoxEmpty(), new StyleBoxEmpty(), new StyleBoxEmpty(), new StyleBoxEmpty(), text, Palette.AccentBright);
            t.SetColor("font_pressed_color", type, text);
            t.SetConstant("h_separation", type, 10);
        }
        t.SetIcon("checked", "CheckBox", Svg(CheckOn, 20));
        t.SetIcon("unchecked", "CheckBox", Svg(CheckOff, 20));
        t.SetIcon("checked_disabled", "CheckBox", Svg(CheckOn, 20, new Color(1, 1, 1, 0.4f)));
        t.SetIcon("unchecked_disabled", "CheckBox", Svg(CheckOff, 20, new Color(1, 1, 1, 0.4f)));
        t.SetIcon("radio_checked", "CheckBox", Svg(RadioOn, 20));
        t.SetIcon("radio_unchecked", "CheckBox", Svg(RadioOff, 20));
        t.SetIcon("checked", "CheckButton", Svg(ToggleOn, 22));
        t.SetIcon("unchecked", "CheckButton", Svg(ToggleOff, 22));
        t.SetIcon("checked_disabled", "CheckButton", Svg(ToggleOn, 22, new Color(1, 1, 1, 0.4f)));
        t.SetIcon("unchecked_disabled", "CheckButton", Svg(ToggleOff, 22, new Color(1, 1, 1, 0.4f)));

        foreach (var type in new[] { "HSlider", "VSlider" })
        {
            t.SetStylebox("slider", type, Styles.Box(new Color(1, 1, 1, 0.1f), 3, null, 0, 0, 2));
            t.SetStylebox("grabber_area", type, Styles.Box(Palette.AccentDim, 3, null, 0, 0, 2));
            t.SetStylebox("grabber_area_highlight", type, Styles.Box(Palette.Accent, 3, null, 0, 0, 2));
            t.SetIcon("grabber", type, Svg(Knob, 18));
            t.SetIcon("grabber_highlight", type, Svg(KnobHover, 18));
            t.SetIcon("grabber_disabled", type, Svg(Knob, 18, new Color(1, 1, 1, 0.4f)));
        }
        t.SetStylebox("background", "ProgressBar", Styles.Box(new Color(0, 0, 0, 0.4f), 4, Palette.Line, 1, 0, 0));
        t.SetStylebox("fill", "ProgressBar", Styles.Box(Palette.Accent, 4, null, 0, 0, 0));
        t.SetColor("font_color", "ProgressBar", text);

        // ---- lists and trees
        var listBg = Styles.Inset(6);
        var selected = Styles.Box(Palette.Accent with { A = 0.18f }, 5, Palette.AccentDim, 1, 6, 4);
        var hovered = Styles.Box(new Color(1, 1, 1, 0.05f), 5, null, 0, 6, 4);
        t.SetStylebox("panel", "ItemList", listBg);
        t.SetStylebox("focus", "ItemList", new StyleBoxEmpty());
        t.SetStylebox("selected", "ItemList", selected);
        t.SetStylebox("selected_focus", "ItemList", selected);
        t.SetStylebox("hovered", "ItemList", hovered);
        t.SetStylebox("cursor", "ItemList", new StyleBoxEmpty());
        t.SetStylebox("cursor_unfocused", "ItemList", new StyleBoxEmpty());
        t.SetColor("font_color", "ItemList", text);
        t.SetColor("font_selected_color", "ItemList", Palette.AccentBright);
        t.SetColor("font_hovered_color", "ItemList", Palette.Text);
        t.SetConstant("v_separation", "ItemList", 6);
        t.SetConstant("h_separation", "ItemList", 8);
        t.SetStylebox("panel", "Tree", listBg);
        t.SetStylebox("focus", "Tree", new StyleBoxEmpty());
        t.SetStylebox("selected", "Tree", selected);
        t.SetStylebox("selected_focus", "Tree", selected);
        t.SetStylebox("hovered", "Tree", hovered);
        t.SetStylebox("cursor", "Tree", new StyleBoxEmpty());
        t.SetStylebox("cursor_unfocused", "Tree", new StyleBoxEmpty());
        t.SetColor("font_color", "Tree", text);
        t.SetColor("font_selected_color", "Tree", Palette.AccentBright);
        t.SetColor("guide_color", "Tree", Palette.Line);
        t.SetConstant("v_separation", "Tree", 6);
        t.SetStylebox("title_button_normal", "Tree", Styles.Box(Palette.Bg2, 0, null, 0, 6, 4));
        t.SetColor("title_button_color", "Tree", Palette.TextSecondary);

        // ---- spin boxes
        t.SetConstant("buttons_width", "SpinBox", 18);

        return t;
    }

    static void Variation(Theme t, string name, string baseType, Font font, int size, Color color)
    {
        t.SetTypeVariation(name, baseType);
        t.SetFont("font", name, font);
        t.SetFontSize("font_size", name, size);
        t.SetColor("font_color", name, color);
    }

    static void ButtonStyles(Theme t, string type, StyleBox normal, StyleBox hover, StyleBox pressed, StyleBox disabled, Color font, Color fontHover)
    {
        t.SetStylebox("normal", type, normal);
        t.SetStylebox("hover", type, hover);
        t.SetStylebox("pressed", type, pressed);
        t.SetStylebox("hover_pressed", type, pressed);
        t.SetStylebox("disabled", type, disabled);
        t.SetStylebox("focus", type, new StyleBoxEmpty());
        t.SetColor("font_color", type, font);
        t.SetColor("font_hover_color", type, fontHover);
        t.SetColor("font_pressed_color", type, fontHover);
        t.SetColor("font_hover_pressed_color", type, fontHover);
        t.SetColor("font_focus_color", type, font);
        t.SetColor("font_disabled_color", type, Palette.TextMuted);
        t.SetColor("icon_normal_color", type, font);
        t.SetColor("icon_hover_color", type, fontHover);
        t.SetColor("icon_pressed_color", type, fontHover);
        t.SetColor("icon_hover_pressed_color", type, fontHover);
        t.SetColor("icon_focus_color", type, font);
        t.SetColor("icon_disabled_color", type, Palette.TextMuted);
    }

    // Small control glyphs drawn as SVG so they stay crisp at any UI scale.
    static string A => Palette.Hex(Palette.Accent);
    static string L => Palette.Hex(Palette.TextSecondary);
    static string CheckOn => $"<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20'><rect x='1.5' y='1.5' width='17' height='17' rx='4' fill='{A}'/><path d='M5.5 10.5l3 3 6-7' stroke='#1a1408' stroke-width='2.2' fill='none' stroke-linecap='round' stroke-linejoin='round'/></svg>";
    static string CheckOff => $"<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20'><rect x='1.5' y='1.5' width='17' height='17' rx='4' fill='#00000055' stroke='{L}' stroke-opacity='0.6' stroke-width='1.4'/></svg>";
    static string RadioOn => $"<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20'><circle cx='10' cy='10' r='8.3' fill='none' stroke='{A}' stroke-width='1.6'/><circle cx='10' cy='10' r='4.5' fill='{A}'/></svg>";
    static string RadioOff => $"<svg xmlns='http://www.w3.org/2000/svg' width='20' height='20'><circle cx='10' cy='10' r='8.3' fill='#00000055' stroke='{L}' stroke-opacity='0.6' stroke-width='1.4'/></svg>";
    static string ToggleOn => $"<svg xmlns='http://www.w3.org/2000/svg' width='38' height='22'><rect x='1' y='2' width='36' height='18' rx='9' fill='{A}'/><circle cx='28' cy='11' r='6.5' fill='#1a1408'/></svg>";
    static string ToggleOff => $"<svg xmlns='http://www.w3.org/2000/svg' width='38' height='22'><rect x='1' y='2' width='36' height='18' rx='9' fill='#ffffff1a' stroke='{L}' stroke-opacity='0.5'/><circle cx='10' cy='11' r='6.5' fill='{L}'/></svg>";
    static string Knob => $"<svg xmlns='http://www.w3.org/2000/svg' width='18' height='18'><circle cx='9' cy='9' r='7' fill='#ece7dc' stroke='{A}' stroke-width='2'/></svg>";
    static string KnobHover => $"<svg xmlns='http://www.w3.org/2000/svg' width='18' height='18'><circle cx='9' cy='9' r='7.5' fill='{Palette.Hex(Palette.AccentBright)}' stroke='{A}' stroke-width='2'/></svg>";
    static string Chevron => "<svg xmlns='http://www.w3.org/2000/svg' width='14' height='14'><path d='M3 5l4 4 4-4' stroke='#ffffff' stroke-width='1.8' fill='none' stroke-linecap='round' stroke-linejoin='round'/></svg>";

    /// <summary>Rasterises an inline SVG at 2x (sharp at UI scales up to 200%), shown at its own size.</summary>
    public static Texture2D Svg(string svg, int size, Color? modulate = null)
    {
        var img = new Image();
        img.LoadSvgFromString(svg, 2f);
        if (modulate is { } m)
            for (int y = 0; y < img.GetHeight(); y++)
                for (int x = 0; x < img.GetWidth(); x++)
                    img.SetPixel(x, y, img.GetPixel(x, y) * m);
        var tex = ImageTexture.CreateFromImage(img);
        tex.SetSizeOverride(new Vector2I(img.GetWidth() / 2, img.GetHeight() / 2));
        return tex;
    }
}
