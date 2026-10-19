# Music, ambient sound and sound objects

How an area's music, ambient sound, placed sound objects and footsteps play, what scripts see of them, and how
OpenWinter matches the original. Code: `src/OpenWinter.Game/Scripting/Functions/SoundFunctions.cs` (the script
functions), `src/OpenWinter.Game/WorldObjects/SoundService.cs` (notifies) and `BattleMusicSystem.cs` (each player's
battle music), `client/Audio/AreaAudio.cs` + `client/Views/WorldFx/AreaAudio.Script.cs` (music and ambience),
`client/Views/SoundView.cs` (sound objects), `client/Views/CreatureView.Footsteps.cs`, and the measured rules,
Godot-free: `client/Gameplay/Logic/MusicRules.cs` (`MusicDirector`, `AmbientDirector`) and `SoundObjectRules.cs`.
Tests: `tests/OpenWinter.Tests/AudioTests.cs`. Measured with the nwserver suite `audio_scripts`; nwmain scenes `musicbattle`,
`musicbattle2`, `audiofollow`, `soundobj`, `footfall` (recorded with `tools/visual/audio_scene.py`, see the end).

## Engine or script

All of it is the engine's. The stock scripts only call the music functions for set pieces: `nw_i0_plot`
(PlayCharacterTheme / RestoreMusicTracks), `x2_inc_cutscene` (saving and restoring tracks around cutscenes) and
`x2_act_ws_makeit` (MusicBattlePlay during weapon crafting); `openwinter-tools grep-nss "(MusicBattle|MusicBackground|
AmbientSound|SoundObject)[A-Za-z]*\(" --base` lists every call. **No combat script starts battle music**: the engine
does, per player (below). Area music, ambience, sound objects and footsteps need no script at all. OpenWinter runs
those stock scripts unchanged; what they call is checked by `audio_scripts`.

## What scripts see (nwserver suite `audio_scripts`)

- MusicBackgroundGetDayTrack / NightTrack / BattleTrack read the area's tracks (GIT AreaProperties), and the
  setters' values back, unchecked (-3 and 9999 are kept). Anything but an area reads **0**.
  The setters do nothing on a non-area.
- Placed sound objects are objects: valid, found by tag and by GetNearestObjectByTag, with a position, area, local
  variables and 1 hit point (current and max). But **GetObjectType returns OBJECT_TYPE_ALL (32767)**, GetName and
  GetResRef return "", **area iteration never returns them** (even for OBJECT_TYPE_ALL), shapes and
  GetNearestObject / GetNearestObjectToLocation find them **only under OBJECT_TYPE_ALL** (never a single type), and
  **AssignCommand on one runs nothing** (ExecuteScript does run, with the sound as OBJECT_SELF).
  SoundObjectSetPosition moves the object (GetPosition follows); DestroyObject removes it. OpenWinter matches all of this.

## Area music

