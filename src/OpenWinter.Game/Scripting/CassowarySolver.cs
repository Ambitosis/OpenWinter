using System.Globalization;
using System.Text;

namespace OpenWinter.Game.Scripting;

/// <summary>
/// Incremental linear constraint solver (the Cassowary algorithm of Badros,
/// Borning and Stuckey): a dual simplex over a tableau of rows with weighted
/// error variables for non-required constraints and edit variables for
/// suggested values. Strengths are plain weights (CASSOWARY_STRENGTH_*:
/// weak 1, medium 1000, strong 1e6, required 1001001000).
/// </summary>
public sealed class CassowarySolver
{
    public const double Required = 1001001000.0, Strong = 1000000.0, Medium = 1000.0, Weak = 1.0;
    const double Epsilon = 1e-8;

    static bool NearZero(double v) => Math.Abs(v) < Epsilon;

    enum Kind { Invalid, External, Slack, Error, Dummy }

    sealed class Symbol
    {
        static long _next;
        public readonly Kind Kind;
        public readonly long Id = Interlocked.Increment(ref _next);
        public readonly string Name;
        public Symbol(Kind kind, string name = "") { Kind = kind; Name = name; }
        public override string ToString() => Kind == Kind.External ? Name : $"{Kind.ToString()[0]}{Id}";
    }

    sealed class Row
    {
        public readonly Dictionary<Symbol, double> Cells = new();
        public double Constant;

        public Row(double constant = 0) { Constant = constant; }
        public Row Clone()
        {
            var r = new Row(Constant);
            foreach (var kv in Cells) r.Cells[kv.Key] = kv.Value;
            return r;
        }

        public double Add(double v) => Constant += v;

        public void Insert(Symbol s, double coefficient = 1)
        {
            double v = Cells.GetValueOrDefault(s) + coefficient;
            if (NearZero(v)) Cells.Remove(s);
            else Cells[s] = v;
        }

        public void Insert(Row other, double coefficient = 1)
        {
            Constant += other.Constant * coefficient;
            foreach (var kv in other.Cells) Insert(kv.Key, kv.Value * coefficient);
        }

        public void Remove(Symbol s) => Cells.Remove(s);

        public void ReverseSign()
        {
            Constant = -Constant;
            foreach (var k in Cells.Keys.ToList()) Cells[k] = -Cells[k];
        }

        /// <summary>Rearranges "0 = ... + a*s + ..." into "s = ...".</summary>
        public void SolveFor(Symbol s)
        {
            double k = -1.0 / Cells[s];
            Cells.Remove(s);
            Constant *= k;
            foreach (var key in Cells.Keys.ToList()) Cells[key] *= k;
        }

        /// <summary>Rearranges "lhs = ... + a*rhs + ..." into "rhs = ...".</summary>
        public void SolveFor(Symbol lhs, Symbol rhs)
        {
            Insert(lhs, -1);
            SolveFor(rhs);
        }

        public double CoefficientFor(Symbol s) => Cells.GetValueOrDefault(s);

        public void Substitute(Symbol s, Row row)
        {
            if (!Cells.TryGetValue(s, out var k)) return;
            Cells.Remove(s);
            Insert(row, k);
        }
    }

    /// <summary>A linear expression: sum of coefficient*variable plus a constant.</summary>
    public sealed class Expression
    {
        public readonly Dictionary<string, double> Terms = new(StringComparer.Ordinal);
        public double Constant;

        public static Expression Of(double c) => new() { Constant = c };
        public static Expression Var(string name) { var e = new Expression(); e.Terms[name] = 1; return e; }
        public bool IsConstant => Terms.Values.All(NearZero);

        public Expression Scale(double k)
        {
            var e = new Expression { Constant = Constant * k };
            foreach (var kv in Terms) e.Terms[kv.Key] = kv.Value * k;
            return e;
        }

        public Expression Plus(Expression o, double sign = 1)
        {
            var e = new Expression { Constant = Constant + sign * o.Constant };
            foreach (var kv in Terms) e.Terms[kv.Key] = kv.Value;
            foreach (var kv in o.Terms) e.Terms[kv.Key] = e.Terms.GetValueOrDefault(kv.Key) + sign * kv.Value;
            return e;
        }
    }

