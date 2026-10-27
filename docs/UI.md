# OpenWinter UI kit and design rules

The client interface is built in code (no .tscn) from the kit in `client/UI/Kit`. Every new or
reworked screen should use it so the game looks like one product.

## Look

- **Obsidian and brass.** Dark, slightly blue-black glass panels (`Palette.Bg0..Bg4`), hairline
  borders (`Palette.Line`, `LineStrong`), brass accents (`Palette.Accent`, `AccentBright`, `AccentDim`).
  Brass is for emphasis (titles, selection, primary actions), never for large fills.
- **Type.** `Fonts.Display` (Cinzel) for titles and big numbers only. `Fonts.Ui` (Inter) for
  everything interactive. `Fonts.Reading` (EB Garamond) for prose: dialogue, journal entries,
  item and spell descriptions. Sizes come from `Palette.Font*`.
- **Text colours.** `Text` for primary, `TextSecondary` for labels, `TextMuted` for hints.
  Semantic colours (`Health`, `Mana`, `Xp`, `Hostile`, `Friendly`, `Neutral`, `Magic`, `Plot`,
  `Success`, `Warning`, `Danger`) only for what they mean.
- **Spacing.** Multiples of 4 (`Palette.Space1..6`). Windows pad 18 px; cards 12 px; rows 6–10 px apart.
- **Shape.** Radius 14 for windows, 10 for HUD/cards, 6 for buttons, slots and fields.
- **Motion.** Short (0.12–0.2 s), ease-out cubic. Windows fade and scale in; toasts slide in.
  No bouncing, nothing longer than 0.3 s on something the player waits for.
- **Icons.** `Icons.Get(name)` / `Icons.Rect(name, size, tint)` for interface glyphs
  (`client/Assets/Icons`, game-icons.net CC BY 3.0: add new ones to CREDITS.md). Game content
  icons (items, spells, feats) still come from the module's textures via `UiTheme.Icon`.

## Pieces

| Need | Use |
|---|---|
| A game window | `UiTheme.Window.Create<T>(title, size, icon)`; content in `Body`, extra header controls in `HeaderExtras`. Esc / close / drag / raise handled; `Closable = false` for must-answer windows. |
| Any stock control | Just create it: the global `GameTheme` styles Button, LineEdit, TabContainer, OptionButton, CheckBox/CheckButton, sliders, scroll bars, ItemList, Tree, PopupMenu, tooltips. |
| Button kinds | `ThemeTypeVariation`: default (secondary), `"PrimaryButton"`, `"GhostButton"`, `"MenuRow"` (big menu entries). Helpers: `Ui.Button/Primary/Ghost`. |
| Text | `Ui.Text(text, Ui.TextStyle.X)`; over the 3D view `Ui.Overlay`. |
| Section heading inside a window | `Ui.Section("Skills")` |
| Panels | `"CardPanel"` / `"InsetPanel"` variations on PanelContainer, or `Styles.Card()/Inset()/Hud()`; frosted glass via `Ui.Glass(panel)`. |
| Bars | `Bar` (hp/xp style with trail), `Set(current, max, extra)`. |
| Icon buttons | `IconButton.Create(icon, tooltip, size, iconSize, pressed)` |
| Key hints | `KeyCap.Create("I")` |
| NWN text (`<cRGB>` colour tokens) | `NwnText.ToBbcode(text)` for a RichTextLabel, `NwnText.Strip(text)` for plain labels. |
| Area map | `AreaMap.Of(session)` (tiles, fog, notes, party; shared) drawn by `MapView` (centre/zoom/angle); `MapGlyphs` for the player arrow, pins, view cone, compass and small glyph buttons. Used by the map window and the HUD `Minimap`. |
| Item / ability slots | `UiTheme.SlotButton(icon, tooltip, size, placeholderIcon)`; `UiTheme.Icon(content, layers, stack)` |
| Ability icons drawn by hand (bar, radial, tiles) | `SlotArt.DrawIcon/DrawBadge/DrawSweep`, rich slot tooltips `SlotArt.Tooltip(look, key, hints)` |
| Using an ability from any window | `AbilityServices.For(session, casting)` → `User.Use(BarSlot, target?)` (targeting, feedback); `Book` holds what the player can use |
| Rich tooltips | `Tip.Card(title, subtitle, body, icon, rows, footer)` built on demand by `TipPanel` / `TipButton` (set `.Tooltip = () => Tip.Card(...)`). Use for anything with a breakdown or a description. |
| Tags | `Chip.Create(text, color, solid)`: ability modifiers, "Prestige", "Class skill", counts. |
| Choice cards | `SelectCard.Create(title, subtitle, icon, iconSize, vertical)`: hover, brass selected state, `Disabled`, `Pressed` / `DoubleClicked`, extra lines in `Body`, badges in `Trailing`. |
| Pill tabs | `PillTabs` (`Add`, `Select`, `Changed`): compact filters such as spell levels or Cast / Prepare. |
| Wizard steps | `StepRail` (`Set(entries)`, `StepClicked`); `CharGen/WizardFrame` is the full creation / level-up layout. |
| 3D creature preview | `CharGen/PreviewStage.Create(content, zoomButtons)` (backdrop, caption, Body / Face) around a `CreaturePreview` (`Show(appearance)`, framed to the model, drag to turn, wheel to zoom). `WizardFrame.Stage` / `SetStage` hosts one in the side column. |
| Colour channels (skin, hair, tattoo) | `CharGen/ColourPicker`: channel tabs plus every palette index as families of shades, read from the game's `pal_*` textures (`PaletteSheet`). |
| Static meters | `Bar` with `ShowTrail = false` (XP, load, progress) so nothing animates on open. |

