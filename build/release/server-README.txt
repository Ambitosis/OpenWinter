OpenWinter Server {version} (early pre-release)
https://github.com/Ambitosis/OpenWinter

The server needs Neverwinter Nights: Enhanced Edition's data. It finds a local
installation (Steam, GOG or Beamdog) by itself; otherwise create
config\local.json next to it:
  { "gameRoot": "D:/Games/Neverwinter Nights" }

Run it as you would nwserver:
  openwinter-server.exe -module "<module name>" -userdirectory <folder>

It takes nwserver's flags (-port, -maxclients, -servervault 1, -pvp,
-dmpassword ...) and reads settings.tml and nwnplayer.ini from the user folder,
where it also keeps the server vault and databases, so an NWN:EE server folder
works as it is. --module also takes a path to a .mod file or module folder.
Without -userdirectory it reads the game's user folder and writes into the
userdata folder next to the server. Settings: docs/SERVER_CONFIG.md in the
source repository.

Players join from Multiplayer with your address (host:5121). Server and players
must run the same OpenWinter version. Missing haks and TLKs download to players
automatically.

OpenWinter is free software under the GNU GPL v3 (LICENSE). Source code:
https://github.com/Ambitosis/OpenWinter. See LEGAL.md for ownership,
trademarks and third-party licences.
