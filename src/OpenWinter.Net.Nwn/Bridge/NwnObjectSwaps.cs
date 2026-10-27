using System.Text.Json.Nodes;

namespace OpenWinter.Net.Nwn.Bridge;

/// <summary>
/// ReplaceObjectAnimation and ReplaceObjectTexture. An object update carries the object's whole current list
/// (camanim scene: a second replacement comes with the first, an undone one drops out, none left is an empty list);
/// the changes go to the client as OpenWinter's server sends each call, "object-animation" and "object-texture"
/// {old, new}, an undone one with new "". The client keeps them until the area changes, and so does this; the
/// player's own carry over and are given again when the player appears in the new area.
/// </summary>
public sealed class NwnObjectSwaps(Action<byte[]> emit)
{
    public const string Animation = "object-animation", Texture = "object-texture";

    readonly Dictionary<(uint Id, string Kind), Dictionary<string, string>> _shown = [];
    readonly HashSet<uint> _carried = [];

    /// <summary>An update's list for one object; null when the update doesn't carry it.</summary>
    public void Update(uint id, string kind, List<(string Old, string New)>? list)
    {
        if (list == null) return;
        var now = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (old, replacement) in list)
            if (old.Length > 0) now[old] = replacement;
        var was = _shown.GetValueOrDefault((id, kind));
        if (was != null)
            foreach (var old in was.Keys.Where(k => !now.ContainsKey(k)))
                Notify(kind, id, old, "");
        foreach (var (old, replacement) in now)
            if (was == null || !was.TryGetValue(old, out var shown) || shown != replacement)
                Notify(kind, id, old, replacement);
        if (now.Count == 0) _shown.Remove((id, kind));
        else _shown[(id, kind)] = now;
    }

    /// <summary>A creature appeared: the player's replacements carried over from the last area are given again.</summary>
    public void Added(uint id)
    {
        if (!_carried.Remove(id)) return;
        foreach (var ((holder, kind), swaps) in _shown)
            if (holder == id)
                foreach (var (old, replacement) in swaps) Notify(kind, id, old, replacement);
    }

    /// <summary>A new area: only the player's replacements are kept.</summary>
    public void Area(uint player)
    {
        foreach (var key in _shown.Keys.Where(k => k.Id != player).ToList()) _shown.Remove(key);
        _carried.Clear();
        if (_shown.Keys.Any(k => k.Id == player)) _carried.Add(player);
    }

    void Notify(string kind, uint id, string old, string replacement) =>
        emit(new NotifyMessage { Kind = kind, Subject = id, Json = new JsonObject { ["old"] = old, ["new"] = replacement }.ToJsonString() }.Write());
}
