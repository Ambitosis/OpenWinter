using System.Globalization;
using System.Numerics;
using System.Text;
using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Compiler;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// Differential tests: every script runs once interpreted and once JIT-compiled, and both runs must make the
/// same engine calls with the same arguments, return the same result and fail with the same error.
/// </summary>
public class ScriptTierAgreementTests
{
    const string Nwscript = """
        #define ENGINE_STRUCTURE_0 effect
        #define ENGINE_STRUCTURE_1 event
        #define ENGINE_STRUCTURE_2 location
        #define ENGINE_STRUCTURE_3 talent
        #define ENGINE_STRUCTURE_4 itemproperty
        #define ENGINE_STRUCTURE_5 sqlquery
        #define ENGINE_STRUCTURE_6 cassowary
        #define ENGINE_STRUCTURE_7 json
        int TRUE = 1;
        int FALSE = 0;
        float PI = 3.141592;
        string GREETING = "hi";
        void RecI(int n);
        void RecF(float f);
        void RecS(string s);
        void RecV(vector v);
        void RecO(object o);
        void RecE(effect e);
        void RecL(location l);
        void DelayCommand(float fSeconds, action aActionToDelay);
        void AssignCommand(object oActionSubject, action aActionToAssign);
        effect MakeEffect(int n);
        int EffectId(effect e);
        object GetObj(int n);
        int Counter();
        int GetScriptInstructionsRemaining();
        void AbortRunningScript(int nFlag = 0);
        location MakeLocation(vector v, float f);
        vector GetVec(int n);
        void Backtrace();
        """;

    static readonly NssDefinitions Defs = NssDefinitions.Parse(Nwscript);

    sealed record Eff(int N);

    /// <summary>A deterministic engine that records every call it receives.</summary>
    sealed class RecordingEngine : IScriptEngine
    {
        public readonly StringBuilder Log = new();
        public readonly Queue<(ScriptSituation Action, uint Self)> Queued = new();
        int _counter;

        public object? Call(ScriptThread thread, NssFunction function, object?[] args)
        {
            Log.Append(function.Name).Append('(').AppendJoin(',', args.Select(Format)).Append(")\n");
            switch (function.Name)
            {
                case "DelayCommand":
                    if (args[1] is ScriptSituation d) Queued.Enqueue((d, thread.Self));
                    return null;
                case "AssignCommand":
                    if (args[1] is ScriptSituation a) Queued.Enqueue((a, (uint)args[0]!));
                    return null;
                case "MakeEffect": return new Eff((int)args[0]!);
                case "EffectId": return args[0] is Eff e ? e.N : -1;
                case "GetObj": return unchecked((uint)(int)args[0]!);
                case "Counter": return ++_counter;
                case "GetScriptInstructionsRemaining": return thread.VM.InstructionLimit - thread.VM.InstructionsUsed;
                case "AbortRunningScript": thread.RequestAbort(); return null;
                case "MakeLocation": return (Vector3)args[0]!;
                case "GetVec": return new Vector3((int)args[0]!, -(int)args[0]!, 0.5f);
                case "Backtrace":
                    // What GetScriptBacktrace reports: the call site, frame pointers and return addresses.
                    Log.Append("ip ").Append(thread.CallIp).Append(" bp ").Append(thread.BasePointer).Append(" sp ").Append(thread.StackPointer)
                        .Append(" returns ").AppendJoin(',', thread.ReturnAddresses).Append('\n');
                    return null;
                default: return null;
            }
        }

        public object DefaultEngineValue(NssType type) => type == NssType.Effect ? new Eff(-1) : type;
        public bool EngineEquals(NssType type, object? a, object? b) => Equals(a, b);
    }

    static string Format(object? v) => v switch
    {
        float f => "f" + BitConverter.SingleToInt32Bits(f).ToString("X8", CultureInfo.InvariantCulture),
        Vector3 x => "[" + Format(x.X) + " " + Format(x.Y) + " " + Format(x.Z) + "]",
        ScriptSituation s => "action@" + s.ResumeIp + "/" + s.Globals.Length + "/" + s.Locals.Length,
        null => "null",
        _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? "",
    };

    static byte[] Compile(string source)
    {
        var ncs = NssCompiler.Compile(source, "t", Defs, _ => null, out var error);
        Assert.True(ncs != null, error);
        return ncs!;
    }

