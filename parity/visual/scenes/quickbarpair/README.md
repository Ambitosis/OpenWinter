# Paired quickbar reference probe

Native runs: `out/visual/shots/vis_quickbarpair_baseline/nwn`
and `out/visual/shots/vis_quickbarpair_followup/nwn`. The follow-up also preserves
`exported-pair.bic` copied before another harness run reset its local vault.

Measured: F5 removes both members when both are equipped; from empty or different
equipment it equips both. With only either desired member equipped it restores the
pair. All transitions completed by the first 0.1-second public-state sample.
Transferring the shield to another creature cleared F5 (visible empty slot), and
returning it did not restore the binding: the final BIC has QBList[4] type 0.
This export does not establish the native secondary backpack coordinates; the
later `quickbarpairbind` export measures both members independently in ItemList.

The baseline run has no event evidence. The follow-up installs hooks with SetEventScript. Full removal
reports left then right; replacement removes the different left then right, and
reports right then left equip. Unequip callback getters expose the departing item;
both desired items are present by the first equip callback. Current generic
OpenWinter inventory callback timing does not yet reproduce those snapshots.

Inspected transition frames show the right arm reaching back on both directions.
The installed model has drwright/drwleft clips, but asynchronous screenshots do
not establish precise start, item visibility switch, or blend timings.
Native F5 layers the horizontal main icon behind the upright shield across its
64-by-32 icon area. No native animation timing has been inferred from the labels.

The installed `data/lcv/cleric.bic` has a native paired item binding in F5/QBList[4]:
type 1, main `QBItemInvSlot=16`, secondary `QBSecItemInvSlot=32`, both repository
coordinates 0,0, no cast property (255). Equipped right is `nw_wblmml007` (base 9),
left `nw_ashmsw010` (base 14). These fields were inspected with the repository's
GFF reader; no behavior is inferred from their names. The disposable character
fixture changes only the display name and retains that native binding.

Run only when monitor 2 is available, through the sandbox harness:

```
python tools/visual/visual.py run quickbarpair --only nwn --native-framebuffer --tag _baseline
```

This uses posted F5 input, never pointer warps or focus. The fixture logs actual
initial item identity, current right/left items, ownership, action ID and ordered
equip/unequip events. It activates F5 with the original pair, repeats it, explicitly
empties the hands, activates again, equips a distinct club/shield, then activates
and repeats. The empty/other preconditions must be verified in each `.before` log;
reject a phase whose setup did not complete. A missing action/equipment change is
not proof of no-op unless key delivery has a positive gate in another phase.

Screenshots show original quickbar art, transition and settled poses. Framebuffer
latency is asynchronous: the `transition` labels alone do not prove exact animation
timing. Before implementing animation, retain action/event timing plus sufficient
valid frames to identify whether native draws/sheathes, plays a separate equip
animation, or changes items immediately. The final character export stays inside
the harness sandbox; inspect its QBList main/secondary references against the
exported item list to measure how native rewrites the pair after moves.

This probe measures activation of an existing genuine native binding. Pair creation
in `quickbarpairbind` passed its single-main F12 gate, then off-hand drop, compound
activation/toggle and export checks. Its preserved export also retains the untouched
original F5 pair after movement. Offhand weapon pairs, destroyed-member handling,
two-handed conflicts and partial equipment failure remain separate native controls.
