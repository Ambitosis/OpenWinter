# Parity lab

Black-box comparison with the original game. A suite is a folder of NWScript that measures
something and logs it. The same generated module runs on the original dedicated server
(`nwserver.exe`) and on OpenWinter, and the two logs are compared by key.

```
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run basics            # both, then diff
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run basics --only nwn # one side, print values
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run basics --own-compiler # OpenWinter runs its own compiler's output
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run basics --only openwinter --saved # against the original's last saved run
```

- `parity/include/parity_inc.nss` holds the reporting helpers: `PLog`, `PLogI`, and `PLogF`
  (a float with an optional tolerance). Call `PDone()` at the end; both engines stop once they see it.
- Each suite is `parity/suites/<name>/`. `parity_main.nss` runs at module load. An optional
  `suite.json` sets `seconds` (the time limit), `tileset`/`tile`/`size` (the flat test area),
  `minPerHour` (real minutes per game hour, default 2; `fx_disease` uses 1) and
  `events` (more module events, e.g. `{"OnHeartbeat": "my_hb"}`).
- `SetTime` moves the clock forward and the original then runs every `DelayCommand` that falls due at once;
  schedule what comes after a jump from the script that jumps (see `fx_disease`).
- Scripts are compiled with the official compiler from neverwinter.nim, found under
  `<userRoot>/tools`, so both engines run the same bytecode. Generated modules use
  base-game data only, with no haks.
- A `baseitems.2da.json` (or another table name) can contain `{"rows":[{"row":200,"copy":49,"values":{"label":"Probe"}}]}`.
  The builder clones installed base-game rows at runtime, then applies authored column values; `copy` and `values`
  are optional. Generated tables go only into the output module and that suite's native sandbox override.
  A `probe.uti.json` containing `{"template":"nw_it_mpotion001","baseItem":200}` similarly creates an item
  blueprint from installed data at runtime. Keep source fixtures as deltas; never commit generated game assets.
- Runtime-cloned `.utc.json` creature fixtures accept optional `"skills":{"9":10}` deltas.
  Keys are skill-row IDs and values are base ranks; unspecified installed ranks remain unchanged.
  Use explicit native rank/eligibility gates before treating a failed skill use as behavioral evidence.
  They also take `"templateModule": "Prelude"` (the template from an installed official module), `"keepScripts": true`,
  `"items": [{"resref", "dropable", "pickpocketable", "embed"}]` (and the same keys on `"equipment"` entries: flags
  left out stay out, as the toolset leaves them; `embed` writes the whole item as an area instance carries it) and
  `"fields": {"DecayTime": {"dword": 0}, "IsDestroyable": {"byte": 0}}` (`corpse_drops`). A visual scene's official
  `"source"` can bring that module's compiled scripts with `"moduleScripts": ["m1q0ddeadgob_9"]` (`pavelroom`, `corpsehilite`).
- `nwserver` runs with `-userdirectory out/parity/user/<suite>`, and each suite gets its own port.
- `"nwnx": true` in suite.json measures NWNX:EE plugins: the original side runs on the Linux nwserver with NWNX:EE
  in WSL (`tools/nwnws/nwnws.py setup` once), in `/home/nwnws/parity/<suite>`, with no outbound network. Scripts
  compile against NWNX:EE's own `nwnx_*.nss` headers (copied from that build into `out/nwnx/scripts`). Every plugin
  loads except those that need outside services or change engine behaviour (Optimizations, Experimental, HTTPClient,
  Redis, WebHook ...); NWNX_SQL uses SQLite. `"nwnx": "risenholm"` also loads the `NWNX_Risenholm` plugin, from the
  build that `nwnxBinaries` in config/local.json names (only those suites use that build; the rest run on upstream
  NWNX:EE), and compiles against that fork's `nwnx_risenholm.nss`. `"nwnxEnv": { "NWNX_TWEAKS_...": "true" }` sets
  environment variables for NWNX on both engines, after the lab's defaults (`NWNX_SQL_TYPE=SQLITE`,
  `NWNX_SQL_DATABASE=nwnx_sql`). `nwnx_pilot` is the smallest example.
  It never touches the real user folder.
- A suite can ship conversations as `<resref>.dlg.json`, in the format visual scenes use (`convo_busy`).
- Results are written to `out/parity/results/<suite>/{nwn,openwinter}.txt`.

- `--own-compiler` compiles the OpenWinter side with OpenWinter's built-in compiler (the one behind
  ExecuteScriptChunk and CompileScript), so a suite also checks that compiler against the official one.
  `openwinter-tools nss-check <folder> [out]` compiles every script in a folder with it.
- Script errors (the original's engine log, OpenWinter's "Script error:" lines) become readings
  `error.<script>|<text>`. Every selected engine must report the exact `__done__|1`
  completion marker and no script errors or unimplemented functions. Otherwise the command
  exits nonzero, including `--only` runs and identical failures on both engines. Matching
  partial readings are not a successful parity run.

Rules of thumb:
- Compare deterministic values exactly. For anything random, run many trials, log the rate,
  and give it a tolerance.
- Areas without players run at a low AI level in the original. Call `SetAILevel(o, AI_LEVEL_NORMAL)`
  on creatures that must act.
- The default tile (`tde01` 121) is a walled corner piece: in the original its walls block line of
  sight and location targets. Suites that cast spells or need open ground use `"tile": 4` (open floor).
- `parity/include/parity_fx.nss` has helpers for effect suites (stat snapshots and diffs, effect lists).
- **NPC hostility needs a faction table.** A generated module has no repute.fac unless suite.json sets
  `"factions": "Prelude"` (or another installed campaign module), and then nwserver makes every NPC faction, Hostile
  included, a friend of every other (`faction_nofac`); Hostile is hostile only to players, and parity runs have none.
  Any suite whose result depends on NPCs being enemies (attacks of opportunity, traps, resting, flanking, combat AI)
  must set `"factions": "Prelude"` and should log `GetIsEnemy` both ways as a gate. `PPair` and `PSquad`
  (`pcombat_inc.nss`) make each attacker and its target enemies with `PEnemies` (SetIsTemporaryEnemy both ways, no
  decay), which holds with or without a table; creatures a suite spawns itself need a table or `PEnemies`. Prelude's
  table isn't symmetric (Commoner rates Commoner, Merchant and Defender 50; Merchant rates Defender 100; Defender rates
  Commoner and Merchant 100; Hostile and the rest 0 both ways).
- Every difference is a bug in OpenWinter unless the suite proves otherwise. Fix the engine,
  then rerun until the suite matches.

Item suites (`items_*`) share `parity/include/parity_items.nss`; `items_events`, `items_give` and `items_saved` also log the
module's item events in order through `parity/include/parity_itemev.nss` (each event script appends to a log that `PEvLog`
writes out). A suite's `"instances"` can also place items on the ground (`"type": "uti"`). `items_blueprints1/2` and `items_value1/2`
are generated: `gen_blueprints.py` takes the list from `openwinter-tools list "" uti --base`, and `gen_value.py`
takes a folder of base-game 2DA dumps (`openwinter-tools 2da <name> 2000 --base`).

`items_value_charges` reads item values around charges, stacks, property changes and holders (docs/CLASSIC_STORES.md);
`craft_feats` runs the stock crafting scripts (docs/CRAFTING.md). A visual scene's `<store>.utm.json` may carry
`"fields": {"MarkUp": 150, "BlackMarket": 1, ...}` (`storeprices`).

`state_scripts` overrides statescripts.2da (a `statescripts.2da.json` fixture) so each condition's state script only
logs, and reads when and which one runs (docs/CONDITIONS.md "Control taken away"); `state_commandable` reads
GetCommandable around them. `round_idle` (round lengths, swing spacing, the idle OnEndCombatRound, the stock
end-of-round AI's choice), `archer_aoo` / `archer_aoo_normal` (the same check of shots, steps and attacks of
opportunity at D&D Hardcore and at Normal, shared `parity/include/paoo_inc.nss`), `strike_back_times` (when queued
casts start), `stealth_check_stance` (observer stances; a beacon marks each observer pass), `perception_leave` and
`stealth_check_interior_far`: docs/STOCK_AI.md, docs/STEALTH.md. `strike_back` (a script-less victim
and an NPC caster), `archer_backoff` (archers with no scripts, ActionMoveAwayFromObject), `round_clear` (ClearAllActions
and a new attack mid-round) and `attack_mate` (ActionAttack on a faction mate): docs/STOCK_AI.md.

Ordering and AI levels (docs/STOCK_AI.md): `sched_events` (DelayCommand ties, SpeakString
listeners' order and who hears creatures and placeables), `sched_percept` (the order of one pass's OnPerception events,
the order observers are served, heartbeat spacing; which observer notices a new creature first varies from run to run
on nwserver, so only each observer's own events are compared), `ai_level_auto` (GetAILevel without players: combat,
SetAILevel clamps and locks, AI_LEVEL_DEFAULT, a placeable) and `attack_leave` (passive and explicit attacks whose
target jumps to another area, connected or not; docs/INTER_AREA_MOVEMENT.md).

