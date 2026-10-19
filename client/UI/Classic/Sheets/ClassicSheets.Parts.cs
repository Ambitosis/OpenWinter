using OpenWinter.Client.UI.Classic.Sheets;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// Classic character record, spellbook (known spells and memorise), journal and map. The modern windows they
/// replace stay hidden (they keep listening, and the journal's data is shared) and the C, B, J and M keys and the
/// HUD's buttons open the classic ones.
/// </summary>
public static partial class ClassicSheets
{
    public static ClassicCharacterSheet? Character { get; private set; }
    public static ClassicSpellbookWindow? Spellbook { get; private set; }
    public static ClassicMemorizeWindow? Memorize { get; private set; }
    public static ClassicJournal? Journal { get; private set; }
    public static ClassicMiniMap? MiniMap { get; private set; }
    public static ClassicAreaMap? AreaMap { get; private set; }
    public static ClassicExamine? Examine { get; private set; }

    static partial void AttachParts(ClassicContext ctx)
    {
        var hud = ctx.Hud;
        Character = Add(hud, new ClassicCharacterSheet { ArrangeWithPanels = true, Ctx = ctx, Name = "ClassicCharacter", LevelUpRequested = () => ctx.OpenLevelUp() });

        var book = new SpellbookModel(ctx);
        Spellbook = Add(hud, new ClassicSpellbookWindow { ArrangeWithPanels = true, Ctx = ctx, Name = "ClassicSpellbook", Model = book });
        // The memorise panel sits against the party bar (80 px wide at the top-right).
        Memorize = Add(hud, new ClassicMemorizeWindow { ArrangeWithPanels = true, Ctx = ctx, Name = "ClassicMemorize", Model = book, Anchor = Control.LayoutPreset.TopRight, Offset = new Vector2(-80, 0), Book = Spellbook });
        Spellbook.Memorize = Memorize;

        Journal = Add(hud, new ClassicJournal { ArrangeWithPanels = true, Ctx = ctx, Name = "ClassicJournal" });

        MiniMap = Add(hud, new ClassicMiniMap { ArrangeWithPanels = true, Ctx = ctx, Name = "ClassicMiniMap" });
        AreaMap = Add(hud, new ClassicAreaMap { Ctx = ctx, Name = "ClassicAreaMap" });
        MiniMap.Enlarge = () => { MiniMap.Close(); AreaMap.Open(); };
        AreaMap.Shrink = () => { AreaMap.Close(); MiniMap.Open(); };

        // Creatures, placeables and doors are examined in the original's window (items stay with the inventory).
        Examine = Add(hud, new ClassicExamine { Ctx = ctx, Name = "ClassicExamine" });

        // The modern windows stay closed: whatever opens one (a key, a HUD button, the minimap) gets the classic one.
        // The lambdas read the current windows, so they keep working after a switch to modern and back.
        Redirect(ctx.Character, () => Character?.Toggle());
        Redirect(ctx.Spellbook, () => Spellbook?.Toggle());
        Redirect(ctx.Journal, () => Journal?.Toggle());
        Redirect(ctx.Map, ToggleMap);
        Model = book;

        ctx.Input.ToggleCharacter = Character.Toggle;
        ctx.Input.ToggleSpellbook = Spellbook.Toggle;
        ctx.Input.ToggleJournal = Journal.Toggle;
        ctx.Input.ToggleMap = ToggleMap;
        hud.OpenCharacter = Character.Toggle;
        hud.OpenSpellbook = Spellbook.Toggle;
        hud.OpenJournal = Journal.Toggle;
        hud.OpenMap = ToggleMap;
    }

    /// <summary>M: closes whichever map is open, else opens the small one.</summary>
    public static void ToggleMap()
    {
        if (AreaMap?.IsOpen == true) { AreaMap.Close(); return; }
        MiniMap?.Toggle();
    }

    static SpellbookModel? Model;
    static readonly HashSet<Control> Hooked = new();

    static partial void DetachParts(ClassicContext ctx)
    {
        foreach (var w in new Node?[] { Character, Spellbook, Memorize, Journal, MiniMap, AreaMap, Examine })
            if (w != null && GodotObject.IsInstanceValid(w)) w.QueueFree();
        Model?.Dispose();
        Model = null;
        Character = null; Spellbook = null; Memorize = null; Journal = null; MiniMap = null; AreaMap = null; Examine = null;
    }

    static void Redirect(Control modern, Action open)
    {
        modern.Visible = false;
        // Once per modern window (attaching again after a switch must not open the classic window twice).
        if (!Hooked.Add(modern)) return;
        modern.VisibilityChanged += () =>
        {
            if (!modern.Visible || ClassicGame.Current == null) return;
            modern.Visible = false;
            open();
        };
    }

    static T Add<T>(Node hud, T window) where T : ClassicWindow
    {
        hud.AddChild(window);
        return window;
    }
}