## Rules

- No raw `new Color(...)` or font sizes in screens; take them from `Palette`.
- No debug text in the shipping HUD; developer readouts go behind Options → Interface → "Show FPS".
- Everything usable by mouse must show a tooltip; everything with a key binding shows its key.
- Windows must work at 1280×720 and scale up (UI scale option); anchor to edges/centre, never absolute
  positions that assume 1600×900.
- NUI windows (`UI/Nui`) are module content and keep their NWN-style skin.
- Every top-level control (one whose parent isn't a Control, e.g. directly under a CanvasLayer) gets
  `GameTheme.Theme` automatically when added (`GameTheme.Install`); windows don't need to set it.
- Autowrapping labels need a `CustomMinimumSize.X`, and `OptionButton`s `FitToLongestItem = false`, or they
  stretch their window.

## Panels scripts control

`UI/GuiPanels.cs` applies SetGuiPanelDisabled and PopUpGUIPanel to both interfaces and sends the built-in GUI
events (the module's OnPlayerGuiEvent) as `gui-event` requests (`type`, `integer`, `object`, `vector`). It watches
the panel windows themselves, so every way of opening one (key, HUD button, portrait, radial entry) is covered.
Measured on the original in the visual scene `guipanels` (run with `--only nwn`):

- A disabled panel (inventory, character sheet, journal, map, spell book, player list) closes at once if open
  and stays shut; trying to open it raises GUIEVENT_DISABLED_PANEL_ATTEMPT_OPEN (14) with the panel, and for the
  character sheet its owner. A target given to SetGuiPanelDisabled limits it to that object: the character sheet
  disabled for another creature still opens the player's own. Disabling and enabling in one script closes the
  panel and leaves it free to open again (the spell book's force-close). The level-up and gold panels are not
  closed.
- PopUpGUIPanel opens a disabled panel anyway; its key then closes it as usual.
- Examining a creature, item, placeable or door whose examine panel is disabled shows nothing and raises 14 with
  the panel and the object. ActionExamine still shows the window.
- A disabled compass is still drawn but its clicks do nothing.
- Other events: 17 (area load screen finished) once each area has been handed over; 10 / 11 as the journal opens
  and closes and 8 / 9 for the map (also when a disable closes them or PopUpGUIPanel opens them); 1 / 2 as the
  chat line takes and loses focus, with the selected channel (0 shout, 1 whisper, 2 talk, 3 party, 4 DM); 24 / 25
  for the game menu; 15 for a click on the (classic) compass; 26 with 1 and the point for a radial menu opened on
  the ground.

Not raised yet, not having been measured: object radials, skill and feat clicks on the character sheet, effect
icon, party bar and chat portrait clicks, creature selection, examine (23) and a cancelled level-up. Disabling the
radial and quickbar panels is not applied.

## Drag and drop onto the action bar

Anything a window lets the player drag returns a Godot `Dictionary` from `_GetDragData` with a `"kind"` and that
kind's fields; the action bar (`UI/Hud/ActionBar.cs`, parsed by `BarSlot.FromDrag`) accepts:

| kind | fields |
|---|---|
| `spell` | `spell` (spells.2da row), `class` (class id, -1 any), optional `meta` |
| `item` | `item` (object id), optional `prop` (item property index; default: its first power, or equip) |
| `feat` | `feat`, optional `sub` (sub-radial spell / called-shot part) |
| `skill` | `skill`, optional `sub` (disable trap: 100 flag, 101 recover, 102 examine) |
| `mode` | `mode` (ACTION_MODE_*) |
| `emote` | `anim` (animation name), `name` |
| `command` | `command`: `attack`, `rest`, `examine`, `talk`, `stance`, `barter`, `craft`, and the associate orders `attacknearest`, `follow`, `standground`, `guardme`, `healme` |

Use `SlotPreview.Centered(look, content)` as the drag preview so drags look the same everywhere.

Right-clicking a slot (both interfaces) opens its radial: assign a spell, special attack, other action, command,
special ability or emote, use what it holds, or empty it ([QUICKBAR_RADIAL.md](QUICKBAR_RADIAL.md)). Dragging a
slot off the bar still empties it.

### Character-file quickbars

`ActionBar` asks the server for the character's `QBList`. With no local OpenWinter quickbar (including the legacy
spell-only save), it imports that layout. `BicQuickbar` reads it in Aurora; `QuickbarRequests` maps it to neutral `QuickbarSlot`
data and resolves saved item references to the character's current items. List positions 0–11, 12–23 and 24–35
are the normal, Shift and Ctrl bars. Empty slots stay empty. An empty saved OpenWinter bar or an empty `QBList` is
authoritative; only characters with neither get `AutoFill`.

| QBObjectType | Action bar slot |
|---|---|
| 0 | Empty |
| 1 | `item`: backpack coordinates or equipment slot bit; property 255 becomes `prop = -1` |
| 2 | `spell`: `QBINTParam1`, `QBMultiClass` → ClassList's class id, `QBMetaType` |
| 3 | `skill`: `QBINTParam1`; parry, stealth and detection skills become their `mode` |
| 4 | `feat`: `QBINTParam1` (ordinary feat ids) |
| 6 | `command`: `talk` |
| 7 | `command`: `attack` |
| 10 | `mode`: `QBINTParam1` (0 detect, 1 stealth) |
| 40 | `command`: `examine` |

Other types, packed feat/sub-radial parameters, unsupported skills, secondary-item pairs, items in containers
and item subproperties are left empty and logged with their slot, type and parameter. Missing items are also
logged and skipped.

Edits send individual `quickbar-set` requests, including explicit clears and both sides of a swap. The server
validates slots and item ownership, keeps live item references, and writes current locations into `QBList` on
character export (including server-vault logout). Untouched unsupported entries survive. Item shortcuts follow
items between inventory and equipment; exported property indices follow the serializer's property ordering.
The world snapshot also saves edits and item bindings.

On a server-vault connection, a layout already saved by OpenWinter overrides the client's local copy, so it follows
the character to another computer. An older local layout migrates once when the vault has no OpenWinter edits yet.
Local-mode play retains its existing local-save priority. Deliberately empty layouts remain empty.

Supported native forms use the fields above. OpenWinter extensions (`ElyQBSaved`, `ElySlot`, `ElyItemUuid`) preserve
all slot kinds and exact item bindings without storing runtime object IDs. Actions without a measured native
encoding (for example rest, stance, emotes, sub-radial selections and container items) appear empty in the original
game but reload in OpenWinter. These fields are our format additions, not claims about NWN's format.
`--capture-quickbar <dir>` (with a saved character and fresh client data) checks edits on all three pages against
the server.

## Settings

`Settings.Current` (namespace `OpenWinter.Client`) is the player's options (`Preferences.GameSettings`: graphics,
audio, controls, interface, gameplay, profile), loaded from `user://settings.json` before the main menu and
applied to the window, renderer, audio buses, input map and interface. Read it where a setting matters and
subscribe to `Settings.Changed` to react live. Change settings with `Settings.Update(s => ...)` (saved) or,
from an editor that can still be cancelled, `Settings.Preview(copy)` then `Settings.Commit(copy)`.
Development captures (`--capture*`, `--look`, `--bench`) run on defaults and never write the file.

- **Keys.** Every key is an InputMap action from `Preferences.ActionCatalog` (rebindable in Options →
  Controls, two keys per action, conflicts resolved by moving the key). Test one-shot actions with
  `InputBindings.Pressed(e, ActionCatalog.X)` (exact modifiers, so F5 is not Ctrl+F5) and held ones with
  `Input.IsActionPressed(ActionCatalog.X)`. Show a key with `InputBindings.KeyText(ActionCatalog.X)`.
  Don't test `Key.*` directly in gameplay code. Defaults are the original game's keys ([CONTROLS.md](CONTROLS.md):
  how they were decoded, and what each does); don't give a new action a key the original uses for something else.
- **Audio.** Every player names a bus from `Preferences.AudioBuses`. Flat players use Music, Ambient,
  Effects, Voice or Interface; positional players use Ambient3D, Effects3D or Voice3D. Both variants obey
  the same category volume. The Audio page's 2D/3D bias attenuates flat effects above 1 and positional
  sounds below 1, with native measured range 0.1..1.9; music is independent. Conversation voice playback
  lives on ClientSession, follows the current speaker and stops on the next line/end/area change.
- **Areas.** `AreaView.Build` registers its environment and sun with `GraphicsApply.RegisterEnvironment`,
  which applies SSAO, bloom, brightness, gamma and shadows now and on every change.
- **Interface.** UI scale is `Window.ContentScaleFactor` (automatic: 100% up to 1080 lines, then in quarter
  steps). Frosted glass off removes the glass material from open panels. `PlayerInput.Blocked` is true while a
  full-screen menu or the loader has the keyboard.

## Front end

`client/UI/FrontEnd` holds the screens outside play and around it: `MainMenu` (in `UI/`), `MenuStage` (a
campaign area circled by the camera behind the menu), `Atmosphere` (smoke, vignette and embers), `GlowTitle`,
`MenuEntry` (the big menu rows), `ModuleBrowser`, `LoadGameScreen`, `MultiplayerScreen`, `OptionsView` /
`OptionsWindow`, `CreditsView` (reads `Assets/Icons/CREDITS.md`), `LoadingScreen`, `GameMenu`, and the save
cards (`SaveCard`, `SaveGrid`). `Kit/Dialogs.Confirm` is the modal question. Screens that are not children of
another kit control set `Theme = GameTheme.Theme` themselves: the root window's theme does not reach controls
under the 3D main node.

Interface-style switching is checked by `--capture-menu-switch <dir> --options-open`: it opens Options, changes the
interface style, verifies Discard, then Apply followed by closing, in both directions. Settings are locked for the
run; the capture writes `options-switch.json` assertions and exits nonzero on failure.
