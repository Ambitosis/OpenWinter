using OpenWinter.Game.Objects;
using OpenWinter.Game.Society;

namespace OpenWinter.Game.Requests;

/// <summary>
/// Orders from an associate's own radial menu (and its party portrait's): { target, command = ASSOCIATE_COMMAND_* }.
/// Only the requesting player's own associate can be ordered, and only with a command its radial offers
/// (docs/ASSOCIATE_COMMANDS.md); the client cannot name another master.
/// </summary>
static class AssociateRequests
{
    [ClientRequest("associate-command")]
    static void Command(Request r)
    {
        if (r.Obj<Creature>("target") is not { } associate || associate.Area == null || r.Player.Area == null) return;
        AssociateService.Command(r.World, r.Player, associate, r.Int("command"));
    }
}
