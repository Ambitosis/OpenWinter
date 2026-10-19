# Content delivery: plan

How players get a server's custom content (haks, TLK, override files, music, portraits, movies) in a way that
matches NWSync and goes beyond it. It replaces the current in-band download
(`src/OpenWinter.Game/Distribution/ContentServer.cs`, `src/OpenWinter.Net/ContentSync.cs`), which keeps working as a
fallback.

## Where we are

When a player connects, the game server sends a catalogue of whole files (each hak and the custom TLK, with
size and SHA-1). The client downloads the missing ones over the game connection in 48 KB chunks (about
2.8 MB/s per player) and verifies each file's hash.

Its limits:
- **The game server does the serving.** File traffic competes with gameplay, and the server's bandwidth
  limits how fast new players can join.
- **Whole files.** Changing one texture in a 500 MB hak means 500 MB for every player again. Two servers that
  both use the same CEP hak download it separately, unless the files are byte-identical.
- **No compression.**
- **Haks and TLK only.** Override files, music, portraits, movies and loose content aren't covered.

## Goals

1. **Separate the file service from the game server.** Content is static files on any web host or CDN
   (S3, R2, nginx, GitHub Pages), or a small server we ship. The game server only says where to look.
2. **Content-addressed per resource.** Every resource is stored once under its hash, so downloads for
   different servers never collide, a shared resource is fetched once across all servers, and an update
   downloads only the resources that changed.
3. **Compression** (zstd), including trained dictionaries for the many small GFF and 2DA files.
4. **Everything a module needs:**
   - haks (in their exact order);
   - custom TLK;
   - a server override layer;
   - music (BMU, WAV, OGG), ambient sound and sound sets;
   - portraits, movies and UI or NUI assets;
   - loose files the server declares.
5. **The original's resolution order exactly.** A resource resolves to the same bytes as it would with the
   haks installed, so content works unchanged.
6. **Safe by construction:** signed manifests, hash-verified data, no server-chosen file paths, and limits on
   size and decompression.
7. **Better than NWSync to use:**
   - parallel, resumable downloads;
   - prefetch at character select, with the starting area's content first;
   - a global cache with a size cap;
   - mirrors with failover;
   - clear progress in the UI;
   - tools for server owners.
8. **Interoperable:** read existing NWSync repositories, and optionally publish one from the same source.

## Design

### Repository layout (static, immutable, CDN-friendly)

```
<base-url>/
  latest.json                          small, short cache: { "manifest": "<hash>", "signature": "..." }
  manifests/<ab>/<hash>.manifest       one per published version, immutable
  blobs/<ab>/<cd>/<hash>               one per distinct resource, zstd-compressed, immutable
  dicts/<hash>                         zstd dictionaries the blobs refer to
```

- Everything except `latest.json` is immutable, so it can be cached forever, and mirrors are just copies.
- Hash: SHA-256 of the uncompressed resource. A resource's identity never depends on how it was compressed.

### Manifest

A compact binary file, also published as JSON for debugging:
- **Header:** format version, the module it belongs to, the creation time, and its own hash.
- **Layers, in resolution order:** the server override layer, then haks top to bottom, then the custom TLK.
  Each layer has a name, so a player who already owns `cep2_top_v23.hak` can still satisfy it from their own
  folder by hash.
- **Entries:** resref (16 characters), resource type, layer, uncompressed and compressed size, hash and
  dictionary id.
- **Groups**, for priority ordering:
  - `critical`: everything needed to log in and draw the starting area;
  - `area:<resref>`, so area content can be fetched before entering it;
  - `music`, `movies` and `portraits`, which can be fetched in the background.
- **Signature:** Ed25519 over the manifest hash, made with the server owner's key. The game server's
  Welcome message carries the manifest hash, its URL or URLs, and the public key. The client pins the key
  per server (trust on first use, with a warning if it changes).

### Client

- **One global content store** in the user's OpenWinter folder (`content/blobs/...`), shared by every server.
  It's keyed by hash, so the same texture downloaded for two servers is stored once.
- **Mounting:** the client reads a server's manifest and mounts it as a ResMan layer stack. Resources are
  read straight from the store by hash, with nothing repacked into haks. A hak the player already has
  locally is used when its hash matches.
- **Downloading:**
  - HTTP/2 with 8–16 parallel requests;
  - resume after interruption;
  - retries and mirror failover;
  - each blob is decompressed with a size limit and its hash checked before use.
