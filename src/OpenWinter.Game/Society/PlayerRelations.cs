using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Society;

/// <summary>
/// Whether two players like or dislike each other (SetPCLike / SetPCDislike, and the player list's PvP buttons).
/// Disliking is mutual: both get a personal reputation of 0 toward the other, so they may fight where the
/// server's PvP setting allows it. Both players are told with a "pc-like" notify.
/// </summary>
public static class PlayerRelations
{
    public static void SetLike(World world, Creature a, Creature b, bool like)
    {
        if (a == b) return;
        if (like)
        {
            world.Factions.ClearPersonal(a, b);
            world.Factions.ClearPersonal(b, a);
            a.Dislikes.Remove(b.Id);
            b.Dislikes.Remove(a.Id);
        }
        else
        {
            world.Factions.SetPersonal(a, b, 0);
            world.Factions.SetPersonal(b, a, 0);
            a.Dislikes.Add(b.Id);
            b.Dislikes.Add(a.Id);
        }
        // "<a> now dislikes you!" (dialog.tlk 7992) to the other player; inferred from the string, not measured.
        if (!like && world.Output != null)
            world.Output.SendMessage(b, world.TlkStringOverridden(PartyService.DislikesYou)
                .Replace("<CUSTOM0>", a.DisplayName(world.Resources.Talk)));
        world.Output?.Notify("pc-like", a, new { other = b.Id, like }, a);
        world.Output?.Notify("pc-like", b, new { other = a.Id, like }, b);
    }
}
