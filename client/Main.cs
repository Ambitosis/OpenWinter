using System.Diagnostics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.UI;
using OpenWinter.Client.UI.Nui;
using OpenWinter.Content;
using OpenWinter.Game;
using OpenWinter.Net.Nwn.Bridge;
using OpenWinter.Net.Nwn.Session;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

/// <summary>
/// Entry point. Single-player: hosts the server in-process over a loopback
/// connection and runs the client against it.
/// Command line (after --): --area resref, --appearance row, --capture dir,
/// --look x,y,z,yaw,pitch,dist (screenshot from a fixed viewpoint).
/// </summary>
public partial class Main : Node3D
{
    GameServer? _server;
    ClientSession _session = null!;
    OrbitCamera _camera = null!;
    PlayerInput _input = null!;
    public InventoryPanel Inventory = null!;
    public StorePanel Store = null!;
    public CharacterPanel Character = null!;
    public NuiManager Nui = null!;
    public JournalPanel Journal = null!;
    public MapPanel Map = null!;
    Minimap? _minimap;
    public ExaminePanel Examine = null!;
    Interaction _interaction = null!;
    public SpellCasting Casting = null!;
    public SpellbookPanel Spellbook = null!;
    public ActionBar ActionBar = null!;
    UI.DM.DmUi _dm = null!;
    UI.HudParts.HudState? _hudState;
    ChatLog? _chatLog;
    double _accumulator;
    bool _attached;
    readonly Dictionary<string, string> _args = new();

