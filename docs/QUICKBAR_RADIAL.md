# The quickbar slot's radial and key labels

Right-clicking a quickbar slot opens a radial for assigning something to that slot, using it, or emptying it. The
behaviour below was measured on the original with the visual scenes `quickbarradial`, `quickbargaps`,
`quickbarsound`, `quickbaremote`, `quickbaremote2` and `quickbarexport`, driven with the real pointer
(`--real-mouse --native-framebuffer --silent`; see [parity/README.md](../parity/README.md)). Sounds and voices were
identified with `tools/visual/combat_audio.py <scene> --sounds gui_|vs_ --real-mouse`.

## Measured

- **Opening.** The ring opens on the right button's press and stays open after it is let go where it opened.
  Its centre is 100 px above the slot's top at 1280x720 (y 565 for slots at the bottom edge: the 154 px clamp from the
  screen edges the chat menu has), straight above the slot. The pointer stays on the slot, with the pointer band from
  the centre down to it, so the south option is hovered at first. Hovered options show their label, as on every ring.
- **A filled slot** (F1 Attack, F12 Light): the slot's contents on the large centre disc; **N** Examine (`ir_examine`,
  for the spell; not offered for Attack), **SE** "Use : Light" (the slot's own icon), **S** Empty Quickslot
  (`ir_emptyqs`, dialog.tlk 1389). Empty Quickslot empties the slot. The numeric keypad picks directions here as on
  other rings (Num8 on F12 opens the Light spell's description).
- **An empty slot**: nothing in the centre; clockwise from the top **N** Custom Text Macro (`ir_textmacro`, 1159),
  **NE** Assign Spell (1158), **E** Attack (assigned at once), **SE** Assign Special Attacks (1156), **S** Assign Other
  Actions (67564), **SW** Assign Command (1157), **W** Assign Special Abilities (1155), **NW** Emote. The categories
  carry the "more" arrows and child previews as on the self radial.
- **Its rings** are the self radial's: Assign Spell has Spell Special Abilities W and Wizard SW; Assign Special
  Attacks has Parry Mode N; Assign Command has Heal Me W, Attack Nearest E, Follow SE, Stand Your Ground S and Guard
  Me SW; Assign Special Abilities has Crafting Skills N, Taunt NE, Cancel Detect Mode E, Summon Familiar (Wizard) SW
  and Stealth Mode W; Emote is below. Assign Other Actions has Assign Examine N, Assign Talk To E and Assign
  Barter NW (67566, 67565, 67569), and the skill actions below for characters with those skills. There is no item
  ring: items go on the bar by dragging.
- **Assign Other Actions** with ranks in the three skills: Assign Examine N, Assign Talk To E, **Assign Animal
  Empathy S** (`isk_aniemp`), **Assign Pick Pocket SW** (`isk_pocket`), **Assign Open Lock W** (68656,
  `ir_picklock`), Assign Barter NW; NE and SE empty.
- **Custom Text Macro** asks twice in the original's util_editpopup, which pauses single player: "Enter Label"
  (6679), then "Input Command" (6628), each with an empty line focused. A box takes characters only while they fit
  its width (41 digits of a 320-digit line; a 50-character label keeps 42 characters). OK puts the macro on the slot.
- **A macro slot** has no icon: its label in white, right-aligned where a potion's count goes ("Hail" ends 28 px right
  of F10's centre, top at y 690), and its key in grey as on an empty slot. A label wider than the slot starts at its
  left and stops at the last character that fits ("A long macro label" shows "A long m"). The tooltip reads "F10 :
  Hail: Hail and well met". Its ring: the centre a black square on the large disc, **E** Edit Label (6903,
  `ir_blank`), **SE** Execute Command (6629, `ir_cast`), **S** Empty Quickslot, **W** Edit Command (6902,
  `ir_blank`); no Examine. Edit Label and Edit Command ask again with an empty line.
- **Using a macro** (a click on the slot or Execute Command) sends the line as typed chat: a line with no prefix goes
  out as **[Shout]** (TALKVOLUME_SHOUT, OnPlayerChat volume 2), "/p party macro" as [Party] "party macro" (volume 5)
  and "/tk talk macro" as plain talk (volume 0).
- **Saved macros**: the exported character's QBList holds a macro as **QBObjectType 18** with **QBCommandLabel** and
  **QBCommandLine**, both CExoStrings.
- **Examine on a spell, feat or skill slot** (Light, Summon Familiar, Taunt) opens the description window: the item
  examine panel (pnl_exam_item, 429x447, centred at 1280x720: x 426-854, y 137-582) titled with the name, the icon 22 px
  in and 54 px down (beside the first line), the talk-table description from x 499, and one OK where the item window has
  Close; no X. A skill slot's ring is a spell's: Examine N, "Use : Taunt" SE, Empty Quickslot S.
- **The emote rings**: Emote has Bow N, Threaten NE, Greet E, Laugh SE, Salute S, Cheer SW, Goodbye W and More Emotes
  NW; its More Emotes Nod N, Tired NE, Beg E, Poisoned SE, Bored S, Look Far SW, Scratch Head W and More Emotes NW;
  that one's Fall down forwards N, (nothing NE), Sit on ground E, Meditate SE, Drink potion S, Read SW, Worship W and
  Fall down backwards NW. Their icons are the game's `ir_<name>` stars (`ir_woosey` for Poisoned), More Emotes
  `ir_moreemotes`; an emote slot shows its star.
- **What an emote does**: Greet says hello (sound-set slot 34), Laugh laughs (42), Cheer cheers (44), Goodbye says
  goodbye (40), Tired the rest line (38), Poisoned the poisoned line (19) and Scratch Head "bad idea" (47), each line
  also in chat; the others are silent. Cheer crouches and raises an arm; Laugh leans back about 2 s; Beg pleads about
  2 s; Tired hunches about 3 s; Poisoned sways about 4 s; Bored stretches for about 4 s; Look Far and Scratch Head
  raise a hand; Drink potion and Read show the potion and book; Worship kneels and raises its arms for about 7 s; Sit
  on ground and Meditate hold about 10 s; the falls lie about 2.5 s and are up by 3 s.
- **Saved emotes**: QBObjectType 8 with an id in QBINTParam1: Bow 7, Threaten 8, Greet 6, Laugh 4, Salute 3, Cheer 1.
- **Sounds**: opening a slot's ring is silent; putting something on the slot sounds **gui_quick_add** and Empty
  Quickslot **gui_quick_erase** (neither with gui_radial_slect); opening a sub-ring, Edit Label and Edit Command sound
  gui_radial_slect; Custom Text Macro sounds gui_quick_add as it is chosen and again at each prompt's OK; Cancel sounds
  gui_button, and so does clicking a macro slot to use it.
- **Key labels.** The slot's key (F1 ...) is drawn only on empty slots: grey (153,153,153), bottom left at the
  model's `text` anchor (F12: x 979-998, baseline y 715). Filled slots show no key at all.

## Implemented

- `RadialMenu.OpenQuickbar` (both interfaces, reached through `Interaction.QuickbarMenu`): the rings above, built from
  the self radial's own (`Spells`, `CombatAbilities`, `SpecialAbilities`, `Emotes`) plus `QuickbarOtherActions` and
  `QuickbarCommands`. In this mode a choice that the bar can hold is put on the slot instead of used; anything ready
  or not can be assigned; what the bar can't hold (Counterspell, Possess Familiar, DM tools) is greyed.
- **The pointer** (a deliberate difference): opening the ring takes the pointer to its centre, as every sub-ring and
  the world and inventory rings do when they open away from the click. The original leaves it on the slot, with the
  south option hovered, until a sub-ring opens.
- `ActionBar.OpenSlotMenu`; the classic `ClassicQuickSlot` and the modern `ActionSlotView` open it on the right
  button's press (right-click does not empty a slot straight away; dragging a slot off does).
- Quickbar commands accepted by the server's `quickbar-set` include `barter` (pick a player), `craft`, and the
  associate orders `attacknearest`, `follow`, `standground`, `guardme`, `healme` (the Issue Command ring's voice
  requests).
- The modern interface uses its own ring with the same entries; its centre says "Empty slot" on an empty slot, and its
  hint reads "Click to put it on the slot".
- **Key labels** (a deliberate difference): the classic quickbar draws the key on filled slots too, over the icon, in
  the original's grey at its anchor. Options → Interface → Quickbar key labels turns it off for the original's look.
  The modern bar's key cap is drawn on every slot.
- **Text macros**: a quickbar slot of kind `macro` with its label in `Name` and its line in `Text`; the server's
  `quickbar-set` accepts it (label up to 512, line up to 1024 characters) and the character file saves it as the
  original's QBObjectType 18 (QBCommandLabel, QBCommandLine), reading the original's back. Using one sends the line
  through `ChatInput.Parse` with Shout as the channel when there is no prefix (/p, /w, /s, /tk, /t, /dm). Classic:
  Custom Text Macro asks with the two util_editpopup prompts (`ClassicRadialSkin.Prompt`, characters refused once the
  box is full), the slot draws the label and key (`ClassicQuickbarText`), the tooltip "F12 : label: line", and the
  macro ring has Edit Label, Execute Command, Empty Quickslot and Edit Command. Modern: one "Text macro" window
  (`MacroEditor`) with both fields, filled in for edits; the action bar shows the label under the slot's talk glyph.
- **Examine on spell, feat and skill slots** opens `ClassicDescription` (classic: the examine_item layout with one OK,
  the icon beside the first line) or `DescriptionWindow` (modern), both filled from the server's `chargen-describe`.
- **Emotes** (`OpenWinter.Content.Emotes`): the three measured rings for the self radial and the slot ring, with the
  original's labels and icons; using one sends its key, and the server clears the player's actions and queues the
  ANIMATION_* as ActionPlayAnimation would (moving ends it), for the measured time, speaking the measured voice-set
  line. Saved as QBObjectType 8 with the measured ids; older OpenWinter emote names (greeting, tlklaugh, taunt,
  victoryfr, tlkplead) map to Greet, Laugh, Threaten, Cheer and Beg.
- **Assign Other Actions**: Animal Empathy S, Pick Pocket SW and Open Lock W, as measured.
- **Sounds**: a choice that goes on the slot plays gui_quick_add instead of the radial's gui_radial_slect, Empty
  Quickslot gui_quick_erase, Custom Text Macro gui_quick_add (and each prompt's OK; its Cancel keeps gui_button).

## Testing

`--capture-quickbar-radial <dir>` (add `--ui classic`) fills F12, reads the filled ring, empties it, reads the empty
ring and each sub-ring, assigns Assign Examine and Sit on ground through the three emote rings (checking each ring's
labels), opens Light's description through Examine, makes a "Hail" macro through the prompts (or the modern window),
checks that a click shouts "Hail and well met" and reads the macro's ring:

```
tools/godot.sh -- --module "<NWN install>/data/nwm/Prelude.nwm" --character <name> --capture-quickbar-radial <out dir>
```

## Not matched yet

- Which animation Bow, Threaten, Greet, Salute, Goodbye and Nod play is not confirmed. They play the ANIMATION_* their
  names say (bow, taunt, greeting, salute, greeting, and LISTEN for Nod); Cheer is victory 1, and Laugh, Beg, Tired,
  Poisoned, Look Far and the rest are as measured.
- The original's quickbar ids for Goodbye and the fourteen More Emotes are not measured. OpenWinter saves those with
  its own field only (the original would show an empty slot).
- How long Sit on ground and Meditate last beyond about 10 s, and whether moving or another emote ends them early in
  the original (OpenWinter: an action that moving cancels).
- The description window's lines are 17 px apart in the original, 20 px in OpenWinter (the examine windows' body
  font); the util_editpopup's typed text uses OpenWinter's small chat face, the original a larger one.
- Cancel on the second macro prompt, tokens in a macro's line, and the modern window's lines longer than the box.
- Whether using other slot kinds by a click also sounds gui_button (only the macro slot was recorded).
