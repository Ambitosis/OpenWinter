# Creature voices and quickchat

Creatures speak from their sound set: an SSF file named by soundset.2da that maps the VOICE_CHAT slots (0-48) to
sound resources and talk-table captions. OpenWinter plays the same slots at the same moments as the original, from
the player's installed data.

## Sound sets

- `SoundSet.Read` parses SSF V1.0 offset tables. `VoiceService` resolves the creature's current `SoundSetFile`
  through soundset.2da, caches per resource manager, and sends the exact SSF resref. Haks, custom TLK captions, NPC
  voices and script `SetSoundset` changes use the same path. Empty SSF entries stay silent.
- `PlayVoiceChat` supports every VOICE_CHAT slot 0-48. Its text is normal localized speech.
- `CreatureVoice` (client) follows the speaking creature and plays on the Voice3D bus. A creature's new voice line
  replaces its ongoing one, as in the original (a scripted 8-second line is cut off by the next automatic grunt or
  pain cue); there is no separate priority queue.
- Missing references are reported, never filled with another character's voice.

## Quickchat (the V menu)

The V menu shares its commands and server handler across the modern and classic interfaces. The classic menu uses the
installed `pnl_quickchat` `gui_boxes` fill and border and `fnt_default` text, with localized captions 4710-4768 and
14-pixel rows. The slot for each path comes from the SSF format and the installed `nwscript.nss`; every keyboard path
was checked against the original.

- The groups are W (combat), E (exploration), D (tasks), S (social) and X (feelings); a second letter selects the
  line (Go Stealthy is V D S). Clicks work too.
- Opening reports GUIEVENT_QUICKCHAT_ACTIVATE (18) with integer 0; the accepted group and leaf letters each report
  SELECT (19) with the uppercase character. A successful choice reports no CLOSE.
- Escape closes the whole menu at either level, and V and invalid letters close it too; each reports CLOSE (20)
  once, with no selection. Menu keys do not move the player; a world click closes the menu.
- The five associate commands (Attack, Heal me, Guard me, Hold, Follow me) run each listening associate's
  OnConversation with the matching listen pattern (-3, -4, -7, -2, -5) and the player as master (see
  [ASSOCIATE_COMMANDS.md](ASSOCIATE_COMMANDS.md)).

## Automatic voices

| Event | Slots | Rule |
|---|---|---|
| Creature death | 18 | Once per death, no chat caption. A fatal melee hit plays the death line only, never a pain line |
| Melee attack attempt | 11-13 (attack grunts) | About 50% of attempts, hits and misses alike, equal choice of the three slots, no cooldown |
| Ranged attack attempt | 11-13 | About 40% of shots, after the ammunition is used; no ammunition, no grunt |
| Damaging melee hit on a living creature | 14-16 (pain) | About 65% of hits that do damage; scripted damage, misses and fully absorbed hits are silent |
| Damaging ranged hit | none | No pain line (0 of 83 measured longbow hits) |
| Player's load state changes | 32 (encumbered) | On every top-level inventory change that enters a different non-zero load state (overloaded to encumbered included); returning to normal or staying in the same state is silent |

- Automatic voices have no chat caption and use their own random numbers, never the combat dice. The rates are
  measured approximations of the original's behaviour, not its random sequence:
  - melee grunts: 99 of 196 attempts (50.5%, 95% interval 43.6-57.4%), cues as close as 0.3 s apart;
  - ranged grunts: 80 of 196 shots (40.8%, 95% interval 34.2-47.8%), slots chosen evenly;
  - pain: 100 of 152 damaging melee hits (65.8%, 95% interval 57.9-72.9%), slots chosen evenly, cues as close as
    0.32 s apart (no fixed cooldown).
- More grunts than hits in high- and low-hit controls show that grunts are not tied to a successful hit.
- The same rules apply to player characters and NPCs.
- Encumbrance: picking up a filled container that takes the player straight from normal to overloaded gives both the
  encumbered and the overloaded load messages. Polymorph cleanup of a form's stored equipment is silent.
- Near death (slot 17) is not an automatic engine warning: player HP changes are silent in the original. The stock
  henchman AI (`x0_inc_henai` `bkCombatAttemptHeal` → `VoiceNearDeath`) requests it by script, below ten percent HP,
  behind a one-in-five and then a 25% random gate.

## Spell chants

Spell chants are separate from sound sets: spells.2da `ConjSoundMale` / `ConjSoundFemale` name them. The base game has
20 distinct chants shared across spell rows and schools (not one per school). Silent Spell suppresses the chant, and
an interrupted cast cuts it short. Chants play on Effects3D.

## Testing

- `dotnet run --project src/OpenWinter.Tools -c Release -- voice-audit --base` audits every soundset row and both
  chant columns on every spell, reporting missing resources. On the base EE data: 5,100 soundset rows, 387 readable
  SSFs, 18,963 slots (10,886 empty), 82 missing SSF/sample references and no malformed SSFs; 840 spell rows hold 846
  chant references to 20 distinct clips, none missing. This establishes resource coverage, not audible parity.
- `python tools/visual/visual.py run voicechat --openwinter-args "--ui classic --visual-delay 0 --visual-voices 1 --fx-verbose"`
  compares the native root and combat menus and runs V W E, then scripted Hello, attack grunt, pain and death slots.
  `--visual-voices` records only the client's Voice3D bus. `voicecombat`, `voicegrunt`, `voicebow` and `voicepain`
  compare automatic voices; `tools/visual/check_voice_clips.py` identifies the original clips in a recording.
- `tools/visual/voice_slots.py <slots> <scene> <clip length> <name>` measures automatic voice triggers on the
  original: inside the harness sandbox it gives each slot a distinct synthetic tone (removed afterwards), records
  only the game's own process audio and logs which slots played. The `summarize_*.py` scripts next to it turn the
  recordings into counts and confidence intervals and reject runs whose gates (identity, perception, hostility,
  attack callbacks, explicit positive cues) fail. Scenes: `voiceactors_*` in `parity/visual/scenes/`.
- Creature playback lines (`[voice]`) appear in the client log only with `--fx-verbose`.

## Not matched yet

- Automatic battlecries (slots 1-3) when a creature enters combat: the original plays one on some combat entries
  only (3 of 5 measured player Attack orders, 2 of 20 stock NPC entries, never the Attack slot 0). The probability
  and any shared or per-creature suppression are unknown, so OpenWinter plays none.
- Other situational barks (ranged pain on thrown weapons and other launchers, special attacks, every target state).
- The exact random constants (the rates above are fits) and voice overlap between several speakers.
- Every sound set's audible waveform, exact attenuation, reverb and mix gain.
- NPC load warnings, load changes inside carried containers, and load changes from Strength or item properties.
- Arbitrary chat-window resizing and dragging voice entries onto the quickbar.
