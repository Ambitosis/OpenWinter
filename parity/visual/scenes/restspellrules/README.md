# Player rest hooks and foreign-created effects

`python tools/visual/visual.py run restspellrules --only nwn --native-framebuffer --tag _reference`

Accepted original artifacts: `out/visual/shots/vis_restspellrules_reference/nwn/`.
All four framebuffer captures were inspected and show the actual PC and rest UI.
Sixteen independent effects cover temporary/permanent, magical/supernatural/
extraordinary/unyielding, and spellID -1/450 via public SetEffectSpellId.
They are created by the area-enter script, not the PC. `restcreatorrules` resolves
that creator distinction explicitly; spellID alone does not cause start cleanup.

All 16 survive STARTED and the observations at +0.105/+0.293/+0.500/+3.996 s.
At +10.504 s completion, only permanent supernatural and either-duration unyielding
remain (six effects). The queued successor runs before module FINISHED in that same
timestamp. A PC creature OnRested handler installed using event 5009 never fires.
The module STARTED callback's assigned ClearAllActions veto preserves all 16, sends
CANCELLED, and cancels the queued successor. No-rest-area rejection preserves 16,
runs the successor and produces no rest hooks.

Limits: the first kit's elapsed value precedes initialization of its rest clock.
Script sample times are authoritative; framebuffer latency is recorded separately
and does not establish exact blend or engine scheduling intervals. The fixture
does not measure creator cleanup, ForceRest global cleanup, or NPC rejection.
Hashes and framebuffer metadata are in `evidence.json`.
