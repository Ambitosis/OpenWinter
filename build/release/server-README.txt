OpenWinter Server {version} (early pre-release)
https://github.com/Ambitosis/OpenWinter

The server needs Neverwinter Nights: Enhanced Edition's data. It finds a local
installation (Steam, GOG or Beamdog) by itself; otherwise create
config\local.json next to it:
  { "gameRoot": "D:/Games/Neverwinter Nights" }

Run:
  openwinter-server.exe --module "<path to a .mod file or module folder>"

Options: --port 5121, --pvp none|party|full, --dmpassword <pw>, --config <file>.
The first run writes server.json with all settings; edit it for the server
name, PvP, difficulty, vault mode and more. The server vault and databases are
kept in the userdata folder next to the server.

Players join from Multiplayer with your address (host:5121). Server and players
must run the same OpenWinter version. Missing haks and TLKs download to players
automatically.

OpenWinter is free software under the GNU GPL v3 (LICENSE). Source code:
https://github.com/Ambitosis/OpenWinter. See LEGAL.md for ownership,
trademarks and third-party licences.
