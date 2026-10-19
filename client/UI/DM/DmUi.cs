using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Client.Views;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.DM;

/// <summary>
/// The Dungeon Master client: a toolbar (creator, possession, appear/disappear, limbo, players,
/// time and weather), DM entries in the object and ground context menus, the possession banner,
/// and the camera following whatever the DM controls. Dormant until the server sends "dm-state".
/// </summary>
public partial class DmUi : CanvasLayer
{
    public ClientSession Session = null!;
    public OrbitCamera Camera = null!;
    public Interaction Interaction = null!;
    public ChatLog Chat = null!;
    public ChatInput ChatInput = null!;

    public bool IsDM { get; private set; }
    public DmCreatorWindow Creator = null!;
    public DmExamineWindow DmExamine = null!;
    DmPrompt _prompt = null!;
    DmTimeWindow _time = null!;
    DmListWindow _list = null!;
    PanelContainer _bar = null!;
    Button _possessButton = null!, _appearButton = null!;
    Label _banner = null!;
    PopupMenu? _groundMenu;
    System.Numerics.Vector3 _groundPoint;
    uint _avatar, _possessed, _lastControlled;
    bool _hidden = true;

    const int IdPossess = 1000, IdHeal = 1001, IdKill = 1002, IdInvulnerable = 1003, IdImmortal = 1004, IdAI = 1005, IdRest = 1006,
        IdGoto = 1007, IdSummon = 1008, IdXP = 1009, IdLevel = 1010, IdGold = 1011, IdGiveItem = 1012, IdLimbo = 1013,
        IdDestroy = 1014, IdExamine = 1015, IdVfx = 1016, IdMute = 1017, IdUnmute = 1018, IdBoot = 1019, IdLock = 1020;

    public static DmUi Create(ClientSession session, OrbitCamera camera, Interaction interaction, ChatLog chat, ChatInput chatInput) =>
        new() { Session = session, Camera = camera, Interaction = interaction, Chat = chat, ChatInput = chatInput, Layer = 4 };

    public override void _Ready()
    {
        _bar = new PanelContainer { Visible = false, AnchorLeft = 0.5f, AnchorRight = 0.5f, OffsetLeft = -330, OffsetRight = 330, OffsetTop = 8 };
        _bar.AddThemeStyleboxOverride("panel", UiTheme.Frame(new Color(0.09f, 0.05f, 0.11f, 0.9f), 6));
        AddChild(_bar);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 6);
        _bar.AddChild(row);
        var tag = UiTheme.Label("DM", 16, new Color(0.88f, 0.56f, 1f));
        row.AddChild(tag);
        Button Tool(string text, string tip, Action act)
        {
            var b = new Button { Text = text, TooltipText = tip, FocusMode = Control.FocusModeEnum.None };
            b.Pressed += act;
            row.AddChild(b);
            return b;
        }
        Tool("Creator", "Blueprint palettes: place creatures, items, placeables, encounters...", () => { if (Creator.Visible) Creator.Close(); else Creator.Open(); });
        _possessButton = Tool("Possess", "Possess the creature under the cursor (or right-click it)", TogglePossess);
        _appearButton = Tool("Appear", "Let players see your avatar", () => Session.Request("dm-action", new { action = _hidden ? "appear" : "disappear" }));
        Tool("Limbo", "Creatures taken out of the world", () => _list.Open("dm-limbo"));
        Tool("Players", "Connected players: go to, summon, mute, boot", () => _list.Open("dm-players"));
        Tool("Time", "Time of day and weather", () => _time.Visible = !_time.Visible);
        Tool("Examine", "Full examine of the creature under the cursor", () => { if (Interaction.Hovered is { } h) DmExamine.Examine(h.ObjectId); });

        _banner = UiTheme.Label("", 20, new Color(0.95f, 0.7f, 1f));
        _banner.AnchorLeft = _banner.AnchorRight = 0.5f;
        _banner.OffsetLeft = -300; _banner.OffsetRight = 300; _banner.OffsetTop = 56;
        _banner.HorizontalAlignment = HorizontalAlignment.Center;
        _banner.AddThemeColorOverride("font_outline_color", Colors.Black);
        _banner.AddThemeConstantOverride("outline_size", 7);
        AddChild(_banner);

