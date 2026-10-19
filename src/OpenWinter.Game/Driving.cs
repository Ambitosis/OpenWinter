using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Net;

namespace OpenWinter.Game;

/// <summary>Native tank steering, measured by the driving visual scene. Camera orientation is never an input.</summary>
public static class Driving
{
    // Approximate fits to native GetFacing samples (integer degrees, asynchronous posted keys).
    public const float TurnRate = 2.2f, RunningTurnRate = 2.6f;

    public static void Receive(World world, Creature c, DriveInputMessage input)
    {
        if (!float.IsFinite(input.Direction.X) || !float.IsFinite(input.Direction.Y) || !float.IsFinite(input.Direction.Z) || !float.IsFinite(input.Turn)) return;
        if (!input.Active)
        {
            if (c.DriveInput is { } previous)
            {
                // Releasing a keyboard move finishes its last step. Mouse release still stops.
                // drivetaps: W 1.6 m, S 0.8 m (0.4 s); Q/E 0.8 m (0.5 s).
                if (!previous.Mouse && (previous.Direction != Vector3.Zero && c.DriveStepRemaining > 0 || c.DriveTurnRemaining > 0))
                    c.DriveReleased = true;
                else Movement.Stop(c);
                world.MarkChanged(c);
            }
            return;
        }
        if (c.IsDead || !c.Commandable || Rules.CreatureRules.IsHelpless(c)) return;
        if (c.Actions.Count > 0) ActionQueues.Clear(world, c);
        c.MoveGoal = null;
        input.Turn = Math.Clamp(input.Turn, -1, 1);
        if (!input.Mouse && input.Turn != 0 && Math.Sign(input.Turn) != Math.Sign(c.DriveReleased ? 0 : c.DriveInput?.Turn ?? 0))
        {
            // A tap completes thirty degrees, but every input edge shares the held-turn rate.
            // Renew the remaining turn; never grant instantaneous or accumulated rotation.
            c.DriveTurnRemaining = MathF.PI / 6;
        }
        if (input.Mouse || input.Turn == 0) c.DriveTurnRemaining = 0;
        if (!input.Mouse) input.Direction = Vector3.Clamp(input.Direction, new(-1, -1, 0), new(1, 1, 0));
        if (c.DriveInput is not { } old || c.DriveReleased || input.Mouse != old.Mouse || input.Direction != old.Direction || input.Walk != old.Walk)
        {
            c.DriveHeldTime = 0;
            c.DriveStepRemaining = StepSeconds(input);
        }
        c.DriveReleased = false;
        c.DriveInput = input;
        c.DriveExpires = world.Time + 0.75;
    }

    static float StepSeconds(DriveInputMessage input) => input.Direction.Y == 0 ? 0.5f : 0.4f;

    public static void Step(World world, Creature c, float dt)
    {
        var input = c.DriveInput!;
        if (c.DriveReleased && (input.Direction == Vector3.Zero || c.DriveStepRemaining <= 0.00001f) && c.DriveTurnRemaining <= 0.00001f)
        {
            Movement.Stop(c);
            c.Speed = 0;
            c.Mode = MovementMode.Stand;
            world.MarkChanged(c);
            return;
        }
        float moveDt = dt;
        if (!input.Mouse && input.Direction != Vector3.Zero)
        {
            // A held key continually renews the next step (driveholds), rather than
            // quantizing release to a multiple of the first step. Don't renew a quick tap.
            if (!c.DriveReleased && c.DriveHeldTime >= 0.05f) c.DriveStepRemaining = StepSeconds(input);
            c.DriveHeldTime += dt;
            moveDt = Math.Min(dt, c.DriveStepRemaining);
            c.DriveStepRemaining = Math.Max(0, c.DriveStepRemaining - moveDt);
        }
        float forward = input.Direction.Y, right = input.Direction.X;
        c.WalkRequested = input.Walk || !input.Mouse && forward <= 0;
        float speed = Movement.MaxSpeed(world, c, out bool walking);
        var mode = MovementMode.Stand;
        Vector2 direction;
        if (input.Mouse)
        {
            var offset = new Vector2(input.Direction.X - c.Position.X, input.Direction.Y - c.Position.Y);
            float distance = offset.Length();
            if (distance < 0.1f || c.Area!.Walkmesh.GroundHeight(input.Direction.X, input.Direction.Y, input.Direction.Z, 0.9f) == null)
                direction = Vector2.Zero;
            else
            {
                // Native GetFacing changes to the mouse heading immediately; the rendered model
                // takes time to catch up. Translate toward the pointer while the client turns it.
                c.Facing = MathF.Atan2(offset.Y, offset.X);
                direction = offset / distance;
                // Fit the measured acceleration with the existing arrival-braking curve toward
                // the continually repicked nearby destination. A short arrow can
                // therefore be running; it is not the walk toggle (mousecal/mouseclips).
                speed = Math.Min(c.Speed + speed * dt, Movement.BrakingStep(speed, distance, dt) / dt);
                mode = walking || speed <= c.WalkRate ? MovementMode.MouseWalk : MovementMode.MouseRun;
            }
        }
        else
        {
            float turn = Math.Abs(input.Turn) * (forward > 0 && !walking ? RunningTurnRate : TurnRate) * dt;
            if (c.DriveReleased) turn = Math.Min(turn, c.DriveTurnRemaining);
            c.DriveTurnRemaining = Math.Max(0, c.DriveTurnRemaining - turn);
            c.Facing += Math.Sign(input.Turn) * turn;
            c.Facing = MathF.IEEERemainder(c.Facing, MathF.Tau);
            if (moveDt <= 0) forward = right = 0;
            var f = new Vector2(MathF.Cos(c.Facing), MathF.Sin(c.Facing));
            direction = f * forward + new Vector2(f.Y, -f.X) * right;
            if (direction.LengthSquared() > 1) direction = Vector2.Normalize(direction);
            if (forward == 0 && right != 0)
            {
                speed *= 0.8f;
                mode = right < 0 ? MovementMode.StrafeLeft : MovementMode.StrafeRight;
            }
            else if (forward < 0) mode = MovementMode.Backward;
            else if (forward > 0)
                mode = right == 0 ? walking ? MovementMode.Walk : MovementMode.Run
                    : walking ? right < 0 ? MovementMode.WalkForwardLeft : MovementMode.WalkForwardRight
                    : right < 0 ? MovementMode.RunForwardLeft : MovementMode.RunForwardRight;
            else if (turn > 0) mode = input.Turn > 0 ? MovementMode.TurnLeft : MovementMode.TurnRight;
        }
        c.Speed = 0;
        c.MoveDirection = new Vector3(direction, 0);
        if (direction.LengthSquared() > 0 && c.RunRate > 0)
        {
            var step = direction * speed * moveDt;
            Vector3? Try(Vector2 amount) => Movement.Step(c.Area!, c.Position, amount) is { } p && PersonalSpace.Step(world, c, p) == null ? p : null;
            var next = Try(step) ?? Try(new(step.X, 0)) ?? Try(new(0, step.Y));
            if (next is { } point)
            {
                c.Speed = Vector3.Distance(c.Position, point) / dt;
                c.Position = point;
                c.Area!.CreatureGrid.Update(c);
            }
            else mode = MovementMode.Stand;
        }
        c.Mode = mode;
        world.MarkChanged(c);
    }
}
