using OpenWinter.Game.Objects;

namespace OpenWinter.Game.Nwnx.Plugins;

/// <summary>
/// NWNX_Administration: the running server's settings. Passwords, bans, names and level limits are the server's own
/// (<see cref="ServerAdministration"/>, consulted at login); play options map onto the rules that use them. Ranges and
/// defaults are nwserver's (parity suite nwnx_administration): levels take 1 - 255, PvP 0 - 2, switches are stored as 0
/// or 1, options 0 - 9 store anything.
/// </summary>
static class AdministrationPlugin
{
    const string P = "Administration";
    /// <summary>NWNX_ADMINISTRATION_OPTION_* that live with other rules.</summary>
    const int PvpSetting = 10, PauseAndPlay = 11, OnePartyOnly = 12, EnforceLegalCharacters = 13, ItemLevelRestrictions = 14,
        UseMaxHitPoints = 23, PlayerPartyControl = 27;

    static GameServer? Server(NwnxCall n) => n.World.Output as GameServer;
    static ServerAdministration Admin(NwnxCall n) => n.World.System<ServerAdministration>();

    static void SetOption(NwnxCall n, string option, Action<ServerOptions> set)
    {
        set(Admin(n).Options);
        Admin(n).Changed(option);
    }

    [NwnxFunc(P, "GetPlayerPassword")] static void GetPlayerPassword(NwnxCall n) => n.Push(Admin(n).PlayerPassword);
    [NwnxFunc(P, "SetPlayerPassword")] static void SetPlayerPassword(NwnxCall n) => Admin(n).PlayerPassword = n.PopString();
    [NwnxFunc(P, "ClearPlayerPassword")] static void ClearPlayerPassword(NwnxCall n) => Admin(n).PlayerPassword = "";

    /// <summary>The DM password; with an empty one no DM can log in, as on a dedicated server without one.</summary>
    [NwnxFunc(P, "GetDMPassword")] static void GetDMPassword(NwnxCall n) => n.Push(Admin(n).Options.DMPassword ?? "");
    [NwnxFunc(P, "SetDMPassword")]
    static void SetDMPassword(NwnxCall n)
    {
        var password = n.PopString();
        SetOption(n, nameof(ServerOptions.DMPassword), o => o.DMPassword = password.Length == 0 ? null : password);
    }

    [NwnxFunc(P, "ShutdownServer")]
    static void Shutdown(NwnxCall n)
    {
        Admin(n).ShutdownRequested = true;
        n.Registry.Log("[nwnx] server shutdown requested by script");
    }

