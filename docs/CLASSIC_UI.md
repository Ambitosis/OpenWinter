# Classic UI

The interface mode that looks and behaves like the original game's, and the default for players. It is drawn from
the original's own art, read at run time from the player's game data (and so from any haks that replace it).
Nothing is shipped with OpenWinter. Switch between it and the modern interface in Options → Interface → Interface
style (it applies from the next menu or game), or with `--ui classic` / `--ui modern`. Command-line captures use
the modern interface unless given `--ui classic`. NUI windows (`UI/Nui`) look the same in both modes.

## How the original's interface is built

- **Panels and controls are flat models** (`pnl_*`, `ctl_*`, `pb_*`, `gui_*`, `inv_*`). They are textured
  meshes in interface units: 1 unit is 100 px, and the original draws at 1x at 1280×720. Y points up. A
  control's model origin is its bottom-left corner; most panels have theirs at the top-left (y runs negative
  downward).
- **Dummy nodes are anchors**: `text0`/`text1` (text box corners), `title0`/`title1`, `icon`, `scrollmin`/`scrollmax`
  (scroll bar ends), `min`/`max`/`thumb` (sliders), `listitem`, quickbar `Slot01..12`, and inventory `repository`,
  `Equip` and `Container`. `openwinter-tools gui-model <resref>` prints them all with their pixel rectangles.
- **Button states are model animations**: `up`, `down`, `hilite`, `disabled`, `checkedup`, `checkeddown`, `uncheckedup`,
  `uncheckeddown`, `checkedhilite`, `encouraged` (pulses). They move nodes and switch mesh faces on and off with
  alpha (controller 128). `GuiModel.PoseOf(state, time)` evaluates them.
- **Layouts** (`.gui`, about 100 in the base game; list them with `openwinter-tools list "" gui --base` and print one with
  `openwinter-tools gui <name>`) place controls on a panel, relative to a node of the containing control's model
  (`Obj_Parent_Hook`). Their kinds: 3 panel, 9 text button, 1 button, 2 checkbox, 5 label, 6 slider, 8 list or
  edit box, and 0 custom (portraits, item grids, model viewers that the game fills itself). Captions name a
  talk-table string or text, a font, a colour and an alignment.
- **Screens without a layout file**: the in-game HUD (quickbar, party bar, compass, chat), the character record,
  the spellbook, the journal, dialogue and the radial menu are placed by the engine around their panel models'
  nodes. Build them with `GuiPanel.FromModel` and place controls at its nodes. Check every position against
  the reference shots.
- **Fonts**: `fnt_galahad14` (labels and buttons) and the other bitmap fonts are a texture plus a TXI character table
  (`GuiFont`). Chat and long text use the installed TrueType `fnt_default`; the high-resolution font option
  selects `fnt_default_hr`. `ClassicText` reads their TXI contrast, outline opacity and outline brightness.
  Coverage is adjusted on text-only canvas layers so portraits and panels retain their original alpha.
- **Texture sampling**: UVs are used as the 3D renderer uses them (v = 0 at the top of the decoded image). Slices
  only a few texels wide are inset by half a texel so filtering doesn't bleed (`GuiDraw.Inset`).

## Code (`client/UI/Classic`)

| Piece | What it is |
|---|---|
| `ClassicAssets.For(content)` | Models (`GuiModel`), textures, fonts, layouts and talk-table strings, cached per content (haks included) |
| `GuiModel` / `GuiModelView` | A model in pixels, its poses (states) and nodes; a control drawing one |
| `GuiButton`, `GuiLabel`, `GuiSlider`, `GuiList` (+ `GuiListRow`, `GuiScrollBar`) | Controls drawn from models; `PlaceOrigin(point)` puts a control's model origin on a point |
| `GuiPanel.FromLayout(assets, "inv_panel")` / `FromModel(assets, "pnl_chrsht")` | A whole panel; `Get<T>(tag)` for a layout's controls, `Node(name)` / `At(node, x, y)` for positions in panel pixels, `ButtonPressed(tag)` |
| `ClassicWindow` | A toggled window placed against a screen corner (`Anchor`, `Offset`), closed by its XButton or Esc (most recent first) |
| `ClassicGame.Attach(ClassicContext)` | Called by Main in classic mode. `ClassicHud`, `ClassicInventory`, `ClassicSheets` and `ClassicDialogs` each hide the modern pieces they replace and take over their keys (`ctx.Input.ToggleInventory` and so on). A part not built yet leaves the modern piece in place |
| `ClassicContext` | The session, input, casting, interaction, HUD state and every modern panel. Reuse the modern panels' logic (requests, drag data, tooltips) and only redraw |

