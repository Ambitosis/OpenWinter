using System.Numerics;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Inventory;

/// <summary>An action whose work is the low reach (getlow): putting an item or gold down, or picking an item up.</summary>
public interface IGroundReach
{
    /// <summary>Whether, started now, it would bend where the creature stands rather than walk somewhere first.</summary>
    bool ReachesWhereItStands(World world, GameObject owner);
}

/// <summary>
/// The low reach's "object-use" notice. The original keeps a creature bent through consecutive queued reaches: three
/// put-downs, three pick-ups or put-down, pick-up, put-down each take 1.5 s and she stays low for all 4.5 s, standing
/// once after the last; a put-down issued 0.2 s after the previous one landed has her start to stand and bend again
/// (visual scene dropchain). The notice's <c>chain</c> tells the client another reach follows straight on, so it holds
/// the low loop across the gap until that reach's own notice arrives instead of starting to stand. A reach queued while
/// one is already bending upgrades it with a second notice for the remaining time.
/// </summary>
public sealed class GroundReachNotice
{
    bool _sent, _chained;

    /// <summary>Sends the notice on the first call, and again once if a reach is queued behind it later.</summary>
    public void Update(World world, Creature creature, uint target, float remaining)
    {
        bool chained = FollowedByReach(world, creature);
        if (_sent && (_chained || !chained)) return;
        _sent = true;
        _chained = chained;
        world.Output?.Notify("object-use", creature, new { target, animation = "getlow", duration = Math.Max(0, remaining), chain = chained });
    }

    /// <summary>
    /// The reach was cancelled (a move, ClearAllActions, its item gone) after its notice: the client ends the bend now
    /// rather than at its time, which a chained hold would lengthen. Moving ends it on the client too, but keys
    /// against a wall cancel the queue without moving.
    /// </summary>
    public void Cancel(World world, GameObject owner)
    {
        if (!_sent || owner is not Creature creature) return;
        _sent = false;
        world.Output?.Notify("object-use", creature, new { target = GameObject.InvalidId, animation = "getlow", duration = 0f, chain = false, cancelled = true });
    }

    /// <summary>Whether the action after the running one (queue[0]) is a reach where she stands. Script commands in
    /// between run in the same tick (the dropchain scene queued an ActionDoCommand after each put-down).</summary>
    public static bool FollowedByReach(World world, GameObject owner)
    {
        var queue = owner.Actions;
        for (int i = 1; i < queue.Count; i++)
        {
            if (queue[i] is DoCommandAction) continue;
            return queue[i] is IGroundReach reach && reach.ReachesWhereItStands(world, owner);
        }
        return false;
    }

    /// <summary>A put-down or gold drop with a chosen spot walks there unless it is within a metre.</summary>
    internal static bool Near(GameObject owner, Location? destination) =>
        destination is not { } target || target.Area == owner.Area?.Id
            && Vector2.Distance(new(target.Position.X, target.Position.Y), new(owner.Position.X, owner.Position.Y)) <= 1.001f;
}
