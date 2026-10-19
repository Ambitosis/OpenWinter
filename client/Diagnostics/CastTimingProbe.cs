using OpenWinter.Client.Anim;
using Godot;

namespace OpenWinter.Client.Diagnostics;

/// <summary>Real-Godot checks for deadline updates without replaying the cast graph.</summary>
public partial class CastTimingProbe : Node3D
{
    int _checks;
    void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        _checks++;
    }

    public override void _Ready()
    {
        try
        {
            var skeleton = new Skeleton3D { Name = "Skeleton" };
            skeleton.AddBone("torso_g"); AddChild(skeleton);
            var library = new AnimationLibrary();
            foreach (string name in new[] { "pause1", "walk", "run", "castself", "castselflp", "worship", "ccastout", "ccastoutlp" })
                library.AddAnimation(name, new Godot.Animation { Length = name == "castself" ? .5 : 1 });
            var animator = new CreatureAnimator(); AddChild(animator);
            animator.Setup(library, skeleton, new AnimationProfile());
            animator.SetProcess(false);
            void Step(double seconds) { animator._Process(seconds); animator.Advance(seconds); }

            animator.PlayAction("castself", 1.5f);
            ulong version = animator.ActionVersion;
            Step(.25);
            var intro = animator.CaptureAction()!;
            animator.RetimeAction(version, 2);
            var extended = animator.CaptureAction()!;
            Check(extended.Clip == "castself" && extended.CastLoop == "castselflp", "intro and pending recovery retained");
            Check(extended.Position == intro.Position && extended.UntilCastLoop == intro.UntilCastLoop, "intro phase is not restarted");
            Check(Math.Abs(extended.Remaining - 2) < .001 && animator.ActionVersion == version, "deadline extends without a new action");
            Step(.3);
            Check(animator.CaptureAction()!.Clip == "castselflp", "original intro boundary enters recovery");
            Step(.2);
            var recovery = animator.CaptureAction()!;
            animator.RetimeAction(version, .4f);
            var shortened = animator.CaptureAction()!;
            Check(shortened.Clip == recovery.Clip && shortened.Position == recovery.Position, "shortening preserves recovery phase");
            Check(animator.ActionVersion == version, "recovery retime retains generation");
            Step(.2);
            Check(animator.CaptureAction() != null, "shortened recovery remains before deadline");
            Step(.21);
            Check(animator.CaptureAction() == null, "shortened recovery ends at deadline");
            animator.RetimeAction(version, 10);
            Check(animator.CaptureAction() == null, "late update cannot resurrect ended action");
            animator.PlayAction("worship", 2);
            var replacement = animator.CaptureAction();
            animator.RetimeAction(version, .01f);
            Check(animator.CaptureAction() == replacement, "old cast cannot shorten replacement action");
            ulong current = animator.ActionVersion;
            animator.RetimeAction(current, float.NaN);
            animator.RetimeAction(current, float.PositiveInfinity);
            Check(animator.CaptureAction() == replacement, "nonfinite updates are ignored");
            animator.RetimeAction(current, 0);
            Check(animator.CaptureAction() == null && animator.ActionVersion != current, "zero deadline ends and invalidates current action");
            animator.RetimeAction(current, 2);
            Check(animator.CaptureAction() == null, "zero-ended action stays ended");
            foreach (double introLength in new[] { 1.0, 2.0 })
            {
                library.GetAnimation("ccastout").Length = introLength;
                animator.PlayAction("castout", 4);
                ulong castVersion = animator.ActionVersion;
                animator.SetCreatureCastRelease(castVersion, .5f);
                Step(.3);
                Check(animator.CaptureAction()!.Clip == "ccastout", "resolved creature intro precedes release");
                Step(.25);
                var held = animator.CaptureAction()!;
                Check(held.Clip == "ccastoutlp" && held.Remaining > 3.4, "creature recovery follows release, not replacement clip length");
                Step(1.1);
                Check(animator.CaptureAction()!.Clip == "ccastoutlp", "creature release does not repeat during recovery");
                animator.StopAction();
                animator.SetCreatureCastRelease(castVersion, 0);
                Check(animator.CaptureAction() == null, "late release cannot restore stopped creature action");
            }
            animator.PlayAction("castout", 4);
            ulong cancelled = animator.ActionVersion;
            animator.SetCreatureCastRelease(cancelled, .5f);
            Step(.2);
            animator.PlayAction("worship", 2);
            animator.SetCreatureCastRelease(cancelled, 0);
            Step(.5);
            Check(animator.CaptureAction()!.Clip == "worship", "cancelled creature intro cannot replace the next action with recovery");

            // Dropping worn armour rebuilds the model at the end of the drop bend. The new graph restores getlowlp
            // with a few milliseconds left, and its deadline passes before the graph has started the restored
            // one-shot: the looping clip must still end instead of playing forever with no deadline.
            foreach (string name in new[] { "getlow", "getlowlp" })
                library.AddAnimation(name, new Godot.Animation { Length = name == "getlow" ? .5 : 1 });
            animator.PlayAction("getlow", 1.5f);
            Step(.05); Step(.6);
            var bend = animator.CaptureAction()!;
            Check(bend.Clip == "getlowlp" && bend.Loop == Godot.Animation.LoopModeEnum.Linear, "drop bend holds the looping low pose");
            var rebuilt = new CreatureAnimator(); AddChild(rebuilt);
            rebuilt.Setup(library, skeleton, new AnimationProfile());
            rebuilt.SetProcess(false);
            void StepRebuilt(double seconds) { rebuilt._Process(seconds); rebuilt.Advance(seconds); }
            rebuilt.RestoreAction(bend with { Remaining = .01 });
            StepRebuilt(.05);
            for (int i = 0; i < 4; i++) StepRebuilt(.25);
            Check(!rebuilt.IsActionPlaying && rebuilt.CaptureAction() == null, "restored loop whose deadline passed before it started still ends");
            GD.Print($"[cast-timing] PASS {_checks} checks");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PrintErr("[cast-timing] FAIL " + e); GetTree().Quit(1); }
    }
}
