using OpenWinter.Client.UI;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --connect-nwn ws://host:port --character resref --capture-nwn-party dir: on an NWN:EE server with a second player
    /// (who answers from elsewhere), opens the player list, invites the first other player and waits for her to join,
    /// passes her the leadership, dislikes and likes her, opens the radial's party ring on her, then waits for an
    /// invitation from her (once she has removed this player) and accepts it, with a shot at each step.
    /// </summary>
    async Task CaptureNwnParty(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        var list = GetTree().Root.FindChildren("*", "", true, false).OfType<PlayerListPanel>().First();
        for (int i = 0; i < 600 && (_session.Controlled == null || _session.Objects.Values.All(v => v.Kind != ObjectKind.Creature || v.ObjectId == _session.ControlledId)); i++) await Wait(0.1);
        await Wait(3);
        list.Toggle();
        for (int i = 0; i < 100 && list.State.Players.Count == 0; i++) await Wait(0.1);
        await Wait(1);
        await Shot(dir, "01_list");
        if (list.State.Players.FirstOrDefault() is not { } other)
        {
            GD.PrintErr("[nwn] nobody else on the list");
            GetTree().Quit(1);
            return;
        }
        GD.Print($"[nwn] inviting {other.Name} ({other.Id})");
        list.Invite(other.Id);
        await Wait(2);
        await Shot(dir, "02_invited");
        for (int i = 0; i < 300 && !list.State.InParty; i++) await Wait(0.1);
        await Wait(1.5);
        await Shot(dir, "03_joined");
        list.Transfer(other.Id);
        await Wait(2);
        await Shot(dir, "04_leader");
        list.SetLike(other.Id, false);
        await Wait(2);
        await Shot(dir, "05_dislike");
        list.SetLike(other.Id, true);
        await Wait(1);
        list.Toggle();
        if (_session.Objects.TryGetValue(other.Id, out var view) && GetViewport().GetCamera3D() is { } camera)
        {
            var radial = GetTree().Root.FindChildren("*", "", true, false).OfType<RadialMenu>().First();
            radial.Open(view, camera.UnprojectPosition(view.GlobalPosition + Vector3.Up * 1.1f));
            await Wait(1);
            if (radial.Choose("Party")) await Wait(0.6);
            await Shot(dir, "06_radial_party");
            GD.Print($"[nwn] party ring: {string.Join(", ", radial.Labels)}");
            radial.Close();
        }
        for (int i = 0; i < 400 && list.OpenInvitation == 0; i++) await Wait(0.1);
        await Wait(1);
        await Shot(dir, "07_invitation");
        list.AnswerOpenInvitation("accept");
        await Wait(2);
        list.Toggle();
        await Wait(1);
        await Shot(dir, "08_rejoined");
        GD.Print($"[nwn] in party: {list.State.InParty}, players: {string.Join(", ", list.State.Players.Select(p => p.Name))}");
        GetTree().Quit();
    }
}