        Creator = DmCreatorWindow.Create(Session, Interaction);
        AddChild(Creator);
        DmExamine = DmExamineWindow.Create(Session);
        AddChild(DmExamine);
        _prompt = DmPrompt.Create();
        AddChild(_prompt);
        _time = DmTimeWindow.Create(Session);
        AddChild(_time);
        _list = DmListWindow.Create(Session, "Players");
        AddChild(_list);
        Session.NotifyReceived += OnNotify;
    }

    void OnNotify(NotifyMessage m)
    {
        switch (m.Kind)
        {
            case "dm-state":
            {
                var d = JsonDocument.Parse(m.Json).RootElement;
                if (!IsDM) Enable();
                _avatar = d.GetProperty("avatar").GetUInt32();
                _possessed = d.GetProperty("possessed").GetUInt32();
                _hidden = d.GetProperty("hidden").GetBoolean();
                string who = d.GetProperty("possessedName").GetString() ?? "";
                _banner.Text = _possessed != _avatar && who.Length > 0 && _possessed != 0x7F000000 ? $"Possessing {who}" : "";
                _possessButton.Text = _banner.Text.Length > 0 ? "Unpossess" : "Possess";
                _appearButton.Text = _hidden ? "Appear" : "Disappear";
                _appearButton.TooltipText = _hidden ? "You are invisible to players. Let them see you." : "Players can see you. Become invisible again.";
                break;
            }
            case "login-refused":
                Chat.Feedback("Login refused: " + JsonDocument.Parse(m.Json).RootElement.GetProperty("reason").GetString(), "#ff8080");
                break;
        }
    }

    void Enable()
    {
        IsDM = true;
        _bar.Visible = true;
        ChatInput.SetChannel(ChatChannel.Dm);
        Interaction.ExtendMenu = ExtendMenu;
        Interaction.MenuChosen = MenuChosen;
        Interaction.GroundMenu = ShowGroundMenu;
        Chat.Feedback("You are a Dungeon Master. Right-click creatures and the ground for DM tools; /dm talks on the DM channel.", "#e090ff");
    }

    public override void _Process(double delta)
    {
        // The camera follows whatever the DM controls (the avatar or a possessed creature).
        if (!IsDM || Session.ControlledId == _lastControlled || Session.Controlled is not { } view) return;
        _lastControlled = Session.ControlledId;
        Camera.Target = view;
    }

    void TogglePossess()
    {
        if (_possessed != 0x7F000000 && _possessed != _avatar && _possessed != 0) Session.Request("dm-action", new { action = "unpossess" });
        else if (Interaction.Hovered is { Kind: ObjectKind.Creature } h) Session.Request("dm-action", new { action = "possess", target = h.ObjectId });
        else Chat.Feedback("Right-click a creature and choose Possess.", "#e090ff");
    }

    // ------------------------------------------------------------------ context menus

    void ExtendMenu(PopupMenu menu, ObjectView t)
    {
        menu.AddSeparator("Dungeon Master");
        void Item(string text, int id) => menu.AddItem(text, id);
        Item("DM Examine", IdExamine);
        Item("Go to", IdGoto);
        if (t.Kind == ObjectKind.Creature)
        {
            Item("Possess", IdPossess);
            Item("Summon here", IdSummon);
            Item("Heal", IdHeal);
            Item("Kill", IdKill);
            Item("Toggle invulnerable", IdInvulnerable);
            Item("Toggle immortal", IdImmortal);
            Item("Toggle AI", IdAI);
            Item("Force rest", IdRest);
            Item("Give experience...", IdXP);
            Item("Give level", IdLevel);
            Item("Give gold...", IdGold);
            if (Creator.Selected is { Kind: "item" } sel) Item($"Give {sel.Name}", IdGiveItem);
            Item("Limbo", IdLimbo);
            Item("Mute", IdMute);
            Item("Unmute", IdUnmute);
            Item("Boot...", IdBoot);
        }
        if (t.Kind is ObjectKind.Door or ObjectKind.Placeable)
        {
            Item("Toggle lock", IdLock);
            Item("Heal", IdHeal);
            if (Creator.Selected is { Kind: "item" } sel) Item($"Put {sel.Name} inside", IdGiveItem);
        }
        Item("Visual effect...", IdVfx);
        Item("Destroy", IdDestroy);
    }

    void Act(string action, uint target, object? extra = null)
    {
        var data = new Dictionary<string, object> { ["action"] = action, ["target"] = target };
        if (extra != null)
            foreach (var p in extra.GetType().GetProperties()) data[p.Name] = p.GetValue(extra)!;
        Session.Request("dm-action", data);
    }

    void MenuChosen(long id, ObjectView t)
    {
        uint target = t.ObjectId;
        switch (id)
        {
            case IdPossess: Act("possess", target); break;
            case IdHeal: Act("heal", target); break;
            case IdKill: Act("kill", target); break;
            case IdInvulnerable: Act("invulnerable", target); break;
            case IdImmortal: Act("immortal", target); break;
            case IdAI: Act("ai", target); break;
            case IdRest: Act("rest", target); break;
            case IdGoto: Act("goto", target); break;
            case IdSummon: Act("summon", target); break;
            case IdLevel: Act("give-level", target, new { amount = 1 }); break;
            case IdLimbo: Act("limbo", target); break;
            case IdDestroy: Act("destroy", target); break;
            case IdLock: Act("lock", target); break;
            case IdMute: Act("mute", target); break;
            case IdUnmute: Act("unmute", target); break;
            case IdExamine: DmExamine.Examine(target); break;
            case IdGiveItem when Creator.Selected is { } sel: Act("give-item", target, new { resref = sel.ResRef }); break;
            case IdXP: Ask("Give experience", "Experience points (negative takes):", "1000", v => Act("give-xp", target, new { amount = v })); break;
            case IdGold: Ask("Give gold", "Gold pieces (negative takes):", "100", v => Act("give-gold", target, new { amount = v })); break;
            case IdVfx: Ask("Visual effect", "visualeffects.2da row:", "20", v => Act("vfx", target, new { value = v })); break;
            case IdBoot: _prompt.Ask("Boot player", "Reason:", "Booted by a Dungeon Master.", r => Act("boot", target, new { reason = r })); break;
        }
    }

    void Ask(string title, string text, string initial, Action<int> done) =>
        _prompt.Ask(title, text, initial, v => { if (int.TryParse(v, out var n)) done(n); });

    void ShowGroundMenu(Vector2 at, System.Numerics.Vector3 point)
    {
        if (!IsDM) return;
        if (_groundMenu == null)
        {
            _groundMenu = new PopupMenu();
            _groundMenu.IdPressed += GroundChosen;
            AddChild(_groundMenu);
        }
        _groundPoint = point;
        _groundMenu.Clear();
        _groundMenu.AddSeparator("Dungeon Master");
        if (Creator.Selected is { } sel) _groundMenu.AddItem($"Create {sel.Name} here", 1);
        _groundMenu.AddItem("Jump here", 2);
        _groundMenu.AddItem("Jump all players here", 3);
        _groundMenu.AddItem("Visual effect here...", 4);
        _groundMenu.AddItem("Open creator", 5);
        _groundMenu.Position = (Vector2I)at;
        _groundMenu.Popup();
    }

    void GroundChosen(long id)
    {
        var p = new[] { _groundPoint.X, _groundPoint.Y, _groundPoint.Z };
        switch (id)
        {
            case 1: Creator.SpawnAt(_groundPoint); break;
            case 2: Session.Request("dm-action", new { action = "jump-point", pos = p }); break;
            case 3: Session.Request("dm-action", new { action = "jump-all", pos = p }); break;
            case 4: Ask("Visual effect", "visualeffects.2da row:", "20", v => Session.Request("dm-action", new { action = "vfx", value = v, pos = p })); break;
            case 5: Creator.Open(); break;
        }
    }

    // ------------------------------------------------------------------ screenshots

    /// <summary>
    /// Scripted DM screenshots (--capture-dm dir): toolbar, creator palette with search, placing a
    /// creature, its DM context menu, possession, DM examine, the DM chat channel, time and weather.
    /// </summary>
    public async Task Capture(string dir, Func<string, string, Task> shot, Func<double, Task> wait, Action quit)
    {
        System.IO.Directory.CreateDirectory(dir);
        await wait(2.0);
        Camera.Distance = 11;
        await shot(dir, "30_dm_toolbar");
        Creator.Open("creature");
        await wait(1.5);
        await shot(dir, "31_dm_creator");
        Creator.Search("commoner"); // a peaceful creature, so the town guards leave it alone
        await wait(0.5);
        Creator.SelectFirst();
        await wait(0.5);
        await shot(dir, "32_dm_creator_search");

        // Place the goblin a few metres in front of the avatar.
        var me = Session.Controlled;
        if (me != null)
        {
            var pos = Rendering.Convert.N(me.Position) + new System.Numerics.Vector3(3, 0, 0);
            Creator.SpawnAt(pos);
        }
        await wait(1.5);
        Creator.Close();
        var npc = Session.Objects.Values.Where(v => v.Kind == ObjectKind.Creature && v.ObjectId != Session.ControlledId && me != null)
            .OrderBy(v => v.GlobalPosition.DistanceTo(me!.GlobalPosition)).FirstOrDefault();
        if (npc != null)
        {
            var at = Camera.UnprojectPosition(npc.GlobalPosition + Vector3.Up * 1.1f);
            Interaction.ShowMenu(npc, at);
            await wait(0.6);
            await shot(dir, "33_dm_context_menu");
            Interaction.CloseRadial?.Invoke();
            Session.Request("dm-action", new { action = "possess", target = npc.ObjectId });
            await wait(1.5);
            Session.Send(new ChatSendMessage { Text = "Grr! Who goes there?" }.Write());
            await wait(1.0);
            await shot(dir, "34_dm_possessed");
            Session.Request("dm-action", new { action = "unpossess" });
            await wait(1.0);
            DmExamine.Examine(npc.ObjectId);
            await wait(1.0);
            await shot(dir, "35_dm_examine");
            DmExamine.Close();
        }
        ChatInput.Send("/dm The goblins attack at dusk.");
        _time.Visible = true;
        await wait(1.0);
        var ground = Session.Controlled is { } c ? Camera.UnprojectPosition(c.GlobalPosition + new Vector3(1.5f, 0, 0)) : new Vector2(600, 400);
        ShowGroundMenu(ground + new Vector2(80, 40), Session.Controlled is { } c2 ? Rendering.Convert.N(c2.Position) : default);
        await wait(0.6);
        await shot(dir, "36_dm_chat_time_ground");
        quit();
    }
}
