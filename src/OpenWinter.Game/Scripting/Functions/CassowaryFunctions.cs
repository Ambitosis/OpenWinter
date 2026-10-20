using System.Runtime.CompilerServices;

namespace OpenWinter.Game.Scripting.Functions;

/// <summary>EE cassowary constraint solver handles and their local-variable storage.</summary>
public static class CassowaryFunctions
{
    static readonly ConditionalWeakTable<Cassowary, CassowarySolver> _solvers = new();

    /// <summary>The solver behind a cassowary handle (handles are references: copies share it).</summary>
    public static CassowarySolver Solver(Cassowary c) => _solvers.GetValue(c, _ => new CassowarySolver());

    /// <summary>Keeps <see cref="Cassowary.Values"/> in step with the solver, for code that serialises handles.</summary>
    static void Sync(Cassowary c, CassowarySolver s)
    {
        c.Values.Clear();
        foreach (var v in s.Variables) c.Values[v] = (float)s.Value(v);
    }

    /// <summary>Script floats can't hold CASSOWARY_STRENGTH_REQUIRED exactly; snap it back.</summary>
    static double Strength(float f) => Math.Abs(f - CassowarySolver.Required) < 1000 ? CassowarySolver.Required : f;

    [NwFunc("GetLocalCassowary")]
    static object? GetLocal(Call c) =>
        c.Obj(0)?.LocalCassowaries.GetValueOrDefault(c.S(1)) ?? new Cassowary();
    [NwFunc("SetLocalCassowary")]
    static object? SetLocal(Call c)
    {
        if (c.Obj(0) is { } o) o.LocalCassowaries[c.S(1)] = c.C(2);
        return null;
    }
    [NwFunc("DeleteLocalCassowary")]
    static object? DeleteLocal(Call c)
    {
        c.Obj(0)?.LocalCassowaries.Remove(c.S(1));
        return null;
    }

    [NwFunc("CassowaryReset")]
    static object? Reset(Call c)
    {
        var h = c.C(0);
        Solver(h).Reset();
        h.Values.Clear();
        return null;
    }
    [NwFunc("CassowaryConstrain")]
    static object? Constrain(Call c)
    {
        var h = c.C(0);
        var s = Solver(h);
        var err = s.Constrain(c.S(1), Strength(c.F(2, (float)CassowarySolver.Required)));
        Sync(h, s);
        return err;
    }
    [NwFunc("CassowarySuggestValue")]
    static object? Suggest(Call c)
    {
        var h = c.C(0);
        var s = Solver(h);
        var err = s.Suggest(c.S(1), c.F(2), Strength(c.F(3, (float)CassowarySolver.Strong)));
        if (err.Length > 0) c.Host.Log($"[script] CassowarySuggestValue: {err}");
        Sync(h, s);
        return null;
    }
    [NwFunc("CassowaryGetValue")] static object? GetValue(Call c) => (float)Solver(c.C(0)).Value(c.S(1));
    [NwFunc("CassowaryDebug")] static object? Debug(Call c) => Solver(c.C(0)).Debug();
}
