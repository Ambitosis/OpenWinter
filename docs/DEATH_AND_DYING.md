# Death, dying and resting

How a player drops, bleeds, dies, comes back and rests, measured black-box on the original. Rest presentation
(sitcross, the Resting panel, effect cleanup at the start of rest) is in [REST_BEHAVIOR.md](REST_BEHAVIOR.md). This
page covers the rules around it.

## Engine or script

Most of what a player sees around death comes from the module's event scripts. OpenWinter runs those scripts
unchanged, so only the engine column is OpenWinter code.

| Engine (native) | Scripts (stock or module) |
|---|---|
| A player at 0 to -9 hit points is dying: lies in the death pose, uncommandable, `GetIsDead` TRUE, takes no move orders | `nw_o0_dying` (OnPlayerDying in every official module) kills at once with `EffectDeath`, so the stock game has no bleeding |
| OnPlayerDying fires when a player drops from above 0 to 0..-9, and fires again each time | Bleeding systems (persistent worlds) are module scripts. No stock script uses `EffectHitPointChangeWhenDying` |
| Death at -10 or below; a dead player reads -11; OnPlayerDeath fires; the death cry and pose | `nw_o0_death` / `x0_o0_death`: reputation repair, then `PopUpGUIPanel(GUI_PANEL_PLAYER_DEATH)` 2.5 s later. `x1_chp1_death`, `x2_death`: `PopUpDeathGUIPanel` with campaign strings |
| The death panel (`dth_deathopts`) and its buttons; Respawn fires OnPlayerRespawn | `nw_o0_respawn` / `x0_o0_respawn` / `m1q0_respawn` / `x2_respawn`: resurrect, heal, `RemoveEffects`, the penalty (50 XP per hit die, no level loss; 10% gold up to 10,000) only when sent to `NW_DEATH_TEMPLE`, floating "XP Loss" / "GP Loss" |
| `EffectHitPointChangeWhenDying` (see below), EffectHeal, EffectResurrection, the healer's kit | Raise Dead / Resurrection spell scripts (`nw_s0_raisdead`, ...) apply `EffectResurrection` and healing |
| The rest action: refusals, duration (`restduration.2da` by hit dice), cancellation, recovery, OnPlayerRest STARTED / FINISHED / CANCELLED | `x2_mod_def_rest` with `x2_inc_restsys`: wandering monsters from the area's `X2_WM_ENCOUNTERTABLE` (only with `MODULE_SWITCH_USE_XP2_RESTSYSTEM`), no rest while mounted. `x1_playerrest`: unsafe areas at core rules and above. `m1q0_rest`: Prelude bookkeeping |
| "Resting." / "Done resting." / "Cancelled Rest." / the refusal lines | Anything a module's rest script says or does (`FadeToBlack`, `NWNX_Player_SetRestDuration` ...) |

## Dying

Measured on nwmain (scenes `dying`, `dyingbleed`, `dyingfx`, `dyingfoe`) with a 5 hit point character and module
events that only log:
- Damage to -3: `GetCommandable` 0 at once, current action 65535, `GetIsDead` 1, OnPlayerDying once. She lies on her
  back in the death pose (`a_dying`). Nothing bleeds by itself.
- `ActionMoveToLocation` assigned while dying: she stays where she is (an uncommandable living creature does walk).
- More damage while dying lowers hit points with no new OnPlayerDying. Healing 5 from -3 gives 2, commandable again,
  and she stands up.
- An enemy told to attack a dying player keeps attacking and hitting her. The stock AI chooses its targets with
  `GetIsDead`, which is TRUE for the dying.
- Dying to -10 gives -11; `EffectDeath` from above 0 gives -11.
- The chat log shows only the damage and heal lines ("Aluvian Darkstar : Healed 5 hit points."). On death a player
  with no creature behind it is told "Someone killed Aluvian Darkstar".

### EffectHitPointChangeWhenDying

It does not tick once per round as its description says:
- **Temporary:** on a dying player it adds its change once every *duration* seconds and stays on past its duration
  (+1 every 3 s with a 3 s duration; -1 every 2 s with 2 s, killing at -10).
- **Permanent:** nothing while it is on.
- **Removed by a script while the player is dying:** from the next update the change keeps landing, about once
  every 8 ms, until she is at 1 (a positive change; she stays uncommandable) or dead (a negative one). A change of
  0.5 runs as fast as 1.
