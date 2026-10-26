using OpenWinter.Game.Objects;
using OpenWinter.Game.Rules;

namespace OpenWinter.Game.Players;

/// <summary>Reliable, viewer-scoped creature labels. Exact NPC hit points never leave the server here.</summary>
public sealed class CreatureLabelFeed : IWorldSystem
{
    readonly World _world;
    readonly Dictionary<PlayerSession, State> _players = new();
    float _timer;
    sealed class State
    {
        public uint Area;
        public readonly Dictionary<uint, Label> Sent = new();
    }
    public sealed record Label(string name, string playerName, int damageLevel);
    public CreatureLabelFeed(World world) { _world = world; }
    public int Order => 102;

    public void Step(float dt)
    {
        _timer -= dt;
        if (_timer > 0 || _world.Output == null) return;
        _timer = 0.25f;
        var connected = new HashSet<PlayerSession>();
        foreach (var pc in _world.ConnectedPCs)
        {
            if (pc.Player is not { } player) continue;
            connected.Add(player);
            if (pc.Area is not { } area) { _players.Remove(player); continue; }
            bool reset = !_players.TryGetValue(player, out var state) || state.Area != area.Id;
            if (reset) _players[player] = state = new State { Area = area.Id };
            var changes = new Dictionary<string, object>();
            var visible = new HashSet<uint>();
            foreach (var c in area.Objects.OfType<Creature>())
            {
                if (c.IsDestroyed || !player.Known.Contains(c.Id) || c.IsDMUndetectable && !player.IsDM && c.DMOwner != player) continue;
                visible.Add(c.Id);
                var label = new Label(c.DisplayName(_world.Resources.Talk), c.Player?.PlayerName ?? "", DamageLevel(_world, c));
                if (state!.Sent.TryGetValue(c.Id, out var old) && old == label) continue;
                state.Sent[c.Id] = label;
                changes[c.Id.ToString()] = label;
            }
            // A hidden/despawned creature must get a fresh snapshot if it becomes visible again.
            foreach (var id in state!.Sent.Keys.Where(id => !visible.Contains(id)).ToList())
            {
                state.Sent.Remove(id);
                changes[id.ToString()] = null!;
            }
            if (reset) changes["reset"] = true;
            if (changes.Count > 0) _world.Output.Notify("creature-labels", pc, changes, pc);
        }
        foreach (var p in _players.Keys.Where(p => !connected.Contains(p)).ToList()) _players.Remove(p);
    }

    /// <summary>Index into damagelevels.2da, including temporary HP; measured by the nameplates visual scene.</summary>
    public static int DamageLevel(World world, Creature c) => Nwnx.Plugins.RisenholmPlugin.ShiftDamageLevel(world, c, HealthLevel(world, c));

    static int HealthLevel(World world, Creature c)
    {
        if (c.IsDead) return 5;
        long hp = CreatureRules.CurrentHitPoints(world, c) + (long)CreatureRules.TemporaryHitPoints(c);
        long max = Math.Max(1, c.MaxHitPoints);
        if (hp * 100 >= max * 95) return 0;
        if (hp * 4 >= max * 3) return 1;
        if (hp * 2 >= max) return 2;
        if (hp * 4 >= max) return 3;
        return 4;
    }
}
