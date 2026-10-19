using System.Numerics;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Players;

/// <summary>
/// Feedback for a player's clicked order, independent of HUD selection and scripted actions. Keep the
/// exact action reference: an unrelated move later in the queue must not keep an old destination alive.
/// Flushed after actions have stepped, including while paused so replacing/cancelling an order still works.
/// </summary>
public sealed class CommandReticles(World world)
{
    sealed record Order(Creature Player, Areas.Area Area, GameAction Action, uint Target, Vector3 Position)
    {
        public bool Sent;
        public Vector3 SentAt;
    }

    readonly Dictionary<uint, Order> _orders = new();

    public void Track(Creature pc, GameAction action, Vector3 position, uint target = GameObject.InvalidId)
    {
        if (pc.Area == null) return;
        _orders[pc.Id] = new(pc, pc.Area, action, target, position);
    }

    public void Flush()
    {
        foreach (var (id, order) in _orders.ToArray())
        {
            var pc = order.Player;
            bool attack = order.Action is Combat.AttackAction;
            var target = attack ? world.Get(order.Target) : null;
            bool invalidGround = order.Action is MoveAction { TargetObject: GameObject.InvalidId }
                && order.Area.Walkmesh.GroundHeight(order.Position.X, order.Position.Y, order.Position.Z, 0.1f) == null;
            if (pc.Player?.Connection.IsConnected != true || pc.IsDestroyed || pc.IsDead || pc.Area != order.Area
                || invalidGround
                || !pc.Actions.Contains(order.Action)
                || attack && (target == null || target.IsDestroyed || target.Area != pc.Area || Rules.HitPoints.IsDead(target)))
            {
                // Send even if a replacement completed in its first tick: it must clear the previous marker.
                world.Output?.Notify("command-reticle", pc, new { kind = "clear" }, pc);
                _orders.Remove(id);
                continue;
            }
            // An approach has no planned stop until its move first steps (paused, or queued behind another
            // action), so the marker starts on the object and moves once the stop is known.
            var point = (order.Action as MoveAction)?.PlannedDestination ?? order.Position;
            if (order.Sent && Vector3.DistanceSquared(point, order.SentAt) < 0.01f) continue;
            world.Output?.Notify("command-reticle", pc, new
            {
                kind = attack ? "attack" : "move", target = order.Target,
                x = point.X, y = point.Y, z = point.Z,
            }, pc);
            order.Sent = true;
            order.SentAt = point;
        }
    }
}
