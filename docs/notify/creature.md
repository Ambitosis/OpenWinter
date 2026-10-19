# Notify kinds: creature package

Sent with `world.Output?.Notify(kind, subject, data, player)`. Appearance setters (appearance,
phenotype, body parts, colours, wings, tails, portrait, sound set, footsteps, gender, action
modes) don't add kinds: they call `world.MarkChanged(creature)` so the object is re-sent.
Combat-log feedback (saving throws, skill checks, XP gained, alignment shifts) goes through
`IWorldOutput.SendMessage`.

| kind | subject | data fields | meaning |
| --- | --- | --- | --- |
| `xp-changed` | the player's creature (sent to that player only) | `xp`, `level`, `nextLevelXp` (-1 at the level cap), `canLevelUp`, `levelLost` | Experience changed (GiveXPToCreature/SetXP). `canLevelUp` lights the level-up button; `levelLost` means SetXP removed levels. |
| `alignment-changed` | the player's creature (sent to that player only) | `lawChaos`, `goodEvil` (0-100) | AdjustAlignment moved the creature into a different alignment band (e.g. neutral to good). |
| `hilite-color` | any object | `color` (0xRRGGBB, -1 clears) | SetObjectHiliteColor: mouse-over highlight colour override. |
| `fog-color` | area | `fogType` (FOG_TYPE_ALL/SUN/MOON), `color` (0xRRGGBB), `fadeTime` (seconds) | SetFogColor. |
| `area-light-color` | area | `colorType` (AREA_LIGHT_COLOR_*: 0 moon ambient, 1 moon diffuse, 2 sun ambient, 3 sun diffuse), `color` (0xRRGGBB), `fadeTime` | SetAreaLightColor. |
| `tile-light` | area | `x`, `y` (tile grid), `mainLight1`, `mainLight2` (TILE_MAIN_LIGHT_COLOR_*), `sourceLight1`, `sourceLight2` (TILE_SOURCE_LIGHT_COLOR_*) | SetTileMainLightColor / SetTileSourceLightColor; the client re-lights that tile. |
