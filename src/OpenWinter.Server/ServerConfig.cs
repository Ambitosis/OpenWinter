using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Dedicated server settings (the equivalent of nwserver's options), read from a JSON file
/// (--config path, default ./server.json) with command-line flags taking precedence.
/// </summary>
public sealed class ServerConfig
{
    public string Name { get; set; } = "OpenWinter Server";
    /// <summary>Module folder or .mod path; empty = config/local.json's module.</summary>
    public string Module { get; set; } = "";
    public int Port { get; set; } = 5121;
    public int MaxPlayers { get; set; } = 64;
    /// <summary>"none", "party" or "full".</summary>
    public string Pvp { get; set; } = "party";
    /// <summary>GAME_DIFFICULTY_*: 1 easy (nwserver's default), 2 normal, 3 core rules (D&amp;D Hardcore), 4 difficult.</summary>
    public int Difficulty { get; set; } = 1;
    /// <summary>"server" (characters stay on the server) or "local" (players bring their own).</summary>
    public string Vault { get; set; } = "server";
    public bool ItemLevelRestrictions { get; set; } = true;
    /// <summary>Save every character to the server vault this often (minutes, 0 = only on logout).</summary>
    public float AutoSaveMinutes { get; set; } = 5;
    /// <summary>Send players the module's haks and custom TLK they're missing.</summary>
    public bool ServeContent { get; set; } = true;
    /// <summary>Dungeon Master password; null/absent = no DMs allowed.</summary>
    public string? DMPassword { get; set; }
    /// <summary>Area resref players start in instead of the module's entry point (testing).</summary>
    public string? StartArea { get; set; }
    /// <summary>Players may pause the game (nwserver's PauseAndPlay); Dungeon Masters always can.</summary>
    public bool AllowPlayerPausing { get; set; }
    /// <summary>One Party Only (nwserver's server.one-party-only, on by default there): every player in one party.</summary>
    public bool OneParty { get; set; } = true;
    /// <summary>Player party control (nwserver's server.player-party-control, off by default): enables SetCommandingPlayer.</summary>
    public bool PlayerPartyControl { get; set; }

    static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ServerConfig Load(string path)
    {
        if (!File.Exists(path)) return new ServerConfig();
        return JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(path), Json) ?? new ServerConfig();
    }

    /// <summary>Command-line flags override the file: --name, --module, --port, --maxplayers, --pvp, --difficulty, --localvault, --area, --oneparty 0|1.</summary>
    public void Apply(Dictionary<string, string> opts)
    {
        if (opts.TryGetValue("name", out var name)) Name = name;
        if (opts.TryGetValue("module", out var module)) Module = module;
        if (opts.TryGetValue("port", out var port)) Port = int.Parse(port);
        if (opts.TryGetValue("maxplayers", out var max)) MaxPlayers = int.Parse(max);
        if (opts.TryGetValue("pvp", out var pvp)) Pvp = pvp;
        if (opts.TryGetValue("difficulty", out var diff)) Difficulty = int.Parse(diff);
        if (opts.ContainsKey("localvault")) Vault = "local";
        if (opts.TryGetValue("area", out var area)) StartArea = area;
        if (opts.TryGetValue("dmpassword", out var dm)) DMPassword = dm;
        if (opts.ContainsKey("pauseandplay")) AllowPlayerPausing = true;
        if (opts.TryGetValue("oneparty", out var oneParty)) OneParty = oneParty != "0";
    }

    public OpenWinter.Game.PvpSetting PvpSetting => Pvp.ToLowerInvariant() switch
    {
        "none" => OpenWinter.Game.PvpSetting.None,
        "full" => OpenWinter.Game.PvpSetting.Full,
        _ => OpenWinter.Game.PvpSetting.Party,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);
}
