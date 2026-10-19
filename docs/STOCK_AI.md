# Stock creature and henchman AI

NWN's creature AI is mostly NWScript that ships with the game. OpenWinter runs those scripts unchanged in its VM; what has
to match the original is the engine underneath them: the functions they call, the events that start them and when, and
how the actions they queue run. This page splits the two, lists what the scripts depend on with its status, and records
how the original behaves as measured by the nwserver parity suites.

## Scripted (runs unchanged, never reimplemented)

| Part | Scripts |
| --- | --- |
| Creature events | `nw_c2_default1` (heartbeat) .. `nw_c2_default9` (spawn), `nw_c2_defaulta`/`b`/`d`/`e` (rested, spell cast at, user defined, blocked); the XP2 `x2_def_*` scripts call them |
| Combat decisions | `nw_i0_generic` (DetermineCombatRound, chooseTactics, RespondToShout), `x0_inc_generic` (bkAcquireTarget, bkTalentFilter, GetCombatDifficulty), `x0_i0_talent` (every Talent* routine: heal self, buffs, summons, spell attacks, melee feats, flee, turning, auras), `x0_i0_enemy`, `x0_i0_equip`, `x0_i0_match`, `x0_i0_combat` (special tactics) |
| Spawn conditions and ambient life | `x0_i0_spawncond`, `x0_i0_anims`, `x0_i0_walkway`, `x0_i0_behavior` |
| Henchmen and associates | `nw_ch_ac1`..`nw_ch_ace`, `x0_inc_henai`, `x0_i0_assoc`, `x0_ch_hen_*`, `x2_hen_*` |
| Spell effects | every `nw_s0_*` / `nw_s1_*` / `x0_s0_*` impact and area script, `x2_inc_spellhook` |

The scripts decide everything that looks like "AI": whom to attack (the nearest seen enemy; the "hated class" they also
roll is ignored by the engine, below), whether to heal (under half hit points, a random healing talent, else a random
potion), when to buff, summon, cast or fight (chooseTactics' offense / magic / compassion numbers, partly random), when to
flee (commoners), how to answer shouts, and how henchmen follow and help.

## Native (the engine supplies it)

- **Talents**: the list of talents a creature can use, how GetCreatureTalentBest ranks and ties them, GetCreatureTalentRandom's
  weighting, and what ActionUseTalent* then does.
- **Queries**: GetNearestCreature and its criteria, perception lists, reputation, the attack / spell / hostility getters,
  GetHasSpell / GetHasSpellEffect / GetHasFeat.
- **Events and their timing**: OnPerception (cadence by AI level, order between targets), OnHeartbeat, OnEndCombatRound
  (attack rounds, defending rounds, spell rounds), OnPhysicalAttacked, OnDamaged, OnSpellCastAt, OnConversation (who
  hears a shout), associate commands, which events the dead get.
- **Actions**: ActionAttack (round clock, swing times, retargeting), ActionCastSpell* (cast cadence, attacks of
  opportunity), feats used as attacks, ClearAllActions and the combat state, Sanctuary.
- **Throttling**: GetAILevel / SetAILevel and the automatic level (very low in an area without players, normal in combat).

## How it is measured

Suites (`parity/suites/ai_*`, generated parts from `python parity/gen_ai.py`):

| Suite | What |
| --- | --- |
| `ai_talents` | GetCreatureTalentBest for every category and nCRMax -1, 0..9, 20, 100; GetCreatureTalentRandom and Best(-1) sets; GetCreatureHasTalent; constructors; spending; potions, kits, wands, scrolls; 13 stock creatures |
| `ai_talent_ties` | tie breaking and weighting from hundreds of draws on fixture creatures holding exactly the spells or feats under test |
| `ai_talent_use` | ActionUseTalentOnObject / AtLocation with each kind of talent: action, timing, spells signalled, uses spent, potion drunk, summon, aura |
| `ai_feat_use`, `ai_attack_queue` | combat feats by script, and orders given to a creature already attacking |
| `ai_queries` | GetNearestCreature criteria (reputation, every PERCEPTION_* type, nth, combinations); the attack / spell / combat getters through attacks, ClearAllActions and spells |
| `ai_events` | heartbeat, perception and combat-round timing by AI level |
| `ai_cast_rounds` | OnEndCombatRound of casters, and attacks of opportunity for casting |
| `ai_melee`, `ai_caster`, `ai_feats`, `ai_ranged`, `ai_flee`, `ai_associate` | stock creatures fighting each other with their own scripts, one fight per area, 6 to 16 replicas |
| `sched_percept`, `sched_events`, `delays` | perception and heartbeat cadence, event order, listeners |
| `ai_level_auto` | the automatic AI level without players |
| `round_clear`, `round_idle` | combat rounds through ClearAllActions and re-orders |

```
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run ai_talents
```

The fight suites keep the creatures' own event scripts and put a one-line wrapper in front of each (`pai_e0`..`pai_e12`,
`parity/include/pai_inc.nss`): it logs the event and runs the creature's script, which reads the same event state. A
poller samples GetCurrentAction and GetAttackTarget every 0.25 s. Spells are read from their targets' OnSpellCastAt.
The original's Random() can't be seeded, so fights are compared statistically: first-event times and counts per creature
are means with tolerances from their spread, decision patterns are rates. Each run also writes every creature's timed
event and action trace to `out/parity/results/<suite>/{nwn,openwinter}.raw.txt` (`RAW|` lines, not compared). A module
without repute.fac makes every NPC faction friendly in the original, so the suites load the Prelude factions.