    public enum Relation { LessOrEqual, Equal, GreaterOrEqual }

    sealed class Constraint
    {
        public required Expression Expression; // expression (relation) 0
        public required Relation Op;
        public required double Strength;
        public required string Text;
    }

    sealed class Tag { public Symbol Marker = new(Kind.Invalid); public Symbol Other = new(Kind.Invalid); }
    sealed class EditInfo { public required Tag Tag; public required Constraint Constraint; public double Constant; }

    readonly Dictionary<Constraint, Tag> _constraints = new();
    readonly Dictionary<Symbol, Row> _rows = new();
    readonly Dictionary<string, Symbol> _vars = new(StringComparer.Ordinal);
    readonly Dictionary<string, EditInfo> _edits = new(StringComparer.Ordinal);
    readonly List<Symbol> _infeasible = new();
    Row _objective = new();
    Row? _artificial;

    public void Reset()
    {
        _constraints.Clear(); _rows.Clear(); _vars.Clear(); _edits.Clear(); _infeasible.Clear();
        _objective = new Row();
        _artificial = null;
    }

    public IEnumerable<string> Variables => _vars.Keys;

    public double Value(string name) =>
        _vars.TryGetValue(name, out var s) && _rows.TryGetValue(s, out var r) ? r.Constant : 0.0;

    /// <summary>Parses and adds "lhs op rhs" (op is &lt;=, == or &gt;=). Returns "" or an error.</summary>
    public string Constrain(string text, double strength)
    {
        if (strength < Weak || strength > Required) return $"invalid strength {strength.ToString(CultureInfo.InvariantCulture)}";
        Constraint c;
        try { c = ParseConstraint(text, strength); }
        catch (FormatException e) { return e.Message; }
        return Add(c);
    }

    string Add(Constraint c)
    {
        var tag = new Tag();
        var row = CreateRow(c, tag);
        var subject = ChooseSubject(row, tag);
        if (subject.Kind == Kind.Invalid && AllDummies(row))
        {
            if (!NearZero(row.Constant)) { RollBack(c, tag); return $"unsatisfiable constraint: {c.Text}"; }
            subject = tag.Marker;
        }
        if (subject.Kind == Kind.Invalid)
        {
            if (!AddWithArtificialVariable(row)) { RollBack(c, tag); return $"unsatisfiable constraint: {c.Text}"; }
        }
        else
        {
            row.SolveFor(subject);
            Substitute(subject, row);
            _rows[subject] = row;
        }
        _constraints[c] = tag;
        try { Optimize(_objective); }
        catch (InvalidOperationException e) { return e.Message; }
        return "";
    }

    void RollBack(Constraint c, Tag tag)
    {
        // The row was never entered; only objective contributions need undoing.
        if (tag.Marker.Kind == Kind.Error) _objective.Remove(tag.Marker);
        if (tag.Other.Kind == Kind.Error) _objective.Remove(tag.Other);
    }

    /// <summary>Sets (or adds as an edit variable) a suggested value for a variable.</summary>
    public string Suggest(string name, double value, double strength)
    {
        if (strength < Weak || strength >= Required) return "suggested values cannot be required";
        if (_edits.TryGetValue(name, out var existing) && Math.Abs(existing.Constraint.Strength - strength) > Epsilon)
        {
            RemoveConstraint(existing.Constraint);
            _edits.Remove(name);
        }
        if (!_edits.TryGetValue(name, out var info))
        {
            var c = new Constraint { Expression = Expression.Var(name), Op = Relation.Equal, Strength = strength, Text = $"{name} == (edit)" };
            var err = Add(c);
            if (err.Length > 0) return err;
            info = new EditInfo { Tag = _constraints[c], Constraint = c };
            _edits[name] = info;
        }
        double delta = value - info.Constant;
        info.Constant = value;
        if (_rows.TryGetValue(info.Tag.Marker, out var mrow))
        {
            if (mrow.Add(-delta) < 0) _infeasible.Add(info.Tag.Marker);
        }
        else if (_rows.TryGetValue(info.Tag.Other, out var orow))
        {
            if (orow.Add(delta) < 0) _infeasible.Add(info.Tag.Other);
        }
        else
        {
            foreach (var (sym, row) in _rows)
            {
                double k = row.CoefficientFor(info.Tag.Marker);
                if (k != 0 && row.Add(delta * k) < 0 && sym.Kind != Kind.External) _infeasible.Add(sym);
            }
        }
        try { DualOptimize(); }
        catch (InvalidOperationException e) { return e.Message; }
        return "";
    }

