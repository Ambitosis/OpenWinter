using System.Numerics;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game;
using OpenWinter.Game.Areas;
using OpenWinter.Game.Objects;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

public sealed class DrivingTests
{
    static (World World, Creature Pc) Floor()
    {
        var resources = new ResMan(TestData.Paths);
        resources.AddBaseGame();
        var world = new World(resources);
        var area = new Area { ResRef = "drive", Width = 4, Height = 4 };
        world.Register(area); world.Areas.Add(area);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 0, 0), new(40, 40, 0), 4, true);
        area.Walkmesh.Add(new(0, 0, 0), new(40, 40, 0), new(0, 40, 0), 4, true);
        var pc = new Creature { WalkRate = 2, RunRate = 4, CurrentHitPoints = 10, MaxHitPoints = 10 };
        world.AddToArea(pc, area, new(20, 20, 0), 0);
        return (world, pc);
    }

    static void Hold(World world, Creature pc, DriveInputMessage input, int frames = 30)
    {
        input.Active = true;
        for (int i = 0; i < frames; i++)
        {
            Driving.Receive(world, pc, input);
            Movement.Step(world, pc, 1f / 30);
        }
    }

    [DataFact]
    public void TankAxesRetainFacingAndUseMeasuredSpeeds()
    {
        foreach (var (axis, expected, mode) in new[]
        {
            (new Vector3(0, 1, 0), new Vector3(24, 20, 0), MovementMode.Run),
            (new Vector3(0, -1, 0), new Vector3(18, 20, 0), MovementMode.Backward),
            (new Vector3(-1, 0, 0), new Vector3(20, 21.6f, 0), MovementMode.StrafeLeft),
            (new Vector3(1, 0, 0), new Vector3(20, 18.4f, 0), MovementMode.StrafeRight),
        })
        {
            var (world, pc) = Floor();
            Hold(world, pc, new() { Direction = axis });
            Assert.InRange(Vector3.Distance(expected, pc.Position), 0, 0.001f);
            Assert.Equal(0, pc.Facing);
            Assert.Equal(mode, pc.Mode);
        }
    }

    [DataFact]
    public void TurningInPlaceHasNoTranslationAndStrafePlusTurnCurves()
    {
        var (world, pc) = Floor();
        Hold(world, pc, new() { Turn = 1 });
        Assert.Equal(new Vector3(20, 20, 0), pc.Position);
        Assert.InRange(pc.Facing, Driving.TurnRate - 0.00001f, Driving.TurnRate + 0.00001f);
        Assert.Equal(MovementMode.TurnLeft, pc.Mode);
        pc.Facing = 0;
        Hold(world, pc, new() { Turn = 1, Direction = new(-1, 0, 0) });
        Assert.True(pc.Position.X < 19.5f && pc.Position.Y > 20.3f);
        Assert.Equal(MovementMode.StrafeLeft, pc.Mode);
    }

    [DataFact]
    public void ReleasingAndExpiredInputStopButDoNotCancelNewActions()
    {
        var (world, pc) = Floor();
        Hold(world, pc, new() { Turn = 1 });
        Driving.Receive(world, pc, new());
        float facing = pc.Facing;
        Movement.Step(world, pc, 0.1f);
        Assert.Equal(facing, pc.Facing);
        Assert.Equal(MovementMode.Stand, pc.Mode);
        pc.Actions.Add(new WaitAction { Remaining = 10 });
        Driving.Receive(world, pc, new());
        Assert.Single(pc.Actions);
        pc.Actions.Clear();
        Driving.Receive(world, pc, new() { Active = true, Turn = 1 });
        facing = pc.Facing;
        pc.DriveExpires = -1;
        Movement.Step(world, pc, 0.1f);
        Assert.Equal(facing, pc.Facing);
        Assert.Null(pc.DriveInput);
    }

    [DataFact]
    public void MouseReversalChangesTravelHeadingAndInvalidGroundDoesNotMove()
    {
        var (world, pc) = Floor();
        Hold(world, pc, new() { Mouse = true, Direction = new(5, 20, 0) }, 1);
        Assert.InRange(Math.Abs(pc.Facing), 3.14f, 3.15f);
        Assert.True(pc.Position.X < 20);
        var before = pc.Position;
        Hold(world, pc, new() { Mouse = true, Direction = new(100, 100, 0) });
        Assert.Equal(before, pc.Position);
        Assert.Equal(MovementMode.Stand, pc.Mode);
    }

    [DataFact]
    public void ReleasedKeyboardTapsCompleteTheMeasuredStepEvenBetweenTicks()
    {
        foreach (float dt in new[] { 1f / 30, 0.17f })
        foreach (var (axis, expected) in new[]
        {
            (new Vector3(0, 1, 0), new Vector3(21.6f, 20, 0)),
            (new Vector3(0, -1, 0), new Vector3(19.2f, 20, 0)),
            (new Vector3(-1, 0, 0), new Vector3(20, 20.8f, 0)),
            (new Vector3(1, 0, 0), new Vector3(20, 19.2f, 0)),
        })
        {
            var (world, pc) = Floor();
            Driving.Receive(world, pc, new() { Active = true, Direction = axis });
            Driving.Receive(world, pc, new());
            for (int i = 0; i < 35; i++)
            {
                // Idle heartbeats must not erase or extend the step already finishing.
                Driving.Receive(world, pc, new());
                Movement.Step(world, pc, dt);
            }
            Assert.InRange(Vector3.Distance(expected, pc.Position), 0, 0.0001f);
            Assert.Equal(MovementMode.Stand, pc.Mode);
            Assert.Equal(0, pc.Speed);
            Assert.Null(pc.DriveInput);
        }
    }

    [DataFact]
    public void HeldMoveFinishesItsLastStepAndDirectionChangeInterruptsIt()
    {
        var (world, pc) = Floor();
        Hold(world, pc, new() { Direction = new(0, 1, 0) }, 18);
        Driving.Receive(world, pc, new());
        for (int i = 0; i < 30; i++) Movement.Step(world, pc, 1f / 30);
        // About 0.4 s of follow-through after a 0.6 s hold, as in driveholds.
        Assert.InRange(pc.Position.X, 23.8f, 24.01f);
        Assert.Null(pc.DriveInput);

        Driving.Receive(world, pc, new() { Active = true, Direction = new(0, 1, 0) });
        Driving.Receive(world, pc, new());
        var before = pc.Position;
        Driving.Receive(world, pc, new() { Active = true, Direction = new(0, -1, 0) });
        Driving.Receive(world, pc, new());
        for (int i = 0; i < 30; i++) Movement.Step(world, pc, 1f / 30);
        Assert.InRange(before.X - pc.Position.X, 0.799f, 0.801f);
    }

    [DataFact]
    public void StepFollowThroughStillRespectsCollisionAndReplacementActions()
    {
        var (world, pc) = Floor();
        pc.Position = new(39.9f, 20, 0);
        Driving.Receive(world, pc, new() { Active = true, Direction = new(0, 1, 0) });
        Driving.Receive(world, pc, new());
        for (int i = 0; i < 30; i++) Movement.Step(world, pc, 1f / 30);
        Assert.InRange(pc.Position.X, 39.9f, 40);
        Assert.Null(pc.DriveInput);

        pc.Position = new(20, 20, 0);
        Driving.Receive(world, pc, new() { Active = true, Direction = new(0, 1, 0) });
        Driving.Receive(world, pc, new());
        pc.Actions.Add(new WaitAction { Remaining = 10 });
        Movement.Step(world, pc, 0.1f);
        Assert.Equal(new Vector3(20, 20, 0), pc.Position);
        Assert.Single(pc.Actions);
        Assert.Null(pc.DriveInput);
    }

    [DataFact]
    public void MouseReleaseDoesNotInheritKeyboardFollowThrough()
    {
        var (world, pc) = Floor();
        Hold(world, pc, new() { Mouse = true, Direction = new(25, 20, 0) }, 1);
        var before = pc.Position;
        Driving.Receive(world, pc, new());
        Movement.Step(world, pc, 0.5f);
        Assert.Equal(before, pc.Position);
        Assert.Null(pc.DriveInput);
    }

    [DataFact]
    public void HeldMouseSpeedVariesContinuouslyAndShortArrowsCanRun()
    {
        var speeds = new List<float>();
        foreach (float distance in new[] { 0.2f, 0.4f, 1f, 3f })
        {
            var (world, pc) = Floor();
            for (int i = 0; i < 60; i++)
                Hold(world, pc, new() { Mouse = true, Direction = pc.Position + new Vector3(distance, 0, 0) }, 1);
            speeds.Add(pc.Speed);
            Assert.Equal(distance < 0.5f ? MovementMode.MouseWalk : MovementMode.MouseRun, pc.Mode);
            if (distance == 1f) Assert.True(distance < Client.Gameplay.MouseDriving.LongArrowDistance);
        }
        Assert.True(speeds[0] < speeds[1] && speeds[1] < speeds[2] && speeds[2] < speeds[3]);
        Assert.InRange(speeds[0], 0.5f, 1.5f);
        Assert.InRange(speeds[3], 3.99f, 4.01f);
    }

    [DataFact]
    public void MouseDriveAcceleratesAndStopsAtDestinationWithoutOvershooting()
    {
        var (world, pc) = Floor();
        var target = pc.Position + new Vector3(3, 0, 0);
        Hold(world, pc, new() { Mouse = true, Direction = target }, 1);
        Assert.InRange(pc.Speed, 0.12f, 0.15f);
        Hold(world, pc, new() { Mouse = true, Direction = target }, 100);
        Assert.InRange(pc.Position.X, target.X - 0.1f, target.X);
        Assert.Equal(0, pc.Speed);
        Assert.Equal(MovementMode.Stand, pc.Mode);
    }

    [DataFact]
    public void DeadAndUncommandableCreaturesCannotBeDrivenAndNanIsIgnored()
    {
        var (world, pc) = Floor();
        pc.Commandable = false;
        Hold(world, pc, new() { Turn = 1 });
        Assert.Null(pc.DriveInput);
        pc.Commandable = true;
        Driving.Receive(world, pc, new() { Active = true, Turn = float.NaN });
        Assert.Null(pc.DriveInput);
        pc.IsDead = true;
        Hold(world, pc, new() { Turn = 1 });
        Assert.Null(pc.DriveInput);
    }

    [Fact]
    public void DriveAndDirectionalUpdateRoundTrip()
    {
        var drive = new DriveInputMessage { Active = true, Direction = new(-1, 1, 0), Turn = 1, Walk = true };
        var read = DriveInputMessage.Read(new MessageReader(drive.Write()));
        Assert.Equal(drive.Direction, read.Direction); Assert.Equal(1, read.Turn); Assert.True(read.Active && read.Walk);
        var update = new UpdateMessage { Mode = MovementMode.StrafeLeft, Surface = 5, FootstepType = 12 };
        var state = UpdateMessage.Read(new MessageReader(update.Write()));
        Assert.Equal(update.Mode, state.Mode); Assert.Equal(5, state.Surface); Assert.Equal(12, state.FootstepType);
    }

    [DataFact]
    public void SeparateTurnTapsAdvanceExactlyThirtyDegreesEvenBetweenTicks()
    {
        foreach (float dt in new[] { 1f / 30, 0.17f })
        {
            var (world, pc) = Floor();
            foreach (int sign in new[] { 1, 1, -1, -1 })
            {
                float before = pc.Facing;
                Driving.Receive(world, pc, new() { Active = true, Turn = sign });
                Driving.Receive(world, pc, new());
                Assert.Equal(before, pc.Facing);
                for (int i = 0; i < 20; i++)
                {
                    float last = pc.Facing;
                    Movement.Step(world, pc, dt);
                    Assert.InRange(Math.Abs(pc.Facing - last), 0, Driving.TurnRate * dt + 0.00001f);
                }
                Assert.InRange(Math.Abs(pc.Facing - before - sign * MathF.PI / 6), 0, 0.00001f);
                Assert.Null(pc.DriveInput);
            }
            Assert.Equal(new Vector3(20, 20, 0), pc.Position);
        }
    }

    [DataFact]
    public void RapidTurnEdgesCannotExceedTheHeldRate()
    {
        foreach (float dt in new[] { 1f / 30, 1f / 120 })
        foreach (int sign in new[] { -1, 1 })
        foreach (var (axis, walk) in new[] { (Vector3.Zero, false), (new Vector3(0, 1, 0), false), (new Vector3(0, 1, 0), true), (new Vector3(1, 0, 0), false) })
        {
            var (world, pc) = Floor();
            float rate = axis.Y > 0 && !walk ? Driving.RunningTurnRate : Driving.TurnRate;
            for (int i = 0; i < 120; i++)
            {
                float before = pc.Facing;
                // Even several input edges delivered in one server tick share its angular budget.
                for (int packet = 0; packet < 8; packet++)
                {
                    Driving.Receive(world, pc, new() { Active = true, Turn = sign, Direction = axis, Walk = walk });
                    Driving.Receive(world, pc, new());
                }
                Assert.Equal(before, pc.Facing);
                Movement.Step(world, pc, dt);
                float delta = MathF.IEEERemainder(pc.Facing - before, MathF.Tau);
                Assert.InRange(sign * delta, rate * dt - 0.00001f, rate * dt + 0.00001f);
            }
        }
    }

    [DataFact]
    public void ReversingAndCancellingAPendingTurnNeverSnapsOrReplaysIt()
    {
        var (world, pc) = Floor();
        Driving.Receive(world, pc, new() { Active = true, Turn = 1 });
        Driving.Receive(world, pc, new());
        Movement.Step(world, pc, 0.1f);
        float before = pc.Facing;
        Driving.Receive(world, pc, new() { Active = true, Turn = -1 });
        Driving.Receive(world, pc, new());
        Assert.Equal(before, pc.Facing);
        Movement.Step(world, pc, 0.1f);
        Assert.InRange(before - pc.Facing, Driving.TurnRate * 0.1f - 0.00001f, Driving.TurnRate * 0.1f + 0.00001f);
        Movement.Stop(pc);
        before = pc.Facing;
        Movement.Step(world, pc, 0.5f);
        Assert.Equal(before, pc.Facing);
        Assert.Null(pc.DriveInput);
    }

    [Theory]
    [InlineData(0, -1, 59)] [InlineData(1, 0, 63)] [InlineData(0, 1, 67)] [InlineData(-1, 0, 71)]
    public void NativeArrowFramesRotateInSixteenSteps(float x, float y, int expected)
    {
        Assert.Equal(expected, Client.Gameplay.MouseDriving.Cursor(x, y, false));
        Assert.Equal(expected + 16, Client.Gameplay.MouseDriving.Cursor(x, y, true));
    }
}
