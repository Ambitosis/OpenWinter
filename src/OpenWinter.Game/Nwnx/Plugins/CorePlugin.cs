namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>NWNX_Core: which plugins are loaded.</summary>
public static class CorePlugin
{
    /// <summary>Plugins with no script functions whose behaviour OpenWinter provides.</summary>
    static readonly HashSet<string> Functionless = new(StringComparer.Ordinal) { "core", "tweaks" };

    /// <summary>Plugins whose functions are answered but whose service OpenWinter does not run, so scripts can check first.</summary>
    static readonly HashSet<string> NotProvided = new(StringComparer.Ordinal) { "profiler", "ruby", "watchcat", "wsnetlayer" };

    /// <summary>
    /// PluginExists: the name as NWNXCall takes it, the "NWNX_" prefix required and case ignored (parity suite
    /// nwnx_core). True for a plugin OpenWinter implements.
    /// </summary>
    public static bool PluginExists(string name)
    {
        if (!name.StartsWith("NWNX_", StringComparison.OrdinalIgnoreCase) || name.Length == 5) return false;
        var key = NwnxRegistry.PluginKey(name);
        if (NotProvided.Contains(key)) return false;
        return Functionless.Contains(key) || NwnxRegistry.HasPlugin(key);
    }

    [NwnxFunc("Core", "PluginExists")] static void Exists(NwnxCall n) => n.Push(PluginExists(n.PopString()));
}
