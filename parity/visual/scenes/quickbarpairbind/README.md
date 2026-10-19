# Paired binding gesture control

Measured run: `out/visual/shots/vis_quickbarpairbind_binding/nwn`.
The club-only F12 positive control equipped the expected club. Dragging the small
shield onto that shortcut created a pair: the next F12 equipped both exact items,
and another press emptied both hands. Source hover settles for 300 ms before each
drag, avoiding the posted-input cached-target problem seen in click probes.

`exported-bindings.bic` was copied before the sandbox reset. Both the untouched
F5 and newly created F12 use type 1 with both `QBItemInvSlot` and
`QBSecItemInvSlot` equal to 4294967295 when in the backpack. Each member has its
own repository X/Y. F5 main is 5,0 and secondary 3,0; F12 main is 2,0 and secondary
0,0. Container coordinates and cast property/subproperty are all 255. Matching
the exported ItemList identifies the original mace/shield and created club/shield.

The independent F5 controls restore and remove the original pair. Inspected
framebuffer sequences show a single right-arm reach, with both held models
switching during it and idle restored by the last frames. No separate left draw
was seen. The installed `a_ba` model's `drwright` clip has length 0.667 seconds,
transition 0.3 seconds and a `draw_weapon` event at 0.3 seconds (duplicate events
at that timestamp are deduplicated). The client uses these authored values;
asynchronous screenshots do not establish exact native blend or frame timing.

The shared quickbar combines inventory off-hand drops onto a main shortcut;
moving an existing quickbar shortcut still swaps slots. Shared server activation
and persistence retain both references. Both interfaces draw the horizontal main
icon behind the upright off-hand icon. Ownership loss clears the binding, and
returning the item does not recreate it. Generic inventory callback snapshots
still differ as recorded in the activation fixture's README.

Limits: native off-hand weapon pairs, two-handed conflicts and failed-member
equipment controls are not measured. Server BIC and snapshot tests prove both
item references survive serialization, including native-only backpack entries.
They do **not** alone prove end-to-end client reload; the separate stale-cache
reload regression below covers that boundary.

Saved-character authority now applies to local-vault/single-player as well as
server-vault replies. `ElyQBSaved` or current-session quickbar edits make the
character layout authoritative, including deliberately empty slots. An unedited
native character still permits the existing one-time client-only layout migration.
`QuickbarSync.Reset` already prevents cached edits from being sent before the
reply; the bug was choosing the stale cache after a non-authoritative local-vault
reply.

The reload capture uses `--capture-hotbar-state <dir> --quickbar-reload-only`:
it serializes the character, writes a stale local cache with both old pair IDs and
a shortcut in a deliberately cleared slot, loads the BIC into a new world with
different IDs, and uses the actual world-reload/ActionBar.Load/server-reply path.
It checks the imported bar, rewritten disk cache, absence of stale edits sent back,
and F5 removal/restoration of the new items. Both classic and modern runs passed
all 10 checks and exited 0 at `out/visual/shots/vis_hotbarstate_reload_classic/openwinter`
and `vis_hotbarstate_reload_modern/openwinter`. Each used a fresh isolated Godot
profile; `invocation.json` records the exact compiled-client invocation and profile.
The inspected `c_reloaded_pair_restored.png` images show the restored pair and
empty F1 in both interfaces. `a_reloaded_pair.png` catches the loading screen and
is not visual evidence.

Client verification: `vis_hotbarstate_pair_classic/openwinter` and
`vis_hotbarstate_pair_modern/openwinter` both exit 0 with 40 checks in
`pair-state.json`. `--capture-hotbar-state <dir> --quickbar-pair-only` runs the
stock cleric fixture: actual inventory drags create F12, actual F12 toggles the
created pair, and actual F5 removes/restores/replaces the imported pair, repairs
either partial member, and verifies clearing on transfer without restoration on
return. Screenshots were inspected for both combined icons and draw/settled poses.
Model-switch checks wait for the actual draw playback to cross its authored event;
they do not assume that keypress time equals animation start. The fixture also
checks final authoritative equipment, highlighting, usability and visible models.

Two details matter: binding eligibility must come directly from the dragged authoritative inventory entry, rather than
waiting for asynchronous item-info; and a paired draw must bypass the server's
one-second appearance throttle for its completed end-of-tick equipment update.
The client retains the old visible equipment until the draw action crosses the
model's `draw_weapon` timestamp, then rebuilds while preserving action playback.
Quickbar/persistence tests cover paired ownership, activation, serialization and
native-only backpack decoding without OpenWinter extension fields.

Run with the monitor lease only:

```
python tools/visual/visual.py run quickbarpairbind --only nwn --native-framebuffer --tag _binding
```

The installed cleric's equipped F5 binding remains untouched. The fixture clears
only carried items and creates a club then small shield, providing deterministic
backpack positions. Inspect `a_inventory_source_gate` before accepting the input.
It drags the club to F12, then requires `single_main_positive_control|main=1`.
Only after that gate is an offhand-over-main result meaningful. The expected
compound state is not asserted by the script: inspect `compound_activation`,
`compound_toggle`, frames, and QBList[11] in the exported BIC.

Copy the newly written `out/visual/user/localvault/*.bic` into this run's `nwn`
shot directory immediately after completion, before any other harness run resets
that shared sandbox. The export should retain the untouched original F5 pair
after its two members moved out of the equipment slots. No real user vault is used.
