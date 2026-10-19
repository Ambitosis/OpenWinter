namespace OpenWinter.Game.Scripting.Compiler;

// ---- syntax tree ----

/// <summary>A type as written: a builtin (int, float, ...), an engine structure, or "struct Name".</summary>
public sealed record TypeRef(string Name, string? StructName = null)
{
    public bool IsStruct => StructName != null;
    public override string ToString() => StructName != null ? "struct " + StructName : Name;
}

public abstract record Node(int Line, string File);

public abstract record Expr(int Line, string File) : Node(Line, File);
public sealed record IntLit(int Line, string File, int Value) : Expr(Line, File);
public sealed record FloatLit(int Line, string File, float Value) : Expr(Line, File);
public sealed record StringLit(int Line, string File, string Value) : Expr(Line, File);
public sealed record ObjectLit(int Line, string File, bool Self) : Expr(Line, File);
public sealed record VectorLit(int Line, string File, Expr X, Expr Y, Expr Z) : Expr(Line, File);
public sealed record NameExpr(int Line, string File, string Name) : Expr(Line, File);
public sealed record CallExpr(int Line, string File, string Name, List<Expr> Args) : Expr(Line, File);
public sealed record MemberExpr(int Line, string File, Expr Target, string Field) : Expr(Line, File);
public sealed record UnaryExpr(int Line, string File, string Op, Expr Operand) : Expr(Line, File);
/// <summary>++/-- before (Prefix) or after the operand.</summary>
public sealed record IncDecExpr(int Line, string File, string Op, Expr Target, bool Prefix) : Expr(Line, File);
public sealed record BinaryExpr(int Line, string File, string Op, Expr Left, Expr Right) : Expr(Line, File);
public sealed record AssignExpr(int Line, string File, string Op, Expr Target, Expr Value) : Expr(Line, File);
public sealed record CondExpr(int Line, string File, Expr Cond, Expr Then, Expr Else) : Expr(Line, File);

public abstract record Stmt(int Line, string File) : Node(Line, File);
public sealed record BlockStmt(int Line, string File, List<Stmt> Body) : Stmt(Line, File);
public sealed record VarDeclStmt(int Line, string File, TypeRef Type, List<(string Name, Expr? Init, int Line)> Vars, bool Const) : Stmt(Line, File);
public sealed record ExprStmt(int Line, string File, Expr Expr) : Stmt(Line, File);
public sealed record IfStmt(int Line, string File, Expr Cond, Stmt Then, Stmt? Else) : Stmt(Line, File);
public sealed record WhileStmt(int Line, string File, Expr Cond, Stmt Body) : Stmt(Line, File);
public sealed record DoStmt(int Line, string File, Stmt Body, Expr Cond) : Stmt(Line, File);
public sealed record ForStmt(int Line, string File, Expr? Init, Expr? Cond, Expr? Step, Stmt Body) : Stmt(Line, File);
public sealed record SwitchStmt(int Line, string File, Expr Value, List<Stmt> Body) : Stmt(Line, File);
public sealed record CaseStmt(int Line, string File, Expr? Value) : Stmt(Line, File);
public sealed record BreakStmt(int Line, string File) : Stmt(Line, File);
public sealed record ContinueStmt(int Line, string File) : Stmt(Line, File);
public sealed record ReturnStmt(int Line, string File, Expr? Value) : Stmt(Line, File);
public sealed record EmptyStmt(int Line, string File) : Stmt(Line, File);

public sealed record Param(TypeRef Type, string Name, Expr? Default);
public sealed record FunctionDecl(int Line, string File, TypeRef Returns, string Name, List<Param> Params, BlockStmt? Body) : Node(Line, File);
public sealed record StructDecl(int Line, string File, string Name, List<(TypeRef Type, string Name)> Fields) : Node(Line, File);
public sealed record GlobalDecl(int Line, string File, VarDeclStmt Decl) : Node(Line, File);

/// <summary>Recursive-descent parser for NWScript; includes are expanded through <paramref name="include"/>.</summary>
public sealed class NssParser
{
    readonly List<Token> _t;
    int _p;
    readonly HashSet<string> _engineTypes;
    readonly Func<string, string?> _include;
    readonly HashSet<string> _included;

    public readonly List<Node> Decls = new();

    NssParser(List<Token> tokens, HashSet<string> engineTypes, Func<string, string?> include, HashSet<string> included)
    {
        _t = tokens;
        _engineTypes = engineTypes;
        _include = include;
        _included = included;
    }

    public static List<Node> Parse(string source, string file, IEnumerable<string> engineTypes, Func<string, string?> include)
    {
        var types = new HashSet<string>(engineTypes, StringComparer.Ordinal);
        var included = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { file };
        var p = new NssParser(NssLexer.Tokenize(source, file, types), types, include, included);
        p.TopLevel();
        return p.Decls;
    }

