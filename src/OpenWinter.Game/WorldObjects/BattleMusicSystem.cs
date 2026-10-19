using OpenWinter.Game.Combat;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.WorldObjects;

/// <summary>
/// Each player's battle music, which the engine decides (no stock script calls MusicBattlePlay for combat). Measured
/// with the nwmain scenes `musicbattle`, `musicbattle2` and `audiofollow` (docs/AUDIO.md): it starts when a creature
/// attacks the player while she is not plot (even when the hit does no damage) (a hostile in view does not start it, nor does the player's own
/// attack, nor attacks on a plot player), and stops <see cref="HoldSeconds"/> after the fight: once no living creature
/// has her as its attack target any more (the original's GetIsInCombat drops the moment her attacker dies). The client
/// is told through a "battle-music" notify ({on}); MusicBattlePlay/Stop (the area-wide scripted flag) is separate.
/// </summary>
public sealed class BattleMusicSystem : IWorldSystem
{
    /// <summary>How long battle music outlives the fight (measured, see docs/AUDIO.md).</summary>
    public const double HoldSeconds = 9.0;

    readonly World _world;
    readonly Dictionary<uint, State> _states = new();
    bool _subscribed;
    public int Order => 45;

    sealed class State
    {
        public bool On;
        public double OutOfCombatSince = -1;
        public uint Area;
    }

    public BattleMusicSystem(World world) { _world = world; }

    /// <summary>Whether a player's battle music is playing (for tests and diagnostics).</summary>
    public bool IsOn(Creature pc) => _states.TryGetValue(pc.Id, out var s) && s.On;

    /// <summary>A hostile's attack on a player who is not plot starts her battle music.</summary>
    public void OnAttack(Creature attacker, GameObject target)
    {
        // Any attack counts: whoever attacks her is hostile to her by then.
        if (target is not Creature { IsPC: true } pc || pc.Plot || pc.Area == null || attacker == pc) return;
        if (!_states.TryGetValue(pc.Id, out var s)) _states[pc.Id] = s = new State();
        s.OutOfCombatSince = -1;
        if (s.On && s.Area == pc.Area.Id) return;
        s.On = true;
        s.Area = pc.Area.Id;
        _world.Output?.Notify("battle-music", pc, new { on = true }, pc);
    }

    /// <summary>A living creature in her area is attacking her.</summary>
    bool Engaged(Creature pc)
    {
        if (pc.IsDead || pc.Area == null) return false;
        var combat = _world.System<CombatSystem>();
        foreach (var o in pc.Area.Objects)
            if (o is Creature { IsDead: false, IsDestroyed: false } c && c != pc && combat.TargetOf(c) == pc.Id)
                return true;
        return false;
    }

    public void Step(float dt)
    {
        if (!_subscribed)
        {
            _subscribed = true;
            _world.System<CombatSystem>().AttackResolved += o => OnAttack(o.Context.Attacker, o.Context.Target);
        }
        if (_states.Count == 0) return;
        double now = _world.Time;
        foreach (var (id, s) in _states.ToList())
        {
            if (_world.Get(id) is not Creature { IsPC: true } pc || pc.IsDestroyed) { _states.Remove(id); continue; }
            if (!s.On) { _states.Remove(id); continue; }
            // A new area starts with its own music (the client resets it on arrival).
            if (pc.Area?.Id != s.Area) { _states.Remove(id); continue; }
            if (Engaged(pc)) { s.OutOfCombatSince = -1; continue; }
            if (s.OutOfCombatSince < 0) s.OutOfCombatSince = now;
            if (now - s.OutOfCombatSince < HoldSeconds) continue;
            s.On = false;
            _states.Remove(id);
            _world.Output?.Notify("battle-music", pc, new { on = false }, pc);
        }
    }
}