    /// <summary>Runs the script and its queued actions; returns a transcript of everything observable.</summary>
    static string Run(byte[] ncs, bool jit, int instructionLimit)
    {
        var engine = new RecordingEngine();
        var vm = new ScriptVM(Defs, engine) { JitThreshold = jit ? 0 : -1, BackgroundCompile = false, InstructionLimit = instructionLimit };
        var program = new NcsProgram("t", ncs);
        var log = engine.Log;
        void Outcome(Action run)
        {
            try
            {
                run();
                log.Append("used ").Append(vm.InstructionsUsed).Append('\n');
            }
            catch (ScriptException e) { log.Append("error ").Append(e.Message).Append('\n'); }
        }
        Outcome(() => log.Append("result ").Append(vm.Run(program, 7)).Append('\n'));
        for (int n = 0; n < 50 && engine.Queued.Count > 0; n++)
        {
            var (action, self) = engine.Queued.Dequeue();
            log.Append("action ").Append(self).Append('\n');
            Outcome(() => vm.Run(action, self));
        }
        if (jit) Assert.True(program.Compiled != null, "the program did not compile");
        else Assert.Null(program.Compiled);
        return log.ToString();
    }

    static void AssertAgree(string source, int instructionLimit = ScriptVM.DefaultInstructionLimit)
    {
        var ncs = Compile(source);
        var interpreted = Run(ncs, jit: false, instructionLimit);
        var compiled = Run(ncs, jit: true, instructionLimit);
        Assert.Equal(interpreted, compiled);
    }

