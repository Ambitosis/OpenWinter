using System.Text.Json.Serialization;

namespace OpenWinter.Net;

// Payloads of NotifyMessage kinds that more than one producer sends (the OpenWinter server and the NWN bridge).
// Each serializes to the same JSON as the anonymous object it replaced.

/// <summary>"area-snapshot-ready": the area and its objects are sent; the client may finish loading.</summary>
public sealed record AreaSnapshotReadyNotice([property: JsonPropertyName("token")] uint Token);

/// <summary>"map-explored": every tile of the area explored, or none.</summary>
public sealed record MapExploredNotice([property: JsonPropertyName("area")] uint Area, [property: JsonPropertyName("explored")] bool Explored);

/// <summary>"tile-explored": one tile's explored state.</summary>
public sealed record TileExploredNotice(
    [property: JsonPropertyName("area")] uint Area,
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("explored")] bool Explored);

/// <summary>"area-map": the explored tiles ('1'/'0', row by row) and the map's pins.</summary>
public sealed record AreaMapNotice(
    [property: JsonPropertyName("area")] uint Area,
    [property: JsonPropertyName("explored")] string Explored,
    [property: JsonPropertyName("pins")] AreaMapPin[] Pins);

/// <summary>A map pin: a waypoint's map note or the player's own note.</summary>
public sealed record AreaMapPin(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("note")] string Note,
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y,
    [property: JsonPropertyName("personal")] bool Personal);
