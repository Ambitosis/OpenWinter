using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;

static class VoiceAudit
{
    public static void Run(ResMan resources)
    {
        var table = resources.TwoDA("soundset");
        int sets = 0, entries = 0, silent = 0, missing = 0, malformed = 0;
        for (int row = 0; row < table.RowCount; row++)
        {
            string name = table.Get(row, "RESREF"); if (name.Length == 0) continue;
            if (resources.Read(name, ResType.Ssf) is not { } data) { Console.WriteLine($"missing SSF {row}: {name}"); missing++; continue; }
            SoundSet set;
            try { set = SoundSet.Read(data); } catch (InvalidDataException e) { Console.WriteLine($"invalid SSF {row}: {e.Message}"); malformed++; continue; }
            sets++;
            for (int slot = 0; slot < set.Entries.Count; slot++)
            {
                var entry = set.Get(slot); entries++;
                if (entry.Sound.Length == 0) silent++;
                else if (!resources.Contains(entry.Sound, ResType.Wav)) { Console.WriteLine($"missing sound {row}/{slot}: {entry.Sound}"); missing++; }
            }
        }
        Console.WriteLine($"Soundsets: rows={table.RowCount}, parsed={sets}, entries={entries}, silent={silent}, missing={missing}, malformed={malformed}");
        var spells = resources.TwoDA("spells");
        var chants = new HashSet<string>(StringComparer.OrdinalIgnoreCase); int references = 0, absent = 0;
        foreach (string col in new[] { "ConjSoundMale", "ConjSoundFemale" })
            for (int row = 0; row < spells.RowCount; row++)
            {
                string chant = spells.Get(row, col); if (chant.Length == 0) continue; references++; chants.Add(chant);
                if (!resources.Contains(chant, ResType.Wav)) { Console.WriteLine($"missing chant {row}/{col}: {chant}"); absent++; }
            }
        Console.WriteLine($"Spell chants: rows={spells.RowCount}, references={references}, unique={chants.Count}, missing={absent}");
        foreach (string chant in chants.Order()) Console.WriteLine($"chant {chant}");
    }
}
