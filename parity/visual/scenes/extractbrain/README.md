# Extract Brain creature casting control

This fixture identifies the body animation used by the installed Extract Brain
ability (spells.2da row 716), with the real stock impact script left intact.
It does not exercise the whole autonomous mind-flayer AI decision tree.

Two stock `x2_mindflayer001` actors face paralyzed human `nw_bandit001` targets
at 1.5 m. Event scripts are cleared, AI level remains normal, and the fixture
waits five seconds for native NPC perception. The left actor is unchanged; the
right receives public `ReplaceObjectAnimation("cspecial", "ctaunt")`.
The action call matches the installed AI's Extract Brain call, including
`METAMAGIC_ANY` and cheat casting. Target `OnSpellCastAt` records the real stock
716 event and the expected caster. Position, sight, paralysis, intelligence,
current action and queued completion are logged separately from images.

Run only through the sandbox harnesses:

```powershell
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run extractbrain_gate --only nwn
python tools/visual/visual.py run extractbrain --only nwn --native-framebuffer --profile plain --tag _tail
python tools/visual/visual.py run extractbrain --only openwinter --profile plain --tag _baseline --openwinter-args "--visual-delay 0 --visual-state"
```

The headless dispatch preflight establishes a real stock 716 event at +0.501 s
and queued completion at +3.617 s. The visual `_stock` and `_tail` runs both
pass real-event, actor, position, perception and paralysis gates. `_tail` events
arrive at +0.526/+0.556 s; both queues finish at +3.623 s. Both targets lose
intelligence in that run. Random attack/drain outcomes are supplementary;
the stock event is the execution gate. `GetIsEnemy` remains zero, so this is
not evidence of autonomous hostile target selection.

Native framebuffer images show the stock actor leaning forward while the
replacement actor has a different, upright arm pose. This identifies `cspecial`
as the selected body clip. The +0.30 requested sample already shows the stock
lean. At +3.05 and +3.35 it is upright, although the +3.35 script marker still
reports action 4. The installed model's authored `cspecial` length is 3.1 s.
These observations support playing the authored clip once, independently of
the roughly 3.6 s action lifetime. They do not establish an exact idle versus
final-pose-hold boundary. Native framebuffer capture adds 0.120-0.154 s plus
log polling; sample labels are not precise animation timestamps. Foreground
targets partly obscure the caster bodies.

The pre-fix client `_baseline` also passes both stock-event gates (+0.533 s),
finishes at +3.633 s, and applies intelligence loss. All nine state snapshots
report null body action for both mind flayers; inspected mid-cast images show
them standing idle. Exit is zero, with existing shutdown texture RID warnings.
This isolates missing client animation from failed ability execution.

## Client correction

`SpellFx.Cast` now plays the current model's `cspecial` once when the transmitted
casting code is `creature`. It uses the existing animation replacement and playback
paths, with no spell-ID or model-resource special case. Item uses and instant casts
retain their prior presentation; their creature-code variants are unmeasured.
Server ability rules, impact and queue timing are unchanged.

The `_fixed` run uses the same command with `--tag _fixed`. Both real events occur
at +0.533 s and queued completion at +3.633 s. Renderer snapshots advance the stock
clip monotonically from 0.340 to 3.084 s, with Loop=None and authored length 3.1 s.
The replacement retains its own two-second duration. Both are finished at the
+3.35 sample without replay. All nine frames were inspected, including full-size
early, middle and tail frames and `out/extractbrain-fixed-contact.jpg`. The body
lean and return now follow the native observations. Exact pose transitions,
target reactions, VFX and audio are not accepted by this body-only comparison.
The capture exits zero with the existing shutdown texture RID warnings.

The stock dispatch suite matches native 11/11 with no differing or missing
readings (`out/extractbrain-gate-parity.txt`). Random intelligence-drain magnitude
is logged as a supplemental diagnostic, excluded from deterministic comparisons.

Artifacts are under `out/visual/shots/vis_extractbrain_{stock,tail,baseline,fixed}/`.
Use the native `.png`/`.tga` and `native-framebuffer.jsonl`; `_window.png` GDI
companions are an invalid owner-window control and are excluded.

Earlier `_reference` and `_eligible` runs are rejected: zero real events and
immediate queue completion. VSpawn's Plot/Merchant overrides and the initial
four-second request were replaced by stock creation and a five-second wait.
Those changes were not isolated, so no single rejection cause is asserted.

Knowledge sources: the player's installed public `spells.2da`, appearance row
413/model `c_mindflayer`, its authored animation data, and public source scripts
`x2_ai_mflayer`/`x2_s1_suckbrain`; then the sandbox native controls above. No game
assets or stock source scripts are copied into this repository. The seven
`CastAnim=attack` rows and ordinary creature-prefixed cast loops remain separate
unmeasured animation work.
