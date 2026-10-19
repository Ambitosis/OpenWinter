# Notify kinds: Dungeon Master package

Sent only to DM clients (except `login-refused`). Requests are in
`src/OpenWinter.Game/DungeonMaster/DMRequests.cs`.

| kind | to | subject | data fields | meaning |
| --- | --- | --- | --- | --- |
| `dm-state` | player | controlled creature | `dm`, `avatar`, `possessed` (id or OBJECT_INVALID), `possessedName`, `hidden` | The DM's avatar, possession and appear/disappear state (on login and every change) |
| `dm-palette` | player | the DM | `kind`, `title`, `tree`: categories `{n, c:[...]}` and blueprints `{n, r, cr, f}` | Answer to `dm-palette` {kind}: the module's custom palette then the standard one |
| `dm-examine` | player | the object | `target`, `kind`, `name`, `lines` (text), `items` [{`id`, `name`, `stack`, `icon`}], `quiet`, `plot`, `immortal`, `aiOff` | Full DM examine (also sent with `quiet` after a DM action changes the object) |
| `dm-limbo` | player | the DM | `creatures` [{`id`, `name`}] | Creatures in limbo |
| `dm-players` | player | the DM | `players` [{`id`, `name`, `player`, `area`, `dm`, `muted`}] | Connected players |
| `login-refused` | (raw notify before login) | none | `reason` | DM login with a wrong password; the connection is closed |

Client requests (refused for non-DMs): `dm-palette` {kind}, `dm-spawn` {kind, resref, pos, facing, into?},
`dm-action` {action, target?, amount?, value?, minute?, pos?, item?, resref?, reason?} with actions
`heal kill invulnerable immortal ai rest limbo from-limbo lock destroy goto summon jump-point jump-all
give-xp give-level give-gold give-item take-item vfx possess unpossess appear disappear mute unmute boot
set-time set-weather`, `dm-examine` {target}, `dm-limbo`, `dm-players`.

Chat: `ChatChannel.Dm` is TALKVOLUME_SILENT_SHOUT in OnPlayerChat and reaches every DM plus the sender.
