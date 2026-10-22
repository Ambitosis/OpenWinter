using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace OpenWinter.Net.Nwn.Session;

/// <summary>Session recordings: one JSON line per frame (dir c2s or s2c, ms since the start, hex), gzipped.</summary>
public static class NwnRecording
{
    sealed class Sink(string path, Stopwatch clock) : IDisposable
    {
        readonly StreamWriter _w = new(new GZipStream(File.Create(path), CompressionLevel.SmallestSize));

        public void Write(bool sent, byte[] frame)
        {
            lock (_w)
            {
                _w.WriteLine(JsonSerializer.Serialize(new { dir = sent ? "c2s" : "s2c", ms = clock.ElapsedMilliseconds, hex = Convert.ToHexString(frame) }));
                _w.Flush(); // a killed client still leaves a readable recording
            }
        }

        public void Dispose()
        {
            lock (_w) _w.Dispose();
        }
    }

    /// <summary>A frame observer for <see cref="NwnSession.FrameObserver"/>; dispose its target to finish the file.</summary>
    public static Action<bool, byte[]> Writer(string path, Stopwatch clock)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        return new Sink(path, clock).Write;
    }

    /// <summary>The frames of a recording, in order.</summary>
    public static IEnumerable<(bool Sent, long Ms, byte[] Frame)> Read(string path)
    {
        using var reader = new StreamReader(new GZipStream(File.OpenRead(path), CompressionMode.Decompress));
        while (reader.ReadLine() is { } line)
        {
            var row = JsonDocument.Parse(line).RootElement;
            yield return (row.GetProperty("dir").GetString() == "c2s", row.GetProperty("ms").GetInt64(),
                Convert.FromHexString(row.GetProperty("hex").GetString()!));
        }
    }
}
