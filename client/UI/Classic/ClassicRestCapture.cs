using System.IO;
using Godot;
using OpenWinter.Client.Views;
using System.Text.Json;

namespace OpenWinter.Client.UI.Classic;

/// <summary>Sandbox client control: authoritative rest, pause, resume, and the actual X handler.</summary>
public static class ClassicRestCapture
{
    public static async Task Run(string dir, Func<string, string, Task> shot, Func<double, Task> wait, Action<int> quit)
    {
        int checks = 0, exitCode = 1;
        string? failure = null;
        var passed = new List<string>();
        void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException($"[classic-rest] FAIL {name}");
            GD.Print($"[classic-rest] PASS {name}"); checks++; passed.Add(name);
        }
        try
        {
            Directory.CreateDirectory(dir);
            await wait(3);
            var ctx = ClassicGame.Current ?? throw new InvalidOperationException("classic mode required");
            var panel = ClassicRest.Current ?? throw new InvalidOperationException("rest panel missing");
            var fx = panel.Fx;
            ctx.Session.Request("clear-actions"); await wait(.3);
            ctx.Session.Request("rest"); await wait(.5);
            Check(fx.RestActive && panel.Visible, "server-start-shows-panel");
            Check(fx.RestDuration > 0 && panel.Progress > 0, "server-duration-progress");
            Check(panel.Position == (panel.GetViewportRect().Size / 2 - panel.Panel.ModelOrigin).Round(), "native-model-origin-placement");
            Check(!fx.GetChildren().OfType<Label>().Any(l => l.Visible && l.Text.StartsWith("Resting")), "modern-rest-banner-suppressed");
            float early = panel.Progress;
            await shot(dir, "01_started"); await wait(1.7);
            var playback = (ctx.Session.Controlled as CreatureView)?.Animator?.CaptureAction();
            Check(playback?.Clip == "sitcrossps", "actual-rest-loop-sitcrossps");
            Check(panel.Progress > early, "progress-advances");
            ctx.Session.Request("pause"); await wait(.3);
            Check(fx.RestPaused, "pause-acknowledged");
            float paused = panel.Progress;
            await wait(.6);
            Check(panel.Progress == paused, "pause-freezes-progress");
            await shot(dir, "02_paused");
            ctx.Session.Request("pause"); await wait(.5);
            Check(!fx.RestPaused && panel.Progress > paused, "resume-advances-progress");
            panel.Panel.Get<GuiButton>("CancelButton")!.EmitSignal(BaseButton.SignalName.Pressed);
            await wait(.5);
            Check(!fx.RestActive && !panel.Visible, "X-cancels-authoritatively");
            await shot(dir, "03_cancelled");
            ctx.Session.Request("rest"); await wait(.4);
            Check(fx.RestActive && panel.Visible, "second-rest-starts");
            await wait(fx.RestDuration + .3);
            Check(!fx.RestActive && !panel.Visible, "server-finish-hides-panel");
            await shot(dir, "04_finished");
            GD.Print($"[classic-rest] COMPLETE checks={checks}");
            exitCode = 0;
            GD.Print("VISUAL|done|1");
        }
        catch (Exception e) { failure = e.ToString(); GD.PrintErr(failure); }
        finally
        {
            File.WriteAllText(Path.Combine(dir, "checks.json"), JsonSerializer.Serialize(new { exitCode, checks, passed, failure }, new JsonSerializerOptions { WriteIndented = true }));
            quit(exitCode);
        }
    }
}
