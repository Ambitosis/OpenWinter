using OpenWinter.Client.Anim;
using Godot;

namespace OpenWinter.Client.Diagnostics;

public partial class RestAnimationProbe : Node3D
{
    public override void _Ready()
    {
        int checks = 0;
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); checks++; }
        try
        {
            var skeleton = new Skeleton3D { Name = "Skeleton" };
            skeleton.AddBone("torso_g"); AddChild(skeleton);
            var library = new AnimationLibrary();
            foreach (string name in new[] { "pause1", "walk", "run", "sitcross", "sitcrossps", "worship" })
            {
                var clip = new Godot.Animation { Length = name == "sitcross" ? 1.5 : 2 };
                int track = clip.AddTrack(Godot.Animation.TrackType.Position3D);
                clip.TrackSetPath(track, "Skeleton:torso_g");
                clip.PositionTrackInsertKey(track, 0, new Vector3(0, name == "sitcrossps" ? 1 : 0, 0));
                clip.PositionTrackInsertKey(track, clip.Length, new Vector3(0, name is "sitcross" or "sitcrossps" ? 1 : 0, 0));
                library.AddAnimation(name, clip);
            }
            var animator = new CreatureAnimator(); AddChild(animator);
            animator.Setup(library, skeleton, new AnimationProfile()); animator.SetProcess(false);
            void Step(double seconds) { animator._Process(seconds); animator.Advance(seconds); }
            animator.PlayAction("sitcross", 5);
            Step(.1); Step(.5);
            Check(animator.CaptureAction() is { Clip: "sitcross", Backward: false }, "entry is forward sitcross");
            Step(1);
            Check(animator.CaptureAction() is { Clip: "sitcrossps" }, "entry changes to seated loop");
            Step(2);
            Check(animator.CaptureAction() is { Clip: "sitcrossps" }, "seated loop never repeats entry");
            var seated = animator.CaptureAction(); animator.StopAction(); animator.RestoreAction(seated);
            Check(animator.CaptureAction() is { Clip: "sitcrossps" }, "appearance rebuild preserves seated loop");
            animator.PlayAction("pause1");
            Check(animator.CaptureAction() is { Clip: "sitcross", Backward: true }, "idle acknowledgement starts reverse entry");
            Step(.05); Step(.3);
            float early = skeleton.GetBonePosePosition(0).Y;
            Step(.5);
            float later = skeleton.GetBonePosePosition(0).Y;
            Check(early > later + .1f, "reverse graph actually moves from seated toward standing");
            var rise = animator.CaptureAction(); animator.PlayAction("pause1");
            Check(animator.CaptureAction() == rise, "duplicate idle does not cancel rise");
            animator.StopAction(); animator.RestoreAction(rise);
            Check(animator.CaptureAction() is { Backward: true }, "appearance rebuild preserves reverse direction");
            Step(.01);
            Check(Math.Abs(skeleton.GetBonePosePosition(0).Y - later) < .06f, "reverse restore preserves actual bone pose");
            Step(1);
            Check(animator.CaptureAction() == null, "rise completes to idle");
            animator.PlayAction("sitcross", 2); Step(1.6); Step(.5);
            Check(animator.CaptureAction() is { Backward: true }, "duration expiry begins rise");
            animator.PlayAction("sitcross", 2); animator.HoldSittingUntilStopped(); Step(1.6); Step(5);
            Check(animator.CaptureAction() is { Clip: "sitcrossps" }, "server rest hold survives elapsed client duration");
            animator.PlayAction("pause1");
            Check(animator.CaptureAction() is { Backward: true }, "server rest completion releases hold");
            animator.PlayAction("worship", 3); Step(.2);
            Check(animator.CaptureAction() is { Clip: "worship", Backward: false }, "new action resets playback direction");
            animator.PlayAction("sitcross", 5); Step(.4); animator.PlayAction("pause1");
            Check(animator.CaptureAction() is { Backward: true, Remaining: < .5 }, "early cancellation reverses only elapsed entry");
            animator.SetDead(true);
            Check(animator.CaptureAction() == null, "death cancels seated recovery");
            GD.Print($"[rest-animation] PASS {checks} checks"); GetTree().Quit();
        }
        catch (Exception error) { GD.PrintErr("[rest-animation] FAIL " + error); GetTree().Quit(1); }
    }
}
