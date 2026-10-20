using System.Text.Json;
using OpenWinter.Content;

namespace OpenWinter.Client.Gameplay;

/// <summary>The local layout, including deliberately empty bars and legacy spell-only saves.</summary>
public sealed class QuickbarSave<T> where T : QuickbarSlot, new()
{
    public int Version { get; set; } = 2;
    public int Page { get; set; }
    public T?[][] Bars { get; set; } = [];

    public static QuickbarSave<T>? Read(string? current, string? legacy)
    {
        try
        {
            if (current != null) return JsonSerializer.Deserialize<QuickbarSave<T>>(current);
            if (legacy != null)
            {
                var spells = JsonSerializer.Deserialize<int[]?[]>(legacy);
                if (spells == null) return null;
                return new()
                {
                    Bars = [spells.Take(12).Select(x => x is { Length: 2 }
                    ? new T { Kind = "spell", Id = x[0], Class = x[1] } : null).ToArray()]
                };
            }
        }
        catch (JsonException) { }
        return null;
    }
}
