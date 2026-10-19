using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.UI.Classic;

/// <summary>
/// Everything the classic interface works with in a game: the session and game services (unchanged from the
/// modern interface: the server, the input, casting, interaction), the modern pieces it replaces (to hide them
/// and borrow their logic) and the HUD layer it lives on. Built by Main once the game's interface exists.
/// </summary>
public sealed class ClassicContext
{
    public required Main Main;
    public required ClientSession Session;
    public required OrbitCamera Camera;
    public required PlayerInput Input;
    public required Interaction Interaction;
    public required SpellCasting Casting;
    public required HudParts.HudState State;
    /// <summary>The HUD layer: classic pieces are added here (cutscene mode and the hide-GUI key hide it).</summary>
    public required Hud Hud;
    public required ChatLog Chat;
    public required DM.ChatInput ChatInput;
    public required DialogPanel Dialog;
    public required InventoryPanel Inventory;
    public required StorePanel Store;
    public required ContainerPanel Loot;
    public required CharacterPanel Character;
    public required SpellbookPanel Spellbook;
    public required JournalPanel Journal;
    public required MapPanel Map;
    public required ExaminePanel Examine;
    public required ActionBar ActionBar;
    public required PlayerListPanel Players;
    public required CharGen.LevelUpWindow LevelUp;
    public required FrontEnd.GameMenu? GameMenu;

    /// <summary>The game's art (the module's haks included), once the content is open; null before.</summary>
    public ClassicAssets Assets => Session.Content is { } c ? ClassicAssets.For(c) : _baseAssets ??= ClassicAssets.For(FrontEnd.FrontEndContent.Base!);
    ClassicAssets? _baseAssets;

    /// <summary>The level-up window: the classic one (client/UI/Classic/Creation), else the modern one.</summary>
    public void OpenLevelUp() => Creation.ClassicCreation.OpenLevelUp(LevelUp.Open);

    /// <summary>Feedback line in the chat (as the modern interface's).</summary>
    public void Feedback(string text) => Chat.Feedback(text);
}

/// <summary>
/// Switches a game to the classic interface: each part hides the modern pieces it replaces and adds its own to
/// the HUD, then takes over their keys. Parts that are not built yet leave the modern piece in place.
/// </summary>
public static class ClassicGame
{
    /// <summary>The game's context while the classic interface is in use; null in modern mode.</summary>
    public static ClassicContext? Current { get; private set; }
    static ModernWiring? _modern;
    static bool _attached;

    public static void Attach(ClassicContext ctx)
    {
        if (Current != null) return;
        Current = ctx;
        _attached = false;
        WindowStack.CloseClassic = ClassicWindow.CloseTop;
        // Parts are attached once the game's content is open (the art comes from it, haks included).
        void Go()
        {
            if (Current != ctx || _attached) return;
            _attached = true;
            ctx.Session.ModuleCleared += ReloadContent;
            _modern = ModernWiring.Capture(ctx);
            ClassicHud.Attach(ctx);
            ClassicInventory.Attach(ctx);
            ClassicSheets.Attach(ctx);
            ClassicDialogs.Attach(ctx);
            ClassicRest.Attach(ctx);
            ClassicPlayerList.Attach(ctx);
        }
        if (ctx.Session.Content != null) Go();
        else
        {
            void Once()
            {
                if (ctx.Session.Content == null) return;
                ctx.Session.AreaLoaded -= Once;
                Go();
            }
            ctx.Session.AreaLoaded += Once;
        }
    }

    static void ReloadContent()
    {
        if (Current is not { } ctx) return;
        Remove();
        Attach(ctx);
    }

    /// <summary>
    /// Switches a running game back to the modern interface: closes and removes every classic piece and gives the
    /// modern pieces back their keys, buttons and visibility (Options → Interface style, changed in play).
    /// </summary>
    public static void Remove()
    {
        if (Current is not { } ctx) return;
        ctx.Session.ModuleCleared -= ReloadContent;
        if (_attached)
        {
            ClassicPlayerList.Detach(ctx);
            ClassicRest.Detach();
            ClassicDialogs.Detach(ctx);
            ClassicSheets.Detach(ctx);
            ClassicInventory.Detach(ctx);
            ClassicHud.Detach(ctx);
            ClassicWindow.Forget();
            _modern?.Restore(ctx);
        }
        _modern = null;
        _attached = false;
        Current = null;
        WindowStack.CloseClassic = null;
    }

    /// <summary>Forgets the game (leaving it for the main menu reloads the whole scene; nothing to restore).</summary>
    public static void Detach()
    {
        if (Current is { } ctx) { ctx.Session.ModuleCleared -= ReloadContent; ClassicPlayerList.Detach(ctx); }
        ClassicRest.Detach();
        Current = null;
        _modern = null;
        _attached = false;
        WindowStack.CloseClassic = null;
    }

