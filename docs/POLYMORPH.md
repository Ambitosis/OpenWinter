# Polymorph and shapechange

How `EffectPolymorph` and `polymorph.2da` work in OpenWinter, checked against the original. Rules were measured on
nwserver with the parity suites `fx_poly`, `fx_poly_hp`, `fx_polyitems`, `fx_poly_abilities`, `fx_poly_death` and
`fx_poly_scripts` (`dotnet run --project src/OpenWinter.Tools -c Release -- parity-run <suite>`); what the player
sees on nwmain with the visual scenes `polymorphpc` and `polymorphpc2` (`python tools/visual/visual.py run <scene>`),
both of which also run on OpenWinter with identical logged readings.

Code: `src/OpenWinter.Game/Rules/EffectSystem.Polymorph.cs` (the form), `CastSpellAction` (casting),
`HitPoints.Kill` (EffectDeath), `AbilityRequests.CancelPolymorph`, `SpellbookRequests` (the client's view),
`client/UI/Hud/Abilities.cs` and `RadialMenu.cs` (quickbar and radial).

## Engine and scripts

Most of what a player thinks of as "polymorph" is two halves. The engine (OpenWinter's C#) does whatever
`EffectPolymorph` does; the stock spell scripts decide which form, for how long, with which visual, and merge the
creature's item properties into the form's items. OpenWinter runs the stock scripts unchanged.

| Part | Side | Notes |
|---|---|---|
| Which `polymorph.2da` row, duration, metamagic | scripts | `nw_s0_polyself` (387-391), `nw_s0_shapechg` (392-396), `nw_s0_tenstrans` (row 28), `nw_s2_wildshape` (401-405, dire forms from druid 12), `nw_s2_elemshape` (397-400, elder from druid 20), `x2_s2_gwildshp` (Greater Wildshape I-IV, Undead/Dragon/Outsider/Construct Shape), `x2_s2_wildshape` and `x2_s2_wildshpedk` |
| The start visual | scripts | VFX_IMP_POLYMORPH (VFX_FNF_SUMMON_MONSTER_3 for Shapechange) |
| `ClearAllActions` "prevents an exploit", Shapechange's 0.5 s delay | scripts | |
| MergeW / MergeI / MergeA | scripts | `IPWildShapeCopyItemProperties` (x2_inc_itemprop) copies the weapon's properties onto the form's weapon (if both are melee or both ranged), armour, helmet and shield onto the hide (MergeA), rings, amulet, cloak, boots and belt onto the hide (MergeI). The scripts read the old items before applying the effect and the new ones straight after, so the engine must swap the items synchronously. The engine itself merges nothing. |
| Shifter ability limits | scripts | `x2_inc_shifter` keeps `X2_GWILDSHP_LIMIT_<spell>` locals; the ability scripts (`x2_s1_driderdark`, `x2_s1_petrgaze` ...) refuse with TLK 83576 when they run out |
| Not while mounted | scripts | `x3_inc_horse`, TLK 111982, unless `X3_NO_SHAPESHIFT_SPELL_CHECK` |
| Tenser's Transformation | scripts | players only (an NPC logs and returns); links the polymorph with attack, Fortitude, extra attacks and a visual, and adds temporary hit points |
| Craft skills refused while polymorphed | scripts | `x2_im_polymorphd` |
| Everything else in this document | engine | |

`fx_poly_scripts` runs each stock script on dressed NPCs (cheat casts) and compares the forms, merged properties,
durations, use-limit locals and the darkness area of effect, all equal.

## The form (engine)

- **Columns**: AppearanceType, RacialType, PortraitId (the portrait's `po_` + BaseResRef), SoundSet (when set; a
  form without one keeps the creature's), size from the appearance, STR/CON/DEX as base scores (racial adjustments
  are the form race's), NATURALACBONUS, HPBONUS as hidden temporary hit points linked to the polymorph.
- **Items**: everything equipped leaves its slot (still possessed, in no slot, not in the backpack, no events).
  CreatureWeapon1-3 go in the creature weapon slots, HideItem in the creature armour slot and EQUIPPED in the right
  hand. **A form with an EQUIPPED weapon gets no creature weapons** (Rakshasa's bite, the frost giant's slam).
  Nothing can be equipped or unequipped while polymorphed.
- **SPELL1-3** are not spells the creature has: GetHasSpell is 0 for NPCs and for the player, and neither can cast
  them through `ActionCastSpellAtObject`. For a player they are what the radial's Cast Spell wedge offers: SPELL1
  NE, SPELL2 E, SPELL3 SE, nothing else (a bear's Cast Spell wedge is empty). Chosen there they go off without a
  slot or a count and can be used again (the mind flayer's Psionic Inertial Barrier twice: the same three effects,
  spell id 741), and the player reads the spell's AltMessage ("000 Polymorph Probe Two uses psionic ability.") as
  it goes off. OpenWinter: `formAbilities` in the spellbook feed, `cast-spell` with `form`, `CastSpellAction.FormAbility`.
- **Casting**: a polymorphed NPC still casts the spells it has (from scripts), and doesn't use them up (`bear102`:
  Mage Armor lands, GetHasSpell stays 2, also after the form). A polymorphed player casts nothing from her
  spellbook, silently: assigned casts of her Mage Armor and of the form's Dispel Magic did nothing, with no chat
  line. Items (potions) and an NPC's own special abilities still work.
- **Levelling**: LevelUpHenchman still levels a polymorphed NPC; a polymorphed player's level-up is refused with
  TLK 112227 "You cannot level up while polymorphed."
- **Carrying capacity** uses the form's Strength (173 lbs for the Rakshasa's 17 where her 10 gave 86).
- **Stacking**: a new polymorph replaces the old one at once (the old form's items go, the creature's own stay put
  away).

## Ending

- **RemoveEffect** (and a script's removal): the effect goes at once, the creature changes back 0.5 s later; its
  bonus hit points last until then (18/8 0.2 s after RemoveEffect on a bear, 5/5 at 0.8 s).
- **Running out, dispel and the player's Cancel Polymorph**: the effect stays listed (second integer set, bonus
  hit points still there) for 0.5 s, then the creature changes back. Cancel was measured on the player: 0.5 s
  after the radial wedge she still had the form and her 35 hit points.
- **The change-back visual**: the engine plays VFX_IMP_POLYMORPH (the green columns) on the creature the moment
  the form ends, before the model changes. No script asks for it. Starting a form shows no visual of its own
  (scripts add it). Death and resting: assumed the same, not measured.
- **Death and resting** change the creature back at once. **Hit points** move with the maximum when the form set
  a Constitution (fx_poly_hp).
- **EffectDeath** leaves -11 hit points. A polymorphed creature whose remaining form bonus hit points would carry
  it above 0 from there (12 or more left) survives: changed back, at 1 hit point. With 11 left it dies; temporary
  hit points from other effects don't count (fx_poly_death). So a drow form (30 bonus hit points) survives
  EffectDeath where a bear (10) does not.
- **Saving**: copies, SQL and campaign objects and character files hold the creature in its own form with its own
  equipment and no polymorph. ExportSingleCharacter of the player as a bear at 8/8 wrote her own appearance, race,
  portrait, sound set and robe, and 5/5 hit points (the move back from the form), and she read "Your character was
  exported successfully." OpenWinter save games are full snapshots and keep the form (and the put-away items).

## What the player sees (nwmain)

| | The original | OpenWinter |
|---|---|---|
| Quickbar | Not swapped. Her spells stay, greyed, with their counts; items and feats stay usable. A greyed spell's key does nothing, with no message. | Same (`AbilityBook.Polymorphed`, from the spellbook's `polymorphed`) |
| Radial | Cancel Polymorph is the self ring's south wedge. Cast Spell (NE) offers the form's SPELL1-3 at NE, E and SE instead of her spells (before: Spellcasting Abilities W, wizard book SW). | Same |
| Portrait icons | The polymorph icon, then the temporary hit points icon when the form has HPBONUS (Rakshasa: both; water elemental: one) | Same |
| Portrait | The form's | Same |
| Inventory | The form's weapon in the right hand, other slots empty, the backpack unchanged; creature slots aren't shown | Same |
| Character sheet | The form's race, abilities, AC; HP with the bonus (35/6) | Same |
| Chat | "Armor/Shield Applies: Skill Armor Check Penalty: 0 Arcane Spell Failure: 0%" in yellow as the form starts and again as it ends | Same (sign of a non-zero penalty not seen) |
| Start | Model changes at once (the staff appears about 1 s later) | Model changes at once |
| End | Green columns at once, model changes 0.5 s later | Same; the white cloud is brighter and lasts longer (emitter calibration, [EMITTERS.md](EMITTERS.md)) |

## Not matched or not measured

- The change-back visual on death and resting, and other players' view of a polymorph (and whether others read
  a form ability's AltMessage).
- Character file hit points come from the own form's Constitution with race and form swapped back; item
  Constitution bonuses of the put-away equipment are not counted in that estimate.
- Area transitions with a form: the form is object state and travels with the player; not compared on nwmain.

Also matched: while polymorphed the radial's Special Abilities W wedge is stealth mode with Move Silently's icon; the
character sheet lists the form weapon's dice, the Strength modifier and a line per damage property and the
enhancement ("1-6 + 3", "+1d10 Fire Damage", "+5 Bludgeoning Damage"); GetHasSpell counts special abilities once.
