using System.Globalization;
using System.Text.Json;
using OpenWinter.Aurora.Formats;

namespace OpenWinter.Game;

/// <summary>
/// A dedicated server's settings, read the way nwserver reads them: its defaults, then nwnplayer.ini's "Server
/// Options" and "Game Options" for keys settings.tml lacks, then settings.tml (both in the user folder), then the
/// command line. Values are kept as settings.tml writes them, by key ("server.vault.mode").
/// </summary>
public sealed class ServerSettings
{
    sealed record Setting(string Key, string Default, string? IniSection = null, string? IniLabel = null);

    const string ServerSection = "Server Options", GameSection = "Game Options";

    /// <summary>nwserver's keys, defaults and older nwnplayer.ini labels (from the schema it writes into settings.tml).</summary>
    static readonly Setting[] Settings =
    [
        new("ruleset.always-roll-max-hitpoints-on-levelup", "false", ServerSection, "Max Hit Points"),
        new("ruleset.combat.max-ability-bonus", "12", ServerSection, "AbilityBonusLimit"),
        new("ruleset.combat.max-ability-penalty", "30", ServerSection, "AbilityPenaltyLimit"),
        new("ruleset.combat.max-attack-bonus", "20", ServerSection, "AttackBonusLimit"),
        new("ruleset.combat.max-damage-bonus", "100", ServerSection, "DamageBonusLimit"),
        new("ruleset.combat.max-saving-throw-bonus", "20", ServerSection, "SavingThrowBonusLimit"),
        new("ruleset.combat.max-skill-bonus", "50", ServerSection, "SkillBonusLimit"),
        new("ruleset.combat.sticky-modes", "false", ServerSection, "StickyCombatModes"),
        new("ruleset.difficulty", "1", GameSection, "Difficulty Level"),
        new("ruleset.encounters.always-reset", "false", ServerSection, "Always Reset Encounter Spawns"),
        new("ruleset.enforce-legal-characters", "true", ServerSection, "Enforce Legal Characters"),
        new("ruleset.hide-hitpoints-gained-on-levelup", "false", ServerSection, "Hide Hit Points Gained"),
        new("ruleset.item-level-restrictions", "false", ServerSection, "ItemLevelRestrictions"),
        new("ruleset.max-character-level", "40", ServerSection, "MaxCharLevel"),
        new("ruleset.min-character-level", "1", ServerSection, "MinCharLevel"),
        new("ruleset.natural-ones-fail-saving-throws", "true", ServerSection, "Saving Throw Automatic Failure On 1"),
        new("ruleset.show-active-effects", "true", ServerSection, "Examine Effects On Creatures"),
        new("ruleset.show-challenge-ratings", "true", ServerSection, "Examine CR On Creatures"),
        new("ruleset.validate-spells", "true", ServerSection, "Validate Spells"),
        new("server.allow-player-pausing", "false", ServerSection, "PauseAndPlay"),
        new("server.autosave.enabled", "false"),
        new("server.autosave.interval", "5"),
        new("server.disallow-shouting", "false", ServerSection, "Disallow Shouting"),
        new("server.identity.game-region", "0", ServerSection, "Game Region"),
        new("server.identity.game-type", "0", ServerSection, "Game Type"),
        new("server.identity.name", "Server", ServerSection, "Server Name"),
        new("server.identity.public", "true", ServerSection, "GameSpy Enabled"),
        new("server.login.admin-password", "", ServerSection, "ServerAdminPassword"),
        new("server.login.dm-password", "", ServerSection, "DMPassword"),
        new("server.login.max-players", "6", ServerSection, "Max Players"),
        new("server.login.player-dm.enabled", "true"),
        new("server.login.player-password", "", ServerSection, "PlayerPassword"),
        new("server.net.port", "5121", ServerSection, "Game Port"),
        new("server.one-party-only", "true", ServerSection, "One Party Only"),
        new("server.player-party-control", "false"),
        new("server.pvp-mode", "1", ServerSection, "PVP Setting"),
        new("server.reload-when-empty", "true", ServerSection, "Reload Module When Empty"),
        new("server.restore-spell-uses-on-login", "false", ServerSection, "Restore Spell Uses On Login"),
        new("server.savegames-store-characters", "true", ServerSection, "SaveCharsInSaveGame"),
        new("server.savegames-vault-characters-only", "false", ServerSection, "VaultCharsOnly"),
        new("server.show-dm-join-messages", "true", ServerSection, "ShowDMJoinedMessage"),
        new("server.show-player-join-messages", "true"),
        new("server.vault.backup-server-characters", "false", ServerSection, "BackupSavedChars"),
        new("server.vault.by-player-name", "false", ServerSection, "Server Vault By Player Name"),
        // 1 lets players bring local characters, 0 keeps characters in the server vault.
        new("server.vault.mode", "1", ServerSection, "AllowLocalChars"),
        new("server.vault.sticky-player-names", "false", ServerSection, "Sticky Player Names"),
        new("server.vault.suppress-base", "false", ServerSection, "Suppress Base Servervault"),
    ];

