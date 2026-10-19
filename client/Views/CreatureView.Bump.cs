using System.Text.Json;
using OpenWinter.Client.Gameplay.Logic;
using Godot;
using N = System.Numerics;

namespace OpenWinter.Client.Views;

public partial class CreatureView
{
    readonly BumpMotion _bump = new();
    /// <summary>The object a combat sidestep keeps facing while it plays.</summary>
    ObjectView? _bumpFace;

    public void Bump(string json, Func<uint, ObjectView?> find)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        static N.Vector3 V(JsonElement a) => new(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());
        var from = V(root.GetProperty("from"));
        var to = V(root.GetProperty("to"));
        float facing = root.GetProperty("facing").GetSingle();
        // A combat sidestep keeps facing its target throughout (clipFacing: the facing halfway through the step).
        _bumpFace = root.TryGetProperty("face", out var face) ? find(face.GetUInt32()) : null;
        string clip = BumpMotion.Clip(to - from, root.TryGetProperty("clipFacing", out var mid) ? mid.GetSingle() : facing);
        // Humanoid cwalk clips are 0.5 s; other models use their own clip if provided.
        float duration = Animator?.BeginBump(clip) ?? 0.5f;
        _bump.Begin(new N.Vector3(Position.X, Position.Y, Position.Z), from, to, duration);
        TargetPosition = Rendering.Convert.V(to);
        TargetFacing = facing;
    }

    void ReconcileBump(OpenWinter.Net.UpdateMessage update)
    {
        if (!_bump.Active) return;
        // The ordinary position update following the event confirms its destination; it must not snap
        // the presentation there. A later walk, jump or death supersedes the transient step.
        if (N.Vector3.DistanceSquared(update.Position, _bump.Destination) < 0.000001f
            && (update.Flags & OpenWinter.Net.ObjectFlags.Dead) == 0) return;
        _bump.Cancel();
        Animator?.EndBump();
    }

    protected override void UpdatePosition(float delta)
    {
        if (_bump.Active)
        {
            Position = Rendering.Convert.V(_bump.Step(delta));
            if (_bumpFace is { } faced && IsInstanceValid(faced))
            {
                var toward = faced.Position - Position;
                if (toward.X * toward.X + toward.Y * toward.Y > 1e-4f) TargetFacing = MathF.Atan2(toward.Y, toward.X);
            }
            if (!_bump.Active) Animator?.EndBump();
        }
        else base.UpdatePosition(delta);
    }
}
