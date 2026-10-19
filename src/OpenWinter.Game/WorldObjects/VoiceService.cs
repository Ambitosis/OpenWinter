using System.Runtime.CompilerServices;
using OpenWinter.Aurora.Formats;
using OpenWinter.Aurora.Resources;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.WorldObjects;

/// <summary>One authoritative sound-set choice for scripts, PC quickchat and NPC barks.</summary>
public static class VoiceService
{
    static readonly ConditionalWeakTable<ResMan, Dictionary<string, SoundSet?>> Cache = new();
    public static SoundSet.Entry Resolve(ResMan resources, int soundset, int slot)
    {
        string name = resources.TwoDA("soundset").Get(soundset, "RESREF");
        var cache = Cache.GetOrCreateValue(resources);
        lock (cache)
        {
            if (!cache.TryGetValue(name, out var set))
            {
                try { set = resources.Read(name, ResType.Ssf) is { } bytes ? SoundSet.Read(bytes) : null; }
                catch (InvalidDataException) { set = null; }
                cache[name] = set;
            }
            return set?.Get(slot) ?? new("", uint.MaxValue);
        }
    }

    public static void Play(World world, Creature creature, int slot, bool caption = true)
    {
        if (slot is < 0 or > 48 || creature.Area == null || creature.IsDestroyed) return;
        var entry = Resolve(world.Resources, creature.SoundSet, slot);
        if (entry.Sound.Length != 0)
            world.Output?.Notify("voice-chat", creature, new { id = slot, soundset = creature.SoundSet, sound = entry.Sound });
        string text = world.Resources.Talk.Get(entry.StringRef);
        if (caption && text.Length != 0) world.Output?.Speak(creature, text, 0);
    }
}
