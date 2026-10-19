using Godot;
using OpenWinter.Client.Rendering;

namespace OpenWinter.Client.Diagnostics;

/// <summary>Real-Godot ownership check: pulse uniforms must not churn twins or recolor another actor.</summary>
public partial class PulseMaterialProbe : Node
{
    int _checks;
    void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException(label);
        _checks++;
    }

    public override void _Ready()
    {
        try
        {
            var shared = new StandardMaterial3D();
            (Node3D Root, MeshInstance3D Mesh) Actor()
            {
                var root = new Node3D(); AddChild(root);
                var mesh = new MeshInstance3D { Mesh = new BoxMesh(), MaterialOverride = shared };
                root.AddChild(mesh); return (root, mesh);
            }
            static ShaderMaterial Light(MeshInstance3D mesh) => (ShaderMaterial)mesh.MaterialOverride.NextPass;
            var a = Actor(); var b = Actor();
            Hilite.SetEffectGlow(a.Root, Colors.Red, true);
            Hilite.SetEffectGlow(b.Root, Colors.Blue, true);
            var copy = a.Mesh.MaterialOverride;
            var light = Light(a.Mesh);
            var twin = a.Root.GetChildren().OfType<MeshInstance3D>().Single(m => m.Layers == Hilite.MaskLayer);
            var mask = twin.GetSurfaceOverrideMaterial(0);
            Check(copy != b.Mesh.MaterialOverride, "dynamic actors own distinct material copies");
            for (int i = 0; i < 600; i++) Hilite.SetEffectGlow(a.Root, Colors.Red.Lerp(Colors.Blue, i / 599f), true);
            Check(a.Mesh.MaterialOverride == copy, "600 updates retain surface material");
            Check(Light(a.Mesh) == light, "600 updates retain light pass");
            Check(a.Root.GetChildren().OfType<MeshInstance3D>().Count() == 3, "600 updates retain exactly two twins");
            Check(twin.GetSurfaceOverrideMaterial(0) == mask, "600 updates retain mask material");
            Check(light.GetShaderParameter("colour").AsColor() == Colors.Blue, "surface reaches new color");
            Check(((ShaderMaterial)mask).GetShaderParameter("colour").AsColor() == Colors.Blue, "mask reaches new color");
            Hilite.SetEffectGlow(a.Root, Colors.Red, true);
            Check(Light(b.Mesh).GetShaderParameter("colour").AsColor() == Colors.Blue, "other actor is unchanged");
            Hilite.Apply(a.Root, Colors.Green);
            Hilite.SetEffectGlow(a.Root, Colors.Yellow, true);
            Check(Light(a.Mesh).GetShaderParameter("colour").AsColor() == Colors.Green, "pointer feedback retains precedence");
            Hilite.Remove(a.Root);
            Check(Light(a.Mesh).GetShaderParameter("colour").AsColor() == Colors.Yellow, "pointer release restores current pulse");
            Hilite.SetEffectGlow(a.Root, null);
            Check(a.Mesh.MaterialOverride == shared, "effect removal restores original shared material");
            Hilite.SetEffectGlow(a.Root, Colors.Red, true);
            a.Root.Free();
            Check(Hilite.Any, "freed actor cleanup retains other actor");
            Hilite.SetEffectGlow(b.Root, null);
            Check(!Hilite.Any, "last removal clears active glow ownership");
            Check(b.Mesh.MaterialOverride == shared, "other actor restores original material");
            GD.Print($"[pulse-material] PASS {_checks} checks");
            GetTree().Quit();
        }
        catch (Exception e) { GD.PrintErr("[pulse-material] FAIL " + e); GetTree().Quit(1); }
    }
}
