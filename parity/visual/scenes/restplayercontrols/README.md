# Player rest replacement and cancellation controls

Four sequential trials on the actual sandbox PC. Retained native evidence:
`out/visual/shots/vis_restplayercontrols_reference/nwn` (13 inspected frames).

1. Replace sitcross with salute. Frame a shows salute during entry, b shows the
   ordinary seated hold, c shows the replacement again during exit, and d/e stand.
   Finished hook and queue callback occur at +10.504s.
2. Restore sitcross; replace sitcrossps with deadfnt. Frame f retains the sit-down
   transition and g shows the replacement floor pose at +2.500s. Frame h returns
   to the exit sequence; i stands. Completion occurs at +10.500s. These two trials
   distinguish transition and sustained-hold clip identities; they do not identify
   arbitrary creature-model fallbacks.
3. ClearAllActions from the started hook. The hook initially sees resting=1; after
   ClearAllActions it still reads 1 inside that hook, then the cancellation hook reads 0
   at the same sampled game time. Frame j (+0.395s) has no rest panel or seated pose.
4. Ordinary rest followed by an owned-window X click. k shows a seated PC/panel;
   posted click occurs around +3.5s, cancellation hook is observed at +3.652s,
   l (+3.996s) has no panel but remains seated, m (+6s) stands. This is a positive
   native panel-X action, not just a presumed coordinate.

Stock Shield of Faith observations are retained only as observations: trial 1 starts
with two spell 450 effects/AC 16 and reads none/AC 13 by +0.246s; hook-cancel trial 3
retains them, trial 4 has none by +2.496s. Caster-level/duration and natural-expiry
controls were not established here. Do not infer a general spell-removal-on-rest
rule from these readings; that needs its own control.

All runs used the sandbox harness and posted input without focus or real-pointer
changes. Raw TGAs and per-frame provenance remain outside source control. See
`evidence.json` for compact artifact/source hashes and frame correspondence.
