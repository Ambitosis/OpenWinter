using System.Buffers.Binary;
using System.Globalization;

namespace OpenWinter.Game.Scripting.Compiler;

/// <summary>A value type during compilation: size in bytes, plus the engine-structure index or struct layout.</summary>
public sealed class CType
{
    public readonly string Name;
    public readonly int Size;
    public readonly int Engine = -1;
    public readonly StructLayout? Struct;

    CType(string name, int size, int engine = -1, StructLayout? s = null) { Name = name; Size = size; Engine = engine; Struct = s; }

    public static readonly CType Void = new("void", 0), Int = new("int", 4), Float = new("float", 4), String = new("string", 4),
        Object = new("object", 4), Vector = new("vector", 12, s: StructLayout.VectorLayout), Action = new("action", 0);
    public static CType EngineType(string name, int index) => new(name, 4, index);
    public static CType StructType(StructLayout s) => new("struct " + s.Name, s.Size, s: s);

    public bool IsStruct => Struct != null && this != Vector;
    public bool Same(CType o) => Name == o.Name;
    public override string ToString() => Name;
}

public sealed class StructLayout
{
    public required string Name;
    public readonly List<(string Name, CType Type, int Offset)> Fields = new();
    public int Size => Fields.Count == 0 ? 0 : Fields[^1].Offset + Fields[^1].Type.Size;

    public static readonly StructLayout VectorLayout = MakeVector();
    static StructLayout MakeVector()
    {
        var v = new StructLayout { Name = "vector" };
        v.Fields.Add(("x", CType.Float, 0));
        v.Fields.Add(("y", CType.Float, 4));
        v.Fields.Add(("z", CType.Float, 8));
        return v;
    }
}

/// <summary>
/// Turns a parsed script into NCS bytecode with the calling conventions of the original compiler: the
/// caller reserves the return value and pushes arguments last-first, the callee pops its arguments,
/// globals live under BP (set up by a "#globals" routine), and action arguments become STORE_STATE blocks.
/// </summary>
public sealed class NssCodeGen
{
    sealed class Label { public int Pos = -1; public readonly List<int> Uses = new(); }

    sealed class Var { public required CType Type; public required int Pos; public bool Global; }

    sealed class Function
    {
        public required string Name;
        public required CType Returns;
        public required List<(CType Type, string Name, Expr? Default)> Params;
        public FunctionDecl? Body;
        public readonly Label Entry = new();
        public readonly Label Exit = new();
        public int ParamsSize => Params.Sum(p => p.Type.Size);
    }

    readonly NssDefinitions _defs;
    readonly List<byte> _code = new();
    readonly Dictionary<string, StructLayout> _structs = new(StringComparer.Ordinal);
    readonly Dictionary<string, Function> _functions = new(StringComparer.Ordinal);
    readonly Dictionary<string, Var> _globals = new(StringComparer.Ordinal);
    readonly Dictionary<string, Expr?> _engineDefaults = new();
    readonly Queue<Function> _pending = new();
    readonly HashSet<Function> _queued = new();
    /// <summary>Initialisers of const globals, which count as constants (in case labels, for one).</summary>
    readonly Dictionary<string, Expr> _constGlobals = new(StringComparer.Ordinal);
    int _globalsSize;
    /// <summary>While the #globals routine initialises globals, BP is not set yet: globals are read from the stack.</summary>
    bool _inGlobals;

    // Per-function state.
    Function? _fn;
    int _sp; // bytes on the stack above the function's frame base (arguments sit below 0)
    readonly List<Dictionary<string, Var>> _scopes = new();
    readonly List<(Label Break, Label? Continue, int Sp)> _loops = new();

    NssCodeGen(NssDefinitions defs) { _defs = defs; }

    /// <summary>Compiles a parsed program into a complete NCS file.</summary>
    public static byte[] Generate(NssDefinitions defs, List<Node> decls, string file, bool mainOnly = false)
    {
        var g = new NssCodeGen(defs);
        return g.Program(decls, file, mainOnly);
    }

    static NssCompileException Error(Node at, string message) => new(at.File, at.Line, message);

    // ---- types ----

    CType Resolve(TypeRef t, Node at)
    {
        if (t.IsStruct)
            return _structs.TryGetValue(t.StructName!, out var s) ? CType.StructType(s) : throw Error(at, "UNDEFINED STRUCTURE");
        return t.Name switch
        {
            "void" => CType.Void,
            "int" => CType.Int,
            "float" => CType.Float,
            "string" => CType.String,
            "object" => CType.Object,
            "vector" => CType.Vector,
            "action" => CType.Action,
            _ => _defs.EngineStructures.IndexOf(t.Name) is var i and >= 0 ? CType.EngineType(t.Name, i) : throw Error(at, "VARIABLE DEFINED WITHOUT TYPE"),
        };
    }

