using System.Text;
using OpenWinter.Aurora.Formats;
using OpenWinter.Game;
using OpenWinter.Net;
using Xunit;

namespace OpenWinter.Tests;

/// <summary>
/// A dedicated server's settings read as nwserver reads them: defaults, nwnplayer.ini, settings.tml, then flags
/// (parity suite server_settings, run on nwserver with each source).
/// </summary>
public class ServerSettingsTests
{
    const string Tml = """
        [ruleset]
        	difficulty = 4
        	enforce-legal-characters = false
        	max-character-level = 20
        	[ruleset.combat]
        		max-attack-bonus = 30
        [server]
        	one-party-only = false
        	pvp-mode = 0
        	[server.identity]
        		name = "Tml \"quoted\""
        	[server.login]
        		dm-password = "tdm"
        	[server.vault]
        		mode = 0
        ["~~schema"]
        	["~~schema".binds."server.pvp-mode"]
        		default = 1
        	[["~~schema".binds."server.pvp-mode".ini-deprecations]]
        		label = "PVP Setting"
        """;

    const string Ini = """
        [Server Options]
        PVP Setting=2
        Max Players=11
        Server Vault By Player Name=1
        PauseAndPlay=1
        [Game Options]
        Difficulty Level=2
        """;

    [Fact]
    public void DefaultsAreNwserversOwn()
    {
        var s = new ServerSettings();
        Assert.Equal(6, s.Int("server.login.max-players"));
        Assert.Equal(PvpSetting.Party, s.Pvp);
        Assert.True(s.LocalVault);
        Assert.True(s.Bool("ruleset.enforce-legal-characters"));
        Assert.False(s.Bool("ruleset.item-level-restrictions"));
        Assert.True(s.Bool("server.one-party-only"));
        Assert.Equal("Server", s.Text("server.identity.name"));
        var o = s.ToOptions();
        Assert.Equal(VaultMode.Local, o.Vault);
        Assert.False(o.VaultByPlayerName);
        Assert.True(o.ReloadWhenEmpty);
        Assert.Equal(0, o.AutoSaveInterval);
        Assert.Null(o.DMPassword);
    }

    [Fact]
    public void SettingsTmlBeatsTheIniKeyByKey()
    {
        var s = ServerSettings.Read(Tml, Encoding.ASCII.GetBytes(Ini));
        // settings.tml's keys win; the ini fills the keys settings.tml lacks.
        Assert.Equal(PvpSetting.None, s.Pvp);
        Assert.Equal(4, s.Int("ruleset.difficulty"));
        Assert.Equal(11, s.Int("server.login.max-players"));
        Assert.True(s.Bool("server.vault.by-player-name"));
        Assert.True(s.Bool("server.allow-player-pausing"));
        Assert.Equal("Tml \"quoted\"", s.Text("server.identity.name"));
        Assert.False(s.LocalVault);
        Assert.Equal(30, s.Int("ruleset.combat.max-attack-bonus"));

        var iniOnly = ServerSettings.Read(null, Encoding.ASCII.GetBytes(Ini));
        Assert.Equal(PvpSetting.Full, iniOnly.Pvp);
        Assert.Equal(2, iniOnly.Int("ruleset.difficulty"));
    }

    [Fact]
    public void FlagsBeatBothFiles()
    {
        var s = ServerSettings.Read(Tml, Encoding.ASCII.GetBytes(Ini));
        var notUnderstood = s.ApplyFlags(["-module", "Prelude", "-pvp", "2", "-difficulty", "2", "-servername", "Flag",
            "-oneparty", "1", "-maxclients", "9", "-elc", "1", "-ilr", "1", "-dmpassword", "dmpw", "-minlevel", "2",
            "-userdirectory", "x", "-interactive", "-quiet", "-bogus", "1", "stray", "--area", "start"]);
        Assert.Equal(["-bogus", "stray"], notUnderstood);
        Assert.Equal(PvpSetting.Full, s.Pvp);
        Assert.Equal(2, s.Int("ruleset.difficulty"));
        Assert.Equal("Flag", s.Text("server.identity.name"));
        Assert.True(s.Bool("server.one-party-only"));
        Assert.Equal(9, s.Int("server.login.max-players"));
        Assert.True(s.Bool("ruleset.enforce-legal-characters"));
        Assert.True(s.Bool("ruleset.item-level-restrictions"));
        Assert.Equal("dmpw", s.ToOptions().DMPassword);
        Assert.Equal(2, s.Int("ruleset.min-character-level"));
        // Settings from the files that no flag names stay.
        Assert.Equal(20, s.Int("ruleset.max-character-level"));
    }