`ClassicDialogs` (`client/UI/Classic/Dialogs`): `ClassicConversation` (drawn over the unseen modern DialogPanel,
which keeps the keys and replies), `ClassicGameMenu`, `ClassicPopup` (`Ok`, `OkCancel`,
`YesNoCancel`, `OkScroll`, `Edit`), `ClassicDeath`, `ClassicPortal` (ActivatePortal's prompt, see "Portal prompt"
below) and `ClassicText` (the TrueType faces with colour runs).
`client/UI/Classic/Creation`: character creation and the level-up window (`ClassicCharGen`, `ClassicLevelUp`, from
the `cg_*.gui` layouts; [CLASSIC_CHARGEN.md](CLASSIC_CHARGEN.md)). `client/UI/Classic/FrontEnd`: `ClassicLoading`
(over the modern loader). The main menu is OpenWinter's own in both interface styles, a deliberate choice: the
original's title screen carries its publisher's logos and branding.

Behaviour stays in the shared game services. Classic screens send the same requests and use the same drag-and-drop
dictionaries ([UI.md](UI.md)) as the modern ones, so both modes play the same.

## Checking against the original

- `tools/godot.sh -- --capture-classic <out dir> [--classic-only a,b] [--classic-models pnl_x,ctl_y]` draws
  every layout (or the named ones), plus any bare models, alone on grey at 1x.
- `python tools/visual/visual.py run classicui --only nwn` captures the original at 1280×720: the HUD, then
  inventory, character, spellbook, journal, map, and inventory and character together. Run it without `--only` and
  pass `--openwinter-args "--ui classic"` for side-by-side pairs. Windows are opened with posted keys
  (`VISUAL|key|<vk>|tap`). Posted mouse moves don't reach the original, so hover states need the real pointer
  (see [parity/README.md](../parity/README.md)). Esc opens the game menu, which pauses single player (scene timers
  stop), so shoot it with `VISUAL|keyshot|27|<name>|<seconds>|27`: the harness taps the key, shoots on its own clock
  and closes it. `classicui_dialogs` captures a conversation, a reply, the game menu and the death panel.
- OpenWinter-side captures (all with `--ui classic`): `--capture-classic-dialogs`, `--capture-classic-hud`,
  `--capture-classic-inventory` (`--classic-inv-tips 1` hovers every first-page item, the equipped ones and a moved
  item, logging the hover lines), `--capture-classic-sheets`, `--capture-classic-map-flow`, `--capture-classic-chat`,
  `--capture-portal`, each followed by an output directory.

## Reference measurements (1280×720)

- Inventory: top-left at (0, 0), 360 px wide, with the equipment panel above the item grid. Character record,
  journal and the spellbook's known-spells panel: top-left. Memorize panel: left of the party bar. Map: a
  small window at the top-left of the screen.
