# Seeker Arrow attack casting control

This scene identifies the native body animation for Seeker Arrow (spell 601)
using the actual `ActionUseFeat(451, target)` route and unchanged stock impact
script. Two authored NPCs have Fighter 6/Wizard 1/Arcane Archer 4, feat 451,
bow proficiency/focus, and installed longbows/arrows. These are ability-qualified
NPC fixtures, not a test of player character creation or prestige prerequisites.

The left caster is unchanged. The right receives public
`ReplaceObjectAnimation("bowshot", "salute")`. Both first make an ordinary bow
attack, stop, then use Seeker Arrow once. Targets log real physical-attack and
stock spell-601 events; script notes also check equipment, actual coordinates,
six-metre separation, perception, feat availability, action state and HP.

```powershell
dotnet run --project src/OpenWinter.Tools -c Release -- parity-run seekerarrow_gate --only nwn
python tools/visual/visual.py run seekerarrow --only nwn --native-framebuffer --profile plain --tag _reference
```

The headless preflight passes on its first native run: feat use 1 -> 0, equipped
longbow base type 8/arrows 20, sight true, real stock event 601 with correct caster,
and target damage. Action completion is +1.514 s; stock impact is +3.828 s after
the request. Impact arrives later because this is a travelling arrow. Native
`GetIsEnemy=0` is retained in the evidence; autonomous AI targeting is not tested.

The first visual run also passes all execution gates. Each target receives one
ordinary attack and one actual stock 601 event. Seeker action completion is
+1.503 s; both stock impacts arrive +3.820 s. HP before/after Seeker is 199 -> 181
and 192 -> 180; exact random damage is diagnostic, not an expected parity value.
`GetHasFeat` reports zero once the one-use feat is spent, so the request snapshot
is the availability gate.

Inspected native framebuffer `e_cast_030.png` shows the left caster drawing/aiming
its bow while the right salutes. The ordinary-attack control also changes the
replacement actor's pose. This establishes `bowshot` identity for this equipped
longbow ability. The +0.85 and +1.35 samples show both back in bow-ready posture,
although the latter still reports action 4. The installed `a_ba` bowshot has
authored length 1 s and hit event 0.5 s. This supports one body clip rather than
looping for the approximately 1.5 s action. Shot names describe requested times;
framebuffer latency and polling prevent an exact return-to-idle timestamp claim.

Artifacts: `out/visual/shots/vis_seekerarrow_reference/nwn/`, including notes,
raw TGA, native PNG and `native-framebuffer.jsonl`. The `_window.png` GDI
companions are excluded. Compact measurements are in this folder's `evidence.json`.

Sources were the player's installed public `spells.2da`, `classes.2da`, `feat.2da`,
`x1_s2_seeker` source, and model animation data, followed by sandbox native
measurements. No installed assets or stock source are copied into the repository.
Rows 600-604 share `CastAnim=attack`, but only 601's longbow path is measured here.
Row 602 shares the impact script and signals event 601. Other bow abilities,
weapon types, zero-time drain rows 769/802 and their model-specific melee clips
require separate controls.


Client baseline command:

```powershell
python tools/visual/visual.py run seekerarrow --only openwinter --profile plain --tag _baseline --openwinter-args "--visual-delay 0 --visual-state"
```

The baseline exits zero and passes both ordinary-attack and stock-spell gates.
At `b_attack_030`, both caster snapshots contain `bowshot` (the replacement is
stored under that name with its replacement animation data). All three cast
snapshots `e_cast_030`, `f_cast_085`, `g_cast_135` have null body actions; inspected
cast images show idle casters. Each caster consumes exactly one feat use and
emits one stock 601 event, with target HP stable after the impact. Ordinary bow
animation therefore works while Seeker's spell-driven body animation is absent.
Existing texture RID warnings appear only during shutdown.

A separate pre-existing mismatch is retained: client stock impact occurs at
+1.367 s versus native +3.820 s at 6 m. Action completion matches (+1.500 versus
+1.503 s). This body-animation fixture does not establish projectile timing or
path parity, and its animation fix must not claim that mismatch is resolved.

Fixed client run (`--tag _fixed`, otherwise the same command) now plays one
`bowshot` on each caster. At +0.30/+0.85 the stock clip advances to 0.319/0.880 s
with loop count zero, then is absent at +1.35. The public replacement retains its
own 0.5 s duration and has already finished at +0.85. All nine frames were
inspected, including `out/seekerarrow-fixed-contact.jpg`. Ordinary attacks still
play, each feat spends one use and produces one stock event with positive damage;
the fix does not generate another physical attack. See `clientFixed` in the
evidence JSON for source/frame hashes and state checks.

The server selects the bow family from current authoritative equipment when
casting begins; the client plays the named clip once. A regression covers a bow
equipped after enqueue, removal/replacement before execution, and instant casts.

Remaining differences are explicit: impact is still +1.367 s, the native casters
return to bow-ready while the client returns to idle, and native quiet targets
stand while the client targets hold combat-ready fists. These require separate
flight and combat-state controls. This proves the missing body clip
is restored, not complete audiovisual or precise pose-transition parity.
