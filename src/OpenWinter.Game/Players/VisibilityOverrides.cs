using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Players;

/// <summary>
/// Per-player and global overrides of which objects a player's client is told about (NWNX_Visibility). A player's
/// own override wins over the global one. The server consults <see cref="Hidden"/> when it decides what to send.
/// What each value does to a client follows the NWNX header; it is unmeasured (the NWNX server runs without players).
/// </summary>
public sealed class VisibilityOverrides : IWorldSystem
{
    public const int Default = -1, Visible = 0, HiddenAlways = 1, DMOnly = 2, AlwaysVisible = 3, AlwaysVisibleDMOnly = 4;

    readonly Dictionary<uint, Dictionary<uint, int>> _byTarget = new();

    public VisibilityOverrides(World world)
    {
        // Only destruction ends overrides: an item picked up (taken out of the area) keeps its own.
        world.ObjectRemoved += o =>
        {
            if (!o.IsDestroyed || _byTarget.Count == 0) return;
            _byTarget.Remove(o.Id);
            if (o is not Creature) return;
            foreach (var (target, map) in _byTarget.ToList())
                if (map.Remove(o.Id) && map.Count == 0) _byTarget.Remove(target);
        };
    }

    public void Step(float dt) { }

    /// <summary>Whether any override is set (the common case has none and skips the lookups).</summary>
    public bool Any => _byTarget.Count > 0;

    /// <summary>The override stored for (<paramref name="player"/>, target); OBJECT_INVALID is the global one. -1 when unset.</summary>
    public int Get(uint player, uint target) =>
        _byTarget.TryGetValue(target, out var map) && map.TryGetValue(player, out int v) ? v : Default;

    /// <summary>Sets an override (-1 removes it).</summary>
    public void Set(uint player, uint target, int value)
    {
        if (value == Default)
        {
            if (_byTarget.TryGetValue(target, out var m) && m.Remove(player) && m.Count == 0) _byTarget.Remove(target);
            return;
        }
        if (!_byTarget.TryGetValue(target, out var map)) _byTarget[target] = map = new Dictionary<uint, int>();
        map[player] = value;
    }

    /// <summary>The override in force for a player: their own, else the global one, else -1.</summary>
    public int For(uint player, uint target)
    {
        if (!_byTarget.TryGetValue(target, out var map)) return Default;
        return map.TryGetValue(player, out int own) ? own : map.TryGetValue(GameObject.InvalidId, out int global) ? global : Default;
    }

    /// <summary>
    /// What the override says for a viewer: true (hidden), false (shown whatever the normal rules say), or null (the
    /// normal rules decide). HIDDEN hides; DM_ONLY hides from players and leaves DMs to the normal rules;
    /// ALWAYS_VISIBLE shows; ALWAYS_VISIBLE_DM_ONLY shows to DMs and hides from players.
    /// </summary>
    public bool? Hidden(uint player, bool viewerIsDM, uint target) => For(player, target) switch
    {
        HiddenAlways => true,
        DMOnly => viewerIsDM ? null : true,
        AlwaysVisible => false,
        AlwaysVisibleDMOnly => !viewerIsDM,
        _ => null,
    };
}