The rule suites match fully or to within a handful of readings; the fight suites' few residual differences move
between runs (statistical readings near their tolerance). Player-side AI levels and attacks are measured on nwmain with
the visual scenes `playerai` and `plotshot`.

## What the original does

### Talents (CreatureTalents.cs)

- Every usable instance is a talent: each ready memorised slot, each known spell of a spontaneous caster with a slot left
  at its level, each ready special ability copy, each potion item in the backpack (a stack counts once), each usable feat
  and skill. Wands, scrolls, healer's kits and items with cast-spell properties that aren't potions are not talents.
- A spell with sub-radial spells (Protection from Alignment) is one talent per sub-radial spell.
- A memorised spell with a metamagic the creature has no feat for is not prepared at all (no talent, GetHasSpell 0).
- Categories are spells.2da / feat.2da Category, except that spell categories 1 and 11 are exchanged: category 1
  (TALENT_CATEGORY_HARMFUL_AREAEFFECT_DISCRIMINANT) finds Fireball and Web, 11 finds Sleep. A blank category is 0. A
  potion's category follows its spell's: healing to 17, conditional to 18, protection to 20, enhancement to 21.
- A spell's rating is (innate level + metamagic level cost) * 2 - 1 (cantrips -1, level 1 spells 1, Fireball 5); a spell
  whose level for the class is below its innate level (Resistance) rates just under the others of its innate level.
- GetCreatureTalentBest: the highest rating not above nCRMax; equal ratings go to the later talent (memorisation order),
  except that sub-radial spells, and anything tying after one, are chosen at random among the ties. Potions ignore
  nCRMax (the highest-rated potion always wins); feats and skills ignore it and are always chosen at random. nCRMax -1
  behaves like GetCreatureTalentRandom; below -1 only potions, feats and skills are found.
- GetCreatureTalentRandom is uniform over the instances (two memorised Cure Light Wounds are twice as likely).
- GetCreatureHasTalent(TalentSpell) needs a copy without metamagic; potions never count. GetHasSpell counts a special
  ability once however many copies, and a sub-radial spell through its master's slots.
- ActionUseTalentOnObject with a potion drinks it (ACTION_ITEMCASTSPELL); with a melee feat it attacks (below).

### Queries

- GetNearestCreature ignores a CREATURE_TYPE_CLASS criterion in the second and third places, so nw_i0_generic's "hated
  class" choice always falls back to the nearest seen enemy. Reputation and every PERCEPTION_* criterion match.
- GetAttackTarget is the target of the creature's last swing: invalid while it walks up to the target and after
  ClearAllActions, set again at the next swing, cleared when the target dies (kept when the target only changes area).
- GetAttemptedAttackTarget is the current attack order's target, but invalid while the creature walks up to it.
- GetIsInCombat is TRUE as soon as an attack order is the current action. ClearAllActions keeps it; ClearAllActions(TRUE)
  ends it. Combat ends about eight seconds after the last attack made or received.
