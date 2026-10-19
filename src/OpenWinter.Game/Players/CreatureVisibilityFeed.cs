using OpenWinter.Game.Objects;
using OpenWinter.Game.Society;

namespace OpenWinter.Game.Players;

/// <summary>Viewer presentation only: creatures remain in the area, AI and network interest sets.</summary>
public sealed class CreatureVisibilityFeed(World world) : IWorldSystem
{
    readonly Dictionary<PlayerSession, State> _players = new();
    float _timer;
    sealed class State
    {
        public uint Area, Viewer;
        public readonly Dictionary<uint, Visibility?> Sent = new();
    }
    public sealed record Visibility(bool seen, bool heard);
    public int Order => 103;
    /// <summary>Area descriptions rebuild client views even when the area and viewer IDs stay the same.</summary>
    public void Invalidate(PlayerSession player) => _players.Remove(player);

    public static Visibility? Read(World world, Creature viewer, Creature target)
    {
        // Missing snapshots and unmeasured categories retain their existing presentation.
        // In particular a dead observer or corpse must not disappear because perception skipped it.
        if (viewer.Area == null || viewer.Area != target.Area || viewer.Society.PerceptionArea != viewer.Area.Id
            || !viewer.IsPC || viewer.IsDMAvatar || viewer.InCutscene || SocietyService.IsDead(viewer)
            || target.IsPC || target.IsDMAvatar || SocietyService.IsDead(target)
            || world.System<Skills.SkillSystem>().AlwaysPerceives(viewer, target)) return null;
        return new(viewer.Society.Seen.Contains(target.Id), viewer.Society.Heard.Contains(target.Id));
    }

    public void Step(float dt)
    {
        _timer -= dt;
        if (_timer > 0 || world.Output == null) return;
        _timer = PerceptionSystem.PlayerInterval;
        var connected = new HashSet<PlayerSession>();
        foreach (var pc in world.ConnectedPCs)
        {
            if (pc.Player is not { } player) continue;
            connected.Add(player);
            if (pc.Area is not { } area) { _players.Remove(player); continue; }
            bool reset = !_players.TryGetValue(player, out var state) || state.Area != area.Id || state.Viewer != pc.Id;
            if (reset) _players[player] = state = new State { Area = area.Id, Viewer = pc.Id };
            var changes = new Dictionary<string, object?>();
            var present = new HashSet<uint>();
            foreach (var target in area.Objects.OfType<Creature>())
            {
                if (target.IsDestroyed || !player.Known.Contains(target.Id)
                    || target.IsDMUndetectable && !player.IsDM && target.DMOwner != player) continue;
                present.Add(target.Id);
                var value = Read(world, pc, target);
                if (state!.Sent.TryGetValue(target.Id, out var old) && old == value) continue;
                state.Sent[target.Id] = value;
                changes[target.Id.ToString()] = value;
            }
            foreach (var id in state!.Sent.Keys.Where(id => !present.Contains(id)).ToArray())
            {
                state.Sent.Remove(id);
                changes[id.ToString()] = null;
            }
            if (reset) changes["reset"] = true;
            if (changes.Count > 0) world.Output.Notify("creature-visibility", pc, changes, pc);
        }
        foreach (var player in _players.Keys.Where(p => !connected.Contains(p)).ToArray()) _players.Remove(player);
    }
}