    /// <summary>nwserver's flags that set a key (each takes a value; 0/1 for switches).</summary>
    static readonly Dictionary<string, string> Flags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["maxclients"] = "server.login.max-players", ["minlevel"] = "ruleset.min-character-level",
        ["maxlevel"] = "ruleset.max-character-level", ["pauseandplay"] = "server.allow-player-pausing",
        ["pvp"] = "server.pvp-mode", ["elc"] = "ruleset.enforce-legal-characters",
        ["ilr"] = "ruleset.item-level-restrictions", ["gametype"] = "server.identity.game-type",
        ["oneparty"] = "server.one-party-only", ["difficulty"] = "ruleset.difficulty",
        ["playerpassword"] = "server.login.player-password", ["dmpassword"] = "server.login.dm-password",
        ["adminpassword"] = "server.login.admin-password", ["servername"] = "server.identity.name",
        ["publicserver"] = "server.identity.public", ["reloadwhenempty"] = "server.reload-when-empty",
        ["port"] = "server.net.port",
    };

    /// <summary>nwserver's flags that don't set a key: they take a value, apart from -interactive and -quiet.</summary>
    static readonly HashSet<string> HostFlags = new(StringComparer.OrdinalIgnoreCase)
        { "module", "load", "userdirectory", "nwsyncurl", "nwsynchash", "servervault", "autosaveinterval" };
    static readonly HashSet<string> Switches = new(StringComparer.OrdinalIgnoreCase) { "interactive", "quiet" };

    readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    /// <summary>OpenWinter's own settings (server.json and --options): not in nwserver.</summary>
    public bool ServeContent = true;
    /// <summary>Exports every character to the server vault this often (minutes, 0 = only on logout and by script).</summary>
    public float ExportMinutes;
    public string? StartArea;
    public string Module = "";

    public ServerSettings()
    {
        foreach (var s in Settings) _values[s.Key] = s.Default;
    }

    public string Text(string key) => _values.TryGetValue(key, out var v) ? v : throw new KeyNotFoundException(key);
    public bool Bool(string key) => Text(key) is "true" or "1";
    public int Int(string key) => int.TryParse(Text(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
        ? v : int.Parse(Settings.First(s => s.Key == key).Default, CultureInfo.InvariantCulture);

    /// <summary>Sets a known key; switches take true/false or 1/0. Unknown keys are ignored.</summary>
    public void Set(string key, string value)
    {
        if (Settings.FirstOrDefault(s => s.Key == key) is not { } setting) return;
        value = value.Trim();
        if (setting.Default is "true" or "false")
        {
            if (value is "1" or "true") value = "true";
            else if (value is "0" or "false") value = "false";
            else return;
        }
        else if (int.TryParse(setting.Default, out _) && !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return;
        _values[key] = value;
    }

    /// <summary>Settings from a settings.tml and an nwnplayer.ini (either may be null): the ini fills only keys settings.tml lacks.</summary>
    public static ServerSettings Read(string? settingsTml, byte[]? nwnplayerIni)
    {
        var result = new ServerSettings();
        var tml = settingsTml != null ? Toml.Read(settingsTml) : new Dictionary<string, string>();
        var ini = nwnplayerIni != null ? Ini.Parse(nwnplayerIni) : null;
        foreach (var s in Settings)
        {
            if (tml.TryGetValue(s.Key, out var value)) result.Set(s.Key, value);
            else if (ini != null && s.IniLabel != null && ini.Get(s.IniSection!, s.IniLabel, "") is { Length: > 0 } old) result.Set(s.Key, old);
        }
        return result;
    }

    /// <summary>settings.tml and nwnplayer.ini from a user folder.</summary>
    public static ServerSettings Load(string userDirectory)
    {
        var tml = Path.Combine(userDirectory, "settings.tml");
        var ini = Path.Combine(userDirectory, "nwnplayer.ini");
        return Read(File.Exists(tml) ? File.ReadAllText(tml) : null, File.Exists(ini) ? File.ReadAllBytes(ini) : null);
    }

    /// <summary>The user folder a command line names with -userdirectory, or null.</summary>
    public static string? UserDirectory(IReadOnlyList<string> args) => FlagValue(args, "userdirectory");

    /// <summary>The module a command line names with -module (or --module), or null.</summary>
    public static string? ModuleName(IReadOnlyList<string> args) => FlagValue(args, "module") ?? OptionValue(args, "module");

    static string? FlagValue(IReadOnlyList<string> args, string flag)
    {
        for (int i = 0; i + 1 < args.Count; i++)
            if (args[i].Length > 1 && args[i][0] == '-' && args[i][1] != '-' && args[i][1..].Equals(flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return null;
    }

    static string? OptionValue(IReadOnlyList<string> args, string option)
    {
        for (int i = 0; i + 1 < args.Count; i++)
            if (args[i] == "--" + option) return args[i + 1];
        return null;
    }

    /// <summary>
    /// Applies nwserver's flags (-maxclients 9 -servervault 1 ...); flags beat both files. Returns what nwserver would
    /// answer "not understood" to. OpenWinter's own --options are left for <see cref="ApplyOptions"/>.
    /// </summary>
    public List<string> ApplyFlags(IReadOnlyList<string> args)
    {
        var notUnderstood = new List<string>();
        for (int i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                // An OpenWinter option and its value, if it has one.
                if (i + 1 < args.Count && !args[i + 1].StartsWith('-')) i++;
                continue;
            }
            if (arg.Length < 2 || arg[0] != '-')
            {
                notUnderstood.Add(arg);
                continue;
            }
            var flag = arg[1..];
            if (Switches.Contains(flag)) continue;
            if (i + 1 >= args.Count)
            {
                notUnderstood.Add(arg);
                continue;
            }
            var value = args[++i];
            if (Flags.TryGetValue(flag, out var key)) Set(key, value);
            else if (flag.Equals("servervault", StringComparison.OrdinalIgnoreCase)) Set("server.vault.mode", value == "1" ? "0" : "1");
            else if (flag.Equals("autosaveinterval", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out int minutes))
            {
                Set("server.autosave.enabled", minutes > 0 ? "true" : "false");
                if (minutes > 0) Set("server.autosave.interval", value);
            }
            else if (!HostFlags.Contains(flag)) notUnderstood.Add(arg);
        }
        return notUnderstood;
    }

    /// <summary>
    /// OpenWinter's own options: --name, --module, --port, --maxplayers, --pvp none|party|full, --difficulty,
    /// --localvault, --area, --dmpassword, --password, --pauseandplay, --oneparty 0|1.
    /// </summary>
    public void ApplyOptions(IReadOnlyDictionary<string, string> opts)
    {
        if (opts.TryGetValue("name", out var name)) Set("server.identity.name", name);
        if (opts.TryGetValue("module", out var module)) Module = module;
        if (opts.TryGetValue("port", out var port)) Set("server.net.port", port);
        if (opts.TryGetValue("maxplayers", out var max)) Set("server.login.max-players", max);
        if (opts.TryGetValue("pvp", out var pvp)) SetPvp(pvp);
        if (opts.TryGetValue("difficulty", out var difficulty)) Set("ruleset.difficulty", difficulty);
        if (opts.ContainsKey("localvault")) Set("server.vault.mode", "1");
        if (opts.TryGetValue("area", out var area)) StartArea = area;
        if (opts.TryGetValue("dmpassword", out var dm)) Set("server.login.dm-password", dm);
        if (opts.TryGetValue("password", out var pw)) Set("server.login.player-password", pw);
        if (opts.ContainsKey("pauseandplay")) Set("server.allow-player-pausing", "true");
        if (opts.TryGetValue("oneparty", out var oneParty)) Set("server.one-party-only", oneParty != "0" ? "true" : "false");
    }

    void SetPvp(string pvp) => Set("server.pvp-mode", pvp.ToLowerInvariant() switch
    {
        "none" => "0",
        "party" => "1",
        "full" => "2",
        _ => pvp,
    });

    /// <summary>
    /// OpenWinter's server.json, from before settings.tml was read: only the properties it names apply, over both
    /// files and under the command line.
    /// </summary>
    public void ApplyJson(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            var v = p.Value;
            string Str() => v.ValueKind switch
            {
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.String => v.GetString()!,
                _ => v.GetRawText(),
            };
            switch (p.Name.ToLowerInvariant())
            {
                case "name": Set("server.identity.name", Str()); break;
                case "module": Module = Str(); break;
                case "port": Set("server.net.port", Str()); break;
                case "maxplayers": Set("server.login.max-players", Str()); break;
                case "pvp": SetPvp(Str()); break;
                case "difficulty": Set("ruleset.difficulty", Str()); break;
                case "vault": Set("server.vault.mode", Str().Equals("local", StringComparison.OrdinalIgnoreCase) ? "1" : "0"); break;
                case "itemlevelrestrictions": Set("ruleset.item-level-restrictions", Str()); break;
                case "autosaveminutes": ExportMinutes = v.TryGetSingle(out var m) ? m : 0; break;
                case "servecontent": ServeContent = v.ValueKind != JsonValueKind.False; break;
                case "dmpassword": if (v.ValueKind == JsonValueKind.String) Set("server.login.dm-password", Str()); break;
                case "playerpassword": if (v.ValueKind == JsonValueKind.String) Set("server.login.player-password", Str()); break;
                case "startarea": StartArea = v.ValueKind == JsonValueKind.String ? Str() : null; break;
                case "allowplayerpausing": Set("server.allow-player-pausing", Str()); break;
                case "oneparty": Set("server.one-party-only", Str()); break;
                case "playerpartycontrol": Set("server.player-party-control", Str()); break;
                case "vaultbyplayername": Set("server.vault.by-player-name", Str()); break;
                case "stickyplayernames": Set("server.vault.sticky-player-names", Str()); break;
            }
        }
    }

    public PvpSetting Pvp => Int("server.pvp-mode") switch { 0 => PvpSetting.None, 2 => PvpSetting.Full, _ => PvpSetting.Party };
    public bool LocalVault => Int("server.vault.mode") != 0;

    /// <summary>The settings scripts can read and change, set on a world before its module loads.</summary>
    public void ApplyTo(World world)
    {
        world.Pvp = Pvp;
        world.ItemLevelRestrictions = Bool("ruleset.item-level-restrictions");
        world.OneParty = Bool("server.one-party-only");
        world.PlayerPartyControl = Bool("server.player-party-control");
        world.GameDifficulty = Math.Clamp(Int("ruleset.difficulty"), 1, 4);
        var limits = world.Limits;
        limits.AbilityBonus = Int("ruleset.combat.max-ability-bonus");
        limits.AbilityPenalty = Int("ruleset.combat.max-ability-penalty");
        limits.AttackBonus = Int("ruleset.combat.max-attack-bonus");
        limits.DamageBonus = Int("ruleset.combat.max-damage-bonus");
        limits.SavingThrowBonus = Int("ruleset.combat.max-saving-throw-bonus");
        limits.SkillBonus = Int("ruleset.combat.max-skill-bonus");
        var progression = world.System<Progression.ProgressionSystem>();
        progression.EnforceLegalCharacters = Bool("ruleset.enforce-legal-characters");
        progression.AlwaysMaxHitPoints = Bool("ruleset.always-roll-max-hitpoints-on-levelup");
        var admin = world.System<ServerAdministration>();
        // What OnModuleLoad reads before the server attaches (nwserver has these from the start).
        var before = admin.Options;
        before.ServerName = Text("server.identity.name");
        before.AllowPlayerPausing = Bool("server.allow-player-pausing");
        before.EnforceLegalCharacters = progression.EnforceLegalCharacters;
        before.MaxHitPointsPerLevel = progression.AlwaysMaxHitPoints;
        before.DMPassword = Text("server.login.dm-password") is { Length: > 0 } dm ? dm : null;
        admin.PlayerPassword = Text("server.login.player-password");
        admin.MinLevel = Math.Clamp(Int("ruleset.min-character-level"), 1, 255);
        admin.MaxLevel = Math.Clamp(Int("ruleset.max-character-level"), 1, 255);
        foreach (var (option, key) in new[]
        {
            (ServerAdministration.DisallowShouting, "server.disallow-shouting"),
            (ServerAdministration.ShowDMJoinMessage, "server.show-dm-join-messages"),
            (ServerAdministration.BackupSavedCharacters, "server.vault.backup-server-characters"),
            (ServerAdministration.AutoFailSaveOn1, "ruleset.natural-ones-fail-saving-throws"),
            (ServerAdministration.ValidateSpells, "ruleset.validate-spells"),
            (ServerAdministration.ExamineEffects, "ruleset.show-active-effects"),
            (ServerAdministration.ExamineChallengeRating, "ruleset.show-challenge-ratings"),
            (ServerAdministration.RestoreSpellUses, "server.restore-spell-uses-on-login"),
            (ServerAdministration.ResetEncounterSpawnPool, "ruleset.encounters.always-reset"),
            (ServerAdministration.HideHitPointsGained, "ruleset.hide-hitpoints-gained-on-levelup"),
            (ServerAdministration.ShowPlayerJoinMessages, "server.show-player-join-messages"),
        })
            admin.SetOption(option, Bool(key) ? 1 : 0);
    }

    /// <summary>The server's own options (a host adds what isn't a setting: module path, harness and so on).</summary>
    public ServerOptions ToOptions() => new()
    {
        StartArea = StartArea,
        CharacterSelection = true,
        Vault = LocalVault ? Net.VaultMode.Local : Net.VaultMode.Server,
        VaultByPlayerName = Bool("server.vault.by-player-name"),
        StickyPlayerNames = Bool("server.vault.sticky-player-names"),
        MaxPlayers = Math.Max(1, Int("server.login.max-players")),
        ExportInterval = ExportMinutes * 60,
        AutoSaveInterval = Bool("server.autosave.enabled") ? Math.Max(1, Int("server.autosave.interval")) * 60 : 0,
        ReloadWhenEmpty = Bool("server.reload-when-empty"),
        // Without a DM password the server takes no DMs (an empty one would otherwise admit anyone as a DM).
        DMPassword = Text("server.login.dm-password") is { Length: > 0 } dm ? dm : null,
        ServeContent = ServeContent,
        AllowPlayerPausing = Bool("server.allow-player-pausing"),
        EnforceLegalCharacters = Bool("ruleset.enforce-legal-characters"),
        MaxHitPointsPerLevel = Bool("ruleset.always-roll-max-hitpoints-on-levelup"),
        ServerName = Text("server.identity.name"),
    };
}
