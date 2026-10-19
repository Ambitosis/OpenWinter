# Encounters

How the original spawns encounters, measured black-box on the installed game, and how OpenWinter does it (`src/OpenWinter.Game/WorldObjects/EncounterSystem.cs`).

## Native or scripted

Encounter spawning is **native**. The stock encounter blueprints (`nw_goblingrp` and the rest of the palette) have
no event scripts at all: the engine decides who sets an encounter off, how many creatures of which kind appear, where,
and when it comes back. Scripts only see the events (OnEntered, OnExit, OnExhausted, OnHeartbeat, OnUserDefined) and
the encounter functions. Spawned creatures run their own blueprint's scripts; the stock AI treats them like any other
creature, except that `nw_c2_default1`, `nw_c2_default2` and `x0_c2_hbt_def` give encounter creatures
(GetIsEncounterCreature) mobile ambient animations. OpenWinter runs those scripts unchanged.

## Testing

- **nwserver** suite `encounter_events` (no player): events, encounter functions, and that nothing spawns.
- **nwmain** visual scenes (`python tools/visual/visual.py run <scene>`; the player is a fixed test character and the
  readings are logged as `VISUAL|note` lines):
  - `encounterspawn`: events, spawn timing, positions, single shot and continuous, resets, level 5, a henchman.
  - `encounterrules`: groups of six identical encounters on top of each other (one walk-in gives six samples): the
    counts are deterministic; NPC and henchman triggers, factions, spawn options, reset times.
  - `encounterbudget` (`parity/gen_encounterbudget.py`): six encounters at once, one list CR each, for the player
    alone at levels 1-20 and every difficulty (61 walk-ins, 366 readings), continuous respawn waves.
  - `encounterparty` (same generator, argument `encounterparty`): 21 parties of the player and henchmen of levels
    1-10, a henchman far away, more respawn waves.
  - `encountermix`: mixed and unique lists, the choice between spawn points, no spawn points, and leaving the area.
  - `encounterwaves` (`parity/gen_encounterwaves.py`): how far from the creature that sets it off a henchman counts,
    and respawn waves with list CRs above, at and below the creatures' own (the pool and points in play).
- Every count of `encounterbudget` and `encounterparty` (over 500) is replayed by
  `tests/OpenWinter.Tests/EncounterRuleTests.cs`; the mixed lists too.

## Events and functions

- **OnEntered / OnExit** fire for every creature that crosses the polygon, player or not, whatever PlayerOnly and
  Active say. A creature created inside fires OnEntered once it moves.
- **OnHeartbeat** runs only while the encounter is active.
- **OnExhausted** fires once every creature of the wave is dead or destroyed. By then the encounter is inactive.
- **GetEncounterDifficulty** returns the encdifficulty.2da **VALUE** of the encounter's row (0, 1, 2, 5, 9), not
  the row. **SetEncounterDifficulty** takes the row (ENCOUNTER_DIFFICULTY_*) and ignores values outside 0..4.
- **SetEncounterActive(5)** reads back 5.
- **GetEncounterSpawnsMax** is the Respawns field (-1 = without end; the stock groups have 0).
- **GetEncounterSpawnsCurrent** counts *respawns*: the first wave leaves it at 0.
- Spawned creatures are GetIsEncounterCreature already in their OnSpawn.

## Who sets it off

A wave spawns, right after OnEntered in the same update, when all of these hold:
- the encounter is **active** and no wave of it is out;
- the entering creature may set it off: **a player** always, **any creature** (a henchman, an NPC) when PlayerOnly is
  off; a henchman entering a PlayerOnly encounter does nothing;
- the **encounter's faction is hostile** to the creature (an encounter of the commoner faction never spawned for the
  player; a commoner NPC set off a hostile encounter with PlayerOnly off);
- **a player is in the area**. On nwserver with no player, hostile NPCs walked through encounters with PlayerOnly
  off and nothing ever spawned; with the player elsewhere in the area, the same NPC set one off.

## How many and which

All 504 single-entry wave sizes of `encounterbudget` and `encounterparty`, the mixed lists of `encountermix`, and the
range and respawn trials of `encounterwaves` come out exactly as these rules predict.

### Point values

Every challenge rating is worth points: **1.5^(whole part of the CR)** from 1 up to 50 (CR 2.0 and 2.9 both 2.25;
1.5^CR above 50), **1** at CR 0, and below 1 the fractionalcr.2da steps: 0.4 and up 1, 0.3 2/3, 0.2 4/9, 0.15 8/27,
less 16/81 (1.5^CR when negative). A list entry is worth what the CR **in the encounter's list** is worth, not the
blueprint's.

### Party strength and budget

- The encounter remembers the **last creature of any kind to step into its polygon** (the player who sets it off, or a
  monster that walks through after her).
- **Party strength S**: 1.5^level summed over every creature within **30 m along X and 30 m along Y** of that
  creature (a square: height ignored, 41 m along the diagonal still counts) that is alive, not dying, hostile to the
  encounter, and a player or an associate of a player. Level is the higher of hit dice and the level its experience
  gives (a level 1 player with 45,000 experience counted as level 10; a level 10 henchman with none as 10). Henchmen
  count at their full level; party membership plays no part; a plain NPC never counts.
- **Party level** = trunc(log1.5(S / 4) + 0.5), toward zero: four members of level L make L, one makes L - 3 (a level 1
  player alone makes -1).
- **Budget** = 1.5^(party level + encdifficulty.2da VALUE - 1) (VALUE: Very Easy 0, Easy 1, Normal 2, Hard 5,
  Impossible 9). If nobody counted (S = 0) no budget is added.

### The pool

