using System.Runtime.CompilerServices;
using OpenWinter.Client.Rendering;
using Godot;

namespace OpenWinter.Client.Views;

/// <summary>Undiscovered tile surfaces still write black coverage and depth.</summary>
sealed class TileExploration
{
    static readonly ConditionalWeakTable<StandardMaterial3D, Material> DarkMaterials = new();
    readonly List<(MeshInstance3D Mesh, int Surface, Material? Original, Material Dark)> _surfaces = new();
    readonly Node3D _details;
    bool? _explored;

    public TileExploration(Node3D root, Node3D details)
    {
        _details = details;
        // Static model batches are direct children; flames and other effects live in details.
        foreach (var mesh in root.GetChildren().OfType<MeshInstance3D>())
        {
            if (mesh.Mesh == null) continue;
            for (int i = 0; i < mesh.Mesh.GetSurfaceCount(); i++)
            {
                if (mesh.Mesh.SurfaceGetMaterial(i) is not StandardMaterial3D source) continue;
                var dark = DarkMaterials.GetValue(source, material =>
                {
                    var black = (StandardMaterial3D)material.Duplicate();
                    black.AlbedoColor = new Color(0, 0, 0, material.AlbedoColor.A);
                    black.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
                    black.EmissionEnabled = false;
                    black.DisableFog = true;
                    return (Material?)Keyhole.Convert(black) ?? black;
                });
                _surfaces.Add((mesh, i, mesh.GetSurfaceOverrideMaterial(i), dark));
            }
        }
    }

    public void SetExplored(bool explored)
    {
        if (_explored == explored) return;
        _explored = explored;
        _details.Visible = explored;
        foreach (var (mesh, surface, original, dark) in _surfaces)
            mesh.SetSurfaceOverrideMaterial(surface, explored ? original : dark);
    }
}
