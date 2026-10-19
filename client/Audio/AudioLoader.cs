using System.Collections.Concurrent;
using OpenWinter.Aurora.Resources;
using Godot;

namespace OpenWinter.Client.Audio;

/// <summary>Loads Aurora sounds (WAV, BMU = MP3 with a small header, voice files with a prefix) into Godot streams.</summary>
public sealed class AudioLoader
{
    readonly ResMan _rm;
    readonly ConcurrentDictionary<string, AudioStream?> _cache = new(StringComparer.OrdinalIgnoreCase);
    readonly ConcurrentDictionary<string, AudioStream?> _loops = new(StringComparer.OrdinalIgnoreCase);

    public AudioLoader(ResMan rm) { _rm = rm; }

    public AudioStream? Get(string resref) =>
        string.IsNullOrEmpty(resref) ? null : _cache.GetOrAdd(resref, r => Decode(_rm.Read(r, ResType.Bmu) ?? _rm.Read(r, ResType.Wav)));

    /// <summary>A sound already loaded (null when it failed to load); false while it hasn't been loaded yet.</summary>
    public bool TryGetLoaded(string resref, out AudioStream? stream) => _cache.TryGetValue(resref, out stream);

    /// <summary>
    /// Loads sounds on a worker thread (Godot allows resources to be made off the main thread), so an area's sound
    /// objects don't read and decode every sound they can play while the area is being built.
    /// </summary>
    public void Preload(IReadOnlyList<string> resrefs)
    {
        if (resrefs.All(r => string.IsNullOrEmpty(r) || _cache.ContainsKey(r))) return;
        _ = Task.Run(() =>
        {
            foreach (var r in resrefs)
                try { Get(r); }
                catch (Exception e) { GD.PrintErr($"[audio] {r}: {e.Message}"); }
        });
    }

    /// <summary>A separate looping stream: changing it must never make a cached one-shot loop.</summary>
    public AudioStream? GetLooping(string resref) => string.IsNullOrEmpty(resref) ? null : _loops.GetOrAdd(resref, r =>
    {
        if (Get(r)?.Duplicate() is not AudioStream stream) return null;
        if (stream is AudioStreamWav wav)
        {
            // Preserve authored loop regions when present; otherwise repeat the full sample.
            if (wav.LoopMode == AudioStreamWav.LoopModeEnum.Disabled)
            {
                wav.LoopBegin = 0;
                wav.LoopEnd = Math.Max(1, (int)Math.Round(wav.GetLength() * wav.MixRate));
                wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            }
        }
        else if (stream is AudioStreamMP3 mp3) mp3.Loop = true;
        return stream;
    });

    public static AudioStream? Decode(byte[]? data)
    {
        if (data == null || data.Length < 16) return null;
        try
        {
            // BMU: "BMU V1.0" then MP3.
            if (data[0] == 'B' && data[1] == 'M' && data[2] == 'U') return new AudioStreamMP3 { Data = data[8..] };
            int riff = IndexOf(data, "RIFF"u8, 1024);
            if (riff >= 0)
            {
                var wav = riff == 0 ? data : data[riff..];
                // Some voice files are MP3 inside a WAVE container (format 0x55).
                int fmt = IndexOf(wav, "fmt "u8, 256);
                if (fmt >= 0 && fmt + 10 < wav.Length && (wav[fmt + 8] | wav[fmt + 9] << 8) == 0x55)
                {
                    int dataChunk = IndexOf(wav, "data"u8, 512);
                    return dataChunk >= 0 ? new AudioStreamMP3 { Data = wav[(dataChunk + 8)..] } : null;
                }
                int format = fmt >= 0 && fmt + 10 < wav.Length ? wav[fmt + 8] | wav[fmt + 9] << 8 : 1;
                // PCM (1) and float (3) load natively; ADPCM variants are decoded here.
                return format is 1 or 3 ? AudioStreamWav.LoadFromBuffer(wav) : WavDecoder.Decode(wav);
            }
            if (data[0] == 'I' && data[1] == 'D' && data[2] == '3' || data[0] == 0xFF && (data[1] & 0xE0) == 0xE0)
                return new AudioStreamMP3 { Data = data };
        }
        catch (Exception) { }
        return null;
    }

    static int IndexOf(byte[] data, ReadOnlySpan<byte> needle, int limit)
    {
        int end = Math.Min(data.Length, limit + needle.Length);
        return data.AsSpan(0, end).IndexOf(needle);
    }
}
