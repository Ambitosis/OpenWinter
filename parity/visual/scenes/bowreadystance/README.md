# Bow ready-pose identification probe

This authored control follows `seekerarrow`, retaining qualified archers,
ordinary-attack and real stock spell-event gates. It changes the public
replacement on the right archer to `bowrdy -> salute` and records caster/target
`GetIsInCombat` alongside action state. The installed public `a_ba` model has a
one-second `bowrdy` clip and a one-second `bowshot`; neither name is assumed.

```powershell
python tools/visual/visual.py run bowreadystance --only nwn --native-framebuffer --profile plain --tag _reference
```

The native run executes one ordinary attack and one real stock spell-601 event
per actor, consumes one feat use each, preserves six-metre positions and damages
both targets. During Seeker recovery both caster combat flags are zero while
target flags are one. By the final post-impact sample all four flags are one.
The empty action queue therefore does not imply an out-of-combat flag, and the
flags alone do not identify the visual pose.

Inspected +0.85/+1.35 cast and ordinary +1.40 frames show bows held ready, while
the +2.0 and final post-impact frames show relaxed poses. The right actor's
replacement has not provided a conclusive ready-clip identity or playback rule.
Do not wire combat flags directly to `bowrdy` or claim a successful replacement
control from these frames. No runtime behavior changes are justified by this
probe alone. Precise return transitions and idle-pose selection remain open.

Native framebuffer artifacts are in
`out/visual/shots/vis_bowreadystance_reference/nwn/`; `_window.png` companions
are excluded. The source and selected frame hashes are in `evidence.json`.
