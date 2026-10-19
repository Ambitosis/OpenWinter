using Godot;

namespace OpenWinter.Client.Rendering.Emitters;

/// <summary>
/// Holds an effect at its object's position but in the area's orientation: the original turns an effect with
/// its object only when visualeffects.2da's OrientWithObject asks for it.
/// </summary>
public partial class WorldAligned : Node3D
{
    Node3D? _space;

    public WorldAligned() { }

    public WorldAligned(Node3D content)
    {
        Name = content.Name + "_aligned";
        AddChild(content);
        Content = content;
        // Gone with its effect.
        content.TreeExited += () => Callable.From(() =>
        {
            if (IsInstanceValid(this) && (!IsInstanceValid(content) || !content.IsInsideTree() || content.GetParent() != this))
                QueueFree();
        }).CallDeferred();
    }

    public Node3D? Content { get; }

    public override void _Ready()
    {
        for (Node? n = GetParent(); n != null; n = n.GetParent())
            if (n.Name == "AuroraSpace" && n is Node3D s) { _space = s; break; }
        Align();
    }

    public override void _Process(double delta) => Align();

    void Align()
    {
        if (_space == null) return;
        var g = GlobalTransform;
        GlobalTransform = new Transform3D(_space.GlobalBasis, g.Origin);
    }
}