- **Healed above 0:** the effect goes, and its change lands once more, so 6 of 5 hit points is possible.
- **Removed on the dead** (EffectDeath stripping it, or a script): its change lands once on the corpse (-11 to
  -12, -11 to -10). When its own tick killed, it goes without that last change.
- `GetEffectType` reads 0 for it. On an NPC it does not land at all (nwserver, `death_raise`).
- **Applied as instant** (the usual way; scene `bleedinst`): it becomes a temporary effect that scripts see (type 0,
  temporary) and ticks one hit point every |6 / change| seconds: -1 every 6 s, -2 every 3 s, -0.5 every 12 s, +2
  every 3 s up to 1, where it ends. Removing it while she is dying costs one more point just after the removing
  script, and it comes straight back with the same period (-4 to -5, then on 6 s later). A temporary 4 s one removed
  after 2 s does the same (-1 at once, then every 4 s). Healing ends it with one last change (raised from -6: 4 of 5).
  A death by its tick, or by the tick of its removal, leaves -12; a script's temporary one's own killing tick leaves
  -11.

These rules are fitted to the measured cases (`EffectSystem.DyingChangeRemoved`, `FromInstant`).

## The death panel

Measured on nwmain:
- `nw_o0_death` shows the panel 2.5 s after death. In single player it shows Load Game, Respawn and Exit Game, with
  "Choose an option below. Respawning will incur a penalty...".
- `PopUpDeathGUIPanel(pc, FALSE, TRUE, 0, "Custom...")`: Respawn is hidden without the others moving up. The custom
  text replaces the message. Single player still shows no Wait For Help.
- A raise (EffectResurrection) closes the panel. Shown to a living player, the panel stays up, and Respawn fires
  OnPlayerRespawn for her while she is alive.
- The stock respawn with a `NW_DEATH_TEMPLE` waypoint: hit points full, commandable, XP 1500 -> 1450, gold
  1000 -> 900. Chat: "Aluvian Darkstar : Healed 4 hit points.", "Experience Points Lost:  50", "Lost 100GP",
  then "XP Loss" and "GP Loss" from its floating text.

## Difficulty, PvP

The difficulty descriptions in `dialog.tlk` (67577 to 67581) say "Players can die" for Easy, Normal, D&D Hardcore
and Very Difficult. Only "DM Easy" (`GAME_DIFFICULTY_VERY_EASY`) says players cannot die, and EE's settings offer
difficulties 1 to 4 only (`ruleset.difficulty` values `[1, 2, 3, 4]`). So no selectable difficulty keeps a player
alive. At the default, 1 (Easy), an enemy hits a dying player for 1-2 and never critically, as Easy's "NPC does a
maximum 50% of damage" and "No critical hits on PCs" say. OpenWinter defaults to 1 too and applies the measured
difficulty rules to combat ([DIFFICULTY.md](DIFFICULTY.md)). PvP and hardcore settings change who can hurt whom, not
the dying and death rules.

## Resting

- **Refusals and lines** (nwmain, chat log): a hostile 25 m away gives "You cannot rest while there are enemies
  nearby." (66234); 35 m away rest starts ("Resting.", 7952). Cancelling gives "Cancelled Rest." (7957); a whole
  rest ends with "Done resting." (7953). A No Rest area gives "No resting is allowed in this area." (10492,
  `restnorest`) and no OnPlayerRest. None of these refusals fires OnPlayerRest.
- **Range on NPCs** (nwserver, `rest_recovery`): ActionRest is refused with an enemy 25 m away and goes ahead at
  35 m (`REST_ENEMY_CHECK_DISTANCE` 30). The suites need a module faction table for this (`rest_start_effects`): with
  Prelude's factions the original refuses the NPC's rest near the enemy, as OpenWinter does.
- **Recovery** (`rest_recovery`): ForceRest and ActionRest both give back hit points, the memorised spell that was
  cast (5 -> 6) and every turn undead use; NPC ActionRest finishes at once. A dead creature isn't brought back.
- **Duration and presentation**: `restduration.2da` by hit dice (10.5 s at level 1). The original shows no screen
  fade of its own; a module script may fade (`FadeToBlack`).
- **Wandering monsters** are scripted (`x2_inc_restsys`), off unless the module switch is set.

## Raising and healing the dead

