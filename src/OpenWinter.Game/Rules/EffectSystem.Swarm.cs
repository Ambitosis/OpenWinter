using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Rules;

/// <summary>
/// EffectSwarm(nLooping, sTemplate1..4), as measured on nwserver (parity suite cond_misc, swarm.*):
/// - The effect stays listed on its target (EFFECT_TYPE_SWARM) and summons the first template at once, a summoned
///   associate in the target's faction beside it (0.9 m off for a goblin).
/// - Each time the current creature dies the next template comes (within 1.5 s; once within 0.2 s). After the last
///   one, a looping swarm starts again with the first; otherwise the effect ends.
/// - When the effect ends (expiry, removal) the creature it summoned last goes.
/// Templates are stored in Strings[0..3], looping in Ints[0]; which template is out is kept here.
/// </summary>
public sealed partial class EffectSystem
{
    readonly Dictionary<long, (GameObject Owner, Effect Effect, int Index)> _swarms = new();

    void StartSwarm(Effect e, Creature owner)
    {
        if (e.Hidden || owner.IsDead) return;
        _swarms[e.UniqueId] = (owner, e, -1);
        NextSwarmer(e.UniqueId);
    }

    /// <summary>Summons the swarm's next creature, or ends a swarm that has run out.</summary>
    void NextSwarmer(long id)
    {
        if (!_swarms.TryGetValue(id, out var sw)) return;
        var (owner, e, index) = sw;
        int next = index + 1;
        if (next >= 4 || e.Strings[next].Length == 0)
        {
            if (e.Ints[0] == 0 || index < 0) { _swarms.Remove(id); RemoveLinks(owner, new[] { e }); return; }
            next = 0;
        }
        _swarms[id] = (owner, e, next);
        if (owner is not Creature master || master.Area is not { } area) return;
        int summoned = K["ASSOCIATE_TYPE_SUMMONED"];
        // Like any summoning, it replaces the master's current summon.
        foreach (var old in _world.AllObjects.OfType<Creature>().Where(x => x.Master == master.Id && x.AssociateType == summoned && !x.IsDestroyed && !x.IsDead).ToList())
            Unsummon(old, -1);
        if (Scripting.Functions.ObjectFunctions.Spawn(_world, (int)ObjectType.Creature, e.Strings[next], area, master.Position, master.Facing) is not Creature c)
        {
            // A template that doesn't exist ends the swarm's summoning (it stays listed).
            e.SummonedObject = GameObject.InvalidId;
            _swarms.Remove(id);
            return;
        }
        c.Master = master.Id;
        c.AssociateType = summoned;
        c.Faction = master.Faction;
        e.SummonedObject = c.Id;
    }

    /// <summary>Polled each update: a swarm whose creature died sends the next.</summary>
    void StepSwarms()
    {
        if (_swarms.Count == 0) return;
        foreach (var (id, (owner, e, _)) in _swarms.ToList())
        {
            if (owner.IsDestroyed || !owner.Effects.Contains(e)) { _swarms.Remove(id); continue; }
            var c = _world.Get<Creature>(e.SummonedObject);
            if (c == null || c.IsDead) NextSwarmer(id);
        }
    }

    void EndSwarm(Effect e)
    {
        _swarms.Remove(e.UniqueId);
        if (_world.Get<Creature>(e.SummonedObject) is { IsDead: false } c) Unsummon(c, -1);
        e.SummonedObject = GameObject.InvalidId;
    }
}
