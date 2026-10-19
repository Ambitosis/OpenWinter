# Door interaction and audio

How doors are picked, opened, closed, seen through and heard, matched against the original client and server.

## Picking

An open ordinary doorway is empty space for picking: clicking through it moves the character, and clicking the
swung leaf closes it. Door picking intersects the model's currently posed triangles, not the skinned model's rest
bounding box, so it follows whichever way the door has swung. The opening side is carried in the object flags,
including authored initial states, so newly spawned observers see the same pose.

A closed door is picked anywhere across its frame, including between the bars of a cell door (the Prelude
menagerie's jail doors highlight with the pointer between the bars, as in the original). The stock door models
carry an invisible `sam` plane spanning the doorway (219 of 241 door models; it matches the frame and does not move
when the door opens), and a closed door's picking includes it. An open door does not use it, so its doorway stays
empty space.

## Transition doorways

An open linked doorway is a transition target. Hovering it shows the model's blue translucent transition geometry
and the installed `gui_mp_transu` cursor; leaving it hides the plane. Left click uses the transition; the radial's
explicit Close action closes the door. Both interface styles send separate OpenDoor/CloseDoor requests rather than
sending Use for a button labelled Close. The doorway plane also accepts right click for the radial.

The transition plane comes from the door MDL, not a separate VFX texture or model. The stock `t_door01`, `t_door03`
and `ttr_udoor_01` have a normally invisible `sam` mesh whose `trans` animation keys alpha to 0.25. Its vertices
define the opening and its self-illumination supplies the blue. The client finds these parts by their
animation/alpha data, without assuming a node name or generating a rectangular replacement, renders them
separately and keeps them out of ordinary leaf highlights. `door-audit <model> ...` (in `OpenWinter.Tools`) prints
the game data used here.

## Linked doors

Opening or closing a linked door also updates its destination door before the source callback. The destination's
lock is retained and its OnOpen/OnClosed hooks do not fire. A one-way destination does not modify the source when
operated directly. `SetTransitionTarget(..., OBJECT_INVALID)` publishes the changed transition flag and does not
restore the old authored tag.

## Sounds

Door sound appearances come from `genericdoors.2da.SoundAppType` or, for tileset doors,
`doortypes.2da.SoundAppType`. Both index `placeableobjsnds.2da`:

| Operation | Column |
| --- | --- |
| Successful state change to open | Opened |
| Successful state change to closed | Closed |
| Attempt to open a locked door without its key | Locked |
| Door destroyed by damage/death | Destroyed |

They play through the positional Effects3D sound path. Opening an already-open door or closing an already-closed
one is silent. Destruction playback is detached from the object, so despawning it does not cut the sound short.
A sound row naming a sample that the installation lacks (base row 10's `as_dr_metlmedcl2`) stays silent unless a
module or override supplies it; no substitute is invented.

## Tutorial locked-door feedback

The Prelude `m0q1a.git` door `M1Q0A_M1Q0B` has `OnFailToOpen = nw_od_feedback8` and
`Conversation = nw_all_feedback`. The stock script selects feedback 8 through `NW_L_FEEDBACK`, calls
`SpeakOneLinerConversation()` with no explicit resource, and queues a reset of that local. The dialog's starting
condition selects entry 6. The result is ordinary door speech, above the door and in chat with the door as
speaker; no conversation window opens. One-liner default-resource lookup uses the same creature/door/placeable
defaults as full conversations.

Failure feedback is distinct from the door's own speech:

| Action | Chat feedback | Short overhead status | Door failure hook |
| --- | --- | --- | --- |
| Open locked door, including key-required | TLK 8296: This object is locked. | TLK 8307: `*locked*`, above actor only | Fires |
| Unlock key-required door without key | TLK 7944: A specific key is required to open that object. | None | Fires |
| Unlock with custom key-required feedback | Custom text | None | Fires |

Open ignores custom key-required feedback. The callback's door speech appears in both chat and overhead on all of
these failures, separately from the feedback above. GetClickingObject in the hook returns the acting player.

## Action timing

Opening a door is a one-second reach followed by a half-second wait before the creature's next action; OnOpen
fires at the end of the reach. Closing fires immediately on arrival, then waits half a second. Opening an
already-open door still reaches and fires OnOpen, and closing an already-closed door still fires OnClosed; neither
repeats the state-change sound.

Repeated clicks on the same door leaf queue rather than cancel the current reach or recovery, so opening callbacks
are 1.5 seconds apart and closing callbacks 0.5 seconds apart however fast the player clicks. Normal walking and
script ClearAllActions still cancel the queue. Actions assigned to the door itself are immediate. This is action
cadence, not a global animation-duration lock.

## Camera-side fading and room discovery

**Room discovery.** Opening a door into an interior room reveals that room's tiles and scenery; closing it again
leaves them discovered. Creatures are different: an actor in a room sealed by a closed door is neither seen nor
heard, in either direction, until the door opens again. This is a current tile-visibility restriction on top of the
ordinary range, line-of-sight and stealth checks. A free-standing door that does not seal a room does not block
hearing.

Interior discovery spreads eight steps (a diamond in tile coordinates) from the player's tile through connected
openings; moving within a tile leaves it unchanged and crossing a tile edge advances it. The fog clip distance does
not affect it. Openings between neighbouring tiles come from the tile walkmeshes: any face whose `surfacemat.2da`
row has `WalkCheck` = 1 on both sides of the seam counts (walkable ground, Nonwalk, Transparent, pits, deep water,
lava); Obscuring and undefined faces (wall tops) do not. A closed door blocks the opening when its
`genericdoors.2da` / `doortypes.2da` `BlockSight` is 1 (wooden doors); a door with `BlockSight` = 0 (a portcullis)
keeps sight, hearing and discovery open. `ExploreAreaForPlayer` also reveals world scenery, not only the minimap.
The per-player explored tiles are saved; the derived room graph is rebuilt at run time.

Only explored tiles are drawn; unexplored tile geometry stays black and depth-blocking. With
`SetCreatureExploresMinimap(pc, FALSE)` nothing new is explored, so new rooms stay black even with the player in
them. `ExploreAreaForPlayer(area, pc, FALSE)` clears the explored map and the visible rooms are refilled straight
away; after an earlier `ExploreAreaForPlayer(..., TRUE)` the area stays drawn even though the map is cleared. When a
tile becomes unexplored again, placeables, doors and ground items already shown on it stay drawn, while static
placeables, never-shown placeables and creatures (including the player's own character) follow the explored state.
A creature that is only heard is still drawn as its translucent shape. A boundary door is revealed when either side
is explored.

**Camera-side fading.** When the camera and the player are on opposite sides of a door's closed frame, the whole
door fades to 25% opacity. This applies whichever side the player is on, to open leaves, and to doors off to the
side of the player at any distance; it is not a ray test against the character. The dividing plane has a band of
about 0.15 m: an actor up to 0.1 m onto the camera's side still fades the door, 0.2 m does not. A faded door
neither highlights nor intercepts left clicks (they reach the floor) and hides its transition plane. Doors are
excluded from keyholing.

## Testing

- Parity suite `door_links`: source/target state and event ordering, read by the same authored scripts on the
  original server and OpenWinter (`dotnet run --project src/OpenWinter.Tools -c Release -- parity-run door_links`).
- Client probe: `tools/godot.sh --headless -- --verify-doors <out dir>` checks closed/open rays, both swing sides,
  hover visibility, cursors, radial action selection, late-spawned observers, transition suppression, tile
  visibility, boundary doors and all base door sound rows.
- Visual scenes (`python tools/visual/visual.py run <scene>`): `doorways` (radial Close, transition click, opening
  click, leaf click), `doorlook` (closed, open and transition-hover looks), `dooraudio` (the four action cues;
  `tools/visual/check_door_audio.py` compares the waveforms), `doorfeedback` (the locked-door feedback table),
  `dooractiontime` / `doorclickrate` (action timing), `doorreveal`, `roombars`, `roomrange`, `storeroom`,
  `minimapoff` and `unexploredobjects` (discovery masks; `tools/visual/check_door_visibility.py` checks them), and
  `doorcamera`, `doorpickside`, `doorpickreverse`, `doorfar`, `doorfaredge` and `dooralpha` (camera-side fading).
- Tools: `room-audit <area> <pc-x> <pc-y> [door-x door-y] --module <module>` prints the current tile mask;
  `room-sweep [area...] --module <module>` prints every interior area's mask from each origin tile, for diffing
  before and after a rule change.

## Limits

- Self-issued opening actions can choose a different swing direction from the original; picking follows whichever
  pose is drawn, but the original's swing-choice rules and animation timing are not claimed identical.
- The transition plane's colour uses authored data; exact lighting and blending are not pixel-identical.
- The camera-side fade uses the same 25% alpha, but Godot blends in linear light while the original blends encoded
  RGB, so a faded door looks lighter than the original's. Fade easing has not been timed.
- Disconnected rooms within one tile, tiny custom openings, bridges and multiple floors are unmeasured; room
  discovery is verified on the stock interiors, not claimed for every custom tileset.
- Custom weighted/skinned door meshes and animated transition geometry beyond the stock alpha-only `trans` clips
  are unmeasured.
- The audio comparison normalises gain and timing: attenuation, onset timing and gain are not claimed identical.
- Container feedback and successful key-use feedback are not covered here.
