# Notify kinds: combat package

Sent by `Combat/CombatSystem*.cs`. Combat log lines also go to the players involved through
`IWorldOutput.SendMessage` (attacker and target, when they are PCs), and are repeated in the `log` field.

| kind | subject | data fields | meaning |
| --- | --- | --- | --- |
| `attack` | the attacker | see below | One resolved attack (a swing, shot, throw, attack of opportunity, cleave, riposte, circle kick or whirlwind strike). Play `animation` on the attacker, the target's reaction a beat later, and float the damage. |
| `attack-impact` | the attacker | as `attack` | A round attack's blow landing, about 0.55 s (ranged 0.7 s) after its `attack` notify, which then came with `pending` true and no damage or log. Float the damage and show `log` now. |
| `combat-feedback` | the creature | `creature` (id), `message` (`"out-of-ammo"`) | Feedback that isn't an attack. `out-of-ammo`: a ranged attacker has nothing left to fire and stops attacking. |

## `attack` fields

The first ten fields describe the attack itself; the rest carry the details of the combat rules.

| field | type | meaning |
| --- | --- | --- |
| `attacker`, `target` | object id | Who attacked whom. |
| `hit` | bool | The attack hit (after concealment, parry, Deflect Arrows and Epic Dodge). |
| `critical` | bool | Confirmed critical hit. |
| `roll` | int | The d20 attack roll. |
| `ab`, `ac` | int | Attack bonus of this attack and the target's AC against it. |
| `damage` | int | Damage rolled (before the target's immunity, resistance and reduction). 0 on a miss. |
| `ranged` | bool | Ranged attack (bow, crossbow, sling, thrown weapon). |
| `animation` | string | Swing animation: `{nw,1h,2h,2w}{slashl,slashr,stab}`, `{..}slasho` for off-hand swings, `bowshot`, `xbowshot`, `throwr`, `whirlwind`; `""` means no new animation (the second and later targets of a whirlwind). Slash/stab choice is weighted by baseitems `%AnimSlashL/R/S`. |
| `dealt` | int | Hit points the target actually lost. |
| `damageByType` | object | Damage rolled by `DAMAGE_TYPE_*` bit, keys are the bit as a string (`"4"` slashing, `"256"` fire, ...). |
| `power` | int | `DAMAGE_POWER_*` of the hit (for damage reduction). |
| `threat`, `confirmRoll` | bool, int | The roll threatened a critical and the confirmation d20 (0 when none). |
| `concealment`, `concealed` | int, bool | Miss chance percentage that applied, and whether the attack missed because of it. |
| `parried`, `deflected`, `epicDodge` | bool | Why a hit turned into a miss. |
| `sneak`, `deathAttack` | int, bool | Sneak attack damage included in the hit, and whether death attack dice were part of it. |
| `killed` | bool | The target died from this attack. |
| `offHand` | bool | Off-hand attack (second weapon or the other end of a double weapon). |
| `weapon` | object id | The weapon used (OBJECT_INVALID for unarmed strikes). |
| `kind` | string | `normal`, `opportunity`, `cleave`, `greatcleave`, `circlekick`, `riposte`, `whirlwind`. |
| `attackType` | int | `SPECIAL_ATTACK_*` of a feat attack (knockdown, disarm, called shot, sap, stunning fist), 0 otherwise. |
| `swing` | float | Seconds this swing takes (the six-second round divided by the attacks planned for it). |
| `feedback` | string[] | Feedback strings in order: the attack's name (`Attack of Opportunity`, `Knockdown`, `Smite Evil`, `Cleave`, `Whirlwind Attack`, ...), then results (`Target Concealed: 50%`, `Parried`, `Deflect Arrows`, `Epic Dodge`, `Critical Hit`, `Sneak Attack`, `Death Attack`, `Knocked Down`, `Disarmed`, `Discipline: resisted`, `Devastating Critical`, ...). |
| `pending` | bool | The blow lands later (a round attack): `damage` is the roll, but `dealt`, `killed` and `log` come with `attack-impact`. |
| `log` | string[] | The combat log lines, e.g. `Bob attacks Goblin : *critical hit* : (19 + 12 = 31 : Threat Roll: 15 + 12 = 27)` and `Bob damages Goblin: 15 (15 Physical)`. |

## Event context keys set by this package

| event | keys |
| --- | --- |
| creature `ScriptAttacked` (OnPhysicalAttacked), placeable/door `OnMeleeAttacked` | `Attacker` (once per attacker per round) |
| creature `ScriptEndRound` (OnEndCombatRound) | none |
| on-hit spell impact scripts (OnHitCastSpell, default hook) | `SpellCastItem`, `SpellId`, `SpellCaster`, `SpellTargetObject`, `SpellTarget`, `SpellTargetLocation`, `CasterLevel` |

`ScriptDamaged` / `OnDamaged` and death events come from the hit point rules (see [effects.md](effects.md)).

## Hooks for other packages

- `CombatSystem.ProvokeAttacksOfOpportunity(creature, reason)`: everyone threatening the creature takes an attack of
  opportunity (spells cast in melee without Defensive Casting should call it; ranged attacks already do).
- `CombatSystem.TouchAttack(attacker, target, ranged, feedback)`: TouchAttackMelee/Ranged (0 miss, 1 hit, 2 critical).
- `CombatSystem.Resolve(context)` / `Rules.Context(...)`: make a weapon attack from code; `AttackResolved` fires after each.
- `CombatSystem.SavingThrow` / `SkillCheck`: rolls on the combat dice (deterministic in tests).
- `CombatHooks.CanSee`: combat sight (invisibility, darkness, blindness, hiding). The stealth package can replace it.
- `CombatHooks.CastOnHitSpell`: OnHitCastSpell item properties; the spells package can route these through spell casting.
- `CombatSystem.TryUseCombatFeat`: ActionUseFeat's attack feats (called from `UseFeatAction`).
