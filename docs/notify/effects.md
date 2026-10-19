# Notify kinds: effects package

Sent by `Rules/EffectSystem.cs` and `Scripting/Functions/EffectFunctions.cs`. Vectors are `[x, y, z]` arrays.

| kind | subject | data fields | meaning |
| --- | --- | --- | --- |
| `effect-applied` | object the effect is on | `id` (long), `type` (EFFECT_TYPE_*), `durationType`, `duration` (s, 0 = permanent), `spellId`, `creator`, `linkId`, `icon` (EffectIcon id, else 0), `subType` | A temporary/permanent effect landed. Clients show effect icons (effecticons.2da) from `type`/`icon`. |
| `effect-removed` | object the effect was on | `id`, `type`, `linkId`, `icon` | The effect expired, was removed or dispelled. Drop its icon and any looping visual with the same `id`. |
| `vfx` | target object, or the area for a location | `vfx` (visualeffects.2da row), `miss`, `scale`, `translate`, `rotate`, `duration` (0 = one-shot, -1 = until removed, else seconds), `id` (effect id, 0 for one-shots); location form adds `pos`, `facing` | Play a visual effect on an object or at a point. Looping ones end with the matching `effect-removed`. |
| `beam` | target object | `vfx`, `from` (source object id), `bodyPart` (BODY_NODE_*), `miss`, `scale`, `translate`, `rotate`, `duration`, `id` | Draw a beam from `from` to the subject. |
| `aoe-created` | area | `id` (AoE object id), `aoe` (vfx_persistent.2da row), `shape` ("C"/"R"), `radius`, `width`, `length`, `pos`, `facing`, `attachedTo` (object it follows, or OBJECT_INVALID) | A persistent area of effect appeared; draw its vfx_persistent models. |
| `aoe-removed` | area | `id` | The area of effect ended. |
| `effect-icon-flash` | the player creature (sent only to that player) | `icon`, `flashing` (bool) | SetEffectIconFlashing. |
| `disappear` | creature | `animation` | EffectDisappear / EffectDisappearAppear: play the leave animation and hide. |
| `appear` | creature | `animation` | EffectAppear, the end of a temporary disappear, or a summon arriving with its appear animation. |

## Event context keys set by this package

| event | keys |
| --- | --- |
| RunScript effect scripts (OBJECT_SELF = object the effect is on) | `RunScriptEffect` (Effect), `RunScriptEffectScriptType` (RUNSCRIPT_EFFECT_SCRIPT_TYPE_*) — for GetLastRunScriptEffect / ...ScriptType |
| AoE `OnObjEnter` / `OnObjExit` (fields on `AreaOfEffect.Scripts`, plus `OnHeartbeat`, `OnUserDefined`) | `Entering` / `Exiting` |
| creature `ScriptDamaged`, placeable/door `OnDamaged` | `Damager` (object), `TotalDamage`, `Damage:<DAMAGE_TYPE_* bit>`, `DamagePower` |
| creature `ScriptDeath`, placeable/door `OnDeath` | `Killer` |
| module `Mod_OnPlrDying` | `PlayerDying`, `Killer` |
| module `Mod_OnPlrDeath` | `PlayerDied`, `Killer` |

Effect constructors read `SpellId` and `CasterLevel` from the current event context when present.

Object and location `vfx` payloads include `creator` (the effect creator's object ID).
The client uses it for `progfx.2da` type 10 projectiles: each script-applied visual launches
one missile from that creator to the target. A missing or unavailable creator cannot be
reconstructed from the target; those projectiles are skipped. Ordinary target/location models
still play.

`spell-conjure-timing` carries `spell`, `presentation`, and `remaining` seconds
when live Haste changes a still-running ordinary preparation phase. It updates
the active action deadline without restarting its clip, effects, or sound.
It does not retime release or recovery, or reconstruct expired actions.

Timed ordinary spell presentation messages carry a `presentation` identifier.
`spell-release` carries the same identifier and `spell` at the server's release
moment; the client then plays the matching release effects, sounds and table projectiles once.
Payloads without an identifier fall back to the client's own delay. This message does not
change rules, the conjure sound's lifetime, or the recovery animation's lifetime.