Measured (scenes `musicbattle`, `musicbattle2`, `audiofollow`, music alone at full volume; identity, offset into
the track and level from correlation with the installed tracks):
- **Level**: music plays at 0.70 (-3.1 dB) of the file's level with the music slider at 1.0; battle music and the
  stinger the same. (soundgain.2da's "music 50" does not show.)
- **Entering an area**: the day (or night) track starts at once, from its beginning. MusicDelay does not apply.
- **A track ends**: silence for MusicDelay (the area's milliseconds, MusicBackgroundSetDelay), then the same track
  again from its beginning (19.3 s track, delay 5000: 5.0 s of silence).
- **MusicBackgroundChangeDay/Night** on the playing track, **MusicBackgroundStop**, and **battle music starting**:
  the playing track fades out linearly over about **0.85 s**; then the new track starts from its beginning.
  **MusicBackgroundPlay** starts the track at once, from its beginning.
- **Dusk/dawn**: the day track fades out over about **10 s**, then the night track starts from its beginning.
- **Battle music ends** (MusicBattleStop, or the fight is over): the battle track stops at once and the battle row's
  **Stinger1** (ambientmusic.2da, a WAV) plays; the background comes back from its beginning **MusicDelay + 0.85 s**
  later (3.8 s with 3000, 8.9 s with 8000), while the stinger's tail is still sounding.
- **Battle music starts by itself** when a creature **attacks the player while she is not plot**, even when the hit
  does no damage (damage reduction soaking everything). It does **not** start for a hostile in view, for the
  player's own attack on a hostile, or for attacks on a plot player. It starts with the attack (background fading
  0.85 s, then battle music: 1.5-1.9 s after the attack was ordered).
- **It ends** 7.8-10.4 s after her attacker dies (four fights; mean 9.1 s), whatever MusicDelay is (3000 and 8000
  gave the same). The exact rule (a time since her last attacker's last swing, checked periodically?) is not
  pinned down; OpenWinter ends it 9 s after no living creature has her as its attack target.

OpenWinter: the server's `BattleMusicSystem` decides each player's battle music and sends a `battle-music` notify
to that player; MusicBattlePlay/Stop keep the area-wide scripted flag (`area-audio`). The client plays battle music
when either is on. `MusicDirector` implements the timings above; `AreaAudio` turns them into players on the Music bus
(music level: see "Not matched"; scene `musiclevel` is the short OpenWinter level check).

## Ambient sound

- Volume is linear on the area's **0-127** scale (GIT AmbientSndDayVol / NitVol); 127 plays the file at full level
  (effects slider at 1.0). **AmbientSoundSetDay/NightVolume** set the same scale but **clamp to 0-100** (100 plays
  at 100/127, 127 the same as 100).
- **AmbientSoundStop** and **AmbientSoundChangeDay/Night** (and nightfall's switch to the night sound) fade the loop
  out linearly over **6 s**; only then does whatever is wanted at that moment start, at full volume
  (a change back within the 6 s plays the original sound again; AmbientSoundPlay after the fade starts at once).
- The original plays ambient files as stereo: a mono file runs at double speed. (Installed ambient files are stereo
  or ADPCM, so this only matters for test tones; `tones.json` has `"channels": 2` for them.)

## Sound objects (UTS)

Measured with tones standing in for the sounds (`soundobj`, `audiofollow`):
- **Volume** is linear (v/127), SoundObjectSetVolume takes effect on the sound already playing. A non-positional
  sound plays at 0.59 of the file at volume 127; a positional one at 0.30 at the listener (Godot's panning costs
  about 3 dB more, so OpenWinter's positional players are raised by that).
- **Falloff**: a positional sound fades **linearly from the listener to MaxDistance** and is silent beyond it;
  **MinDistance made no measurable difference** (curves with min 1/max 10 and min 3/max 15 both fit
  `(max - d) / max` within half a decibel).
- **The listener** is about a fifth of the way (0.19) from the player to the camera: zooming the camera out makes
  sounds quieter and shortens their reach (camera 20 m away: the 10 m sound stops at 6.5 m from the player;
  3 m away: at 9.5 m). Panning follows the camera.
- **Repeating** sounds (Continuous, not Looping): the first plays as soon as the object starts; then each waits
  **Interval plus or minus IntervalVrtn (uniform) after the previous one ends** (0.5 s sound, interval 2 s: 2.5 s
  apart; interval 1000 +- 800: gaps 0.19-1.77 s).
- **Neither looping nor continuous**: the list plays once in order with the interval between, then stops;
  SoundObjectPlay starts it again from the first sound. **Looping** goes straight on.
- **VolumeVrtn**: each play is Volume plus or minus VolumeVrtn, never above 127 (half the plays at full level).
- **PitchVariation has no audible effect** (0.3 on sequential, random, positional and area-wide sounds: every play
  at the file's pitch). OpenWinter ignores it.
- **Times**: 0 plays only in the hours set in Hours (bit n = hour n), 1 by day, 2 by night, 3 always. Day/night and
  the hour changing start or silence the sound at once.
- **EnvAudio** (soundeax.2da EAX presets): no audible effect in EE. The same click in an area with EnvAudio 0 and one
  with 8 (cave) decays identically within 40 ms. OpenWinter does nothing with it.

## Footsteps

Measured with `footfall` (a human, footstep type normal, and a badger, type soft, running away on stone):
footsteps play at 0.071 of the sample at the listener and fade **linearly to silence at about 20 m** (fits within
0.2 dB using the listener above). The **badger's (non-humanoid) footsteps play** with the same gain and falloff: its
model's c-walk/c-run events carry snd_footstep, which OpenWinter plays. soundgain.2da's "footsteps 40/100" do
not show as metres.

## Not matched yet

- The battle-music hold is an average (9 s); the original varied 7.8-10.4 s and its exact rule is unknown.
- OpenWinter switches day/night music and ambience when its clock reaches night; after SetTime the client's clock
  updates 3-4 s later than the original's switch.
- OpenWinter's mixer comes out about 3 dB under the original for music (stereo) and positional sounds; the player
  levels are calibrated against the recordings (music player at 0 dB, positional x1.41) rather than explained.
- The original's volume sliders' curve is not measured (all runs at 1.0); OpenWinter keeps its own.
- RandomPosition and sounds with several files at random are implemented from the fields, not measured; Priority
  is not used. NWNX_Player's per-player music calls reach the client as `nwnx-player` notifies it still ignores.

## Recording audio (`tools/visual/audio_scene.py`)

`run <scene> --engine nwn|openwinter --music 1 --effects 1 --audible` records the scene's sound from the harness-owned
process (nwmain under the nwmain lock, with native framebuffer shots; OpenWinter with `--visual-audio` and
`--visual-volumes`). **Both play through the speakers**, so it refuses without `--audible`.
A scene's `tones.json` puts pure tones in the sandbox override for the run only. Analyses are silent:
`tones` (each tone's intervals, level, frequency), `levels` / `phases` (a tone's level over time or per scene phase;
`--nfft 16384` separates tones a few hertz apart), `music --refs a,b` (which installed track plays, where in it, and
its level; the scene's tones are notched out first), `envelope`.