    void RemoveConstraint(Constraint c)
    {
        if (!_constraints.Remove(c, out var tag)) return;
        RemoveMarkerEffects(tag.Marker, c.Strength);
        RemoveMarkerEffects(tag.Other, c.Strength);
        if (!_rows.Remove(tag.Marker))
        {
            var leaving = MarkerLeavingRow(tag.Marker);
            if (leaving == null) throw new InvalidOperationException("failed to find leaving row");
            var row = _rows[leaving];
            _rows.Remove(leaving);
            row.SolveFor(leaving, tag.Marker);
            Substitute(tag.Marker, row);
        }
        Optimize(_objective);
    }

    void RemoveMarkerEffects(Symbol marker, double strength)
    {
        if (marker.Kind != Kind.Error) return;
        if (_rows.TryGetValue(marker, out var row)) _objective.Insert(row, -strength);
        else _objective.Insert(marker, -strength);
    }

    Symbol? MarkerLeavingRow(Symbol marker)
    {
        double r1 = double.MaxValue, r2 = double.MaxValue;
        Symbol? first = null, second = null, third = null;
        foreach (var (sym, row) in _rows)
        {
            double k = row.CoefficientFor(marker);
            if (k == 0) continue;
            if (sym.Kind == Kind.External) third = sym;
            else if (k < 0)
            {
                double r = -row.Constant / k;
                if (r < r1) { r1 = r; first = sym; }
            }
            else
            {
                double r = row.Constant / k;
                if (r < r2) { r2 = r; second = sym; }
            }
        }
        return first ?? second ?? third;
    }

    Symbol VarSymbol(string name)
    {
        if (!_vars.TryGetValue(name, out var s)) _vars[name] = s = new Symbol(Kind.External, name);
        return s;
    }

    Row CreateRow(Constraint c, Tag tag)
    {
        var row = new Row(c.Expression.Constant);
        foreach (var (name, k) in c.Expression.Terms)
        {
            if (NearZero(k)) continue;
            var s = VarSymbol(name);
            if (_rows.TryGetValue(s, out var basic)) row.Insert(basic, k);
            else row.Insert(s, k);
        }
        switch (c.Op)
        {
            case Relation.LessOrEqual:
            case Relation.GreaterOrEqual:
            {
                double k = c.Op == Relation.LessOrEqual ? 1.0 : -1.0;
                var slack = new Symbol(Kind.Slack);
                tag.Marker = slack;
                row.Insert(slack, k);
                if (c.Strength < Required)
                {
                    var error = new Symbol(Kind.Error);
                    tag.Other = error;
                    row.Insert(error, -k);
                    _objective.Insert(error, c.Strength);
                }
                break;
            }
            case Relation.Equal:
                if (c.Strength < Required)
                {
                    var plus = new Symbol(Kind.Error);
                    var minus = new Symbol(Kind.Error);
                    tag.Marker = plus;
                    tag.Other = minus;
                    row.Insert(plus, -1);
                    row.Insert(minus, 1);
                    _objective.Insert(plus, c.Strength);
                    _objective.Insert(minus, c.Strength);
                }
                else
                {
                    var dummy = new Symbol(Kind.Dummy);
                    tag.Marker = dummy;
                    row.Insert(dummy);
                }
                break;
        }
        if (row.Constant < 0) row.ReverseSign();
        return row;
    }

    static Symbol ChooseSubject(Row row, Tag tag)
    {
        foreach (var s in row.Cells.Keys)
            if (s.Kind == Kind.External) return s;
        if (tag.Marker.Kind is Kind.Slack or Kind.Error && row.CoefficientFor(tag.Marker) < 0) return tag.Marker;
        if (tag.Other.Kind is Kind.Slack or Kind.Error && row.CoefficientFor(tag.Other) < 0) return tag.Other;
        return new Symbol(Kind.Invalid);
    }

