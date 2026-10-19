using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.DM;

/// <summary>
/// The DM creator: the module's custom and the game's standard blueprint palettes by kind
/// (creatures, items, placeables, encounters, waypoints, triggers, sounds, stores), with search.
/// Picking a blueprint and pressing Place (or double-clicking) enters placement mode: the next
/// click on the ground creates it there; an item clicked onto a creature goes into its inventory.
/// </summary>
public partial class DmCreatorWindow : UiTheme.Window
{
    public ClientSession Session = null!;
    public Interaction Interaction = null!;
    /// <summary>The blueprint currently picked (kind, resref, name), also used by "Give selected item".</summary>
    public (string Kind, string ResRef, string Name)? Selected { get; private set; }

    static readonly (string Key, string Title)[] Kinds =
    {
        ("creature", "Creatures"), ("item", "Items"), ("placeable", "Placeables"), ("encounter", "Encounters"),
        ("waypoint", "Waypoints"), ("trigger", "Triggers"), ("sound", "Sounds"), ("store", "Stores"),
    };

    readonly Dictionary<string, JsonElement> _palettes = new();
    OptionButton _kind = null!;
    LineEdit _search = null!;
    Tree _tree = null!;
    Label _status = null!;
    Button _place = null!;

    public static DmCreatorWindow Create(ClientSession session, Interaction interaction)
    {
        var w = Create<DmCreatorWindow>("Creator", new Vector2(400, 600));
        w.Session = session;
        w.Interaction = interaction;
        w.AnchorLeft = w.AnchorRight = 1;
        w.OffsetLeft = -420; w.OffsetRight = -20; w.OffsetTop = 70;
        return w;
    }

    public override void _Ready()
    {
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 6);
        Body.AddChild(top);
        _kind = new OptionButton { FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(130, 0) };
        foreach (var (_, title) in Kinds) _kind.AddItem(title);
        _kind.ItemSelected += _ => Load();
        top.AddChild(_kind);
        _search = new LineEdit { PlaceholderText = "Search name or resref", SizeFlagsHorizontal = SizeFlags.ExpandFill, ClearButtonEnabled = true };
        _search.TextChanged += _ => Rebuild();
        _search.FocusEntered += () => ChatInputGuard(true);
        _search.FocusExited += () => ChatInputGuard(false);
        top.AddChild(_search);

        _tree = new Tree { SizeFlagsVertical = SizeFlags.ExpandFill, HideRoot = true, SelectMode = Tree.SelectModeEnum.Single };
        _tree.AddThemeStyleboxOverride("panel", UiTheme.Frame(UiTheme.Slot, 4));
        _tree.AddThemeColorOverride("font_color", UiTheme.Text);
        _tree.AddThemeFontSizeOverride("font_size", 14);
        _tree.ItemSelected += OnSelected;
        _tree.ItemActivated += () => { OnSelected(); Place(); };
        Body.AddChild(_tree);

