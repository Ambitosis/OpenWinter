# Player rest creator and subtype matrix

`python tools/visual/visual.py run restcreatorrules --only nwn --native-framebuffer --tag _reference`

Accepted artifacts: `out/visual/shots/vis_restcreatorrules_reference/nwn/`.
Both neutral targets have 48 independent effects: three creators (area-enter
script, resting PC, other NPC), two spellIDs (-1/450), two durations
(temporary 300 s/permanent), four subtypes (magical/supernatural/extraordinary/
unyielding). AssignCommand changes the constructor's OBJECT_SELF. Every effect
logs creator ID, spellID, duration and subtype. Prefix `m` actually records
creator 1 (the area), not module 0; `p` records PC 7fffffff; `n` records NPC d.

STARTED sees 48 on both targets. The accepted-rest sample at +0.3 s has 44 each:
only p000, p010, p100 and p110 disappeared. Thus PC-created magical effects end
on self and the other recipient, for either duration and either spellID. All
other creators and subtypes survive. Cancellation after accepted start does not
restore those four. The second full 48-effect kit is vetoed by assigned
ClearAllActions in STARTED: CANCELLED fires and all 48 remain on both recipients.
All three framebuffer images were inspected and show the expected active/cancelled
rest UI; script effect enumeration supplies the rule evidence.

Scope: accepted player ActionRest only, independent SkillIncrease effects and the
stock linked buff in `reststockduration`. Cross-area recipients, linked mixtures
of different subtypes, area-of-effect objects, effect-removal callback reentrancy,
NPC caster-origin cleanup and ForceRest caster-origin cleanup remain unmeasured.
No rule depends on spell 450 or this appearance. Script note times and framebuffer
latencies are distinct; `evidence.json` hashes inputs, notes and frames.
