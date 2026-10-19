# Native door input

Run `python tools/visual/visual.py run doorways --only nwn --native-framebuffer`.
Posted input touches only the harness-owned secondary-monitor window. The source
fixture's installed wooden doors use a one-way authored transition.

State gates: `open` both 1; `radial_close` both 0; `left_transition` both 1 and PC
near (17.37,17.30); `plain_opening_click` both 1 and PC beyond the opening near
(16.69,22.96); `plain_leaf_click` plain 0/linked 1. Earlier quick right-click controls
closed their radial before inspection; the final fixture holds the button while
capturing/selecting the menu, as required by the original.

This is the native input control. The paired visual fixture is `doorlook`; the
client's menus open on button release, so this held-native sequence is not an
identical client input timing test. See [door evidence](../../../../docs/DOORS.md).
