using System.Text.Json;
using OpenWinter.Net;
namespace OpenWinter.Client.Gameplay;
public partial class ClientSession
{
    void ApplyExploration(NotifyMessage message)
    {
        if (Area == null || message.Subject != Area.Info.AreaId) return;
        if (message.Kind is not ("map-explored" or "tile-explored" or "area-map")) return;
        using var document = JsonDocument.Parse(message.Json);
        var data = document.RootElement;
        if (message.Kind == "map-explored") Area.SetAllExplored(data.GetProperty("explored").GetBoolean());
        else if (message.Kind == "tile-explored") Area.SetExplored(data.GetProperty("x").GetInt32(), data.GetProperty("y").GetInt32(), data.GetProperty("explored").GetBoolean());
        else
        {
            string mask = data.GetProperty("explored").GetString() ?? "";
            for (int i = 0; i < Area.Info.Width * Area.Info.Height; i++) Area.SetExplored(i % Area.Info.Width, i / Area.Info.Width, i < mask.Length && mask[i] == '1');
        }
    }
}
