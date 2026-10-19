# Notify kinds

Server-to-client engine events sent with `IWorldOutput.Notify(kind, subject, data, player)`.
Each entry: kind, subject, and the JSON fields. Kinds are listed by the package that sends them:

- [combat](notify/combat.md): attacks, impacts and combat feedback
- [creature](notify/creature.md): experience, alignment, highlight, fog and light colours
- [dm](notify/dm.md): the Dungeon Master client
- [effects](notify/effects.md): visual effects, hit points, death and spell presentation timing
- [items](notify/items.md): inventory, equipment and stores
- [player](notify/player.md): messages, journal, NUI, parties and the player list
- [progression](notify/progression.md): character creation and level-up
- [savegame](notify/savegame.md): save, load and the Save/Load window
- [skills](notify/skills.md): action modes, stealth and skill use
- [society](notify/society.md): party membership
- [spells](notify/spells.md): casting, targeting and spell visuals
- [world](notify/world.md): doors, placeables, sound, music, weather and area settings

New kinds go in the file of the package that sends them. Kinds that belong to no package:

| kind | subject | data |
| --- | --- | --- |
| `creature-bump` | displaced creature | `from`, `to`: three-number world positions (Z-up); `facing`: retained radians. Server position changes immediately; the client presents a directional step across one clip cycle. |
| `inventory-denied` | current controlled familiar | `strref`:68708. Closes an attempted inventory window while possessed; the server separately sends the localized feedback message. Does not restrict the character sheet. |
| `quickbar` | current controlled creature | `present`: whether a character/creature layout exists; `slots`: 36 nullable quickbar entries (or null when absent); `authoritative`: saved server character state wins over legacy client layouts; `localCache`: whether the client may read/write its named character cache. A possessed familiar returns `localCache=false`, `authoritative=true`, and an empty present layout when it has no QBList. |

## Quickbar requests and control changes

`quickbar` reads the current controlled creature. `quickbar-set` accepts
`{creature, index, slot}`: `creature` is the controlled object ID used when the edit
was made, `index` is 0-35, and `slot` is an action or null (a deliberate clear).
The server rejects a supplied `creature` that no longer matches control. A client
may omit the field; item ownership validation still applies.

`ControlObject` changes the client session's controlled ID and synchronously
raises `ControlledChanged(uint)`. The quickbar clears its displayed slots and
ability/item/target caches immediately, then requests `quickbar`, `abilities`,
and `spellbook` for the new creature once its object view is available. Replies
for the previous creature cannot alter the new bar. Character disk cache access
waits for the `quickbar.localCache` decision. The quickbar itself is described in
[QUICKBAR_RADIAL.md](QUICKBAR_RADIAL.md).
