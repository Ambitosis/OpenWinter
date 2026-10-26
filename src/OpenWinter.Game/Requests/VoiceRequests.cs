using OpenWinter.Content;
using OpenWinter.Game.Society;
using OpenWinter.Game.WorldObjects;
namespace OpenWinter.Game.Requests;

static class VoiceRequests
{
    [ClientRequest("voice-chat")]
    static void Play(Request r)
    {
        int slot = r.Int("id", -1);
        if (!QuickChat.IsSelectable(slot) || r.Player.IsDead || r.Player.Area == null) return;
        if (slot == 1) slot += Random.Shared.Next(3);
        // NWNX_ON_QUICKCHAT (QUICKCHAT_COMMAND a VOICE_CHAT_* constant); a skip says nothing.
        var ev = Nwnx.NwnxEvent.Begin(r.World, "NWNX_ON_QUICKCHAT", r.Player)?.Add("QUICKCHAT_COMMAND", slot);
        if (ev?.Before() == true) { ev.After(); return; }
        using var after = new Nwnx.NwnxEvent.AfterScope(ev);
        VoiceService.Play(r.World, r.Player, slot);
        int command = QuickChat.AssociateCommand(slot);
        if (command != 0)
            foreach (var associate in AssociateService.AllAssociates(r.World, r.Player).ToArray())
                AssociateService.IssueCommand(r.World, r.Player, associate, command);
    }
}
