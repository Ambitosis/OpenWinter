# Stock buff duration and caster-origin control

`python tools/visual/visual.py run reststockduration --only nwn --native-framebuffer --tag _neutral`

Accepted artifacts: `out/visual/shots/vis_reststockduration_neutral/nwn/`.
The harness clones the installed cleric into its sandbox. Actual PC identity is
logged, cleric level 15, effect caster level 10. The stock Shield of Faith script
is dispatched on the PC and a separate neutral non-resting creature. Each target
receives two spell 450 effects, one actual stock EventSpellCastAt callback (which
reports 421), and 600 s remaining duration. Both actors are non-enemies and quiet.

At native 6.004 s STARTED, the PC still has both effects with 595 s remaining.
By 6.305 s, the PC and the non-resting target both have zero effects. They remain
absent during rest and after explicit cancellation at 12.008 s. This rules out natural
expiration and establishes cleanup reaching another recipient of the PC's buffs.
Native PC GetAC remains 20 while the target falls 15 to 12: no claim that all stat
caches update synchronously. The effect enumeration is the removal gate.
All four native framebuffer images were inspected, including active Resting UI.

The public installed source `x0_s0_shieldfait.nss` (base_scripts.bif, nwn_base.key)
computes duration using TurnsToSeconds(GetCasterLevel(OBJECT_SELF)); the script
does not explicitly remove effects on rest. Only the public source was inspected;
no installed asset is committed. `restcreatorrules` isolates the actual predicate.

Rejected earlier `_reference` artifacts are retained: buffs dispatched, but rest
never began, and the required character-fixture name note was missing. They do
not prove rest behavior. The accepted source changes neutralize the control and
emit the identity note. Current input hashes apply only to `_neutral`. Exact
cleanup latency and frame-exact animation are not inferred from sparse samples.
