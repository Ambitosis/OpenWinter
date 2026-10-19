using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// A window of the classic interface: a <see cref="GuiPanel"/> placed where the original puts it (most windows sit
/// against a screen corner at 1x scale), toggled by its key, closed by its XButton or Esc (most recent first).
/// Subclasses build <see cref="Panel"/> in <see cref="Build"/> and refresh in <see cref="Refresh"/> when opened.
/// </summary>
public abstract partial class ClassicWindow : Control
{
    static readonly List<ClassicWindow> Stack = new();
    // Native order: first left, second right, third middle. Slots survive z-order changes.
    static readonly Control?[] PanelSlots = new Control?[3];
    public bool ArrangeWithPanels;

    static void ArrangeOpen(Control window)
    {
        if (window is ClassicWindow { ArrangeWithPanels: false } || Array.IndexOf(PanelSlots, window) >= 0) return;
        for (int i = 0; i < PanelSlots.Length; i++)
            if (PanelSlots[i] is { } old && (!IsInstanceValid(old) || !old.Visible)) PanelSlots[i] = null;
        int slot = Array.IndexOf(PanelSlots, null);
        if (slot < 0)
        {
            if (Stack.FirstOrDefault(w => w != window && Array.IndexOf(PanelSlots, w) >= 0) is { } displaced) displaced.Close();
            // A live conversation is never the one evicted: the oldest window goes (storeconvopanels: with the
            // conversation, inventory and character sheet open, the journal closed the inventory).
            else if (PanelSlots[2] is ClassicWindow middle) middle.Close();
            else return;
            slot = Array.IndexOf(PanelSlots, null);
        }
        PanelSlots[slot] = window;
        PlacePanels();
    }

    /// <summary>
    /// OpenStore clears the left slot. A live conversation there isn't closed: it moves to the middle and the
    /// store takes the left, inventory the right (storeconvopanels).
    /// </summary>
    public static void CloseLeftPanel()
    {
        if (PanelSlots[0] is ClassicWindow left) left.Close();
        else if (PanelSlots[0] is { } conversation && PanelSlots[2] == null && IsInstanceValid(conversation) && conversation.Visible)
        {
            PanelSlots[2] = conversation;
            PanelSlots[0] = null;
            PlacePanels();
        }
    }

    static void ArrangeClosed(Control window)
    {
        int slot = Array.IndexOf(PanelSlots, window);
        if (slot < 0) return;
        PanelSlots[slot] = null;
        if (slot < 2 && PanelSlots[2] is { } middle)
        {
            PanelSlots[slot] = middle;
            PanelSlots[2] = null;
        }
        else if (slot == 0 && PanelSlots[1] is { } right)
        {
            PanelSlots[0] = right;
            PanelSlots[1] = null;
        }
        PlacePanels();
    }

    // Native isolated controls: Inventory and Character close, Journal survives.
    // Snapshot the types before any close can promote a different panel into that slot.
    public static void OpenConversation(ClassicConversation conversation)
    {
        var dismiss = Stack.Where(w => w is ClassicInventoryWindow or Sheets.ClassicCharacterSheet).ToArray();
        foreach (var window in dismiss) window.Close();
        ArrangeOpen(conversation);
    }

    public static void CloseConversation(ClassicConversation conversation) => ArrangeClosed(conversation);

    public static Vector2 PanelPosition(Control panel, float width)
    {
        int slot = Array.IndexOf(PanelSlots, panel);
        float right = Math.Max(0, panel.GetViewportRect().Size.X - 80 - width);
        return new Vector2(slot == 1 ? right : slot == 2 ? right / 2 : 0, 0).Floor();
    }

    static void PlacePanels()
    {
        foreach (var window in PanelSlots)
            if (window != null && IsInstanceValid(window) && window.IsInsideTree())
            {
                if (window is ClassicWindow classic) classic.Place();
                else window.Position = PanelPosition(window, 360);
            }
    }

