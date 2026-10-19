using System.Numerics;
using OpenWinter.Game.Objects;
using OpenWinter.Game.Scripting;

namespace OpenWinter.Game.Requests;

/// <summary>Module-defined ground radial actions from surfacemat.2da.</summary>
static class TileActionRequests
{
    sealed record Entry(int Id, string Name, string Icon);

    static List<Entry> Entries(Request r, Vector3 point)
    {
        if (r.Element("position") is not { ValueKind: System.Text.Json.JsonValueKind.Array } coordinates || coordinates.GetArrayLength() != 3) return [];
        var area = r.Player.Area;
        if (area == null || r.Id("area") != area.Id || !float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z)) return [];
        // Ground menus are picked from walkable geometry. Reject points outside the current area floor.
        if (!area.Walkmesh.Triangles.Any(t => t.Walkable && t.TryHeightAt(point.X, point.Y, out float z) && MathF.Abs(z - point.Z) < 0.5f)) return [];
        int surface = area.Walkmesh.SurfaceAt(point);
        var table = r.World.Resources.TwoDA("surfacemat");
        var entries = new List<Entry>();
        for (int id = 1; id <= 8; id++)
        {
            int name = table.GetInt(surface, $"Act{id}_Strref", -1);
            string icon = table.Get(surface, $"Act{id}_Icon");
            if (name < 0) continue;
            entries.Add(new Entry(id, r.World.Resources.TlkString((uint)name), icon.ToLowerInvariant()));
        }
        return entries;
    }

    [ClientRequest("tile-actions")]
    static void Describe(Request r)
    {
        var entries = Entries(r, r.Vec("position"));
        r.World.Output?.Notify("tile-actions", r.Player, new
        {
            query = r.Int("query"),
            entries = entries.Select(e => new { id = e.Id, name = e.Name, icon = e.Icon }).ToArray(),
        }, r.Player);
    }

    [ClientRequest("tile-action")]
    static void Use(Request r)
    {
        var point = r.Vec("position");
        int id = r.Int("id");
        if (!Entries(r, point).Any(e => e.Id == id)) return;
        r.World.RunEvent(r.World.Module, "Mod_OnPlrTileAct", new EventContext()
            .Set("EventScriptType", 3020).Set("TileActionId", id)
            .Set("TileActionPosition", point).Set("TileActionPlayer", r.Player.Id));
    }
}
