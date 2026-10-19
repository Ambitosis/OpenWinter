using System.Text.Json;
using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.Views.WorldFx;

/// <summary>Real content, geometry and audio ownership checks for repeated instant beams.</summary>
public static class BeamLifetimeCapture
{
    public static async Task Run(Node host, ClientSession session, string dir)
    {
        int checks = 0;
        async Task Wait(double seconds) => await host.ToSignal(host.GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);
        void Check(bool ok, string name) { if (!ok) throw new InvalidOperationException(name); checks++; GD.Print($"[beam-lifetime] PASS {name}"); }
        try
        {
            System.IO.Directory.CreateDirectory(dir);
            await Wait(3);
            var fx = host.GetChildren().OfType<WorldFxPlayer>().Single().Beams;
            fx.Clear(); await Wait(.1);
            var existing = session.Space.GetChildren().ToHashSet();
            var content = session.Content!;
            var vfx = content.Resources.TwoDA("visualeffects");
            var progfx = content.Resources.TwoDA("progfx");
            var rows = Enumerable.Range(0, vfx.RowCount).Where(row =>
                progfx.GetInt(vfx.GetInt(row, "ProgFX_Duration", -1), "Type", -1) == 7).Take(2).ToArray();
            Check(rows.Length == 2, "two-installed-beam-rows");
            uint target = session.ControlledId;
            void Add(int row, long id = 0, float duration = 0)
                => fx.Add(target, JsonSerializer.SerializeToElement(new { vfx = row, from = target, id, duration, bodyPart = 0 }));
            Node3D[] Roots() => session.Space.GetChildren().OfType<Node3D>().Where(n => !existing.Contains(n)).ToArray();
            int Sounds() => Roots().SelectMany(n => n.GetChildren()).OfType<AudioStreamPlayer3D>().Count();
            Add(rows[0]);
            var original = Roots().Single();
            int sounds = Sounds();
            for (int i = 0; i < 1000; i++) Add(rows[0]);
            Check(fx.Count == 1 && Roots().Length == 1, "1000-repeats-retain-one-render-root");
            Check(Roots().Single() == original && Sounds() == sounds, "repeats-reuse-geometry-and-audio");
            Add(rows[1]); Check(fx.Count == 2, "different-row-is-independent");
            Add(rows[0], 1001, -1); Add(rows[0], 1002, -1);
            Check(fx.Count == 4, "retained-effect-ids-remain-independent");
            Add(rows[0], 1001, -1); await Wait(.1);
            Check(fx.Count == 4 && Roots().Length == 4, "same-retained-id-replaces-only-itself");
            fx.Remove(1001); await Wait(.1);
            Check(fx.Count == 3 && Roots().Length == 3, "effect-removal-leaves-instant-beam");
            Add(rows[0], 1003, .2f); await Wait(.4);
            Check(fx.Count == 3 && Roots().Length == 3, "temporary-beam-expires");
            await host.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            host.GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(dir, "beams.png"));
            // Temporarily remove the render recipient, exactly as a despawn does.
            var view = session.Objects[target]; session.Objects.Remove(target);
            fx._Process(.01); session.Objects[target] = view; await Wait(.1);
            Check(fx.Count == 0 && Roots().Length == 0, "recipient-removal-frees-all-roots-and-audio");
            Add(rows[0]); Check(fx.Count == 1, "removed-instant-key-can-be-added-again");
            fx.Clear(); await Wait(.1);
            Check(fx.Count == 0 && Roots().Length == 0, "area-clear-frees-roots-and-audio");
            Add(rows[0]); Check(fx.Count == 1, "area-clear-resets-instant-keys");
            fx.Clear();
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "checks.json"), JsonSerializer.Serialize(new { checks, passed = true }));
            GD.Print($"[beam-lifetime] COMPLETE {checks} checks"); GD.Print("VISUAL|done|1"); host.GetTree().Quit();
        }
        catch (Exception e) { GD.PrintErr(e.ToString()); host.GetTree().Quit(1); }
    }
}