    /// <summary>Parses a lone expression (a default parameter value from nwscript.nss).</summary>
    public static Expr ParseExpression(string text, IEnumerable<string> engineTypes)
    {
        var types = new HashSet<string>(engineTypes, StringComparer.Ordinal);
        var p = new NssParser(NssLexer.Tokenize(text, "nwscript", types), types, _ => null, new HashSet<string>());
        return p.Expression();
    }

    Token Cur => _t[_p];
    Token Peek(int n = 1) => _t[Math.Min(_p + n, _t.Count - 1)];
    bool Is(string text) => Cur.Kind is TokenKind.Operator or TokenKind.Keyword && Cur.Text == text;
    bool Accept(string text)
    {
        if (!Is(text)) return false;
        _p++;
        return true;
    }
    NssCompileException Error(string message, Token? at = null) => new((at ?? Cur).File, (at ?? Cur).Line, message);
    void Expect(string text, string message)
    {
        if (!Accept(text)) throw Error(message);
    }

    bool IsTypeStart() =>
        Cur.Kind == TokenKind.Keyword && (Cur.Text is "int" or "float" or "string" or "object" or "vector" or "void" or "struct" or "action" || _engineTypes.Contains(Cur.Text));

    TypeRef ParseType()
    {
        var t = Cur;
        if (Accept("struct"))
        {
            if (Cur.Kind != TokenKind.Identifier) throw Error("UNDEFINED STRUCTURE");
            return new TypeRef("struct", _t[_p++].Text);
        }
        if (!IsTypeStart()) throw Error("VARIABLE DEFINED WITHOUT TYPE");
        _p++;
        return new TypeRef(t.Text);
    }

    void TopLevel()
    {
        while (Cur.Kind != TokenKind.End)
        {
            if (Cur.Kind == TokenKind.Include)
            {
                var name = Cur.Text;
                var at = Cur;
                _p++;
                if (name.Equals("nwscript", StringComparison.OrdinalIgnoreCase) || !_included.Add(name)) continue;
                var src = _include(name) ?? throw new NssCompileException(at.File, at.Line, "FILE NOT FOUND");
                var inner = new NssParser(NssLexer.Tokenize(src, name, _engineTypes), _engineTypes, _include, _included);
                inner.TopLevel();
                Decls.AddRange(inner.Decls);
                continue;
            }
            if (Cur.Kind == TokenKind.Define) { _p++; continue; }
            if (Accept(";")) continue;
            var start = Cur;
            bool isConst = Accept("const");
            if (Is("struct") && Peek().Kind == TokenKind.Identifier && Peek(2).Text == "{")
            {
                var structName = Peek();
                _p += 2;
                Decls.Add(StructBody(start, structName));
                continue;
            }
            var type = ParseType();
            if (Cur.Kind != TokenKind.Identifier) throw Error("FUNCTION DEFINITION MISSING NAME");
            var name1 = Cur;
            if (Peek().Text == "(" && Peek().Kind == TokenKind.Operator)
            {
                _p += 2;
                var ps = Params();
                if (Accept(";")) { Decls.Add(new FunctionDecl(start.Line, start.File, type, name1.Text, ps, null)); continue; }
                if (!Is("{")) throw Error("NO SEMICOLON AFTER EXPRESSION");
                Decls.Add(new FunctionDecl(start.Line, start.File, type, name1.Text, ps, Block()));
                continue;
            }
            Decls.Add(new GlobalDecl(start.Line, start.File, VarList(start, type, isConst)));
        }
    }

    StructDecl StructBody(Token start, Token name)
    {
        Expect("{", "NO LEFT BRACE ON STRUCTURE");
        var fields = new List<(TypeRef, string)>();
        while (!Accept("}"))
        {
            var ft = ParseType();
            do
            {
                if (Cur.Kind != TokenKind.Identifier) throw Error("BAD VARIABLE NAME");
                fields.Add((ft, _t[_p++].Text));
            } while (Accept(","));
            Expect(";", "NO SEMICOLON AFTER EXPRESSION");
        }
        Expect(";", "NO SEMICOLON AFTER STRUCTURE");
        return new StructDecl(start.Line, start.File, name.Text, fields);
    }

    List<Param> Params()
    {
        var ps = new List<Param>();
        if (Accept(")")) return ps;
        do
        {
            if (Is("void") && Peek().Text == ")") { _p++; break; }
            var pt = ParseType();
            if (Cur.Kind != TokenKind.Identifier) throw Error("MALFORMED PARAMETER LIST");
            var pn = _t[_p++].Text;
            Expr? def = Accept("=") ? Expression() : null;
            ps.Add(new Param(pt, pn, def));
        } while (Accept(","));
        Expect(")", "MALFORMED PARAMETER LIST");
        return ps;
    }

