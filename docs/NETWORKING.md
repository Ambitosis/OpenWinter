# Multiplayer compatibility

OpenWinter clients connect to OpenWinter dedicated servers over LiteNetLib UDP (default port 5121). Single player
uses an in-process loopback connection. This is OpenWinter's own protocol. The client can also join NWN:EE servers
that run the NWNX_WSNetLayer plugin ([below](#joining-nwnee-servers-wsnetlayer)); the original UDP protocol is not
implemented.

## Version check

Remote connections require an exact match of:

- `NetworkVersion.CurrentProtocol`: the gameplay wire protocol revision (currently 3).
- The engine version from the OpenWinter.Net assembly's build metadata.
- A SHA-256 fingerprint of the engine's runtime source and build files.

The dedicated server prints its version and shortened fingerprint at startup. A rejected client's error screen
shows its version and the server's version, with a button to return to the main menu. Install the same build on
both machines and reconnect. There is no automatic updater or switch to bypass the check.

`build/NetworkBuild.targets` generates the fingerprint into OpenWinter.Net, shared by the dedicated server and
Godot client. It includes C# source and project files from Aurora, Content, Game, Net, Server and the client,
plus the root build properties/targets, network build target and Godot project settings. Changes to local,
uncommitted runtime source count. Tests, tools, documentation, game data and generated build outputs do not.
Text line endings and relative path separators are normalized, and paths are sorted ordinally. Identical source
has the same identity across checkout locations and Debug/Release builds; timestamps and commit IDs do not
affect it. Rebuild and distribute the complete client/server output together; mixing DLLs from different builds
is unsupported. The fingerprint identifies source compatibility, not executable authenticity.

## Handshake ordering

The connection request carries a small, bounded envelope (`openwinter/handshake/1`) containing the three version
fields. The server rejects missing, malformed or mismatched identities through LiteNetLib's rejection payload,
before raising `NetServerHost.Connected` or creating a GameServer session. The rejection includes the server
identity, independent of gameplay message serialization.

After acceptance, the server sends its identity as its first reliable ordered packet. The client validates it
before sending any queued gameplay messages, including Hello. Gameplay bytes are never parsed as such until
the handshake succeeds. Handshake failure clears queued input and stops the connection; a handshake that never
completes times out. Leaving the error screen disposes the transport and returns to the menu in either UI style.

The earlier `openwinter/1` transport cannot negotiate versions: servers reject it, and clients explain a rejection
by such a server. Both endpoints must run the same build.

Keep the envelope stable when changing gameplay messages; increment `CurrentProtocol` for incompatible wire
changes. The generated source fingerprint also catches changes when a protocol bump is accidentally omitted.
Allowing different builds of a compatible protocol would be an explicit future relaxation of this policy.

## Testing

`NetworkVersionTests` uses real localhost UDP sockets: matching peers, queued login, protocol/build/engine
mismatches, malformed and legacy handshakes, client-side verification, cancellation, and two players entering
the same world. The latter needs configured game data; the transport cases do not.

Two players on a real dedicated server: `python tools/multiplayer/mp.py run mp_sync` ([MULTIPLAYER_HARNESS.md](MULTIPLAYER_HARNESS.md))
records what each client shows of the other and of the world and compares it with the server.

For UI verification, run the client with `--connect host:port --capture-connection-failure <directory>` against
a mismatched test server. It captures the actual failure, returns to the menu and captures the menu; add
`--ui classic` to verify that style. Launch it through `tools/godot.sh` as usual.

## Module presentation resources

Single-player enables the same server content catalogue as remote play. Alongside haks
and custom TLKs, the server announces `ContentKind.ModuleResources` (protocol 3): a
hash-verified ERF containing only supported client resource types from the module.
The client does not open the `.mod` or read area state, scripts, dialogs, or templates.
Models, textures, tilesets/walkmeshes, UI/font assets, soundsets/audio, and 2DAs are
allowlisted in `ModuleClientResources.IsClientResource`; unknown types remain excluded.
Runtime sources and development overrides are not implicitly exported.

Packages are built deterministically in `PersistenceSystem.UserRoot/cache/content`
(the configured sandbox write root in development). Clients download to their existing
Godot content cache. Lookup is announced haks, module presentation package, then base
resources. An unchanged package reuses its hash; changing module resources creates a
new package. Dedicated servers with `ServeContent=false` still deliberately disable
all automatic content distribution. With content serving on, the module's own 2DAs are
shipped to every client (they are needed for presentation), so their contents are not private.

Single player also runs with `ServeContent = true`, so every launch re-hashes the module's haks and custom TLK,
rebuilds and hashes the presentation package, and the first launch of a module version streams the package over
the loopback connection. The in-process client could load the server's package (or the module's resources)
directly instead.

A new Welcome interrupts an old download, disposes incomplete files, discards held and pending old-module packets,
and replaces scene and audio resources. The `ModuleCleared` event closes modern, NUI and dialog windows and pending
targeting without sending old close callbacks into the new server world. Classic panels detach and rebuild after
the next area loads. Verified installation raises `ClientSession.ContentChanged`. Retired resource sources are
disposed after one frame, once old widgets and scene nodes finish deferred removal; session exit disposes any that
remain. This supports switching modules on the server (`StartNewModule`) but does not implement it.

Testing: `ModuleClientResourcesTests` and `ContentSyncTests` cover loose and archive modules, lookup precedence,
server-only exclusions, deterministic hashes and cache reuse, and interrupted transfers. The headless client probe
`tools/godot.sh --headless -- --verify-module-content <directory>` checks the client session's Welcome, download,
area and audio replacement, including cutscene, pause and rest recovery when a replacement Welcome discards queued
old-module notices. The visual scenes `modcontenta` and `modcontentb` show a module-only model changing colour
under the same resource name, without haks, on both engines (`parity/visual/scenes/modcontenta/README.md`); material
brightness still differs.

## Joining NWN:EE servers (WSNetLayer)

[NWNX_WSNetLayer](https://github.com/niv/NWNX_WSNetLayer) is an NWNX plugin that exposes NWN:EE's message layer over
websockets. A server that loads it can be joined from the OpenWinter client:

    tools/godot.sh -- --connect-nwn ws://host:port --name <player name> [--character <server-vault resref>]

From the main menu, Multiplayer joins one too: tick "NWN:EE server (WSNetLayer)" or type a `ws://` or `wss://`
address. Without `--character` the server's character list is shown. The server needs nothing beyond the plugin:
no scripts, settings or other plugins.

- Use `wss://` when the server puts the plugin behind a TLS proxy, as its README asks.
- The client logs in with the CD key from the player's `cdkey.ini` (read only). Only the public part and an MD5
  answer to the server's challenge are sent. `--nwn-key generate` makes a throwaway key for test servers that can't
  reach the master server.
- Only NWN:EE 8193.37.17 is supported, the version the plugin and its message definitions cover.

`src/OpenWinter.Net.Nwn` has three parts:

- **Codec.** It reads and writes the plugin's encoding, which puts a type byte before every field. Most of it is
  generated by `tools/nwnproto` from the message definitions in
  [nwn-message.py](https://github.com/niv/nwn-message.py) (GPL-3.0); the types that library codes by hand are written
  by hand in `Messages/Custom`. Where recorded traffic differs from those definitions, the generator corrects them
  (`apply_corrections`): NUI windows, binds, events and layouts carry JSON fields (type byte, length, text) rather
  than length-prefixed blobs, and a NUI bind entry ends after its value; a body bag names its creature by object id.
- **Session.** It handles login, keep-alives and echoes.
- **Bridge.** `NwnBridgeConnection` is an `IConnection` that turns NWN messages into the OpenWinter messages the client
  already understands, and the client's requests into NWN input. NWN sends a walking creature's path once; the bridge
  moves the creature along it at its own rate.

Payloads that both the OpenWinter server and the bridge send are records in `OpenWinter.Net/Notices.cs`.

Supported so far:

- login and choosing a server-vault character
- the area and its static objects
- creatures with appearance and equipment, placeables, doors (open or closed) and ground items
- chat in both directions, explored map tiles, and the server's feedback lines (items and gold gained or lost,
  locked doors, script messages)
- click-to-move walking, keyboard driving and walking towards a held mouse button; other creatures' walks
- opening and closing doors, using placeables and picking up ground items
- script-built NUI windows: opening, closing, binds and group layouts, with clicks and edited values sent back
- the player's items (`NwnInventory`, `NwnItems`): the backpack, bags and equipment with icons, stacks, charges,
  identified state, names, weight and gold; equipping, unequipping, dropping, splitting, using and moving items, into
  and out of bags; examining items; placeable containers and body bags (open, take, take all, put, close); stores
  (every category, buying, selling, identifying, the store's prices and gold); barter between players (asking starts
  it for both at once, as the server does; offers of items and gold, locking, accepting, cancelling)

The server shows the client only the panels an NWN client has open: the equipment and the shown backpack page while
the inventory is open, one container or bag at a time, and a store's shown category and page. OpenWinter's windows
list everything, so the bridge keeps the inventory open and shows the server each backpack page in turn, then each
bag (only while no container is open, since opening a bag closes it), and each store category and page, and looks
again after every change the server confirms. A page's answer comes with the server's next update, so the pages are
shown one at a time. Item tooltips (category, damage, armour, properties, size, slots) come from the item's
appearance and the client's 2DAs.

Dialog, combat feedback, NWSync and character creation are still to come.

Testing:

- `NwnCodecConformanceTests` decodes frames encoded by nwn-message.py and re-encodes them to the same bytes.
- `NwnSessionTests` covers the login against a scripted server; `NwnBridgeTests` covers the translation.
- `tools/nwnws/nwnws.py` runs a sandboxed Linux nwserver with NWNX and the plugin in WSL. It can't open outbound
  connections, so it never contacts the master server.
- `openwinter-tools nwn-probe`, `nwn-dump` and `nwn-bridge` record and inspect sessions against it. `nwn-bridge`
  can also say a line, use objects, drive with the keyboard, click NUI elements and record (`--say`, `--use`,
  `--drive`, `--nui-click`, `--record`), and run a script of client requests (`--do "use:Chest;req:container-take
  {\"item\":\"@c:Arrow\"}"`, items named from the last inventory, container or store contents). `nwn-dump` reads
  2DAs with the haks the recorded server named, since some fields' presence depends on them.
- `parity/suites/bridge_inventory` builds a test module with a chest, a body bag and a store opened by using a
  barrel; the entering player is given weapons, armour, stacks, a wand, an unidentified ring, a bag and gold. The
  client's `--capture-nwn-inventory dir` opens the inventory, equips a weapon, loots the chest, buys, sells and
  examines, with a shot after each step.
