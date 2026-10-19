using OpenWinter.Game.Scripting;
using OpenWinter.Game.Scripting.Functions;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>Engine-function semantics measured on the original server (see parity/suites).</summary>
public class ScriptSemanticsTests
{
    [Theory]
    [InlineData(0.5f, 0, 0, "1")]
    [InlineData(2.5f, 0, 0, "3")]
    [InlineData(-2.5f, 0, 0, "-3")]
    [InlineData(-0f, 0, 2, "-0.00")]
    [InlineData(1f, 30, 2, "              1.00")]
    [InlineData(1f, -5, -1, "1")]
    [InlineData(3.14159265f, 0, 12, "3.141592741")]
    [InlineData(1e20f, 0, 1, "100000002004087734272.0")]
    [InlineData(1e-10f, 0, 9, "0.000000000")]
    [InlineData(float.PositiveInfinity, 5, 2, "  inf")]
    [InlineData(float.NegativeInfinity, 0, 2, "-inf")]
    public void FloatToStringMatchesOriginal(float f, int width, int decimals, string expected) =>
        Assert.Equal(expected, CoreFunctions.FloatToString(f, width, decimals));

    [Fact]
    public void FloatToStringNanKeepsSign()
    {
        Assert.Equal("nan", CoreFunctions.FloatToString(CoreFunctions.ParseLeadingFloat("nan"), 0, 2));
        Assert.Equal("-nan", CoreFunctions.FloatToString(CoreFunctions.ParseLeadingFloat("-nan"), 0, 2));
    }

    [Theory]
    [InlineData("12abc", 12)]
    [InlineData("  -7  ", -7)]
    [InlineData(" 5", 0)]
    [InlineData("12345678901234567890", int.MaxValue)]
    [InlineData("-99999999999", int.MinValue)]
    [InlineData("+-5", 0)]
    public void StringToIntIsStrtol(string s, int expected) => Assert.Equal(expected, CoreFunctions.ParseLeadingInt(s));

    [Theory]
    [InlineData("0x10", 16f)]
    [InlineData("0x1p3", 8f)]
    [InlineData("1e", 1f)]
    [InlineData("  +2.5x", 2.5f)]
    [InlineData(".e1", 0f)]
    [InlineData("infinity", float.PositiveInfinity)]
    public void StringToFloatIsStrtod(string s, float expected) => Assert.Equal(expected, CoreFunctions.ParseLeadingFloat(s));

    sealed class NoEngine : IScriptEngine
    {
        public object? Call(ScriptThread thread, NssFunction function, object?[] args) => null;
        public object DefaultEngineValue(NssType type) => 0;
        public bool EngineEquals(NssType type, object? a, object? b) => false;
    }

