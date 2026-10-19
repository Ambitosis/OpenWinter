# Cutscene Escape module hook

Original and client controls use the same authored scene on a real player character,
through sandbox visual harnesses on DISPLAY2. Native sound volumes were zero; client
used the Godot Dummy audio driver. No screenshots or installed assets are included.

The original's Escape during SetCutsceneMode signals Mod_OnCutsnAbort (event 3014).
It does not itself exit cutscene mode or restore Plot. Two Escape presses with a
no-op hook yield two callbacks; script-only SetCutsceneMode(FALSE) yields none.
A third callback explicitly exits, restoring the original Plot value. Both
bLeftClickingEnabled settings accept Escape. GetLastPCToCancelCutscene starts
invalid and remains the player outside the callback and after mode exit.

The client sends a payload-free request from its unhandled Escape path; the server
uses the session-bound player and rejects NPCs and players outside cutscene mode.
The module decides what cancellation means. Its last cancelling player is retained
on the module, independently of a transient script context. Existing conversation
Escape/abort conditions are unchanged: this input path defers while DialogPanel
is visible. Dialogue-visible cutscenes and left-click cancellation are not claimed.

Verification:

- Original: `python tools/visual/visual.py run cutsceneabort --only nwn --profile plain --silent --tag _reference`
- Client: `python tools/visual/visual.py run cutsceneabort --only openwinter --profile plain --silent --tag _fixed --openwinter-args "--visual-gui --visual-delay 0"`
- Both owned runs exited 0 and reached VISUAL|done. All nine shared state/callback
  notes match exactly. The client does not retain the pre-load `before` note;
  native and focused tests verify its initial invalid getter state.
- `dotnet test tests/OpenWinter.Tests --filter 'FullyQualifiedName~CutsceneAbortTests|FullyQualifiedName~ScriptEventContextTests'`.
- `tools/godot.sh --headless --audio-driver Dummy res://Diagnostics/CutsceneAbortProbe.tscn`:
  10 checks, including real Godot input dispatch and loopback request serialization.

Native notes: `out/visual/shots/vis_cutsceneabort_reference/nwn/notes.txt`.
Client notes/log: `out/visual/shots/vis_cutsceneabort_fixed/openwinter/`.
There is no frame-exact timing or visual-body claim here.
Source and note hashes, plus complete note readings, are in evidence.json.

The request regressions cover repeated callbacks, module-owned exit, persistent
getter state, script-only transitions, identity spoofing rejection, and NPC
rejection. It builds on the event context measured in `parity/suites/event_context`.