Measured on nwserver (`death_raise`):
- Heal and Cure Light Wounds do nothing to a corpse. A healer's kit used on one is spent and heals nothing.
- An NPC cleric's cheat-cast Raise Dead or Resurrection at a corpse does nothing: the corpse is not "seen"
  (`GetObjectSeen` 0), and casting at an unseen creature fails. EffectResurrection itself is covered by
  `resurrection_flags`.

## Corpses and what they drop

Measured on nwserver (`corpse_drops`, `corpse_flags`) and nwmain (scenes `pavelroom`, `pavelarrive`, `pavelgobs`,
`pcdropflag`, `blacklakethugs`).

How long a body lasts:
- A destroyable corpse lasts its `DecayTime` (ms) but never less than five seconds: DecayTime 0, 1000, 5000 and no
  field at all are all gone between 5 and 5.5 s, 20000 at 20.5 s. Every creature in the official campaigns has
  DecayTime 0, so they all lie five seconds. The original's save games write 5000 for such a creature. Neither the AI
  level, a player in the area nor a droppable item changes this.
- `SetIsDestroyable(FALSE)` before death, or a blueprint's `IsDestroyable` 0, keeps the corpse for good. This is what
  the Prelude's corpses are: Pavel's room (m0q1d) has nine `m1q0ddeadgob` goblins placed in the area whose spawn
  script queues `SetIsDestroyable(FALSE)` and then `EffectDeath` on itself. They are dead at -11 and not destroyable
  before the player first comes in, and stay.
- The original's save game keeps no dead flag: such a corpse is saved with `CurrentHitPoints` -11, `IsDestroyable`
  0, `DeadSelectable` 0. A creature at -11 or fewer in a save is dead.
- A blueprint's `Lootable` 1 makes the body selectable when dead and not raiseable (`GetIsSelectableWhenDead` 1,
  `GetIsRaiseable` 0) unless the creature says otherwise. A lootable body with something droppable stays and opens
  as an inventory; with nothing droppable it is an ordinary corpse and goes after five seconds with no bag.
- A killing blow that leaves -11 hit points or fewer ends the body in three seconds, as a spectacular death does;
  one that leaves -1 to -10 gives the usual five.

What drops (the bag holds exactly the droppable, non-plot items, equipped or carried):
- Items in a creature's blueprint lists (`Equip_ItemList` `EquippedRes`, `ItemList` `InventoryRes`) are droppable
  only when the entry has the toolset's `Dropable` 1, and pickpocketable only with `Pickpocketable` 1. An entry
  without them is neither. The same for items embedded in a creature placed in an area (the GIT), for its copies
  (CopyObject keeps each item's flags) and for encounter or CreateObject creatures (they come from the blueprint).
  The campaigns tick Droppable on very few entries (Chapter 1: 6 of 525 blueprint equipment entries), so most
  monsters drop only what scripts gave them. The original's saves write both fields on every item.
- An item made on the ground (`CreateObject`) reads 0 and 0. `CreateItemOnObject` gives droppable 1, pickpocketable
  0 (on a creature or in a chest); `CopyItem` gives 1 and 1 whatever the original was. A creature that isn't a player
  keeps an item's flags when it picks it up, is given it or takes it from a container.
- A player taking an item from anywhere makes it droppable and pickpocketable (a ground item at 0 and 0 reads 1 and 1
  once picked up; CreateItemOnObject on a player gives 1 and 1). `SetDroppableFlag(FALSE)` after that sticks, and a
  player can still `ActionPutDownItem` such an item.
- `GetIsCreatureDisarmable` is 0 when the right-hand weapon isn't droppable (the Blacklake thug's club, the
  Prelude goblin's dagger).
- Blacklake's diseased thugs (`nw_disthug`, `nw_disthug001`, placed in map_m1q3i/k/l/o and in its `NW_Disthug`
  encounter) carry clothes and a club with no Dropable: killed, they leave no bag.

## Highlighting corpses

Measured on nwmain (scene `corpsehilite`) in Pavel's room with its dead, unselectable goblins, plus goblins killed in
the scene (selectable when dead, lootable, and ordinary):
- Holding the highlight key (Tab) lights up every dead creature, unselectable ones too, in the creature's reaction
  colour (hostile red, neutral blue) with the creature's name label and "Dead" under it ("Dead Goblin / Dead",
  portrait included in text bubble mode 2). An ordinary body is lit and named during its five seconds. Dead
  placeables stay dark.
