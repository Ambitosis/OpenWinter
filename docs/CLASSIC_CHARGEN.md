# Classic character creation and level-up

With the classic interface style (Options → Interface, or `--ui classic`), New Character on the selection screen opens
the original's character creation and the level-up buttons (party bar portrait, character record) open its level-up
window, both drawn from the game's own `cg_*.gui` layouts and interface models (`client/UI/Classic/Creation`). The
rules are the server's: every screen is another view of the same requests as the modern wizard (`chargen-data`,
`chargen-validate` → `chargen-plan`, `chargen-create`, `levelup-data` → `levelup-plan`, `levelup-apply`), and the
server checks the whole level again when it is taken.

The behaviour below was measured on the original game at 1280x720 by driving its main menu (New → Other Modules → the
`chargen` scene module → Create New Character) through every creation screen and three level-ups, with window
captures after each step (`tools/visual/menudrive.py`).

## Creation

**The main screen** (`cg_main` on `pnl_chr_genm`, its model origin on the middle of the screen, over black):
Gender, Race, Portrait, Class, Alignment, Abilities, Packages and Customize on the left, Play / Reset / Cancel under
them, and the character record (`pnl_chrsht2` on the panel's `CharacterSheet` node) on the right.
- Only the steps taken and the next one can be pressed; the next one's frame pulses (`encouraged`), the others are
  greyed. Play opens (and pulses) when all eight are taken.
- **OK on a step makes every later step to be taken again** (changing the alignment after Customize greys Packages,
  Customize and Play); the choices made there stay (the abilities remain as they were). Cancel changes nothing.
- The record fills as steps are taken: the unknown portrait (`gui_po_nwnlogo`) before the portrait step, the race,
  then ", <alignment>", the class line "<Class> (1)", the ability scores, AC and hit points once the abilities are
  taken. Its combat summary shows the saves **without the class's base saves** while creating (an elf wizard with
  Wisdom 12 reads Will 1, not 3), and damage as dice and bonus ("1-3 - 1").
- Its four tabs (Character, Skills, Feats, Spells: the in-game record's `ctl_chr_btn_tab1..4`) show the lists; before
  a row is chosen the description box shows the tab's name. Spells are listed by name across the levels.

**Steps** (each a screen with OK and Cancel; Recommended where the layout has it, hidden when the module turns the
button off with ruleset `CHARGEN_ENABLE_RECOMMENDED_BUTTON`):

| Step | Layout | Behaviour |
|---|---|---|
| Gender | `cg_sex` | Help: TLK 203 / 447, then the choice's own (445 / 199, 446 / 200). The selected button is the texture's blue picture |
| Race | `cg_race` | racialtypes row order; help title is the race's plural ("Humans"), text its description. Sub Race asks for the subrace in a text box |
| Portrait | `cg_portrait` | The race's portraits (portraits.2da Race / Sex, no plot ones) first, then other playable races' (the order of the rest is inferred); uninstalled ones are left out. 6 a row, 72 px apart, rows 107 px; the chosen one large on the left |
| Class | `cg_class` | Classes that can be taken by name, then prestige classes by name, greyed; a greyed class can be read about but OK greys. The general help (TLK 484) has an empty title until a class is clicked |
| Alignment | `cg_alignment` | Opens on the recommended alignment (Lawful Neutral for a wizard); alignments the class forbids are greyed. Help titles "Lawful Neutral \"Judge\"" (469 ...) |
| Abilities | `cg_attributes` | Scores with racial adjustments and modifiers ("8 (-1)"), Remaining Points, Cost To Increase of the ability last pressed (empty before). A spellcaster's casting ability starts at 11 (Intelligence for a wizard; other classes not measured). OK opens at 0 points left. Recommended: the class's classes.2da scores fitted to the points |
| Packages | `cg_package` | OK takes the package as it is and goes back to the main screen; Configure Packages walks through the level screens below with the package's choices as the start |
| Customize | `cg_appearance`, then `cg_char_info` | Head, Tattoo, Body, Clothing cycle with arrows; skin, hair and tattoo colours open the palette grid (`ctl_cg_colr_*`) with its bottom-left on the swatch; the model is dressed in the starting clothes. Character Info: a generated name (racialtypes NameGenTableA/B + m/f/l letter tables), the race's age and biography, the voice sets of the gender in soundset.2da order, Deity (a text box), Generate Name |

## The level screens (Configure Packages, and every level-up)

In the original's order: ability increase (every fourth level), skills, feats, school (specialist wizards) or
domains (clerics), spells, familiar or animal companion; a screen is skipped when its level has nothing to choose
(no feat screen for a wizard 2).
- **Skills** (`cg_skills`): every skill by name, "(Class skill)" after class skills, restricted ones greyed;
  ranks in a box (`ctl_cg_numbox3`) on the row's skillrank node; Remaining Points and the selected skill's Cost.
  The help shows Number of Ranks, Skill Modifier and Skill Check over the description.
- **Feats** (`cg_feats`): choosable feats by name with a → arrow, the master feats' choices under one row (its popup
  `cg_feats_popup`); the added list shows the feats the level grants (greyed, by name) then the chosen ones. A
  clicked row turns blue and shows its ? button, which opens the feat's information (`cg_spellinfo`: icon, name,
  description). OK opens when every feat is chosen or nothing is left to choose.