    public override void _Ready()
    {
        var a = OS.GetCmdlineUserArgs();
        for (int i = 0; i < a.Length; i++)
            if (a[i].StartsWith("--")) _args[a[i][2..]] = i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[++i] : "true";

        if (_args.TryGetValue("verify-module-content", out var moduleProbeDir))
        {
            async void Verify() => GetTree().Quit(await ModuleContentProbe.Run(this, moduleProbeDir));
            Verify();
            return;
        }

        if (_args.TryGetValue("verify-tile-occlusion", out var tileProbeDir))
        {
            async void Verify() => GetTree().Quit(await Views.TileOcclusionProbe.Run(this, tileProbeDir));
            Verify();
            return;
        }

        if (_args.TryGetValue("verify-doors", out var doorProbeDir))
        {
            async void Verify() => GetTree().Quit(await Views.DoorPresentationProbe.Run(this, doorProbeDir));
            Verify();
            return;
        }

        if (_args.TryGetValue("verify-hilite-occlusion", out var hiliteProbeDir))
        {
            async void Verify() => GetTree().Quit(await Rendering.HiliteOcclusionProbe.Run(this, hiliteProbeDir));
            Verify();
            return;
        }

        if (_args.TryGetValue("verify-shield-attachment", out var shieldProbeDir))
        {
            GetTree().Quit(Views.ShieldAttachmentProbe.Run(shieldProbeDir));
            return;
        }

        if (_args.TryGetValue("verify-trap-detection", out var trapProbeDir))
        {
            GetTree().Quit(Views.WorldFx.TrapDetectionProbe.Run(trapProbeDir));
            return;
        }

        if (_args.TryGetValue("verify-dm-radial", out var dmProbeDir))
        {
            GetTree().Quit(UI.RadialMenu.VerifyDmDirections(dmProbeDir));
            return;
        }

        if (_args.TryGetValue("verify-disappear-visibility", out var disappearProbeDir))
        {
            GetTree().Quit(Views.DisappearVisibilityProbe.Run(disappearProbeDir));
            return;
        }

        if (_args.TryGetValue("verify-potion-targeting", out var potionProbeDir))
        {
            GetTree().Quit(UI.RadialMenu.VerifyPotionTargeting(potionProbeDir));
            return;
        }

        // --fx-verbose: log every visual effect and world effect message (diagnosing effects in captures).
        if (_args.ContainsKey("fx-verbose")) Views.WorldFx.WorldFxPlayer.Verbose = true;
        // --visual-placeable-light "range,attenuation,energy": placeable light experiments.
        if (_args.TryGetValue("visual-placeable-light", out var pl))
        {
            var v = pl.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            (Views.StaticObjectView.PlaceableLightRange, Views.StaticObjectView.PlaceableLightAttenuation, Views.StaticObjectView.PlaceableLightEnergy) = (v[0], v[1], v[2]);
        }
        // --visual-particle-gamma "blended,additive,blended alpha": the powers applied to emitter colours, textures and opacity.
        if (_args.TryGetValue("visual-particle-gamma", out var pg))
        {
            var v = pg.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Rendering.Emitters.AuroraEmitter.ColorGamma = v[0];
            if (v.Length > 1) Rendering.Emitters.AuroraEmitter.AdditiveGamma = v[1];
            if (v.Length > 2) Rendering.Emitters.AuroraEmitter.BlendedAlphaGamma = v[2];
        }
        // --visual-creature-light "height,range scale,attenuation,energy[,shadow opacity]": torch light experiments (Views.CreatureLight).
        if (_args.TryGetValue("visual-creature-light", out var cl))
        {
            var v = cl.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            (Views.CreatureLight.Height, Views.CreatureLight.RangeScale, Views.CreatureLight.Attenuation, Views.CreatureLight.Energy) = (v[0], v[1], v[2], v[3]);
            if (v.Length > 4) Views.CreatureLight.ShadowOpacity = v[4];
        }
        if (_args.TryGetValue("visual-static-light", out var sl))
        {
            var v = sl.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            (Views.StaticObjectView.StaticLightRange, Views.StaticObjectView.StaticLightAttenuation, Views.StaticObjectView.StaticLightEnergy) = (v[0], v[1], v[2]);
        }
        UI.Kit.GameTheme.Install(GetTree());
        UI.HudParts.Cursors.InstallPointer();
        // A player's install has no config/local.json: find the game, and keep OpenWinter's writes in its own user folder.
        var cfg = LocalConfig.Find(ProjectSettings.GlobalizePath("res://")) ?? LocalConfig.Detect(System.IO.Path.Combine(OS.GetUserDataDir(), "userdata"));
        if (cfg == null)
        {
            OS.Alert("Neverwinter Nights: Enhanced Edition was not found.\n\nInstall it from Steam, GOG or Beamdog, or create " +
                "config/local.json next to OpenWinter with its location:\n{ \"gameRoot\": \"C:/Path/To/Neverwinter Nights\" }", "OpenWinter");
            GetTree().Quit(1);
            return;
        }
        _cfg = cfg;
        _paths = new GamePaths(_cfg.GameRoot, _cfg.UserRoot, _cfg.Language);
        OpenWinter.Game.Persistence.PersistenceSystem.SandboxRoot = _cfg.WriteRoot;
        InitFrontEnd(); // settings, loading screen, overlays (Main.FrontEnd.cs)
        UI.FrontEnd.FrontEndContent.Begin(_paths);
        // The original cursors come from the game data, which opens in the background.
        UI.FrontEnd.FrontEndContent.BaseAsync.ContinueWith(_ => Callable.From(UI.HudParts.Cursors.InstallPointer).CallDeferred());

        // Command-line play (development, captures) starts straight away; otherwise the main menu decides.
        if (_args.Keys.Any(k => k is "module" or "connect" or "connect-nwn" or "character" or "area" or "bench" or "look" or "dm" or "load" or "playtest" || k.StartsWith("capture") && k is not "capture-menu" and not "capture-classic" and not "capture-menu-switch"))
            StartGame();
        else if (_args.TryGetValue("capture-classic", out var classicDir))
            _ = UI.Classic.ClassicCapture.Run(this, classicDir, _args.GetValueOrDefault("classic-only"), _args.GetValueOrDefault("classic-models"),
                async () => await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame), () => GetTree().Quit());
        else ShowMainMenu();
    }

    LocalConfig _cfg = null!;
    GamePaths _paths = null!;

    /// <summary>Starts a game from the current arguments: --connect host[:port] to join a server, otherwise --module (default: the configured one) in single player.</summary>
    void StartGame()
    {
        var cfg = _cfg;
        var paths = _paths;
        var sw = Stopwatch.StartNew();
        IConnection client;
        if (_args.TryGetValue("connect", out var endpoint))
        {
            var parts = endpoint.Split(':');
            client = new NetClientConnection(parts[0], parts.Length > 1 ? int.Parse(parts[1]) : 5121);
            GD.Print($"[client] connecting to {endpoint}");
        }
        else if (_args.TryGetValue("connect-nwn", out var nwnServer))
        {
            // An NWN:EE server running WSNetLayer: ws://host:port, or wss:// behind a TLS proxy.
            var keyArg = _args.GetValueOrDefault("nwn-key", System.IO.Path.Combine(cfg.UserRoot, "cdkey.ini"));
            var key = keyArg == "generate" ? NwnCdKey.Generate() : NwnCdKey.FromIni(keyArg);
            client = new NwnBridgeConnection(new NwnBridgeOptions(new Uri(nwnServer), key, _args.GetValueOrDefault("password", ""))
            {
                RecordTo = _args.GetValueOrDefault("nwn-record"), // every frame, for diagnostics and replay tests
                Log = GD.Print,
            }, new NwnResManData(paths));
            GD.Print($"[client] connecting to NWN server {nwnServer} with CD key {key.PublicKey}");
        }
        else
        {
            var rm = ResMan.ForModule(paths, _args.GetValueOrDefault("module", cfg.Module));
            if (ScriptDir() is { } scriptDir) rm.AddFirst(new DirectorySource(scriptDir));
            if (_args.ContainsKey("capture-fx")) Views.WorldFx.FxCapture.AddTestScripts(rm);
            var world = new World(rm);
            // Single player's difficulty is the player's option (--difficulty for captures and the visual harness).
            world.GameDifficulty = int.TryParse(_args.GetValueOrDefault("difficulty"), out var difficulty) ? Math.Clamp(difficulty, 1, 4) : Settings.Current.Gameplay.Difficulty;
            if (!_args.ContainsKey("difficulty"))
            {
                void Difficulty(Preferences.GameSettings s) { if (_server != null) _server.World.GameDifficulty = s.Gameplay.Difficulty; }
                Settings.Changed += Difficulty;
                TreeExiting += () => Settings.Changed -= Difficulty;
            }
            world.EnableScripts();
            world.Scripts!.Log = line => { GD.Print(line); VisualLog(line); };
            world.LoadModule();
            _server = new GameServer(world, new ServerOptions
            {
                StartArea = _args.GetValueOrDefault("area"), // the module's entry point unless --area
                DefaultAppearance = int.TryParse(_args.GetValueOrDefault("appearance"), out var app) ? app : -1,
                DefaultCharacter = _args.GetValueOrDefault("character"),
                DevCommands = true,
                // Single player plays from the local vault; --character skips the selection screen.
                CharacterSelection = true,
                Vault = VaultMode.Local,
                DMPassword = _args.GetValueOrDefault("dm-password", ""), // local games take DMs (--dm)
                SaveGames = true,
                ServeContent = true, // Module presentation resources use the same verified transfer as remote games.
                ModulePath = _args.GetValueOrDefault("module", cfg.Module),
                LoadOnLogin = _args.GetValueOrDefault("load"), // Load Game / Continue from the main menu
            });
            GD.Print($"[server] module loaded: {world.Areas.Count} areas in {sw.ElapsedMilliseconds} ms");
            if (int.TryParse(_args.GetValueOrDefault("hour"), out var hour)) world.Clock.Set(hour, 0, 0, 0);
            var (loop, serverEnd) = LoopbackConnection.CreatePair();
            _server.Accept(serverEnd);
            client = loop;
        }
        _session = new ClientSession { Name = "Session", Connection = client, Paths = paths };
        AddChild(_session);
        _camera = new OrbitCamera { Current = true };
        AddChild(_camera);
        AddChild(new Rendering.HiliteGlow { Camera = _camera });
        AddChild(new Rendering.Shaders.PostProcess { Session = _session, Camera = _camera });
        _input = new PlayerInput { Session = _session, Camera = _camera };
        AddChild(_input);
        var hudState = _hudState = new UI.HudParts.HudState(_session);
        var hud = new Hud { Session = _session, Camera = _camera, State = hudState };
        AddChild(hud);
        var chat = _chatLog = new ChatLog { Session = _session };
        hud.AddChild(chat);
        _session.ChatReceived += chat.Add;
        // Content download progress shows on the loading screen.
        var dialog = new DialogPanel { Reply = choice => _session.Send(new DialogReplyMessage { Choice = choice }.Write()), Session = _session, Camera = _camera };
        // Conversations get their own layer above the HUD and above script fades (PlayerFx, layer 5): cutscene mode
        // hides the HUD, and cutscene and narrator lines are often spoken over a black screen.
        var dialogLayer = new CanvasLayer { Layer = 6, Name = "Dialog" };
        AddChild(dialogLayer);
        dialogLayer.AddChild(dialog);
        _session.DialogReceived += dialog.ShowStep;
        // The conversation takes the bottom of the screen: the chat steps aside while it's open.
        dialog.VisibilityChanged += () => chat.Visible = !dialog.Visible;
        Inventory = InventoryPanel.Create(_session);
        hud.AddChild(Inventory);
        Store = StorePanel.Create(_session, Inventory);
        hud.AddChild(Store);
        _input.ToggleInventory = Inventory.Toggle;
        Character = CharacterPanel.Create(_session);
        hud.AddChild(Character);
        _input.ToggleCharacter = Character.Toggle;
        Nui = new NuiManager { Session = _session, JuiDirectory = ScriptDir() };
        hud.AddChild(Nui);
        Journal = JournalPanel.Create(_session);
        // The original prints the journal line in yellow (255, 255, 1 in scene journalpc).
        Journal.Updated += text => chat.Feedback(text, "#ffff00");
        Journal.AttentionChanged += () => hud.Menu?.Attention("Journal", Journal.Attention);
        hud.AddChild(Journal);
        _input.ToggleJournal = Journal.Toggle;
        Map = MapPanel.Create(_session);
        hud.AddChild(Map);
        _input.ToggleMap = Map.Toggle;
        // ui-world: the minimap, top-right (the lead places it in the HUD).
        _minimap = new Minimap { Session = _session, OpenMap = Map.Toggle, SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
        hud.AttachMinimap(_minimap);
        Examine = ExaminePanel.Create(_session);
        hud.AddChild(Examine);
        Inventory.Examine = Examine.Examine;
        Store.Examine = Examine.Examine;
        var loot = ContainerPanel.Create(_session, Inventory);
        loot.Examine = Examine.Examine;
        hud.AddChild(loot);
        var barter = BarterPanel.Create(_session, Inventory);
        barter.Examine = Examine.Examine;
        hud.AddChild(barter);
        var hover = new UI.HudParts.ObjectLabel();
        hover.Visible = false;
        hud.AddChild(hover);
        _interaction = new Interaction { Session = _session, Camera = _camera, HoverLabel = hover, ShowPlayerNames = _server == null, Examine = Examine.Examine };
        AddChild(_interaction);
        hud.Interaction = _interaction;
        Inventory.PickGround = _interaction.PickGround;
        _interaction.CarryGroundItem = Inventory.CarryGroundItem;
        // Over-head speech has its own layer: cutscene mode hides the HUD but not what creatures say.
        var speechLayer = new CanvasLayer { Layer = 2, Name = "Speech" };
        AddChild(speechLayer);
        var speech = new UI.HudParts.SpeechText { Session = _session, Camera = _camera, Interaction = _interaction };
        speechLayer.AddChild(speech);
        _session.SpeechReceived += speech.Say;
        AddChild(new UI.HudParts.Cursors { Session = _session, Interaction = _interaction, State = hudState });
        _session.Space.AddChild(new CommandReticle { Session = _session });
        var effects = new Views.EffectPlayer { Session = _session };
        AddChild(effects);
        _session.NotifyReceived += effects.Handle;
        Casting = new SpellCasting { Session = _session, Interaction = _interaction };
        Spellbook = SpellbookPanel.Create(_session, Casting);
        hud.AddChild(Spellbook);
        // Action bar, radial (right-click) menu and abilities window (K).
        ActionBar = new ActionBar { Session = _session, Casting = Casting };
        hud.ActionBarSlot.AddChild(ActionBar);
        Spellbook.AddToQuickbar = ActionBar.Add;
        Inventory.Radial = new RadialMenu { Session = _session, Casting = Casting, Examine = Examine.Examine };
        hud.AddChild(Inventory.Radial);
        _session.ControlledChanged += _ =>
        {
            _interaction.Targeting = null;
            Inventory.Radial.Close();
            GetViewport().GuiCancelDrag();
        };
        hud.AddChild(AbilitiesPanel.Create(_session, Casting));
        _input.ToggleSpellbook = Spellbook.Toggle;
        var players = PlayerListPanel.Create(_session);
        hud.AddChild(players);
        _input.OpenPlayerList = players.Toggle;
        var quickChat = new QuickChatMenu { Session = _session };
        hud.AddChild(quickChat);
        _input.OpenQuickChat = quickChat.Toggle;
        _input.KeyboardMenuOpen = () => quickChat.CapturingKeys;
        _input.CycleInventory = Inventory.Cycle;
        _input.ToggleGui = () => hud.Visible = !hud.Visible;
        _input.Feedback = text => chat.Feedback(text);
        _input.SinglePlayer = _server != null;
        _input.HudState = hudState;
        _input.Interaction = _interaction;
        var spellFx = new Views.SpellFx { Session = _session, Effects = effects };
        AddChild(spellFx);
        _session.NotifyReceived += spellFx.Handle;
        AddChild(new PlayerFx { Session = _session, Camera = _camera, Hud = hud, Dialog = dialog, Chat = chat, Input_ = _input, Interaction = _interaction, EffectsHost = hud.Player.Effects });
        AddChild(new GuiPanels { Session = _session, Input_ = _input }); // SetGuiPanelDisabled, PopUpGUIPanel, GUI events
        var saves = SaveLoadPanel.Create(_session);
        saves.Input_ = _input;
        saves.Feedback = text => chat.Feedback(text);
        hud.AddChild(saves);
        var select = CharacterSelectPanel.Create(_session, System.IO.Path.Combine(cfg.UserRoot, "localvault"));
        // New characters are saved to the sandboxed user folder in development runs.
        if (cfg.WriteRoot != null) select.ExtraVaults.Add(System.IO.Path.Combine(cfg.WriteRoot, "localvault"));
        hud.AddChild(select);
        var chargen = UI.CharGen.CharGenWindow.Create(_session);
        hud.AddChild(chargen);
        select.NewCharacterRequested = chargen.Open;
        chargen.Cancelled = () => { if (_session.ControlledId == 0) select.Reopen(); };
        // The classic interface's creation and level-up (client/UI/Classic/Creation) take over in classic mode.
        UI.Classic.Creation.ClassicCreation.Wire(hud, _session, select, chargen, _server != null);
        var levelUp = UI.CharGen.LevelUpWindow.Create(_session);
        hud.AddChild(levelUp);
        Character.LevelUpRequested = () => UI.Classic.Creation.ClassicCreation.OpenLevelUp(levelUp.Open);
        hud.OpenCharacter = Character.Toggle;
        hud.OpenInventory = Inventory.Toggle;
        hud.OpenSpellbook = Spellbook.Toggle;
        hud.OpenJournal = Journal.Toggle;
        hud.OpenMap = Map.Toggle;
        hud.Rest = () => _session.Request("rest");
        hud.OpenLevelUp = () => UI.Classic.Creation.ClassicCreation.OpenLevelUp(levelUp.Open);
        hud.OpenMenu = () => GameMenu?.Toggle();
        if (_args.TryGetValue("capture-select", out var selectDir)) _ = CaptureSelect(selectDir, select);
        var worldFx = new Views.WorldFx.WorldFxPlayer { Name = "WorldFx", Session = _session, Camera = _camera };
        _interaction.TrapUnder = point => worldFx.Traps?.At(point);
        AddChild(worldFx);
        _session.NotifyReceived += worldFx.Handle;
        if (_args.TryGetValue("capture-chargen", out var chargenDir)) _ = CaptureChargen(chargenDir, select, chargen, levelUp);
        if (_args.TryGetValue("capture-classic-chargen", out var classicChargenDir)) _ = UI.Classic.Creation.CreationCapture.Run(classicChargenDir, select, _session, Shot, Wait, () => GetTree().Quit());
        // Chat entry and the Dungeon Master client (dormant unless the server says we are a DM: --dm [--dm-password pw]).
        var chatInput = new UI.DM.ChatInput { Session = _session };
        hud.AddChild(chatInput);
        _dm = UI.DM.DmUi.Create(_session, _camera, _interaction, chat, chatInput);
        AddChild(_dm);
        AttachFrontEnd(hud, saves, select); // loading screen, game menu, options (Main.FrontEnd.cs)
        AttachPortal(chat); // ActivatePortal's prompt (Main.Portal.cs)
        // The classic interface (Options → Interface → Interface style, or --ui classic) replaces the modern pieces it has built.
        // Its context is kept so the style can be switched during play (Main.FrontEnd.cs ApplyInterfaceStyle).
        _classic = new UI.Classic.ClassicContext
        {
            Main = this, Session = _session, Camera = _camera, Input = _input, Interaction = _interaction, Casting = Casting, State = hudState,
            Hud = hud, Chat = chat, ChatInput = chatInput, Dialog = dialog, Inventory = Inventory, Store = Store, Loot = loot,
            Character = Character, Spellbook = Spellbook, Journal = Journal, Map = Map, Examine = Examine, ActionBar = ActionBar,
            Players = players, LevelUp = levelUp, GameMenu = GameMenu,
        };
        _session.ModuleCleared += () =>
        {
            WindowStack.CloseAll();
            Nui.CloseAll();
            dialog.ShowStep(new DialogMessage { Ended = true });
            _interaction.Targeting = null;
            Casting.Info.Clear();
        };
        if (Settings.Current.Interface.Classic) UI.Classic.ClassicGame.Attach(_classic);
        if (_args.TryGetValue("capture-frontend", out var frontDir)) _ = CaptureFrontEnd(frontDir);
        client.Send(new HelloMessage
        {
            PlayerName = _args.GetValueOrDefault("name", "Player"), PlayerKey = PlayerKey(), AreaReadyHandshake = true,
            // On an NWN server, --character picks that server-vault character without the selection screen.
            CharacterResRef = _args.ContainsKey("connect-nwn") ? _args.GetValueOrDefault("character", "") : "",
            DM = _args.ContainsKey("dm") || _args.ContainsKey("capture-dm"), DMPassword = _args.GetValueOrDefault("dm-password", ""),
            PortalWaypoint = _args.GetValueOrDefault("portal-waypoint", ""), // arriving through ActivatePortal (Main.Portal.cs)
            Password = _args.GetValueOrDefault("password", ""),
        }.Write());
        _session.AreaLoaded += () => GD.Print($"[client] area loaded at {sw.ElapsedMilliseconds} ms");
        if (_args.TryGetValue("capture-prologue", out var prologueDir)) _ = CapturePrologue(prologueDir);
        if (_args.TryGetValue("capture-nwn-chargen", out var nwnChargenDir)) _ = CaptureNwnChargen(nwnChargenDir);
        if (_args.TryGetValue("mp-trace", out var mpDir)) StartMultiplayerTrace(mpDir, effects, hudState); // client/MainMultiplayerTrace.cs
        if (_args.ContainsKey("playtest")) StartPlaytest(hud); // client/Playtest/Main.Playtest.cs
    }

    public override void _PhysicsProcess(double delta)
    {
        // A held single-player game stands still on screen too (a multiplayer world goes on whatever this window does).
        if (_session != null) _session.LocalPause = _server != null && WorldPaused;
        if (_server == null || WorldPaused) return; // client of a remote server, still on the main menu, or paused in a menu
        _accumulator += delta;
        float step = 1f / GameServer.TickRate;
        while (_accumulator >= step)
        {
            _server.Tick(step);
            _accumulator -= step;
        }
    }

    uint _cameraControlledId;

    public override void _Process(double delta)
    {
        if (_session == null) return; // still on the main menu
        // A loaded game (or area change) replaces the player's view: follow the new one.
        if (_attached && _session.Controlled is { } now && (_cameraControlledId != _session.ControlledId || _camera.Target == null || !IsInstanceValid(_camera.Target))) { _cameraControlledId = _session.ControlledId; _camera.Target = now; _camera.Snap(); }
        if (_attached || _session.Controlled == null) return;
        _attached = true;
        _cameraControlledId = _session.ControlledId;
        _camera.Target = _session.Controlled;
        _camera.Snap();
        if (_args.TryGetValue("look", out var look)) _ = Look(look);
        else if (_args.TryGetValue("capture", out var dir)) _ = Capture(dir);
        else if (_args.TryGetValue("capture-ui", out var uiDir)) _ = CaptureUi(uiDir);
        else if (_args.TryGetValue("capture-action-queue", out var actionQueueDir)) _ = CaptureActionQueue(actionQueueDir);
        else if (_args.TryGetValue("capture-drop-freeze", out var dropFreezeDir)) _ = CaptureDropFreeze(dropFreezeDir);
        else if (_args.TryGetValue("capture-hotbar-state", out var hotbarStateDir)) _ = CaptureHotbarState(hotbarStateDir);
        else if (_args.TryGetValue("capture-classic-rest", out var restDir)) _ = UI.Classic.ClassicRestCapture.Run(restDir, Shot, Wait, code => GetTree().Quit(code));
        else if (_args.TryGetValue("capture-quickbar", out var quickbarDir)) _ = QuickbarCapture.Run(quickbarDir, _session, ActionBar, Shot, Wait, code => GetTree().Quit(code));
        else if (_args.TryGetValue("capture-quickbar-radial", out var quickbarRadialDir))
            _ = QuickbarRadialCapture.Run(quickbarRadialDir, ActionBar, GetTree().Root.FindChildren("*", "Control", true, false).OfType<RadialMenu>().First(), Shot, Wait, code => GetTree().Quit(code));
        else if (_args.TryGetValue("capture-hud", out var hudDir)) _ = CaptureHud(hudDir);
        else if (_args.TryGetValue("capture-nwn", out var nwnDir)) _ = CaptureNwn(nwnDir);
        else if (_args.TryGetValue("capture-nwn-nui", out var nwnNuiDir)) _ = CaptureNwnNui(nwnNuiDir);
        else if (_args.TryGetValue("capture-nwn-camera", out var nwnCameraDir)) _ = CaptureNwnCamera(nwnCameraDir);
        else if (_args.TryGetValue("capture-camera-timeline", out var cameraTimelineDir)) _ = CaptureCameraTimeline(cameraTimelineDir);
        else if (_args.TryGetValue("capture-nwn-actions", out var nwnActionsDir)) _ = CaptureNwnActions(nwnActionsDir);
        else if (_args.TryGetValue("capture-nwn-inventory", out var nwnInvDir)) _ = CaptureNwnInventory(nwnInvDir);
        else if (_args.TryGetValue("capture-nwn-dialog", out var nwnDialogDir)) _ = CaptureNwnDialog(nwnDialogDir);
        else if (_args.TryGetValue("capture-nwn-fx", out var nwnFxDir)) _ = CaptureNwnFx(nwnFxDir);
        else if (_args.TryGetValue("capture-nwn-play", out var nwnPlayDir)) _ = CaptureNwnPlay(nwnPlayDir);
        else if (_args.TryGetValue("capture-nwn-combat", out var nwnCombatDir)) _ = CaptureNwnCombat(nwnCombatDir);
        else if (_args.TryGetValue("capture-nwn-party", out var nwnPartyDir)) _ = CaptureNwnParty(nwnPartyDir);
        else if (_args.TryGetValue("capture-nwn-levelup", out var nwnLevelUpDir)) _ = CaptureNwnLevelUp(nwnLevelUpDir);
        else if (_args.TryGetValue("capture-nwn-gui", out var nwnGuiDir)) _ = CaptureNwnGui(nwnGuiDir);
        else if (_args.TryGetValue("capture-cutscene", out var cutDir)) _ = CaptureCutscene(cutDir);
        else if (_args.TryGetValue("capture-world", out var worldDir)) _ = CaptureWorld(worldDir);
        else if (_args.TryGetValue("capture-items", out var itemsDir)) _ = CaptureItems(itemsDir);
        else if (_args.TryGetValue("capture-spells", out var spellDir)) _ = CaptureSpells(spellDir);
        else if (_args.TryGetValue("capture-item-center", out var itemCenterDir))
            _ = UI.ItemCenterCapture.Run(itemCenterDir, _session, _camera, _interaction,
                GetTree().Root.FindChildren("*", "Control", true, false).OfType<RadialMenu>().First(), Shot, Wait, code => GetTree().Quit(code));
        else if (_args.TryGetValue("capture-inventory-radial", out var inventoryRadialDir))
            _ = UI.InventoryRadialCapture.Run(inventoryRadialDir, _session, _interaction, Inventory, Inventory.Radial!, Shot, Wait, code => GetTree().Quit(code));
        else if (_args.TryGetValue("capture-small-item-radial", out var smallItemDir))
            _ = UI.SmallItemRadialCapture.Run(smallItemDir, _session, _camera, _interaction,
                GetTree().Root.FindChildren("*", "Control", true, false).OfType<RadialMenu>().First(), Shot, Wait, code => GetTree().Quit(code));
        else if (_args.TryGetValue("capture-ground-item", out var groundItemDir))
            _ = UI.GroundItemCapture.Run(groundItemDir, _session, _camera, _interaction,
                GetTree().Root.FindChildren("*", "Control", true, false).OfType<RadialMenu>().First(), Shot, Wait, code => GetTree().Quit(code));
        else if (_args.TryGetValue("capture-trap-radial", out var trapRadialDir))
            _ = UI.TrapRadialCapture.Run(trapRadialDir, _session, _camera, _interaction,
                GetTree().Root.FindChildren("*", "Control", true, false).OfType<RadialMenu>().First(), Shot, Wait, code => GetTree().Quit(code));
        else if (_args.TryGetValue("capture-object-radial", out var objectRadialDir))
            _ = UI.ObjectRadialCapture.Run(objectRadialDir, _session, _camera, _interaction,
                GetTree().Root.FindChildren("*", "Control", true, false).OfType<RadialMenu>().First(), Shot, Wait, code => GetTree().Quit(code));
        else if (_args.TryGetValue("capture-radial", out var radialDir))
            _ = UI.RadialCapture.Run(radialDir, _session, _camera, GetTree().Root.FindChildren("*", "Control", true, false).OfType<RadialMenu>().First(), Shot, Wait, () => GetTree().Quit());
        else if (_args.TryGetValue("capture-abilities", out var abilityDir)) _ = AbilitiesCapture.Run(abilityDir, _session, _camera, ActionBar, Shot, Wait, () => GetTree().Quit());
        else if (_args.TryGetValue("capture-character", out var charDir)) _ = CaptureCharacter(charDir);
        else if (_args.TryGetValue("bench", out var benchSeconds)) _ = Bench(double.Parse(benchSeconds, System.Globalization.CultureInfo.InvariantCulture));
        else if (_args.TryGetValue("capture-nui", out var nuiDir)) _ = CaptureNui(nuiDir);
        else if (_args.TryGetValue("capture-fx", out var fxDir)) _ = Views.WorldFx.FxCapture.Run(this, _session, _camera, fxDir);
        else if (_args.TryGetValue("capture-beam-lifetime", out var beamDir)) _ = Views.WorldFx.BeamLifetimeCapture.Run(this, _session, beamDir);
        else if (_args.TryGetValue("capture-controls", out var controlsDir)) _ = CaptureControls(controlsDir);
        else if (_args.TryGetValue("capture-modern-map-flow", out var modernMapDir)) _ = Map.CapturePins(modernMapDir, Shot, Wait, () => GetTree().Quit());
        else if (_args.TryGetValue("capture-classic-map-flow", out var mapFlowDir)) _ = UI.Classic.Sheets.ClassicMapFlowCapture.Run(mapFlowDir, Shot, Wait, () => GetTree().Quit());
        else if (_args.TryGetValue("capture-visual", out var visualDir)) _ = CaptureVisual(visualDir);
        else if (_args.TryGetValue("capture-shaders", out var shadersDir)) _ = CaptureShaders(shadersDir);
        else if (_args.TryGetValue("capture-classic-sheets", out var sheetsDir)) _ = UI.Classic.Sheets.SheetsCapture.Run(sheetsDir, Shot, Wait, () => GetTree().Quit());
        else if (_args.TryGetValue("capture-dm", out var dmDir)) _ = _dm.Capture(dmDir, Shot, Wait, () => GetTree().Quit());
        else if (_args.TryGetValue("capture-style-switch", out var switchDir)) _ = UI.Classic.StyleSwitchCapture.Run(switchDir, new UI.Classic.StyleSwitchCapture.PlayerInputHandle(_input), Shot, Wait, () => GetTree().Quit());
        else if (_args.TryGetValue("capture-classic-hud", out var classicHudDir)) _ = UI.Classic.ClassicHudCapture.Run(classicHudDir, Shot, Wait, () => GetTree().Quit());
        else if (_args.TryGetValue("capture-classic-chat", out var classicChatDir)) _ = UI.Classic.ClassicChatCapture.Run(classicChatDir, Shot, Wait, () => GetTree().Quit());
        else if (_args.TryGetValue("capture-classic-dialogs", out var classicDialogsDir)) _ = UI.Classic.ClassicDialogsCapture.Run(classicDialogsDir, _session, Shot, Wait, () => GetTree().Quit());
        else if (_args.TryGetValue("capture-classic-inventory", out var classicInvDir)) _ = CaptureClassicInventory(classicInvDir); // UI/Classic/Inventory
    }

    /// <summary>
    /// Dev override directory for scripts and .jui files (--script-dir), defaulting
    /// to the repo's tests/scripts for NUI capture runs. Loaded ahead of the module.
    /// </summary>
    string? ScriptDir()
    {
        if (_args.TryGetValue("script-dir", out var dir)) return System.IO.Path.GetFullPath(dir);
        return _args.ContainsKey("capture-nui") ? System.IO.Path.GetFullPath(ProjectSettings.GlobalizePath("res://../tests/scripts")) : null;
    }

    /// <summary>
    /// Screenshots of script-driven NUI windows: runs each --nui-script (comma
    /// separated, via the /script dev command), captures it, then performs that
    /// script's --nui-click actions ("script=window:element[:row]|@x,y|@x,y>x2,y2;script2=...":
    /// scripted button clicks, or real mouse clicks / drags at screen pixels) and captures again.
    /// </summary>
    async Task CaptureNui(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(1.5);
        var clicks = _args.GetValueOrDefault("nui-click", "").Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Split('=', 2)).Where(x => x.Length == 2).ToDictionary(x => x[0], x => x[1]);
        foreach (var script in _args.GetValueOrDefault("nui-script", "nui_form").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            _session.Send(new ChatSendMessage { Text = "/script " + script }.Write());
            await Wait(1.2);
            await Shot(dir, script);
            if (clicks.TryGetValue(script, out var actions))
            {
                foreach (var action in actions.Split('|'))
                {
                    if (action.StartsWith('@'))
                    {
                        // "@x,y" clicks, "@x,y>x2,y2" drags with the real input path (screen pixels).
                        var pts = action[1..].Split('>').Select(v => v.Split(',')).Select(v => new Vector2(float.Parse(v[0]), float.Parse(v[1]))).ToArray();
                        await MouseDrag(pts[0], pts[^1]);
                        GD.Print($"[capture] mouse {action}");
                        await Wait(0.4);
                        continue;
                    }
                    var p = action.Split(':');
                    if (p.Length < 2) continue;
                    bool ok = Nui.Click(p[0], p[1], p.Length > 2 && int.TryParse(p[2], out var row) ? row : -1);
                    GD.Print($"[capture] click {action}: {(ok ? "ok" : "not found")}");
                    await Wait(0.4);
                }
                await Wait(0.6);
                await Shot(dir, script + "_clicked");
            }
            Nui.CloseAll();
            await Wait(0.3);
        }
        GetTree().Quit();
    }

    /// <summary>The HUD: at rest, with a target selected, in a fight (combat text, chat combat log), with effects, then dead.</summary>
    async Task CaptureHud(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        _camera.Distance = 9;
        await Wait(4.5);
        await Shot(dir, "30_hud");
        GetChildren().OfType<UI.HudParts.Cursors>().FirstOrDefault()?.SaveSheet(System.IO.Path.Combine(dir, "cursors.png"));
        // Holding the highlight key (Tab) lights up and names everything usable.
        Input.ActionPress(Preferences.ActionCatalog.HighlightObjects);
        await Wait(0.5);
        await Shot(dir, "30b_highlight_all");
        Input.ActionRelease(Preferences.ActionCatalog.HighlightObjects);
        // A hostile goblin (red) and one moved to the Defender faction (green: they like players).
        _session.Send(new ChatSendMessage { Text = "/spawn nw_goblina" }.Write());
        _session.Send(new ChatSendMessage { Text = "/spawn nw_goblina 4" }.Write());
        await Wait(2.0);
        Input.ActionPress(Preferences.ActionCatalog.HighlightObjects);
        await Wait(0.5);
        await Shot(dir, "30c_highlight_reactions");
        Input.ActionRelease(Preferences.ActionCatalog.HighlightObjects);
        var me = _session.Controlled;
        var npc = _session.Objects.Values.Where(v => v.Kind == ObjectKind.Creature && v != me)
            .OrderBy(v => v.GlobalPosition.DistanceTo(me!.GlobalPosition)).FirstOrDefault();
        if (npc != null)
        {
            _hudState!.Select(npc.ObjectId);
            await Wait(1.0);
            await Shot(dir, "31_target");
        }
        _session.Send(new ChatSendMessage { Text = "/effect haste" }.Write());
        _session.Send(new ChatSendMessage { Text = "/effect ac_increase" }.Write());
        _session.Send(new ChatSendMessage { Text = "/attack" }.Write());
        await Wait(3.2);
        await Shot(dir, "32_combat");
        await Wait(2.0);
        await Shot(dir, "33_combat2");
        _session.Send(new ChatSendMessage { Text = "/kill" }.Write());
        await Wait(5.0);
        await Shot(dir, "34_death");
        GetTree().Quit();
    }

    /// <summary>Screenshots of the game windows: inventory, then a store.</summary>
    async Task CaptureUi(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(1.5);
        Inventory.Toggle();
        await Wait(1.0);
        await Shot(dir, "11_inventory");
        _session.Send(new ChatSendMessage { Text = "/store" + (_args.TryGetValue("store", out var tag) ? " " + tag : "") }.Write());
        await Wait(1.5);
        await Shot(dir, "12_store");
        Store.Close();
        Inventory.Close();
        Character.Toggle();
        await Wait(1.0);
        await Shot(dir, "13_character");
        Character.Close();
        _session.Send(new ChatSendMessage { Text = "/journal" }.Write());
        await Wait(0.5);
        Journal.Toggle();
        await Wait(1.0);
        await Shot(dir, "14_journal");
        Journal.Close();
        // Walk a little so the map has explored ground, then open it.
        _session.Send(new MoveInputMessage { Direction = new System.Numerics.Vector3(0, 1, 0) }.Write());
        await Wait(2.5);
        _session.Send(new MoveInputMessage { Direction = System.Numerics.Vector3.Zero }.Write());
        Map.Toggle();
        await Wait(1.2);
        await Shot(dir, "15_map");
        Map.Close();
        // Examine the main-hand weapon (or any carried item).
        var examined = Inventory.Equipped.GetValueOrDefault(4) ?? Inventory.Carried.FirstOrDefault();
        if (examined != null)
        {
            Examine.Examine(examined.Id);
            await Wait(1.0);
            await Shot(dir, "16_examine");
        }
        Examine.Close();
        _session.Send(new ChatSendMessage { Text = "/loot" }.Write());
        await Wait(2.0);
        await Shot(dir, "16b_loot");
        _session.Send(new ChatSendMessage { Text = "/effect haste" }.Write());
        _session.Send(new ChatSendMessage { Text = "/effect ac_increase" }.Write());
        await Wait(0.5);
        _session.Send(new ChatSendMessage { Text = "/kill" }.Write());
        await Wait(2.5);
        await Shot(dir, "17_death");
        _session.Send(new ChatSendMessage { Text = "/effect haste" }.Write());
        _session.Send(new ChatSendMessage { Text = "/effect ac_increase" }.Write());
        _session.Send(new ChatSendMessage { Text = "/effect ability_increase" }.Write());
        await Wait(1.0);
        await Shot(dir, "18_effects");
        GetTree().Quit();
    }

    /// <summary>ui-world screenshots: minimap, journal (with history), map, then a conversation with the nearest creature.</summary>
    async Task CaptureWorld(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(1.5);
        _session.Send(new MoveInputMessage { Direction = new System.Numerics.Vector3(0, 1, 0) }.Write());
        await Wait(2.0);
        _session.Send(new MoveInputMessage { Direction = System.Numerics.Vector3.Zero }.Write());
        await Wait(1.0);
        await Shot(dir, "w1_minimap");
        _session.Send(new ChatSendMessage { Text = "/journal demo" }.Write());
        await Wait(0.6);
        Journal.Toggle();
        await Wait(1.0);
        await Shot(dir, "w2_journal");
        Journal.SetTab(false);
        await Wait(0.4);
        await Shot(dir, "w2b_journal_active");
        Journal.Close();
        Map.Toggle();
        await Wait(1.2);
        await Shot(dir, "w3_map");
        Map.Close();
        _session.Send(new ChatSendMessage { Text = "/explore" }.Write());
        await Wait(0.5);
        Map.Toggle();
        await Wait(1.2);
        await Shot(dir, "w3b_map_explored");
        Map.Close();
        var me = _session.Controlled;
        var npc = _session.Objects.Values.Where(v => v.Kind == ObjectKind.Creature && v != me)
            .OrderBy(v => v.GlobalPosition.DistanceTo(me!.GlobalPosition)).FirstOrDefault();
        if (npc != null)
        {
            _session.Send(new ActionRequestMessage { Action = ActionKind.Talk, Target = npc.ObjectId }.Write());
            await Wait(6.0);
            // Point at the first reply to show its hover state.
            var talk = GetTree().Root.FindChildren("*", "Control", true, false).OfType<DialogPanel>().FirstOrDefault();
            if (talk?.ReplyPosition(0) is { } at) Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at });
            await Wait(0.4);
            await Shot(dir, "w4_dialog");
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Key1, Pressed = true });
            await Wait(2.5);
            await Shot(dir, "w5_dialog_next");
            if (talk?.ReplyPosition(1) is { } at2)
            {
                Input.WarpMouse(at2);
                Input.ParseInputEvent(new InputEventMouseMotion { Position = at2, GlobalPosition = at2 });
                await Wait(0.4);
                GD.Print($"[capture] hovered: {GetViewport().GuiGetHoveredControl()?.GetPath()}");
                await Shot(dir, "w6_dialog_hover");
            }
        }
        GetTree().Quit();
    }

    /// <summary>Spellbook, then casting the first ready spell on yourself (conjure and release).</summary>
    async Task CaptureSpells(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(1.5);
        Spellbook.Toggle();
        await Wait(1.0);
        await Shot(dir, "20_spellbook");
        Spellbook.Close();
        _camera.Distance = 6;
        // Cheat-cast so the visuals show whatever the character has memorised (--spell, default Bless).
        _session.Send(new ChatSendMessage { Text = "/cast " + _args.GetValueOrDefault("spell", "58") + " nearest" }.Write());
        await Wait(0.8);
        await Shot(dir, "21_conjure");
        await Wait(1.4);
        await Shot(dir, "22_cast");
        await Wait(1.0);
        await Shot(dir, "23_after");
        GetTree().Quit();
    }

    /// <summary>Screenshots of the character selection screen, then of the game after choosing.</summary>
    async Task CaptureSelect(string dir, CharacterSelectPanel select)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(2.5);
        await Shot(dir, "00_select");
        select.Play();
        await Wait(4.0);
        await Shot(dir, "01_entered");
        GetTree().Quit();
    }

    /// <summary>
    /// Screenshots of character creation (an elf wizard: every wizard step, recommended picks), entering
    /// the game with the new character, then a level-up from the character sheet.
    /// </summary>
    async Task CaptureChargen(string dir, CharacterSelectPanel select, UI.CharGen.CharGenWindow gen, UI.CharGen.LevelUpWindow lvl)
    {
        System.IO.Directory.CreateDirectory(dir);
        await Wait(2.5);
        await Shot(dir, "00_select");
        select.Visible = false;
        gen.Open();
        await Wait(2.0);
        // The first step, untouched: the preview must already show the default character.
        await Shot(dir, "01a_first_step");
        gen.SetGender(1);
        await Wait(0.3);
        await Shot(dir, "01_gender");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Race);
        gen.SetRace(1);
        await Wait(0.4);
        await Shot(dir, "02_race");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Portrait);
        await Wait(1.5);
        if (gen.Portraits.Count > 2) gen.SetPortrait(gen.Portraits[2].Id);
        await Wait(0.5);
        await Shot(dir, "03_portrait");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Class);
        gen.SetClass(10);
        await Wait(0.6);
        await Shot(dir, "04_class");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Alignment);
        gen.SetAlignment(50, 85);
        await Wait(0.4);
        await Shot(dir, "05_alignment");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Abilities);
        gen.RecommendAbilities();
        await Wait(0.8);
        await Shot(dir, "06_abilities");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Package);
        await Wait(0.6);
        await Shot(dir, "07_package");
        gen.Recommend("all");
        await Wait(1.2);
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Options);
        await Wait(1.5);
        await Shot(dir, "08_options");
        // Another familiar (the pseudo dragon), then a druid's animal companion, then back to the wizard.
        gen.Character.Level.Familiar = gen.Model!.Familiars.Values.FirstOrDefault(f => f.Name.Contains("Pseudo", StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
        gen.Character.Level.FamiliarName = "Ember";
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Options);
        await Wait(1.5);
        await Shot(dir, "08b_familiar");
        gen.SetClass(3);
        gen.Recommend("all");
        await Wait(1.2);
        gen.Character.Level.Companion = gen.Model!.Companions.Values.FirstOrDefault(f => f.Name.Contains("Wolf", StringComparison.OrdinalIgnoreCase))?.Id ?? gen.Character.Level.Companion;
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Options);
        await Wait(1.5);
        await Shot(dir, "08c_companion");
        gen.SetClass(10);
        gen.Recommend("all");
        await Wait(1.2);
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Skills);
        await Wait(0.8);
        await Shot(dir, "09_skills");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Feats);
        await Wait(0.8);
        await Shot(dir, "10_feats");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Spells);
        await Wait(0.8);
        await Shot(dir, "11_spells");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Appearance);
        await Wait(1.5);
        await Shot(dir, "12a_appearance_default");
        var heads = gen.AvailableHeads;
        if (heads.Count > 3) gen.SetHead(heads[3]);
        gen.SetColor(1, 22);
        await Wait(1.5);
        await Shot(dir, "12b_hair");
        gen.SetColor(0, 4);
        if (gen.Preview != null) gen.Preview.Zoom = 0;
        await Wait(1.5);
        await Shot(dir, "12_appearance");
        gen.SelectColourChannel(2);
        gen.SetColor(2, 30);
        await Wait(1.0);
        await Shot(dir, "12c_tattoo");
        // An extended colour: the picker scrolls to it.
        gen.SetColor(1, 141);
        await Wait(1.5);
        await Shot(dir, "12d_extended");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Details);
        gen.SetName("Lirael", "Starwind");
        gen.SetDetails("Corellon Larethian", "A young elven wizard who left the forest to study the ruins.");
        await Wait(0.4);
        await Shot(dir, "13_details");
        gen.GoTo(UI.CharGen.CharGenWindow.Step.Finish);
        await Wait(1.0);
        await Shot(dir, "14_finish");
        gen.Finish();
        await Wait(6.0);
        await Shot(dir, "15_entered");
        _session.Send(new ChatSendMessage { Text = "/xp 1000" }.Write());
        await Wait(1.0);
        Character.Toggle();
        await Wait(1.2);
        await Shot(dir, "16_sheet");
        lvl.Open();
        await Wait(1.5);
        lvl.SetClass(10);
        await Wait(1.0);
        await Shot(dir, "17_levelup_class");
        // Multiclassing into druid brings the animal companion choice, with its preview.
        lvl.SetClass(3);
        await Wait(1.0);
        lvl.GoTo(UI.CharGen.LevelUpWindow.Step.Options);
        await Wait(1.5);
        await Shot(dir, "17b_levelup_companion");
        lvl.SetClass(10);
        await Wait(1.0);
        lvl.Recommend("all");
        await Wait(1.2);
        lvl.GoTo(UI.CharGen.LevelUpWindow.Step.Skills);
        await Wait(0.6);
        await Shot(dir, "18_levelup_skills");
        lvl.GoTo(UI.CharGen.LevelUpWindow.Step.Spells);
        await Wait(0.6);
        await Shot(dir, "19_levelup_spells");
        lvl.GoTo(UI.CharGen.LevelUpWindow.Step.Confirm);
        await Wait(0.6);
        await Shot(dir, "20_levelup_confirm");
        lvl.Apply();
        await Wait(1.5);
        await Shot(dir, "21_after_levelup");
        Character.Close();
        Spellbook.Toggle();
        await Wait(1.2);
        await Shot(dir, "22_spellbook");
        GetTree().Quit();
    }

    /// <summary>This install's persistent player key (like a public CD key), created on first run.</summary>
    static string PlayerKey()
    {
        const string path = "user://player_key.txt";
        if (Godot.FileAccess.FileExists(path)) return Godot.FileAccess.GetFileAsString(path).Trim();
        var key = Guid.NewGuid().ToString("N").ToUpperInvariant();
        using var f = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
        f?.StoreString(key);
        return key;
    }

    /// <summary>Frame-time benchmark: settles, then slowly orbits the camera and prints fps and render statistics.</summary>
    async Task Bench(double seconds)
    {
        await Wait(3.0);
        var frames = new List<double>();
        double start = Time.GetTicksMsec() / 1000.0, last = start;
        long drawCalls = 0, primitives = 0, samples = 0;
        while (Time.GetTicksMsec() / 1000.0 - start < seconds)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            double now = Time.GetTicksMsec() / 1000.0;
            frames.Add(now - last);
            last = now;
            _camera.Yaw += 0.01f;
            drawCalls += (long)Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
            primitives += (long)Performance.GetMonitor(Performance.Monitor.RenderTotalPrimitivesInFrame);
            samples++;
        }
        frames.Sort();
        double avg = frames.Average(), p99 = frames[(int)(frames.Count * 0.99)];
        GD.Print($"[bench] {_session.Area?.Info.Name}: {frames.Count} frames, avg {1 / avg:0.0} fps ({avg * 1000:0.00} ms), 1% low {1 / p99:0.0} fps, " +
            $"draw calls {drawCalls / Math.Max(1, samples)}, primitives {primitives / Math.Max(1, samples)}, objects {Performance.GetMonitor(Performance.Monitor.RenderTotalObjectsInFrame)}, " +
            $"nodes {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount)}, video mem {Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed) / 1048576:0} MB");
        GetTree().Quit();
    }

    /// <summary>Synthesised left-button press at a, motion to b, release at b.</summary>
    async Task MouseDrag(Vector2 a, Vector2 b)
    {
        Input.ParseInputEvent(new InputEventMouseMotion { Position = a, GlobalPosition = a });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = a, GlobalPosition = a, ButtonMask = MouseButtonMask.Left });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (int i = 1; i <= 8; i++)
        {
            var p = a.Lerp(b, i / 8f);
            Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p, Relative = (b - a) / 8, ButtonMask = MouseButtonMask.Left });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = b, GlobalPosition = b });
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    async Task Wait(double seconds) => await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

    async Task Shot(string dir, string name)
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir, name + ".png"));
        GD.Print($"captured {name}");
    }

    /// <summary>Screenshots from fixed viewpoints: "x,y,z,yaw,pitch,dist[;...]" in area coordinates, saved to --out.</summary>
    async Task Look(string spec)
    {
        string dir = _args.GetValueOrDefault("out", "out/look");
        System.IO.Directory.CreateDirectory(dir);
        await Wait(1.0);
        int n = 0;
        foreach (var view in spec.Split(';'))
        {
            var v = view.Split(',').Select(s => float.Parse(s, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var anchor = new Node3D { Position = new Vector3(v[0], v[1], v[2]) };
            _session.Space.AddChild(anchor);
            _camera.Target = anchor;
            _camera.Yaw = Mathf.DegToRad(v[3]);
            _camera.Pitch = Mathf.DegToRad(v[4]);
            _camera.Distance = v[5];
            _camera.HeightOffset = 0;
            _camera.Snap();
            await Wait(0.5);
            await Shot(dir, $"look_{n++:00}");
            anchor.QueueFree();
        }
        GetTree().Quit();
    }

    /// <summary>Scripted demo: idle, walk, gesture while walking, run, combat stance, attack, overview.</summary>
    async Task Capture(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        _camera.Distance = 5.5f;
        _camera.Pitch = Mathf.DegToRad(-12);
        _camera.HeightOffset = 1.0f;
        _camera.Snap();
        await Wait(1.0);
        await Shot(dir, "01_idle");
        // Close-up from the front.
        float yaw = _camera.Yaw, dist = _camera.Distance, height = _camera.HeightOffset, pitch = _camera.Pitch;
        _camera.Yaw = (_session.Controlled?.Rotation.Z ?? 0) + Mathf.Pi;
        _camera.Distance = 2.6f;
        _camera.HeightOffset = 1.45f;
        _camera.Pitch = Mathf.DegToRad(-6);
        _camera.Snap();
        await Wait(0.4);
        await Shot(dir, "00_portrait");
        _camera.Yaw = yaw; _camera.Distance = dist; _camera.HeightOffset = height; _camera.Pitch = pitch;
        _camera.Snap();
        _input.Scripted = (new Vector2(1, 0), true);
        await Wait(1.6);
        await Shot(dir, "02_walk");
        _input.Gesture();
        await Wait(0.7);
        await Shot(dir, "03_walk_gesture");
        _input.Scripted = (new Vector2(1, 0), false);
        await Wait(1.5);
        await Shot(dir, "04_run");
        _input.Scripted = (Vector2.Zero, false);
        _input.ToggleStance();
        await Wait(1.3);
        await Shot(dir, "05_combat");
        _input.Attack();
        await Wait(0.35);
        await Shot(dir, "06_attack");
        _camera.Distance = 45;
        _camera.Pitch = Mathf.DegToRad(-55);
        await Wait(1.2);
        await Shot(dir, "07_overview");

        // Visual effects and a fight with the nearest creature.
        _session.Send(new ChatSendMessage { Text = "/vfx 20" }.Write());
        _camera.Target = _session.Controlled;
        _camera.Distance = 9;
        _camera.Pitch = Mathf.DegToRad(-25);
        await Wait(0.6);
        await Shot(dir, "09_vfx");
        _session.Send(new ChatSendMessage { Text = "/attack" }.Write());
        await Wait(5.0);
        await Shot(dir, "10_combat");

        // Talk to the nearest other creature.
        var me = _session.Controlled;
        var npc = _session.Objects.Values.Where(v => v.Kind == ObjectKind.Creature && v != me)
            .OrderBy(v => v.GlobalPosition.DistanceTo(me!.GlobalPosition)).FirstOrDefault();
        GD.Print($"[capture] creatures visible: {_session.Objects.Values.Count(v => v.Kind == ObjectKind.Creature)}, nearest: {npc?.DisplayName}");
        if (npc != null)
        {
            _session.Send(new ActionRequestMessage { Action = ActionKind.Talk, Target = npc.ObjectId }.Write());
            _camera.Target = npc;
            _camera.Distance = 7;
            _camera.Pitch = Mathf.DegToRad(-20);
            await Wait(8.0);
            await Shot(dir, "08_dialog");
        }
        GetTree().Quit();
    }
}