- GetLastSpellCaster / GetLastSpell / GetLastSpellHarmful keep the last EventSpellCastAt the object received, outside
  OnSpellCastAt too; a harmful one makes the caster its GetLastHostileActor. An area of effect's event comes from its
  creator (an aura's heartbeat signals with OBJECT_SELF).
- In a module without repute.fac every NPC faction likes every other (GetReputation 100); Hostile still attacks players.

### Events and timing

- Perception: every 4 s from a creature's appearance at AI levels low and above; every 6 s at very low (an area without
  players), the first about 7.8 s after it appears. The cadence follows the level at the time (an attacked very-low
  creature is at the normal level). A pass goes through the creatures on the observer's perception list newest first,
  then the newly noticed ones along x (newest first at equal x). A new one gives heard then seen; one on the list that
  gains senses seen then heard; one that loses them inaudible then vanished. An entry with no senses left stays on the
  list while it is within perception range and leaves it once out of range (`sched_percept`).
- Creatures due in the same frame are served by AI level, then in the order they joined that level (by creation,
  SetAILevel or an automatic change). Four observers made in one order and put on AI_LEVEL_NORMAL in another run their
  first passes in the SetAILevel order, one frame (15 ms) apart on nwserver; OpenWinter serves them in that order in one
  tick (`sched_percept`).
- Heartbeat: 6.0-7.2 s apart at low and above (mean 6.64 s), 7.5-8.9 s at very low (mean 8.15 s), following the level
  at the time. Dead creatures get no heartbeat, perception, spell-cast-at, attacked, damaged, end-of-round or blocked
  events.
- OnEndCombatRound: attack rounds of 5 s (one attack) or 5.5 s (more) from the order; the first round of an attack starts
  on arrival. The original's first swing comes 0.05 to 0.9 s after the order (its AI update phase, about 0.3 on
  average); OpenWinter's at 0.2 to 0.3. A cast ends a round 6 s after it began, in combat or not; a caster still in
  combat then ends its next round 3 s later, then every 6 s.