    VarDeclStmt VarList(Token start, TypeRef type, bool isConst)
    {
        var vars = new List<(string, Expr?, int)>();
        do
        {
            if (Cur.Kind != TokenKind.Identifier) throw Error("BAD VARIABLE NAME");
            var n = _t[_p++];
            Expr? init = Accept("=") ? Expression() : null;
            vars.Add((n.Text, init, n.Line));
        } while (Accept(","));
        Expect(";", "NO SEMICOLON AFTER EXPRESSION");
        return new VarDeclStmt(start.Line, start.File, type, vars, isConst);
    }

    // ---- statements ----

    BlockStmt Block()
    {
        var start = Cur;
        Expect("{", "NO LEFT BRACE ON STATEMENT");
        var body = new List<Stmt>();
        while (!Is("}"))
        {
            if (Cur.Kind == TokenKind.End) throw Error("UNEXPECTED END COMPOUND STATEMENT");
            body.Add(Statement());
        }
        _p++;
        return new BlockStmt(start.Line, start.File, body);
    }

    Stmt Statement()
    {
        var t = Cur;
        if (Is("{")) return Block();
        if (Accept(";")) return new EmptyStmt(t.Line, t.File);
        if (Is("const") || IsTypeStart())
        {
            bool isConst = Accept("const");
            var type = ParseType();
            return VarList(t, type, isConst);
        }
        if (Accept("if"))
        {
            Expect("(", "NO LEFT BRACKET ON EXPRESSION");
            var c = Expression();
            Expect(")", "NO RIGHT BRACKET ON EXPRESSION");
            var then = Statement();
            Stmt? els = Accept("else") ? Statement() : null;
            return new IfStmt(t.Line, t.File, c, then, els);
        }
        if (Accept("while"))
        {
            Expect("(", "NO LEFT BRACKET ON EXPRESSION");
            var c = Expression();
            Expect(")", "NO RIGHT BRACKET ON EXPRESSION");
            return new WhileStmt(t.Line, t.File, c, Statement());
        }
        if (Accept("do"))
        {
            var body = Statement();
            Expect("while", "NO WHILE AFTER DO KEYWORD");
            Expect("(", "NO LEFT BRACKET ON EXPRESSION");
            var c = Expression();
            Expect(")", "NO RIGHT BRACKET ON EXPRESSION");
            Expect(";", "NO SEMICOLON AFTER EXPRESSION");
            return new DoStmt(t.Line, t.File, body, c);
        }
        if (Accept("for"))
        {
            Expect("(", "NO LEFT BRACKET ON EXPRESSION");
            Expr? init = Is(";") ? null : Expression();
            Expect(";", "NO SEMICOLON AFTER EXPRESSION");
            Expr? cond = Is(";") ? null : Expression();
            Expect(";", "NO SEMICOLON AFTER EXPRESSION");
            Expr? step = Is(")") ? null : Expression();
            Expect(")", "NO RIGHT BRACKET ON EXPRESSION");
            return new ForStmt(t.Line, t.File, init, cond, step, Statement());
        }
        if (Accept("switch"))
        {
            Expect("(", "NO LEFT BRACKET ON EXPRESSION");
            var v = Expression();
            Expect(")", "NO RIGHT BRACKET ON EXPRESSION");
            var block = Block();
            return new SwitchStmt(t.Line, t.File, v, block.Body);
        }
        if (Accept("case"))
        {
            var v = Expression();
            Expect(":", "NO COLON AFTER CASE LABEL");
            return new CaseStmt(t.Line, t.File, v);
        }
        if (Accept("default"))
        {
            Expect(":", "NO COLON AFTER DEFAULT LABEL");
            return new CaseStmt(t.Line, t.File, null);
        }
        if (Accept("break")) { Expect(";", "NO SEMICOLON AFTER EXPRESSION"); return new BreakStmt(t.Line, t.File); }
        if (Accept("continue")) { Expect(";", "NO SEMICOLON AFTER EXPRESSION"); return new ContinueStmt(t.Line, t.File); }
        if (Accept("return"))
        {
            Expr? v = Is(";") ? null : Expression();
            Expect(";", "NO SEMICOLON AFTER EXPRESSION");
            return new ReturnStmt(t.Line, t.File, v);
        }
        if (Is("else")) throw Error("ELSE WITHOUT CORRESPONDING IF");
        var e = Expression();
        Expect(";", "NO SEMICOLON AFTER EXPRESSION");
        return new ExprStmt(t.Line, t.File, e);
    }

    // ---- expressions (C precedence) ----

