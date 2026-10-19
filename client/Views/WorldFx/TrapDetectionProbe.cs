using System.Text.Json;
using OpenWinter.Client.Gameplay;
using Godot;

namespace OpenWinter.Client.Views.WorldFx;

/// <summary>Headless Godot regression for detection arriving before object/model creation.</summary>
public static class TrapDetectionProbe
{
    public static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var checks = new List<string>();
        var session = new ClientSession();
        var fx = new TrapFx { Session = session };
        var objects = new List<ObjectView>();
        void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException(name);
            checks.Add(name);
        }
        void Notify(string kind, uint id, bool detected = true)
        {
            using var json = JsonDocument.Parse(detected ? "{\"detected\":true}" : "{\"detected\":false}");
            fx.Handle(kind, id, json.RootElement);
        }
        MeshInstance3D Add(uint id)
        {
            var view = new ObjectView { ObjectId = id };
            var mesh = new MeshInstance3D { Mesh = new BoxMesh() };
            view.AddChild(mesh);objects.Add(view);session.Objects[id] = view;
            return mesh;
        }
        try
        {
            Notify("trap-detected", 1);
            fx._Process(0);
            Check(fx.Count == 1, "retain detection before spawn");
            var mesh = Add(1);
            fx._Process(0);
            Check(mesh.MaterialOverlay != null, "late spawn receives detected tint");
            Notify("trap-detected", 1, false);
            Check(mesh.MaterialOverlay == null && fx.Count == 0, "undetect clears tint and state");
            Notify("trap-detected", 2);
            Notify("trap-disarmed", 2);
            var disarmed = Add(2);fx._Process(0);
            Check(disarmed.MaterialOverlay == null, "disarm before spawn prevents later tint");
            Notify("trap-detected", 3);
            Notify("trap-detected", 3, false);
            var hidden = Add(3);fx._Process(0);
            Check(hidden.MaterialOverlay == null, "undetect before spawn prevents later tint");
            Notify("trap-detected", 4);fx.Clear();
            var cleared = Add(4);fx._Process(0);
            Check(cleared.MaterialOverlay == null && fx.Count == 0, "area clear removes pending detections");
            var delayed = new ObjectView { ObjectId = 5 };objects.Add(delayed);session.Objects[5] = delayed;
            Notify("trap-detected", 5);fx._Process(0);
            var child = new MeshInstance3D { Mesh = new BoxMesh() };delayed.AddChild(child);fx._Process(0);
            Check(child.MaterialOverlay != null, "late model receives retained tint");
            var foreign = new StandardMaterial3D();child.MaterialOverlay = foreign;
            fx._Process(0);fx.Clear();
            Check(child.MaterialOverlay == foreign, "clearing preserves another overlay owner");
            child.MaterialOverlay = null;foreign.Dispose();
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonSerializer.Serialize(new { checks }, new JsonSerializerOptions { WriteIndented = true }));
            GD.Print($"[trap-detection] {checks.Count} checks passed");
            return 0;
        }
        catch (Exception e)
        {
            File.WriteAllText(Path.Combine(directory, "failure.txt"), e.ToString());GD.PrintErr(e);return 2;
        }
        finally
        {
            fx.Clear();fx.Free();foreach (var view in objects) view.Free();session.Free();
        }
    }
}
