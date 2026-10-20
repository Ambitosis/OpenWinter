using System.Text.Json;
using OpenWinter.Client.Gameplay;
using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>Runs real appear/disappear notifications and subsequent look updates without game assets.</summary>
public static class DisappearVisibilityProbe
{
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        var session = new ClientSession();
        var fx = new EffectPlayer { Session = session };
        var view = new ObjectView { ObjectId = 42 };
        session.Objects[42] = view;
        void Check(bool pass, string name)
        {
            if (!pass) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        void Notify(string kind, int animation) => fx.Handle(new NotifyMessage
        { Kind = kind, Subject = view.ObjectId, Json = $"{{\"animation\":{animation}}}" });
        try
        {
            foreach (int animation in new[] { 0, 1 }) // Requested animation without a model must still hide.
            {
                string prefix = $"animation={animation}: ";
                Notify("disappear", animation);
                Check(!view.Visible, prefix + "disappear hides");
                view.SetEffectLook(1, new ObjectView.EffectLook { Alpha = .8f, PulsePeriod = 1, PulseAlpha = .2f });
                Check(!view.Visible, prefix + "applying a duration effect does not reveal");
                view._Process(.25);
                Check(!view.Visible && view.EffectAlpha < .8f, prefix + "active pulse advances without revealing");
                view.SetPerception(true); view.SetStealth(true); view.SetStealth(false);
                Check(!view.Visible, prefix + "perception and stealth updates do not reveal");
                view.SetEffectLook(1, null);
                Check(!view.Visible, prefix + "removing final effect does not reveal");
                view.AddChild(new MeshInstance3D { Mesh = new BoxMesh() });
                view.SetEffectAlpha(2, .4f);
                Check(!view.Visible, prefix + "new model look update retains disappearance");
                view.SetPerception(false); Notify("appear", animation);
                Check(!view.Visible, prefix + "appear respects unseen perception");
                view.SetPerception(true);
                Check(view.Visible && Math.Abs(view.PresentationAlpha - .4f) < .001f, prefix + "appear cleared disappearance and retained opacity");
                view.SetStealthVisibility(true); Notify("disappear", animation); Notify("appear", animation);
                Check(!view.Visible, prefix + "appear respects hidden stealth");
                view.SetStealthVisibility(false);
                Check(view.Visible, prefix + "clearing final independent blocker reveals");
                view.SetEffectAlpha(2, null);
            }
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[disappear-visibility] {checks.Count} checks passed"); return 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(directory, "failure.txt"), e.ToString()); GD.PrintErr(e); return 1;
        }
        finally { fx.Free(); view.Free(); session.Free(); }
    }
}