- **School** (`cg_school`): the package's school chosen when it opens (General).
- **Domains** (`cg_domain`): domains by name with → arrows; the description box names the domain clicked, the two
  chosen sit at its domain1 / domain2 nodes with ← arrows. Recommended picks the package's.
- **Spells** (`cg_spells`): level tabs with the level icons, opening on the highest level with spells to learn;
  available spells stay listed when none are left to choose; chosen ones by name; the automatic ones (a wizard's
  cantrips) greyed; "There are no more spells available at this level." when the list is empty, under the level's
  name.
- **Familiar / companion** (`cg_familiar`): hen_familiar / hen_companion row order, the record of the creature at
  the master's level beside them, a generated name (familiar / animal letter tables) in the name box. Offered again
  at every level of a class that grants the associate (a wizard 2, not a cleric level), the current one kept unless
  another is chosen. The record's combat box ends 10 px above OK.
- **The familiar's record** (measured on a level-up to wizard 2 for the bat, panther, hell hound, imp, fire and ice
  mephits, pixie and raven): the blueprint at the master's level (BASERESREF + level) gives the name line, race and
  alignment (also the box's "Alignment Title" and "Alignment: Neutral (50), Good (100)" for the pixie), the class
  line ("Rogue (3)" for the panther), the abilities and the hit points (bat 9/9, panther and hell hound 13/13, imp
  and mephits 7/7, raven 11/11). The rest is not the creature's: **AC 9 and Fortitude, Reflex and Will -1 for every
  one** (Dexterity 13 to 19, Outsiders' good saves: as a creature with 8 in each ability and no class saves), "Main
  Weapon: Unarmed" (its bite and hide not counted), no size modifier (the tiny bat's unarmed damage is 1-3), "Spell
  Resistance: 0". Attack bonus = base attack + Strength, or Dexterity with Weapon Finesse (bat +3 = 1 + 2; panther
  +6; hell hound +4; imp +5; fire mephit +2 = 2 + 0, Base Attack 2); damage 1-3 plus Strength ("1-3 - 4" for the
  bat). The server builds it so (`ChargenRequests.AssociateRecord`; `FamiliarRecordTests`); the animal companion
  gets the same (not measured).

**Hover** (measured on the level-up screens): the original shows no help for rows. Class, skill, feat, spell, domain
and familiar rows don't change under the pointer (`CgButton.Row`); buttons (Recommended, OK, the spell arrows) show
their `hilite` state. Two things do appear:
- A familiar row under the pointer shows its **?** (`ctl_cg_btn_help` at the row's `helpbutton`), which stays on the
  last row hovered. Pressing it selects the row and opens the creature's description (hen_familiar DESCRIPTION) in
  the scrolling message box (`okscrollpanel`) with OK.
- The record's tabs have **hover text** in the HUD's hover-text style (`ClassicTooltip`): "Character Sheet",
  "Skills", "Feats" (none over the spells tab), centred about 62 px right of the pointer and its top about 44 px
  below; the tab isn't switched.

## Level-up

Opened from the party bar's level-up portrait or the record's level-up button (classic style). While it is open,
single player is paused ("PAUSED" / "UNPAUSED" in red in the chat; the server keeps answering).
- **Class** (`cg_class`): classes that can be taken by name, then the rest by name, greyed (prestige classes whose
  requirements aren't met, classes the alignment forbids); the class of the last level selected, on the list's
  fifth row; Recommended keeps it.
- Screen titles carry the name and classes: "<Name>   Wizard (2)   Allocate Skill Points".
- **Ability increase** (`cg_levup_attrib`): one point, Recommended raises the class's primary ability.
- **Level Up Summary** (`cg_lvlup_summary`): "Class : Wizard", "Hit Points Gained : +5", for casters "Spells (Added)"
  and "Spells (Removed)", then "Skills" ("Concentration+1") and "Feats", "(none)" for empty lists. The hit points
  are rolled when the summary is asked for and kept for the level. OK takes the level; nothing is written in the
  chat.

## Server rules (measured on nwserver)

- **cls_pres SAVE rows** (EE): ReqParam1 1 / 2 / 3 is Fortitude / Reflex / Will, compared with the class save plus
  the feats' save bonuses (Great Fortitude counts; Constitution and EffectSavingThrowIncrease don't); other save
  types are met; **ALIGN rows block nothing** (parity suite `prog_prereq`). VAR rows bar NPCs' LevelUpHenchman too.
- **cls_feat GrantedOnLevel**: grants the feat by itself only on List 3; on Lists 0-2 it is the class level from
  which the feat can be picked (Brew Potion from wizard 3, Craft Wand from wizard 5). **MINSPELLLVL** is checked
  against the character before a later level (a fighter's first wizard level can't take Combat Casting) but against
  the new character at creation (a first-level wizard can) (parity suite `prog_feats`).

## Testing

OpenWinter's walk of the same screens:
`tools/godot.sh --resolution 1280x720 -- --module <vis_chargen.mod> --ui classic --capture-classic-chargen <out dir>`
(add `--character <name>` for the level-ups alone).

At 1600x900 with the default interface scale the original draws the creation panels at 1x, centred, as OpenWinter
does (its automatic scale is 1 up to 1080 lines); the captures match position for position. Larger interface scales
were not measured for these screens.

## Not matched yet

- The original's record during creation shows odd values before a class is chosen (Base Attack 1); OpenWinter
  shows the level-0 creature.
- The Clothing arrows (the original changes the preview's clothes; here they do nothing) and Tattoo (inferred).
- Recommended on the race and class screens and the alignment for classes other than the wizard (not captured).
- Hover on the creation screens before the game (only the level-up's were measured; the hover text needs the HUD,
  so creation shows none), feat and domain rows (inferred from the others), and whether spell rows show their ? on
  hover (one measurement on Burning Hands showed none; OpenWinter still shows it). The ? popup's text runs 1 px a
  line looser than the original's and its bar is the generic list bar.
- Two henchman level paths (fighter 1 then wizard 2 at character 3; fighter 4 then wizard 2 at character 6) take
  Brew Potion at wizard 2 in the original, while fighter 4 / wizard 2 with the first wizard level earlier doesn't;
  OpenWinter offers it from wizard 3. The player-facing rule at those levels was not measured.
- Character selection (`pre_playmod`) in the classic style is still the modern screen.
- The voice-set list follows soundset.2da order; a user `development` folder that overrides soundset.2da or
  portraits.2da changes the order.
