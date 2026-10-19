using OpenWinter.Net;
using Godot;

namespace OpenWinter.Client.Views;

public partial class AreaView
{
    bool[]? _explored;
    // unexploredmemory: once ExploreAreaForPlayer(TRUE) has revealed the area, the original keeps drawing all of it
    // (objects too) even after ExploreAreaForPlayer(FALSE) clears the map. Ordinary exploration has no such memory.
    bool _revealedAll;
    void InitExploration()
    {
        if (!Info.IsInterior) return;
        _explored = new bool[Info.Width * Info.Height];
        foreach (var tile in _tileDiscovery) tile?.SetExplored(false);
    }
    public void SetExplored(int x, int y, bool explored)
    {
        if (_explored == null || x < 0 || y < 0 || x >= Info.Width || y >= Info.Height) return;
        int i = y * Info.Width + x;
        _explored[i] = explored;
        _tileDiscovery[i]?.SetExplored(explored || _revealedAll);
    }
    public void SetAllExplored(bool explored)
    {
        if (_explored == null) return;
        if (explored) _revealedAll = true;
        for (int y = 0; y < Info.Height; y++) for (int x = 0; x < Info.Width; x++) SetExplored(x, y, explored);
    }
    public bool IsExplored(Vector3 position)
    {
        if (_explored == null || _revealedAll) return true;
        int x = (int)MathF.Floor(position.X / 10), y = (int)MathF.Floor(position.Y / 10);
        return x >= 0 && y >= 0 && x < Info.Width && y < Info.Height && _explored[y * Info.Width + x];
    }
    public bool IsExplored(ObjectView view)
    {
        if (view.Kind != ObjectKind.Door) return IsExplored(view.Position);
        // A door belongs to both sides of its frame, including a door exactly on a tile seam.
        var normal = view.Basis.Y * .5f;
        return IsExplored(view.Position + normal) || IsExplored(view.Position - normal);
    }
}