    public Expr Expression() => Assignment();

    static readonly HashSet<string> AssignOps = new() { "=", "+=", "-=", "*=", "/=", "%=", "&=", "|=", "^=", "<<=", ">>=", ">>>=" };

    Expr Assignment()
    {
        var left = Conditional();
        if (Cur.Kind == TokenKind.Operator && AssignOps.Contains(Cur.Text))
        {
            var op = _t[_p++];
            var right = Assignment();
            return new AssignExpr(op.Line, op.File, op.Text, left, right);
        }
        return left;
    }

    Expr Conditional()
    {
        var c = Binary(0);
        if (Is("?"))
        {
            var q = _t[_p++];
            var a = Assignment();
            Expect(":", "NO COLON IN CONDITIONAL EXPRESSION");
            var b = Assignment();
            return new CondExpr(q.Line, q.File, c, a, b);
        }
        return c;
    }

    static readonly string[][] Levels =
    {
        new[] { "||" }, new[] { "&&" }, new[] { "|" }, new[] { "^" }, new[] { "&" },
        new[] { "==", "!=" }, new[] { "<", "<=", ">", ">=" }, new[] { "<<", ">>", ">>>" },
        new[] { "+", "-" }, new[] { "*", "/", "%" },
    };

    Expr Binary(int level)
    {
        if (level == Levels.Length) return Unary();
        var left = Binary(level + 1);
        while (Cur.Kind == TokenKind.Operator && Array.IndexOf(Levels[level], Cur.Text) >= 0)
        {
            var op = _t[_p++];
            var right = Binary(level + 1);
            left = new BinaryExpr(op.Line, op.File, op.Text, left, right);
        }
        return left;
    }

    Expr Unary()
    {
        var t = Cur;
        if (Cur.Kind == TokenKind.Operator && Cur.Text is "-" or "!" or "~" or "+")
        {
            _p++;
            var operand = Unary();
            return t.Text == "+" ? operand : new UnaryExpr(t.Line, t.File, t.Text, operand);
        }
        if (Cur.Kind == TokenKind.Operator && Cur.Text is "++" or "--")
        {
            _p++;
            return new IncDecExpr(t.Line, t.File, t.Text, Unary(), true);
        }
        return Postfix();
    }

    Expr Postfix()
    {
        var e = Primary();
        while (true)
        {
            var t = Cur;
            if (Accept("."))
            {
                if (Cur.Kind != TokenKind.Identifier) throw Error("UNDEFINED FIELD IN STRUCTURE");
                e = new MemberExpr(t.Line, t.File, e, _t[_p++].Text);
            }
            else if (Cur.Kind == TokenKind.Operator && Cur.Text is "++" or "--")
            {
                _p++;
                e = new IncDecExpr(t.Line, t.File, t.Text, e, false);
            }
            else return e;
        }
    }

    Expr Primary()
    {
        var t = Cur;
        switch (t.Kind)
        {
            case TokenKind.Int: _p++; return new IntLit(t.Line, t.File, t.IntValue);
            case TokenKind.Float: _p++; return new FloatLit(t.Line, t.File, t.FloatValue);
            case TokenKind.String: _p++; return new StringLit(t.Line, t.File, t.Text);
            case TokenKind.Identifier:
                _p++;
                if (t.Text == "OBJECT_SELF") return new ObjectLit(t.Line, t.File, true);
                if (t.Text == "OBJECT_INVALID") return new ObjectLit(t.Line, t.File, false);
                if (Accept("("))
                {
                    var args = new List<Expr>();
                    if (!Accept(")"))
                    {
                        do args.Add(Assignment()); while (Accept(","));
                        Expect(")", "NO RIGHT BRACKET ON EXPRESSION");
                    }
                    return new CallExpr(t.Line, t.File, t.Text, args);
                }
                return new NameExpr(t.Line, t.File, t.Text);
            case TokenKind.Operator when t.Text == "(":
            {
                _p++;
                var e = Expression();
                Expect(")", "NO RIGHT BRACKET ON EXPRESSION");
                return e;
            }
            case TokenKind.Operator when t.Text == "[":
            {
                _p++;
                Expr x = Is("]") ? new FloatLit(t.Line, t.File, 0) : Expression(), y = new FloatLit(t.Line, t.File, 0), z = new FloatLit(t.Line, t.File, 0);
                if (Accept(",")) { y = Expression(); if (Accept(",")) z = Expression(); }
                Expect("]", "NO RIGHT BRACKET ON VECTOR");
                return new VectorLit(t.Line, t.File, x, y, z);
            }
            case TokenKind.End: throw Error("UNEXPECTED END OF FILE");
            default: throw Error("NO SEMICOLON AFTER EXPRESSION");
        }
    }
}