    CType FromNss(NssType t) => t switch
    {
        NssType.Void => CType.Void,
        NssType.Int => CType.Int,
        NssType.Float => CType.Float,
        NssType.String => CType.String,
        NssType.Object => CType.Object,
        NssType.Vector => CType.Vector,
        NssType.Action => CType.Action,
        _ => CType.EngineType(_defs.EngineStructures[(int)t - (int)NssType.Effect], (int)t - (int)NssType.Effect),
    };

    static int Cell(CType t) => t == CType.Int ? 3 : t == CType.Float ? 4 : t == CType.String ? 5 : t == CType.Object ? 6 : 0x10 + t.Engine;

    // ---- emitting ----

    int Here => _code.Count;
    void Op(byte op, byte type) { _code.Add(op); _code.Add(type); }
    void I32(int v) { Span<byte> b = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(b, v); _code.AddRange(b.ToArray()); }
    void I16(int v) { Span<byte> b = stackalloc byte[2]; BinaryPrimitives.WriteInt16BigEndian(b, (short)v); _code.AddRange(b.ToArray()); }

    void CpTopSp(int pos, int size) { Op(0x03, 1); I32(pos - _sp); I16(size); _sp += size; }
    void CpDownSp(int pos, int size) { Op(0x01, 1); I32(pos - _sp); I16(size); }
    void CpTopBp(int pos, int size) { Op(0x27, 1); I32(pos - _globalsSize); I16(size); _sp += size; }
    void CpDownBp(int pos, int size) { Op(0x26, 1); I32(pos - _globalsSize); I16(size); }
    void MovSp(int bytes) { if (bytes == 0) return; Op(0x1B, 0); I32(-bytes); _sp -= bytes; }
    /// <summary>Pops for a jump out of a block (break, continue, return): the compile-time depth stays.</summary>
    void PopFor(int bytes) { if (bytes != 0) { Op(0x1B, 0); I32(-bytes); } }
    void Jump(byte op, Label l) { int at = Here; Op(op, 0); I32(0); if (l.Pos >= 0) Patch(at, l.Pos); else l.Uses.Add(at); }
    void Mark(Label l) { l.Pos = Here; foreach (var u in l.Uses) Patch(u, l.Pos); l.Uses.Clear(); }
    void Patch(int at, int target) => BinaryPrimitives.WriteInt32BigEndian(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(_code)[(at + 2)..], target - at);

    void RsAdd(CType t)
    {
        if (t.Size == 0) return;
        if (t.Struct != null) { foreach (var f in t.Struct.Fields) RsAdd(f.Type); return; }
        Op(0x02, (byte)Cell(t));
        _sp += 4;
    }

    void ConstInt(int v) { Op(0x04, 3); I32(v); _sp += 4; }
    void ConstFloat(float v) { Op(0x04, 4); I32(BitConverter.SingleToInt32Bits(v)); _sp += 4; }
    void ConstString(string s)
    {
        var bytes = Aurora.Text.Windows1252.GetBytes(s);
        Op(0x04, 5);
        I16(bytes.Length);
        _code.AddRange(bytes);
        _sp += 4;
    }
    void ConstObject(int v) { Op(0x04, 6); I32(v); _sp += 4; }

    // ---- program ----

