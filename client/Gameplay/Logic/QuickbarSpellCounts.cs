namespace OpenWinter.Client.UI;

/// <summary>A memorised, spontaneous or innate spell and its authoritative remaining uses.</summary>
public sealed record BookSpell(int Spell, int Class, int Level, int Meta, int Ready, int Total);

/// <summary>Shared spell count presentation for both quickbars. An empty loaded book means zero uses.</summary>
public readonly record struct QuickbarSpellCounts(int Ready, int Total, bool Loaded)
{
    public int Badge => Loaded ? Ready : -1;
    public bool Usable => !Loaded || Ready > 0;

    public static QuickbarSpellCounts Read(IEnumerable<BookSpell> spells, bool loaded, int spell, int cls, int meta = 255)
    {
        int ready = 0, total = 0;
        foreach (var entry in spells)
            if (entry.Spell == spell && (cls < 0 || entry.Class == cls) && (meta == 255 || entry.Meta == meta))
            {
                ready += entry.Ready;
                total += entry.Total;
            }
        return new(ready, total, loaded);
    }
}
