# Tutorial door failure feedback

`python tools/visual/visual.py run doorfeedback --native-framebuffer --openwinter-args "--ui classic --visual-delay 0"`

Copies the installed Prelude barracks and restores the tutorial door's installed
failure script through a logging wrapper. No game script or dialog is copied
into the fixture. Five actions isolate key-required Open, key-required Unlock,
ordinary locked Open, custom-feedback Unlock and custom-feedback Open.

Every action must invoke OnFailToOpen with GetClickingObject identifying the PC.
Open displays TLK 8296 in chat and the short TLK 8307 status above the actor.
Unlock displays TLK 7944 (or the custom text) only in chat. Every failure also
runs the installed one-liner, speaking above the door and in chat. See
[door feedback findings](../../../../docs/DOORS.md#tutorial-locked-door-feedback).

These are action/API controls, not physical left-click/radial timing tests.
Exact camera framing, bubble presentation and lifetime are not parity assertions.