Cast timing: `projectile_paths` (flight time by ProjType, the pole, script path types;
docs/SPELL_PRESENTATION.md), `breath_timing` (a dragon's breath, hasted, instant, Wing Buffet and the walk before it;
docs/STOCK_AI.md), `cast_concentration` (damage, entangle and clears while casting;
it waits 12 s and spaces its casts, because a loaded nwserver ends conjuring late) and `queue_cadence` (six queued
scrolls, wands, potions or casts, every gap). `cast_provoke`
(which casts draw attacks of opportunity, defensive casting set idle or mid-round).
More timing and targeting suites: `aoo_land` (who makes a cast's attack of opportunity and when: plot, immortal,
idle, fighting, facing away), `swing_marker` / `swing_marker_busy` (swings timed by OnDamaged, empty and populated),
`queue_single` (single or doubled scroll gaps, instant and cheat casts), `conc_previous` (what a failed Concentration
check costs), `route_arrival` / `moveaway_legs` (stands on arrival, move-away), `approach_click` (ActionMoveToObject at
triggers with and without OnClick, trap triggers, transition triggers and doors), `trap_crit_ctl` (disarm critical
failures with controlled factions, plot creatures, the search faction split) and `equip_round3` (most-damaging scores
by target). Several readings vary from run to run on nwserver; their tolerances say so in the suite.

