using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI.HudParts;
using OpenWinter.Client.UI.Kit;
using Godot;

namespace OpenWinter.Client.UI;

/// <summary>
/// The in-game heads-up display. Layout (anchored, scales with the window):
/// top-left the player and party frames; top-centre the target frame with notifications under it;
/// top-right area name, clock and minimap; bottom-left chat; bottom-centre the action bar;
/// bottom-right the menu buttons. Floating combat text and the area title sit over the world.
/// Game windows are added as children too (they float above the HUD pieces).
/// </summary>
public partial class Hud : CanvasLayer
{
    public ClientSession Session = null!;
    public Camera3D Camera = null!;
    public Interaction Interaction = null!;
    public HudState State = null!;
    /// <summary>Menu actions (set by Main before the HUD enters the tree).</summary>
    public Action? OpenCharacter, OpenInventory, OpenSpellbook, OpenJournal, OpenMap, Rest, OpenMenu, OpenLevelUp;

    public PlayerFrame Player = null!;
    public PartyFrames Party = null!;
    public ActionQueuePanel Queue = null!;
    public TargetFrame Target = null!;
    public Toasts Toasts = null!;
    public AreaTitle AreaTitle = null!;
    public HudMenu Menu = null!;
    /// <summary>Top-right column: the area/clock pill, then the minimap when one is added.</summary>
    public VBoxContainer TopRight = null!;
    public QuestTracker Tracker = null!;
    /// <summary>Bottom-centre holder for the action bar.</summary>
    public CenterContainer ActionBarSlot = null!;
    Label _area = null!, _clock = null!, _fps = null!;
    TextureRect _dayIcon = null!;
    public static bool ShowFps;