    public static TheoryData<string, string> Corpus() => new()
    {
        { "int arithmetic", """
            void main()
            {
                int big = 2147483647, small = -2147483647 - 1;
                RecI(big + 1); RecI(small - 1); RecI(big * 2); RecI(small * -1); RecI(-small);
                RecI(7 / 2); RecI(-7 / 2); RecI(7 / -2); RecI(7 % 3); RecI(-7 % 3); RecI(7 % -3); RecI(small % 3);
                RecI(~big); RecI(!big); RecI(!0); RecI(big & 255); RecI(big | small); RecI(big ^ -1);
                int i = 5; i += 3; i -= 1; i *= 4; i /= 3; i %= 5; i <<= 3; i >>= 1; i >>>= 1; i |= 64; i &= 127; i ^= 9;
                RecI(i); RecI(i++); RecI(++i); RecI(i--); RecI(--i);
            }
            """ },
        { "shifts", """
            void main()
            {
                int a = -16, n;
                for (n = -33; n <= 33; n += 3) { RecI(a << n); RecI(a >> n); RecI(a >>> n); RecI(1000 >> n); RecI(1 << n); }
                RecI((-2147483647 - 1) >> 1); RecI((-2147483647 - 1) >>> 31);
            }
            """ },
        { "division by zero", "void main() { int z = 0; RecI(1); RecI(5 / z); RecI(2); }" },
        { "modulo by zero", "void main() { int z = 0; RecI(1); RecI(5 % z); RecI(2); }" },
        { "int overflow division", "void main() { int m = -2147483647 - 1, d = -1; RecI(1); RecI(m / d); }" },
        { "int overflow modulo", "void main() { int m = -2147483647 - 1, d = -1; RecI(1); RecI(m % d); }" },
        { "float division by zero", "void main() { float z = 0.0; RecF(1.0 / 3.0); RecF(1.0 / z); RecI(2); }" },
        { "vector division by zero", "void main() { vector v = [1.0, 2.0, 3.0]; float z = 0.0; RecV(v / 2.0); RecV(v / z); }" },
        { "floats", """
            void main()
            {
                float big = 3.0e38, tiny = 1.0e-38, z = 0.0;
                float inf = big * 10.0, ninf = -inf, nan = inf - inf;
                RecF(inf); RecF(ninf); RecF(nan); RecF(-z); RecF(tiny * tiny); RecF(PI);
                RecI(nan == nan); RecI(nan != nan); RecI(nan < 1.0); RecI(nan > 1.0); RecI(nan <= nan); RecI(nan >= nan);
                RecI(inf > big); RecI(ninf < -big); RecI(z == -z); RecI(inf == inf);
                RecF(1 + 0.5); RecF(0.5 + 1); RecF(3 * 1.5); RecF(7 / 2.0); RecF(7.0 / 2); RecF(-(1.5)); RecF(0.1 + 0.2);
                float f = 1.0; f += 2; f -= 0.25; f *= 3.0; f /= 2.0; RecF(f);
                RecI(1.5 > 1.0); RecI(2.0 >= 2.0); RecI(0.1 + 0.2 == 0.3);
            }
            """ },
        { "strings", """
            string Twice(string s) { return s + s; }
            void main()
            {
                string a = "abc", b = "", c = GREETING;
                RecS(a + b + c); RecS(Twice(a)); RecI(a == "abc"); RecI(a != c); RecI(b == ""); RecS("tab\tquote\"nl\n");
                string s = ""; int i; for (i = 0; i < 5; i++) s += a; RecS(s);
            }
            """ },
        { "vectors", """
            vector Scale(vector v, float f) { return v * f; }
            void main()
            {
                vector a = [1.0, 2.0, 3.0], b = [0.5, -0.5, 4.0];
                RecV(a + b); RecV(a - b); RecV(a * 2.0); RecV(2.0 * a); RecV(a / 4.0); RecV(Scale(b, -1.0));
                RecI(a == b); RecI(a != b); RecI(a == [1.0, 2.0, 3.0]);
                a.x += 10.0; a.y = a.z * a.x; RecV(a); RecF(a.y); RecV(GetVec(3)); RecF(GetVec(4).y);
            }
            """ },
        { "structs", """
            struct Inner { int a; string s; };
            struct Outer { struct Inner i; vector v; float f; object o; };
            struct Outer Make(int n)
            {
                struct Outer r; r.i.a = n; r.i.s = "n" + "x"; r.v = [1.0, 2.0, 3.0]; r.f = 0.5 * n; r.o = GetObj(n);
                return r;
            }
            int Sum(struct Outer o) { return o.i.a * 100 + FloatToIntish(o.f); }
            int FloatToIntish(float f) { int n = 0; while (f >= 1.0) { f -= 1.0; n++; } return n; }
            void main()
            {
                struct Outer a = Make(3), b = Make(3), c = Make(4);
                RecI(a == b); RecI(a != c); RecI(Sum(c)); RecS(c.i.s); RecV(c.v); RecO(c.o); RecF(a.f);
                b.i.a = 9; RecI(a == b); a = b; RecI(a == b); RecI(a.i.a);
            }
            """ },
        { "recursion", """
            int Fib(int n) { if (n < 2) return n; return Fib(n - 1) + Fib(n - 2); }
            int Ack(int m, int n) { if (m == 0) return n + 1; if (n == 0) return Ack(m - 1, 1); return Ack(m - 1, Ack(m, n - 1)); }
            float Pow(float b, int e) { if (e == 0) return 1.0; return b * Pow(b, e - 1); }
            string Rep(string s, int n) { return n <= 0 ? "" : s + Rep(s, n - 1); }
            void main() { RecI(Fib(15)); RecI(Ack(2, 3)); RecF(Pow(1.5, 9)); RecS(Rep("ab", 6)); }
            """ },
        { "deep recursion overflows", "int D(int n) { return n == 0 ? 0 : 1 + D(n - 1); } void main() { RecI(D(100)); RecI(D(200)); }" },
        { "loops", """
            void main()
            {
                int i, j, s = 0;
                for (i = 0; i < 10; i++) { if (i == 2) continue; if (i == 8) break; for (j = 0; j < i; j++) { if (j > 3) break; s += j; } }
                RecI(s);
                i = 0; while (TRUE) { i++; if (i % 2) continue; if (i > 12) break; RecI(i); }
                i = 0; do { i += 3; if (i == 9) continue; RecI(i); } while (i < 20);
                for (;;) { s--; if (s < 0) break; }
                RecI(s);
            }
            """ },
        { "switch", """
            void Sw(int n)
            {
                switch (n)
                {
                    case -1: RecI(-100);
                    case 0: RecI(0); break;
                    case 1: RecI(1);
                    case 2: { RecI(2); break; }
                    case 3: RecI(3); return;
                    default: RecI(99);
                    case 5: RecI(5);
                }
                RecI(1000 + n);
            }
            void main() { int i; for (i = -2; i < 8; i++) Sw(i); switch (Counter()) { } switch (7) { default: RecI(7); } }
            """ },
        { "logic", """
            int Side(int n) { RecI(n); return n; }
            void main()
            {
                RecI(Side(0) && Side(1)); RecI(Side(2) && Side(3)); RecI(Side(4) || Side(5)); RecI(Side(0) || Side(0));
                RecI(5 && 7); RecI(0 || -3); RecI(Side(1) ? Side(10) : Side(20)); RecI(Side(0) ? Side(10) : Side(20));
            }
            """ },
        { "globals", """
            int g = 3;
            float gf = 1.5;
            string gs = "g";
            vector gv = [1.0, 1.0, 1.0];
            object go = OBJECT_SELF;
            effect ge;
            struct S { int a; float b; };
            struct S gstruct;
            void Bump() { g++; gf *= 2.0; gs += "!"; gv.z += 1.0; gstruct.a += g; }
            int StartingConditional()
            {
                int i; for (i = 0; i < 4; i++) Bump();
                RecI(g); RecF(gf); RecS(gs); RecV(gv); RecO(go); RecI(EffectId(ge)); RecI(gstruct.a); RecF(gstruct.b);
                ge = MakeEffect(g); RecE(ge); RecI(ge == MakeEffect(7)); RecI(ge != MakeEffect(8));
                return g * 10;
            }
            """ },
        { "objects", """
            void main()
            {
                object a = OBJECT_SELF, b = OBJECT_INVALID, c = GetObj(-5);
                RecO(a); RecO(b); RecO(c); RecI(a == b); RecI(a != c); RecI(GetObj(7) == OBJECT_SELF);
            }
            """ },
        { "engine types", """
            effect Chain(effect e, int n) { return n == 0 ? e : Chain(MakeEffect(EffectId(e) + 1), n - 1); }
            void main()
            {
                effect e = MakeEffect(1), f;
                RecE(Chain(e, 5)); RecI(EffectId(f)); RecE(f); RecI(e == f);
                location l = MakeLocation([1.0, 2.0, 3.0], 90.0); RecL(l); location m; RecL(m);
            }
            """ },
        { "delay and assign", """
            int g = 1;
            void Later(int n, string s, vector v) { g += n; RecI(g); RecS(s); RecV(v); RecI(Counter()); }
            void Nested(int depth) { RecI(depth); if (depth < 3) DelayCommand(0.5, Nested(depth + 1)); }
            void main()
            {
                int i; string s = "x"; vector v = [1.0, 2.0, 3.0];
                for (i = 0; i < 3; i++) { s += "y"; v.x += 1.0; DelayCommand(IntToFloatish(i), Later(i, s, v)); }
                AssignCommand(GetObj(42), Later(100, "assign", v));
                AssignCommand(GetObj(43), RecO(OBJECT_SELF));
                DelayCommand(1.0, Nested(0));
                g = 50;
                RecI(g);
            }
            float IntToFloatish(int n) { return n * 1.0; }
            """ },
        { "action in a function", """
            struct P { int a; string b; };
            void Show(struct P p, effect e) { RecI(p.a); RecS(p.b); RecE(e); }
            void Queue(int n)
            {
                struct P p; p.a = n; p.b = "q" + "r";
                effect e = MakeEffect(n * 2);
                if (n > 0) DelayCommand(0.0, Show(p, e));
                if (n > 1) Queue(n - 1);
            }
            void main() { Queue(4); }
            """ },
        { "stack heavy", """
            float Mix(int a, int b, int c, int d, float e, float f, string g, vector h, int i, int j, int k, int l)
            {
                return a + b * c - d + e * f + h.x + h.y + h.z + i * j - k / (l == 0 ? 1 : l) + GetStringLengthish(g);
            }
            int GetStringLengthish(string s) { return s == "" ? 0 : 1; }
            void main()
            {
                int a1 = 1, a2 = 2, a3 = 3, a4 = 4, a5 = 5, a6 = 6, a7 = 7, a8 = 8, a9 = 9, a10 = 10;
                float f1 = 0.5, f2 = 1.5, f3 = 2.5; vector v1 = [1.0, 2.0, 3.0], v2 = [4.0, 5.0, 6.0];
                string s1 = "a", s2 = "b";
                int i; float acc = 0.0;
                for (i = 0; i < 200; i++)
                {
                    int t1 = i * a1, t2 = i * a2, t3 = t1 + t2;
                    acc += Mix(t1, t2, t3, a4, f1, f2, s1, v1, a5, a6, a7, i % 5);
                    { vector w = v1 + v2; float q = w.x * f3; acc -= q; string ss = s1 + s2; if (ss == "ab") acc += 1.0; }
                }
                RecF(acc); RecI(a8 + a9 + a10 + a3); RecF(f3); RecV(v2); RecS(s2);
            }
            """ },
        { "instructions remaining", """
            int Burn(int n) { int s = 0; while (n-- > 0) s += n; return s; }
            void main()
            {
                RecI(GetScriptInstructionsRemaining());
                int x = 3 + Burn(5);
                RecI(GetScriptInstructionsRemaining());
                if (x > 2) { x = Burn(x) * 2; RecI(GetScriptInstructionsRemaining()); x = 1; }
                RecI(x + GetScriptInstructionsRemaining());
            }
            """ },
        { "abort", """
            void Deep(int n) { if (n == 0) { RecI(-1); AbortRunningScript(); RecI(-2); } else Deep(n - 1); RecI(n); }
            int StartingConditional() { RecI(1); Deep(3); RecI(2); return 5; }
            """ },
        { "abort in action", """
            void Act(int n) { RecI(n); if (n == 2) AbortRunningScript(); RecI(n * 10); }
            void main() { int i; for (i = 0; i < 4; i++) DelayCommand(0.0, Act(i)); RecI(99); }
            """ },
        { "backtrace", """
            int g = 4;
            void Leaf(int n) { Backtrace(); if (n > 0) Leaf(n - 1); }
            int Mid(string s) { Leaf(2); Backtrace(); return 1; }
            void main() { Backtrace(); Mid("a"); DelayCommand(1.0, Leaf(1)); { int x = Mid("b"); Leaf(x); } }
            """ },
        { "conditional result", "int StartingConditional() { int a = 41; if (a > 40) return a + 1; return 0; }" },
        { "nested scopes", """
            void main()
            {
                int a = 1;
                { int a2 = a + 1; { int a3 = a2 * 3; RecI(a3); } { string t = "s"; RecS(t); } RecI(a2); }
                if (a) { vector v = [1.0, 0.0, 0.0]; RecV(v); } else { float f = 1.0; RecF(f); }
                RecI(a);
            }
            """ },
    };

