using System.Text.Json.Nodes;
using OpenWinter.Client.UI.Nui.Model;

namespace OpenWinter.Client.UI.Nui;

/// <summary>
/// What a window's elements share: its bind store, skin, and the way back to the
/// server (events and user-edited binds). Elements register for the binds they
/// read and are refreshed when one changes.
/// </summary>
public sealed class NuiContext
{
    public required int Token;
    public required NuiBinds Binds;
    public required NuiSkin Skin;
    public required NuiWindowView Window;
    /// <summary>(type, element id, array index, payload) to the server.</summary>
    public required Action<string, string, int, JsonNode?> Event;
    /// <summary>(bind, full value) to the server.</summary>
    public required Action<string, JsonNode?> BindToServer;

    readonly Dictionary<string, HashSet<NuiElement>> _watchers = new(StringComparer.Ordinal);

    public void Watch(NuiElement e, IEnumerable<string> binds)
    {
        foreach (var b in binds)
        {
            if (!_watchers.TryGetValue(b, out var set)) _watchers[b] = set = new HashSet<NuiElement>();
            set.Add(e);
        }
    }

    public void Unwatch(NuiElement e)
    {
        foreach (var set in _watchers.Values) set.Remove(e);
    }

    public void Refresh(string bind)
    {
        if (!_watchers.TryGetValue(bind, out var set)) return;
        foreach (var e in set.ToArray())
            if (Godot.GodotObject.IsInstanceValid(e) && e.IsInsideTree()) e.Refresh();
    }

    /// <summary>
    /// The user changed a bound value: store it, re-render everything reading the
    /// bind, and tell the server (which fires "watch" for watched binds). Static
    /// (unbound) properties don't change.
    /// </summary>
    public void Write(JsonNode? prop, int row, JsonNode? value)
    {
        if (!NuiBinds.IsBind(prop, out var name)) return;
        var full = Binds.WithValue(name, row, value);
        Binds.Set(name, full);
        BindToServer(name, full?.DeepClone());
    }

    /// <summary>Sends an event for an element; untagged elements send nothing.</summary>
    public void Send(string type, string id, int row, JsonNode? payload = null)
    {
        if (id.Length == 0) return;
        Event(type, id, row, payload);
    }
}
