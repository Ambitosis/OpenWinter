# Stock player rest presentation

Generated sandbox, actual PC plus two stationary NPC controls. Run through the visual
harness with native framebuffer capture; retained run is
`out/visual/shots/vis_restpresentation_reference/nwn`.

Accepted PC observations (native game-time elapsed from the request):

- Start hook sees resting=1 at 0.000s. Frames b/c show the sit-down transition; d/e
  show the seated hold at 2.199/5.000s.
- The installed Resting... panel is about 128x60 at (577,270), 1280x720 viewport.
  Blue progress grows through b-e. It is hidden in f after completion.
- Queue callback and finished hook report resting=0 at 10.500s. Frame f (+10.695)
  still shows a seated pose, g (+11.602) shows getting up, h (+12.402) is standing.
  Server completion and visual standing therefore need separate lifetime handling.
- The authored 90-second generic +2 AC effect is present at start and +5s, absent
  at completion (AC 15 to 13). This does not establish every spell's removal policy.

Rejected animation context: both NPCs finish their queued rest immediately (0.000s),
never report resting=1, and remain standing. Their sitcross/sitcrossps replacements
prove nothing about successful NPC rest animation. The PC is the valid visual control.

Nine native frames were inspected. Raw TGAs, metadata and notes are retained; the
compact evidence manifest hashes their existing framebuffer manifest, rather than
copying assets into the repository.
