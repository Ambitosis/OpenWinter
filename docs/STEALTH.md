# Stealth and detection

How stealth mode, detect mode and the Hide / Move Silently / Spot / Listen checks work, measured black-box on the
original (`nwserver` parity suites and `nwmain` visual scenes). Code: `Skills/SkillSystem.Stealth.cs` (modes, rolls,
checks), `Society/PerceptionSystem.cs` (where the checks are asked), `GameServer.HiddenFrom` / `SyncConcealed` (what
clients are told), `client/UI/PlayerFx.cs` (own-stealth look).

## Engine or script

| Part | Native or scripted | Where OpenWinter does it |
| --- | --- | --- |
| Stealth / detect modes, their speed, what ends them | native | SkillSystem |
| Hide/MS vs Spot/Listen checks, perception lists, OnPerception events | native | SkillSystem + PerceptionSystem |
| Invisibility, darkness, See Invisibility, True Seeing, Ultravision in perception | native | PerceptionSystem |
| Sneak attack against a defender that can't see the attacker | native | CombatVision / CreatureCombatRules (suite `combat_sneak`) |
| Henchmen entering stealth with their master, AI using stealth | scripted (`x0_i0_modes`, `nw_i0_generic`, `x0_inc_henai`) | the stock scripts, unchanged |
| Secret doors and objects | scripted: `x0_sectrig_*` triggers run `x0_o2_sec_door1` etc. (`x0_i0_secret`: d20 + `GetSkillRank(SKILL_SEARCH)` > the trigger's KeyName); `nw_o2_detectdoor` / `x2_hb_secretdoor` heartbeats roll the best nearby PC's Search against the placeable's Will save within its Reflex save in metres | the stock scripts; parity suite `stealth_secret` runs the x0 trigger |
| Elves' "passive search" for secret doors | no special engine path: the scripts read `GetSkillRank(SKILL_SEARCH)`, which carries the racial bonus feats; Keen Sense is not consulted by them | GetSkillRank |
| Trap Search | native ([TRAPS.md](TRAPS.md)) | SkillSystem.Traps |

## Modes

Suite `stealth_modes`.

- `SetActionMode` stealth and detect are independent: both can be on. `GetStealthMode` / `GetActionMode` agree.
- Either mode makes the creature walk: a normal-rate human ordered to run covers 7.0 m in 4 s (walk rate 1.75 m/s).
- A creature that is **attacking** can't enter stealth (the player is told TLK 10719), with or without Hide in Plain
  Sight. One that is only being attacked (`GetIsInCombat` 1) can. Detect mode can be turned on while attacking.
- **What ends them** (the flag sampled every 0.1 s):

| Action | Stealth ends after | Detect ends after |
| --- | --- | --- |
| melee attack, target adjacent | 0.3-0.4 s (the first swing) | 0.5 s |
| melee attack from 8 m | 3.7-3.9 s (on arrival, at the swing) | |
| bow attack from 8 m | 0.3-0.6 s | |
| spell (self buff or hostile, cheat cast) | 0.1 s (casting starts) | 0.1 s |
| potion (`ActionUseItemOnObject`) | 1.0 s (when used) | not measured (OpenWinter: same) |
| damage effect, being hit, invisibility, sanctuary | never | |

- Invisibility does not switch stealth mode on (despite the skill description); stealth set before invisibility
  stays on when it ends.
- `GetDetectMode` returns **2** once a creature (not in detect mode) has cast a spell (from the start of the cast)
  or been hurt (from its next perception update), and still after 90 s; healing doesn't do it, and detect mode
  makes it 1. OpenWinter: `CreatureStats.DetectAlert` (runtime only).

## Who sees a hider

Suite `stealth_rules`, with deterministic skills (+50 against nothing).

- Hidden from hostile, neutral **and same-faction** creatures. A summoned creature sees and hears its master.
  OpenWinter keeps players seeing their party (not measurable on nwserver) and DMs seeing everything.
- Sight and hearing are separate: a hider can be seen and not heard, or heard and not seen.
- **Sticky:** a spotted hider stays seen while it stays in view, even if the observer's Spot then drops and the
  hider's Hide rises by 50. It is hidden again only after leaving the observer's sight (out of range and back).
- **Entering stealth in view hides nothing:** the observers keep seeing and hearing it, and toggling stealth off and
  on again doesn't help, unless the hider has **Hide in Plain Sight** (then the observer gets inaudible and vanished
  events and must find it again).