- Quickbar: bottom centre, 12 slots 67 px apart (the model's 0.67 units), counts at a slot's bottom-right.
- Item grids: plain cell backgrounds are transparent; only hover and unusable states draw a cell texture
  (unidentified items rest on plain cells). Icon canvases crop padding to 32 texels per occupied cell before
  fitting; a 1x3 torch uses the first 96 rows of its 128-row texture (scene `iconfit`). Armour icons are built from
  the armour's parts ("Armour icons" below). A spell scroll shows the iprp_spells.2da `Icon` of its first Cast Spell
  property, whole and unscaled, in place of its own icon.
- Labels in layouts: `Obj_Label_X/Y` is the box's top-left corner (y up) and the caption's vertical alignment runs
  from the top. A single line wider than its box (many keep the editor's default 0.83 x 0.17) hangs from the box's
  x by its alignment: left from it, centred on it, right up to it.
- EE draws layout captions (`fnt_galahad14`) with the TrueType `fnt_maintext` at 16 px, its characters laid out
  wider than Godot's: OpenWinter adds half a pixel to each exact advance (`GuiFont.SubstituteTracking`), which
  matches the original's caption widths to a pixel on the creation and main-menu screens ("Select the Gender of
  your Character" 263 px, "Recommended" 97, "Load" 30).
- Conversation (pnl_dialog): a box at (5, 4), 350 x 254 px at its smallest, 1-pixel border and fill from
  gui_boxes_dialog (white and black at alpha 166); portrait 64 x 100 at (8, 7) (the medium portrait's top 100
  rows); name in `fnt_maintext` 16 px, (102, 178, 255), centred in name0..name1; regular text/replies use
  `fnt_default` 11 px with 14 px lines. High-resolution text uses `fnt_default_hr` 13 px (replies 14 px)
  with 16 px lines. Text is white, from entrytext, wrapped at ctl_dialog_entry's text0..text1 (261 px); replies from
  repliestext (26, 112) or 4 px under the text, wrapped at ctl_dialog_reply's 311 px, numbers "1." right-aligned in
  its indextext box; replies (102, 178, 255), <StartAction> green (1, 254, 1). Esc ends the conversation.
- Game menu (options_main), death panel (dth_deathopts), message boxes (pnl_util_ok) and the main menu (pre_main):
  the model origin at the middle of the screen. The game menu hides the HUD and pauses single player; EE hides
  Sound Options there and renames Video / Controls to "Resolution and UI Scaling" / "Change Key Settings"; its
  build number is grey (102, 102, 102). Single player's death panel offers Load Game, Respawn, Exit Game (no Wait
  For Help) with the message starting "Choose an option below". A hidden Respawn leaves a gap (the other buttons
  don't move up); a raise closes the panel; Respawn pressed on a panel a script showed a living player still runs
  OnPlayerRespawn ([DEATH_AND_DYING.md](DEATH_AND_DYING.md)).
- Main menu: the pregame painting (pnl_main_bg's gui_pre_bknd3) fills the screen; pre_main centred.
- Party bar and portrait: top-right. The compass is at the bottom-right. Chat sits bottom-left, above a full-width
  "Talk:" line, with a scroll bar on its right.

## In-game HUD (`client/UI/Classic/Hud`)

`ClassicHud` (`ClassicHud.Parts.cs`) adds `ClassicHudRoot` to the HUD layer, below the game windows, and hides
the modern player, party and target frames, toasts, the area title banner, the top-right column (minimap, clock,
quest tracker), the action bar slot, the menu buttons and the modern chat (the action bar itself keeps running
hidden: it loads and saves the slots and takes the F-keys; the chat entry stops processing).

| Piece | Built from | Notes |
|---|---|---|
| `ClassicQuickbar` | pnl_quick_bar (Slot01..12), qb_but67 / qb_but67end | Draws the modern `ActionBar`'s slots; Shift / Ctrl banks, scroll to change the base bank, drag and drop as [UI.md](UI.md), right-click opens the slot's radial ([QUICKBAR_RADIAL.md](QUICKBAR_RADIAL.md)); uses at count1; the key at the `text` anchor on empty slots (and, optionally, filled ones) |
| `ClassicChat` | drag bar, pb_scrl_up / pb_scrl_down, gui_scrollbar, ir_chat* icons | `fnt_default_hr` at 14 px, 16.25 px lines, the original's channel colours; speakers' tiny portraits; drag a bar to show more lines, right-click it for the chat menu; split, two windows side by side (see "Chat window" below) |
| `ClassicTalkLine` | pnl_chat_prompt, ctl_btn_chatmode, ctl_lst_chatmode | Enter to type, Up / Down history, mode list; sends through `ChatInput.Send` |
| `ClassicPartyBar` | pnl_party_bar, pb_portrait, ctl_btn_pb_lvlup, ctl_playr_health, pb_but_*, pb_pnl_member, pb_mbr_pic, ctl_party_health, pb_mbr_pos, pb_scrl_up / pb_scrl_down | Buttons call `Hud.Open*` / `Input.OpenPlayerList` late, so whichever interface owns a window opens it. While a level waits the portrait shows `po_levelup` (ctl_btn_pb_lvlup) and clicking it opens the level-up. Party members: see "Party members" below |
| `ClassicCompass` | pnl_compass, ctl_compass | Rose turns with the camera; tilted by 0.78 × the camera's angle from the vertical (matched to the reference) |
| `ClassicActionQueue` | pnl_actionqueue, ctl_actionqueue | The HUD feed's `self.queue`; a click sends `clear-actions` |
| `ClassicRadialSkin` | gui_ra_menu hooks, gui_ra_circle56, gui_ra_circle112, gui_ra_clearring, gui_ra_more | A `RadialMenu.ISkin`: the modern radial's rings and input, drawn in the original's look (8 positions) |

Commands, emotes and modes without game icons use the radial menu's ir_* icons (`ClassicIcons`).

**Hover text** (`ClassicTooltip`): controls implementing `IClassicHover` or carrying a `classic_hover` meta get the
original's hover box, on its own canvas layer (12) over the game windows and popups. Measured on the original's
inventory: white text in the text face (`ClassicText.For`: `fnt_default` 11 px, `fnt_default_hr` with the large
font; "Backpack Dagger" 88 px wide in both engines), centred on a borderless black box at 60% opacity (the
background keeps 0.40 of its value), 20 px high for one line with the capitals 4 px under its top, at least about
112 px wide, its top-left 5 px right of and 40 px below the pointer (OpenWinter's box lands within a pixel of the
original's).
- Items (inventory, equipment, bags, containers, stores) show their name at once; the name is read from where
  the pointer is, so a page turn or a refresh under a resting pointer doesn't lose it. Godot's own tooltip is not
  used for items, the inventory's buttons, store tabs, spell cells, the pin editor or the party bar's effect icons.
- Portraits (the player's and party members') show at once: the name, then the name of the area the creature is
  in (the HUD feed carries `area` for the player and each party member).
- Map pins show their note at once, over the map.
- Other controls keep a 0.4 s wait (not measured). Lines of an empty name show no box at all; lines wider than
  420 px wrap (not measured). Near the bottom edge the box goes above the pointer, and it is kept on screen (not
  measured: the portrait's box slides left against the right edge).

**Examine windows** (`examine_basic`, `examine_item`) use the installed scroll bar (`ClassicScrollBar`: pb_scrl_up /
pb_scrl_down and the ridged gui_scrollbar thumb). The original always shows it, the thumb filling the whole track
when the description fits and shortening when it doesn't (scene `examinescroll`).

**Party members** (measured with the `partyframes` scene: three henchmen and a summon, 1280x720 at 1x and
1600x900, where the original draws its interface at 1.2x):
- The bar (`pnl_party_bar`) sits one column further left than its 79-pixel texture would put it: the original
  leaves the panel's dark outer texel in the screen's last column. OpenWinter places it at `view - 80` and leaves
  that column to the scene.
- The member frames (`pb_pnl_member`, 63x57, the same 1:1 texture scale as the bar) hang from `PartyMembers`
  against the bar's **left** edge, one `NextLink` (66 px) apart.
- The list's scroll bar fills the 16 px at their right: pb_scrl_up at `scrollmin`'s x with its track starting at
  `scrollmin` (y 199, so the arrow's top meets the first frame's), the ridged thumb filling the track, pb_scrl_down
  ending at the last row's bottom. Five rows fit before `scrollmax` (not measured past four members).
- **The thumb** (`ClassicScrollBar`, so also the chat, examine and creation lists; matched texel for texel against
  the decoded gui_scrollbar): 12 px wide, gui_scrollbar's columns 2..13 across it. Its first two pixel rows are the
  texture's first two rows upright (the rounded top), its last two the texture's last two (the rounded bottom);
  between them the texture turned a quarter, its rows 2..13 across (so rows 2 and 13 make the black edge columns)
  and its columns along the bar, column 0 right under the cap, repeating every 16 px. `GuiDraw.ScrollThumbCapped`;
  the other bars (`GuiScrollBar`, the sheets') draw `GuiDraw.ScrollThumb`, not compared.
- Each frame shows the direction to the member in its upper box (`pb_mbr_pos` at `Pos`: gui_partylocator turned
  from straight up, the camera's ahead, clockwise; none while the member is in another area, not measured).
- The name is under the frame: left at `Text` x, its baseline 2 px above `Text`, (204, 204, 204), in the text face
  at the chat's size: with the large font (the original's default) `fnt_default_hr` at 13 px ("Hench A" 54 px wide,
  9 px capitals), with the regular font `fnt_default` at 11 px (48 px, 8 px capitals).
- **Glyph spacing**: the original lays each character at its unhinted advance plus the face's TXI `spacingR` x 128
  px, every advance rounded to a whole pixel. `fnt_default_hr`'s 0.003 adds 0.384 px: "Hench A"'s letters start at
  0, 11, 18, 27, 34 and 46 where plain rounding gives 0, 10, 17, 25, 32 and 44, while "Summon" lands the same either
  way. The fit holds for 0.38-0.41 px; 128 is an inferred factor (other sizes weren't measured). `fnt_default` has
  no spacing; `fnt_maintext` has 0.005 (not compared). `ClassicText.DrawLineTracked` and `MeasureTracked` lay text
  out this way (the faces' advances from `TrueTypeMetrics`); only the party names use it so far.
- Characters starting within 56 px of the name's left are drawn, cut at the frame's last column: the regular font's
  "Summoned ..." shows "Summone" and the edge of its 'd' (55 px in), the large font's stops at "Summon" (its 'e' would
  start 56 px in).
- The PvP button is greyed in single player. Its model's `disabled` animation changes nothing; the original draws a
  disabled one from the texture's greyed picture: pb_but_* textures stack three 17-texel pictures 18 texels apart
  (up at the model's coordinates, then the glow, then the greyed one; for pb_but_rest the third is orange) and the
  greyed PvP button matches the third exactly, two rows (0.5625) down (`GuiButton.DisabledUvShift`).
- Not matched yet: individual glyph pixels (Godot's rasterizer hints a few stems differently: H's crossbar row,
  the serif face's right stems), and the large-font chat lines, which the same spacing rule would give at 13 px
  (OpenWinter draws them at 14 px plain); the frames themselves match.

**Interface scale** (measured with the `partyframes` scene and `visual.py --size --ui-scale`, reading the scale off
the party bar: the player's health bar is 100 px tall at 1x). The original doesn't always draw at its `ui.scale`: it
takes the largest scale that keeps the virtual screen at least about 937.5 x 750, rounded to the nearest tenth, and
never less than 1. OpenWinter's classic interface does the same (`InterfaceSettings.ClassicScale`; the modern
interface is unchanged).

| Window | ui.scale | Drawn at |
| --- | --- | --- |
| 1280x720 | 1.5 | 1.0 |
| 1600x900 | 1.5, 1.25 / 1.1 | 1.2 / 1.1 |
| 1440x960 | 2.0 | 1.3 |
| 1600x790, 1600x760 | 1.5 | 1.1, 1.0 |
| 1080x900, 1024x900, 1000x900 | 1.5 | 1.2, 1.1, 1.1 |
| 968x900, 960x900 | 1.5 | 1.0 |

The readings bound the height to 724 to 752 and the width to 922 to 939 px; 750 and 937.5 (750 x 1.25) are the
round values inside. Not measured: windows smaller than 1280x720, scales below 1, and whether the original also
caps its menus (only the in-game interface was read).

### Character record, spellbook, journal, map (`client/UI/Classic/Sheets`)

- **Character record** (C, top-left, 360×468): `pnl_chrsht` (title box title0/title1, `xbutton`, tabs from the
  `tabs` node chained by each tab's `next` node: `ctl_chr_btn_tab1..3`, eye / tools / dagger = Character, Skills,
  Feats), a page under `pages` (y 59) clipped at `resizebar` (y 468, `ctl_chrsht_resiz`). `pnl_chrsht_char` holds
  the portrait (the `gui_replace01` plane, a 128×200 "l" portrait), ability boxes `l<Ability>0/1`, `<ABL>Stat0/1`,
  `<ABL>Mod0/1`, the `<abl>Icon` centres (`gui_chrsht_str` ... 16 px), AC/HP, experience, `LevelUpButton`
  (`ctl_chrsht_lvlup`, pulsing "encouraged") and the combat list from `List` (y 258) in `fnt_default`, captions
  grey (170) and values white, 20 px a line. Skills and Feats: `pnl_chrsht_skils`, description over a list.
- **Spellbook** (B): `pnl_spellbook` top-left (class icons in the first box, the ten level cells of
  `gui_spell_splv` 33 px apart at y 68, `ir_cantrips` / `ir_level1..6` / `ir_level789`; a "Known" tree under it,
  33 px rows), and `pnl_spellmemoriz` against the party bar (x 840): a heading per level with its slots.
  An empty slot is a clear box (the panel shows through) in white edges at alpha 48, as `ctl_spelbx_next`'s
  `gui_boxes_ingame` box; memorized ones keep the icon on black (`memorizeslots` scene).
- **Journal** (J): `jnl_panel` + `jnl_page_quest` (Date / Name / Priority, `ctl_btn_xch`) or `jnl_page_notes`;
  tabs `ctl_jnl_btn_tab`, `tab2`, `tab3` (quests, completed, notes); quest rows as `ctl_jnl_questbtn`. The World page
  (`jnl_page_world`) has no tab: see "Journal world page" below.
- **Map** (M): a 75×94 window at (8, 64): ◆ (gui_map_pieces cell 0: the large map, `map_area`), `ctl_map_zoomin`,
  `ctl_map_zoomout`, X; explored tiles at 16 px a tile centred on the player, `gui_map_arrow` for the player.
- Open character and inventory records poll authoritative statistics every 0.5 s, keep their tab and scroll state,
  and ignore identical payloads; the classic inventory also refreshes AC. Status-effect tiles use ctl_ig_plreffect
  40-pixel frames at a 43-pixel pitch; the HUD snapshot owns expiry, stacking and area-change state.
- `--capture-classic-sheets <dir>` (with `--ui classic`) opens each and saves shots named like the reference ones.

### Portal prompt

ActivatePortal, measured with one nwmain on a sandboxed nwserver (`python tools/multiplayer/mp.py run mp_portal --only
nwn --size 1280x720`):
- **Single player shows nothing** and prints nothing. OpenWinter does the same.
- **On a server** the original opens `ingame_portal` (pnl_con_prtl_inf, 370 x 254 px, its model origin at the middle of
  the screen like the message boxes): "Server Info", the server box (ServerInfoEdit, no scroll bar) reading "Checking
  For Server" (8131) and the address's host without its port, "Password:" over the password box holding the
  script's password in plain text with the caret before it, Cancel, and Join (8133 "Join", not the layout's "Join
  Internet Game"), greyed while the server is checked. The box's lines are the layout font 17 px apart from its text0.
- **A server that answers** (within a second) is listed instead: "Server Name: x", "Module Name: y", "Players: (1/3)
  Levels: 1 - 40", "Password Required: False", "Local Characters Allowed: False" (8134-8138, 8141/8142, 57998), and
  Join lights up. **One that never answers**: after 10 s the chat says "Direct connect and UDP hole punch failed.
  Requesting relay token.", "Relay access was denied.", "Relay said:", "Server is in a private network." and "You are
  no longer portalling, you can do things again." (53293), and the prompt stays as it was, Join still greyed.
- **Portalling**: the first portal prints "You are portalling, you can't do anything right now." (53292), yellow; a
  portal while portalling prints nothing more. Cancel closes the prompt and prints 53293. Esc does nothing (no game
  menu either). Another portal while the prompt is open replaces the host and password and checks again; one with an
  empty address (nw_defportal's call) leaves an open prompt alone and opens none, but still starts portalling.
- **Join** goes straight to the loading screen (no 53293) and the player arrives **at the waypoint** the portal named.
  **Seamless** (bSeamless TRUE) shows no prompt and no lines and arrives at the waypoint too.

OpenWinter (`client/UI/PortalPrompt.cs` for the logic, `ClassicPortal` and the modern `PortalDialog` for the looks):
the check is a connection that sends only a pre-login `server-info` request, which OpenWinter servers answer (server
name, module, players, levels 1 - 40, no password, local characters when the vault is local); anything else fails
after the same 10 s, printing only 53293 (the relay lines belong to the original's master server). Join reloads into
`--connect` with the portal's waypoint in the Hello (`PortalWaypoint`), and the server places a newly arriving player
at the waypoint with exactly that tag (the tag is case sensitive). OpenWinter servers take no player password, so the
password box is shown but not sent. The modern prompt has the same contents and also closes on Esc.
`--capture-portal <dir>` shoots both looks in single player; `mp.py run mp_portal [--client-args "--ui classic"]` runs
the measured scene on OpenWinter.

### Journal world page

The game data has a World page for the journal (`jnl_page_world` on pnl_jnl_page2: "Sort by:" Date / Name on
`ctl_chk_pre`, a list of entries (`QuestBox`, ctl_jnl_lstbx) over the selected entry's text (`TextBox`,
edit_jnl_348x313), and a `newbutton` node) and a Dungeon Master popup to add an entry (`jnl_wld_dm_new` on
pnl_con_prtl_inf: a title line, a message box, Create New Entry (10317) and Cancel), with a world tab model
(`ctl_jnl_tab_wrld`) from an older four-tab icon set (`ctl_jnl_tab_qst` / `done` / `note` / `wrld`, 51 px each).
**The current original never shows any of it**: the journal has the same three tabs, Quests, Completed Quests and
Notes, in single player (`classicui`), for a player on nwserver (`mp_journal`) and for a Dungeon Master on nwserver
with the DM client (`mp_journaldm`); the three EE tabs fill the 360 px panel. No script function makes a world entry
and nwserver offers no setting for it, so what fed the page can't be measured.

OpenWinter builds the page (`ClassicJournal`, from the layout: the entries newest first or by name, the selected
entry's date in grey over its text; `ctl_chk_pre` draws its X only while checked, as its states don't hide it; the
list is clipped at its Viewport0 node because its scrolling row pattern reaches 31 px lower) but, as the original,
gives it no tab: `ShowTab(3)` opens it, with entries a server sends as a `journal-world` notify
(`{"entries": [{"title", "text", "day", "time"}]}`; no server sends one) or a capture sets
(`--capture-classic-sheets` shoots `e_journal_world` with two sample entries). The modern journal has no world page
either, and Dungeon Masters have no way to add world entries (neither OpenWinter's server nor nwserver supports
them), so the DM popup is not built.

### Chat window

Measured with the scenes `chatlines`, `chatsplit`, `chatsplit2`, `chatradial`, `chatmenuheld` and `convoend` (chat
logs from the original's `game.log.chat.all`).
- **Lines:** spoken lines (talk, shout, whisper), conversation lines and answers show the speaker's tiny portrait
  in the margin. It is the 16 x 25 picture part of the "t" portrait in a 1-pixel (99,101,99) frame at x 6,
  with text from x 30, and such a line stands 35 px from the next. The speaker's name is lavender (143,127,255),
  with "Name: line", but "Name : [Shout] line". Server messages and feedback have no portrait.
- **Combat and rolls:** orange (255,102,1). The acting creature's name comes first, cyan (153,255,255) for your
  own character and pink (204,153,204) for anyone else; "Someone" stays orange. Each damage amount takes its
  `damagetypegroups.2da` colour (Magical violet, Fire red, and so on).
- **Menu:** right-clicking a bar opens the original's chat menu, a classic ring like the others (hover labels, the
  pointer band, hold-and-release or click, Esc / click outside to close). Its centre is `ir_chat` on the large disc
  (`gui_ra_circle112`) and it centres 154 px from the screen edges. Clockwise from the top: split / combine
  (`ir_chatsplit` / `ir_chatcombine`, strings 1405 / 1406), party chat (`ir_partychat`), server messages
  (`ir_servermsg`), Dungeon Master messages (`ir_dmchat`, 67572 / 67573), normal chat (`ir_chat`, 67574 / 67575:
  talk, whispers and tells), combat info (`ir_cmbtinfo`) and shouts (`ir_shouts`); the upper left is empty on the
  original. Only the bottom label ("Exclude Normal Chat") was read from a native shot; the others follow their icons.
  The original shows no dialogue, dice roll or player list options on this ring; OpenWinter keeps them in a submenu
  (`ir_chatmenu`, "Other") in the empty upper left.
- **Split:** two windows side by side, each half the chat's width (x 0-359 and 360-719 at 1280x720), each with its
  own bar (black, grey rim, rounded at both ends, grip in its middle), scroll bar and height. Speech stays on the
  left; server messages, combat information and dice rolls go to the right. A window drags down to no lines (its bar
  alone), as the original's does. OpenWinter saves the heights in lines (`ChatLines`, `ChatSecondaryLines`) with the
  menu's choices. Not measured: whether the original can change the widths (its bars carry only the vertical grip),
  whether it saves the split itself (its saved heights `ui.chat.pane.primary/secondary.height` don't split it), and
  where conversation lines go when split (kept on the left).
- `--capture-classic-chat <dir>` (with `--ui classic`) holds the menu over its options, opens the submenu, hovers in
  click mode, splits and drags both windows, with checks.

### Text rendering

`classictext` captures names/status, chat, a conversation and a selected reply with the regular face:
`python tools/visual/visual.py run classictext --openwinter-args "--ui classic --visual-delay 0"`. The scene's
`largeFont` is forwarded to both clients; `--large-font true|false` overrides the client for a run. Options →
Interface → High-resolution font selects the serif face for the next game. Regular chat is 11 px, 15 px pitch;
high-resolution chat is 14 px, 16 px pitch. Script server messages are yellow; ordinary feedback stays grey.
Nameplates share the TXI coverage correction. These are measured rendering fits; Godot's rasterization and glyph
advances are not identical to NWN's.

### Armour icons

Measured on nwmain with `--profile plain` (scenes `armouricons`: elf female, `armouriconsf`: human female,
`armouriconsm`: human male; 23 built pieces, a helmet, two cloaks; inventory pages, the equipped slot, the quickbar,
examine windows, a chest and a store), every pixel compared with the installed part PLTs painted offline. Body
armour's icon is built from its parts (`ArmourIcon`, `InventorySystem.PartIcons.cs`; the client paints each layer
with `PltIcon`):
- Layers, bottom first: `ip?_pelvis`, `chest`, `belt`, `shol` (the left shoulder's model), `shor` (the right
  shoulder's), `robe`. Each is a 64x128 PLT over the whole canvas; the icon is its top 64x96 (2x3 cells) in the grid,
  the equipped slot, stores, containers and the examine window; the quickbar fits that 64x96 into the slot (21x32),
  and the modern bar uses the same shown canvas.
- `ipf_` for a female player, `ipm_` for a male one, also for items in stores and containers.
- A part with model 0 or no installed icon is left out. A robe hides what its parts_robe.2da row hides (HIDEPELVIS,
  HIDECHEST, HIDEBELT, HIDESHOL, HIDESHOR) and goes on top; robe 30 keeps the chest and shoulders, robe 20 the pelvis.
- Colours: the item's six (leather 1/2, cloth 1/2, metal 1/2 on PLT layers 6/7, 4/5, 2/3). Both metal layers read
  `pal_armor02`; a pixel also takes the palette's alpha, so layers blend where the palette is translucent (a dark
  leather 2 belt edge over the pelvis). Skin, hair and tattoo rows are 0 (a player with skin 14 still sees row 0).
- EE per-part colours: pelvis, chest and belt take their own, the `shol` icon the right shoulder's
  (ITEM_APPR_ARMOR_MODEL_RSHOULDER) and `shor` the left shoulder's; the robe takes none.
- Unidentified armour shows the same icon on plain cells.
- Helmets (`ihelm_###`) and cloaks (`icloak_m_###`, also for female players) are one PLT each, painted the same way.
- Haks (scene `armouriconshak`: part PLTs under new numbers, longer parts tables, a parts_robe.2da row and a new base
  item copied from armour): the original draws hak part icons at any number, names them with at least three digits
  (chest 1000 is `ip?_chest1000`; `chest100` and `chest000` are ignored), hides what the hak's robe row hides and
  builds the icon for a hak base item with ModelType 3. OpenWinter matches all four (`ArmourIconTests`, side by side
  within 0.25 per channel).
- The server decides which layers exist from its own data and sends them; a client missing a layer's PLT (a
  server-only override) leaves that layer out and draws the rest (when no layer resolves the classic slots stay empty
  and the modern ones show their kit glyph). A part the server lacks is never sent, even if the client has it.
- Not matched: with the elf female player the original also drops parts when the pelvis or chest is missing or hidden
  (no pelvis icon: `shol` only; no chest icon: no `shor`; robe 30: robe and `shol`; robe 38: robe only); the human
  players don't, and OpenWinter follows them. Not measured: the drag cursor and the male elf.

### Inventory handling

- **Sounds.** Pickup and placement are separate sounds: taking an item plays the installed `it_pickup`; placement
  plays its server-resolved inventorysnds sound. Armour resolves from physical base AC (cloth 0/1, leather 2/3,
  chain 4/5, plate 6+), unaffected by magical AC or identification. Classic controls and modern slots use the same
  rules.
- **Carrying.** Carrying an item across a page tab needs a short dwell; Escape or right-click cancels without
  dropping. A completed put-down refreshes the inventory. The examine button selects `cursor17`.
- **Gold.** Gold buttons attach the selected amount to the cursor (the classic dialog uses `inv_stacksplit`). A
  terrain drop sends a queued action the server revalidates against the balance: the player runs to within one
  metre of the point, faces it, then puts the gold down after the put-down delay; near points need no movement.
  Cancelling keeps the gold, and the server rejects stale-area or invalid terrain points.
- `--capture-items <dir> --inventory-drop-only` (add `--ui classic` or `--ui modern`) checks drag cancellation, brief
  page-tab crossings and the completed ground-drop action, writing PNGs and `inventory-drop.json` per phase;
  `--visual-audio` also records the client's Master bus.

### Item right-clicks open a radial menu

The original has no drop-down lists for items. Right-clicking an item in the inventory, the equipment or an open bag
opens the item's radial menu whatever else is open: a container, a store or a bag. A bag's ring adds Open at E
(double-click still opens it). With a store open the ring has Sell (`ir_sell`) at E in place of Equip (scene
`storeradial`), and a ware's ring is Examine N and Buy E. Putting items into a container stays a drag. Items in a
container (Examine N, Take S, Take All SE) open a ring too, which closes with its window; those directions are
OpenWinter's choice (not measured). The modern interface opens the same rings with a store open and keeps its
drop-down for bags and with a container open. `--capture-classic-inventory` right-clicks a ware and a backpack item
with a store open, and a backpack and a chest item with a chest open; `--verify-dm-radial` checks the bag's Open
entry.

**Shift-click sells with a store open** (a deliberate [quality-of-life upgrade](QUALITY_OF_LIFE.md), the counterpart
of Shift-click buying in [CLASSIC_STORES.md](CLASSIC_STORES.md)): Shift-click on a backpack or bag item sells it at
once, a whole stack included (Shift-click on a stack splits it only when no store will buy it).
`--capture-items <dir> --quick-sell-only --ui classic` sells a dagger and a stack of three potions.

### Radial menus

The shared radial model reserves eight compass directions; `ClassicRadialSkin` reads the `gui_ra_more` arrows and the
`gui_radialband` strip, draws child previews and places yellow labels outside the hovered icon. Submenus remove the
target portrait, recentre the pointer and draw its white line.
- **Self**: Character Sheet N, Cast Spell NE, Attack E, Special Attacks SE, Issue Command SW, Special Abilities W and
  Emotes NW. Wizard spells keep the class and level rings even when few spells are available. Spellcasting
  Abilities contains Counterspell, Defensive Casting and innate spell abilities. Issue Command uses the authenticated
  voice-chat associate-command request. Craft opens the installed `x0_skill_ctrap` on the requesting player and
  leaves its authored condition/action scripts intact. Search stays present for characters with Keen Sense.
- **Another creature**: Examine N and its portrait at the root; a submenu replaces the portrait with its parent icon.
  One's own associate has its own ring of commands instead of Attack and Special Attacks
  ([ASSOCIATE_COMMANDS.md](ASSOCIATE_COMMANDS.md)).
- **Doors and placeables**: Examine N, Cast Spell NE, Use/Open/Close E, Trap SE, Bash SW and conditional
  Lock/Unlock W. The classic ring shows the object's authored portrait, including the original's default for a door
  with portrait id 0. Lock commands dispatch the authoritative actions, including matching keys.
- **Dungeon Master tools** fill a vacant root wedge rather than displacing an entry. A full self ring (Cancel
  Polymorph present) keeps all eight root directions and puts the DM tools inside Special Abilities. The original's
  DM-specific placement has not been measured. `tools/godot.sh --headless -- --verify-dm-radial <out dir>` checks
  the root builder and DM extension across self, polymorphed self, creature, door, placeable and item contexts.
- **Sounds**: opening a ring by keyboard or world click is silent in the original (scene `radialsounds`), so neither
  style plays the installed `gui_radial_open`; submenu selection plays `gui_radial_slect`.
- Visual scenes: `radialmenus`, `radialtarget`, `radialdetails`, `radialcraft` and `radialmodes`.
- Not matched yet: non-wizard class/feat ordering, non-creature roots beyond doors and placeables, trained trap
  submenu order, ground-item equipment commands, preview rasterization and long-ring pagination.

### Barter window and item targets

`ClassicBarterWindow` draws `ingame_barter` beside the inventory ([BARTER.md](BARTER.md); the layout comes from the
.gui file, the behaviour is inferred). A spell or ability waiting for a target takes a clicked inventory item
(casting at blank scrolls, bottles and wands; [CRAFTING.md](CRAFTING.md)). Store panel placement with a live
conversation is described in [CLASSIC_STORES.md](CLASSIC_STORES.md).

### Level-up feedback

Measured with the `levelup` scene. When experience first allows a level the original plays `gui_level_up`, floats
"\*Level Up!\*" (TLK 53242) over the player in the mode-feedback style (white, gone within 1.5 s), writes TLK 8302 in
the chat and swaps the party bar's portrait for `po_levelup` until the experience no longer allows a level. More
experience, or a level taken while another is allowed, repeats none of it. The character sheet's level-up button
reads "Click To Level Up" (TLK 8346). Taking a level through the level-up window is
described in [CLASSIC_CHARGEN.md](CLASSIC_CHARGEN.md).
