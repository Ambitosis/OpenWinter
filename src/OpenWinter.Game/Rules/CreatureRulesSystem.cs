using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Rules;

/// <summary>
/// Keeps creatures' rules state current: reads template-only state the first time a creature
/// is in an area, and moves maximum hit points when Constitution changes (effects expiring,
/// items equipped) so current hit points follow, as in the original.
/// </summary>
public sealed class CreatureRulesSystem : IWorldSystem
{
    // Constitution rarely changes; a couple of seconds of lag on max HP is invisible and saves a full scan.
    const float Interval = 2f;
    readonly World _world;
    float _elapsed;

    public CreatureRulesSystem(World world) { _world = world; }

    public int Order => 50;

    public void Step(float dt)
    {
        _elapsed += dt;
        if (_elapsed < Interval) return;
        _elapsed = 0;
        foreach (var area in _world.Areas)
            foreach (var o in area.Objects.ToArray())
                if (o is Creature c && !c.IsDestroyed)
                {
                    CreatureRules.EnsureInit(_world, c);
                    CreatureRules.SyncHitPoints(_world, c);
                }
    }
}
