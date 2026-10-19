using OpenWinter.Game.Conversations;

namespace OpenWinter.Game.Requests;

/// <summary>The radial Craft command opens the installed craft-skills conversation on the requesting PC.</summary>
static class CraftingRequests
{
    [ClientRequest("craft")]
    static void Craft(Request r) => r.World.System<ConversationService>().Start(r.Player, r.Player, "x0_skill_ctrap");
}