- **Order:** `critical` first, then the groups for the starting area, then everything else in the background
  while the player is at character select or already playing. If a resource is needed before it has
  arrived, the client asks for it at the front of the queue and waits for it alone. Area transitions wait
  only for that area's group.
- **Cache management:** a size cap with least-recently-used eviction, never evicting blobs a recently played
  server needs. "Clear cache" and "verify cache" are in Options.
- **UI:** a download panel showing bytes per group, speed, time left and pause or resume, with a
  per-server size shown before joining.

### Server side and tools

- **`openwinter-tools content publish <module> --out <dir>`** builds or updates a repository:
  1. Reads module.ifo (haks, custom TLK) and the server's override and extra folders (music, portraits,
     movies).
  2. Hashes every resource and compresses only new blobs (incremental, multi-threaded).
  3. Trains or reuses dictionaries per resource type.
  4. Groups resources by area, using each area's tiles, placeables, creatures, sounds and music.
  5. Writes and signs the manifest, then reports what changed: added, removed and bytes per group.
- **`openwinter-tools content serve <dir> --port 8080`**: a small static HTTP server for owners without a
  host. It is a separate process, so it never shares the game loop.
- **`openwinter-tools content verify <dir|url>`**: checks every blob against its hash and the manifest
  signature.
- **`openwinter-tools content diff <old> <new>`**: what players will download for an update.
- **Server config:** `contentUrl` (one or more mirrors) and the signing key path. On start, the game server
  checks that the manifest matches the content it is actually running, and refuses to advertise a mismatch.
- **Fallback:** without a `contentUrl`, the game server serves the same blobs in-band (the current
  mechanism, upgraded to per-resource blobs with compression), so small servers still work with nothing to
  set up.

### NWSync interoperability

- **Read:** the client can mount an NWSync repository directly. The format is documented by neverwinter.nim
  (MIT), which is credited. An OpenWinter server can advertise an existing NWSync URL, and
  players get the content with nothing re-published.
- **Write:** `content publish --nwsync <dir>` also writes an NWSync repository from the same source, so NWN:EE
  clients and OpenWinter clients are served from one publish step.

## Security

- Downloaded data is identified only by hash. Server-provided names are resrefs, checked against the
  resref rules, and never used as file paths.
- Every blob is verified after decompression. Decompression stops at the size the manifest declared.
- Manifests are signed, and the key is pinned per server. A CDN or mirror can't substitute content.
- Limits: the manifest's total size, per-resource size, and a per-server cache quota the player can see.
- Resource types are allow-listed (no executable types). Scripts arrive only as compiled NCS used by the
  server, never run by the client.

## Phases

| Phase | What | Done when |
|---|---|---|
| 1 | Manifest format, content store, per-resource hashing, ResMan layer from a manifest, `content publish` (no compression yet) | A module plays with its haks served only from the store; resolution matches the hak-installed result for every resource (test against the campaign modules and a large persistent world) |
| 2 | HTTP client (parallel, resume, mirrors), `content serve`, Welcome carries URL and hash, download panel | A player with no haks joins a server whose content is on a separate HTTP host |
| 3 | zstd compression and per-type dictionaries | Repository size and download time measured on a large persistent world's haks against NWSync and uncompressed |
| 4 | Overrides, music, portraits, movies and extra folders; area groups; play while downloading; cache cap and eviction | Joining downloads only the critical and starting-area groups before play; the rest arrives in the background |
| 5 | Signing and key pinning, limits, `verify` and `diff` | A tampered blob or manifest is refused with a clear message |
| 6 | NWSync read and write | The client mounts a live NWSync repository; `--nwsync` output loads in NWN:EE |
| 7 | In-band fallback rebuilt on per-resource blobs | A server with no `contentUrl` still works, compressed and per resource |

Each phase gets unit tests (format round trips, resolution order, corruption and limit cases) and an end-to-end
test: a scratch server, a scratch HTTP host, and an empty client cache.

## Measures of success

- **Per-resource updates:** changing one texture in a 500 MB hak downloads that texture only (a few KB).
- **Shared content:** a second server that uses the same CEP version downloads nothing for it.
- **Size:** the compressed repository is smaller than NWSync's for the same content (target 10–20% smaller
  with dictionaries).
- **Time to play:** a new player is in the starting area after downloading only that area's content plus the
  essentials, not the whole repository.
- **No game-server load:** the game server sends no file bytes when a content URL is set.
