# Rejected map-pin input delivery control

This scene tests posted held clicks and text input in the owned, sandboxed native
window. It never activates a window or moves the real pointer. It is a diagnostic,
not an accepted map-pin fixture.

```
python tools/visual/visual.py run classicpintext --only nwn --native-framebuffer --profile plain --tag _hardware
```

The hardware-cursor run opened the map successfully, but the attempted
Add Map Pin click moved the PC from (10,10) to (4.77,10.99). The final PC position
was (0.09,11.87); the inspected framebuffer contains no saved pin or editor. An
earlier `_heldtext` run with `softwareCursor: true` failed the same gate, moving to
(4.88,10.97) at the second frame. The final frame's historical name
`03_saved_pin_hover` describes its intended observation, not a successful save.

Both runs completed and exported/autosaved in the harness sandbox, but none of
those exports establish native pin persistence. The emitted text marker likewise
does not prove that a text editor received it. Images, positions and logs remain
under `out/visual/shots/vis_classicpintext_{heldtext,hardware}/nwn/`.

Do not repeat this probe without a new input-delivery control. A valid follow-up
must visibly activate pin mode, open the editor and retain the unique pin text
before interpreting a save. The failure's cause is not established.