- True Seeing sees hiders; See Invisibility and Ultravision don't.
- Invisible (not in stealth): not seen, **heard** by NPC observers at close range (below) even against Move Silently
  +50. See Invisibility and True Seeing see it. Invisible and in stealth: See Invisibility still needs a Spot success.
- Darkness on the target: not seen, heard at close range; Ultravision sees it. An observer in darkness or blind hears
  only, at close range.
- Sanctuary (suite `stealth_sanctuary`): each hostile observer makes one Will save. One that saves treats the warded
  creature as invisible: never seen, heard as usual. One that fails sees and hears it at the update where it first
  comes into its perception, loses sight of it at its next update and hearing at the one after; if it already
  perceived it when the sanctuary landed it loses sight at its next update and hearing at the one after.
  `PerceptionSystem.Sanctuary`.

## The checks

Measured on NPCs (suites `stealth_check_*`) with pairs of an idle observer and a hider in stealth (bandit blueprints;
Spot, Listen, Hide and Move Silently set exactly with skill effects and read back with `GetSkillRank`), one pair per
isolated 20 m cell, 24 pairs to a group. A trial brings the hider back from a parking area every 14 s and reads
`GetObjectSeen` / `GetObjectHeard` 4.2 s later, so each trial is the observer's first pass on an unperceived hider:
about 1,000 trials per group. Rates below are nwserver's, with a 95% Wilson interval where given.

### How often

Suite `stealth_check_cadence` (32 observers, 300 s).

- Nobody rolls per pair. Every creature keeps a Hide, a Move Silently, a Spot and a Listen die, refreshed at most
  every 6 s inside its own perception pass, when it evaluates someone. An NPC's pass comes every 4.04 s (4.006-4.11),
  so its dice change on every second pass.
- Every pass compares the cached dice: a hider whose Hide dropped from 51 to 1 is seen at the observer's next pass,
  0.0-4.04 s later, not at its next refresh.
- Hide and Move Silently dice are re-rolled (d20) only while the creature is in stealth. A hider with nobody within
  its own perception range never refreshes (hiders beyond their own 10 m range are heard about twice as often as a
  fresh d20 predicts, as a fixed 1-10 die does). New creatures start with 1-10 dice; OpenWinter refreshes them at the
  first pass that evaluates someone.
- Spot and Listen dice are a **d20 for an active observer** and a **d10** otherwise. Active: detect mode, Keen Sense,
  or standing in the **idle stance** out of combat (`stealth_check_stance`). A creature made while the module runs
  (CreateObject, encounters) stands in the combat-ready stance, passive, until it first comes to a standing stop (the
  end of a move, a jump); one placed in the toolset or loaded from a save starts idle. OpenWinter:
  `Creature.IdleStance` (cleared in `ObjectFunctions.Spawn`, set again by `Movement` at a stop and `World.Jump`),
  `SkillSystem.ActiveObserver`. GetDetectMode is unchanged.
- A new creature's first pass comes 4.0-5.7 s after it is made (mean 4.2), not at once.
- A creature that jumps to another area or is destroyed is dropped by every observer at once, in the same frame:
  OnPerception "inaudible", then "vanished"; the leaver gets no events of its own (`perception_leave`). OpenWinter
  does that for creatures that aren't players (`PerceptionSystem.Left`, the destroyed-object handler); a player
  leaving is dropped at the next pass.

### The Spot check

Seen when the total is above 0 (a tie hides):

total = Spot (full when active, half towards zero when not) + Spot die + area ModSpotCheck - hider's Hide die - Hide,
-5 if the hider is still, +5 if the observer is still, -10 if the observer is in combat, + size, + night

