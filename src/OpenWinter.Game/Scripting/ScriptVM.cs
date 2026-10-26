using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Scripting;

/// <summary>One decoded NCS instruction. Jump targets are absolute code offsets.</summary>
public readonly struct NcsInstruction
{
    public readonly int Offset, Size;
    public readonly byte Op, Type;
    public readonly int A, B, C;
    public readonly string? S;

    public NcsInstruction(int offset, int size, byte op, byte type, int a = 0, int b = 0, int c = 0, string? s = null)
    {
        Offset = offset; Size = size; Op = op; Type = type; A = a; B = b; C = c; S = s;
    }

    public int Next => Offset + Size;
}

/// <summary>A compiled script (NCS V1.0). Code addresses are relative to the byte after the 13-byte header.</summary>
public sealed class NcsProgram
{
    public const int HeaderSize = 13;
    public readonly string Name;
    public readonly byte[] Code;
    public readonly NcsInstruction[] Instructions;
    /// <summary>Instruction index by code offset (-1 where no instruction starts).</summary>
    readonly int[] _indexAt;

    /// <summary>Times this program has been started (drives JIT tiering).</summary>
    public int Runs;
    /// <summary>The JIT-compiled form, once available: (thread, entry offset).</summary>
    public Action<ScriptThread, int>? Compiled;
    public bool CompileFailed;
    /// <summary>A background compile has been started.</summary>
    public bool CompileQueued;
    /// <summary>Code offset of each return site of the compiled form, by return-site id.</summary>
    internal int[] ReturnSites = Array.Empty<int>();

    public NcsProgram(string name, byte[] file)
    {
        Name = name;
        if (file.Length < HeaderSize || Encoding.ASCII.GetString(file, 0, 8) != "NCS V1.0")
            throw new InvalidDataException($"{name}: not an NCS V1.0 file");
        Code = file.AsSpan(HeaderSize).ToArray();
        Instructions = Decode(Code, name);
        _indexAt = new int[Code.Length + 1];
        Array.Fill(_indexAt, -1);
        for (int i = 0; i < Instructions.Length; i++) _indexAt[Instructions[i].Offset] = i;
    }

    public int IndexAt(int offset) =>
        offset >= 0 && offset < _indexAt.Length && _indexAt[offset] >= 0 ? _indexAt[offset] : throw new ScriptException($"{Name}: no instruction at {offset}");

    static NcsInstruction[] Decode(byte[] code, string name)
    {
        var list = new List<NcsInstruction>(code.Length / 4);
        int ip = 0;
        while (ip < code.Length)
        {
            if (ip + 2 > code.Length) break;
            byte op = code[ip], type = code[ip + 1];
            NcsInstruction ins;
            try
            {
                ins = Decode(code, ip, op, type, name);
            }
            catch (Exception e) when (e is ArgumentOutOfRangeException or IndexOutOfRangeException)
            {
                throw new InvalidDataException($"{name}: truncated instruction at {ip}");
            }
            if (ins.Next > code.Length) throw new InvalidDataException($"{name}: truncated instruction at {ip}");
            list.Add(ins);
            ip += ins.Size;
        }
        return list.ToArray();
    }

    static NcsInstruction Decode(byte[] code, int ip, byte op, byte type, string name)
    {
        int I32(int at) => BinaryPrimitives.ReadInt32BigEndian(code.AsSpan(at));
        short I16(int at) => BinaryPrimitives.ReadInt16BigEndian(code.AsSpan(at));
        ushort U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(code.AsSpan(at));
        return op switch
        {
            0x01 or 0x03 or 0x26 or 0x27 => new(ip, 8, op, type, I32(ip + 2), I16(ip + 6)),
            0x02 => new(ip, 2, op, type),
            // Engine-structure constants: json (0x17) carries text; the others (LOCATION_INVALID is 0x12) an int.
            0x04 => type switch
            {
                3 or 4 or 6 => new(ip, 6, op, type, I32(ip + 2)),
                5 or 0x17 => new(ip, 4 + U16(ip + 2), op, type, s: Aurora.Text.Windows1252.GetString(code, ip + 4, U16(ip + 2))),
                >= 0x10 => new(ip, 6, op, type, I32(ip + 2)),
                _ => throw new InvalidDataException($"{name}: bad CONST type {type} at {ip}"),
            },
            0x05 => new(ip, 5, op, type, U16(ip + 2), code[ip + 4]),
            0x0B or 0x0C => type == 0x24 ? new(ip, 4, op, type, I16(ip + 2)) : new(ip, 2, op, type),
            0x1B or 0x23 or 0x24 or 0x28 or 0x29 => new(ip, 6, op, type, I32(ip + 2)),
            0x1D or 0x1E or 0x1F or 0x25 => new(ip, 6, op, type, ip + I32(ip + 2)),
            0x21 => new(ip, 8, op, type, I16(ip + 2), I16(ip + 4), I16(ip + 6)),
            0x2C => new(ip, 10, op, type, I32(ip + 2), I32(ip + 6), ip + 16),
            _ => new(ip, 2, op, type),
        };
    }
}