- Combat rounds through re-orders (`round_clear`, `round_idle`): ClearAllActions and a new attack in the middle of a
  round don't end the round; the new target's first swing comes about 0.45 s after the old round's end, and
  OnEndCombatRound fires at that end (the round's clock is kept through ClearAllActions). Two attacks in a round (or
  one, hasted) come 2.25 s apart. Hasted, every swing comes about 0.2 s earlier in the round (the first 0.0-0.2 s after
  the round's start; `CombatSystem.HasteSwingLead`).
- A creature whose attack is cleared after a swing: the blow still lands (0.38 s after ClearAllActions(TRUE) made
  0.2 s after the swing) and the round still ends with OnEndCombatRound 4.4 s later. The engine doesn't take the fight
  up again; the stock `nw_c2_default3` does only when the target is an enemy it perceives. A creature in combat with no
  attack order also gets OnEndCombatRound every 6 s, counted from when the order went: 5.8-5.95 s after
  ClearAllActions(), 5.85-5.97 s after the blow that puts it back in combat after ClearAllActions(TRUE), none if that
  blow missed (`CombatSystem.DefenderRound`; a landing blow puts the attacker back in combat, `Impact`).
- Shouts: an NPC's speech at every volume (talk, whisper, shout and the silent ones) reaches only creatures that hear
  it: on their perception list as heard. A listener 30 m from a shout that saw the speaker but was beyond its 20 m
  hearing gets nothing, and nor does a blind one 6 m away. A placeable's speech reaches listeners within their own
  hearing range. Listeners' OnConversation events run in order of increasing x, after the speaker's script
  (`sched_events`). With creatures perceiving one another in turn, only the later ones' NW_I_WAS_ATTACKED is heard.
- Events due at the same time: zero-delay ones (DelayCommand(0.0), AssignCommand) run first in, first out; delayed ones
  due at the same instant run last queued first, across objects too (`sched_events`, `delays`).
- A master's associates get ASSOCIATE_COMMAND_MASTERUNDERATTACK in OnConversation as its attacker swings (NPC masters too).
- AI level (`ai_level_auto`, no players): very low by default, normal while in combat, back to very low as combat
  ends. SetAILevel locks the level: values above HIGH read HIGH (VERY_HIGH and 9 alike), values below -1 lock VERY_LOW
  (a creature set to -5 stays there in a fight), LOW stays LOW in a fight and through a jump to another area.
  AI_LEVEL_DEFAULT returns to the rule level at once (NORMAL if fighting). An NPC's henchman is at very low like any
  creature; a placeable reads VERY_LOW.
- AI level with a player (nwmain visual scene `playerai`): the player reads VERY_HIGH (4), her henchman HIGH (3), also
  while it stands in an area she has left; an NPC in her area LOW; an NPC in an area she jumps into LOW at once; one she
  leaves drops to VERY_LOW at its next perception pass; a placeable reads VERY_LOW even with her beside it.
  `World.EffectiveAILevel`, `CreatureSociety.PlayersAtPass`.

### Actions

- ActionAttack(o, TRUE), a passive attack, doesn't follow a target that leaves the area: the attack ends in the next
  update, or goes on against the nearest enemy within reach (a commoner 1.5 m from the attacker). ActionAttack(o, FALSE)
  follows it, even with that enemy beside it. With no route to the target's area the NPC keeps its attack action
  (reading ACTION_ATTACKOBJECT and no attack target) for good, and does not turn to an enemy beside it
  (`attack_leave`; see [INTER_AREA_MOVEMENT.md](INTER_AREA_MOVEMENT.md)).
- A player's attack whose target leaves the area ends in the next update, or turns to an enemy within her reach at her
  next swing. An encounter creature's ActionAttack(player, FALSE) follows her through a transition like a plain NPC.
- ActionAttack while the current action is an attack changes that attack (the target clears and is set again at the
  next swing on the round's schedule); actions queued behind an attack wait for it to end.
- ActionUseFeat / ActionUseTalent* with a combat feat (Knockdown, Disarm...) is an attack action that goes on; the other
  melee feats (Power Attack, Improved Power Attack, Expertise) start an ordinary attack. A combat feat ordered mid-round
  (some of the round's attacks made, some left) is lost.
- NPCs draw no attacks of opportunity for casting next to an attacker (players are not measured and keep them); moving
  away draws one only from the creature the mover is attacking, shooting in melee none (`combat_aoo`,
  `combat_archer`). Players' shots draw attacks of opportunity at D&D Hardcore only, never from a plot creature
  (`plotshot`).
- Sanctuary: an attacker that fails its save drops its attack as the sanctuary lands if it has perceived already, else at
  its first perception update, and idles until the sanctuary ends.
- Dragon breath (`ai_talent_use` drag.breath, `breath_timing`): a breath is an ordinary spell-ability cast, released
  2.5 s after the action starts (ConjTime 2000 + 0.5 s; 1.5 s hasted, at once as an instant cast) and finished at
  5.5 s. Breath range is touch, so a dragon 6 m from its target first walks up, braking to a stop at the casting
  distance like any move.

## Engine functions and events the stock AI depends on

Status: **M** measured and matching, **D** a known remaining difference, **–** not measured by these suites (ordinary
script plumbing or measured elsewhere).

| Function / event | Status | Notes |
| --- | --- | --- |
| GetCreatureTalentBest, GetCreatureTalentRandom | M | categories, ratings, ties, items, feats (ai_talents, ai_talent_ties) |
| GetCreatureHasTalent, TalentSpell/Feat/Skill, GetIsTalentValid, GetIdFromTalent, GetTypeFromTalent | M | |
| ActionUseTalentOnObject, ActionUseTalentAtLocation | M | potions drink, feats attack; spells, summons, auras (ai_talent_use) |
| ActionUseFeat | M | combat feats are attacks (ai_feat_use) |
| GetHasSpell | M | special abilities once, sub-radial spells (spell_counts) |
| DecrementRemainingSpellUses, GetHasSpellEffect, GetHasFeat, GetHasFeatEffect | M | |
| GetNearestCreature (all criteria, nth) | M | class criterion only first (ai_queries, nearest) |
| GetObjectSeen, GetObjectHeard, GetLastPerceived, GetLastPerception* | M | (perception, ai_queries) |
| GetIsEnemy, GetIsFriend, GetIsNeutral, GetReputation, GetFactionEqual | M | including modules without repute.fac |
| GetAttackTarget, GetAttemptedAttackTarget, GetIsInCombat | M | (ai_queries) |
| GetAttemptedSpellTarget | M | set as the cast starts |
| GetLastAttacker, GetLastDamager, GetTotalDamageDealt | M | |
| GetLastHostileActor | M | harmful spells set it |
| GetLastSpellCaster, GetLastSpell, GetLastSpellHarmful | M | kept outside the event; AoE creator |
| GetLastAttackType | D | the original reports 0 in OnPhysicalAttacked for Knockdown and Disarm; OpenWinter the special type (the stock AI never reads it) |
| GetCurrentAction | M | feats as ACTION_ATTACKOBJECT |
| ActionAttack, ClearAllActions | M | retarget, combat kept, first round on arrival; a friendly faction mate isn't attacked (attack_mate); a cleared round runs on (round_clear) |
| ActionCastSpellAtObject / AtLocation | M | spell rounds end a round; no casting attacks of opportunity for NPCs |
| ActionMoveAwayFromObject (TalentFlee) | M | moves fRange further than where it started (ai_flee, archer_backoff) |
| ActionEquipMostDamagingMelee/Ranged, bkEquipAppropriateWeapons | M | (ai_ranged) |
| GetAILevel, SetAILevel, automatic level | M | very low perception and heartbeat, clamps and locks (ai_level_auto) |
| SpeakString (all volumes), SetListening, SetListenPattern, GetListenPatternNumber | M | listeners must hear; increasing x (sched_events) |
| AddHenchman (NPC master), GetMaster, GetAssociate, associate commands | M | MASTERUNDERATTACK |
| OnPerception, OnHeartbeat, OnEndCombatRound, OnPhysicalAttacked, OnDamaged, OnSpellCastAt, OnConversation | M | timing and order above (ai_events, ai_cast_rounds, fight suites) |
| OnBlocked, OnDisturbed, OnRested, OnUserDefined | – | not exercised by these fights |
| GetAC, GetHitDice, GetAbilityModifier, GetChallengeRating, GetCreatureSize, GetRacialType, GetClassByPosition, GetLevelByClass, GetArcaneSpellFailure, GetIsPlayableRacialType, GetIsImmune, GetIsCreatureDisarmable | – | rules values measured by other suites |

## Not matched yet

- **Hasted first round.** In a hasted creature's first round the second swing comes 2.4-3.1 s after the first, but
  only when the fight starts about 2 s after the creature was made (with the order 12 s after creation, 1.7-2.6 s).
  The first swing's place in the round is constant for an attacker but varies 0.2-0.65 s between attackers (an
  initiative); OpenWinter uses 0.2 s.
- **GetLastAttackType** (above).
- **Frame staggering.** The original gives the AI about 10 ms of each frame, shared out by AI level (60 % of what is
  left to each level from VERY_HIGH down, the rest to VERY_LOW), round-robin within a level, at most one update per
  object per frame, and a level's turn ends when an update queues events. So creatures made in the same instant are
  spread over successive frames (15 ms apart). OpenWinter keeps the order (by level, then by when each joined it) and
  runs the events already due between one creature's perception pass and the next, as the original runs its event
  queue before each update (so a henchman whose pass follows its master's gets the master's shout first), but updates
  them all in the same tick; a frame-time budget isn't modelled, so the rarer orders that spreading produces aren't
  reproduced. The heartbeat also runs inside the creature's own AI update in the original, just before its perception
  pass; OpenWinter runs all heartbeats of a tick before all perception passes.
- **A pursuer in the target's new area** walks on past the target (to about where the door it took stood in the old
  area) and only then turns back and fights, 13.5 s after arriving; OpenWinter's fights 8 s after arriving.
- **Fight outcomes** in the caster and henchman suites still differ in some readings (cleric fights, a sorcerer's
  Stoneskin, henchman first attacked), within the noise of six to twelve replicas or not explained yet.
- **Players**, not measured: doors, triggers and areas of effect at AI level LOW; shouts players hear and players'
  speech (any NPC listener in range hears them); a player who can't reach her target in her own area.
- **Invisible creatures' hearing** is random in the original (see [STEALTH.md](STEALTH.md)).