Stock AI suites (`ai_*`, docs/STOCK_AI.md) share `parity/include/pai_inc.nss`: creatures keep their own event scripts
behind one-line wrappers (`pai_e0`..`pai_e12`, written with the suites' suite.json by `python parity/gen_ai.py`) that log
each event first; fights run one per area and are compared as rates and means. `PLogSeq` (parity_inc) logs a
"value@time" sequence as the values (exact) and each time (with a tolerance). Lines a suite writes as `RAW|key|text`
are kept, uncompared, in `out/parity/results/<suite>/{nwn,openwinter}.raw.txt`.

Combat suites (`combat_*`) share `parity/include/pcombat_inc.nss`: attacker/target pairs (`PPair`) and
squads of attackers on one passive target (`PSquad`) on a grid of 10 m cells, event scripts that count
attacks, hits and damage per group (each suite carries one-line `pc_*.nss` wrappers), and report helpers
whose rate and mean tolerances come from the sample size. Notes from measuring nwserver:
- A creature attacking nobody is flat-footed even in combat, so passive targets lose their Dexterity bonus.
- `OnPhysicalAttacked` and `OnEndCombatRound` events can arrive a few ticks late under load: compare
  counts and averages, not exact times within a round.
- Modes set with `SetActionMode` (Power Attack, Flurry of Blows...) have no effect on these scripted NPCs: they read on
  through the first round and off after it (`combat_modes`).
- `combat_hitdelay` (when blows land), `combat_immortal` (immortal and plot targets), `combat_archer` (archers step
  back), `combat_unarmed`, `combat_equip` (weapon sizes), `combat_races` (racial immunity), `combat_extras` (Haste and
  Slow AC, Defensive Roll, Death Attack). Visual scenes `difficulty`, `difficultyhits` and
  `difficultypc` measure the game difficulty on a player; run them with `visual.py run <scene> --difficulty N`
  (docs/DIFFICULTY.md). `diffscale` logs about 1,800 weapon blows per difficulty (player, henchman and creatures,
  both ways); `python tools/visual/check_diffscale.py out/visual/shots/vis_diffscale_dN/nwn` compares them with the
  diffsettings.2da formulas.

## Multiplayer

`python tools/multiplayer/mp.py run mp_sync` runs openwinter-server and two OpenWinter clients side by side on the dev
monitor, driven by a visual scene's scripts (`parity/visual/include/mp_inc.nss`), and reports where the two views and
the server disagree; `--only nwn` does the same with a sandboxed nwserver and two nwmain clients (the second needs its
own CD key). `mp_delivery` and `mp_join` measure, with one original client, which lines reach a player. See
docs/MULTIPLAYER_HARNESS.md.

## Visual

`runturn` holds W with A/D, then repeats with Shift, using the installed human `fighter` character.
Run with `--openwinter-args "--visual-movement-trace 1"`, then
`python tools/visual/check_runturn.py out/visual/shots/vis_runturn/openwinter/movement.csv`.
The checker requires two uninterrupted runs and two walks, with no animation transitions during
each steady segment. `namewidth` compares short, medium and long names in full portrait-bubble mode.

The same scripted scene on the original client (`nwmain.exe`) and on OpenWinter, screenshotted from the same
camera and paired side by side. Python 3 with Pillow and NumPy drives it (Windows only: it captures the
original's window).

```
python tools/visual/visual.py run calib rural interior   # build, capture both engines, pair
python tools/visual/visual.py run all                    # every scene
python tools/visual/visual.py run vfx --only nwn         # one engine (the other side's old shots are kept)
python tools/visual/visual.py compare city               # re-pair existing shots
python tools/visual/visual.py run lightcal --profile plain                          # original without post-processing
python tools/visual/visual.py run rural --only openwinter --openwinter-args "--visual-graphics Glow=false;Ssao=false"
python tools/visual/visual.py run keyhole --keyhole "min-diameter=0.5,max-diameter=0.5" --tag _05   # keyholing settings
python tools/visual/visual.py run partyframes --large-font false --tag _small   # both engines' font (the original's default is the large one)
```

How it works:
- A scene is `parity/visual/scenes/<name>/` with a `scene.json` and NWScript (`vis_main.nss` runs when the
  player enters the area). `openwinter-tools visual-build <scene>` builds `out/visual/build/vis_<scene>[_<variant>].mod`
  with our GFF/ERF writers and the official compiler. The area is either generated from one tileset (as in the
  parity lab) or copied from an official module in `data/nwm` / `data/mod` (`"source": {"module", "area"}`) with
  its placeables, doors and sounds but no creatures, encounters or triggers. `"are"` overrides area fields
  (lighting, fog, day/night, flags); `"variants"` builds one module per set of overrides (day and night, say).
  `openwinter-tools visual-tiles <tileset>` lists a tileset's flat tiles.
- `parity/visual/include/visual_inc.nss` has the helpers: `VBegin` (cutscene mode, which hides the original's
  GUI; top-down camera mode; wide camera limits), `VCamera` (`SetCameraFacing`: the camera orbits the player),
  `VMovePC`, `VHidePC`, `VSpawn` (a standard blueprint, made neutral), `VFxAt`, `VStandardViews` (three views,
  then `VDone`) and `VShot`,
  which logs `VISUAL|shot|<name>|<player position>|<facing>|<player name>`. Scenes hold still for two seconds
  A scene can ship its own conversations as `<resref>.dlg.json` (entries, replies, Animation, sound; see
  `Visual.Dialog`), and `VISUAL|note|...` lines are kept in `notes.txt` beside each engine's shots (readings to diff).
  after each shot and end with `VDone`.
- The original: a sandbox user folder `out/visual/user` (never the real one) gets a `settings.tml` (windowed
  1280x720, no movies, no pause in the background; `--profile plain` also turns off bloom, SSAO, contrast,
  vibrance and sharpening), the module and one character. `nwmain.exe -userdirectory out/visual/user
  +TestNewModule vis_<scene>` loads the module straight into the game with no clicks. It is started minimised
  and restored without activation, so it never takes focus or the mouse; the harness tails
  `logs/nwclientLog1.txt` and captures the window with `PrintWindow` (`tools/visual/wincap.py`) on each shot line.
  Both engines' windows open on the monitor named by `"devDisplay"` in `config/local.json` (e.g. `"DISPLAY2"`),
  moved there without activation, so the primary monitor stays free.
- OpenWinter: `--capture-visual <dir>` (client/MainCaptureVisual.cs) runs the same module in single player at the
  same size, through its own config pointing at the same sandbox user folder, hides every 2D layer and
  screenshots 0.35 s after each shot line (about the original's capture latency). `--visual-probe 1` also writes
  `<shot>.probe.txt`: the view depth and height of whatever each 4x4-pixel cell sees (rays against the camera
  collision and tile-fade geometry), to relate the original's pictures to the geometry.
- `--keyhole "key=value,..."` sets both engines' keyholing (the original's `[graphics.keyholing]`: `enabled`,
  `min-diameter`, `max-diameter`, `min-z-offset`); `--tag _x` keeps such a run's shots and pairs apart
  (`vis_<scene>_x`). The original with `min-z-offset=6` cuts nothing at these views, a clean reference to diff
  the hole against (`enabled=false` also turns its camera collision back on, which moves the camera).
- Shots: `out/visual/shots/<module>/{nwn,openwinter}/<shot>.png` with `shots.txt` (what the script logged, so you
  can check both engines put the player in the same place). Pairs: `out/visual/compare/<module>__<shot>.png`
  with mean luma and RGB on each side, and `out/visual/compare/index.html` with all of them.

Scenes: `calib` (camera calibration), `lightcal` (ground under known ambient and diffuse colours), `tilelight`
(a castle floor lit only by tile main lights of known lightcolor.2da rows), `placelight` / `placelight3` (a dark
floor lit by script-made braziers and a lamp post; `python tools/visual/lightprofile.py nwn|openwinter` prints the
falloff), `throne` (the HotU throne room, lit only by static braziers), `fogcalnight` (moon fog), `lightcalnight` (moonlight,
including the dim Chapter 1 graveyard colours), `flames` (a brazier, candelabra and torch up close, for flame and glow
work), `fogcal`
(fog amounts 5 to 15 at 6 to 36 m; `python tools/visual/fogfit.py nwn|openwinter` prints the fog fraction, and
`python tools/visual/clipfog.py nwn|openwinter` the distance fog along the low view),
`city` (Chapter 1 docks, tcn01, day/night), `rural` (Chapter 1 graveyard, ttr01, day/night), `woods`
(Chapter 1 end, ttr01 night with a lighting scheme), `interior` (a tin01 house), `castle` (the Prelude academy,
tic01), `sewer` (tds01), `crypt` (tdc01), `convobusy` to `convobusy4` (busy refusals and fights during conversations; `python tools/visual/check_convobusy.py`), `talk` (conversations: who faces whom, line animations, a sitting, an uncommandable and a placeable partner), `talkanim` (every line Animation value beside a creature playing the ANIMATION_* constant), `talktorch` (a torch holder talking), `paveltalk` (Pavel in the Prelude barracks, talked to), `creatures` (standard monsters and part-based NPCs, the player up
close), `vfx` (glows, Magic Missile, Fireball, a summoning, day and night), `torch` (NPCs holding torches on a dark
floor, one walking), `torchwalk` (torch-bearers walking and running, close up), `torchlight` (one torch-bearer's
light on an unlit floor; `python tools/visual/torchprofile.py` compares the falloff) and `pavel` (Pavel in the
Prelude's senior barracks with his uniform and torch, and the uniform's torso on a human, an elf and a dwarf).

Weapon scenes (`docs/WEAPON_STANCES.md`): `weaponstance1..3` (twelve actors each: idle, walking, running, combat and
defence with `ReplaceObjectAnimation` marker clips), `weaponready1..6` (four actors large in the frame, one candidate
group per phase) and `weaponclose` (a light mace and a morning star close up, idle, walking and attacking). Run them
with `--native-framebuffer`.
Creature swings (`docs/WEAPON_STANCES.md` "Creature models", `parity/visual/include/cswing_inc.nss`): `creatureswing`,
`creatureswing2` (creatures with different creature weapons attack skeleton dummies; one candidate clip group per
phase becomes the marker `ckdbckdie`, sampled by 6 fps bursts), `creatureswing3` (four stock dire badgers, distances
logged), `creatureswing4..7` (creatures rooted at set distances from their dummies) and `creaturereach` (where creatures
stop closing in; `VISUAL|note|ws|dist` lines only). Run them with `--silent`; a run's bursts and pairs take a few GB,
so delete `out/visual/compare` and the burst frames after reading them.

Trap scenes (`docs/TRAPS.md`): `traprules` (a player and a henchman: Search finds, walking into traps, disarm,
examine, flag, recover, Set Trap with a kit, opening a trapped chest; chat log on) and `traplook` (hostile, own
and inactive trap triggers and a trapped chest from three cameras (its flagged trap stayed off-screen), a left click and the radial menu on a trap
trigger, the map, then more rules; run with `--real-mouse --native-framebuffer`). The trap suites share
`parity/include/ptrap_inc.nss` (an event log, per-object event strings and every trap property); `trap_api`,
`trap_trigger`, `trap_disarm`, `trap_detect`, `trap_set` and `trap_npc` cover the script functions, who sets traps
off, Disable Trap, Search and Set Trap with NPCs. `trap_trigger`, `trap_detect`, `trap_npc`, `trap_disarm`,
`trap_rules_npc` and `trap_disarm_crit` use `"factions": "Prelude"` (without a faction table no NPC finds
or sets off another's trap); the last two set the reputations they need with SetStandardFactionReputation.

Encounter scenes (`docs/ENCOUNTERS.md`): `encounterbudget`, `encounterparty` (`parity/gen_encounterbudget.py`),
`encounterrules`, `encountermix`, `encounterspawn` and `encounterwaves` (`parity/gen_encounterwaves.py`: how far a
henchman may stand and still count, and respawn waves against the point pool).

Player-side checks: `encmodes` (stealth and detect stages), `bleedinst`
(instant EffectHitPointChangeWhenDying), `playerai` (AI levels, a player's attack whose target leaves, an encounter
creature's chase), `plotshot` (a player's shots, plot and immortal threateners; run with `--difficulty 3` and `2`),
`loadevents` (an autosave for the original's load events; loading it under menudrive didn't work).

Encumbrance (`docs/ENCUMBRANCE.md`): the suite `encumbrance_speed` (NPC loads, a cloned 1-tenth probe item) and the
scenes `encumberspeed` (load lines, inventory weight) and `encumberdrive` (the player's speed by keyboard and script at
each load, with Strength effects, stealth, speed effects and plot). `combatlines`: which fights print lines for a
nearby player. `quickbarradial` (`--real-mouse --native-framebuffer --silent`): the quickbar
slot's radial (docs/QUICKBAR_RADIAL.md); `VISUAL|pointer|x,y` rests the real pointer for hover labels.
`quickbargaps`, `quickbarexport` and `quickbaremote2` (same flags) and `quickbarsound` / `quickbaremote`
(`python tools/visual/combat_audio.py <scene> --sounds gui_|vs_fshaldrf_ --real-mouse`) measure text macros, slot
Examine, the emote rings and the ring's sounds. `VISUAL|wall|<seconds>|<command>` runs a command on the harness's
wall clock (steps inside windows that pause single player); `VISUAL|mouse|x,y|down|right|real` presses the real right
button (the quickbar ring opened only now and then from posted ones). A native screenshot that a focused text box
swallows falls back to the window capture.
`scrollicons` (`--real-mouse` for its drag and radial shots): scroll, potion and wand icons in the inventory,
quickbar, examine, a chest and a store. Visual scenes take the parity
lab's runtime-cloned `<table>.2da.json` (built into the scene's hak, which both clients read) and `<item>.uti.json`
fixtures; a `.uti.json` may also set `"castSubtype"` (the iprp_spells row of the blueprint's first Cast Spell property).

Kill scenes (`docs/KILL_XP.md`): `killxp` and `killxp2` log the experience every party member gains per kill
(challenge ratings, rows by SetXP, scale, party, distance, multiclass), `killxp3` the finer checks (rounding,
single precision, OnDeath order, associates as victims, a dead henchman; run at two difficulties), `killmsg` the chat
lines a death prints.
Their goblins come from a `.utc.json` with `"challengeRatings": [..]`, which builds one blueprint per value
(`kx0`, `kx1`, ...); a single `"challengeRating"` sets one.

Death scenes (`docs/DEATH_AND_DYING.md`): `dying` (dying, healing, bleeding, the stock death panel and respawn, a
custom panel, a raise, the panel shown to the living, rest refusals and a whole rest), `dyingbleed` and `dyingfx`
(EffectHitPointChangeWhenDying: permanent and temporary, removed, healed, killing), `dyingfoe` (an enemy attacking a
dying player) and `restnorest` (a No Rest area). Run the original with `--real-mouse --native-framebuffer`: the
death panel's Respawn takes the real pointer resting on it plus a posted click (`VISUAL|mouse|x,y|up` then
`VISUAL|click|x,y`). Suites `rest_recovery` and `death_raise` cover NPC rest recovery, the 30 m enemy check and the
dead.

Keyholing scenes (the Prelude academy, a wall between camera and player; client/Rendering/Keyhole.cs has what
they showed): `keyhole` (zooms and pitches), `keyzoom` (the camera backing away from one wall, with the default
and the game's camera limits), `keycurve` (the same, finer and from 5 m), `keydepth` (the camera distance held while
the player steps away from the wall), `keytime` (one view held: the hole does not change over time) and `keynear`
(walls just in front of, beside and behind the player).

`keybed` reproduces the Prelude bedroom occlusion issues: bedposts versus mattress, black area borders, and
two nearby doorway angles with a clear sight line. `keybedclose` backs a low camera below and then above the
wall-top border cap. Run it with `--keyhole min-z-offset=6` to see the uncut border; below the cap the original
still sees through the near wall. For the mattress, compare `keybed` at defaults and `--keyhole min-z-offset=0`.
The original cuts the mattress only in the lowered-cutoff run. These scenes use installed Prelude resources.
`keyborder` moves the player close to the bedroom's outside edge. Its `d_edge_reverse` view catches a border
cap hiding the player if the border material skips keyholing; compare defaults with `--keyhole min-z-offset=6`
to check the cut and the intact cap at the same camera position.

Ground item scenes (`docs/GROUND_ITEMS.md`): `grounditems` (one item of each kind on the ground, from above and
low down, with other parts, colours and facings), `grounddrop` (the player puts items down facing north and east),
and OpenWinter-only `groundcarrytop` / `groundcarrytopmodern` (a picked-up ground item drawn over the windows).

Armour icon scenes (docs/CLASSIC_UI.md "Armour icons"): `armouricons` (elf female, a chain shirt on the quickbar),
`armouriconsf` (human female) and `armouriconsm` (human male) share `parity/visual/include/armouricons_inc.nss`: 23
built armour pieces, a helmet and cloaks in the inventory, examine windows, a chest and a store. Run with
`--profile plain` (post-processing changes the colours). `armouriconshak` puts hak art in the scene hak:
`"copyResources": {"ipf_chest300.plt": "ipf_chest186"}` in scene.json clones installed resources under new names into
the hak at build time (with the `<table>.2da.json` and `<item>.uti.json` fixtures).

Container scenes: `chestuse` clicks a created chest and logs the player's position, facing, current action
and chest open state at 0.1 s intervals. `chestbed` uses the placed Prelude bedroom chest, with a potion
added because the fixture strips the source area's loot-spawning scripts. Both take opening frames at
0/0.15/0.30/0.45/0.65/1/2 seconds after observing the open state. Run with
`--openwinter-args "--ui classic --visual-delay 0"`; log polling means these are approximate frame phases.
`chesttable` places a chest at z=1 on a table and logs position, 3D distance, action and open state every
0.1 s. Native opens at ~1.1 s and OpenWinter at ~0.9 s with the blocked-approach fallback; their stopping points
still differ. The ground/bedroom captures also show a remaining lid timing gap: native is shut at 0.30 s
and opens by 0.45 s, whereas OpenWinter is already open at 0.30 s.

Highlight scenes: `hilite` compares creatures and a chest at two distances; `hilitebed` checks the Prelude
bedroom's chest beside the black border, an NPC partly behind a bedpost, close/far views and Tab release;
`hiliteocc` checks wall occlusion. `hilitedark` runs identical NPC/chest views with normal lighting and with
zero ambient/diffuse light, including unhighlighted and released shots. `--openwinter-args "--visual-hilite-probe 1"`
also saves `.HiliteMask.png` and `.HiliteDepth.png` beside each shot for diagnosing coverage and depth-layer
collisions (these are intermediate targets, stale while highlights are off, not visible output).

Limitations:
- `reticles` posts left clicks (`VISUAL|click|fractionX,fractionY[|object tag]`) without moving the pointer or
  taking focus. The optional tag aims OpenWinter at the same object despite camera differences. Posted buttons
  did reach the original: green ground marker, no NPC-talk marker, red hostile corners, arrival and
  cancellation. The chest did not show a visible native green marker; OpenWinter shows one (a deliberate difference).
  Run with `--openwinter-args "--ui classic --visual-delay 0"`; `notes.txt` records OpenWinter's actual click pixels.
  Capture timing and movement speeds differ, so animation phase is approximate. Chest approach and opening are
  measured separately (`chestuse`).
- `camorbit` holds/releases the left and right camera keys. On OpenWinter, run with
  `--openwinter-args "--visual-delay 0 --visual-camera-trace 1"`, then `python tools/visual/cameraorbit.py`.
  This checks rendered yaw/pitch against the requested angles every frame, including after release.
  The original did not turn under the harness's posted arrow keys, so those captures
  do **not** establish its orbit timing; exact native speed and mouse sensitivity remain unmeasured.
- `+TestNewModule` always plays the first pregenerated character (Aluvian Darkstar, `data/lcv`), whatever the
  local vault holds, so the harness uses that character on both sides.
- The original's log is polled, so its screenshots land 0.1 to 0.4 s after the shot line; fast effects
  (Fireball, Magic Missile) are compared at roughly, not exactly, the same moment. Particle effects are random.
- The original's keyholing (the see-through circle around the player) and its post-processing have no
  equivalent here; mean luma is a coarse number, so look at the pictures.
- Each scene takes about 40 s on the original and two to three minutes on OpenWinter (start-up). An OpenWinter run
  occasionally dies without a message (a Godot crash); rerun the scene.
- `python tools/visual/stress.py <scene> <runs>` runs a scene on OpenWinter again and again and reports each run's
  exit code (a crash, a kill and a timeout look alike otherwise); the harness also asks .NET for a minidump in
  `out/visual/dumps` if the runtime fails.
- Experiments: `--openwinter-args "--visual-placeable-light range,attenuation,energy"` (and `--visual-static-light`)
  try other placeable light falloffs; `--fx-verbose` logs every visual effect the client plays.

Area fields: `"are": { "IsNight": 1, "ModSpotCheck": -10 }` at the suite's top level (the one area) or on an `"areas"`
entry changes those ARE fields, keeping each field's type.

Stealth suites (docs/STEALTH.md): `stealth_modes` (mode flags, speeds, what ends stealth and detect),
`stealth_rules` (who sees a hider, vision effects, stickiness, Hide in Plain Sight; Prelude factions),
`stealth_checks` (detection rates of 216 observer/hider pairs; statistical) and `stealth_secret` (the stock
`x0_sectrig_wdr` secret-door trigger). Visual scenes `stealthview` and `stealthself` show the presentation.
The `stealth_check_*` suites (NPCs only, `parity/include/pstealth_inc.nss`) measure the checks themselves: pairs whose
hider parks in a second area between trials (one observer pass per trial, about 1,000 trials a group, rates compared
with a tolerance and raw counts in the .raw.txt files), the pass and dice cadence (`cadence`), skill totals of 0
(`zero`), size, night and light, Listen distance and combat (`mods`), natural rolls, ranges, invisible and blind
close-range hearing (`invis`, `blind`) and interior walls. Run them one at a time; each takes about 10 minutes on
nwserver.

Multi-area suites: `"areas": [{ "resref", "tag", "size", "tile", "tileset", "instances", "areaEvents" }]` builds
one area per entry (the first is the entry area; missing fields come from the suite's own `size`/`tile`/`tileset`/
`areaEvents`). Instances can be triggers: `{ "type": "utt", "from": "<utt template>", "x", "y", "w", "h", "set" }`
places a w x h rectangle centred on (x, y), or `"points": [[dx, dy], ...]` relative to (x, y) (a trigger whose
position is a corner: `approach_use`). Without `"areas"` a suite gets the one `parity` area as before.

Movement and use: `door_leafside` finds which way a door's leaf swung by jumping probe creatures
onto either side's open leaf (the one on the leaf is moved off it); `move_startdelay` samples walkers every 0.05 s from
the order; `approach_use` logs where walkers stop before using doors, chests, creatures, items, waypoints and
triggers. Script functions: `faction_bestac` (GetFactionBestAC/WorstAC; tile 121's walls for line of
sight), `equip_mostdamaging` (GetFirstItemInInventory lists the newest item first) and `aoe_subarea`
(GetIsInSubArea; an area of effect the module script laid was gone within seconds, so a creature lays them).

Encounters and journals (docs/ENCOUNTERS.md, docs/JOURNAL.md; `src/OpenWinter.Tools/ModuleFixtures.cs`): a `"ute"`
template (`{ "from": "nw_goblingrp", "type": "ute", "set": {...}, "creatures": [{ "resref", "cr", "single" }] }`)
placed as `{ "type": "ute", "from", "x", "y", "w", "h", "ox", "oy", "set", "creatures", "spawnPoints": [[x, y,
facing], ...] }` is an encounter (the rectangle centred on (x, y) or offset by ox/oy; spawn points in area
coordinates). Visual scenes take the same as `"encounters": [{ "from", "tag", ... }]`. `"journal": [{ "tag", "name",
"xp", "priority", "entries": [{ "id", "text", "end" }] }]` writes a module.jrl into a suite or scene, and a scene
conversation node takes `"quest"` / `"questEntry"`. Creature fixtures (`.utc.json`) take `"tag"` and
`"scripts": { "ScriptSpawn": "..." }`. Suites `encounter_events` and `journal_funcs`; nwmain scenes
`encounterspawn`, `encounterrules`, `encounterbudget` / `encounterparty` (`parity/gen_encounterbudget.py`),
`encountermix` and `journalpc`. The original spawns nothing without a player in the area, so wave sizes are measured
on nwmain.
`interarea_move` and `interarea_pursuit` (docs/INTER_AREA_MOVEMENT.md) use it; `python parity/gen_interarea.py`
writes their suite.json files (chains of 30 x 30 m areas joined by door pairs and transition triggers; also
`route_ties`, equal routes chosen by the exits' X) and
`parity/include/pinterarea_inc.nss` logs each tracked creature's area entries, arrival and end state. Their timing
tolerance grows by 0.4 s per area crossed (door-leaf side and turning, see the doc). `door_bashed` (killed doors,
same doc) is generated there too; `parity/include/pbashed_inc.nss` logs each door event once with its order and time
and reads a door's whole state. The visual scene `doorbash` shows the original's killed doors.

Visual scenes can have more than one area: `"extraAreas": [{ "source": { "module", "area" }, "onEnter": "script" }]`
copies further official areas into the scene module with their doors (LinkedTo kept, scripts stripped), so a scene
can take the player across. `transitfollow` (the Prelude's Graduation Chamber and Training Halls) walks the player
into the doorway with posted W keys, closed and open, with an attacker on it, then with a henchman, familiar, summon
and dominated creature (two of them sent far off), then jumps and moves it across by script; everything is read from
notes.txt (docs/INTER_AREA_MOVEMENT.md, "Players"). It needs no captures beyond its two shots.

Personal space suites (`coll_place`, `coll_walls`, `coll_move`, `coll_doors`, see `PersonalSpace`): creatures created on
a spot that is taken move at once, so suites that need creatures side by side should space them by the sum of their
CREPERSPACE. Walkers only bump creatures they perceive, and perception first updates 4 s after a creature appears:
create creatures at least that long before they move. In the original some crowd outcomes vary from run to run (who
of two walkers meeting head on is stopped, how far back a queue stops); compare what is stable.

- The original's post-processing has no equivalent here; mean luma is a coarse number, so look at the pictures.
- The original moves the player off an unwalkable `VMovePC` point; OpenWinter does not, so pick points it accepts
  (check both `shots.txt`).
- Each scene takes about 40 s on the original and two to three minutes on OpenWinter (start-up).


`nameplates` measures creature labels, health boundaries (100 HP actors), healing, temporary HP,
death, viewer reputation, party membership, camera rotation and a rat appearance. `nameplate_modes` checks
creature/chest/door hover and Tab visibility; its posted native hover is not reliable, so the native
hover rule comes from observing the original by hand. OpenWinter applies `VISUAL|hover|x,y` to its local
picking coordinates without moving the OS cursor. Run with `--text-bubble-mode 0|1|2 --tag _off|_text|_full`
(the tag is an arbitrary suffix) to capture modes separately in both engines. `.utc.json` files name an installed `template` and exact
`hitPoints`; the builder copies the blueprint only into the generated module, removes scripts/feats and sets
Constitution to 10. No game asset is checked in. Scene option `textBubbleMode` (default 2, overridden by the
CLI flag) applies to both engines. Scenes can place doors (which NWScript cannot create) through
`doors: [{"template": "nw_door_normal", "tag": "VIS_DOOR", "position": [22.3,17,0], "facing": 0}]`;
blueprints are read from installed data into the generated area. `largeFont` (default true) and `uiScale` (default 1) configure the native
sandbox; use `--openwinter-args "--ui-scale 1.5"` for a scaled client run. Use `gui: true` to include the client HUD. Blank native frames are not parity evidence.
`chatLog: true` turns on the original's whole-chat-window logging and the client's `--visual-chat`, so both
write `chat.txt` beside the shots (the original's `[CHAT WINDOW TEXT]` lines). `chatSplit: [primary, secondary]`
writes the original's saved chat heights into the sandbox. They don't split its window (it starts as one window whose
height depends on them); `chatmenuheld` splits it through its chat menu with a held right button.
`triggers: [{"tag", "position": [x,y,z], "points": [[dx,dy], ...], "linkedTo", "linkedToFlags"}]` places triggers in
the scene's own area (an area transition when `linkedTo` names a door, flags 1, or a waypoint, flags 2); scenes
`doormove` and `doormove2` use one. `serverSettings: {"player-party-control": true}` writes a `[server]` section into
the original's sandbox settings.tml (scene `partycontrol`). `castlines3` and `castlines4` record the item, ability,
summon and AC chat lines (docs/PARTY.md).

`python tools/visual/combat_audio.py <scene>` records which `weaponsounds.2da` sounds the original plays: it
swaps each for a pure tone in the sandbox's override folder for the run, records the process and lists the tones
it hears per scene phase (`docs/COMBAT_PRESENTATION.md`). `--sounds <prefix>` tones every installed sound
with that resref prefix instead (`combat_audio.py levelup --sounds gui_`); phases are `VISUAL|note|<scene>|phase|<name>`.
`partyframes` adds three henchmen (one at half health) and a summoned creature and shoots the classic party bar with
its member list; run it with `--native-framebuffer` and, for OpenWinter, `--openwinter-args "--ui classic"`.
`--ui-scale <s>` sets both engines' interface scale for a run (overrides the scene's `uiScale`).
`levelup` gives experience past a level (chat log, timed shots) and `memorizeslots` opens the Memorize Spells panel
with empty slots; run both with `--native-framebuffer` and, for OpenWinter, `--openwinter-args "--ui classic"`.

Two capture limits:
- With the display asleep or locked, PrintWindow captures of the original come back black. Run with
  `--native-framebuffer`, which uses the game's own screenshot.
- The original ignores the position of a posted click unless a mouse button is already held. It reads the real
  pointer's resting place instead (SDL resets the pointer when the window isn't under it), so `click` and
  `rightclick` land wherever the pointer sits. Drags (`mouse ... down`, `hover`, `mouse ... up`) and keys do
  work. Clicks that must hit something need `--real-mouse` (it moves the real pointer; secondary display only).

### NPC bump eligibility and animation

`parity-run coll_bump_flags` varies relationship, blocker state and the default OnBlocked
script with otherwise identical lanes, including one-way friendship. All 210 readings
match after the relationship check: the blocker must consider the mover friendly. `python tools/visual/visual.py run pavelbump --only nwn` tests the placed Pavel with
posted W keys and records positions/relationships in `notes.txt`; it needs no captures.

Both fixtures use `"factions": "Prelude"` to copy the installed campaign's `repute.fac`
into their generated module. Without a faction table, native collapses NPC faction
relationships. Do not use an omitted table to infer friendship or hostility behavior.
The optional `factions` setting also accepts other installed campaign module names.

`bumpanim` captures a single friendly push from four retained facings. `bumpclips` is a
native diagnostic that replaces each directional `cwalk` clip with `sitcross`, proving
which animation the native client selects. Run with the usual `visual.py run <scene>`;
`bumpclips --only nwn` is the clip identification experiment. The harness understands
`VISUAL|burst|name|seconds|fps` for short sequences (up to 5 seconds, 30 requested fps),
writing actual sample times to `frames.txt`. Slow capture can stretch the requested
sequence; compare poses and timestamps, not equal frame numbers or log times.

### Character driving and footstep fixtures

`driving` holds W/S/A/D/Q/E and combinations and records position/facing. `driveclips` and
`drivediagonals` replace candidate clips with sitcross in native to identify animation selection.
`turnsteps` captures close footwork bursts; `turntap` measures isolated 30-degree taps and a held turn.
Run `python tools/visual/visual.py run turntap` (or another scene); for client HUD captures pass
`--openwinter-args "--ui classic --visual-delay 0"`. `--visual-camera-trace 1` verifies unchanged top-down yaw.
Multiple `VISUAL|key|vk|down/up` commands can remain held together.

`drivetaps` posts one brief W/S/Q/E/A/D press and release, records position/facing every 0.1 s,
and captures the follow-through. `driveholds` measures movement after longer holds without capture
stalls. `moveturn` uses script-timed 90/180-degree facing changes, a sit-to-run interruption and a
running reversal; it needs no real pointer or repeated keys. For example:
`python tools/visual/visual.py run drivetaps moveturn --openwinter-args "--visual-delay 0 --ui classic"`.
Compare burst poses using each engine's `frames.txt` timestamps, not equal frame numbers: PNG capture
can stretch OpenWinter's sequence substantially. `--visual-notes-only 1` skips ordinary shots, not bursts;
use it with `driveholds`, `driving` and `turntap` for movement readings without screenshot stalls.

`mousecal` measures near/far held driving, `mouseclips` separates walk/run clips from short/long arrows,
`mouseground` logs exact ground coordinates using targeting-mode clicks. Mouse commands are
`VISUAL|mouse|fractionX,fractionY|down/up[|left/right or nativeGroundOffsetX,nativeGroundOffsetY]` (both engines
read `right` as the right button; scene `rightdrag` drags with it held). The optional offset
projects the initial client pointer at the same ground distance despite camera framing differences;
native still uses the original pixel coordinates. OpenWinter uses a virtual pointer and renders its cursor
in captures. Add `--visual-notes-only 1` to collect timing without screenshot stalls.

Posted native mouse holds drift because the original also reads OS pointer state. They are not reliable
calibration. When moving the real pointer is acceptable, use
`python tools/visual/visual.py run mousecal --only nwn --real-mouse --tag _repeat`. This opt-in requires
DISPLAY2, uses sandboxed userdata, and restores pointer and foreground in `finally`. Keep hands off the
mouse during the 27-second sequence. `mouseclips` also needs the real pointer; `mouseground`, keyboard
fixtures and client virtual-pointer captures do not.

`footsteps --only openwinter --openwinter-args "--ui classic --visual-delay 0 --visual-footsteps 1"` records
sample names and an Effects3D-bus WAV while changing hard/soft/none/hoof footstep types. No microphone or
desktop audio is captured. `dotnet run --project src/OpenWinter.Tools -c Release -- animation-events a_ba`
prints the installed clip contacts. Regression suite: `parity-run movement_speed` (86 matching readings).


`movefinish` captures destination walking/running through arrival at 5 fps and logs position/action
at 0.1 s intervals. It drives the same server MoveAction used by ground clicks without relying on
native posted mouse coordinates. Run `python tools/visual/visual.py run movefinish --openwinter-args
"--visual-delay 0"` and inspect the `runstop`/`walkstop` sequences and each engine's `frames.txt`.


`rapidclick` issues six ground clicks 0.3 s apart at short/medium/long distances. Run
`python tools/visual/visual.py run rapidclick --openwinter-args "--visual-delay 0 --visual-movement-trace 1"`.
Clicks use `VISUAL|click|fractionX,fractionY[|objectTag[|groundOffsetX,groundOffsetY]]`; an optional offset
projects the client target relative to the current PC, while native uses screen coordinates. Posted
native clicks work without the real-mouse option. `movement.csv` records client speed, animation input,
transition count and held-mouse state each rendered frame. The Stand input is the base speed blend,
not necessarily a stationary pose: check speed too. Expect one Run entry per medium/long trial until
arrival braking, with no new transition when the destination changes. Scripted resets between trials
can introduce turn transitions. PNG capture/dispatch timing is not frame-synchronous across engines.

`selfhilite` captures the visible PC with no highlight, on hover, after hover leaves, with Tab held,
and after release. Run `python tools/visual/visual.py run selfhilite --openwinter-args "--visual-delay 0 --ui classic"`.
Native posted hover remains unreliable; use the Tab pair to establish the white self-highlight color.


`tapfinish` compares all six keyboard tap endings at 0.30/0.40/0.50/0.60/0.70/0.85 s. It uses one
screenshot per fresh tap to avoid burst readback changing the preceding animation. Run
`python tools/visual/visual.py run tapfinish --openwinter-args "--visual-delay 0 --visual-movement-trace 1"`.
The 0.30/0.40 s shots can stall later frames; use separate later trials to judge settling. Resetting
facing between turn trials also creates a brief opposite turn in the client trace before the actual tap.
`animation-events a_ba pause1` prints a specific clip's length/transition even without sound events.

`rapidturn` compares one-second A/D holds with ten taps 0.1 s apart and logs facing each 0.1 s.
Run `python tools/visual/visual.py run rapidturn --openwinter-args "--visual-delay 0"`.
Compare angular increments while turning, allowing for asynchronous posted-key delivery and the final
short tap's follow-through. Input edges must never add rotation outside the server tick's held-rate cap.

`selflight` compares the visible PC front/back with Tab held in lit and zero-ambient/diffuse rooms,
plus plain/released frames. Run `python tools/visual/visual.py run selflight --openwinter-args
"--visual-delay 0 --ui classic"`. Inspect the textured surface as well as the halo: native white
self-highlighting reveals the character evenly in both rooms. `selfhilite` also checks hover/release.

`classictext` is the focused text-rendering fixture: Tab name/status labels, script chat, conversation
body/action/replies and a reply transition. It sets largeFont=false and textBubbleMode=1 in both clients.
Run with `--openwinter-args "--ui classic --visual-delay 0"`. Glyph rasterization still differs from the
original.

`defcast` measures defensive casting with a real player character and memorized
spells. It tests low/high Concentration, mode off/on, nearby/distant/absent enemies,
and a sandbox BIC copy without Combat Casting. Run:
`python tools/visual/visual.py run defcast --openwinter-args "--ui classic --visual-delay 0"`.
For each variant, run `python tools/visual/check_defcast.py out/visual/shots/vis_defcast_<variant>`.
Inspect defensive_failure/success and mode_feedback PNGs for DC and localized text.
Attack counts and the legacy GetDefensiveCastingMode query are diagnostic, with
known differences.

`modes` tests every ACTION_MODE on/off, with and without Keen Sense, equipped
Rapid Shot, rapid replacement, and posted Detect keys. Run with the same client
arguments and check each variant using `tools/visual/check_modes.py <capture-folder>`.
Inspect the PNGs for overhead indicators and lifetime. Scene/variant `removeFeats`
creates a modified copy of the installed character BIC beside the generated module;
the harness copies it only to out/visual/user/localvault for both engines. It cannot
be combined with --character. No game assets are stored in the repository.

`modeswitch` is the short cross-mode fixture: Defensive Casting, Power Attack and
Expertise cancel one another; a rapid burst measures stacked feedback. Run with
the same client arguments and check it with `tools/visual/check_modes.py <folder>`.

### Spell voice playback

`spellvoice` casts Bless Weapon twice and cancels a third cast during conjuring; every chant should
finish. `spellvoice_interrupt` compares an actual memorized Magic Missile's success, cancellation,
damage-induced concentration failure and EffectSpellFailure(100), then plays reference samples.
Run `python tools/visual/visual.py run spellvoice spellvoice_interrupt --openwinter-args
"--fx-verbose --visual-footsteps 1 --visual-delay 0"`. Client godot.log records stream length, Finished
and cleanup timing under [spell-audio]; footsteps.wav is the Effects3D bus recording. Expect three
finished chants in spellvoice, four in spellvoice_interrupt, plus one finished sff_spellfail for damage.

For native amplitude measurements, optionally install pycaw/comtypes under ignored out/audio-probe:
`python -m pip install --target out/audio-probe pycaw comtypes`. Run
`python tools/visual/spellvoice_audio.py spellvoice_interrupt isolated`, then repeat with
`isolated_nofail`. Only the harness-owned process is metered. Synthetic silent overrides temporarily
isolate chants from conjure/cast SFX; isolated_nofail also silences sff_spellfail to prove the chant
continues underneath it. Overrides are confined to sandbox userdata and removed afterward. JSON
readings go to out/spellvoice_interrupt_<mode>.json.

`combat_placeables` attacks four installed furniture blueprints from four sides.
It compares attack occurrence, continued attack action and stopping distance;
`BASH_POSITION` lines retain exact coordinate diagnostics. Side/route selection is
not yet matched. This guards the mismatch between a PWK interaction point and the
origin used by combat range checks.

`armoirehilite` captures the Prelude armoire before, during and after Tab, including a front view.
Run with default keyhole settings to check that highlighting respects the cutaway, or with
`--keyhole min-z-offset=6` to inspect the full texture atlas. Its scripted open step currently also
exposes that the client ignores placeable PlayAnimation messages; open-state parity is not claimed.

`touch_states` checks melee/ranged scripted touch attacks under low Dexterity, seven helpless
conditions, blindness, invisibility, prone targets, movement and confirmed flanking. It uses
3,600 samples per attack kind/case and staggered scripts to stay below the instruction ceiling.

### Object animation and resurrection lifecycle

`python tools/visual/visual.py run objectstates --keyhole min-z-offset=6` compares scripted
armoire opening and closing. `deathclips` uses public ReplaceObjectAnimation markers plus normal
humanoid/dog controls to identify fall and corpse clips. `resurrectclips` similarly identifies the
two get-up phases; use `--tag _raiseable` for the corrected reference set. Inspect the 6 fps burst
pairs as well as settled shots. Marker replacement lengths differ between engines, so compare
unmodified controls for recovery timing.

`dotnet run --project src/OpenWinter.Tools -c Release -- parity-run resurrection_flags` checks the
SetIsDestroyable raiseable flag, blocked/allowed resurrection and retry after enabling the flag
(6 readings, all must match).

### Difficulty context

The runner explicitly sets the same difficulty on native nwserver and OpenWinter. suite.json accepts
`"difficulty": 1` (Easy), 2 (Normal), 3 (Hardcore, the default), or 4 (Difficult); 0 leaves each engine at its own
default (nwserver without `-difficulty` reads 1 from its settings, as OpenWinter now does). `difficulty_default`,
`difficulty_easy`, `difficulty_normal`, `difficulty_hardcore`, and `difficulty_difficult` verify GetGameDifficulty on
both engines; `difficulty_npc1` and `difficulty_npc4` show that difficulty changes nothing between creatures that
aren't players (docs/DIFFICULTY.md).

Both parity engines resolve user resources from out/parity/user/<suite>, including intentional
sandbox overrides. OpenWinter never reads the real user folder's development/override folders for these runs.
After a Release tools build, `python tools/parity/check_isolation.py` verifies this with a synthetic
foreign development script, entirely under out. It fails against the previous unsandboxed lookup.

### Movement percentage boundary

`speed_payload` checks raw and applied EffectMovementSpeedIncrease integers around the absolute
percentage boundary. `speed_absolute` measures three movement trials per value, reports sample
counts and compares median speed ratios to the same engine's baseline (tolerance 0.025).
Values below 100 add to 100%; values 100 and above are already percentages. The fixtures cover
negative inputs and the normal speed floor/cap. Existing `movement_speed` and `fx_misc` cover
ordinary effect stacking and haste/slow interactions.

### Broad self-buff application audit

After building OpenWinter.Tools in Release, `python parity/gen_buffscope.py` generates 93 curated
legal self-target cases in buffscope01..08 using stock spell metadata and an empty diagnostic user
folder. Run all eight with parity-run, then `python parity/check_buffscope.py`. The checker requires
complete fresh matching results, positive application for actual buffs, and valid rolled bonus
ranges/stat changes. It writes out/spell-audit/statistics-ledger.json with spell IDs and input hashes.

These are cheat/instant application cases for one caster each. Six original rows have visual/text
behavior but no lasting stats. Random distributions, other contexts, effect consumption/expiry and
presentation are explicitly unverified; see the ledger's notVerified list.


### Authored creature fixture profiles

A visual scene or headless parity suite may supply `<resref>.utc.json` to copy an installed creature into
its disposable module with explicit fixture deltas. Resource lookup uses an empty
sandbox user directory. Only authored JSON is committed:

```json
{
  "template": "nw_bandit001",
  "hitPoints": 100,
  "classLevels": [{"class": 3, "level": 20}, {"class": 34, "level": 10}],
  "feats": [305, 257, 45],
  "equipment": [{"resref": "nw_wswls001", "slot": 4}]
}
```

`classLevels`, `feats` and `equipment` replace those complete template lists when
present; omitted lists stay as installed. Classes do not automatically grant
feats, spellbooks, companion selections or skill ranks. Equipment slots are
NWScript `INVENTORY_SLOT_*` indices (4 is right hand), not GFF bitmasks. Missing
creature/item resources, duplicate classes/slots and out-of-range values stop the
build. All creature event scripts are removed. `hitPoints` retains the existing
label-fixture behavior: set exact HP, Constitution 10 and clear template feats;
explicit `feats` is applied afterward. Omit `hitPoints` to preserve base HP/feats.

The `utcprofile` scene logs native class levels, feat presence and the equipped
weapon resource/base item. These runtime checks matter: a successful JSON/GFF
build alone does not establish a legal spell trigger or complete class behavior.


### Music, ambient sound and sound objects

`tools/visual/audio_scene.py run <scene> --engine nwn|openwinter --music 1 --effects 1 --audible` records a scene's
sound (both engines play through the speakers; it refuses without `--audible`). A scene's
`tones.json` replaces sounds with pure tones in the sandbox override for that run only. `tones`, `levels`, `phases`,
`music --refs` and `envelope` analyse a recording silently. Scenes `musicbattle`, `musicbattle2`, `audiofollow`,
`soundobj`, `footfall`, `musiclevel`; nwserver suite `audio_scripts` (needs `"areaProperties"` and `"uts"`
instances, both supported by the builder now; visual scenes accept `"areaProperties"` and sound objects with any UTS
field in `"set"`). Findings: docs/AUDIO.md.

### Client audio evidence

`--visual-audio` records the client's own Master bus while a visual scene runs:

```powershell
python tools/visual/visual.py run audiosmoke --only openwinter --openwinter-args=--visual-audio
```

The capture folder receives `audio.wav` (16-bit stereo at the mixer rate) and
`audio.json` (PCM peak, sample count, settings, UTC start time and timestamped
VISUAL markers). Its metadata follows the native process-recorder shape so timed
spell waveform analysis can use either engine's recording. Capture occurs before
final Master bus gain and the output device; it does not record other apps or
change Windows audio sessions. Marker timestamps use the UTC wall clock and do
not compensate mixer startup latency, so identity evidence is stronger than
sample-accurate timing or absolute-volume evidence.

The existing `--visual-footsteps` recorder remains separate and both flags can be
used together. A nonempty WAV is not proof of correct cues: compare installed
reference samples within each marked spell interval and retain unresolved cases.

### Cast-time metamagic eligibility

`metamagic_masks` prepares eight real Wizard/Cleric spell slots through SetMemorizedSpell,
then attempts normal, Maximize or Extend casts of Magic Missile, Mage Armor and Inflict Minor
Wounds. It compares initial readiness and consumption, including combinations the spellbook UI
normally prevents. Run `dotnet run --project src/OpenWinter.Tools -c Release -- parity-run metamagic_masks`.
The fixture does not cover known-spell or spontaneous conversion paths, or cheat/ability casts.

### Offensive spell peak-damage contexts

`python parity/gen_spell_damage.py` creates 25 stock offensive-spell suites. Run each
`spell_damage_NNN` with parity-run, then `python parity/check_spell_damage.py` to validate all
results and write `out/spell-audit/damage-ledger.json`. Real slots, no cheat, instant casting;
Maximize except unmodified Inflict Minor Wounds. Every target must be seen and every slot consumed.
Each case compares the greatest HP loss observed five seconds after the request across eleven
independent pairs (21 for Ball Lightning's fifteen separate saves). Individual native amounts
remain in the sandbox nwserver log. This establishes the sampled peak in those contexts, not
random distributions, all levels/targets, secondary targets, later damage, or audiovisual parity.

### Persistent spell area destruction

`aoe_destruction` compares script removal, timed expiry and script removal of an attached AoE:
creation, invalidation, exit-event count and remaining attached effect. `aoecleanup` is the visual
fixture: a sixty-second Acid Fog is explicitly destroyed at five seconds; captures at3/6/10/15s
show whether the client stops its lasting visual. Its note markers report existence before/after.
Use `python tools/visual/visual.py run aoecleanup --only openwinter` for the client, and the standard
sandboxed native harness for paired rendering when the displays are awake.

### Self-healing and regeneration

`spell_healing` measures ten real level-20 Cleric self-casts from1HP: five Cure spells,
Heal, Mass Heal, Healing Circle, Regenerate and Monstrous Regeneration. It checks slots,
HP and effect presence at2/8/14 seconds. Maximize is used for the four larger Cure spells
and Healing Circle. Run parity-run, then `python parity/check_spell_healing.py` for a
120-reading guarded report. Other levels, party/undead targeting, random cure amounts,
later regeneration/expiry and audiovisual parity remain separate measurements.

### Starting equipment

`parity-run creature_equipment` uses twelve script-free authored UTC profiles and
two area-placed controls. It reads equipment and inventory immediately after
creation, at 0.1 seconds and at 2 seconds. Its current result is 266 matching
readings and six known differences: a malformed right-longsword/left-greatsword
pair hides the displaced longsword from native inventory enumeration, while
OpenWinter retains it visibly in the backpack. Both retain possession and the
greatsword in the right hand.


### Casting sound lifetime

`python tools/visual/conjure_sound.py` records a successful Aid cast and a cancelled
Aid cast with an authored eight-second casting tone in the sandbox. After a client
build, add `client` for the corresponding Master-bus capture. Analyze either WAV
with `python tools/visual/check_conjure_sound.py <path>`: native completes both
source cues even though the second spell never impacts.

### Client build before visual capture

The visual harness builds the managed Godot client before launching each OpenWinter capture. Building a visual fixture only builds OpenWinter.Tools and does not update the client DLL. A compilation failure stops capture before launching Godot, so an older successful binary cannot silently stand in for the current source.

`reaction_context`, `reputation_decay` and `party_spells` measure native dispositions,
reaction predicates, fractional decay and five actual prepared party spell casts.
`reactionpc` uses the visual harness for actual-PC script contexts; black native frames
are not visual evidence. Battletide, AdjustReputation and PvP cases are not covered.

`aoe_payload` measures applied spell-context metadata. `battletide_saves` compares forty
prepared casts with low/high saving-throw controls; run `python parity/check_battletide.py`
afterward to reject incomplete or jointly incorrect rates.

`aoe_expiry` measures requested versus actual short attached-AoE lifetime across 32
contexts. Run `python parity/check_aoe_expiry.py` as an independent correctness check.
The carried expiry in fx_aoe now compares elapsed clock time rather than a separately
scheduled integer phase counter.

`vfx_lifetime` covers all666 typed stock visualeffects rows plus two untyped controls
across three duration types. `vfx_lifetime_links` measures linked buffs, delayed
expiry and removal. Both use actual effect enumeration.

### Prepared spells and selected associates in creature fixtures

Each `classLevels` entry accepts `knownSpells` (spell-level keys `0` through `9`,
arrays of spell IDs) and `memorizedSpells` (the same keys, arrays of objects with
`spell`, optional `ready` defaulting true, and optional `metamagic` defaulting 0).
For example: `{"class":10,"level":20,"knownSpells":{"1":[174]},
"memorizedSpells":{"1":[{"spell":174},{"spell":174,"ready":false}]}}`.
Top-level `abilities` accepts the six lowercase full ability names. `familiar`
and `companion` accept a numeric `type` and optional `name`. These are explicit
fixture fields, not automatic class progression.

`summon_casts` uses real prepared Summon Creature I-IX casts, Wizard familiar and
Druid companion feats. Its initial native audit confirms spellbook readiness,
slot/feat consumption, ownership and summoned statistics against the selected
installed blueprint. Random elemental ranks are checked against their legal
choices. The initial diagnostic intentionally exposes missing caster summon
effects and script-visible resource-name casing; these are not fixture failures.

### Summon statistics and lifetime audits

`summon_casts`, `summon_duration`, `summon_lifecycle`, `summon_modes` and
`associate_profiles` are headless native/OpenWinter comparisons. Final results are
242, 24, 113, 72 and 1155 matching readings respectively, with zero differences or
missing readings. The first two exercise real prepared spells; associate profiles
exercise actual familiar/companion feats in 33 explicit class/type contexts. The
lifecycle/modes suites isolate short-duration effect removal, replacement,
detachment, dispel and owner death/destruction. These are mechanics evidence, not visual or
player-party UI evidence.

`summon_nocreator` adds 15 module/placeable/creature creator and target controls
across all three duration types: 180 matching readings. Location summons require
a creature creator; object summons require a creature target to create a creature,
although non-creature targets retain temporary/permanent effect bookkeeping.

`beam_lifetime` measures script-visible beam storage across duration, source death,
source destruction and target death. `fx_death_visual` distinguishes standalone
beams from linked buffs and duration visual/icon controls.

`associate_death` casts real familiar/companion feats then measures death-script
ownership, capped master damage, corpse faction, remaining uses and destruction
controls: 238 matching readings. Its authored wrapper calls the installed stock
death script and observes state before/after it and at 0.1 seconds.

Class fixture entries may supply `domains:[1,0]` (two distinct numeric domain IDs).
`summon_animal` validates the selected domains/feats and casts all nine real
prepared Summon Creature ranks as an Animal-domain Cleric 20: 225 matching readings.
It checks upgraded creature choices, ownership, blueprint statistics, spell slots
and duration, retaining legal random elemental alternatives.

`summon_existing` checks 14 named-resource/adoption contexts, including protected
unrelated/owned creatures, immediate in-place adoption, replacement, removal,
expiry, self-adoption and instant/permanent controls. It isolates the
EffectSummonCreature `oSummonToAdd` contract without using a native window:
466 matching readings, zero differences/missing.

`spell_control_004`, `008`, `082`, `083`, and `166` perform real prepared Wizard 20
casts of Bestow Curse, Blindness/Deafness, Hold Monster, Hold Person and Slow.
Each checks 20 low-save and 20 high-save targets, slot use, effect payloads and
stat deltas. Run `python parity/check_spell_control.py` afterward for independent
save-rate and result-validity checks.

`spell_race_082` and `spell_race_083` isolate Hold Monster/Person racial eligibility
with 25 Race-only blueprint deltas and five prepared casts each. The same checker
requires complete runs and all slots consumed. These retain human blueprint
feats; they do not assert that natural undead/construct creatures lack immunities.

`fx_curse_immunity` checks when curse ability penalties are suppressed, delayed
Constitution, immunity expiry, link ordering and copied-effect reapplication.
`spell_immune_004/008/082/083/166` cast five harmful control spells against eight
immunity contexts. Run `python parity/check_spell_immunity.py` after those suites.

### Object serialization and the original's save games

`persist_objects` serializes every object type (creature, item, placeable, waypoint, store, door, trigger, sound
object, encounter, area) with ObjectToJson, StoreCampaignObject and SqlBindObject, with and without the object
state, and reads each back (264 readings: which fields are present, VarTable entry types, what JsonToObject,
RetrieveCampaignObject and SqlGetObject restore, the campaign row's compressed header). `persist_effects` applies 80
effect constructors one per creature and compares their EffectList entries (effect number, subtype, integers,
floats, strings), action queue entries, transitions and the combined area format (180 readings). Full key lists and
dumps are RAW notes. The visual scene `nativesave` (nwmain, `--only nwn --silent --native-framebuffer`) makes a
single-player autosave with known state for OpenWinter's loader of the original's saves; copy
`out/visual/user/saves/000001 - Auto Save` to `out/visual/shots/vis_nativesave/nwn/saves` straight after the run
(the shared user folder is reused). See docs/SAVEGAME.md.

### Native save/reload parity (Windows)

A suite can set `"saveReload": true` and log `PARITY|__save__|1` once. The
runner saves at that marker, restores a fresh world/server, then compares only
the restored execution through `PDone`. The fixture must guard setup with a
persistent module local: native dedicated-server Load fires OnModuleLoad again
while also preserving the saved DelayCommands. This is explicitly distinct from
single-player load/event and PC HUD behavior.

`tools/parity/save_control.py` starts only owned sandbox nwserver processes,
minimized, moves them to devDisplay, and uses their observed Save Game name/slot
controls and exact saved dropdown entry/Load button. It never activates a window
or sends desktop input. This is a DISPLAY2 lease operation, unlike ordinary
headless suites. It writes fixed native slot `000007 - ParitySave` only beneath
the suite's `out/parity/user` root and rejects paths outside `out/parity`. A new
process loads the actual archive. Failure to save, restore, or finish fails the
run. OpenWinter uses SaveGameService and LoadInto; snapshot warnings fail validation.

`summon_save` is the first small baseline: a temporary dire-badger summon saved
four seconds into its twenty-second lifetime. Blueprint, HP, owner/type, hidden
master effect, remaining duration and removal at the original expiry match
(26 readings, zero differences/missing). Result pairs and `native-reload.json`
under `out/parity/results/summon_save` record the native archive path/hash,
module hash, revision, observed controls and before/after logs. Game archives are
ignored outputs, not repository assets. The follow-up below covers selected
familiar/companion contexts, pending arrival and replacement; PC UI restoration
remains unmeasured.

`feat_immunity` compares sleep/fear/disease/poison protection from class/racial
feats, a stock elf and IgnoreEffectImmunity. `feat_spell_054/155/165` verify real
prepared Fear/Scare/Sleep casts, slot use and spell-owned effect subsets. Run
`python parity/check_feat_immunity.py`.

`associate_save` extends native archive reloads to actual familiar/companion
feats, prepared Summon I and delayed/replaced/adopted/removed summons: 505 matching
readings after correction. `summon_pending` isolates the native loss of pending
arrival across all three duration types (53 matches); the identical uninterrupted
`summon_pending_control` lands normally (67 matches). Only summon arrival timers
opt out of snapshots; saved effects and their original expiry remain intact.
These are NPC mechanics controls, not PC HUD/possession or native single-player
callback evidence.

`prepared_slot_level` tests sixteen isolated script-prepared ordinary slots,
including wrong levels, metamagic offsets and a spell absent from the class.
All 80 readings match. `python parity/check_prepared_slots.py` independently
checks expected native acceptance.

`associate_health_save` adds injured/dead familiar, companion and summon archive
controls (235 matches); its uninterrupted control matches 234. `summon_death`
measures stock/blank death event ownership before/after the callback (38 matches).
`curse_mask_save` measures seven immunity/reload cases (56 matches), contrasted
with `curse_mask_control` (55). Reload reconstructs curse suppression against
current immunity; it does not preserve immunity which already expired. These
five suites match 618 readings after two narrow runtime corrections. They do not
establish PC HUD/possession, single-player load callbacks or corpse resurrection.

`spell_control_expiry` waits through normal/Extended Blindness/Deafness, Hold
Monster/Person and Slow and keeps a permanent Curse control:45 prepared casts,
79 matching readings, natural expiry and 31-stat restoration. Run
`python parity/check_control_expiry.py` (expiry is checked within 1.5-second
bounds).

`domain_slots` compares 40 Cleric 20 domain/class slot contexts and records public
stock spell-hook metadata (600 matching readings). Run
`python parity/check_domain_slots.py` after the suite: it checks actual casts,
consumption, base spell levels, metamagic and DC against the observed acceptance
set.

### Native framebuffer screenshots

When Windows window capture returns black, use the original game's own screenshot action:

```sh
python tools/visual/visual.py run nativeframebuffer --only nwn --native-framebuffer --tag _verified
python tools/visual/test_native_framebuffer.py
```

`--native-framebuffer` applies to ordinary `VShot` markers. It posts PrintScreen only to the
harness-owned HWND using the documented E0:37 scan code; it does not activate the window, move
the pointer, or change display power. The existing sandbox SCREENSHOTS alias receives each TGA.
The harness accepts one newly changed, fully decodable file and retains its exact bytes as
`<shot>.tga`, decoded `<shot>.png`, the Windows comparison `<shot>_window.png`, and
`native-framebuffer.jsonl` (source name, timestamps, elapsed time, size, SHA256 hashes, nonblack).
Missing or ambiguous new files fail. `VISUAL|native-shot|name` is also available to authored
scripts and writes `name_native` files without enabling the CLI flag.

Inspect context and timing before using images as evidence. A nonblack image alone does not prove
the expected action occurred. Black frames are retained and flagged because an authored cleanup
control may legitimately be black; unexplained black images remain invalid visual evidence.
Capture is asynchronous: the measured three-shot control took 0.106-0.178 seconds per request,
so do not treat these as exact marker-time frames or use closely spaced markers for timing claims.
Burst and keyshot markers retain their existing Windows capture path.

Further rules suites: `faction_nofac` and `faction_release`
(standard factions without and with a repute.fac), `hp_blueprints` (every base creature blueprint's hit points;
regenerate with `python parity/gen_hp_blueprints.py utc_base.txt`) and `hp_items`, `hasspell_abilities` (special
abilities; creature fixtures take `"specAbilities": [{ "spell": 90, "level": 6, "ready": true }]`), `death_keeps`
(what death strips), `cast_reach` and `cast_crowd` (where casters stop, crowding), `cond_charm_ai` (what charmed
casters do) and `stealth_sanctuary` (sanctuary and perception). Visual scene `rulesfollowpc` covers the player side
(factions, a charmed player, the Special Abilities ring, the sheet's damage lines).
