using System.Runtime.CompilerServices;

namespace OpenWinter.Game.Scripting;

/// <summary>
/// Script introspection and control flow requested by engine functions:
/// AbortRunningScript, SetJmp/LongJmp and GetScriptBacktrace. The VM calls
/// <see cref="AfterEngineCall"/> after an ACTION when a request is pending.
/// </summary>
public sealed partial class ScriptThread
{
    /// <summary>A SetJmp target: where to resume and the stack shape to restore.</summary>
    public sealed class JumpPoint
    {
        public required int Ip;
        public required int Sp;
        public required int Bp;
        public required int[] Returns;
    }

    sealed class Control
    {
        public bool Abort;
        public JumpPoint? Jump;
        public int JumpValue;
    }

    Control? _control;
    readonly Dictionary<string, JumpPoint> _jumps = new(StringComparer.Ordinal);

    /// <summary>Code offset of the ACTION instruction currently being executed.</summary>
    public int CallIp { get; internal set; }
    public int BasePointer => _bp;
    /// <summary>Return addresses of enclosing script functions, innermost first (code offsets).</summary>
    public IEnumerable<int> ReturnAddresses => RunsCompiled ? Returns.Select(id => Program.ReturnSites[id]) : Returns;
    /// <summary>Compiled code pushes return-site ids, which <see cref="ReturnAddresses"/> maps to code offsets.</summary>
    internal bool RunsCompiled;
    /// <summary>True while an abort or jump is waiting to be applied after the current engine call.</summary>
    public bool HasPendingControl => _control != null;
    /// <summary>The live stack, bottom first.</summary>
    public ReadOnlySpan<Cell> StackCells => _stack.AsSpan(0, _sp);

    /// <summary>Stops the script once the current engine call returns (AbortRunningScript).</summary>
    public void RequestAbort() => (_control ??= new Control()).Abort = true;

    /// <summary>Records the current call site under a label (SetJmp). Must be called from inside an engine function.</summary>
    public void MarkJump(string label) =>
        _jumps[label] = new JumpPoint { Ip = CallIp, Sp = _sp, Bp = _bp, Returns = Returns.ToArray() };

    public bool HasJump(string label) => _jumps.ContainsKey(label);

    /// <summary>Resumes at the SetJmp with this label once the current engine call returns, as if SetJmp returned <paramref name="value"/>.</summary>
    public void RequestJump(string label, int value)
    {
        if (!_jumps.TryGetValue(label, out var p)) throw new ScriptException($"LongJmp to unknown label '{label}'");
        var c = _control ??= new Control();
        c.Jump = p;
        c.JumpValue = value == 0 ? 1 : value;
    }

    /// <summary>
    /// Applies a pending abort or jump. Returns true when the script must stop;
    /// otherwise <paramref name="resumeOffset"/> is the code offset to continue at (-1 = next instruction).
    /// </summary>
    internal bool AfterEngineCall(out int resumeOffset)
    {
        resumeOffset = -1;
        var c = _control!;
        _control = null;
        if (c.Abort) return true;
        if (c.Jump is { } j)
        {
            while (_sp > j.Sp) Pop();
            if (_sp < j.Sp)
            {
                if (j.Sp > _stack.Length) Array.Resize(ref _stack, Math.Max(j.Sp, _stack.Length * 2));
                _sp = j.Sp;
            }
            _bp = j.Bp;
            Returns.Clear();
            for (int i = j.Returns.Length - 1; i >= 0; i--) Returns.Push(j.Returns[i]);
            Push(Cell.Int(c.JumpValue));
            resumeOffset = j.Ip + 5; // the instruction after SetJmp's ACTION
        }
        return false;
    }

    /// <summary>Thrown by compiled code to unwind on AbortRunningScript.</summary>
    internal sealed class AbortSignal : Exception { }

    /// <summary>Compiled code calls this after ACTION: aborts unwind; jumps can't happen (SetJmp scripts stay interpreted).</summary>
    public void CheckControlCompiled()
    {
        if (_control == null) return;
        if (AfterEngineCall(out _)) throw new AbortSignal();
    }
}

/// <summary>Script chunks (ExecuteScriptChunk source) attached to compiled programs, for GetScriptChunk.</summary>
public static class ScriptChunks
{
    static readonly ConditionalWeakTable<NcsProgram, string> _chunks = new();

    public static void Attach(NcsProgram program, string chunk) => _chunks.AddOrUpdate(program, chunk);
    public static string Of(NcsProgram program) => _chunks.TryGetValue(program, out var s) ? s : "";
}