    byte[] Program(List<Node> decls, string file, bool mainOnly)
    {
        foreach (var d in decls.OfType<StructDecl>())
        {
            var s = new StructLayout { Name = d.Name };
            int off = 0;
            foreach (var (ft, fn) in d.Fields)
            {
                var t = Resolve(ft, d);
                if (s.Fields.Any(f => f.Name == fn)) throw Error(d, "VARIABLE ALREADY USED WITHIN SCOPE");
                s.Fields.Add((fn, t, off));
                off += t.Size;
            }
            if (!_structs.TryAdd(d.Name, s)) throw Error(d, "STRUCTURE REDEFINED");
        }
        foreach (var d in decls.OfType<FunctionDecl>())
        {
            var ps = d.Params.Select(p => (Resolve(p.Type, d), p.Name, p.Default)).ToList();
            var ret = Resolve(d.Returns, d);
            if (_defs.ByName.ContainsKey(d.Name)) throw Error(d, $"FUNCTION IMPLEMENTATION AND DEFINITION DIFFER ({d.Name})");
            if (_functions.TryGetValue(d.Name, out var f))
            {
                if (!f.Returns.Same(ret) || f.Params.Count != ps.Count || f.Params.Zip(ps).Any(z => !z.First.Type.Same(z.Second.Item1)))
                    throw Error(d, "FUNCTION IMPLEMENTATION AND DEFINITION DIFFER");
                if (d.Body != null)
                {
                    if (f.Body?.Body != null) throw Error(d, $"DUPLICATE FUNCTION IMPLEMENTATION ({d.Name})");
                    f.Body = d;
                    // The implementation's parameter names count; defaults may be given on either.
                    for (int i = 0; i < ps.Count; i++) f.Params[i] = (f.Params[i].Type, ps[i].Name, ps[i].Default ?? f.Params[i].Default);
                }
                continue;
            }
            _functions[d.Name] = new Function { Name = d.Name, Returns = ret, Params = ps, Body = d.Body != null ? d : null };
        }
        var main = _functions.GetValueOrDefault("main") is { Body: not null } m && m.Returns == CType.Void && m.Params.Count == 0 ? m
            : !mainOnly && _functions.GetValueOrDefault("StartingConditional") is { Body: not null } sc && sc.Returns == CType.Int && sc.Params.Count == 0 ? sc
            : null;
        if (main == null) throw new NssCompileException(file, 0, "NO FUNCTION MAIN() IN SCRIPT");
        bool conditional = main.Name == "StartingConditional";

        var globals = decls.OfType<GlobalDecl>().ToList();
        foreach (var g in globals)
        {
            var t = Resolve(g.Decl.Type, g);
            foreach (var v in g.Decl.Vars)
            {
                if (_globals.ContainsKey(v.Name)) throw Error(g, "VARIABLE ALREADY USED WITHIN SCOPE");
                _globals[v.Name] = new Var { Type = t, Pos = _globalsSize, Global = true };
                if (g.Decl.Const && v.Init != null) _constGlobals[v.Name] = v.Init;
                _globalsSize += t.Size;
            }
        }

        // Entry stub.
        _fn = new Function { Name = "#entry", Returns = CType.Void, Params = new() };
        if (conditional) Op(0x02, 3);
        var globalsRoutine = new Label();
        Jump(0x1E, globals.Count > 0 ? globalsRoutine : main.Entry);
        Op(0x20, 0);
        if (globals.Count > 0)
        {
            Mark(globalsRoutine);
            _fn = new Function { Name = "#globals", Returns = CType.Void, Params = new() };
            _sp = 0;
            _scopes.Clear();
            _inGlobals = true;
            foreach (var g in globals)
                foreach (var v in g.Decl.Vars)
                {
                    var gv = _globals[v.Name];
                    RsAdd(gv.Type);
                    if (v.Init != null)
                    {
                        Convert(Expression(v.Init), gv.Type, v.Init);
                        CpDownSp(gv.Pos, gv.Type.Size);
                        MovSp(gv.Type.Size);
                    }
                }
            _inGlobals = false;
            Op(0x2A, 0); // SAVEBP
            if (conditional) Op(0x02, 3);
            Jump(0x1E, main.Entry);
            if (conditional)
            {
                // Copy the result down to the slot the entry stub reserved, under the globals and the saved BP.
                Op(0x01, 1); I32(-(4 + _globalsSize + 4 + 4)); I16(4);
                Op(0x1B, 0); I32(-4);
            }
            Op(0x2B, 0); // RESTOREBP
            Op(0x1B, 0); I32(-_globalsSize);
            Op(0x20, 0);
        }

        // Only functions reachable from main are generated (a prototype without a body is fine while unused).
        _pending.Enqueue(main);
        _queued.Add(main);
        while (_pending.TryDequeue(out var next)) FunctionBody(next);

        var file2 = new List<byte>(System.Text.Encoding.ASCII.GetBytes("NCS V1.0")) { 0x42 };
        Span<byte> size = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(size, 13 + _code.Count);
        file2.AddRange(size.ToArray());
        file2.AddRange(_code);
        return file2.ToArray();
    }

    void FunctionBody(Function f)
    {
        _fn = f;
        _sp = 0;
        _scopes.Clear();
        _loops.Clear();
        Mark(f.Entry);
        var scope = new Dictionary<string, Var>(StringComparer.Ordinal);
        int pos = 0;
        foreach (var p in f.Params)
        {
            pos -= p.Type.Size;
            if (!scope.TryAdd(p.Name, new Var { Type = p.Type, Pos = pos })) throw Error(f.Body!, "VARIABLE ALREADY USED WITHIN SCOPE");
        }
        _scopes.Add(scope);
        var body = f.Body!.Body!;
        Block(body.Body, newScope: true);
        if (f.Returns != CType.Void && !AlwaysReturns(body)) throw Error(body, "NOT ALL CONTROL PATHS RETURN A VALUE");
        // Returns jump here with only the arguments left to pop, as the original compiler lays functions out.
        Mark(f.Exit);
        PopFor(f.ParamsSize);
        Op(0x20, 0);
    }

    static bool AlwaysReturns(Stmt s) => s switch
    {
        ReturnStmt => true,
        BlockStmt b => b.Body.Any(AlwaysReturns),
        IfStmt i => i.Else != null && AlwaysReturns(i.Then) && AlwaysReturns(i.Else),
        _ => false,
    };