        var bottom = new HBoxContainer();
        bottom.AddThemeConstantOverride("separation", 8);
        Body.AddChild(bottom);
        _status = UiTheme.Label("", 13, UiTheme.Muted);
        _status.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _status.ClipText = true;
        bottom.AddChild(_status);
        _place = new Button { Text = "Place", Disabled = true, CustomMinimumSize = new Vector2(90, 0) };
        _place.Pressed += Place;
        bottom.AddChild(_place);
        Session.NotifyReceived += OnNotify;
    }

    static void ChatInputGuard(bool typing) => ChatInput.Typing = typing;

    public void Open(string kind = "creature")
    {
        Visible = true;
        int i = Array.FindIndex(Kinds, k => k.Key == kind);
        if (i >= 0 && _kind.Selected != i) _kind.Select(i);
        Load();
    }

    public void Search(string text)
    {
        _search.Text = text;
        Rebuild();
    }

    string Kind => Kinds[Math.Max(0, _kind.Selected)].Key;

    void Load()
    {
        if (_palettes.ContainsKey(Kind)) { Rebuild(); return; }
        _tree.Clear();
        _status.Text = "Loading palette...";
        Session.Request("dm-palette", new { kind = Kind });
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind != "dm-palette") return;
        var root = JsonDocument.Parse(m.Json).RootElement;
        _palettes[root.GetProperty("kind").GetString() ?? ""] = root.GetProperty("tree").Clone();
        if (root.GetProperty("kind").GetString() == Kind) Rebuild();
    }

    /// <summary>Fills the tree; with a search term, only matching blueprints (and their categories), expanded.</summary>
    void Rebuild()
    {
        _tree.Clear();
        if (!_palettes.TryGetValue(Kind, out var tree)) return;
        string q = _search.Text.Trim();
        var root = _tree.CreateItem();
        int shown = 0;
        foreach (var node in tree.EnumerateArray()) shown += Add(root, node, q, depth: 0);
        _status.Text = shown == 0 ? "Nothing matches." : $"{shown} blueprint{(shown == 1 ? "" : "s")}";
    }

    int Add(TreeItem parent, JsonElement node, string q, int depth)
    {
        string name = node.GetProperty("n").GetString() ?? "";
        if (node.TryGetProperty("r", out var r))
        {
            string resref = r.GetString() ?? "";
            if (q.Length > 0 && !name.Contains(q, StringComparison.OrdinalIgnoreCase) && !resref.Contains(q, StringComparison.OrdinalIgnoreCase)) return 0;
            var item = _tree.CreateItem(parent);
            float cr = node.TryGetProperty("cr", out var c) ? c.GetSingle() : -1;
            item.SetText(0, cr >= 0 ? $"{name}   (CR {cr:0.##})" : name);
            item.SetTooltipText(0, resref);
            item.SetMetadata(0, resref + "|" + name);
            return 1;
        }
        var cat = _tree.CreateItem(parent);
        cat.SetText(0, name);
        cat.SetCustomColor(0, UiTheme.Title);
        cat.SetSelectable(0, false);
        int count = 0;
        foreach (var child in node.GetProperty("c").EnumerateArray()) count += Add(cat, child, q, depth + 1);
        if (count == 0) { parent.RemoveChild(cat); cat.Free(); return 0; }
        cat.Collapsed = q.Length == 0 && depth > 0;
        return count;
    }

    void OnSelected()
    {
        var item = _tree.GetSelected();
        if (item == null || item.GetMetadata(0).VariantType != Variant.Type.String) return;
        var parts = item.GetMetadata(0).AsString().Split('|', 2);
        Selected = (Kind, parts[0], parts.Length > 1 ? parts[1] : parts[0]);
        _place.Disabled = false;
        _status.Text = $"{Selected.Value.Name} ({parts[0]})";
    }

    /// <summary>Selects the first visible blueprint (captures).</summary>
    public void SelectFirst()
    {
        var item = _tree.GetRoot();
        while (item != null && item.GetMetadata(0).VariantType != Variant.Type.String)
        {
            item.Collapsed = false;
            item = item.GetFirstChild();
        }
        if (item == null) return;
        item.Select(0);
        _tree.ScrollToItem(item);
        OnSelected();
    }

    /// <summary>Placement mode: the next click places the selected blueprint.</summary>
    public void Place()
    {
        if (Selected is not { } sel) return;
        _status.Text = $"Click to place {sel.Name} (right-click cancels)";
        // The original's create cursor (MOUSECURSOR_CREATE) while placing.
        HudParts.Cursors.TargetCursor = HudParts.Cursors.BadTargetCursor = 47;
        Interaction.Targeting = (target, ground) =>
        {
            Interaction.Targeting = null;
            _status.Text = $"{sel.Name} ({sel.ResRef})";
            if (sel.Kind == "item" && target is { Kind: ObjectKind.Creature or ObjectKind.Placeable })
                Session.Request("dm-spawn", new { kind = sel.Kind, resref = sel.ResRef, into = target.ObjectId });
            else if (ground is { } g)
                SpawnAt(g);
        };
    }

    /// <summary>Creates the selected blueprint at a point (area coordinates).</summary>
    public void SpawnAt(System.Numerics.Vector3 g)
    {
        if (Selected is not { } sel) return;
        Session.Request("dm-spawn", new { kind = sel.Kind, resref = sel.ResRef, pos = new[] { g.X, g.Y, g.Z }, facing = 0f });
    }
}
