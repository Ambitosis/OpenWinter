# Traps

Trap triggers (trigger type 2), trapped doors and trapped placeables: what the engine does, what the stock scripts
do, and how the original behaves. Code: `src/OpenWinter.Game/WorldObjects/TrapSystem.cs` (springing,
detection bookkeeping, disarming, making traps), `Skills/SkillSystem.Traps.cs` (Search, Disable Trap, Set Trap),
`Skills/UseSkillAction.cs` (the 4.5 s at the trap), `Scripting/Functions/TrapFunctions.cs`, and on the client
`client/Views/WorldFx/TrapFx.cs` (how a seen trap looks) and `client/UI/Hud/RadialMenu.cs` (`OpenTrap`).

## Engine and scripts

| Scripted (stock scripts that ship with the game, run unchanged) | Native (OpenWinter's engine) |
| --- | --- |
| What a trap does when it goes off: `traps.2da` TrapScript (`nw_t1_*`, `x2_t1_*`), e.g. `nw_t1_spikeminoc` calls `DoTrapSpike(d6(2))` from `x0_i0_spells`; the gas traps make an area of effect (`nw_t1_gasminoc1` ...). An object's own OnTrapTriggered replaces it. | Who sets a trap off, and when; running OnTrapTriggered (else the `traps.2da` script) with `GetEnteringObject()` = the victim; one-shot traps going away; "You triggered a Trap!". |
| OnDisarm, OnEnter on trap triggers, module reactions. | Search: who searches, how often, how far, the checks, and who else learns of a find. |
| | Disable Trap (disarm, examine, flag, recover) and Set Trap: DCs, take 20, critical failures, 4.5 s at the trap, kits, the chat lines, the examine window. |
| | All trap script functions (CreateTrapAtLocation, GetTrapDetectedBy, SetTrapDisabled ...). |
| | What each player sees: the tinted trap square or object, its colour, the radial menu. |

## Rules (measured)

### Going off
- A trap trigger notices only creatures it goes off for (`World.Triggers`, `TrapSystem.SidesClash`, `NpcClash`).
  - Player side (a player, its associates, its party): one of trap and creature on it, the other hostile to players.
    A player or henchman sets off a hostile trap, not a commoner one (traplook `COMMONER_TRAP`); a hostile creature
    sets off the player's trap, a commoner does not (traprules `SET`, traplook).
  - Between NPCs (`trap_rules_npc` and `trap_trigger` with Prelude's faction table): a creature of another
    faction, not immune to traps, that rates the trap's setter below 90, or with the setter gone the trap's faction.
    Kit traps and scripted trap triggers went off for walkers rated 0 or 50, not for their own faction or one rated
    100, and not when the setter had joined the walker's faction. The direction is the walker's view: in Prelude's table Defender rates Commoner 100 and Commoner rates Defender 50, and a Defender
    trap went off for a Commoner walker while a Commoner trap didn't for a Defender one (`trap_trigger`). For a living
    setter the direction is not measured. (Without a faction table every faction likes every other on nwserver, so
    no NPC sets any trap off.)
  Creatures it doesn't notice get no OnEnter either.
- For one that sets it off, OnTrapTriggered runs first, then OnEnter, both with GetIsTrapped still TRUE; a one-shot
  trap trigger is then destroyed (traprules `WALK`, `D5`). A recurring one fires on every entry.
- Players walk straight across traps they have detected (traprules track: no detour round `D5`).
- A trapped door goes off for whoever opens it, NPC or player, whatever the door's faction, and stays shut
  (`trap_trigger` `T_DOOR*`). A locked trapped door only fires OnFailToOpen.
- A trapped container goes off when a player opens it (traprules `C_OPEN`). An NPC using it (ActionInteractObject or
  DoPlaceableObjectAction) sets it off when the container's faction rates the NPC below 90, as for a botched disarm,
  and then runs no OnUsed; otherwise it just fires OnUsed, the chest still shut (`trap_trigger` `T_CHEST*` with
  Prelude's factions: a Hostile chest with a Hostile trap went off for Commoners, not for a Hostile user).
- `CreateTrapAtLocation`'s size is half the side: a 2.0 trap is a 4 m square (traprules: the PC set off `D5` 1.9 m
  off its centre; the screenshots measure 4.06 m). It is square to the area axes and reports facing 0 whatever the
  location's facing.

### Detection
- NPCs search too (`trap_rules_npc`, `trap_detect` and `trap_npc` with Prelude's faction table; without
  one every faction likes every other and no NPC found anything): every 6 s within 3.33 m with a d10, in detect mode (or Keen
  Sense) every 3 s within 6.66 m with a d20, the full Search skill, for traps of another faction that rates them below
  90. For a trapped door or placeable the "same faction" test uses the trap's faction and the "friendly" test the
  object's own faction (`trap_crit_ctl`: a Commoner never found a Commoner trap on a Hostile chest,
  0 of 3, a Merchant did, 3 of 3). Standing still is not detect mode: idle NPCs found traps
  3 m from the edge, not 5 m; in detect mode 5 m, not 7 m; walkers 2.5 m off, not 5 m. Non-rogues found DC 35, not 36.
  An NPC's summon finding a trap tells its master however far off (masters 5.8 m and 14.6 m from the trap's edge knew
  it); an NPC of the same faction 15 m off didn't learn of it (`trap_npc`).