    // ---- statements ----

    void Block(List<Stmt> body, bool newScope)
    {
        int start = _sp;
        if (newScope) _scopes.Add(new Dictionary<string, Var>(StringComparer.Ordinal));
        foreach (var s in body) Statement(s);
        if (newScope) _scopes.RemoveAt(_scopes.Count - 1);
        MovSp(_sp - start);
    }

    void Statement(Stmt s)
    {
        switch (s)
        {
            case BlockStmt b: Block(b.Body, newScope: true); break;
            case EmptyStmt: break;
            case VarDeclStmt d:
            {
                var t = Resolve(d.Type, d);
                if (t == CType.Void || t == CType.Action) throw Error(d, "VARIABLE DEFINED WITHOUT TYPE");
                foreach (var (name, init, line) in d.Vars)
                {
                    var scope = _scopes[^1];
                    if (scope.ContainsKey(name)) throw new NssCompileException(d.File, line, "VARIABLE ALREADY USED WITHIN SCOPE");
                    int at = _sp;
                    RsAdd(t);
                    scope[name] = new Var { Type = t, Pos = at }; // visible in its own initialiser, as in the original
                    if (init != null)
                    {
                        Convert(Expression(init), t, init);
                        CpDownSp(at, t.Size);
                        MovSp(t.Size);
                    }
                }
                break;
            }
            case ExprStmt e: MovSp(Expression(e.Expr).Size); break;
            case IfStmt i:
            {
                Condition(i.Cond);
                var otherwise = new Label();
                Jump(0x1F, otherwise);
                _sp -= 4;
                Statement(i.Then);
                if (i.Else != null)
                {
                    var end = new Label();
                    Jump(0x1D, end);
                    Mark(otherwise);
                    Statement(i.Else);
                    Mark(end);
                }
                else Mark(otherwise);
                break;
            }
            case WhileStmt w:
            {
                var top = new Label();
                var end = new Label();
                Mark(top);
                Condition(w.Cond);
                Jump(0x1F, end);
                _sp -= 4;
                _loops.Add((end, top, _sp));
                Statement(w.Body);
                _loops.RemoveAt(_loops.Count - 1);
                Jump(0x1D, top);
                Mark(end);
                break;
            }
            case DoStmt d:
            {
                var top = new Label();
                var cont = new Label();
                var end = new Label();
                Mark(top);
                _loops.Add((end, cont, _sp));
                Statement(d.Body);
                _loops.RemoveAt(_loops.Count - 1);
                Mark(cont);
                Condition(d.Cond);
                Jump(0x1F, end);
                _sp -= 4;
                Jump(0x1D, top);
                Mark(end);
                break;
            }
            case ForStmt f:
            {
                if (f.Init != null) MovSp(Expression(f.Init).Size);
                var top = new Label();
                var cont = new Label();
                var end = new Label();
                Mark(top);
                if (f.Cond != null)
                {
                    Condition(f.Cond);
                    Jump(0x1F, end);
                    _sp -= 4;
                }
                _loops.Add((end, cont, _sp));
                Statement(f.Body);
                _loops.RemoveAt(_loops.Count - 1);
                Mark(cont);
                if (f.Step != null) MovSp(Expression(f.Step).Size);
                Jump(0x1D, top);
                Mark(end);
                break;
            }
            case SwitchStmt sw: Switch(sw); break;
            case CaseStmt c: throw Error(c, "CASE STATEMENT OUTSIDE OF SWITCH");
            case BreakStmt b:
            {
                if (_loops.Count == 0) throw Error(b, "BREAK OUTSIDE OF LOOP OR CASE STATEMENT");
                var l = _loops[^1];
                PopFor(_sp - l.Sp);
                Jump(0x1D, l.Break);
                break;
            }
            case ContinueStmt c:
            {
                int i = _loops.FindLastIndex(l => l.Continue != null);
                if (i < 0) throw Error(c, "CONTINUE OUTSIDE OF LOOP STATEMENT");
                PopFor(_sp - _loops[i].Sp);
                Jump(0x1D, _loops[i].Continue!);
                break;
            }
            case ReturnStmt r:
            {
                var want = _fn!.Returns;
                if (r.Value == null)
                {
                    if (want != CType.Void) throw Error(r, "RETURN TYPE AND FUNCTION TYPE MISMATCHED");
                    PopFor(_sp);
                }
                else
                {
                    if (want == CType.Void) throw Error(r, "RETURN TYPE AND FUNCTION TYPE MISMATCHED");
                    Convert(Expression(r.Value), want, r.Value, "RETURN TYPE AND FUNCTION TYPE MISMATCHED");
                    CpDownSp(-(_fn.ParamsSize + want.Size), want.Size);
                    PopFor(_sp);
                    _sp -= want.Size;
                }
                Jump(0x1D, _fn.Exit);
                break;
            }
            default: throw Error(s, "UNKNOWN STATE IN COMPILER");
        }
    }