/// <summary>One stack cell: ints, floats (as bits) and object ids in I; strings and engine structures in R.</summary>
public struct Cell
{
    public int I;
    public object? R;

    public float F => BitConverter.Int32BitsToSingle(I);
    public static Cell Int(int v) => new() { I = v };
    public static Cell Float(float v) => new() { I = BitConverter.SingleToInt32Bits(v) };
    public static Cell Obj(uint id) => new() { I = unchecked((int)id) };
    public static Cell Ref(object? r) => new() { R = r };
}

/// <summary>A saved script state (STORE_STATE): the code an "action" argument refers to, with its captured stack.</summary>
public sealed class ScriptSituation
{
    public required NcsProgram Program;
    public required int ResumeIp;
    public required Cell[] Globals;
    public required Cell[] Locals;
    public required uint Self;
    public string ScriptName => Program.Name;
}

public sealed class ScriptException : Exception
{
    public ScriptException(string message) : base(message) { }

    /// <summary>TMI: the scripts that (via ExecuteScript) called the failing one abort too.</summary>
    public bool AbortsCallers { get; init; }

    public const string TooManyInstructions = "TOO MANY INSTRUCTIONS";
}

/// <summary>
/// Executes NCS bytecode, either interpreted or JIT-compiled to .NET IL once a
/// program becomes hot. Both paths share the per-instruction operations on
/// <see cref="ScriptThread"/>; they differ only in control flow.
/// </summary>
public sealed class ScriptVM
{
    /// <summary>The original's default: 2^20 instructions per run, shared by the scripts it ExecuteScripts.</summary>
    public const int DefaultInstructionLimit = 1_048_576;
    /// <summary>Script function calls nest at most this deep (the entry call into main counts), then STACK OVERFLOW.</summary>
    public const int MaxCallDepth = 124;
    /// <summary>Instructions used by the current outermost run and everything it ExecuteScripts.</summary>
    public int InstructionsUsed;

    public readonly NssDefinitions Definitions;
    public readonly IScriptEngine Engine;
    public int InstructionLimit = DefaultInstructionLimit;
    /// <summary>The result of the last StartingConditional script to finish.</summary>
    public int LastConditionalResult;
    /// <summary>Runs before a program is compiled; 0 compiles on first use, negative disables the JIT.</summary>
    public int JitThreshold = 8;
    public int CompiledPrograms => Volatile.Read(ref _compiledPrograms);
    int _compiledPrograms;
    /// <summary>Compile hot programs on a worker thread (default) instead of on the run that crosses the threshold.</summary>
    public bool BackgroundCompile = true;

    public ScriptVM(NssDefinitions definitions, IScriptEngine engine)
    {
        Definitions = definitions;
        Engine = engine;
    }

    /// <summary>Runs a program from its entry point. Returns the int result for StartingConditional scripts.</summary>
    public int Run(NcsProgram program, uint self)
    {
        var t = new ScriptThread(this, program, self);
        if (Running.Count == 0) InstructionsUsed = 0;
        Running.Add(t);
        try { Execute(t, 0); }
        finally { Running.RemoveAt(Running.Count - 1); }
        return t.ConditionalResult;
    }

    /// <summary>Script threads currently executing, outermost first (script recursion levels).</summary>
    public readonly List<ScriptThread> Running = new();

    /// <summary>Runs a stored action (DelayCommand / AssignCommand / ActionDoCommand).</summary>
    public void Run(ScriptSituation s, uint self)
    {
        var t = new ScriptThread(this, s.Program, self);
        t.Load(s);
        if (Running.Count == 0) InstructionsUsed = 0;
        Running.Add(t);
        try { Execute(t, s.ResumeIp); }
        finally { Running.RemoveAt(Running.Count - 1); }
    }

