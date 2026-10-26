using System.Text;
using OpenWinter.Game.Objects;

namespace OpenWinter.Game;

/// <summary>
/// Server settings that can change while the server runs (NWNX_Administration): the player password, bans, level
/// limits and the play options that have no other home. Defaults are nwserver's (parity suite nwnx_administration).
/// Scripts may change them before a server is attached (OnModuleLoad runs first); the server then takes those
/// changes over its configured values (<see cref="Adopt"/>).
/// </summary>
public sealed class ServerAdministration : IWorldSystem
{
    public ServerAdministration(World world) { }
    public void Step(float dt) { }

    /// <summary>Play options (NWNX_ADMINISTRATION_OPTION_*) kept here; PvP, pausing, one party, legal characters,
    /// item level restrictions, hit points and party control live with the rules that use them.</summary>
    public const int CdKeyAllowList = 15, DisallowShouting = 16, ShowDMJoinMessage = 17, BackupSavedCharacters = 18,
        AutoFailSaveOn1 = 19, ValidateSpells = 20, ExamineEffects = 21, ExamineChallengeRating = 22, RestoreSpellUses = 24,
        ResetEncounterSpawnPool = 25, HideHitPointsGained = 26, ShowPlayerJoinMessages = 28;

    /// <summary>Required of players logging in ("" for none).</summary>
    public string PlayerPassword = "";
    /// <summary>The module name shown to the server list, when a script changed it.</summary>
    public string? ModuleName;
    public readonly List<string> BannedKeys = new(), BannedAddresses = new(), BannedNames = new();
    /// <summary>Character levels allowed to log in (1 - 255 each).</summary>
    public int MinLevel = 1, MaxLevel = 40;
    /// <summary>Combat, saving throw, movement speed and hit die debug output switches.</summary>
    public readonly bool[] Debug = new bool[4];
    /// <summary>Set by NWNX_Administration_ShutdownServer; the host stops at the end of the tick.</summary>
    public bool ShutdownRequested;

    /// <summary>The attached server's options, or the ones scripts set before a server was attached.</summary>
    public ServerOptions Options { get; private set; } = new();
    readonly HashSet<string> _changed = new();

    /// <summary>Notes a script's change to an option of <see cref="Options"/>, so an attaching server keeps it.</summary>
    public void Changed(string option) => _changed.Add(option);

    /// <summary>A server attaches: the options scripts already changed carry over to its own.</summary>
    public void Adopt(ServerOptions server)
    {
        if (server == Options) return;
        var before = Options;
        if (_changed.Contains(nameof(ServerOptions.DMPassword))) server.DMPassword = before.DMPassword;
        if (_changed.Contains(nameof(ServerOptions.ServerName))) server.ServerName = before.ServerName;
        if (_changed.Contains(nameof(ServerOptions.AllowPlayerPausing))) server.AllowPlayerPausing = before.AllowPlayerPausing;
        if (_changed.Contains(nameof(ServerOptions.EnforceLegalCharacters))) server.EnforceLegalCharacters = before.EnforceLegalCharacters;
        if (_changed.Contains(nameof(ServerOptions.MaxHitPointsPerLevel))) server.MaxHitPointsPerLevel = before.MaxHitPointsPerLevel;
        Options = server;
    }

    readonly Dictionary<int, int> _options = new()
    {
        [CdKeyAllowList] = 0, [DisallowShouting] = 0, [ShowDMJoinMessage] = 1, [BackupSavedCharacters] = 0,
        [AutoFailSaveOn1] = 1, [ValidateSpells] = 1, [ExamineEffects] = 1, [ExamineChallengeRating] = 1,
        [RestoreSpellUses] = 0, [ResetEncounterSpawnPool] = 0, [HideHitPointsGained] = 0, [ShowPlayerJoinMessages] = 1,
    };

    /// <summary>A stored play option. Options 0 - 9 have no effect in the original; 7 - 9 (the amounts lost) keep any value,
    /// the others are switches.</summary>
    public int Option(int option) => _options.GetValueOrDefault(option);
    public bool Has(int option) => _options.ContainsKey(option) || option is >= 0 and <= 9;
    public void SetOption(int option, int value) => _options[option] = option is >= 7 and <= 9 ? value : value != 0 ? 1 : 0;
    public bool Enabled(int option) => Option(option) != 0;

