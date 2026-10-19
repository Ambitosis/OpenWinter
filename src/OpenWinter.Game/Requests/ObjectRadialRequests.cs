using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.WorldObjects;

namespace OpenWinter.Game.Requests;

/// <summary>Object radial lock commands use the same actions as public NWScript.</summary>
static class ObjectRadialRequests
{
    [ClientRequest("lock-object")]
    static void Lock(Request request)
    {
        var target = request.World.Get(request.Id("target"));
        if (target is not ILockable state || target is not (Door or Placeable)
            || target.Area == null || target.Area != request.Player.Area || target.IsDestroyed
            || !ActionQueues.Accepts(request.Player)) return;
        bool locked = request.Bool("locked");
        if (locked && !state.Lock.Lockable) return;
        ActionQueues.EnqueuePlayerTask(request.World, request.Player, locked ? new LockAction { Target = target.Id } : new UnlockAction { Target = target.Id });
    }
}
