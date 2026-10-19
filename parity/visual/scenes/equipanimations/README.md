# Single-hand equipment animation control

Three matched stock bandits execute public ActionEquipItem/ActionUnequipItem
commands for a club in the main hand and a small shield in the left hand,
separately. STOCK is untouched; RIGHT replaces drwright with salute; LEFT
replaces drwleft with salute. Both hands start empty. Four waves each have an
early and settled native framebuffer sample, plus an initial framing control.

```powershell
python tools/visual/visual.py run equipanimations --only nwn --native-framebuffer --tag _reference
```

Acceptance requires all actor/item identities, actual hand contents, ownership,
stationary coordinates, and four queued completion callbacks per actor. Module
PC equip/unequip handlers also log if invoked; these NPC actors are expected to
produce zero PC callbacks, so callback absence is not an execution failure.

The requested early frames are +0.20 s and settled frames +1.60 s. Actual log
times and native framebuffer latency must be retained; these are pose samples,
not exact blend or transition timing. A distinct replacement pose is required
before identifying a clip. This script-driven control does not measure UI input
or combat interruption policy.

## Original result

The single original run exits 0. All 51 actor-state rows have valid actors,
appearance 269, exact stationary positions, both item ownership gates, and the
expected hands. Each actor completes all four commands; queue callbacks execute
in the same observed public clock tick as the request. No module PC equipment
events fire, as expected for NPC actors. These immediate action completions do
not mean the visible draw animation is instantaneous.

All nine native framebuffer images were inspected. In `b_main_equip_early`,
only center RIGHT salutes; STOCK and LEFT reach over their right shoulder. In
`d_left_equip_early`, only rightmost LEFT salutes; STOCK and RIGHT perform the
left-hand shield motion. This establishes **drwright for single main-hand equip
and drwleft for single left-hand equip**, rejecting a shared all-hand drwright
selection. In the removal early frames, the corresponding replacement actor
also differs from both controls: it has already returned toward idle while the
two controls are still performing the relevant draw. Removal samples support
the same clip identities but do not independently measure playback direction
or exact speed.

All settled samples return to idle. The replaced actor can lack the visibly
attached club/shield even though public hand state is correct: replacing a body
clip also changes its authored model-switch events. These controls identify the
body clips, not identical attachment timing or a new attachment rule.

Actual early script notes are +0.196 to +0.201 s; settled notes +1.600 to
+1.606 s. Native framebuffer latency is recorded separately in evidence.json;
log polling adds further delay. Initial `a_empty` elapsed is an absolute public
clock reading because no wave start has been set yet. No millisecond animation
duration, blend, PC callback timing, simultaneous pair precedence, combat
interruption, other weapon families, or UI input behavior is claimed.

Artifacts: `out/visual/shots/vis_equipanimations_reference/nwn/`, including raw
TGA/frame metadata, notes and all nine native PNGs; harness log
`out/equipanimations-native.txt`. Source/frame hashes and gates are retained in
`evidence.json`. Only authored scene files are committed, never installed assets.