    void Switch(SwitchStmt sw)
    {
        if (Expression(sw.Value) != CType.Int) throw Error(sw, "SWITCH MUST EVALUATE TO AN INTEGER");
        var cases = new List<(CaseStmt Stmt, Label Label, int? Value)>();
        foreach (var c in sw.Body.OfType<CaseStmt>())
        {
            int? v = c.Value == null ? null : ConstantInt(c.Value) ?? throw Error(c, "CASE PARAMETER NOT A CONSTANT INTEGER");
            if (v == null && cases.Any(x => x.Value == null)) throw Error(c, "MULTIPLE DEFAULT STATEMENTS WITHIN SWITCH");
            if (v != null && cases.Any(x => x.Value == v)) throw Error(c, "MULTIPLE CASE CONSTANT STATEMENTS WITHIN SWITCH");
            cases.Add((c, new Label(), v));
        }
        var end = new Label();
        foreach (var (_, label, value) in cases.Where(c => c.Value != null))
        {
            CpTopSp(_sp - 4, 4);
            ConstInt(value!.Value);
            Op(0x0B, 0x20);
            _sp -= 4;
            Jump(0x25, label);
            _sp -= 4;
        }
        var def = cases.FirstOrDefault(c => c.Value == null);
        Jump(0x1D, def.Label ?? end);
        _loops.Add((end, null, _sp));
        _scopes.Add(new Dictionary<string, Var>(StringComparer.Ordinal));
        int start = _sp;
        foreach (var s in sw.Body)
        {
            if (s is CaseStmt c)
            {
                if (_sp != start) throw Error(c, "JUMPING OVER DECLARATION STATEMENTS CASE DISALLOWED");
                Mark(cases.First(x => x.Stmt == c).Label);
                continue;
            }
            Statement(s);
        }
        MovSp(_sp - start);
        _scopes.RemoveAt(_scopes.Count - 1);
        _loops.RemoveAt(_loops.Count - 1);
        Mark(end);
        MovSp(4);
    }

    void Condition(Expr e)
    {
        if (Expression(e) != CType.Int) throw Error(e, "NON INTEGER EXPRESSION WHERE INTEGER REQUIRED");
    }

    /// <summary>The value of an integer constant expression, or null.</summary>
    int? ConstantInt(Expr e) => e switch
    {
        IntLit i => i.Value,
        UnaryExpr { Op: "-" } u => ConstantInt(u.Operand) is int v ? -v : null,
        NameExpr n when Lookup(n.Name) is { Global: true } && _constGlobals.TryGetValue(n.Name, out var init) => ConstantInt(init),
        NameExpr n when Lookup(n.Name) == null && Constant(n.Name) is int v => v,
        BinaryExpr b when ConstantInt(b.Left) is int l && ConstantInt(b.Right) is int r => b.Op switch
        {
            "+" => l + r,
            "-" => l - r,
            "*" => l * r,
            "|" => l | r,
            "&" => l & r,
            "^" => l ^ r,
            "<<" => l << r,
            "/" when r != 0 => l / r,
            "%" when r != 0 => l % r,
            _ => null,
        },
        _ => null,
    };

    readonly Dictionary<string, object?> _constants = new(StringComparer.Ordinal);

    /// <summary>The value (int, float or string) of a constant from nwscript.nss, or null.</summary>
    object? Constant(string name)
    {
        if (_constants.TryGetValue(name, out var v)) return v;
        _constants[name] = null; // guards against cycles
        if (!_defs.Constants.TryGetValue(name, out var c)) return null;
        v = Evaluate(NssParser.ParseExpression(c.Value, _defs.EngineStructures));
        if (c.Type == NssType.Float && v is int i) v = (float)i;
        _constants[name] = v;
        return v;
    }

    object? Evaluate(Expr e) => e switch
    {
        IntLit i => i.Value,
        FloatLit f => f.Value,
        StringLit s => s.Value,
        NameExpr n => Constant(n.Name),
        UnaryExpr { Op: "-" } u => Evaluate(u.Operand) switch { int i => -i, float f => -f, _ => null },
        BinaryExpr { Left: var l, Right: var r } bin when Evaluate(l) is int x && Evaluate(r) is int y => bin.Op switch
        {
            "+" => x + y,
            "-" => x - y,
            "*" => x * y,
            "|" => x | y,
            "&" => x & y,
            "^" => x ^ y,
            "<<" => x << y,
            "/" when y != 0 => x / y,
            "%" when y != 0 => x % y,
            _ => null,
        },
        _ => null,
    };

