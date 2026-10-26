using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Profiler: OpenWinter exports no metrics, so scopes are matched and otherwise ignored.</summary>
static class ProfilerPlugin
{
    const string P = "Profiler";
    const string Reason = "OpenWinter exports no metrics; perf scopes are not recorded";

    [NwnxFunc(P, "PushPerfScope")]
    static void Push(NwnxCall n)
    {
        n.PopAll();
        n.ReportUnsupported(Reason);
        var st = n.State(GameObject.InvalidId);
        st["profiler:depth"] = NwnxValue.Int((st.TryGetValue("profiler:depth", out var d) && d.Value is int i ? i : 0) + 1);
    }

    [NwnxFunc(P, "PopPerfScope")]
    static void Pop(NwnxCall n)
    {
        var st = n.State(GameObject.InvalidId);
        int depth = st.TryGetValue("profiler:depth", out var d) && d.Value is int i ? i : 0;
        if (depth <= 0) n.Registry.Log($"[nwnx] Profiler: PopPerfScope without a matching PushPerfScope{(n.Thread != null ? $" (in {n.Thread.Program.Name})" : "")}");
        st["profiler:depth"] = NwnxValue.Int(Math.Max(0, depth - 1));
    }
}