    static bool AllDummies(Row row) => row.Cells.Keys.All(s => s.Kind == Kind.Dummy);

    bool AddWithArtificialVariable(Row row)
    {
        var art = new Symbol(Kind.Slack);
        _rows[art] = row.Clone();
        _artificial = row.Clone();
        Optimize(_artificial);
        bool success = NearZero(_artificial.Constant);
        _artificial = null;
        if (_rows.Remove(art, out var artRow))
        {
            if (artRow.Cells.Count == 0) return success;
            var entering = artRow.Cells.Keys.FirstOrDefault(s => s.Kind is Kind.Slack or Kind.Error);
            if (entering == null) return false;
            artRow.SolveFor(art, entering);
            Substitute(entering, artRow);
            _rows[entering] = artRow;
        }
        foreach (var r in _rows.Values) r.Remove(art);
        _objective.Remove(art);
        return success;
    }

    void Substitute(Symbol s, Row row)
    {
        foreach (var (sym, r) in _rows)
        {
            r.Substitute(s, row);
            if (sym.Kind != Kind.External && r.Constant < 0) _infeasible.Add(sym);
        }
        _objective.Substitute(s, row);
        _artificial?.Substitute(s, row);
    }

    void Optimize(Row objective)
    {
        for (int guard = 0; guard < 10000; guard++)
        {
            Symbol? entering = null;
            foreach (var (s, k) in objective.Cells)
                if (s.Kind != Kind.Dummy && k < 0) { entering = s; break; }
            if (entering == null) return;
            Symbol? leaving = null;
            double best = double.MaxValue;
            foreach (var (s, r) in _rows)
            {
                if (s.Kind == Kind.External) continue;
                double k = r.CoefficientFor(entering);
                if (k >= 0) continue;
                double ratio = -r.Constant / k;
                if (ratio < best) { best = ratio; leaving = s; }
            }
            if (leaving == null) throw new InvalidOperationException("the objective is unbounded");
            var row = _rows[leaving];
            _rows.Remove(leaving);
            row.SolveFor(leaving, entering);
            Substitute(entering, row);
            _rows[entering] = row;
        }
    }

    void DualOptimize()
    {
        while (_infeasible.Count > 0)
        {
            var leaving = _infeasible[^1];
            _infeasible.RemoveAt(_infeasible.Count - 1);
            if (!_rows.TryGetValue(leaving, out var row) || row.Constant >= 0) continue;
            Symbol? entering = null;
            double best = double.MaxValue;
            foreach (var (s, k) in row.Cells)
            {
                if (k <= 0 || s.Kind == Kind.Dummy) continue;
                double ratio = _objective.CoefficientFor(s) / k;
                if (ratio < best) { best = ratio; entering = s; }
            }
            if (entering == null) throw new InvalidOperationException("dual optimize failed");
            _rows.Remove(leaving);
            row.SolveFor(leaving, entering);
            Substitute(entering, row);
            _rows[entering] = row;
        }
    }