    static int ParseIntConstant(string v)
    {
        v = v.Trim();
        bool neg = v.StartsWith('-');
        if (neg) v = v[1..].Trim();
        int r = v.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? (int)uint.Parse(v[2..], NumberStyles.HexNumber) : int.Parse(v, CultureInfo.InvariantCulture);
        return neg ? -r : r;
    }

    // ---- expressions ----

    Var? Lookup(string name)
    {
        for (int i = _scopes.Count - 1; i >= 0; i--)
            if (_scopes[i].TryGetValue(name, out var v)) return v;
        return _globals.GetValueOrDefault(name);
    }

    void Convert(CType got, CType want, Node at, string message = "MISMATCHED TYPES")
    {
        if (!got.Same(want)) throw Error(at, message);
    }

    /// <summary>Compiles an expression, leaving its value on the stack; returns its type.</summary>
    CType Expression(Expr e)
    {
        switch (e)
        {
            case IntLit i: ConstInt(i.Value); return CType.Int;
            case FloatLit f: ConstFloat(f.Value); return CType.Float;
            case StringLit s: ConstString(s.Value); return CType.String;
            case ObjectLit o: ConstObject(o.Self ? 0 : 1); return CType.Object;
            case VectorLit v:
                foreach (var c in new[] { v.X, v.Y, v.Z })
                {
                    if (c is IntLit il) ConstFloat(il.Value);
                    else if (Expression(c) != CType.Float) throw Error(c, "MISMATCHED TYPES");
                }
                return CType.Vector;
            case NameExpr n: return Name(n);
            case CallExpr c: return Call(c);
            case MemberExpr m: return Member(m);
            case UnaryExpr u: return Unary(u);
            case IncDecExpr d: return IncDec(d);
            case BinaryExpr b: return Binary(b);
            case AssignExpr a: return Assign(a);
            case CondExpr c:
            {
                Condition(c.Cond);
                var otherwise = new Label();
                var end = new Label();
                Jump(0x1F, otherwise);
                _sp -= 4;
                int before = _sp;
                var t = Expression(c.Then);
                Jump(0x1D, end);
                _sp = before;
                Mark(otherwise);
                Convert(Expression(c.Else), t, c.Else);
                Mark(end);
                return t;
            }
            default: throw Error(e, "UNKNOWN STATE IN COMPILER");
        }
    }

    CType Name(NameExpr n)
    {
        if (Lookup(n.Name) is { } v)
        {
            Load((v, 0, v.Type));
            return v.Type;
        }
        switch (n.Name)
        {
            case "LOCATION_INVALID":
                Op(0x04, (byte)(0x10 + _defs.EngineStructures.IndexOf("location"))); I32(0); _sp += 4;
                return CType.EngineType("location", _defs.EngineStructures.IndexOf("location"));
            case "JSON_NULL":
            case "JSON_OBJECT":
            case "JSON_ARRAY":
            case "JSON_TRUE":
            case "JSON_FALSE":
            case "JSON_STRING":
            {
                int ji = _defs.EngineStructures.IndexOf("json");
                string text = n.Name switch { "JSON_NULL" => "null", "JSON_OBJECT" => "{}", "JSON_ARRAY" => "[]", "JSON_TRUE" => "true", "JSON_FALSE" => "false", _ => "\"\"" };
                var bytes = System.Text.Encoding.ASCII.GetBytes(text);
                Op(0x04, (byte)(0x10 + ji)); I16(bytes.Length); _code.AddRange(bytes); _sp += 4;
                return CType.EngineType("json", ji);
            }
        }
        switch (Constant(n.Name))
        {
            case int i: ConstInt(i); return CType.Int;
            case float f: ConstFloat(f); return CType.Float;
            case string str: ConstString(str); return CType.String;
        }
        throw Error(n, $"UNDEFINED IDENTIFIER ({n.Name})");
    }

    /// <summary>The variable and byte offset an lvalue expression (a variable or its fields) names, or null.</summary>
    (Var Var, int Offset, CType Type)? LValue(Expr e)
    {
        switch (e)
        {
            case NameExpr n when Lookup(n.Name) is { } v: return (v, 0, v.Type);
            case MemberExpr m when LValue(m.Target) is { } t:
            {
                var layout = t.Type.Struct ?? throw Error(m, "LEFT OF STRUCTURE PART NOT STRUCTURE");
                var f = layout.Fields.FirstOrDefault(f => f.Name == m.Field);
                if (f.Name == null) throw Error(m, "UNDEFINED FIELD IN STRUCTURE");
                return (t.Var, t.Offset + f.Offset, f.Type);
            }
            default: return null;
        }
    }

    void Load((Var Var, int Offset, CType Type) lv)
    {
        if (lv.Var.Global && !_inGlobals) CpTopBp(lv.Var.Pos + lv.Offset, lv.Type.Size);
        else CpTopSp(lv.Var.Pos + lv.Offset, lv.Type.Size);
    }