    public override void _Ready()
    {
        // Before there's a world to show (character selection and creation), the front end's smoky backdrop
        // stands in for it and the in-game pieces stay hidden.
        _backdrop = new FrontEnd.Atmosphere { AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore, LeftShade = 0, Embers = 0.6f };
        AddChild(_backdrop);
        var combat = new CombatText { Session = Session, Camera = Camera };
        AddChild(combat);

        AreaTitle = new AreaTitle { AnchorLeft = 0, AnchorRight = 1, AnchorTop = 0.2f, AnchorBottom = 0.2f, OffsetBottom = 90 };
        AddChild(AreaTitle);
        Session.AreaLoaded += () => AreaTitle.Show(NwnText.Strip(Session.Area?.Info.Name ?? "").Trim(), NwnText.Strip(Session.ModuleName));

        var left = Ui.VBox(10);
        left.Position = new Vector2(16, 16);
        left.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(left);
        Player = new PlayerFrame { Session = Session, State = State, OpenCharacter = () => OpenCharacter?.Invoke(), OpenLevelUp = () => OpenLevelUp?.Invoke() };
        left.AddChild(Player);
        Player.TargetPortrait = id => Interaction?.TargetPortrait(id) == true;
        Queue = new ActionQueuePanel { Session = Session, State = State };
        left.AddChild(Queue);
        Party = new PartyFrames { Session = Session, State = State };
        Party.TargetPortrait = id => Interaction?.TargetPortrait(id) == true;
        // Right-clicking a party portrait opens that creature's radial, as the original's (associatesubmenus p_*).
        Party.OpenRadial = (id, at) => { if (Session.Objects.TryGetValue(id, out var view)) Interaction?.ShowMenu(view, at); };
        left.AddChild(Party);

        var top = Ui.VBox(10);
        top.AnchorLeft = top.AnchorRight = 0.5f;
        top.OffsetLeft = -200; top.OffsetRight = 200; top.OffsetTop = 16;
        top.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(top);
        Target = new TargetFrame { Session = Session, State = State, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        top.AddChild(Target);
        Toasts = new Toasts();
        top.AddChild(Toasts);

        TopRight = Ui.VBox(8);
        TopRight.AnchorLeft = TopRight.AnchorRight = 1;
        TopRight.OffsetLeft = -236; TopRight.OffsetRight = -16; TopRight.OffsetTop = 16;
        TopRight.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(TopRight);
        var pill = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        Ui.Glass(pill, Styles.Hud(8, Palette.Radius));
        TopRight.AddChild(pill);
        var pillCol = Ui.VBox(2);
        pill.AddChild(pillCol);
        _area = Ui.Text("", Ui.TextStyle.Title, Palette.Text, 14);
        _area.HorizontalAlignment = HorizontalAlignment.Center;
        _area.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        pillCol.AddChild(_area);
        var clockRow = Ui.HBox(6);
        clockRow.Alignment = BoxContainer.AlignmentMode.Center;
        pillCol.AddChild(clockRow);
        _dayIcon = Icons.Rect("sun", 14, Palette.AccentBright);
        clockRow.AddChild(_dayIcon);
        _clock = Ui.Text("", Ui.TextStyle.Caption, Palette.TextSecondary, 12);
        clockRow.AddChild(_clock);
        _fps = Ui.Text("", Ui.TextStyle.Caption, Palette.TextMuted, 11);
        _fps.HorizontalAlignment = HorizontalAlignment.Right;
        TopRight.AddChild(_fps);
        Tracker = new QuestTracker { Session = Session, OpenJournal = () => OpenJournal?.Invoke() };
        TopRight.AddChild(Tracker);

        ActionBarSlot = new CenterContainer { AnchorLeft = 0.5f, AnchorRight = 0.5f, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = -400, OffsetRight = 400, OffsetTop = -84, OffsetBottom = -12 };
        ActionBarSlot.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(ActionBarSlot);

        Menu = new HudMenu { AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, GrowHorizontal = Control.GrowDirection.Begin, GrowVertical = Control.GrowDirection.Begin, OffsetRight = -16, OffsetBottom = -16 };
        Menu.Entries.AddRange([
            ("character", "Character", "C", () => OpenCharacter?.Invoke()),
            ("inventory", "Inventory", "I", () => OpenInventory?.Invoke()),
            ("spellbook", "Spellbook", "B", () => OpenSpellbook?.Invoke()),
            ("journal", "Journal", "J", () => OpenJournal?.Invoke()),
            ("map", "Map", "M", () => OpenMap?.Invoke()),
            ("rest", "Rest", "R", () => Rest?.Invoke()),
            ("menu", "Game menu", "Esc", () => OpenMenu?.Invoke()),
        ]);
        AddChild(Menu);

        State.LevelUpAvailable += () =>
        {
            Toasts.Show("New level available", "Open your character sheet (C) or click your level to level up.", "levelup", Palette.AccentBright, 6);
            Menu.Attention("Character", true);
        };
        State.Changed += () => Menu.Attention("Character", State.CanLevelUp);
    }

    /// <summary>Puts the minimap at the top of the right-hand column; it shows the area name itself, so the panel below keeps only the clock.</summary>
    public void AttachMinimap(Control minimap)
    {
        TopRight.AddChild(minimap);
        TopRight.MoveChild(minimap, 0);
        _area.Visible = false;
    }

    /// <summary>Clicking an object in the world selects it for the target frame.</summary>
    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && Interaction?.Hovered is { } picked && picked.ObjectId != Session.ControlledId
            && Interaction.HoveredSelectable)
            State.Select(picked.ObjectId);
    }

    /// <summary>
    /// Keeps the bottom row from overlapping on narrow screens: the chat narrows into the space left of
    /// the action bar, and when the chat or the menu buttons can't fit beside the bar they move up a row.
    /// </summary>
    void LayoutBottomRow()
    {
        var view = GetViewport().GetVisibleRect().Size;
        if (view == _laidOutFor) return;
        _laidOutFor = view;
        float bar = 0;
        foreach (var c in ActionBarSlot.GetChildren()) if (c is Control ctl && ctl.Visible) bar = Math.Max(bar, ctl.GetCombinedMinimumSize().X);
        if (bar <= 0) bar = 740;
        float side = (view.X - bar) / 2 - 16 - 12;       // free width either side of the bar
        float raise = ActionBarSlot.OffsetBottom - ActionBarSlot.OffsetTop + 12;
        // Menu buttons: beside the bar if they fit, otherwise stacked above its right end.
        float menuW = Menu.GetCombinedMinimumSize().X;
        Menu.OffsetBottom = menuW <= side ? -16 : -16 - raise;
        // Chat: as wide as the space beside the bar allows (up to 460), or full size above the bar.
        float chatW = Math.Min(460, side);
        bool above = chatW < 300;
        if (above) chatW = Math.Min(460, view.X * 0.36f);
        float lift = above ? raise : 0;
        foreach (var c in GetChildren())
        {
            if (c is ChatLog log) { log.OffsetRight = 16 + chatW; log.OffsetBottom = -56 - lift; log.OffsetTop = -300 - lift; }
            else if (c is DM.ChatInput input) { input.OffsetRight = 16 + chatW; input.OffsetBottom = -16 - lift; input.OffsetTop = -50 - lift; }
        }
    }

    Vector2 _laidOutFor;
    Control _backdrop = null!;
    bool? _inWorld;

    /// <summary>Decides again which world pieces show (after the classic interface hid them).</summary>
    public void RefreshWorldPieces() => _inWorld = null;

    void ShowWorldPieces()
    {
        bool inWorld = Session.Controlled != null && Session.Area != null;
        if (inWorld == _inWorld) return;
        _inWorld = inWorld;
        _backdrop.Visible = !inWorld;
        TopRight.Visible = ActionBarSlot.Visible = Menu.Visible = inWorld;
        foreach (var c in GetChildren())
            if (c is ChatLog or DM.ChatInput) ((Control)c).Visible = inWorld;
    }

    public override void _Process(double delta)
    {
        LayoutBottomRow();
        ShowWorldPieces();
        _area.Text = NwnText.Strip(Session.Area?.Info.Name ?? "");
        if (Session.Area is { } area)
        {
            int minutes = (int)area.ClockMinutes;
            _clock.Text = $"{minutes / 60:00}:{minutes % 60:00}";
            bool day = minutes >= 6 * 60 && minutes < 18 * 60;
            _dayIcon.Texture = Icons.Get(day ? "sun" : "moon", 32);
            _dayIcon.SelfModulate = day ? Palette.AccentBright : Palette.Info;
        }
        _fps.Visible = ShowFps;
        if (ShowFps) _fps.Text = $"{Engine.GetFramesPerSecond()} FPS";
    }
}
