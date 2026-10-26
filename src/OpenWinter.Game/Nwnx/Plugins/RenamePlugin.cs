using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Rename: player characters' names (and community names on the player list) as other players see them,
/// overridden for everyone or for one observer, which takes precedence. Only players can be renamed (parity suite
/// nwnx_rename); scripts and DMs keep seeing the real names, as with the plugin's defaults.
/// </summary>
public static class RenamePlugin
{
    const string P = "Rename";
    /// <summary>NWNX_RENAME_PLAYERNAME_*: what the player list shows for the community name.</summary>
    public const int PlayerNameDefault = 0, PlayerNameObfuscate = 1, PlayerNameOverride = 2, PlayerNameAnonymous = 3;

    sealed record Override(string Name, int PlayerNameState);

    sealed class RenameState
    {
        /// <summary>By (target, observer); observer InvalidId is the global override.</summary>
        public readonly Dictionary<(uint Target, uint Observer), Override> Overrides = new();
    }

    static readonly System.Runtime.CompilerServices.ConditionalWeakTable<NwnxRegistry, RenameState> _state = new();
    static RenameState State(World w) => _state.GetValue(w.System<NwnxRegistry>(), _ => new RenameState());

    /// <summary>
    /// The character name and community name <paramref name="observer"/> sees for the player <paramref name="target"/>.
    /// DM observers see the real ones.
    /// </summary>
    public static (string Name, string PlayerName) Shown(World world, Creature target, Creature observer, string name, string playerName)
    {
        var st = State(world);
        if (st.Overrides.Count == 0 || observer.Player?.IsDM == true) return (name, playerName);
        if (!st.Overrides.TryGetValue((target.Id, observer.Id), out var o) && !st.Overrides.TryGetValue((target.Id, GameObject.InvalidId), out o))
            return (name, playerName);
        string community = o.PlayerNameState switch
        {
            PlayerNameObfuscate => Obfuscated(target, observer),
            PlayerNameOverride => o.Name,
            PlayerNameAnonymous => Environment.GetEnvironmentVariable("NWNX_RENAME_ANONYMOUS_NAME") ?? "Someone",
            _ => playerName,
        };
        return (o.Name, community);
    }

    /// <summary>A stable random-looking community name for one observer.</summary>
    static string Obfuscated(Creature target, Creature observer)
    {
        var rng = new Random(unchecked((int)(target.Id * 2654435761u ^ observer.Id)));
        var chars = new char[8];
        for (int i = 0; i < chars.Length; i++) chars[i] = (char)('a' + rng.Next(26));
        return new string(chars);
    }

    static void Changed(NwnxCall n, Creature target) =>
        n.World.NotifyAllPlayers("nwnx-player", new { op = "name-override", target = target.Id });

    [NwnxFunc(P, "SetPCNameOverride")]
    static void SetOverride(NwnxCall n)
    {
        var target = n.PopObject<Creature>(); var name = n.PopString(); var prefix = n.PopString(); var suffix = n.PopString();
        int playerNameState = n.PopInt(); uint observer = n.PopObjectId();
        if (target is not { IsPC: true }) return;
        if (observer != GameObject.InvalidId && n.World.Get(observer) is not Creature { IsPC: true }) return;
        State(n.World).Overrides[(target.Id, observer)] = new Override(prefix + name + suffix, playerNameState);
        Changed(n, target);
    }

    [NwnxFunc(P, "GetPCNameOverride")]
    static void GetOverride(NwnxCall n)
    {
        var target = n.PopObject<Creature>(); uint observer = n.PopObjectId();
        var st = State(n.World);
        if (target == null) { n.Push(""); return; }
        n.Push(st.Overrides.TryGetValue((target.Id, observer), out var o) || st.Overrides.TryGetValue((target.Id, GameObject.InvalidId), out o) ? o.Name : "");
    }

    /// <summary>
    /// Clears one observer's override of a target; with no target, every override that observer has (or all of them
    /// with no observer either); clearAll clears a target's global and personal overrides.
    /// </summary>
    [NwnxFunc(P, "ClearPCNameOverride")]
    static void ClearOverride(NwnxCall n)
    {
        uint target = n.PopObjectId(); uint observer = n.PopObjectId(); bool all = n.PopInt() != 0;
        var overrides = State(n.World).Overrides;
        var removed = overrides.Keys.Where(k =>
            target == GameObject.InvalidId ? observer == GameObject.InvalidId || k.Observer == observer
            : k.Target == target && (all && observer == GameObject.InvalidId || k.Observer == observer)).ToList();
        foreach (var k in removed) overrides.Remove(k);
        foreach (var t in removed.Select(k => k.Target).Distinct())
            if (n.World.Get<Creature>(t) is { } c) Changed(n, c);
    }
}