    void Execute(ScriptThread t, int entry)
    {
        var p = t.Program;
        p.Runs++;
        if (p.Compiled == null && !p.CompileFailed && !p.CompileQueued && JitThreshold >= 0 && p.Runs > JitThreshold)
        {
            // SetJmp/LongJmp can resume at any call site, which compiled branches can't express.
            if (Definitions.ByName.TryGetValue("SetJmp", out var setJmp) && p.Instructions.Any(i => i.Op == 0x05 && i.A == setJmp.Index))
                p.CompileFailed = true;
            else if (BackgroundCompile)
            {
                // Like .NET's tiered compilation: keep interpreting while a worker thread builds and JITs
                // the native version, so a big script never stalls the simulation.
                p.CompileQueued = true;
                Task.Run(() =>
                {
                    try
                    {
                        var compiled = NcsCompiler.Compile(p);
                        // Calling with an entry point the program doesn't have JITs the method and throws straight away.
                        try { compiled(new ScriptThread(this, p, 0), int.MinValue); } catch (ScriptException) { }
                        Volatile.Write(ref p.Compiled, compiled);
                        Interlocked.Increment(ref _compiledPrograms);
                    }
                    catch (Exception) { p.CompileFailed = true; }
                });
            }
            else
                try { p.Compiled = NcsCompiler.Compile(p); Interlocked.Increment(ref _compiledPrograms); }
                catch (Exception) { p.CompileFailed = true; }
        }
        long start = Stats != null ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        try
        {
            if (Volatile.Read(ref p.Compiled) is { } compiled) { t.RunsCompiled = true; compiled(t, entry); }
            else t.Interpret(entry);
        }
        catch (ScriptThread.AbortSignal) { }
        catch (Exception e) when (e is not ScriptException and not OutOfMemoryException)
        {
            // A fault inside the VM (such as malformed code reading outside the stack) aborts the script like any
            // script error instead of escaping into the caller.
            throw new ScriptException($"{e.GetType().Name}: {e.Message}");
        }
        finally
        {
            if (Stats != null)
            {
                // Inclusive of nested scripts (ExecuteScript) and engine calls.
                var (runs, ins, ms) = Stats.GetValueOrDefault(p.Name);
                Stats[p.Name] = (runs + 1, ins + t.InstructionsExecuted, ms + (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
            }
        }
    }

    /// <summary>When set, per-program runs, instructions and wall time (diagnostics).</summary>
    public Dictionary<string, (int Runs, long Instructions, double Ms)>? Stats;
}

/// <summary>Engine-side services the VM needs.</summary>
public interface IScriptEngine
{
    /// <summary>Calls an engine function; args are in declaration order.</summary>
    object? Call(ScriptThread thread, NssFunction function, object?[] args);
    object DefaultEngineValue(NssType type);
    bool EngineEquals(NssType type, object? a, object? b);
}

/// <summary>The running state of one script invocation, and the instruction operations both execution paths use.</summary>
public sealed partial class ScriptThread
{
    readonly ScriptVM _vm;
    public readonly NcsProgram Program;
    /// <summary>OBJECT_SELF.</summary>
    public uint Self;
    Cell[] _stack = new Cell[256];
    int _sp, _bp;
    /// <summary>Return addresses: code offsets (interpreter) or return-site ids (compiled code).</summary>
    public readonly Stack<int> Returns = new();
    ScriptSituation? _pendingAction;
    public int ConditionalResult;
    public int InstructionsExecuted;
    readonly int _limit;

    public ScriptThread(ScriptVM vm, NcsProgram program, uint self)
    {
        _vm = vm;
        Program = program;
        Self = self;
        _limit = vm.InstructionLimit;
    }

    public ScriptVM VM => _vm;
    public int StackPointer => _sp;

    internal void Load(ScriptSituation s)
    {
        foreach (var c in s.Globals) Push(c);
        _bp = _sp;
        foreach (var c in s.Locals) Push(c);
    }

    // ---- stack primitives ----

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void Push(Cell c)
    {
        if (_sp == _stack.Length) Array.Resize(ref _stack, _stack.Length * 2);
        _stack[_sp++] = c;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    Cell Pop()
    {
        if (_sp <= 0) throw new ScriptException("stack underflow");
        var c = _stack[--_sp];
        _stack[_sp].R = null;
        return c;
    }

    void PushVector(Vector3 v) { Push(Cell.Float(v.X)); Push(Cell.Float(v.Y)); Push(Cell.Float(v.Z)); }

    Vector3 PopVector()
    {
        float z = Pop().F, y = Pop().F, x = Pop().F;
        return new Vector3(x, y, z);
    }

    /// <summary>Counts executed instructions against the run's shared budget; throws on TMI.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Tick(int n)
    {
        InstructionsExecuted += n;
        if ((_vm.InstructionsUsed += n) > _limit) throw new ScriptException(ScriptException.TooManyInstructions) { AbortsCallers = true };
    }

    /// <summary>JSR: pushes a return address; past <see cref="ScriptVM.MaxCallDepth"/> the script fails with STACK OVERFLOW.</summary>
    public void PushReturn(int address)
    {
        if (Returns.Count >= ScriptVM.MaxCallDepth) throw new ScriptException("STACK OVERFLOW");
        Returns.Push(address);
    }

    // ---- instruction operations (shared by interpreter and JIT) ----

    public void CpDownSp(int offBytes, int sizeBytes)
    {
        int n = sizeBytes / 4, dst = _sp + offBytes / 4, src = _sp - n;
        for (int i = 0; i < n; i++) _stack[dst + i] = _stack[src + i];
    }

    public void CpTopSp(int offBytes, int sizeBytes)
    {
        int n = sizeBytes / 4, src = _sp + offBytes / 4;
        for (int i = 0; i < n; i++) Push(_stack[src + i]);
    }

    public void CpDownBp(int offBytes, int sizeBytes)
    {
        int n = sizeBytes / 4, dst = _bp + offBytes / 4, src = _sp - n;
        for (int i = 0; i < n; i++) _stack[dst + i] = _stack[src + i];
    }

    public void CpTopBp(int offBytes, int sizeBytes)
    {
        int n = sizeBytes / 4, src = _bp + offBytes / 4;
        for (int i = 0; i < n; i++) Push(_stack[src + i]);
    }

    public void RsAdd(int type) => Push(type switch
    {
        3 => Cell.Int(0),
        4 => Cell.Float(0),
        5 => Cell.Ref(""),
        6 => Cell.Obj(GameObject.InvalidId),
        >= 0x10 => Cell.Ref(_vm.Engine.DefaultEngineValue((NssType)((int)NssType.Effect + (type - 0x10)))),
        _ => throw new ScriptException($"bad RSADD type {type}"),
    });

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ConstInt(int v) => Push(Cell.Int(v));
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ConstFloatBits(int bits) => Push(new Cell { I = bits });
    public void ConstString(string s) => Push(Cell.Ref(s));

    /// <summary>Compiled object constants: 0 = OBJECT_SELF, 1 = OBJECT_INVALID.</summary>
    public void ConstObject(int v) => Push(Cell.Obj(v == 0 ? Self : v == 1 ? GameObject.InvalidId : unchecked((uint)v)));

    /// <summary>EE engine-structure constants: json literals carry text, LOCATION_INVALID an int.</summary>
    public void ConstEngine(int type, int value, string? text) =>
        Push(Cell.Ref(_vm.Engine.Call(this, ConstantFactory, new object?[] { type, (object?)text ?? value })));

    public void Action(int routine, int argc, int callOffset)
    {
        CallIp = callOffset;
        var defs = _vm.Definitions.Functions;
        if (routine >= defs.Count) throw new ScriptException($"unknown engine routine {routine}");
        var f = defs[routine];
        var args = new object?[Math.Min(argc, f.Params.Length)];
        for (int i = 0; i < args.Length; i++)
        {
            args[i] = f.Params[i].Type switch
            {
                NssType.Int => Pop().I,
                NssType.Float => Pop().F,
                NssType.String => (string?)Pop().R ?? "",
                NssType.Object => unchecked((uint)Pop().I),
                NssType.Vector => PopVector(),
                NssType.Action => TakeAction(),
                _ => Pop().R,
            };
        }
        var result = _vm.Engine.Call(this, f, args);
        switch (f.Returns)
        {
            case NssType.Void: break;
            case NssType.Int: Push(Cell.Int(result is int i ? i : result is bool b ? (b ? 1 : 0) : System.Convert.ToInt32(result ?? 0))); break;
            case NssType.Float: Push(Cell.Float(result is float fl ? fl : System.Convert.ToSingle(result ?? 0f))); break;
            case NssType.String: Push(Cell.Ref(result as string ?? "")); break;
            case NssType.Object: Push(Cell.Obj(result is uint o ? o : GameObject.InvalidId)); break;
            case NssType.Vector: PushVector(result is Vector3 v ? v : Vector3.Zero); break;
            default: Push(Cell.Ref(result ?? _vm.Engine.DefaultEngineValue(f.Returns))); break;
        }
    }

    ScriptSituation? TakeAction()
    {
        var a = _pendingAction;
        _pendingAction = null;
        return a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogAnd() { int b = Pop().I, a = Pop().I; Push(Cell.Int(a != 0 && b != 0 ? 1 : 0)); }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogOr() { int b = Pop().I, a = Pop().I; Push(Cell.Int(a != 0 || b != 0 ? 1 : 0)); }
    public void IncOr() { int b = Pop().I, a = Pop().I; Push(Cell.Int(a | b)); }
    public void ExcOr() { int b = Pop().I, a = Pop().I; Push(Cell.Int(a ^ b)); }
    public void BoolAnd() { int b = Pop().I, a = Pop().I; Push(Cell.Int(a & b)); }

    /// <summary>EQUAL / NEQUAL; <paramref name="structBytes"/> is the struct size for type 0x24.</summary>
    public void Equal(int type, int structBytes, bool negate)
    {
        bool eq;
        if (type == 0x24)
        {
            int n = structBytes / 4;
            eq = true;
            for (int i = 0; i < n; i++)
            {
                var a = _stack[_sp - 2 * n + i];
                var b = _stack[_sp - n + i];
                if (a.I != b.I || !Equals(a.R, b.R)) { eq = false; break; }
            }
            for (int i = 0; i < 2 * n; i++) Pop();
        }
        else
        {
            var b = Pop();
            var a = Pop();
            eq = type switch
            {
                0x20 or 0x22 => a.I == b.I,
                0x21 => a.F == b.F,
                0x23 => string.Equals((string?)a.R ?? "", (string?)b.R ?? "", StringComparison.Ordinal),
                >= 0x30 and <= 0x39 => _vm.Engine.EngineEquals((NssType)((int)NssType.Effect + (type - 0x30)), a.R, b.R),
                _ => throw new ScriptException($"bad EQUAL type {type:X2}"),
            };
        }
        Push(Cell.Int(eq != negate ? 1 : 0));
    }

    /// <summary>GEQ/GT/LT/LEQ: kind 0=GEQ, 1=GT, 2=LT, 3=LEQ.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Compare(int type, int kind)
    {
        var b = Pop();
        var a = Pop();
        bool r;
        if (type == 0x21)
        {
            float x = a.F, y = b.F;
            r = kind switch { 0 => x >= y, 1 => x > y, 2 => x < y, _ => x <= y };
        }
        else
        {
            int x = a.I, y = b.I;
            r = kind switch { 0 => x >= y, 1 => x > y, 2 => x < y, _ => x <= y };
        }
        Push(Cell.Int(r ? 1 : 0));
    }

    public void ShLeft() { int b = Pop().I, a = Pop().I; Push(Cell.Int(a << b)); }
    // As in the original: >> shifts a negative number's magnitude (so -16 >> 16 is 0), and >>> is the
    // sign-propagating shift. Shift counts use their low five bits.
    public void ShRight() { int b = Pop().I, a = Pop().I; Push(Cell.Int(a < 0 ? unchecked(-(-a >> b)) : a >> b)); }
    public void UShRight() { int b = Pop().I, a = Pop().I; Push(Cell.Int(a >> b)); }

    /// <summary>ADD/SUB/MUL/DIV with op 0=+, 1=-, 2=*, 3=/.</summary>
    public void Arith(int type, int op)
    {
        switch (type)
        {
            case 0x20: // II
            {
                int b = Pop().I, a = Pop().I;
                if (op == 3) CheckDivisor(a, b);
                Push(Cell.Int(op switch { 0 => unchecked(a + b), 1 => unchecked(a - b), 2 => unchecked(a * b), _ => a / b }));
                break;
            }
            case 0x21:
            case 0x25:
            case 0x26: // FF, IF, FI
            {
                var cb = Pop();
                var ca = Pop();
                float a = type == 0x25 ? ca.I : ca.F;
                float b = type == 0x26 ? cb.I : cb.F;
                if (op == 3 && b == 0) throw new ScriptException(DivideByZero);
                Push(Cell.Float(op switch { 0 => a + b, 1 => a - b, 2 => a * b, _ => a / b }));
                break;
            }
            case 0x23: // SS
            {
                var b = (string?)Pop().R ?? "";
                var a = (string?)Pop().R ?? "";
                Push(Cell.Ref(a + b));
                break;
            }
            case 0x3A: // VV
            {
                var b = PopVector();
                var a = PopVector();
                PushVector(op == 0 ? a + b : a - b);
                break;
            }
            case 0x3B: // VF
            {
                float f = Pop().F;
                var v = PopVector();
                if (op == 3 && f == 0) throw new ScriptException(DivideByZero);
                PushVector(op == 2 ? v * f : v / f);
                break;
            }
            case 0x3C: // FV
            {
                var v = PopVector();
                float f = Pop().F;
                PushVector(v * f);
                break;
            }
            default:
                throw new ScriptException($"bad arithmetic type {type:X2}");
        }
    }

    /// <summary>Fast path for ADD/SUB/MUL on two ints (the JIT emits this for type 0x20).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ArithII(int op)
    {
        if (_sp < 2) throw new ScriptException("stack underflow");
        int b = _stack[_sp - 1].I, a = _stack[_sp - 2].I;
        _sp--;
        if (op == 3) CheckDivisor(a, b);
        _stack[_sp - 1].I = op switch { 0 => unchecked(a + b), 1 => unchecked(a - b), 2 => unchecked(a * b), _ => a / b };
        _stack[_sp - 1].R = null;
    }

    public void Mod()
    {
        int b = Pop().I, a = Pop().I;
        CheckDivisor(a, b);
        Push(Cell.Int(a % b));
    }

    public const string DivideByZero = "DIVIDE BY ZERO";

    /// <summary>Division by zero aborts the script. int.MinValue / -1 crashes the original server; here it aborts too.</summary>
    static void CheckDivisor(int a, int b)
    {
        if (b == 0) throw new ScriptException(DivideByZero);
        if (b == -1 && a == int.MinValue) throw new ScriptException("INTEGER OVERFLOW");
    }

    public void Neg(int type)
    {
        var a = Pop();
        Push(type == 4 ? Cell.Float(-a.F) : Cell.Int(unchecked(-a.I)));
    }

    public void Comp() { var a = Pop(); Push(Cell.Int(~a.I)); }
    public void NotI() { var a = Pop(); Push(Cell.Int(a.I == 0 ? 1 : 0)); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void MovSp(int offBytes)
    {
        int n = -offBytes / 4;
        for (int i = 0; i < n; i++) Pop();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int PopInt() => Pop().I;

    public void Destruct(int sizeBytes, int keepOffBytes, int keepBytes)
    {
        int n = sizeBytes / 4, keepOff = keepOffBytes / 4, keepN = keepBytes / 4;
        int start = _sp - n;
        var kept = new Cell[keepN];
        Array.Copy(_stack, start + keepOff, kept, 0, keepN);
        for (int i = 0; i < n; i++) Pop();
        foreach (var c in kept) Push(c);
    }

    public void DecISp(int offBytes) => _stack[_sp + offBytes / 4].I--;
    public void IncISp(int offBytes) => _stack[_sp + offBytes / 4].I++;
    public void DecIBp(int offBytes) => _stack[_bp + offBytes / 4].I--;
    public void IncIBp(int offBytes) => _stack[_bp + offBytes / 4].I++;

    /// <summary>SAVEBP: BP becomes the current top; the old BP is pushed above it.</summary>
    public void SaveBp()
    {
        int old = _bp;
        _bp = _sp;
        Push(Cell.Int(old));
    }

    public void RestoreBp() => _bp = Pop().I;

    public void StoreState(int bpBytes, int spBytes, int resumeIp)
    {
        int bpN = bpBytes / 4, spN = spBytes / 4;
        var globals = new Cell[bpN];
        Array.Copy(_stack, _bp - bpN, globals, 0, bpN);
        var locals = new Cell[spN];
        Array.Copy(_stack, _sp - spN, locals, 0, spN);
        _pendingAction = new ScriptSituation { Program = Program, ResumeIp = resumeIp, Globals = globals, Locals = locals, Self = Self };
    }

    /// <summary>The outermost RETN: a StartingConditional result sits in the bottom cell.</summary>
    public void Finish()
    {
        if (_sp > 0) _vm.LastConditionalResult = ConditionalResult = _stack[0].I;
    }

    /// <summary>Pseudo-function used to build engine-structure constants (EE json literals).</summary>
    static readonly NssFunction ConstantFactory = new(-1, "__constant", NssType.Void, Array.Empty<NssParam>());

    // ---- interpreter ----

    /// <summary>Runs from <paramref name="entry"/> (a code offset) until the outermost RETN.</summary>
    public void Interpret(int entry)
    {
        var code = Program.Instructions;
        int pc = Program.IndexAt(entry);
        while (true)
        {
            if ((uint)pc >= (uint)code.Length) throw new ScriptException($"ran off the end of the code");
            ref readonly var ins = ref code[pc];
            Tick(1);
            switch (ins.Op)
            {
                case 0x01: CpDownSp(ins.A, ins.B); break;
                case 0x02: RsAdd(ins.Type); break;
                case 0x03: CpTopSp(ins.A, ins.B); break;
                case 0x04:
                    switch (ins.Type)
                    {
                        case 3: ConstInt(ins.A); break;
                        case 4: ConstFloatBits(ins.A); break;
                        case 5: ConstString(ins.S!); break;
                        case 6: ConstObject(ins.A); break;
                        default: ConstEngine(ins.Type, ins.A, ins.S); break;
                    }
                    break;
                case 0x05:
                    Action(ins.A, ins.B, ins.Offset);
                    if (HasPendingControl)
                    {
                        if (AfterEngineCall(out int resume)) return; // AbortRunningScript
                        if (resume >= 0) { pc = Program.IndexAt(resume); continue; } // LongJmp
                    }
                    break;
                case 0x06: LogAnd(); break;
                case 0x07: LogOr(); break;
                case 0x08: IncOr(); break;
                case 0x09: ExcOr(); break;
                case 0x0A: BoolAnd(); break;
                case 0x0B: Equal(ins.Type, ins.A, negate: false); break;
                case 0x0C: Equal(ins.Type, ins.A, negate: true); break;
                case 0x0D: Compare(ins.Type, 0); break;
                case 0x0E: Compare(ins.Type, 1); break;
                case 0x0F: Compare(ins.Type, 2); break;
                case 0x10: Compare(ins.Type, 3); break;
                case 0x11: ShLeft(); break;
                case 0x12: ShRight(); break;
                case 0x13: UShRight(); break;
                case 0x14: Arith(ins.Type, 0); break;
                case 0x15: Arith(ins.Type, 1); break;
                case 0x16: Arith(ins.Type, 2); break;
                case 0x17: Arith(ins.Type, 3); break;
                case 0x18: Mod(); break;
                case 0x19: Neg(ins.Type); break;
                case 0x1A: Comp(); break;
                case 0x1B: MovSp(ins.A); break;
                case 0x1D: pc = Program.IndexAt(ins.A); continue;
                case 0x1E: PushReturn(ins.Next); pc = Program.IndexAt(ins.A); continue;
                case 0x1F: if (PopInt() == 0) { pc = Program.IndexAt(ins.A); continue; } break;
                case 0x25: if (PopInt() != 0) { pc = Program.IndexAt(ins.A); continue; } break;
                case 0x20:
                    if (Returns.Count == 0) { Finish(); return; }
                    pc = Program.IndexAt(Returns.Pop());
                    continue;
                case 0x21: Destruct(ins.A, ins.B, ins.C); break;
                case 0x22: NotI(); break;
                case 0x23: DecISp(ins.A); break;
                case 0x24: IncISp(ins.A); break;
                case 0x26: CpDownBp(ins.A, ins.B); break;
                case 0x27: CpTopBp(ins.A, ins.B); break;
                case 0x28: DecIBp(ins.A); break;
                case 0x29: IncIBp(ins.A); break;
                case 0x2A: SaveBp(); break;
                case 0x2B: RestoreBp(); break;
                case 0x2C: StoreState(ins.A, ins.B, ins.C); break;
                case 0x2D: case 0x1C: case 0x42: break;
                default: throw new ScriptException($"unknown opcode 0x{ins.Op:X2} at {ins.Offset}");
            }
            pc++;
        }
    }
}
