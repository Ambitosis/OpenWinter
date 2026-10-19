using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Requests;

/// <summary>Optional terrain destination shared by item and gold drops.</summary>
static class ItemDropDestination
{
    public static bool Read(Request request, out Location? destination)
    {
        destination = null;
        if (request.Element("position") is not { } coordinates) return true;
        if (request.Player.Area is not { } area || request.Id("area") != area.Id
            || coordinates.ValueKind != System.Text.Json.JsonValueKind.Array || coordinates.GetArrayLength() != 3)
            return false;
        var values = new float[3];
        for (int i = 0; i < 3; i++)
            if (coordinates[i].ValueKind != System.Text.Json.JsonValueKind.Number
                || !coordinates[i].TryGetSingle(out values[i]) || !float.IsFinite(values[i])) return false;
        var point = new System.Numerics.Vector3(values[0], values[1], values[2]);
        if (area.Walkmesh.GroundHeight(point.X, point.Y, point.Z, 0.5f) is not { } z) return false;
        destination = new Location(area.Id, point with { Z = z }, 0);
        return true;
    }
}