    [Theory]
    [MemberData(nameof(Corpus))]
    public void InterpreterAndJitAgree(string name, string source)
    {
        _ = name;
        AssertAgree(source);
    }

    /// <summary>Malformed bytecode fails the same way in both tiers.</summary>
    [Theory]
    [InlineData("04030000000104030000000214200000")] // CONSTI 1; CONSTI 2; ADD; opcode 0
    [InlineData("0403000000010403000000021420")] // no RETN
    [InlineData("1D00000000082000 1E00FFFFFFFE")] // JMP to a JSR that is the last instruction; its RETN has nowhere to go
    [InlineData("040300000001 5000 2000")] // unknown opcode
    public void InterpreterAndJitFailAlike(string hex)
    {
        var code = Convert.FromHexString(hex.Replace(" ", ""));
        var file = new List<byte>(Encoding.ASCII.GetBytes("NCS V1.0")) { 0x42 };
        file.AddRange(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(13 + code.Length)));
        file.AddRange(code);
        var ncs = file.ToArray();
        Assert.Equal(Run(ncs, jit: false, ScriptVM.DefaultInstructionLimit), Run(ncs, jit: true, ScriptVM.DefaultInstructionLimit));
    }

    [Theory]
    [InlineData(40)]
    [InlineData(97)]
    [InlineData(150)]
    [InlineData(313)]
    public void InterpreterAndJitAgreeAtTheInstructionLimit(int limit) => AssertAgree("""
        int Step(int n) { RecI(n); return n + 1; }
        void main() { int i = 0, s = 0; while (TRUE) { s += Step(i); i++; if (i % 3 == 0) RecI(GetScriptInstructionsRemaining()); } }
        """, limit);
}
