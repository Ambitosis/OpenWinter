using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    /// <summary>
    /// --connect-nwn ws://host:port --character resref --capture-nwn dir: on an NWN:EE server, waits for the area and the
    /// controlled character, then walks three metres and takes shots on arrival, mid-walk and after; then drives
    /// forward with the keyboard for a second and a half, and clicks the nearest door.
    /// </summary>
    async Task CaptureNwn(string dir)
    {
        System.IO.Directory.CreateDirectory(dir);
        for (int i = 0; i < 600 && (_session.Area == null || _session.Controlled == null); i++) await Wait(0.1);
        if (_session.Area == null || _session.Controlled == null)
        {
            GD.PrintErr("[nwn] no area or controlled character after 60 s");
            GetTree().Quit(1);
            return;
        }
        GD.Print($"[nwn] in {_session.Area.Info.ResRef} with {_session.Objects.Count} objects; controlling {_session.ControlledId}");
        await Wait(2);
        await Shot(dir, "01_arrived");
        var start = _session.Controlled.NetPosition;
        _session.Send(new ActionRequestMessage { Action = ActionKind.MoveTo, Position = new System.Numerics.Vector3(start.X + 3, start.Y + 3, start.Z), Run = false }.Write());
        await Wait(1);
        await Shot(dir, "02_walking");
        await Wait(3);
        await Shot(dir, "03_arrived_at_target");
        GD.Print($"[nwn] moved from {start} to {_session.Controlled.NetPosition}");
        var before = _session.Controlled.NetPosition;
        // The forward key, held as the player would (the input handler reads the action's state each frame).
        Input.ActionPress(Preferences.ActionCatalog.MoveForward);
        await Wait(1.5);
        await Shot(dir, "04_driving");
        Input.ActionRelease(Preferences.ActionCatalog.MoveForward);
        await Wait(1);
        GD.Print($"[nwn] drove from {before} to {_session.Controlled.NetPosition}");
        var here = _session.Controlled.NetPosition;
        if (_session.Objects.Values.Where(o => o.Kind == ObjectKind.Door).OrderBy(o => o.NetPosition.DistanceTo(here)).FirstOrDefault() is { } door)
        {
            var open = (door.Flags & ObjectFlags.Open) != 0;
            _session.Send(new ActionRequestMessage { Action = ActionKind.Use, Target = door.ObjectId }.Write());
            for (int i = 0; i < 100 && ((door.Flags & ObjectFlags.Open) != 0) == open; i++) await Wait(0.1);
            await Wait(1.5);
            await Shot(dir, "05_door");
            GD.Print($"[nwn] door {door.ObjectId} was {(open ? "open" : "closed")}, now {((door.Flags & ObjectFlags.Open) != 0 ? "open" : "closed")}");
        }
        GetTree().Quit();
    }
}
