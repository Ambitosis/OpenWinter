# Notify kinds: world package

Doors, placeables, traps, transitions, encounters, sound, music, weather, area
settings, tiles and object visual overrides. Sent with
`IWorldOutput.Notify(kind, subject, data, player)`. Unless "to player" is noted,
the message goes to everyone in the subject's area (everyone, when the subject is
null or the module). Vectors are `[x, y, z]` arrays.

Door and placeable animations are not notify kinds: they go through
`IWorldOutput.PlayAnimation` with `ANIMATION_DOOR_OPEN1/OPEN2/CLOSE` (205/206/204)
and `ANIMATION_PLACEABLE_OPEN/CLOSE` (202/203); the open and locked states reach
clients in the object's update flags (`ObjectFlags.Open`, `ObjectFlags.Locked`).
Sitting uses `PlayAnimation(ANIMATION_LOOPING_SIT_CHAIR)`.

When a player enters an area, `AreaStateSync` re-sends the current state of the
area-wide kinds below (audio, weather, fog, skybox, wind, light directions set by
script, grass, the player's explored tiles) and of object overrides
(visual transforms, texture/animation replacements, disabled map pins, traps the player
has detected) to that player.

On the client, `client/Views/WorldFx/WorldFxPlayer.cs` shows these kinds (and `beam`,
`fog-color`, `area-light-color`, `tile-light`, `hilite-color` from the other packages);
`--capture-fx <dir>` screenshots each of them using `tests/scripts/fx_*.nss`.

| kind | subject | data fields | meaning |
| --- | --- | --- | --- |
| `object-use` | the creature using a container | `target` (object id), `animation` (model clip name, currently `getmid`) | One short reach after a successful container click. Scripted use and `/loot` do not emit this. The lid uses its open-state transition independently. |
| `play-sound` | the object playing it | `resref`, `position`, `strref` (-1 unless PlaySoundByStrRef) | One-shot mono sound at the object (PlaySound, PlaySoundByStrRef). |
| `sound-object` | sound object | `action` (`play`/`stop`/`volume`/`position`), `active`, `volume` (0-127), `position`, `sounds` (resrefs) | A placed sound object changed. |
| `area-audio` | area | `musicDay`, `musicNight`, `musicBattle` (ambientmusic.2da rows), `musicDelay` (ms), `musicPlaying`, `battlePlaying`, `ambientDay`, `ambientNight` (ambientsound.2da rows), `ambientDayVolume`, `ambientNightVolume` (the area's 0-127 scale; the setters allow 0-100), `ambientPlaying` | Background/battle music or ambient sound changed (Music*/AmbientSound*). |
| `battle-music` | the player | `on` | This player's battle music starts (a creature attacked her while she is not plot) or ends (a while after the fight). See [AUDIO.md](../AUDIO.md). |
| `weather` | area | `weather` (WEATHER_CLEAR 0, RAIN 1, SNOW 2), `lightning` (the area's ChanceLightning, percent) | The area's weather changed (SetWeather, or the periodic roll from ChanceRain/ChanceSnow). |
| `area-fog` | area | `sun`, `moon` (0-100) | Fog amounts changed (SetFogAmount). |
| `area-skybox` | area | `skybox` (skyboxes.2da row) | SetSkyBox. |
| `area-light-direction` | area | `type` (AREA_LIGHT_DIRECTION_MOON 0 / SUN 1), `direction`, `fade` (seconds) | SetAreaLightDirection. |
| `area-wind` | area | `direction`, `magnitude`, `yaw`, `pitch` | SetAreaWind. |
| `area-recompute-lighting` | area | `placeables`: `[{id, on}]` illumination of every placeable | RecomputeStaticLighting (applies buffered SetPlaceableIllumination changes). |
| `area-tiles` | area | `tileset`, `flags` (SETTILE_FLAG_*), `tiles`: `[{index, id, orientation, height, animLoop1, animLoop2, animLoop3}]` | Tiles changed (SetTile, SetTileJson, SetTileAnimationLoops). The server walkmesh is rebuilt. |
| `area-reload` | area | `what` (`grass` / `border`) | ReloadAreaGrass / ReloadAreaBorder. |
| `area-grass` | area | either `material`, `texture`, `density`, `height`, `ambient`, `diffuse`; or `material`, `removed: true`; or `defaultDisabled` | Grass overrides (SetAreaGrassOverride, RemoveAreaGrassOverride, SetAreaDefaultGrassDisabled). |
| `map-pin` | waypoint | `enabled`, `note`, `position` | SetMapPinEnabled. |
| `map-explored` | area | `area` (id), `explored` | ExploreAreaForPlayer, to player. |
| `tile-explored` | area | `area` (id), `x`, `y`, `explored` | SetTileExplored, to player. |
| `area-transition-bmp` | the player | `predefined` (AREA_TRANSITION_*), `custom` (bitmap resref) | SetAreaTransitionBMP, to player. |
| `day-night` | the player | `night` (bool), `transition` (seconds) | DayToNight / NightToDay, to player. |
| `camera-lock` | the player | `which` (`pitch`/`distance`/`direction`), `locked` | LockCamera*, to player. |
| `trap-triggered` | trapped object | `victim` (object id), `type` (TRAP_BASE_TYPE_*) | A trap went off. |
| `trap-detected` | trapped object | `detected`, `type`, `flagged`, `geometry` (trap triggers: their polygon `[[x, y, z], ...]`, since clients don't get triggers; else null) | The player detected (or lost) a trap, to player. |
| `trap-disarmed` | trapped object | `by` (object id or OBJECT_INVALID) | A trap was disarmed or disabled. |
| `visual-transform` | creature/placeable/item/door | `transform` (OBJECT_VISUAL_TRANSFORM_*), `scope`, `from`, `value`, `lerp` (LERP_*), `duration`, `pauseWithGame`, `behavior`, `repeats`; when re-sent on area entry also `elapsed` (seconds the lerp has run) | SetObjectVisualTransform: animate from `from` to `value`. |
| `visual-transform-clear` | object | `scope` (-1 = all) | ClearObjectVisualTransform. |
| `object-texture` | object | `old`, `new` ("" restores) | ReplaceObjectTexture. |
| `object-animation` | object | `old`, `new` ("" restores) | ReplaceObjectAnimation. |
| `texture-override` | the player, or none (all players) | `old`, `new` ("" clears) | SetTextureOverride. |
| `material-uniform` | object | `material`, `param`, `type` (`int`/`vec4`), `value` (array) | SetMaterialShaderUniformInt/Vec4. |
| `material-uniform-reset` | object | `material`, `param` ("" = all) | ResetMaterialShaderUniforms. |
| `shader-uniform` | the player | `shader` (SHADER_UNIFORM_*), `type` (`float`/`int`/`vec4`), `value` (array) | SetShaderUniform*, to player. |

## Event data keys

Set by this package (read with the usual getters): `OpenedBy`, `ClosedBy`, `LockedBy`,
`UnlockedBy`, `UsedBy`, `Clicking` (door/trigger OnClick, OnFailToOpen, placeable OnClick),
`Entering`/`Exiting` (encounters; trap scripts get `Entering` = the victim),
`BlockingDoor` (creature OnBlocked), `Disarmer` (OnDisarm).

Ground `tile-action` requests raise the module's `Mod_OnPlrTileAct` event with
`TileActionId` (int), `TileActionPosition` (Vector3), `TileActionPlayer` (object id),
and `EventScriptType`3020. The corresponding `tile-actions` query returns the
module's surface-defined names/icons/IDs to the requesting player only. Both
requests validate current area and ground position; activation rechecks the
configured action.
