using System.Text.Json.Nodes;
using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.UI.Nui;

/// <summary>
/// Client side of NUI: opens, updates and closes script-driven windows from the
/// server's "nui-open" / "nui-close" / "nui-bind" / "nui-group" notifies, and
/// sends "nui-event" (click, mousedown, mouseup, mousescroll, open, close, focus,
/// blur) and "nui-bind" (user-edited values) requests back. Windows from
/// NuiCreateFromResRef are read from the client's resources (.jui).
/// </summary>
public partial class NuiManager : Control
{
    public ClientSession Session = null!;
    /// <summary>Extra directory searched for .jui files (dev / capture runs).</summary>
    public string? JuiDirectory;

    readonly Dictionary<int, NuiWindowView> _windows = new();
    NuiSkin? _skin;
    NuiWindowView? _focused, _resizing;

    public NuiSkin Skin => _skin ??= new NuiSkin(Session.Content);
    public IReadOnlyDictionary<int, NuiWindowView> Windows => _windows;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        Session.NotifyReceived += OnNotify;
    }

    void OnNotify(NotifyMessage m)
    {
        if (!m.Kind.StartsWith("nui-")) return;
        JsonObject d;
        try { d = JsonNode.Parse(m.Json) as JsonObject ?? new JsonObject(); }
        catch (Exception e) { GD.PrintErr($"[nui] bad {m.Kind}: {e.Message}"); return; }
        int token = d["token"] is JsonValue t && t.TryGetValue<int>(out var tv) ? tv : 0;
        switch (m.Kind)
        {
            case "nui-open":
                Open(token, Str(d["id"]), Str(d["resref"]), d["window"] as JsonObject);
                break;
            case "nui-close":
                Remove(token);
                break;
            case "nui-bind" when _windows.TryGetValue(token, out var w):
                w.Binds.Set(Str(d["bind"]), d["value"]?.DeepClone());
                break;
            case "nui-group" when _windows.TryGetValue(token, out var w):
                w.SetGroupLayout(Str(d["element"]), d["layout"]?.DeepClone());
                break;
        }
    }

    static string Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

    void Open(int token, string id, string resref, JsonObject? window)
    {
        if (window == null && resref.Length > 0) window = LoadJui(resref);
        if (window == null)
        {
            GD.PrintErr($"[nui] window {token} ({id}): no definition{(resref.Length > 0 ? $" for {resref}.jui" : "")}");
            return;
        }
        // A window with the same id replaces the old one (the server already dropped it).
        foreach (var old in _windows.Values.Where(w => id.Length > 0 && w.WindowId == id).ToList()) Remove(old.Token);
        Remove(token);
        var view = new NuiWindowView();
        view.Init(this, token, id, (JsonObject)window.DeepClone());
        _windows[token] = view;
        AddChild(view);
        SendEvent(token, "open", "_window_", -1, null);
        Focus(view);
    }

    JsonObject? LoadJui(string resref)
    {
        byte[]? data = null;
        if (JuiDirectory != null)
        {
            var path = System.IO.Path.Combine(JuiDirectory, resref + ".jui");
            if (System.IO.File.Exists(path)) data = System.IO.File.ReadAllBytes(path);
        }
        data ??= Session.Content?.Resources.Read(resref, OpenWinter.Aurora.Resources.ResType.Jui);
        if (data == null) return null;
        try { return JsonNode.Parse(data) as JsonObject; }
        catch (Exception e) { GD.PrintErr($"[nui] {resref}.jui: {e.Message}"); return null; }
    }

    void Remove(int token)
    {
        if (!_windows.Remove(token, out var w)) return;
        if (_focused == w) _focused = null;
        if (_resizing == w) _resizing = null;
        w.QueueFree();
    }

    /// <summary>The user closed a window (title bar X): "close" event, then gone.</summary>
    public void CloseByUser(NuiWindowView w)
    {
        SendEvent(w.Token, "close", "_window_", -1, null);
        Remove(w.Token);
    }

    /// <summary>Brings a window to the front and moves focus to it (focus / blur events).</summary>
    public void Focus(NuiWindowView? w)
    {
        if (w != null && IsInstanceValid(w) && w.GetParent() == this) MoveChild(w, GetChildCount() - 1);
        if (_focused == w) return;
        if (_focused != null && IsInstanceValid(_focused) && _windows.ContainsKey(_focused.Token))
            SendEvent(_focused.Token, "blur", "_window_", -1, null);
        _focused = w;
        if (w != null) SendEvent(w.Token, "focus", "_window_", -1, null);
    }

    public void SendEvent(int token, string type, string element, int index, JsonNode? payload) =>
        Session.Request("nui-event", new JsonObject
        {
            ["token"] = token, ["type"] = type, ["element"] = element, ["index"] = index, ["payload"] = payload?.DeepClone(),
        });

    public void SendBind(int token, string bind, JsonNode? value, int index = -1) =>
        Session.Request("nui-bind", new JsonObject { ["token"] = token, ["bind"] = bind, ["value"] = value?.DeepClone(), ["index"] = index });

    /// <summary>Topmost window under a screen point.</summary>
    NuiWindowView? WindowAt(Vector2 p)
    {
        for (int i = GetChildCount() - 1; i >= 0; i--)
            if (GetChild(i) is NuiWindowView w && w.Visible && w.AcceptsInput && w.GetGlobalRect().HasPoint(p)) return w;
        return null;
    }

    public override void _Input(InputEvent e)
    {
        switch (e)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } mb:
                if (mb.Pressed)
                {
                    var w = WindowAt(mb.Position);
                    Focus(w);
                    if (w != null && w.CanResizeAt(mb.Position))
                    {
                        _resizing = w;
                        w.BeginResize(mb.Position);
                        GetViewport().SetInputAsHandled();
                    }
                }
                else if (_resizing != null)
                {
                    _resizing.EndResize();
                    _resizing = null;
                    GetViewport().SetInputAsHandled();
                }
                break;
            case InputEventMouseMotion mm when _resizing != null:
                _resizing.ResizeTo(mm.Position);
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    // ----- scripted access (capture runs) -----

    public NuiWindowView? Find(string id) => _windows.Values.FirstOrDefault(w => w.WindowId == id);

    /// <summary>Clicks a button by element id (and list row) in the window with the given id; "_collapse_" toggles collapse.</summary>
    public bool Click(string windowId, string element, int row = -1)
    {
        if (element == "_collapse_" && Find(windowId) is { } w) { w.ToggleCollapsed(); return true; }
        if (Find(windowId)?.FindElement(element, row) is NuiButtonView b) { b.Click(); return true; }
        return false;
    }

    public void CloseAll()
    {
        foreach (var t in _windows.Keys.ToList()) Remove(t);
    }
}
