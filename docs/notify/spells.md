# Notify kinds: spells package

Sent by `Spells/CastSpellAction.cs`, `Spells/SpellSystem.cs` and `Spells/RestAction.cs`. Vectors are `[x, y, z]`
arrays; visual and sound columns are spells.2da resrefs (`""` when the column is `****`). Casting and rest animations
also go out as ordinary `PlayAnimation` messages (ANIMATION_LOOPING_CONJURE1/2 for the conjure, CAST* for the cast,
DRINK/READ for potions and scrolls, SIT_CROSS while resting), so a client can ignore the `animation` field.

| kind | subject | data fields | meaning |
| --- | --- | --- | --- |
| `spell-conjure` | caster | `spell` (spells.2da row), `target` (object id or OBJECT_INVALID), `pos` (aim point), `duration` (s, ConjTime), `animation` (ConjAnim: "hand"/"head"), `headVfx`, `handVfx`, `groundVfx` (ConjHeadVisual/ConjHandVisual/ConjGrndVisual models), `sound` (ConjSoundVFX), `voice` (ConjSoundMale/Female for the caster's gender; "" when Silent Spell), `metaMagic`, `fake` | The caster starts conjuring. Attach the conjure models to the head/hand nodes / ground for `duration` and play the chant. Not sent for items, quickened or instant spells. |
| `spell-cast` | caster | `spell`, `target`, `pos`, `duration` (s, CastTime), `animation` (CastAnim: out/self/up/area/touch), `headVfx`, `handVfx`, `groundVfx` (Cast*Visual), `sound` (CastSound), `item` (item used, or OBJECT_INVALID), `fake` | Conjuring finished and the spell is released. Stop conjure effects, but let the chant finish its full audio stream. |
| `spell-projectile` | caster | `spell`, `target`, `from`, `to`, `model` (ProjModel), `path` ("homing", "ballistic", "highballistic", "accelerating", "linked", ... from ProjType or the script's PROJECTILE_PATH_TYPE_*), `spawnPoint` (ProjSpwnPoint node: hand, head, monster0...), `sound` (ProjSound), `orientation` (ProjOrientation), `delay` (s until launch), `travel` (s in flight) | A spell with `Proj` = 1 fires a missile; the impact script runs when it lands (`delay` + `travel`). |
| `spell-interrupted` | caster | `spell`, `reason` ("concentration", "defensive-casting", "failure" (arcane/effect spell failure), "countered", "interrupted" (disabled), "cancelled") | The spell was lost (or cancelled before release). Stop conjure effects but let the chant finish. For `concentration`, also play the installed `sff_spellfail` sound over the chant. A defensive-casting failure occurs after conjuring, consumes the slot and does not release the spell. |
| `spell-countered` | caster whose spell was countered | `spell`, `counter` (spell the counterspeller used), `by` (counterspeller id) | ActionCounterSpell stopped a spell. The counterspeller also gets a `castout` animation. |
| `spell-targeting` | the player (sent to that player only) | `spell`, `shape` (SPELL_TARGETING_SHAPE_*), `sizeX`, `sizeY`, `flags` (SPELL_TARGETING_FLAGS_*) | SetSpellTargetingData: use this shape instead of spells.2da TargetShape/TargetSizeX/Y/TargetFlags for the rest of the session. |
| `rest` | resting creature | `state` ("started", "finished", "cancelled"), `duration` (s, for "started"), `nwnxAnimation` (engine animation id set with NWNX_Player_SetRestAnimation, else -1; "started" only) | Resting began (show the rest bar for `duration`), completed, or was interrupted. |

## Client requests

| kind | data | meaning |
| --- | --- | --- |
| `cast-spell` | `spell`, `target` (object id) or `pos` ([x,y,z] in the player's area), optional `class` (-1 any), `meta` (METAMAGIC_*, 255 any), `domain` (domain level), `spontaneous` (bool) | Cast from the player's own slots (clears the action queue first). With neither target nor pos the spell targets the player. |
| `counterspell` | `target` (creature id) | Enter counterspell mode against a creature. |
| `rest` | — | The rest button. |