    public string Debug()
    {
        var sb = new StringBuilder();
        static string F(double v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        sb.Append("Objective\n---------\n").Append(F(_objective.Constant));
        foreach (var (s, k) in _objective.Cells) sb.Append(" + ").Append(F(k)).Append(" * ").Append(s);
        sb.Append("\n\nTableau\n-------\n");
        foreach (var (s, r) in _rows)
        {
            sb.Append(s).Append(" | ").Append(F(r.Constant));
            foreach (var (cs, k) in r.Cells) sb.Append(" + ").Append(F(k)).Append(" * ").Append(cs);
            sb.Append('\n');
        }
        sb.Append("\nConstraints\n-----------\n");
        foreach (var c in _constraints.Keys) sb.Append(c.Text).Append(" | strength ").Append(F(c.Strength)).Append('\n');
        sb.Append("\nVariables\n---------\n");
        foreach (var name in _vars.Keys.OrderBy(n => n, StringComparer.Ordinal)) sb.Append(name).Append(" = ").Append(F(Value(name))).Append('\n');
        return sb.ToString();
    }

    // ---- Constraint parser: linear expressions with + - * / and parentheses ----

    static Constraint ParseConstraint(string text, double strength)
    {
        var p = new Parser(text);
        var lhs = p.ParseExpression();
        Relation op;
        if (p.Accept("<=")) op = Relation.LessOrEqual;
        else if (p.Accept(">=")) op = Relation.GreaterOrEqual;
        else if (p.Accept("==")) op = Relation.Equal;
        else throw new FormatException($"expected <=, >= or == at position {p.Position} in '{text}'");
        var rhs = p.ParseExpression();
        p.SkipSpace();
        if (!p.AtEnd) throw new FormatException($"unexpected '{text[p.Position..]}' in '{text}'");
        var expr = lhs.Plus(rhs, -1);
        if (expr.IsConstant)
        {
            bool ok = op switch
            {
                Relation.LessOrEqual => expr.Constant <= Epsilon,
                Relation.GreaterOrEqual => expr.Constant >= -Epsilon,
                _ => NearZero(expr.Constant),
            };
            if (!ok) throw new FormatException($"constraint is always false: '{text}'");
        }
        return new Constraint { Expression = expr, Op = op, Strength = strength, Text = text.Trim() };
    }

    sealed class Parser
    {
        readonly string _s;
        int _i;
        public Parser(string s) { _s = s; }
        public int Position => _i;
        public bool AtEnd => _i >= _s.Length;

        public void SkipSpace() { while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++; }

        public bool Accept(string token)
        {
            SkipSpace();
            if (string.CompareOrdinal(_s, _i, token, 0, token.Length) != 0) return false;
            _i += token.Length;
            return true;
        }

        bool PeekRelation()
        {
            SkipSpace();
            return _i + 1 < _s.Length && _s[_i + 1] == '=' && _s[_i] is '<' or '>' or '=';
        }

        public Expression ParseExpression()
        {
            var e = ParseTerm();
            while (true)
            {
                if (PeekRelation()) return e;
                if (Accept("+")) e = e.Plus(ParseTerm());
                else if (Accept("-")) e = e.Plus(ParseTerm(), -1);
                else return e;
            }
        }

        Expression ParseTerm()
        {
            var e = ParseFactor();
            while (true)
            {
                if (Accept("*"))
                {
                    var f = ParseFactor();
                    if (f.IsConstant) e = e.Scale(f.Constant);
                    else if (e.IsConstant) e = f.Scale(e.Constant);
                    else throw new FormatException("cannot multiply two variable expressions (non-linear)");
                }
                else if (Accept("/"))
                {
                    var f = ParseFactor();
                    if (!f.IsConstant) throw new FormatException("cannot divide by a variable expression (non-linear)");
                    if (NearZero(f.Constant)) throw new FormatException("division by zero");
                    e = e.Scale(1.0 / f.Constant);
                }
                else return e;
            }
        }

        Expression ParseFactor()
        {
            SkipSpace();
            if (Accept("-")) return ParseFactor().Scale(-1);
            if (Accept("+")) return ParseFactor();
            if (Accept("("))
            {
                var e = ParseExpression();
                if (!Accept(")")) throw new FormatException($"expected ')' at position {_i}");
                return e;
            }
            SkipSpace();
            int start = _i;
            if (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.'))
            {
                while (_i < _s.Length && (char.IsDigit(_s[_i]) || _s[_i] == '.')) _i++;
                if (_i < _s.Length && _s[_i] is 'e' or 'E')
                {
                    _i++;
                    if (_i < _s.Length && _s[_i] is '+' or '-') _i++;
                    while (_i < _s.Length && char.IsDigit(_s[_i])) _i++;
                }
                if (_i < _s.Length && _s[_i] is 'f' or 'F') _i++;
                var num = _s[start.._i].TrimEnd('f', 'F');
                if (!double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    throw new FormatException($"bad number '{num}'");
                return Expression.Of(v);
            }
            if (_i < _s.Length && (char.IsLetter(_s[_i]) || _s[_i] == '_'))
            {
                while (_i < _s.Length && (char.IsLetterOrDigit(_s[_i]) || _s[_i] is '_' or '.' or ':')) _i++;
                return Expression.Var(_s[start.._i]);
            }
            throw new FormatException(_i < _s.Length ? $"unexpected '{_s[_i]}' at position {_i}" : "unexpected end of constraint");
        }
    }
}