| Group (`stealth_check_rates`, `_detail`, `_mods`) | Model | nwserver | OpenWinter |
| --- | --- | --- | --- |
| idle, Spot 10 vs Hide 1 (passive, both still) | 42.5% | 41.6-43.0% | 42.9-45.4% |
| detect mode, idle, Spot 10 vs Hide 1 | 81.25% | 78.7-80.1% | 81.5-82.0% |
| detect mode, Spot 10 vs Hide 10 / Hide 18 | 47.5 / 16.5% | 47.1 / 16.9% | 48.8 / 18.4% |
| walking patrol, Spot 10 vs Hide 1 (passive, moving) | 18% | 21.1-24.3% | 18.8-20.5% |
| walking patrol in detect mode | 65% | 63.6-64.7% | 66.0% |
| idle observer, hider walking past, Spot 4 vs Hide 1 | 52.5% | 44.9% | 54.4% (differs) |
| Spot 10 vs Hide -5, idle / attacked (in combat) | 72.5 / 22.5% | 71.7 / 20.5% | 72.7 / 23.1% |

- **No natural 20.** Where the numbers can't fail, every observer pass sees the hider.
- **Hide exactly 0 is seen without a check; -1 is not** (`stealth_check_zero`, observer Spot and Listen -10): 99.8%
  seen and heard at 0; at -1 7.2% seen (passive model 7.5%) and 6.6% heard (5.0%). Move Silently 0 likewise.
- **Size** (`stealth_check_size`): Spot by the hider's size category minus the observer's, -2/-1/+1/+2 steps giving
  -8/-4/+4/+8 and three steps 0. Listen by the hider's own size: tiny -8, small -4, large +4, huge +8.
- **Night and light** (`stealth_check_night`, by the module clock: dawn and dusk hours): an observer with no light
  (`VFX_DUR_LIGHT_*`, visualeffects.2da rows 153-180) and no `VFX_DUR_DARKVISION` takes off ruleset.2da
  SPOT_NO_LIGHT_MODIFIER (5); a hider carrying a light adds HIDING_LIGHT_MODIFIER (10). Listen has no night or light
  modifier. The area's `IsNight` does nothing.
- **Moving.** The still modifiers follow the creature's motion at the moment of the check. OpenWinter counts any
  movement (`Speed`); the original's walkers set off about 0.6 s after a jump and slow down near each turn, so its
  moving groups sit between the still and the moving predictions.

### The Listen check

The same with Listen against Move Silently, a point off per whole 3 m of distance, the hider's own size, and no
night, light or facing modifier. The master hears its associates without a check.

Interior areas (`stealth_check_interior`, `stealth_check_interior_far`): plain creatures in neighbouring walled tiles
(no walkable path between them) are heard and not seen in exterior, interior (ARE Flags 1), underground and
interior-and-underground areas alike. Further away it differs: an exterior observer hears creatures many tiles away,
while interior and underground observers hear only those in the tiles next to their own. OpenWinter
(`PerceptionSystem.SoundCarries`): in interior and underground areas a creature its room connectivity (closed doors
included) cuts off is still heard, not seen, from the neighbouring tiles, unless a closed door stands in either tile.

**What sticks** (`stealth_rules`): a hider seen and heard is not checked again while it stays in sight range and
view, even if it enters stealth; one only seen stays seen, one only heard stays heard, while in view; out of range or
view it is checked afresh.

### Invisibility, darkness, blindness

Suites `stealth_check_invis`, `stealth_check_blind` (exact readings).

- An invisible creature (or one carrying darkness, to an observer without ultravision) is never seen, and heard only
  within the observer's appearance.2da PREFATCKDIST + the target's HITDIST + 1.5 m: two humans 3.5 m (3.40 m heard,
  3.60 m not); a halfling hearing humans 2.9 m; a human hearing halflings 3.45 m; a human hearing brown bears 4.4 m.
  Not sneaking, it is heard without a check inside that.
- A blind observer (EffectBlindness, or EffectDarkness on it without ultravision) sees nothing and hears plain
  creatures only within the same close range. True Seeing cancels blindness.
