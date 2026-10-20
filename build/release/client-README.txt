OpenWinter {version} (early pre-release)
An open-source engine for Neverwinter Nights: Enhanced Edition.
https://github.com/Ambitosis/OpenWinter

You need Neverwinter Nights: Enhanced Edition installed (Steam, GOG or Beamdog)
and Windows 10/11 64-bit. OpenWinter contains no game data.

1. Run OpenWinter.exe. It finds your NWN:EE installation by itself.
2. Choose New Game, pick a module, then pick or create a character.

The classic interface (the original game's look) is the default. The modern
interface is under Options > Interface.

OpenWinter never writes to your Neverwinter Nights folder. Its settings, saves
and new characters are kept in %APPDATA%\Godot\app_userdata\OpenWinter.

If the game isn't found, create config\local.json next to OpenWinter.exe:
  { "gameRoot": "D:/Games/Neverwinter Nights" }

To join a server: Multiplayer, enter host:port, Connect. Server and players
must run the same OpenWinter version.

Report bugs at https://github.com/Ambitosis/OpenWinter/issues and attach
%APPDATA%\Godot\app_userdata\OpenWinter\logs\godot.log.

OpenWinter is free software under the GNU GPL v3 (LICENSE). Source code:
https://github.com/Ambitosis/OpenWinter. See LEGAL.md for ownership,
trademarks and third-party licences.
