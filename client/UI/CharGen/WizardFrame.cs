using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI.CharGen;

/// <summary>
/// The shared layout of character creation and level-up: a step rail with progress on the left,
/// the step's title, content, validation message and Back / Recommended / Next actions in the
/// middle, and a side column (summary and the description of whatever is selected) on the right.
/// </summary>
public sealed class WizardFrame
{
    public StepRail Rail = null!;
    public Label Caption = null!, Progress = null!, StepTitle = null!, StepHint = null!, ErrorText = null!, DescTitle = null!;
    public Bar ProgressBar = null!;
    public VBoxContainer Content = null!, Side = null!, SummaryBox = null!;
    public PanelContainer ErrorPanel = null!, SummaryCard = null!, DescCard = null!;
    /// <summary>A 3D stage at the top of the summary card (hidden until a window puts a preview in it).</summary>
    public PanelContainer Stage = null!;
    public Button Cancel = null!, Back = null!, Next = null!, Recommend = null!;
    public RichTextLabel Desc = null!;
    public TextureRect DescIcon = null!;
    public Control DescIconFrame = null!;

    /// <summary>Builds the layout into a window body.</summary>
    public static WizardFrame Build(VBoxContainer body, string caption, bool summary)
    {
        var f = new WizardFrame();
        var main = Ui.HBox(0);
        main.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        body.AddChild(main);

        // Left: step rail.
        var railPanel = new PanelContainer { CustomMinimumSize = new Vector2(214, 0) };
        railPanel.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.22f), Palette.Radius, Palette.Line, 1, 14, 16));
        main.AddChild(railPanel);
        var railBox = Ui.VBox(8);
        railPanel.AddChild(railBox);
        f.Caption = Ui.Text(caption.ToUpperInvariant(), Ui.TextStyle.Caption, Palette.AccentDim.Lightened(0.3f));
        f.Caption.AddThemeFontOverride("font", Fonts.UiBold);
        railBox.AddChild(f.Caption);
        f.Progress = Ui.Text("", Ui.TextStyle.Small);
        railBox.AddChild(f.Progress);
        f.ProgressBar = new Bar { CustomMinimumSize = new Vector2(0, 4), ShowText = false, Radius = 2, Fill = Palette.Accent, FillTop = Palette.AccentBright, ShowTrail = false };
        railBox.AddChild(f.ProgressBar);
        railBox.AddChild(new Control { CustomMinimumSize = new Vector2(0, 6) });
        f.Rail = new StepRail();
        railBox.AddChild(f.Rail);
        main.AddChild(new Control { CustomMinimumSize = new Vector2(Palette.Space5, 0) });

        // Middle: title, content, validation, actions.
        var center = Ui.VBox(12);
        center.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        main.AddChild(center);
        var head = Ui.VBox(4);
        center.AddChild(head);
        f.StepTitle = Ui.Text("", Ui.TextStyle.Heading);
        head.AddChild(f.StepTitle);
        f.StepHint = Ui.Text("", Ui.TextStyle.Secondary);
        f.StepHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        f.StepHint.CustomMinimumSize = new Vector2(420, 0);
        head.AddChild(f.StepHint);
        head.AddChild(new Divider { Color = Palette.AccentDim, Fade = Divider.FadeMode.End, CustomMinimumSize = new Vector2(0, 1) });
        f.Content = Ui.VBox(10);
        f.Content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        center.AddChild(f.Content);

        f.ErrorPanel = new PanelContainer { Visible = false };
        f.ErrorPanel.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Danger with { A = 0.1f }, Palette.RadiusSmall, Palette.Danger with { A = 0.45f }, 1, 12, 8));
        var errRow = Ui.HBox(10);
        errRow.AddChild(Chip.Create("!", Palette.Danger, solid: true, fontSize: Palette.FontSmall));
        f.ErrorText = Ui.Text("", Ui.TextStyle.Small, Palette.Danger.Lightened(0.25f));
        f.ErrorText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        f.ErrorText.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        f.ErrorText.CustomMinimumSize = new Vector2(360, 0);
        errRow.AddChild(f.ErrorText);
        f.ErrorPanel.AddChild(errRow);
        center.AddChild(f.ErrorPanel);

        var nav = Ui.HBox(10);
        center.AddChild(nav);
        f.Cancel = Ui.Button("Cancel", null, "GhostButton");
        f.Cancel.CustomMinimumSize = new Vector2(96, 40);
        nav.AddChild(f.Cancel);
        nav.AddChild(Ui.Spacer());
        f.Recommend = Ui.Button("Recommended", null, "", "dice");
        f.Recommend.CustomMinimumSize = new Vector2(150, 40);
        f.Recommend.TooltipText = "Let your class package choose for you";
        nav.AddChild(f.Recommend);
        f.Back = Ui.Button("Back");
        f.Back.CustomMinimumSize = new Vector2(110, 40);
        nav.AddChild(f.Back);
        f.Next = Ui.Primary("Next");
        f.Next.CustomMinimumSize = new Vector2(160, 40);
        nav.AddChild(f.Next);
        main.AddChild(new Control { CustomMinimumSize = new Vector2(Palette.Space5, 0) });

        // Right: summary card and description.
        f.Side = Ui.VBox(12);
        f.Side.CustomMinimumSize = new Vector2(320, 0);
        main.AddChild(f.Side);
        f.SummaryCard = new PanelContainer { Visible = summary };
        f.SummaryCard.AddThemeStyleboxOverride("panel", Styles.Card(12));
        var summaryColumn = Ui.VBox(10);
        f.SummaryCard.AddChild(summaryColumn);
        f.Stage = new PanelContainer { Visible = false, ClipContents = true, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        f.Stage.AddThemeStyleboxOverride("panel", Styles.Box(Palette.Bg0, Palette.RadiusSmall, Palette.Line, 1, 0));
        summaryColumn.AddChild(f.Stage);
        f.SummaryBox = Ui.VBox(8);
        summaryColumn.AddChild(f.SummaryBox);
        f.Side.AddChild(f.SummaryCard);
        var descCard = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        descCard.AddThemeStyleboxOverride("panel", Styles.Card(16));
        f.DescCard = descCard;
        f.Side.AddChild(descCard);
        var descBox = Ui.VBox(10);
        descCard.AddChild(descBox);
        var titleRow = Ui.HBox(12);
        descBox.AddChild(titleRow);
        var iconFrame = new PanelContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        iconFrame.AddThemeStyleboxOverride("panel", Styles.Box(new Color(0, 0, 0, 0.45f), Palette.RadiusSmall, Palette.AccentDim, 1, 2));
        f.DescIcon = new TextureRect { CustomMinimumSize = new Vector2(44, 44), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        iconFrame.AddChild(f.DescIcon);
        f.DescIconFrame = iconFrame;
        titleRow.AddChild(iconFrame);
        f.DescTitle = Ui.Text("", Ui.TextStyle.Title);
        f.DescTitle.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        f.DescTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        f.DescTitle.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        f.DescTitle.CustomMinimumSize = new Vector2(200, 0);
        titleRow.AddChild(f.DescTitle);
        descBox.AddChild(new Divider { Color = Palette.AccentDim, Fade = Divider.FadeMode.End, CustomMinimumSize = new Vector2(0, 1) });
        f.Desc = new RichTextLabel { SizeFlagsVertical = Control.SizeFlags.ExpandFill, ScrollActive = true, FitContent = false, BbcodeEnabled = false };
        f.Desc.AddThemeFontOverride("normal_font", Fonts.Reading);
        f.Desc.AddThemeFontSizeOverride("normal_font_size", 17);
        f.Desc.AddThemeColorOverride("default_color", Palette.Text);
        f.Desc.AddThemeConstantOverride("line_separation", 2);
        descBox.AddChild(f.Desc);
        return f;
    }

    public void SetDescription(string title, string text, Texture2D? icon)
    {
        DescTitle.Text = NwnText.Strip(title);
        Desc.Text = NwnText.Strip(text);
        Desc.ScrollToLine(0);
        DescIcon.Texture = icon;
        DescIconFrame.Visible = icon != null;
    }

    /// <summary>
    /// Sizes the stage: a fixed height above the description, or (expanded) the whole side column
    /// with the description hidden, for steps where the preview is the point (appearance).
    /// </summary>
    public void SetStage(float height, bool expanded)
    {
        Stage.Visible = true;
        Stage.CustomMinimumSize = new Vector2(0, expanded ? 0 : height);
        Stage.SizeFlagsVertical = expanded ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill;
        SummaryCard.SizeFlagsVertical = expanded ? Control.SizeFlags.ExpandFill : Control.SizeFlags.Fill;
        DescCard.Visible = !expanded;
    }

    public void ShowErrors(IEnumerable<string> errors)
    {
        var list = errors.Where(e => e.Length > 0).Distinct().Take(5).ToList();
        ErrorText.Text = string.Join("\n", list);
        ErrorPanel.Visible = list.Count > 0;
    }

    public void SetSteps(IReadOnlyList<StepRail.Entry> entries, int currentIndex, string title, string hint)
    {
        Rail.Set(entries);
        Progress.Text = $"Step {currentIndex + 1} of {entries.Count}";
        ProgressBar.Set(currentIndex + 1, entries.Count);
        StepTitle.Text = title;
        StepHint.Text = hint;
        StepHint.Visible = hint.Length > 0;
    }

    /// <summary>A grid of cards that fills the content width (scrolls when long).</summary>
    public static (ScrollContainer Scroll, GridContainer Grid) CardGrid(int columns, int gap = 10)
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var grid = new GridContainer { Columns = columns, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", gap);
        grid.AddThemeConstantOverride("v_separation", gap);
        scroll.AddChild(Ui.Pad(grid, 2, 2, 12, 2));
        ((MarginContainer)grid.GetParent()).SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        return (scroll, grid);
    }
}
