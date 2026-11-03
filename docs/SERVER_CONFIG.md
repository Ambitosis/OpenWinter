# Dedicated server configuration

`openwinter-server` is configured the way NWN:EE's `nwserver` is, so an existing server folder and start command
work unchanged. Everything here was measured on nwserver 8193.37 (parity suite `server_settings` and logins with
`openwinter-tools nwn-probe` over NWNX_WSNetLayer).

## Where settings come from

Per setting, the last of these that has it wins:

1. nwserver's defaults (below).
2. `nwnplayer.ini` in the user folder, `[Server Options]` and `[Game Options]` (the older labels: `Max Players`,
   `PVP Setting`, `Server Vault By Player Name`, `AllowLocalChars`, ...). nwserver only uses these for keys that
   settings.tml doesn't have.
3. `settings.tml` in the user folder (`[server]`, `[ruleset]` ...).
4. OpenWinter's `server.json` (`--config <file>`, default `./server.json`), only the properties it names. Older
   OpenWinter servers wrote this file with every property; delete it to use settings.tml alone.
5. The command line. nwserver doesn't write flags back into settings.tml, and neither does OpenWinter.

The user folder is `-userdirectory <dir>`. Without it the server reads the game's own user folder (Documents/Neverwinter
Nights, `~/.local/share/Neverwinter Nights` on Linux) and, in a source checkout, writes into the sandbox
(`out/sandbox-userdata`) unless `config/local.json` sets `"liveUserData": true`.

## Command line

| nwserver flag | Setting |
|---|---|
| `-module <name>` | module `<name>.mod` in the user folder's `modules`, or the game's `data/nwm`, `data/mod` (OpenWinter also takes a path) |
| `-userdirectory <dir>` | the user folder |
| `-port <n>` | `server.net.port` (5121) |
| `-maxclients <n>` | `server.login.max-players` (6) |
| `-servername <text>` | `server.identity.name` ("Server") |
| `-pvp 0\|1\|2` | `server.pvp-mode` (1: party) |
| `-difficulty 1-4` | `ruleset.difficulty` (1) |
| `-servervault 0\|1` | `server.vault.mode`: 1 keeps characters on the server, 0 takes local characters |
| `-elc 0\|1`, `-ilr 0\|1` | `ruleset.enforce-legal-characters` (on), `ruleset.item-level-restrictions` (off) |
| `-oneparty 0\|1` | `server.one-party-only` (on) |
| `-pauseandplay 0\|1` | `server.allow-player-pausing` (off) |
| `-minlevel`, `-maxlevel` | `ruleset.min-character-level` (1), `ruleset.max-character-level` (40) |
| `-playerpassword`, `-dmpassword`, `-adminpassword` | `server.login.*-password` |
| `-reloadwhenempty 0\|1` | `server.reload-when-empty` (on): the module loads again when the last player leaves |
| `-autosaveinterval <minutes>` | `server.autosave`: saves the game to the autosave slot on a timer (0: off, the default) |
| `-gametype`, `-publicserver`, `-nwsyncurl`, `-nwsynchash`, `-load`, `-interactive`, `-quiet` | accepted; not used |

Anything else is answered `Server: <arg> not understood`, as nwserver does. OpenWinter's own options still work:
`--module <path>`, `--area <resref>` (start area, for testing), `--config <file>`, and the older `--port`,
`--maxplayers`, `--pvp none|party|full`, `--difficulty`, `--localvault`, `--dmpassword`, `--password`,
`--pauseandplay`, `--oneparty 0|1`.

## The server vault

- `server.vault.mode = 1` (the default) lets players bring local characters; `0` or `-servervault 1` keeps them in
  the server vault.
- Vault folders are named by the player's public key: `servervault/<key>/`. With
  `server.vault.by-player-name = true` they are named by player name instead, and then any player using that name
  gets that folder.
- `server.vault.sticky-player-names = true` ties a player name to the first key that logs in with it: the server
  records the pair in the user folder's `override/knownservernames.2da` (columns `CDKey`, `ConnectType`,
  `PlayerName`) and refuses the name under another key, whatever its case. One key may hold several names.
  NWNX's `NWNX_ON_CHECK_STICKY_PLAYER_NAME_RESERVED` runs only when this is on.

## Not matched yet

- `server.vault.backup-server-characters` and `suppress-base` are stored but do nothing (no backup file appeared on
  nwserver either in the measured logins).
- OpenWinter's own client sends an installation key, not the player's NWN CD key, so a vault folder named by an
  NWN:EE public CD key is not found for that player; set `by-player-name` or rename the folder.
- Sticky names for Dungeon Masters are not measured; DMs are not checked.
- The server doesn't write settings.tml (nwserver rewrites it at start, adding the keys it read from the ini).
- `ruleset.natural-ones-fail-saving-throws`, `validate-spells`, `show-active-effects`, `show-challenge-ratings`,
  `restore-spell-uses-on-login`, `encounters.always-reset`, `hide-hitpoints-gained-on-levelup` are passed to the
  play options NWNX_Administration reads but don't yet change the rules.