    /// <summary>What the classic parts take over from the modern interface, as it was before they did.</summary>
    sealed class ModernWiring
    {
        Action? _inv, _char, _journal, _map, _book, _menu, _hudChar, _hudInv, _hudBook, _hudJournal, _hudMap, _hudMenu;
        Action<int>? _cycle;
        Action<string>? _feedback;
        Action<bool, bool, string>? _death;
        PlayerFx? _fx;
        Node.ProcessModeEnum _chatInput;
        readonly List<(CanvasItem Item, bool Visible)> _shown = new();

        public static ModernWiring Capture(ClassicContext c)
        {
            var w = new ModernWiring
            {
                _inv = c.Input.ToggleInventory, _char = c.Input.ToggleCharacter, _journal = c.Input.ToggleJournal, _map = c.Input.ToggleMap,
                _book = c.Input.ToggleSpellbook, _menu = c.Input.OpenGameMenu, _cycle = c.Input.CycleInventory,
                _hudChar = c.Hud.OpenCharacter, _hudInv = c.Hud.OpenInventory, _hudBook = c.Hud.OpenSpellbook, _hudJournal = c.Hud.OpenJournal,
                _hudMap = c.Hud.OpenMap, _hudMenu = c.Hud.OpenMenu, _feedback = c.ActionBar.User.Feedback, _chatInput = c.ChatInput.ProcessMode,
                _fx = c.Main.GetChildren().OfType<PlayerFx>().FirstOrDefault(),
            };
            w._death = w._fx?.DeathPanel;
            var hud = c.Hud;
            foreach (CanvasItem item in new CanvasItem[] { hud.Player, hud.Queue, hud.Party, hud.Target, hud.Toasts, hud.AreaTitle })
                w._shown.Add((item, item.Visible));
            foreach (var child in c.Dialog.GetChildren().OfType<CanvasItem>()) w._shown.Add((child, child.Visible));
            return w;
        }

        public void Restore(ClassicContext c)
        {
            c.Input.ToggleInventory = _inv; c.Input.ToggleCharacter = _char; c.Input.ToggleJournal = _journal; c.Input.ToggleMap = _map;
            c.Input.ToggleSpellbook = _book; c.Input.OpenGameMenu = _menu; c.Input.CycleInventory = _cycle;
            c.Hud.OpenCharacter = _hudChar; c.Hud.OpenInventory = _hudInv; c.Hud.OpenSpellbook = _hudBook; c.Hud.OpenJournal = _hudJournal;
            c.Hud.OpenMap = _hudMap; c.Hud.OpenMenu = _hudMenu;
            c.ActionBar.User.Feedback = _feedback;
            c.ChatInput.ProcessMode = _chatInput;
            if (_fx != null && GodotObject.IsInstanceValid(_fx)) _fx.DeathPanel = _death;
            foreach (var (item, visible) in _shown)
                if (GodotObject.IsInstanceValid(item)) item.Visible = visible;
            // The modern HUD decides again which of its world pieces show (minimap, action bar, menu buttons, chat).
            c.Hud.RefreshWorldPieces();
            c.State.Refresh();
        }
    }
}

/// <summary>The in-game HUD: quickbar, party bar and portrait, compass, chat and talk line, action queue, radial menu.</summary>
public static partial class ClassicHud
{
    public static void Attach(ClassicContext ctx) => AttachParts(ctx);
    static partial void AttachParts(ClassicContext ctx);
    /// <summary>Removes the part's pieces and lets go of what it took over (switching back to modern in play).</summary>
    public static void Detach(ClassicContext ctx) => DetachParts(ctx);
    static partial void DetachParts(ClassicContext ctx);
}

/// <summary>Inventory with equipment, containers, stores, barter and item examine.</summary>
public static partial class ClassicInventory
{
    public static void Attach(ClassicContext ctx) => AttachParts(ctx);
    static partial void AttachParts(ClassicContext ctx);
    /// <summary>Removes the part's pieces and lets go of what it took over (switching back to modern in play).</summary>
    public static void Detach(ClassicContext ctx) => DetachParts(ctx);
    static partial void DetachParts(ClassicContext ctx);
}

/// <summary>Character record, spellbook (known and memorize), journal, area map and creature examine.</summary>
public static partial class ClassicSheets
{
    public static void Attach(ClassicContext ctx) => AttachParts(ctx);
    static partial void AttachParts(ClassicContext ctx);
    /// <summary>Removes the part's pieces and lets go of what it took over (switching back to modern in play).</summary>
    public static void Detach(ClassicContext ctx) => DetachParts(ctx);
    static partial void DetachParts(ClassicContext ctx);
}

/// <summary>Conversations, the in-game options menu, popups (OK / OK-cancel), death options and the player list.</summary>
public static partial class ClassicDialogs
{
    public static void Attach(ClassicContext ctx) => AttachParts(ctx);
    static partial void AttachParts(ClassicContext ctx);
    /// <summary>Removes the part's pieces and lets go of what it took over (switching back to modern in play).</summary>
    public static void Detach(ClassicContext ctx) => DetachParts(ctx);
    static partial void DetachParts(ClassicContext ctx);
}