    /// <summary>Closes the most recently opened closable classic window; false when none is open.</summary>
    public static bool CloseTop()
    {
        for (int i = Stack.Count - 1; i >= 0; i--)
        {
            var w = Stack[i];
            if (!IsInstanceValid(w) || !w.Visible) { Stack.RemoveAt(i); continue; }
            if (!w.Closable) continue;
            w.Close();
            return true;
        }
        return false;
    }

    public static IReadOnlyList<ClassicWindow> OpenWindows => Stack;

    /// <summary>Drops every window from the Esc order (the classic interface was removed).</summary>
    public static void Forget() { Stack.Clear(); Array.Clear(PanelSlots); }

    public ClassicContext Ctx = null!;
    public ClassicAssets Assets => Ctx.Assets;
    public GuiPanel Panel = null!;
    public bool Closable = true;
    /// <summary>Where the window sits: a corner or edge of the screen (Godot layout preset) plus an offset in pixels.</summary>
    public LayoutPreset Anchor = LayoutPreset.TopLeft;
    public Vector2 Offset;
    public event Action? Opened, Closed;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        Panel = Build();
        AddChild(Panel);
        if (Panel.Get<GuiButton>("XButton") is { } x) x.Pressed += Close;
        Size = Panel.Size;
        GetViewport().SizeChanged += Place;
        TreeExiting += () => { GetViewport().SizeChanged -= Place; Stack.Remove(this); ArrangeClosed(this); };
        Place();
    }

    /// <summary>The window's panel (built once).</summary>
    protected abstract GuiPanel Build();

    /// <summary>Called on opening and whenever the window should show new data.</summary>
    public virtual void Refresh() { }

    public void Place()
    {
        if (!IsInsideTree()) return;
        if (!ArrangeWithPanels) ArrangeClosed(this);
        else if (Visible) ArrangeOpen(this);
        var view = GetViewportRect().Size;
        var size = Panel?.Size ?? Size;
        float x = Anchor switch
        {
            LayoutPreset.TopRight or LayoutPreset.BottomRight or LayoutPreset.CenterRight or LayoutPreset.RightWide => view.X - size.X,
            LayoutPreset.Center or LayoutPreset.CenterTop or LayoutPreset.CenterBottom => (view.X - size.X) / 2,
            _ => 0,
        };
        float y = Anchor switch
        {
            LayoutPreset.BottomLeft or LayoutPreset.BottomRight or LayoutPreset.CenterBottom or LayoutPreset.BottomWide => view.Y - size.Y,
            LayoutPreset.Center or LayoutPreset.CenterLeft or LayoutPreset.CenterRight => (view.Y - size.Y) / 2,
            _ => 0,
        };
        int slot = Array.IndexOf(PanelSlots, this);
        if (slot >= 0)
        {
            Position = PanelPosition(this, this is Sheets.ClassicMiniMap ? 360 : size.X)
                + (this is Sheets.ClassicMiniMap ? Offset : Vector2.Zero);
        }
        else Position = (new Vector2(x, y) + Offset).Floor();
        Size = size;
    }

    public bool IsOpen => Visible;

    public void Toggle()
    {
        if (Visible) Close();
        else Open();
    }

    public virtual void Open()
    {
        if (Panel == null) return;
        if (ArrangeWithPanels && this is not Sheets.ClassicMiniMap)
            foreach (var map in Stack.OfType<Sheets.ClassicMiniMap>().ToArray()) map.Close();
        // Like the inventory, the map closes Memorize and leaves the spell book (storeconvopanels).
        if (this is Sheets.ClassicMiniMap) ClassicSheets.Memorize?.Close();
        Refresh();
        Visible = true;
        Stack.Remove(this);
        Stack.Add(this);
        GetParent()?.MoveChild(this, -1);
        ArrangeOpen(this);
        Place();
        Opened?.Invoke();
    }

    public virtual void Close()
    {
        if (!Visible) return;
        Visible = false;
        Stack.Remove(this);
        ArrangeClosed(this);
        Closed?.Invoke();
    }
}