- A player found a DC 1 trap whose edge was 5.5 m away at once, but not one 7 m away, nor a chest 9.9 m away, in 10 s
  with +65 Search (traplook `R6`/`R8`/`RC10`). OpenWinter: 6 m from the edge in detect mode.
- Players start in detect mode (GetActionMode = 1), and `SetActionMode(pc, ACTION_MODE_DETECT, FALSE)` left it on, so
  the passive range and the half-skill rule couldn't be measured. The game's own descriptions are kept: passive uses
  half the skill, Keen Sense the full skill, and only rogues find traps above DC 35.
- What the player or a henchman finds, both know at the same moment (traprules `D5`, `D9`, the second with the
  henchman 7 m from the trap and 16 m from the player).
- `GetLastTrapDetected` stayed invalid after every find and after SetTrapDetectedBy.
- `SetTrapDetectedBy` returns TRUE for any door, placeable or trigger with a creature, but FALSE when clearing a trap
  the creature hadn't detected and when the detector isn't a creature.
- `GetTrapDetectedBy` doesn't count flagging.
- Disarming clears every creature's detection.

### Disable Trap (`trap_disarm`, traprules, traplook)
- DC = disarm DC - 7 to examine, - 5 to flag, + 10 to recover (never below 1), with +2 for 5 ranks of Set Trap.
- Out of combat the check takes 20; in combat it rolls.
- A taken 20 that can't reach the DC prints `*success not possible*` and never sets the trap off.
- A rolled disarm that misses by 11 or more is a `*critical failure*`: "You triggered a Trap!" and the trap goes off
  on the player (traplook `C_FIGHT`, missed by 20) or NPC (`trap_crit_ctl`, Prelude factions,
  disarmers kept in combat by scripted damage): misses by 14 to 33 set off a Hostile trap trigger on Commoners 10 of 10
  and a Hostile `plc_chest1`'s trap 10 of 10; skill 12 against DC skill + 17 set it off 16 times in 72 (rolls 1-4: 20%;
  a threshold of 5 would give 45%). The trap must be able to fire on the disarmer: never its own faction (0 of 6), and
  for a placeable the placeable's own faction's view counts (a Hostile disarmer at a Hostile chest with a Defender
  trap: 0 of 6). **A trap never goes off on a plot creature**, walking in (0 of 4) or disarming (0 of 12).
- Scripted damage keeps a creature in combat even from a plot source and even on a plot target that loses nothing
  (`trap_crit_ctl`: `GetIsInCombat` 1 at the end of every disarm).
