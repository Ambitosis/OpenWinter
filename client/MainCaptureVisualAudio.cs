using Godot;

namespace OpenWinter.Client;

public partial class Main
{
    readonly System.Collections.Concurrent.ConcurrentQueue<object[]> _visualAudioNotes = new();
    double _visualAudioStarted;
    AudioEffectRecord? _visualMasterRecording;
    int _visualMasterBus, _visualMasterEffect;

    static double VisualAudioUtc() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;

    void StartVisualAudioEvidence()
    {
        _visualMasterBus = AudioServer.GetBusIndex("Master");
        if (_visualMasterBus < 0) throw new InvalidOperationException("Master audio bus is unavailable");
        _visualMasterRecording = new AudioEffectRecord { Format = AudioStreamWav.FormatEnum.Format16Bits };
        _visualMasterEffect = AudioServer.GetBusEffectCount(_visualMasterBus);
        AudioServer.AddBusEffect(_visualMasterBus, _visualMasterRecording);
        _visualAudioNotes.Clear();
        _visualAudioStarted = VisualAudioUtc();
        _visualMasterRecording.SetRecordingActive(true);
    }

    void RecordVisualAudioNote(string line)
    {
        if (_visualAudioStarted > 0) _visualAudioNotes.Enqueue([VisualAudioUtc(), line]);
    }

    void SaveVisualAudioEvidence(string dir)
    {
        if (_visualMasterRecording == null) return;
        _visualMasterRecording.SetRecordingActive(false);
        var recording = _visualMasterRecording.GetRecording();
        AudioServer.RemoveBusEffect(_visualMasterBus, _visualMasterEffect);
        _visualMasterRecording = null;
        if (recording == null) throw new InvalidOperationException("Master audio recording is unavailable");
        var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(dir, "audio.wav"));
        if (recording.SaveToWav(path) != Error.Ok) throw new System.IO.IOException($"Could not save {path}");
        var bytes = recording.Data;
        int peak = 0, channels = recording.Stereo ? 2 : 1;
        if (recording.Format == AudioStreamWav.FormatEnum.Format16Bits)
            for (int i = 0; i + 1 < bytes.Length; i += 2)
                peak = Math.Max(peak, Math.Abs((int)System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i, 2))));
        var report = new
        {
            engine = "openwinter",
            captureStage = "Master bus effect before final bus gain and output device",
            clock = "UTC marker/record-enable timestamps; audio mixer startup latency is not corrected",
            recording = new
            {
                pid = System.Environment.ProcessId, started = _visualAudioStarted, path,
                frames = bytes.Length / (2 * channels), rate = recording.MixRate, channels, pcm_peak = peak,
                format = recording.Format.ToString(), session = new { untouched = true }
            },
            audioSettings = Settings.Current.Audio,
            notes = _visualAudioNotes.ToArray()
        };
        System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "audio.json"),
            System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
        _visualAudioStarted = 0;
    }
}