    void Store((Var Var, int Offset, CType Type) lv)
    {
        if (lv.Var.Global && !_inGlobals) CpDownBp(lv.Var.Pos + lv.Offset, lv.Type.Size);
        else CpDownSp(lv.Var.Pos + lv.Offset, lv.Type.Size);
    }

    CType Member(MemberExpr m)
    {
        if (LValue(m) is { } lv) { Load(lv); return lv.Type; }
        var t = Expression(m.Target);
        var layout = t.Struct ?? throw Error(m, "LEFT OF STRUCTURE PART NOT STRUCTURE");
        var f = layout.Fields.FirstOrDefault(f => f.Name == m.Field);
        if (f.Name == null) throw Error(m, "UNDEFINED FIELD IN STRUCTURE");
        Op(0x21, 1); I16(t.Size); I16(f.Offset); I16(f.Type.Size);
        _sp += f.Type.Size - t.Size;
        return f.Type;
    }

    CType Unary(UnaryExpr u)
    {
        var t = Expression(u.Operand);
        switch (u.Op)
        {
            case "-" when t == CType.Int: Op(0x19, 3); return t;
            case "-" when t == CType.Float: Op(0x19, 4); return t;
            case "!" when t == CType.Int: Op(0x22, 3); return t;
            case "~" when t == CType.Int: Op(0x1A, 3); return t;
            default: throw Error(u, "MISMATCHED TYPES");
        }
    }

    CType IncDec(IncDecExpr d)
    {
        var lv = LValue(d.Target) ?? throw Error(d, "OPERAND MUST BE AN INTEGER LVALUE");
        if (lv.Type != CType.Int) throw Error(d, "OPERAND MUST BE AN INTEGER LVALUE");
        bool bp = lv.Var.Global && !_inGlobals;
        byte op = (byte)(d.Op == "++" ? (bp ? 0x29 : 0x24) : (bp ? 0x28 : 0x23));
        if (!d.Prefix) Load(lv);
        Op(op, 3);
        I32(bp ? lv.Var.Pos + lv.Offset - _globalsSize : lv.Var.Pos + lv.Offset - _sp);
        if (d.Prefix) Load(lv);
        return CType.Int;
    }

    static readonly Dictionary<string, byte> ArithOps = new() { ["+"] = 0x14, ["-"] = 0x15, ["*"] = 0x16, ["/"] = 0x17, ["%"] = 0x18 };
    static readonly Dictionary<string, byte> IntOps = new() { ["|"] = 0x08, ["^"] = 0x09, ["&"] = 0x0A, ["<<"] = 0x11, [">>"] = 0x12, [">>>"] = 0x13 };
    static readonly Dictionary<string, byte> CompareOps = new() { [">="] = 0x0D, [">"] = 0x0E, ["<"] = 0x0F, ["<="] = 0x10 };

    CType Binary(BinaryExpr b)
    {
        if (b.Op is "&&" or "||")
        {
            Condition(b.Left);
            CpTopSp(_sp - 4, 4);
            var end = new Label();
            if (b.Op == "&&")
            {
                Jump(0x1F, end);
                _sp -= 4;
                Condition(b.Right);
                Op(0x06, 0x20);
                _sp -= 4;
            }
            else
            {
                var right = new Label();
                Jump(0x1F, right);
                _sp -= 4;
                CpTopSp(_sp - 4, 4);
                var join = new Label();
                Jump(0x1D, join);
                _sp -= 4;
                Mark(right);
                Condition(b.Right);
                Mark(join);
                Op(0x07, 0x20);
                _sp -= 4;
            }
            Mark(end);
            return CType.Int;
        }
        var l = Expression(b.Left);
        var r = Expression(b.Right);
        return ApplyBinary(b.Op, l, r, b);
    }