    /// <summary>Runs "CONSTI a; CONSTI b; op; RETN" and returns the int left on the stack.</summary>
    static int RunBinary(byte op, int a, int b)
    {
        var code = new List<byte>();
        foreach (int v in new[] { a, b })
        {
            code.AddRange(new byte[] { 0x04, 0x03 });
            code.AddRange(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(v)));
        }
        code.AddRange(new byte[] { op, 0x20, 0x20, 0x00 });
        var file = new List<byte>(System.Text.Encoding.ASCII.GetBytes("NCS V1.0"));
        file.Add(0x42);
        file.AddRange(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(13 + code.Count)));
        file.AddRange(code);
        var vm = new ScriptVM(NssDefinitions.Parse(""), new NoEngine()) { JitThreshold = -1 };
        return vm.Run(new NcsProgram("t", file.ToArray()), 0);
    }

    static int RunCode(params byte[] code)
    {
        var file = new List<byte>(System.Text.Encoding.ASCII.GetBytes("NCS V1.0"));
        file.Add(0x42);
        file.AddRange(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(13 + code.Length)));
        file.AddRange(code);
        var vm = new ScriptVM(NssDefinitions.Parse(""), new NoEngine()) { JitThreshold = -1 };
        return vm.Run(new NcsProgram("t", file.ToArray()), 0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void LocationConstantsDecode(int jit)
    {
        // CONST.location 0 (LOCATION_INVALID, an int payload); MOVSP -4; CONST.I 7; RETN.
        byte[] code = { 0x04, 0x12, 0, 0, 0, 0, 0x1B, 0x00, 0xFF, 0xFF, 0xFF, 0xFC, 0x04, 0x03, 0, 0, 0, 7, 0x20, 0x00 };
        var file = new List<byte>(System.Text.Encoding.ASCII.GetBytes("NCS V1.0")) { 0x42 };
        file.AddRange(BitConverter.GetBytes(System.Buffers.Binary.BinaryPrimitives.ReverseEndianness(13 + code.Length)));
        file.AddRange(code);
        var vm = new ScriptVM(NssDefinitions.Parse(""), new NoEngine()) { JitThreshold = jit, BackgroundCompile = false };
        Assert.Equal(7, vm.Run(new NcsProgram("t", file.ToArray()), 0));
    }

    [Fact]
    public void RunawayRecursionIsAStackOverflow() =>
        Assert.Equal("STACK OVERFLOW", Assert.Throws<ScriptException>(() => RunCode(0x1E, 0, 0, 0, 0, 0, 0x20, 0)).Message);

    [Fact]
    public void EndlessLoopsHitTheOriginalsInstructionLimit()
    {
        var e = Assert.Throws<ScriptException>(() => RunCode(0x1D, 0, 0, 0, 0, 0));
        Assert.Equal(ScriptException.TooManyInstructions, e.Message);
        Assert.True(e.AbortsCallers);
        Assert.Equal(1_048_576, ScriptVM.DefaultInstructionLimit);
    }

    [Theory]
    [InlineData(-16, 1, -8)]
    [InlineData(-16, 16, 0)]
    [InlineData(int.MinValue, 1, 1073741824)]
    [InlineData(int.MinValue, 31, 1)]
    [InlineData(1000, 33, 500)]
    public void ShiftRightShiftsTheMagnitude(int a, int b, int expected) => Assert.Equal(expected, RunBinary(0x12, a, b));

    [Theory]
    [InlineData(-16, 16, -1)]
    [InlineData(int.MinValue, 1, -1073741824)]
    [InlineData(-1, 31, -1)]
    [InlineData(1000, 1, 500)]
    public void UnsignedShiftRightPropagatesTheSign(int a, int b, int expected) => Assert.Equal(expected, RunBinary(0x13, a, b));

    [Fact]
    public void ShiftLeftUsesLowFiveBits() => Assert.Equal(int.MinValue, RunBinary(0x11, 1, -1));

    [Fact]
    public void DivisionByZeroAborts()
    {
        Assert.Equal(ScriptThread.DivideByZero, Assert.Throws<ScriptException>(() => RunBinary(0x17, 5, 0)).Message);
        Assert.Equal(ScriptThread.DivideByZero, Assert.Throws<ScriptException>(() => RunBinary(0x18, 5, 0)).Message);
        Assert.Throws<ScriptException>(() => RunBinary(0x17, int.MinValue, -1));
    }

    [Theory]
    [InlineData(90f, 82183016f)]
    [InlineData(-270f, -27394338f)]
    public void TanUsesTheOriginalDegreeFactor(float degrees, float expected) =>
        Assert.Equal(expected, (float)Math.Tan(CoreFunctions.Radians(degrees)));

    [Theory]
    [InlineData(float.NaN, int.MinValue)]
    [InlineData(1e10f, int.MinValue)]
    [InlineData(-2.7f, -2)]
    public void FloatToIntIsX86Truncation(float f, int expected) => Assert.Equal(expected, CoreFunctions.FloatToIntX86(f));

    [Fact]
    public void RandomIsFifteenBitsModuloN()
    {
        for (int i = 0; i < 2000; i++)
        {
            Assert.InRange(CoreFunctions.Random15Mod(100000), 0, 32767);
            Assert.InRange(CoreFunctions.Random15Mod(-3), -2, 0);
            Assert.InRange(CoreFunctions.Random15Mod(int.MinValue), -32767, 0);
        }
        Assert.Equal(0, CoreFunctions.Random15Mod(0));
    }

    [Theory]
    [InlineData(-90f, 270f)]
    [InlineData(450f, 90f)]
    [InlineData(-360f, 0f)]
    [InlineData(180.5f, 180.5f)]
    public void LocationFacingIsNormalised(float degrees, float expected) =>
        Assert.Equal(expected, CoreFunctions.NormalizeFacing(degrees), 3);

    [Fact]
    public void NormalizeHandlesZeroAndTinyVectors()
    {
        Assert.Equal(System.Numerics.Vector3.Zero, CoreFunctions.NormalizeVector(System.Numerics.Vector3.Zero));
        var tiny = CoreFunctions.NormalizeVector(new System.Numerics.Vector3(1e-20f, 0, 0));
        Assert.True(float.IsPositiveInfinity(tiny.X) && float.IsNaN(tiny.Y));
    }

    [Theory]
    [InlineData(1.100000023841858, "1.100000023841858")]
    [InlineData(3.0, "3.0")]
    [InlineData(1e20, "1e+20")]
    [InlineData(1e15, "1e+15")]
    [InlineData(999999986991104.0, "999999986991104.0")]
    [InlineData(0.0001, "0.0001")]
    [InlineData(0.00001, "1e-05")]
    [InlineData(-0.0, "-0.0")]
    [InlineData(double.NaN, "null")]
    public void JsonFloatsDumpLikeTheOriginal(double d, string expected) => Assert.Equal(expected, JsonCore.FormatDouble(d));

    [Fact]
    public void JsonDumpSortsKeysAndEscapes()
    {
        var n = JsonCore.Parse("{\"b\":1,\"a\":\"\u00e9\u007f\",\"a\":[1.5e3,-0]}");
        Assert.Equal("{\"a\":[1500.0,0],\"b\":1}", JsonCore.Dump(n));
        Assert.Equal("[\n  1\n]", JsonCore.Dump(JsonCore.Parse("[1]"), 2));
        Assert.Equal("\"\\u00e9\\u007f\\n\"", JsonCore.Dump(System.Text.Json.Nodes.JsonValue.Create("é\u007f\n")));
    }

    [Theory]
    [InlineData("", "[json.exception.parse_error.101] parse error at line 1, column 1: syntax error while parsing value - unexpected end of input; expected '[', '{', or a literal")]
    [InlineData("{x", "[json.exception.parse_error.101] parse error at line 1, column 2: syntax error while parsing object key - invalid literal; last read: '{x'; expected string literal")]
    [InlineData("[1,\n2,\n x]","[json.exception.parse_error.101] parse error at line 3, column 2: syntax error while parsing value - invalid literal; last read: '2,<U+000A> x'")]
    [InlineData("1e400", "[json.exception.out_of_range.406] number overflow parsing '1e400'")]
    public void JsonParseErrorsMatchTheOriginal(string text, string expected) =>
        Assert.Equal(expected, Assert.Throws<JsonCore.ParseException>(() => JsonCore.Parse(text)).Message);

    [Fact]
    public void RegexFollowsStdRegex()
    {
        Assert.Equal(new[] { "xy" }, StdRegex.Match("[[:alpha:]]+", "42xy", 0, 0));
        Assert.Equal(new[] { "abbb", "a" }, StdRegex.Match(@"\(a\)b*", "abbb", 2, 0)); // any other grammar is POSIX basic
        Assert.Empty(StdRegex.Match("a+", "xaaa", 2, 0));
        Assert.Equal("ax", StdRegex.Replace("(a)", "a", "$01$10x", 0, 0));
        Assert.Equal(@"ba\x", StdRegex.Replace("(a)(b)", "ab", @"\2\1\\x", 0, 256));
        Assert.Equal("baa", StdRegex.Replace("a", "aaa", "b", 0, 1024));
        Assert.Equal("regex_error(error_brack): The expression contained mismatched [ and ].",
            Assert.Throws<StdRegex.RegexError>(() => StdRegex.Match("[", "", 0, 0)).Message);
        Assert.Equal("regex_error(error_syntax)", Assert.Throws<StdRegex.RegexError>(() => StdRegex.Match("(?<=a)b", "", 0, 0)).Message);
    }

    static int CompileAndRun(string source, int jit = -1)
    {
        var defs = NssDefinitions.Parse("int TRUE = 1;\nint FALSE = 0;\nint PAIR = TRUE + 1;\n");
        var ncs = OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile(source, "t", defs, _ => null, out var error);
        Assert.True(ncs != null, error);
        var vm = new ScriptVM(defs, new NoEngine()) { JitThreshold = jit, BackgroundCompile = false };
        return vm.Run(new NcsProgram("t", ncs!), 0);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void CompilerHandlesTheLanguage(int jit)
    {
        Assert.Equal(29, CompileAndRun(@"
            int g = 2;
            int Sq(int n) { return n * n; }
            int StartingConditional()
            {
                int s = 0, i;
                for (i = 0; i < 5; i++) { if (i == 3) continue; s += Sq(i) + g; }
                return s;
            }", jit));
        Assert.Equal(1234, CompileAndRun(@"
            struct P { int a; vector v; };
            struct P Make(int a) { struct P p; p.a = a; p.v = [1.0, 2.0, 3.0]; return p; }
            int StartingConditional()
            {
                struct P p = Make(1);
                p.v.y += 1.0;
                int r = p.a * 1000;
                switch (PAIR) { case 1: r += 1; case 2: r += 200; case 3: r += 30; break; default: r = 0; }
                int x = 0;
                if (x == 0 && p.v.y == 3.0 || FALSE) r += 4;
                do { x++; } while (x < 10);
                return r + (x == 10 ? 0 : 99);
            }", jit));
    }

    [Theory]
    [InlineData("void main() { int x = 1 }", "t(1): ERROR: NO SEMICOLON AFTER EXPRESSION")]
    [InlineData("void main() { Nope(); }", "t(1): ERROR: UNDEFINED IDENTIFIER (Nope)")]
    [InlineData("void main() { int x = \"a\"; }", "t(1): ERROR: MISMATCHED TYPES")]
    [InlineData("int x;", "t: ERROR: NO FUNCTION MAIN() IN SCRIPT")]
    public void CompilerErrorsReadLikeTheOriginal(string source, string expected)
    {
        Assert.Null(OpenWinter.Game.Scripting.Compiler.NssCompiler.Compile(source, "t", NssDefinitions.Parse(""), _ => null, out var error));
        Assert.Equal(expected, error);
    }
}