    /// <summary>
    /// Boots the player and removes their character from the server vault: renamed to "&lt;file&gt;.bic.deleted0"
    /// (the next free number) with bPreserveBackup, deleted without. The kick message defaults to "Delete Character".
    /// </summary>
    [NwnxFunc(P, "DeletePlayerCharacter")]
    static void DeleteCharacter(NwnxCall n)
    {
        var pc = n.PopObject<Creature>(); bool backup = n.PopInt() != 0; var message = n.PopString();
        if (pc is not { IsPC: true } || Players.FamiliarPossession.SessionOf(pc) is not { } session) return;
        var character = session.PlayerCreature ?? pc;
        if (character.VaultFile.Length > 0)
        {
            var path = Path.Combine(Persistence.ServerVault.VaultDir(n.World, character), character.VaultFile + ".bic");
            try
            {
                if (File.Exists(path))
                {
                    if (backup)
                    {
                        int i = 0;
                        while (File.Exists($"{path}.deleted{i}")) i++;
                        File.Move(path, $"{path}.deleted{i}");
                    }
                    else File.Delete(path);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { n.Registry.Log($"[nwnx] deleting {path} failed: {e.Message}"); }
        }
        var reason = message.Length > 0 ? message : "Delete Character";
        if (Server(n) is { } server) server.Boot(session, reason, skipVaultSave: true);
        else
        {
            session.SkipVaultSave = true;
            session.Connection.Disconnect();
        }
    }

    [NwnxFunc(P, "AddBannedIP")] static void AddIp(NwnxCall n) => ServerAdministration.Add(Admin(n).BannedAddresses, n.PopString());
    [NwnxFunc(P, "RemoveBannedIP")] static void RemoveIp(NwnxCall n) => Admin(n).BannedAddresses.Remove(n.PopString());
    [NwnxFunc(P, "AddBannedCDKey")] static void AddKey(NwnxCall n) => ServerAdministration.Add(Admin(n).BannedKeys, n.PopString());
    [NwnxFunc(P, "RemoveBannedCDKey")] static void RemoveKey(NwnxCall n) => Admin(n).BannedKeys.Remove(n.PopString());
    [NwnxFunc(P, "AddBannedPlayerName")] static void AddName(NwnxCall n) => ServerAdministration.Add(Admin(n).BannedNames, n.PopString());
    [NwnxFunc(P, "RemoveBannedPlayerName")] static void RemoveName(NwnxCall n) => Admin(n).BannedNames.Remove(n.PopString());
    [NwnxFunc(P, "GetBannedList")] static void BannedList(NwnxCall n) => n.Push(Admin(n).BannedList());

    /// <summary>The module name on the server list; GetModuleName keeps the module's own (nwnx_administration).</summary>
    [NwnxFunc(P, "SetModuleName")] static void SetModuleName(NwnxCall n) => Admin(n).ModuleName = n.PopString();

    [NwnxFunc(P, "SetServerName")]
    static void SetServerName(NwnxCall n)
    {
        var name = n.PopString();
        SetOption(n, nameof(ServerOptions.ServerName), o => o.ServerName = name);
    }
    [NwnxFunc(P, "GetServerName")] static void GetServerName(NwnxCall n) => n.Push(Admin(n).Options.ServerName);

    [NwnxFunc(P, "GetPlayOption")]
    static void GetPlayOption(NwnxCall n)
    {
        int option = n.PopInt();
        var w = n.World;
        var progression = w.System<Progression.ProgressionSystem>();
        n.Push(option switch
        {
            PvpSetting => (int)w.Pvp,
            PauseAndPlay => Admin(n).Options.AllowPlayerPausing ? 1 : 0,
            OnePartyOnly => w.OneParty ? 1 : 0,
            EnforceLegalCharacters => progression.EnforceLegalCharacters ? 1 : 0,
            ItemLevelRestrictions => w.ItemLevelRestrictions ? 1 : 0,
            UseMaxHitPoints => progression.AlwaysMaxHitPoints ? 1 : 0,
            PlayerPartyControl => w.PlayerPartyControl ? 1 : 0,
            _ => Admin(n).Has(option) ? Admin(n).Option(option) : -1,
        });
    }

    [NwnxFunc(P, "SetPlayOption")]
    static void SetPlayOption(NwnxCall n)
    {
        int option = n.PopInt(); int value = n.PopInt();
        var w = n.World;
        var progression = w.System<Progression.ProgressionSystem>();
        bool on = value != 0;
        switch (option)
        {
            case PvpSetting:
                if (value is >= 0 and <= 2) w.Pvp = (Game.PvpSetting)value;
                break;
            case PauseAndPlay: SetOption(n, nameof(ServerOptions.AllowPlayerPausing), o => o.AllowPlayerPausing = on); break;
            case OnePartyOnly: w.OneParty = on; break;
            case EnforceLegalCharacters:
                progression.EnforceLegalCharacters = on;
                SetOption(n, nameof(ServerOptions.EnforceLegalCharacters), o => o.EnforceLegalCharacters = on);
                break;
            case ItemLevelRestrictions: w.ItemLevelRestrictions = on; break;
            case UseMaxHitPoints:
                progression.AlwaysMaxHitPoints = on;
                SetOption(n, nameof(ServerOptions.MaxHitPointsPerLevel), o => o.MaxHitPointsPerLevel = on);
                break;
            case PlayerPartyControl: w.PlayerPartyControl = on; break;
            default:
                if (Admin(n).Has(option)) Admin(n).SetOption(option, value);
                break;
        }
    }

    /// <summary>
    /// A deleted TURD lets a player stuck at a saved location log in at the start. OpenWinter keeps no TURDs, so
    /// there is never one to delete (nwserver also answers FALSE when there is none).
    /// </summary>
    [NwnxFunc(P, "DeleteTURD")]
    static void DeleteTurd(NwnxCall n)
    {
        n.PopString(); n.PopString();
        n.Push(0);
    }

    [NwnxFunc(P, "GetDebugValue")]
    static void GetDebug(NwnxCall n)
    {
        int type = n.PopInt();
        var debug = Admin(n).Debug;
        n.Push(type >= 0 && type < debug.Length && debug[type]);
    }

    [NwnxFunc(P, "SetDebugValue")]
    static void SetDebug(NwnxCall n)
    {
        int type = n.PopInt(); bool state = n.PopInt() != 0;
        var debug = Admin(n).Debug;
        if (type >= 0 && type < debug.Length) debug[type] = state;
    }

    /// <summary>Reads the rules tables again: cached 2DAs are dropped, so the next lookup reads the current files.</summary>
    [NwnxFunc(P, "ReloadRules")] static void ReloadRules(NwnxCall n) => n.World.Resources.ClearCaches();

    [NwnxFunc(P, "GetMinLevel")] static void GetMinLevel(NwnxCall n) => n.Push(Admin(n).MinLevel);
    [NwnxFunc(P, "SetMinLevel")]
    static void SetMinLevel(NwnxCall n)
    {
        int level = n.PopInt();
        if (level is >= 1 and <= 255) Admin(n).MinLevel = level;
    }
    [NwnxFunc(P, "GetMaxLevel")] static void GetMaxLevel(NwnxCall n) => n.Push(Admin(n).MaxLevel);
    [NwnxFunc(P, "SetMaxLevel")]
    static void SetMaxLevel(NwnxCall n)
    {
        int level = n.PopInt();
        if (level is >= 1 and <= 255) Admin(n).MaxLevel = level;
    }
}
