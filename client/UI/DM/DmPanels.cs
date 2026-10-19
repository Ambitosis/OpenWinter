using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.DM;

/// <summary>DM examine: every stat, flag, local variable and carried item, with quick actions.</summary>
public partial class DmExamineWindow : UiTheme.Window
{
    public ClientSession Session = null!;
    VBoxContainer _content = null!;
    uint _target;

    public static DmExamineWindow Create(ClientSession session)
    {
        var w = Create<DmExamineWindow>("DM Examine", new Vector2(460, 520));
        w.Session = session;
        w.OffsetLeft = 40; w.OffsetTop = 120;
        return w;
    }

    public override void _Ready()
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        Body.AddChild(scroll);
        _content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _content.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(_content);
        Session.NotifyReceived += OnNotify;
    }

    public void Examine(uint id)
    {
        _target = id;
        Session.Request("dm-examine", new { target = id });
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind != "dm-examine") return;
        var d = JsonDocument.Parse(m.Json).RootElement;
        uint id = d.GetProperty("target").GetUInt32();
        // Quiet refreshes (after an action) only update an open window about the same object.
        if (d.GetProperty("quiet").GetBoolean() && (!Visible || id != _target)) return;
        _target = id;
        foreach (var c in _content.GetChildren()) c.QueueFree();
        TitleLabel.Text = d.GetProperty("name").GetString() + "  ·  " + d.GetProperty("kind").GetString();
        foreach (var line in d.GetProperty("lines").EnumerateArray())
        {
            var l = UiTheme.Label(line.GetString() ?? "", 14);
            l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            l.CustomMinimumSize = new Vector2(400, 0);
            _content.AddChild(l);
        }
        var items = d.GetProperty("items");
        if (items.GetArrayLength() > 0)
        {
            _content.AddChild(UiTheme.Label("Inventory", 15, UiTheme.Title));
            foreach (var item in items.EnumerateArray())
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 6);
                var icon = item.GetProperty("icon").EnumerateArray().Select(i => i.GetString() ?? "").ToArray();
                if (Session.Content != null) row.AddChild(UiTheme.Icon(Session.Content, icon, item.GetProperty("stack").GetInt32(), 28));
                var name = UiTheme.Label(item.GetProperty("name").GetString() ?? "", 14);
                name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(name);
                uint itemId = item.GetProperty("id").GetUInt32();
                var take = new Button { Text = "Take", FocusMode = FocusModeEnum.None };
                take.Pressed += () => { Session.Request("dm-action", new { action = "take-item", item = itemId }); Examine(_target); };
                row.AddChild(take);
                _content.AddChild(row);
            }
        }
        Visible = true;
    }
}

/// <summary>A one-line prompt (amounts, reasons, effect numbers).</summary>
public partial class DmPrompt : UiTheme.Window
{
    LineEdit _line = null!;
    Label _text = null!;
    Action<string>? _done;

    public static DmPrompt Create()
    {
        var w = Create<DmPrompt>("", new Vector2(340, 150));
        w.AnchorLeft = w.AnchorRight = 0.5f;
        w.AnchorTop = w.AnchorBottom = 0.5f;
        w.OffsetLeft = -170; w.OffsetRight = 170; w.OffsetTop = -75; w.OffsetBottom = 75;
        return w;
    }

    public override void _Ready()
    {
        _text = UiTheme.Label("", 14, UiTheme.Muted);
        Body.AddChild(_text);
        var row = new HBoxContainer();
        Body.AddChild(row);
        _line = new LineEdit { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _line.TextSubmitted += Submit;
        _line.FocusEntered += () => ChatInput.Typing = true;
        _line.FocusExited += () => ChatInput.Typing = false;
        row.AddChild(_line);
        var ok = new Button { Text = "OK", CustomMinimumSize = new Vector2(64, 0) };
        ok.Pressed += () => Submit(_line.Text);
        row.AddChild(ok);
    }

    public void Ask(string title, string text, string initial, Action<string> done)
    {
        TitleLabel.Text = title;
        _text.Text = text;
        _line.Text = initial;
        _done = done;
        Visible = true;
        _line.GrabFocus();
        _line.SelectAll();
    }

    void Submit(string value)
    {
        var done = _done;
        _done = null;
        _line.ReleaseFocus();
        Close();
        done?.Invoke(value.Trim());
    }
}

/// <summary>Time of day and weather for the DM's area.</summary>
public partial class DmTimeWindow : UiTheme.Window
{
    public ClientSession Session = null!;
    SpinBox _hour = null!;

