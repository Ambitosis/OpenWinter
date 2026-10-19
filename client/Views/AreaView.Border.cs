using OpenWinter.Aurora.Models;
using OpenWinter.Client.Rendering;
using Godot;

namespace OpenWinter.Client.Views;

public partial class AreaView
{
    Node3D? _border;

    void BuildBorder()
    {
        if (_border != null)
        {
            RemoveChild(_border);
            _border.QueueFree();
            _border = null;
        }
        if (_tileContent == null || _tileSet == null) return;
        var placements = InteriorBorder.Build(_tileSet, _tileContent.Resources.TwoDA(Info.Tileset + "_edge"),
            Info.Width, Info.Height, Info.Tiles.Select(t => new InteriorBorder.Tile(t.Id, t.Orientation, t.Height)).ToArray());
        if (placements.Count == 0) return;
        _border = new Node3D { Name = "Border" };
        AddChild(_border);
        var templates = new Dictionary<string, Node3D?>();
        foreach (var p in placements)
        {
            if (!templates.TryGetValue(p.Model, out var template))
            {
                template = _tileContent.Models.Get(p.Model) is { } data
                    ? ModelBuilder.BuildStatic(data, _tileContent.Materials, lights: false, emitters: false).Root : null;
                if (template != null) Keyhole.Apply(template);
                else _tileContent.WarnOnce($"border model '{p.Model}' of {Info.Tileset} not found");
                templates[p.Model] = template;
            }
            if (template == null) continue;
            // Share mesh and keyholed materials. The border caps can obscure the player near the area
            // edge (keyborder); they use the same cut as the tiles and have no collision or walkable surface.
            var tile = (Node3D)template.Duplicate(0);
            tile.Position = new Vector3((p.X + 0.5f) * 10f, (p.Y + 0.5f) * 10f, p.Height);
            _border.AddChild(tile);
        }
        foreach (var template in templates.Values) template?.Free();
    }
}
