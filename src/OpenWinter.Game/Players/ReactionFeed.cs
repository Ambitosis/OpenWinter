using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Players;

/// <summary>
/// How each creature in a player's area reacts to that player, for the client's highlight and name
/// colours (red hostile, green in the same party/faction, blue otherwise). Reputation is per player,
/// so each player gets their own "reactions" notify: { "id": 0 neutral | 1 friendly | 2 hostile },
/// with only the creatures that changed, and "reset" when the player entered a new area.
/// Other players only count as hostile when the server's PvP setting lets the two fight.
/// </summary>
public sealed class ReactionFeed : IWorldSystem
{
    const float Interval = 0.5f;
    public const int Neutral = 0, Friendly = 1, Hostile = 2;

    readonly World _world;
    readonly Dictionary<uint, (uint Area, Dictionary<uint, int> Sent)> _players = new();
    float _timer;

    public ReactionFeed(World world) { _world = world; }

    public int Order => 101;

    public void Step(float dt)
    {
        _timer -= dt;
        if (_timer > 0 || _world.Output == null) return;
        _timer = Interval;
        var seen = new HashSet<uint>();
        foreach (var pc in _world.ConnectedPCs)
        {
            seen.Add(pc.Id);
            if (pc.Area is not { } area) continue;
            bool reset = !_players.TryGetValue(pc.Id, out var state) || state.Area != area.Id;
            if (reset) _players[pc.Id] = state = (area.Id, new Dictionary<uint, int>());
            var changes = new Dictionary<string, object>();
            foreach (var o in area.Objects)
            {
                if (o is not Creature c || c == pc || c.IsDestroyed) continue;
                int r = Of(c, pc);
                if (state.Sent.TryGetValue(c.Id, out var last) && last == r) continue;
                state.Sent[c.Id] = r;
                changes[c.Id.ToString()] = r;
            }
            if (reset) changes["reset"] = true;
            if (changes.Count > 0) _world.Output.Notify("reactions", pc, changes, pc);
        }
        foreach (var gone in _players.Keys.Where(id => !seen.Contains(id)).ToList()) _players.Remove(gone);
    }

    /// <summary>How <paramref name="c"/> reacts to <paramref name="pc"/>.</summary>
    public int Of(Creature c, Creature pc)
    {
        int rep = _world.Factions.ReputationOf(c, pc);
        if (rep < 0) return Neutral;
        if (rep <= FactionTable.HostileTo)
            return _world.ControllingPlayer(c) != null && !_world.PvpAllows(pc, c) ? Neutral : Hostile;
        // Native nameplates scene: reputation 100 / GetIsFriend=true stays blue; joining the
        // player's faction as a henchman turns green. Friendly reputation alone is not a green label.
        return _world.Factions.FactionOf(c) == _world.Factions.FactionOf(pc) ? Friendly : Neutral;
    }
}
