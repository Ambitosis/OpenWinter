using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    async Task TraceVisualMovement(string dir)
    {
        using var trace = new System.IO.StreamWriter(System.IO.Path.Combine(dir, "movement.csv"));
        trace.WriteLine("seconds,x,y,speed,animation_state,transitions,mouse_drive");
        ulong start = Time.GetTicksMsec();
        while (!_visualDone)
        {
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            if (_session.Controlled is not Views.CreatureView { Animator: { } animator } pc) continue;
            trace.WriteLine(FormattableString.Invariant($"{(Time.GetTicksMsec() - start) / 1000.0:F3},{pc.Position.X:F4},{pc.Position.Y:F4},{animator.GroundSpeed:F4},{animator.DriveMode},{animator.DriveTransitions},{(_interaction.MouseDriveTarget != null ? 1 : 0)}"));
        }
    }
}
