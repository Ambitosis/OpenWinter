using System.Collections.Concurrent;
using System.Text;
using Godot;

namespace OpenWinter.Client.Playtest;

/// <summary>One captured log line: when (seconds since the run started), where it came from, and its text.</summary>
public readonly record struct LogLine(double Time, string Source, string Text, bool Error, bool Warning);

/// <summary>
/// Everything the game prints during a playtest: Godot's log (messages, errors with backtraces, warnings
/// such as the "[content]" missing-model notes), the server's console and the script VM's log. Keeps the
/// recent lines for bug excerpts and queues the interesting ones for the watchdogs. Thread safe: Godot
/// may log from any thread.
/// </summary>
public sealed partial class PlaytestLog
{
    readonly ConcurrentQueue<LogLine> _recent = new();
    readonly ConcurrentQueue<LogLine> _pending = new();
    readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    StreamWriter? _file;
    readonly object _fileLock = new();

    public double Now => _clock.Elapsed.TotalSeconds;

    /// <summary>Also writes every line to this file (the full log of the run).</summary>
    public void OpenFile(string path)
    {
        lock (_fileLock) _file = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
    }

    public void Close()
    {
        lock (_fileLock) { _file?.Dispose(); _file = null; }
    }

    public void Add(string source, string text, bool error = false, bool warning = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var line = new LogLine(Now, source, text.TrimEnd(), error, warning);
        _recent.Enqueue(line);
        while (_recent.Count > 400 && _recent.TryDequeue(out _)) { }
        _pending.Enqueue(line);
        lock (_fileLock) _file?.WriteLine($"{line.Time,8:0.0} [{source}] {(error ? "ERROR " : warning ? "WARN " : "")}{line.Text}");
    }

    /// <summary>Lines added since the last call (for the watchdogs).</summary>
    public IEnumerable<LogLine> Drain()
    {
        while (_pending.TryDequeue(out var l)) yield return l;
    }

    /// <summary>The last <paramref name="count"/> lines, oldest first.</summary>
    public List<string> Tail(int count = 25) =>
        _recent.ToArray().TakeLast(count).Select(l => $"{l.Time,7:0.0} [{l.Source}] {l.Text}").ToList();

    /// <summary>Godot's logger: prints, errors and warnings (including C# exceptions Godot reports).</summary>
    public sealed partial class GodotSink : Logger
    {
        public PlaytestLog Log = null!;

        public override void _LogMessage(string message, bool error) => Log.Add("godot", message, error: error);

        public override void _LogError(string function, string file, int line, string code, string rationale, bool editorNotify, int errorType,
            Godot.Collections.Array<ScriptBacktrace> scriptBacktraces)
        {
            var text = rationale.Length > 0 ? rationale : code;
            if (code.Length > 0 && rationale.Length > 0 && code != rationale) text = $"{rationale} ({code})";
            // Godot 4: 0 error, 1 warning, 2 script error, 3 shader error.
            bool warning = errorType == 1;
            Log.Add("godot", $"{text}  at {function} ({System.IO.Path.GetFileName(file)}:{line})", error: !warning, warning: warning);
        }
    }

    /// <summary>Copies the process console (the server prints there) into the log while still writing it out.</summary>
    public sealed class ConsoleTee : TextWriter
    {
        readonly TextWriter _inner;
        readonly PlaytestLog _log;
        readonly StringBuilder _line = new();
        public ConsoleTee(TextWriter inner, PlaytestLog log) { _inner = inner; _log = log; }
        public override Encoding Encoding => _inner.Encoding;

        public override void Write(char value)
        {
            _inner.Write(value);
            lock (_line)
            {
                if (value == '\n') { _log.Add("console", _line.ToString()); _line.Clear(); }
                else if (value != '\r') _line.Append(value);
            }
        }

        public override void Write(string? value)
        {
            if (value == null) return;
            foreach (var c in value) Write(c);
        }

        public override void WriteLine(string? value)
        {
            Write(value);
            Write('\n');
        }

        public override void Flush() => _inner.Flush();
    }
}