    /// <summary>The ban lists as nwserver's banlist command prints them (nwnx_administration).</summary>
    public string BannedList()
    {
        var sb = new StringBuilder();
        void Section(string title, List<string> list, bool last)
        {
            sb.Append(title).Append(": ").Append(list.Count).Append('\n');
            foreach (var entry in list) sb.Append(entry).Append('\n');
            if (!last) sb.Append(" \n");
        }
        Section("CD Keys", BannedKeys, false);
        Section("IP Addresses", BannedAddresses, false);
        Section("Player Names", BannedNames, true);
        return sb.ToString();
    }

    /// <summary>Adds an entry once, as given (the original keeps the order, drops exact repeats and is case sensitive).</summary>
    public static void Add(List<string> list, string entry)
    {
        if (!list.Contains(entry)) list.Add(entry);
    }

    /// <summary>Why a login is refused by the bans, or null.</summary>
    public string? BanRefusal(string playerName, string key, string address)
    {
        bool keyListed = key.Length > 0 && BannedKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
        // With the CD key allow list on, the key list names the only keys that may join.
        if (Enabled(CdKeyAllowList) ? !keyListed : keyListed) return "You have been banned from this server.";
        if (address.Length > 0 && BannedAddresses.Contains(address)) return "You have been banned from this server.";
        if (playerName.Length > 0 && BannedNames.Contains(playerName, StringComparer.OrdinalIgnoreCase)) return "You have been banned from this server.";
        return null;
    }
}

public sealed partial class GameServer
{
    /// <summary>Settings scripts change at run time (NWNX_Administration).</summary>
    public ServerAdministration Admin => World.System<ServerAdministration>();

    /// <summary>The host should stop: a script asked for a shutdown.</summary>
    public bool ShutdownRequested => Admin.ShutdownRequested;

    /// <summary>Refuses a login: tells the client why and drops the connection.</summary>
    static void Refuse(PlayerSession s, string reason)
    {
        s.Send(new Net.NotifyMessage { Kind = "login-refused", Json = System.Text.Json.JsonSerializer.Serialize(new { reason }) }.Write());
        s.Connection.Disconnect();
    }

    /// <summary>Login checks from the administration settings: bans and the player password. DMs give the DM password instead.</summary>
    bool AdmitLogin(PlayerSession s, Net.HelloMessage hello)
    {
        if (Admin.BanRefusal(hello.PlayerName, hello.PlayerKey, s.Address) is { } banned) { Refuse(s, banned); return false; }
        if (!hello.DM && Admin.PlayerPassword.Length > 0 && hello.Password != Admin.PlayerPassword)
        {
            Refuse(s, "Invalid password.");
            return false;
        }
        return true;
    }

    /// <summary>A character outside the server's level limits can't enter (DMs are not limited).</summary>
    bool AdmitLevel(PlayerSession s, Creature c)
    {
        if (s.IsDM) return true;
        int level = Math.Max(1, c.TotalLevel);
        if (level >= Admin.MinLevel && level <= Admin.MaxLevel) return true;
        s.Send(new Net.ChatMessage { Channel = Net.ChatChannel.Server, Text = $"This server takes characters of level {Admin.MinLevel} to {Admin.MaxLevel}." }.Write());
        return false;
    }

    /// <summary>
    /// Boots a player (NWNX_Administration_DeletePlayerCharacter, BootPC): the client is told why and disconnected at
    /// the end of the tick. <paramref name="skipVaultSave"/> keeps the leaving character from being saved again.
    /// </summary>
    public void Boot(PlayerSession s, string reason, bool skipVaultSave = false)
    {
        if (skipVaultSave) s.SkipVaultSave = true;
        s.Send(new Net.NotifyMessage { Kind = "boot", Json = System.Text.Json.JsonSerializer.Serialize(new { reason }) }.Write());
        s.Connection.Disconnect();
    }
}
