using System.Numerics;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Society;

/// <summary>GetNearestCreature / GetNearestCreatureToLocation with their CREATURE_TYPE_* criteria.</summary>
public static class NearestCreature
{
    public const int RacialType = 0, PlayerChar = 1, Class = 2, Reputation = 3, IsAlive = 4, HasSpellEffect = 5, DoesNotHaveSpellEffect = 6, Perception = 7;
    public const int RacialTypeAll = 28;
    public const int RepFriend = 0, RepEnemy = 1, RepNeutral = 2;
    public const int SeenAndHeard = 0, NotSeenAndNotHeard = 1, HeardAndNotSeen = 2, SeenAndNotHeard = 3, NotHeard = 4, Heard = 5, NotSeen = 6, Seen = 7;

    /// <summary>
    /// The <paramref name="nth"/> (1-based; less than 1 means 1) nearest creature to <paramref name="from"/> in
    /// <paramref name="area"/> meeting every criterion (a class criterion only counts in the first place), the newest first on ties
    /// (the original's order for exact ties varies from run to run). Dead creatures only count when a
    /// criterion asks for them (CREATURE_TYPE_IS_ALIVE, FALSE). Reputation and perception criteria are judged from
    /// <paramref name="perspective"/> (whose feelings and whose perception lists count); <paramref name="exclude"/>
    /// is never returned.
    /// </summary>
    public static Creature? Find(World world, Area? area, Vector3 from, GameObject? exclude, GameObject? perspective, int nth,
        params (int Type, int Value)[] criteria)
    {
        if (area == null) return null;
        nth = Math.Max(nth, 1);
        bool wantDead = criteria.Any(x => x.Type == IsAlive && x.Value == 0);
        var candidates = new List<(Creature C, float D)>();
        foreach (var o in area.Objects)
        {
            if (o is not Creature c || c == exclude || c.IsDestroyed) continue;
            if (!wantDead && SocietyService.IsDead(c)) continue;
            bool ok = true;
            for (int i = 0; i < criteria.Length; i++)
            {
                var (type, value) = criteria[i];
                // The original ignores a class criterion in the second and third places (nwserver ai_queries), which
                // makes nw_i0_generic's "hated class" target choice fall back to the nearest seen enemy.
                if (type < 0 || type == Class && i > 0) continue;
                if (!Matches(world, c, perspective, type, value)) { ok = false; break; }
            }
            if (ok) candidates.Add((c, Vector3.DistanceSquared(c.Position, from)));
        }
        if (candidates.Count < nth) return null;
        candidates.Sort((a, b) => a.D != b.D ? a.D.CompareTo(b.D) : b.C.Id.CompareTo(a.C.Id));
        return candidates[nth - 1].C;
    }

    public static bool Matches(World world, Creature c, GameObject? perspective, int type, int value)
    {
        switch (type)
        {
            case RacialType:
                return value == RacialTypeAll || c.Race == value;
            case PlayerChar:
                return c.IsPC == (value != 0);
            case Class:
                return c.Classes.Any(k => k.Class == value && k.Level > 0);
            case Reputation:
            {
                if (perspective == null) return false;
                int rep = world.Factions.ReputationOf(perspective, c);
                if (rep < 0) return false;
                return value switch
                {
                    RepFriend => rep >= FactionTable.FriendFrom,
                    RepEnemy => rep <= FactionTable.HostileTo,
                    RepNeutral => rep > FactionTable.HostileTo && rep < FactionTable.FriendFrom,
                    _ => false,
                };
            }
            case IsAlive:
                return SocietyService.IsDead(c) != (value != 0);
            case HasSpellEffect:
                return PerceptionSystem.Effects(c).Any(e => e.SpellId == value);
            case DoesNotHaveSpellEffect:
                return !PerceptionSystem.Effects(c).Any(e => e.SpellId == value);
            case Perception:
            {
                var p = perspective as Creature;
                bool seen = p != null && p.Society.Seen.Contains(c.Id);
                bool heard = p != null && p.Society.Heard.Contains(c.Id);
                return value switch
                {
                    SeenAndHeard => seen && heard,
                    NotSeenAndNotHeard => !seen && !heard,
                    HeardAndNotSeen => heard && !seen,
                    SeenAndNotHeard => seen && !heard,
                    NotHeard => !heard,
                    Heard => heard,
                    NotSeen => !seen,
                    Seen => seen,
                    _ => false,
                };
            }
            default:
                return false; // an unknown criterion matches nothing
        }
    }
}