- A visible creature 5 m away that turns invisible is lost from sight at the observer's next pass and from hearing
  exactly one pass later (4.00-4.05 s); an invisible one heard at 3 m that moves to 5 m is lost at the next pass, and
  heard again at the next pass when it comes back.
- Invisible and in stealth, Listen is tried on every pass with the cached dice.

### Distance limits

Suite `stealth_check_ranges` (exact). ranges.2da rows 8, 9, 10 and the default (appearance PERCEPTIONDIST 9) give
sight/hearing of 10/10, 20/20, 35/20 and 20/20 m. In combat (being attacked, `GetIsInCombat` 1) an NPC uses row 10
(PercepRngLng) if its own sight range is at least as long, otherwise row 13 (PercepRngMonster): rows 8, 9 and the
default then see 28 m and hear 15 m; row 10 keeps 35/20.

## Presentation

Measured on nwmain (visual scenes `stealthview`, `stealthself`).

- Entering stealth floats `*Stealth Mode Activated (Run Disabled)*` (TLK 58277) and writes nothing in chat; the
  quickbar slot holding stealth gets the mode frame while it is on.
- The player sees her own character **pulsing** while in stealth, on a 4 s cycle from entering it: opaque for about
  0.5 s, fading to about 45% opacity by 0.9 s, faint until 2.5 s, opaque again by 3 s. Being watched doesn't change
  it.
- Trying to enter stealth while attacking prints `You cannot enter stealth mode while in combat.` in the chat.
- A player's character does **not** hear an invisible (not stealthy) creature, whatever its Move Silently, although
  NPC observers always do. OpenWinter does the same unless the player has See Invisibility or True Seeing.
- Other creatures in stealth that she has spotted are drawn **normally (opaque)**; ones she only hears are faint;
  ones she neither sees nor hears are not drawn and Tab gives them no label or highlight (heard ones get both).
- OpenWinter: own character pulsing (`ObjectView.StealthAlpha`), others opaque (only `Session.ControlledId` gets
  `SetStealth`); heard-only faint through the creature-visibility feed. A concealed creature (stealth, invisibility,
  darkness, sanctuary, etherealness) that the player neither sees nor hears is **not sent to her client at all** (no
  spawn, no position updates, no notices); it is spawned when she perceives it.

## Testing

```
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run stealth_rules
python tools/visual/visual.py run stealthview
```

Server suites: `stealth_modes`, `stealth_rules`, `stealth_sanctuary`, `stealth_secret`, `perception_leave` and the
`stealth_check_*` family (`cadence`, `rates`, `detail`, `mods`, `stance`, `natural`, `zero`, `size`, `night`, `invis`,
`blind`, `ranges`, `interior`, `interior_far`). Rate suites compare statistically. Visual scenes: `stealthview`,
`stealthself`, `radialmodes`.

## Not matched yet

- A player's own checks (the original's player pass and dice cadence are not measured; OpenWinter keeps its 0.1 s
  player pass and the 6 s dice).
- Moving observers and hiders at turns and starts (above), a player leaving the area, rooms that the tileset
  connects beyond the neighbouring tiles.
- Facing (NPC observer, player hider), favoured enemy, the exterior wall-thickness obstruction penalty, blindsight
  feats, the forced pass when Hide in Plain Sight starts, Sanctuary-pacified observers' close-range hearing: not
  measured, not implemented.
- Attacking a bandit from stealth, the original printed `Bandit : Immune to Sneak Attacks.`; OpenWinter rolls a
  sneak attack.
- In OpenWinter a plot creature refuses skill decreases, so `stealthview` clears plot on its NPCs; the original gives
  the same readings with and without plot.
- Passive Search range and the half-skill passive Search rule (players stay in detect mode in the original).
- Torches as light sources (only the light visual effects are measured).
- The detect-mode keyboard toggle is not measured; the radial is (scene `radialmodes`).
- Sanctuary against See Invisibility or True Seeing (a saved observer's "invisible" warded creature).
- Party members' view of each other in stealth (multiplayer).
