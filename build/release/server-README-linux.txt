OpenWinter Server {version} for Linux x64 (early pre-release)
https://github.com/Ambitosis/OpenWinter

The server needs Neverwinter Nights: Enhanced Edition's data from a current,
fully updated install: the data/, lang/ and ovr/ folders (a copy from any
platform works). Beamdog's older standalone dedicated-server downloads are not
recent enough. A Steam install in ~/.local/share/Steam is found by itself;
otherwise create config/local.json next to the server:
  { "gameRoot": "/srv/nwn", "userRoot": "/srv/nwn-user" }

userRoot is where it reads haks, TLKs and modules (hak/, tlk/, modules/), as
in the game's own user folder. File names there may use any case.

Run it as you would nwserver:
  ./openwinter-server -module "<module name>" -userdirectory <folder>

It takes nwserver's flags (-port, -maxclients, -servervault 1, -pvp,
-dmpassword ...) and reads settings.tml and nwnplayer.ini from the user folder,
where it also keeps the server vault and databases, so an NWN:EE server folder
works as it is. --module also takes a path to a .mod file or module folder.
Without -userdirectory it uses userRoot and writes into the userdata folder
next to the server. Settings: docs/SERVER_CONFIG.md in the source repository.
Open UDP port 5121 (or the one you chose) in your firewall.

Players join from Multiplayer with your address (host:5121). Server and players
must run the same OpenWinter version. Missing haks and TLKs download to players
automatically.

OpenWinter is free software under the GNU GPL v3 (LICENSE). Source code:
https://github.com/Ambitosis/OpenWinter. See LEGAL.md for ownership,
trademarks and third-party licences.