- Skill Mastery takes 20 in combat; only base Set Trap ranks give the +2 (an effect of +10 didn't); the trap's
  creator disarms its own trap at any DC (`trap_rules_npc`).
- Only rogues disarm traps above DC 35 (a fighter disarmed 35, not 36). Flagging and recovering a DC 36 trap are open
  to anyone.
- Untrained creatures can't; Disarmable FALSE stops disarm and recover; Recoverable FALSE doesn't stop an NPC's
  recover; the trap need not have been detected.
- Each use runs to the trap (7.2 m in about 2.6 s) and stops at its own distance:
  - 1.78 m from a chest's centre;
  - close to a door;
  - for a trap trigger, a creature 1 m outside the edge stays put.
  It then kneels and resolves 4.5 s later (ruleset `SKILL_*_TRAP_DURATION`) plus about 0.2 s (4.68 s from a creature already in reach; native runs vary by about 0.3 s).
- Recover puts the `traps.2da` ResRef kit in the inventory ("Acquired Item" before the check line). Examine opens the
  examine window: the trap's name and icon, "Difficulty Level: Effortless" (ASSESSTRAP bands) and "This trap is a
  complicated mass of cogs and gears.". Flag makes everyone see the trap. Disarming, recovering, setting and examining
  gave no experience.
- OnDisarm fires for disarm and recover with GetLastDisarmed() = the disarmer.
  - A trap trigger is still armed during OnDisarm and is destroyed a moment later.
  - A door or placeable is disarmed first.
  `SetTrapDisabled` does the same on the event queue (OnDisarm with GetLastDisarmed() = the caller).

### Set Trap (`trap_set`, traprules)
- Only the kit sets traps: `ActionUseItemOnObject` (door or placeable) and `ActionUseItemAtLocation`.
  `ActionUseSkill(SKILL_SET_TRAP)` does nothing.
- The check against `traps.2da` SetDC takes 20 out of combat, with +2 for 5 ranks of Disable Trap.
- Untrained: nothing happens. A target already trapped ends it on arrival. Failure keeps the kit.
- The trap's DCs are DetectDCMod and DisarmDCMod plus the setter's Set Trap bonus, with no roll (rank 30 + 2: 42 and
  54).
- On the ground it is a 4 m square at the spot, facing 0, with no tag or name, of the setter's faction (a player's:
  its party's) and with the setter as creator.
- The setter's party sees it, but GetTrapDetectedBy stays FALSE.
- The chat shows "Lost Item: <kit>", then "Take 20 : <name> : Set Trap : *success* : (20 + 15 = 35 vs. DC: 5)".
- Failing by 10 or more in combat sets it off (from the description; not measured).

### Script functions (`trap_api`, 309 readings)
- Getters read the trap fields of any door, placeable or trigger, trapped or not: an untrapped `plc_chest1` has
  TrapType 0 and DisarmDC 15, and SetTrapDisabled keeps the type and DCs. On anything else they return -1 for the
  base type, TRUE for GetTrapActive and GetTrapRecoverable, and 0 or "" for the rest. Setters clamp DCs to 0-250
  and work on untrapped objects too.
- The original reads no TrapActive or TrapRecoverable field from an area or blueprint: a toolset trap starts active
  and recoverable.
- `CreateTrapAtLocation`:
  - fails (OBJECT_INVALID) below size 1.0 and for a type `traps.2da` lacks;
  - names the trigger after the trap (`traps.2da` TrapName, "Spike Trap, Minor");
  - has no creator, even when a creature runs it;
  - gives the trigger the trap's faction.
- `CreateTrapOnObject` does nothing to an object that is already trapped.
- An empty OnTrapTriggered falls back to the `traps.2da` script; DCs start at DetectDCMod / DisarmDCMod.

## Presentation (traplook, traprules)

- A trap trigger the player sees is drawn as the ground inside its square tinted, keeping the ground's detail as if
  multiplied:
  - red for a hostile trap, about x2.1 / x0.72 / x0.8 over grass (an inactive one too);
  - green for one the party set, about x0.82 / x2.25 / x0.9.
  There is no outline, grass blades inside take the tint, and tree canopies over it hide it. `TrapFx` draws a box
  whose shader multiplies the scene behind it, masked by the polygon and limited to the box by the scene depth.
- A detected trapped chest glows red.
- The player sees traps it detected, flagged traps, and its own party's traps. The server sends `trap-detected` with
  the polygon, name, own / flagged / active / disarmable / recoverable to exactly those players
  (`TrapSystem.Shows`/`Publish`), again on entering the area (`AreaStateSync`), and `trap-disarmed` when it goes.
- Right-clicking a detected trap trigger opens the four trap commands straight away: Examine north, Disable Trap east,
  Flag Trap south, Recover Trap west, the same icons as a chest's Trap submenu, with nothing in the centre. A left
  click on it did nothing (the player stayed put); there was no hover highlight on it.
- The area map at its default zoom showed nothing for traps.

## Testing

Parity suites (`dotnet run --project src/OpenWinter.Tools -c Release -- parity-run <suite>`): `trap_trigger`,
`trap_rules_npc`, `trap_detect`, `trap_npc`, `trap_disarm`, `trap_disarm_crit`, `trap_crit_ctl`, `trap_set` and
`trap_api`. Visual scenes (`python tools/visual/visual.py run <scene>`): `traprules` (walking in, detection, disarming,
setting) and `traplook` (how traps look, the radial, critical failures).

## Not matched or not measured
- Flagged traps' colour: the flagged trap was off-screen in every shot, so it is drawn red like a hostile one.
- Players' passive (non-detect-mode) Search range and half-skill rule (NPCs: above); elves' and dwarves' racial
  Search bonuses; another player party's traps; traps in multiplayer parties of several players; associates searching
  from their master's position.
- Set Trap's in-combat failure; a player's critical failure at misses of 11 to 19; a trapped door's critical failure
  (which reputation decides it).
- Trapped doors' look; a trap trigger's look at night; the hover cursor over a trap; trap markers on a zoomed map.
- The ActionDoCommand queued after ActionUseSkill runs before the trap state changes in the original (it reads
  GetIsTrapped TRUE); in OpenWinter the state has already changed.
- The damage name in the combat log for a stock trap ("Someone damages ..." here) was not compared.
- Trap state is not saved with a savegame beyond the placeable fields `ObjectSerializer` already writes.
