using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// The original's in-game HUD: quickbar (bottom centre), chat window and the full-width "Talk:" line (bottom
/// left), party bar and portrait (top right), compass (bottom right), action queue (top left) and the radial menu
/// in the original's look. The modern pieces they replace are hidden; floating combat text and speech stay.
/// </summary>
public static partial class ClassicHud
{
    /// <summary>The classic HUD of the running game (null in modern mode).</summary>
    public static ClassicHudRoot? Current { get; private set; }

    static partial void AttachParts(ClassicContext ctx)
    {
        var root = new ClassicHudRoot { Ctx = ctx, Name = "ClassicHud" };
        ctx.Hud.AddChild(root);
        // Below the game windows (they were added after the chat) so windows open over the HUD, as in the original.
        ctx.Hud.MoveChild(root, ctx.Chat.GetIndex());
        Current = root;
        root.TreeExiting += () => { if (Current == root) Current = null; };
    }

    static partial void DetachParts(ClassicContext ctx)
    {
        ctx.Main.GetChildren().OfType<PlayerFx>().FirstOrDefault()?.SetEffectsHost(ctx.Hud.Player.Effects);
        foreach (var radial in ctx.Hud.GetChildren().OfType<RadialMenu>()) { radial.Skin = null; radial.QueueRedraw(); }
        if (Current is { } root && GodotObject.IsInstanceValid(root)) root.QueueFree();
        Current = null;
    }
}

/// <summary>
/// Holds the classic HUD pieces (full screen, clicks pass through where there is nothing) and keeps the modern
/// pieces they replace hidden (the modern HUD shows its pieces again on area changes and after conversations).
/// </summary>
public partial class ClassicHudRoot : Control
{
    public ClassicContext Ctx = null!;
    public ClassicQuickbar Quickbar = null!;
    public ClassicChat Chat = null!;
    public ClassicTalkLine Talk = null!;
    public ClassicPartyBar PartyBar = null!;
    public ClassicCompass Compass = null!;
    public ClassicActionQueue Queue = null!;
    public ClassicRadialSkin Radial = null!;
    Control[] _hidden = [];
    ClassicTooltip _hover = null!;
    Control? _hoverOwner;
    double _hoverFor;
    Vector2 _pointer;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var hud = Ctx.Hud;
        _hidden = [hud.Player, hud.Queue, hud.Party, hud.Target, hud.Toasts, hud.AreaTitle, hud.TopRight, hud.ActionBarSlot, hud.Menu, Ctx.Chat, Ctx.ChatInput];
        // The modern chat entry stops listening for its key (the talk line takes it); the action bar keeps running
        // hidden: it loads and saves the slots and takes the F-keys, and the quickbar draws its slots.
        Ctx.ChatInput.ProcessMode = ProcessModeEnum.Disabled;
        Ctx.ActionBar.User.Feedback = Ctx.Feedback;

        Chat = new ClassicChat { Ctx = Ctx, Name = "Chat" };
        AddChild(Chat);
        Compass = new ClassicCompass { Ctx = Ctx, Name = "Compass" };
        AddChild(Compass);
        PartyBar = new ClassicPartyBar { Ctx = Ctx, Name = "PartyBar" };
        AddChild(PartyBar);
        Queue = new ClassicActionQueue { Ctx = Ctx, Name = "ActionQueue" };
        AddChild(Queue);
        Quickbar = new ClassicQuickbar { Ctx = Ctx, Name = "Quickbar" };
        AddChild(Quickbar);
        // Last: the chat mode list opens over the chat window.
        Talk = new ClassicTalkLine { Ctx = Ctx, Name = "TalkLine" };
        AddChild(Talk);
        Radial = ClassicRadialSkin.Install(Ctx);
        _hover = ClassicTooltip.Create(Ctx.Assets);
        // Over the game windows and popups too (the inventory's items, the map's pins), below the pointer.
        var hoverLayer = new CanvasLayer { Layer = 12, Name = "ClassicTooltip" };
        AddChild(hoverLayer);
        hoverLayer.AddChild(_hover);
        GetViewport().SizeChanged += Layout;
        Layout();
        Hide_();
    }

    /// <summary>Places the pieces against the screen edges at the original's 1x scale.</summary>
    public void Layout()
    {
        var view = GetViewportRect().Size;
        Talk.Place(view);
        Quickbar.Place(view);
        Chat.Place(view, Talk.Top);
        Compass.Place(view, Talk.Top);
        PartyBar.Place(view);
        Queue.Place(view);
    }

    void Hide_()
    {
        foreach (var c in _hidden)
            if (IsInstanceValid(c) && c.Visible) c.Visible = false;
    }

    public override void _Process(double delta)
    {
        Hide_();
        // Before there is a world (character selection and creation) the HUD stays hidden, as the modern one.
        bool inWorld = Ctx.Session.Controlled != null && Ctx.Session.Area != null;
        if (Visible != inWorld) Visible = inWorld;
        UpdateHover(delta);
    }

    /// <summary>The pointer as the last mouse event put it (posted capture events included).</summary>
    public override void _Input(InputEvent e)
    {
        if (e is InputEventMouse m) _pointer = m.Position;
    }

    /// <summary>The hover text being shown (null when none): for captures and checks.</summary>
    public string[]? HoverShown => _hover.Visible ? _hover.Lines : null;
    /// <summary>Where the hover box is and the pointer it was placed for (captures).</summary>
    public (Rect2 Box, Vector2 Pointer)? HoverPlacement => _hover.Visible ? (_hover.GetRect(), _pointer) : null;

    /// <summary>
    /// Hover text in the original's look for the control under the pointer: at once over items and portraits (as
    /// the original), after a short wait over other controls; it follows the pointer.
    /// </summary>
    void UpdateHover(double delta)
    {
        var vp = GetViewport();
        if (!Visible) { _hoverOwner = null; _hover.Visible = false; return; }
        var found = vp.GuiIsDragging() ? null : ClassicTooltip.LinesFor(vp.GuiGetHoveredControl());
        if (found is not { } f || !f.Owner.IsVisibleInTree()) { _hoverOwner = null; _hover.Visible = false; return; }
        if (f.Owner != _hoverOwner) { _hoverOwner = f.Owner; _hoverFor = 0; _hover.Visible = false; }
        _hoverFor += delta;
        if (!f.Instant && _hoverFor < ClassicTooltip.Delay) return;
        _hover.SetLines(f.Lines);
        // Lines that come out empty (a name of colour tokens only) show no box at all rather than a blank one.
        if (_hover.Lines.Length == 0) { _hover.Visible = false; return; }
        _hover.PlaceAt(_pointer, GetViewportRect().Size);
        _hover.Visible = true;
    }
}