- Corpses also highlight under the pointer, even unselectable ones. OpenWinter draws the same highlight and label as
  under Tab.

## OpenWinter

- `HitPoints` (Rules/EffectHitPoints.cs): dropping to dying clears the queue and makes the player uncommandable.
  A heal from 0 or below gives control back and ends hit point change when dying effects. EffectResurrection gives
  control back and closes the death panel. A dead player is set to -11 or lower.
- `EffectSystem`: the hit point change when dying rules above (temporary interval, removal run-on, once on the
  healed or dead). EffectHeal tells "X : Healed N hit points." (62504).
- `Creature.IsDying`; `ObjectFlags.Dying` goes to every client, and both interfaces lie the player down in the
  death pose while it is set, so other players see it too. Dying players can't drive or take move orders.
- Respawn (BuiltinRequests): accepted only while a death panel with its Respawn button is up
  (`PlayerSession.DeathPanelRespawn`), dead or alive.
- KillRewards: "Someone killed X" to a player killed with no creature behind it. Gold scripts tell "Acquired NGP" /
  "Lost NGP". RestAction uses the original's lines from the talk table; the classic interface drops the modern
  rest chat lines.
- Healer's kit on a corpse: spent, nothing healed.
- Corpse highlight (`client/Gameplay/Logic/PickRules.cs`, `Interaction`): the pointer and the highlight key light up and
  name every dead creature (the server's `ObjectFlags.Dead`), selectable or not; clicks, the radial, targeting and the
  modern target frame still take only selectable objects (`ObjectFlags.Selectable`), so a click on an unselectable
  corpse goes to the ground under it. The cursor over a corpse is the default one. Both interfaces share this.
- `CorpseSystem`: corpse lifetime max(5 s, DecayTime) (3 s after a spectacular death or a blow to -11 or below);
  lootable bodies with nothing to drop decay as ordinary corpses. `ItemFactory`: an item without Dropable /
  Pickpocketable is neither; `InventorySystem`: CreateItemOnObject makes it droppable, CopyItem both, a player taking
  it both; a player's own items without the fields (a new character's, OpenWinter-written characters) are both at
  login. `NativeSaveGame`: a creature saved at -11 or below loads dead. `CreatureStats`/`CreatureRules`: a blueprint's
  Lootable makes the body selectable and not raiseable unless the fields say otherwise.

## Testing

- Unit tests: `DeathAndDyingTests`, `CorpseDropTests`, `CorpseHighlightTests`.
- Server suites: `rest_recovery`, `death_raise`, `corpse_drops`, `corpse_flags`, `resurrection_flags`
  (`dotnet run --project src/OpenWinter.Tools -c Release -- parity-run <suite>`).
- Visual scenes on both engines: `dying`, `dyingbleed`, `dyingfx`, `dyingfoe`, `bleedinst`, `restnorest`,
  `corpsehilite`, `pavelroom`, `pcdropflag` (`python tools/visual/visual.py run <scene>`).

## Not matched yet

- In the original, what a click, a right-click (radial) or a targeting mode does on a corpse that isn't selectable
  when dead, and the exact cursor, colour and label shown while hovering any corpse, are not measured. OpenWinter: the
  default cursor, the Tab highlight and label, clicks go to the ground beneath.
- Whether a player can drop an undroppable item through the interface (OpenWinter refuses, as for barter); the
  original's script put-down works. Store and barter transfers between players are not measured for the flags
  (OpenWinter treats them as a player taking the item).
- Whether an overkilled body also looks different as it goes (the original's three-second end is only timed).
- Other players' view of a dying or dead player in the original (needs two clients). OpenWinter shows them the
  same pose.
- The original runs a removed permanent change about once per 8 ms. OpenWinter runs it at 125 changes a second
  spread over its updates, so the timing within those few frames differs.
- "Equipped item swapped out." three times about a second after the stock respawn (62079), from its jump. Not
  reproduced.
- The stock respawn's JumpToLocation is still in progress 0.5 s later in the original (action 36); OpenWinter jumps
  at once.
- What a dying player sees and hears beyond the pose (any sound, how the get-up looks frame by frame). Rest refused
  for being in combat (OpenWinter says the enemies line).