    public static DmTimeWindow Create(ClientSession session)
    {
        var w = Create<DmTimeWindow>("Time & Weather", new Vector2(330, 200));
        w.Session = session;
        w.AnchorLeft = w.AnchorRight = 0.5f;
        w.OffsetLeft = -165; w.OffsetRight = 165; w.OffsetTop = 70;
        return w;
    }

    public override void _Ready()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        Body.AddChild(row);
        row.AddChild(UiTheme.Label("Hour", 15));
        _hour = new SpinBox { MinValue = 0, MaxValue = 23, Value = 12, CustomMinimumSize = new Vector2(90, 0) };
        row.AddChild(_hour);
        var set = new Button { Text = "Set time" };
        set.Pressed += () => Session.Request("dm-action", new { action = "set-time", value = (int)_hour.Value, minute = 0 });
        row.AddChild(set);
        Body.AddChild(UiTheme.Label("Weather in this area", 15, UiTheme.Title));
        var weather = new HBoxContainer();
        weather.AddThemeConstantOverride("separation", 6);
        Body.AddChild(weather);
        foreach (var (name, value) in new[] { ("Clear", 0), ("Rain", 1), ("Snow", 2), ("Area default", -1) })
        {
            var b = new Button { Text = name, FocusMode = FocusModeEnum.None };
            b.Pressed += () => Session.Request("dm-action", new { action = "set-weather", value });
            weather.AddChild(b);
        }
    }
}

/// <summary>A list of objects with buttons per row: limbo (bring back) and the player list.</summary>
public partial class DmListWindow : UiTheme.Window
{
    public ClientSession Session = null!;
    VBoxContainer _rows = null!;
    string _kind = "";

    public static DmListWindow Create(ClientSession session, string title)
    {
        var w = Create<DmListWindow>(title, new Vector2(420, 360));
        w.Session = session;
        w.OffsetLeft = 40; w.OffsetTop = 120;
        return w;
    }

    public override void _Ready()
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        Body.AddChild(scroll);
        _rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(_rows);
        Session.NotifyReceived += OnNotify;
    }

    /// <summary>Opens the limbo list ("dm-limbo") or the player list ("dm-players").</summary>
    public void Open(string kind)
    {
        _kind = kind;
        TitleLabel.Text = kind == "dm-limbo" ? "Limbo" : "Players";
        Visible = true;
        Session.Request(kind, new { });
    }

    void OnNotify(NotifyMessage m)
    {
        if (m.Kind != _kind || !Visible) return;
        var d = JsonDocument.Parse(m.Json).RootElement;
        foreach (var c in _rows.GetChildren()) c.QueueFree();
        var list = d.GetProperty(_kind == "dm-limbo" ? "creatures" : "players");
        if (list.GetArrayLength() == 0) _rows.AddChild(UiTheme.Label(_kind == "dm-limbo" ? "Nobody is in limbo." : "No players.", 14, UiTheme.Muted));
        foreach (var e in list.EnumerateArray())
        {
            uint id = e.GetProperty("id").GetUInt32();
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            string text = e.GetProperty("name").GetString() ?? "";
            if (_kind == "dm-players")
                text += $"  ({e.GetProperty("player").GetString()}{(e.GetProperty("dm").GetBoolean() ? ", DM" : "")}{(e.GetProperty("muted").GetBoolean() ? ", muted" : "")})  {e.GetProperty("area").GetString()}";
            var label = UiTheme.Label(text, 14);
            label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            label.ClipText = true;
            row.AddChild(label);
            void Btn(string caption, object request)
            {
                var b = new Button { Text = caption, FocusMode = FocusModeEnum.None };
                b.Pressed += () => { Session.Request("dm-action", request); Session.Request(_kind, new { }); };
                row.AddChild(b);
            }
            if (_kind == "dm-limbo") Btn("Bring here", new { action = "from-limbo", target = id });
            else
            {
                Btn("Go to", new { action = "goto", target = id });
                Btn("Summon", new { action = "summon", target = id });
                Btn(e.GetProperty("muted").GetBoolean() ? "Unmute" : "Mute", new { action = e.GetProperty("muted").GetBoolean() ? "unmute" : "mute", target = id });
                Btn("Boot", new { action = "boot", target = id, reason = "Booted by a Dungeon Master." });
            }
            _rows.AddChild(row);
        }
    }
}
