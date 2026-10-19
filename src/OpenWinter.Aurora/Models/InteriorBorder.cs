using OpenWinter.Aurora.Formats;

namespace OpenWinter.Aurora.Models;

/// <summary>
/// Uniform interior border terrain from the tileset's *_edge.2da. These are real tile models outside the
/// area, not black backfaces of its walls: the Castle border is an upward-facing cap at wall-top height.
/// Mixed terrain, crossers and exterior edge scenery need their own placement rules and are left alone.
/// </summary>
public static class InteriorBorder
{
    public readonly record struct Tile(int Id, int Orientation, int Height);
    public readonly record struct Placement(int X, int Y, float Height, string Model);
    static readonly string[] Corners = ["BottomRight", "TopRight", "TopLeft", "BottomLeft"];
    static readonly string[] Sides = ["Right", "Top", "Left", "Bottom"];

    /// <summary>Five tiles of inaccessible border scenery, as documented by the tileset construction guide.</summary>
    public static IReadOnlyList<Placement> Build(Ini set, TwoDA edges, int width, int height, IReadOnlyList<Tile> tiles)
    {
        var result = new List<Placement>();
        if (width <= 0 || height <= 0 || tiles.Count < width * height || set.GetInt("GENERAL", "Interior") != 1) return result;
        string border = set.Get("GENERAL", "Border");
        if (border.Length == 0) return result;
        int row = -1;
        for (int i = 0; i < edges.RowCount; i++)
            if (Same(edges.Get(i, "Corner1"), border) && Same(edges.Get(i, "Corner2"), border)
                && edges.Get(i, "Edge").Length == 0 && edges.Get(i, "Model").Length > 0)
            { row = i; break; }
        if (row < 0) return result;
        string model = edges.Get(row, "Model");
        float step = set.GetFloat("GENERAL", "Transition", 5f);
        float modelHeight = edges.GetFloat(row, "Height");
        const int reach = 5;
        for (int y = -reach; y < height + reach; y++)
            for (int x = -reach; x < width + reach; x++)
            {
                if (x >= 0 && x < width && y >= 0 && y < height) continue;
                var tile = tiles[Math.Clamp(y, 0, height - 1) * width + Math.Clamp(x, 0, width - 1)];
                string section = "TILE" + tile.Id;
                int LocalCorner(int c) => (c - tile.Orientation) & 3;
                bool IsBorder(int c) => Same(set.Get(section, Corners[LocalCorner(c)]), border);
                int Level(int c) => set.GetInt(section, Corners[LocalCorner(c)] + "Height");
                int corner;
                if ((x < 0 || x >= width) && (y < 0 || y >= height))
                {
                    corner = x < 0 ? (y < 0 ? 3 : 2) : (y < 0 ? 0 : 1);
                    if (!IsBorder(corner)) continue;
                }
                else
                {
                    int side = x < 0 ? 2 : x >= width ? 0 : y < 0 ? 3 : 1;
                    corner = side;
                    int next = (side + 1) & 3;
                    if (!IsBorder(corner) || !IsBorder(next) || Level(corner) != Level(next)
                        || set.Get(section, Sides[(side - tile.Orientation) & 3]).Length > 0) continue;
                }
                result.Add(new(x, y, (tile.Height + Level(corner) - modelHeight) * step, model));
            }
        return result;
    }

    static bool Same(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
}