    /// <summary>Emits the operator for two operands already on the stack; returns the result type.</summary>
    CType ApplyBinary(string op, CType l, CType r, Node at)
    {
        int size = l.Size + r.Size;
        CType result;
        if (ArithOps.TryGetValue(op, out var code))
        {
            (byte type, result) = (l.Name, r.Name, op) switch
            {
                ("int", "int", _) => ((byte)0x20, CType.Int),
                ("float", "float", not "%") => ((byte)0x21, CType.Float),
                ("int", "float", not "%") => ((byte)0x25, CType.Float),
                ("float", "int", not "%") => ((byte)0x26, CType.Float),
                ("string", "string", "+") => ((byte)0x23, CType.String),
                ("vector", "vector", "+" or "-") => ((byte)0x3A, CType.Vector),
                ("vector", "float", "*" or "/") => ((byte)0x3B, CType.Vector),
                ("float", "vector", "*") => ((byte)0x3C, CType.Vector),
                _ => throw Error(at, "MISMATCHED TYPES"),
            };
            Op(code, type);
        }
        else if (IntOps.TryGetValue(op, out code))
        {
            if (l != CType.Int || r != CType.Int) throw Error(at, "MISMATCHED TYPES");
            Op(code, 0x20);
            result = CType.Int;
        }
        else if (CompareOps.TryGetValue(op, out code))
        {
            byte type = l == CType.Int && r == CType.Int ? (byte)0x20 : l == CType.Float && r == CType.Float ? (byte)0x21 : throw Error(at, "MISMATCHED TYPES");
            Op(code, type);
            result = CType.Int;
        }
        else if (op is "==" or "!=")
        {
            if (!l.Same(r)) throw Error(at, "MISMATCHED TYPES");
            byte opc = (byte)(op == "==" ? 0x0B : 0x0C);
            if (l.Struct != null) { Op(opc, 0x24); I16(l.Size); }
            else Op(opc, (byte)(l == CType.Int ? 0x20 : l == CType.Float ? 0x21 : l == CType.Object ? 0x22 : l == CType.String ? 0x23 : 0x30 + l.Engine));
            result = CType.Int;
        }
        else throw Error(at, "UNKNOWN STATE IN COMPILER");
        _sp += result.Size - size;
        return result;
    }

    CType Assign(AssignExpr a)
    {
        var lv = LValue(a.Target) ?? throw Error(a, "OPERAND MUST BE AN LVALUE");
        if (a.Op == "=")
        {
            Convert(Expression(a.Value), lv.Type, a);
            Store(lv);
            return lv.Type;
        }
        Load(lv);
        var r = Expression(a.Value);
        var t = ApplyBinary(a.Op[..^1], lv.Type, r, a);
        Convert(t, lv.Type, a);
        Store(lv);
        return lv.Type;
    }

    // ---- calls ----

    CType Call(CallExpr c)
    {
        if (_functions.TryGetValue(c.Name, out var f))
        {
            if (c.Args.Count > f.Params.Count) throw Error(c, "DECLARATION DOES NOT MATCH PARAMETERS");
            int before = _sp;
            RsAdd(f.Returns);
            for (int i = f.Params.Count - 1; i >= 0; i--)
            {
                var arg = i < c.Args.Count ? c.Args[i] : f.Params[i].Default ?? throw Error(c, "DECLARATION DOES NOT MATCH PARAMETERS");
                Argument(arg, f.Params[i].Type, c);
            }
            if (f.Body?.Body == null) throw Error(c, $"FUNCTION DEFINITION MISSING ({f.Name})");
            if (_queued.Add(f)) _pending.Enqueue(f);
            Jump(0x1E, f.Entry);
            _sp = before + f.Returns.Size;
            return f.Returns;
        }
        if (_defs.ByName.TryGetValue(c.Name, out var nf))
        {
            if (c.Args.Count > nf.Params.Length) throw Error(c, "DECLARATION DOES NOT MATCH PARAMETERS");
            int before = _sp;
            for (int i = nf.Params.Length - 1; i >= 0; i--)
            {
                var arg = i < c.Args.Count ? c.Args[i] : EngineDefault(nf, i) ?? throw Error(c, "DECLARATION DOES NOT MATCH PARAMETERS");
                Argument(arg, FromNss(nf.Params[i].Type), c);
            }
            Op(0x05, 0); I16(nf.Index); _code.Add((byte)nf.Params.Length);
            var ret = FromNss(nf.Returns);
            _sp = before + ret.Size;
            return ret;
        }
        throw Error(c, $"UNDEFINED IDENTIFIER ({c.Name})");
    }

    Expr? EngineDefault(NssFunction f, int i)
    {
        var key = f.Name + "#" + i;
        if (_engineDefaults.TryGetValue(key, out var e)) return e;
        var text = f.Params[i].Default;
        if (text != null)
        {
            var p = NssParser.ParseExpression(text, _defs.EngineStructures);
            e = p;
        }
        _engineDefaults[key] = e;
        return e;
    }

    /// <summary>Pushes one argument; an action argument becomes a STORE_STATE block running the expression later.</summary>
    void Argument(Expr arg, CType want, Node at)
    {
        if (want == CType.Action)
        {
            int frame = _sp + _fn!.ParamsSize;
            int bp = _fn.Name.StartsWith('#') ? 0 : _globalsSize;
            Op(0x2C, 0x10); I32(bp); I32(frame);
            var after = new Label();
            Jump(0x1D, after);
            int saved = _sp;
            MovSp(Expression(arg).Size);
            Op(0x20, 0);
            _sp = saved;
            Mark(after);
            return;
        }
        if (want == CType.Float && arg is IntLit il) { ConstFloat(il.Value); return; }
        Convert(Expression(arg), want, arg);
    }
}
