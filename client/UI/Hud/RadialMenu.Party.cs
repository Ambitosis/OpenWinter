using System.Text.Json;
using OpenWinter.Client.Views;

namespace OpenWinter.Client.UI;

public partial class RadialMenu
{
    /// <summary>A field of the target's "party" facts (another player; docs/PARTY.md).</summary>
    bool PartyFact(string name) =>
        Facts is { } f && f.TryGetProperty("party", out var p) && p.ValueKind == JsonValueKind.Object
        && p.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

    /// <summary>
    /// The Party wedge on another player (dialog.tlk 6382, ir_party) and its ring: Invite Into Party (1067), Accept /
    /// Reject / Ignore Invitations from (6386 / 6385 / 6384) while she has invited you, Remove From Party (1068) and
    /// Transfer Leadership (6383) for the leader, and Leave Party (1071) in her party. The entries and their icons are the
    /// game's own strings and ir_* textures; their places on the ring are inferred, not measured (one original client
    /// can't show another player's radial; docs/PARTY.md).
    /// </summary>
    RadialEntry? PartyEntry(ObjectView t)
    {
        if (!PartyFact("player")) return null;
        uint id = t.ObjectId;
        RadialEntry Act(uint strref, string fallback, string icon, bool enabled, string kind, object data) => Icon(new RadialEntry
        {
            Label = Tlk(strref, fallback), Glyph = "party", Disabled = !enabled, Act = () => Session.Request(kind, data),
        }, icon);
        bool invitedMe = PartyFact("invitedMe");
        var ring = FixedRing(
            (0, Act(1067, "Invite Into Party", "ir_invite", PartyFact("canInvite") && !PartyFact("invitedThem"), "party-invite", new { other = id })),
            (1, Act(6386, "Accept Invitation from", "ir_accept", invitedMe, "party-answer", new { from = id, answer = "accept" })),
            (2, Act(6385, "Reject Invitation from", "ir_reject", invitedMe, "party-answer", new { from = id, answer = "reject" })),
            (3, Act(6384, "Ignore Invitations from", "ir_ignore", invitedMe, "party-answer", new { from = id, answer = "ignore" })),
            (4, Act(1071, "Leave Party", "ir_leave", PartyFact("sameParty") && !PartyFact("oneParty"), "party-leave", new { })),
            (5, Act(1068, "Remove From Party", "ir_kick", PartyFact("canKick"), "party-kick", new { other = id })),
            (6, Act(6383, "Transfer Leadership", "ir_transleader", PartyFact("canTransfer"), "party-leader", new { other = id })));
        return Icon(new RadialEntry { Label = Tlk(6382, "Party"), Glyph = "party", Open = () => ring }, "ir_party");
    }
}
