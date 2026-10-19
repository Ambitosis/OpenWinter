# Encumbrance

What a player character's load does to her, measured on the original and implemented on the server
(`src/OpenWinter.Game/Inventory/EncumbranceSystem.cs`, `Movement.MaxSpeed`). The encumbrance voice cue is in
[VOICES.md](VOICES.md).

## Engine or scripts

All of it is native. No stock script takes part: the weights come from `baseitems.2da` (TenthLBS), the limits from
`encumbrance.2da`, the movement stages from `ruleset.2da`, and the lines from `dialog.tlk`. Scripts only see it
through GetWeight.

## Measured

Black-box, with a Strength 10 test character (Aluvian Darkstar: `encumbrance.2da` row 10, Normal 860 and Heavy 1100 tenths of a
pound) in the visual scenes `encumberspeed`, `encumberdrive` and `encmodes` on `nwmain` (`python tools/visual/visual.py run
<scene>`), and the parity suite `encumbrance_speed` on `nwserver`.

- **Limits.** Encumbered above Normal (861 tenths, not 860), overloaded above Heavy (1101, not 1100). Strength
  counts with its effects: raised to 14 by EffectAbilityIncrease she carries 1000 unencumbered (Normal 1160), lowered
  to 6 she is encumbered at 650 and overloaded at 701.
- **Speed.** A player runs at 4 and walks at 2 m/s. Encumbered she moves at 2 (running is walking), overloaded at 1,
  whether she asked to run or to walk, by keyboard (W held) or by ActionMoveToLocation. Five times the Heavy limit is
  no slower than just over it. The change is immediate: weight added while she runs slows her from the next 0.5 s
  sample.
- **Stages add up.** ruleset.2da has MOVEMENT_STAGE_PENALTY_ENCUMBRANCE_HEAVY 1, _OVERLOADED 2, _STEALTH_MODE 1 and
  _DETECT_MODE 1. In stealth: unloaded 2, encumbered 1, overloaded 0.5 m/s. So stage 0 runs, stage 1 walks and each
  further stage halves the walk. (Aluvian's Keen Sense keeps detect mode from costing a stage.)
  Detect mode switched on costs its own stage beside stealth's (visual scene `encmodes`, Aluvian with Keen Sense
  removed): detect alone or stealth alone 2, both 1; encumbered with both 0.5; overloaded with both 0.25 m/s, an
  exact eighth of the walk. Overloaded, she still picks up a dagger from the ground: nothing in picking up checks the
  load.
- **Effects scale the result:** hasted, encumbered 3 and overloaded 1.5; EffectMovementSpeedDecrease(50), encumbered 1
  and overloaded 0.5; EffectMovementSpeedIncrease(50), overloaded 1.5.
- **Lines.** "You are encumbered and cannot run." (dialog.tlk 6632) on becoming encumbered, from normal or from
  overloaded; "You are encumbered and cannot walk at normal speed." (6633) on becoming overloaded. Nothing on
  returning to normal (and nothing at 1000 with Strength raised to 14). Each item counts: loading item
  by item from nothing to overloaded prints both lines, unloading from overloaded prints 6632 on the way down. A
  filled box picked up straight past both limits gives both lines.
- **The plot flag.** A plot player is told but not slowed (full speed at every load). Made plot only after she was slowed, she stays slowed: the slowing is fixed when her state changes.
- **Other creatures:** nothing. GetWeight reads 0 for any creature that is not a player (the items are there: the
  suite sums them), and a goblin carrying five times its Heavy limit walks and runs at its full speed.
- **Display.** The inventory shows the carried weight over the Normal limit, in whole pounds ("86 / 86 lbs." at 861,
  "110 / 86" at 1095), white at every load. The character record shows no weight.

## Implemented

- `ItemRules.CarryStrength` is the Strength score with effects (a polymorph form's Strength while polymorphed).
- `EncumbranceSystem` keeps each player's load state and movement stage. Inventory changes update it at once
  (after each change, through `InventorySystem.VoiceLoadChanged`); its `Step` catches Strength changes. On entering
  state 1 or 2 it sends the line (both lines when a change goes straight from normal to overloaded). The stage is
  fixed then, and is 0 for a plot player.
- `Movement.MaxSpeed`: stage = max(asked to walk, slowed, force-walked or dazed ? 1 : 0, stealth/detect (1) + load stage);
  speed = run rate at stage 0, else walk rate × 0.5^(stage − 1), times the effects' factor. NPCs have no load stage.
- Tests: `EncumbranceMovementTests` (lines, stages, stealth, a pick-up past both limits, NPCs, Strength effects,
  haste, plot). The parity suite `encumbrance_speed` matches.

## Not matched yet

- The order of the line among "Acquired Item" lines: the original sometimes prints it before the acquisitions of the
  same script; OpenWinter prints it right after the item that crossed the limit.
- "You are encumbered and cannot pick up anything else." (6634) was never printed, even at five times the Heavy
  limit or picking up while overloaded; what triggers it is unknown.
- What a player who logs in already encumbered is told: OpenWinter sets her stage silently.
- A possessed familiar's or a DM avatar's load.
