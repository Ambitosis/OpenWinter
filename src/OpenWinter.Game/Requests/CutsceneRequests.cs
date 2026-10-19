using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Requests;

static class CutsceneRequests
{
    [ClientRequest("cutscene-abort")]
    static void Abort(Request r)
    {
        // The bound session supplies the player. Modules decide how to
        // handle Escape; neither a no-op/missing hook nor this request ends the mode.
        if (!r.Player.IsPC || r.Player.IsDestroyed || !r.Player.InCutscene) return;
        r.World.Module.LastCutsceneCanceller = r.Player.Id;
        r.World.RunEvent(r.World.Module, "Mod_OnCutsnAbort",
            new EventContext().Set("CutsceneCanceller", r.Player.Id));
    }
}