    [Fact]
    public void ServerVaultFlagOverridesTheVaultMode()
    {
        var s = ServerSettings.Read(Tml, null);
        Assert.False(s.LocalVault);
        s.ApplyFlags(["-servervault", "0"]);
        Assert.True(s.LocalVault);
        s.ApplyFlags(["-servervault", "1"]);
        Assert.False(s.LocalVault);
        Assert.Equal(VaultMode.Server, s.ToOptions().Vault);
    }

    [Fact]
    public void AutosaveIntervalTurnsTheAutosaveOn()
    {
        var s = new ServerSettings();
        s.ApplyFlags(["-autosaveinterval", "3"]);
        Assert.Equal(180, s.ToOptions().AutoSaveInterval);
        s.ApplyFlags(["-autosaveinterval", "0"]);
        Assert.Equal(0, s.ToOptions().AutoSaveInterval);
    }

    [Fact]
    public void CommandLineNamesTheUserFolderAndModule()
    {
        string[] args = ["-userdirectory", "C:/srv/user", "-module", "Contest Of Champions 0492"];
        Assert.Equal("C:/srv/user", ServerSettings.UserDirectory(args));
        Assert.Equal("Contest Of Champions 0492", ServerSettings.ModuleName(args));
        Assert.Equal("m.mod", ServerSettings.ModuleName(["--module", "m.mod"]));
        Assert.Null(ServerSettings.UserDirectory(["--module", "m.mod"]));
    }

    [Fact]
    public void ServerJsonSetsOnlyWhatItNames()
    {
        var s = ServerSettings.Read(Tml, null);
        s.ApplyJson("""{ "MaxPlayers": 64, "Pvp": "full", "Vault": "server", "AutoSaveMinutes": 5, "VaultByPlayerName": true }""");
        Assert.Equal(64, s.Int("server.login.max-players"));
        Assert.Equal(PvpSetting.Full, s.Pvp);
        Assert.Equal(300, s.ToOptions().ExportInterval);
        Assert.True(s.ToOptions().VaultByPlayerName);
        Assert.Equal(4, s.Int("ruleset.difficulty"));
        s.ApplyOptions(new Dictionary<string, string> { ["pvp"] = "none", ["localvault"] = "true" });
        Assert.Equal(PvpSetting.None, s.Pvp);
        Assert.True(s.LocalVault);
    }

    [Fact]
    public void BadValuesKeepTheDefault()
    {
        var s = ServerSettings.Read("[server]\n\tpvp-mode = \"x\"\n\tone-party-only = maybe\n", null);
        Assert.Equal(PvpSetting.Party, s.Pvp);
        Assert.True(s.Bool("server.one-party-only"));
    }

    [Fact]
    public void TomlReadsTablesQuotedKeysAndStrings()
    {
        var t = Toml.Read("a = 1 # note\n[b.\"c.d\"]\n\te = 'lit'\n\tf = \"x\\u0041\\ty\"\n[[g]]\n\th = 2\n[i]\n\tj = [1, 2]\n");
        Assert.Equal("1", t["a"]);
        Assert.Equal("lit", t["b.c.d.e"]);
        Assert.Equal("xA\ty", t["b.c.d.f"]);
        Assert.False(t.ContainsKey("g.h"));
        Assert.Equal("[1, 2]", t["i.j"]);
    }
}
