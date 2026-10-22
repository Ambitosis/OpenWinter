using System.Text.Json;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Shared notice payloads serialize exactly as the anonymous objects the client was written against.</summary>
public class NoticePayloadTests
{
    static void Same(object typed, object anonymous) => Assert.Equal(JsonSerializer.Serialize(anonymous), JsonSerializer.Serialize(typed));

    [Fact]
    public void PayloadsKeepTheirJson()
    {
        Same(new AreaSnapshotReadyNotice(7), new { token = 7u });
        Same(new MapExploredNotice(3, true), new { area = 3u, explored = true });
        Same(new TileExploredNotice(3, 1, 2, false), new { area = 3u, x = 1, y = 2, explored = false });
        Same(new AreaMapNotice(3, "0110", [new AreaMapPin("9", "Inn", 1.5f, 2.25f, false)]),
            new { area = 3u, explored = "0110", pins = new[] { new { id = "9", note = "Inn", x = 1.5f, y = 2.25f, personal = false } } });
    }
}