The encounter keeps a **point pool** and the **points in play** (the list values of its creatures alive). Firing adds
the budget to the pool (what an earlier wave left over stays); if the pool is not above 0 nothing spawns. Each
creature that appears takes its list value out of the pool (not below 0; a pool below the cheapest list value is
cleared first) and adds it to the points in play. **A dying creature takes the value of its own challenge rating**
(GetChallengeRating) out of the points in play, not its list value.

### Choosing

A = pool - points in play; slack = A / 10. From the highest list CR down (equal CRs keep their order and form a
group; CR 0 entries are skipped), while A is above 0 and MaxCreatures leaves room: as many of that CR as still fit
A + slack, each a random one of the group's entries (a **unique** entry only once until the encounter is exhausted).
So one entry gives floor(1.1 x budget / value) creatures: 1, 1, 2, 3, 5, 8, 12, 18, 28 for budget / value = 1, 1.5,
... 1.5^8. Target-6 mixes (a level 1 player at Impossible, budget 17.1): CR 1, 2, 3 gave five CR 3 and one CR 1; CR 6
and 0.25 gave one CR 6 and sixteen CR 0.25 (the group goes on while the next still fits A + slack, A below 0 or not);
unique CR 4 and CR 2 gave one CR 4 and six CR 2.

- **RecCreatures** is a minimum: short of it, chosen creatures are broken down into cheaper ones (when the pool is
  above the cheapest value), then the wave is padded with the cheapest entry, whatever the budget (three CR 5 for a
  level 1 player at Normal; a unique entry three times). **MaxCreatures** caps it.

## Respawns

At reset time a continuous encounter fires if a creature that may set it off is inside, with the party strength
measured round the **remembered creature**, not round whoever is there; **straight after, the pool and the points in
play are cleared**, whether or not anything spawned. So when list CRs are above the creatures' own, the first respawn
of a big wave is small (12 then 3, 18 then 4): a list entry of CR 1 (1.5 points) for the stock goblin, whose own CR is
0.25 (4/9), leaves 1.06 points in play per dead goblin, and the respawn's budget less those points buys only the
minimum or a few. `encounterwaves` (a level 1 player at Impossible, budget 17.1, standing
inside, ResetTime 4 s, each wave killed):

| trial | list CR | creature's CR | RecCreatures | waves |
| --- | --- | --- | --- | --- |
| w1 | 3 | 1 | 3 | 5, 3, 5, 5, 5 |
| w1b | 1 | 1 | 3 | 12, 12, 12, 12, 12 |
| w1c | 1 | 0.25 (stock goblin) | 1 | 12, 3, 12, 12, 12 |
| w2 | 1 | 1 | 3 | 12, then a bandit walks through and is destroyed: nothing at the reset; she walks out and in: 12, 12 |
| w3 | 6 | 6 | 1 | 1, 2, 1, 1, 1 (the 5.7 points the first wave left over carry into the first respawn only) |
| w4 | 1 (MaxCreatures 4) | 1 | 1 | 4, 4, 4, 4, 4 |

## Range trials

`encounterwaves` r-trials: a level 1 player walks in alone (1 creature: the minimum), or with a level 12 henchman held
still at an offset from the point where she entered (42 when it counts, as the budget predicts):

| henchman at | counts |
| --- | --- |
| +29 m east, -29 m west, +29 m north | yes |
| +31 m east, -31 m west, +31 m north, +35 m east | no |
| +25, +25 (35 m) and +29, +29 (41 m) | yes |
| +29, +31 | no |
| a plain level 12 NPC (not an associate) 5 m away | no |

## Where

- **Spawn points**: every creature of a wave starts at **the spawn point farthest from the creature that set it off**
  (the first of equally far ones); the engine then moves each off the others (0.6 m steps round the point).
- **No spawn points**: round the encounter's **position** (the GIT XPosition/YPosition), even when the polygon lies
  elsewhere.
- Spawned creatures **face east** (GetFacing 0) whatever the spawn point's orientation.

## Single shot, continuous, reset

- **SpawnOption 1 is single shot, 0 continuous** (the stock groups are 1).
- **Exhausted**, every encounter turns itself off. A **single-shot** one stays off, whatever Reset says (re-entered
  after 8 s and 46 s with Reset on: nothing).
- A **continuous** one with **Reset** on comes back **ResetTime seconds** after it was exhausted (ResetTime 10: not at
  6 s, back by 11 s) while respawns are left (Respawns, -1 = without end; Respawns 2 gave two respawns, then it stayed
  off), and counts the respawn in GetEncounterSpawnsCurrent. If someone who may set it off is inside, the new wave
  spawns at once; otherwise the next entry spawns it (the player was in another area when it came back: the count
  went up, nothing spawned, and walking in later spawned a full wave). With **Reset off** a continuous encounter never
  comes back.
- While a wave is out (creatures left alive), leaving and re-entering spawns nothing more.

## Leaving the area

Encounter creatures **stay**: twelve left alive were all still there after the player spent 30 s in another area,
and the encounter was still active with its wave out.

## Not matched yet / not measured

- More than one player (the party of players, and the level of players in different parties): the harness has one
  client. OpenWinter combines every party member the same way.
- The "Always Reset Encounter Spawns" server option (the pool becomes exactly the budget): not read.
- How far a reset respawn's creatures stand from the spawn point (OpenWinter: within 20 m) and how breaking chosen
  creatures down into cheaper ones picks them: unmeasured.
- A creature destroyed (not killed) takes its CR's value out of the points in play in OpenWinter, as a death does;
  not measured.
- Summoned creatures and familiars in the party (only henchmen were measured).
- Exact positions of the creatures after the first (the engine's free-spot search round the point).
- Whether DMs set off encounters (OpenWinter: no).
