# Emitters

What the base game's particle emitters use, and how OpenWinter handles each field. The counts come from
`openwinter-tools emitter-survey` over every model in the base game (32,832 models, 9,518 emitters in 3,638 of
them; 7,235 models are text models). `openwinter-tools anim-controllers [prefix]` lists the controllers that
animations key, and `openwinter-tools emitter-raw <model...>` prints one model's emitters in full.

Semantics marked *measured* were checked against the original client in `parity/visual` (scene `emitcal`,
which ships its own text model; see `tools/visual/emitcal_model.py`). The rest follows the public modding
descriptions of the format or is a best reading of the data.

Code: `src/OpenWinter.Aurora/Models` (reading), `client/Rendering/Emitters` (simulation and drawing),
`client/Rendering/ModelBuilder.cs` (animation tracks), `client/Views/EffectPlayer.cs` (effect playback).

## Update modes

| Value | Count | Status |
| --- | --- | --- |
| Fountain | 5,593 | Done: GPU particles, rate from the (animated) birthrate. |
| Explosion | 3,274 | Done: a burst of the birthrate when an animation turns the birthrate on (or `detonate`), at once for unkeyed effect models; never on its own for placeables and creatures. |
| Single | 551 | Done: one particle that follows the emitter. A finite life reaches and holds its final ramp state without replaying the fade (*measured*: scenes `singlefade`, `singlelong`). |
| Lightning | 100 | Done: a jagged bolt to the reference node (the emitter's first child), reshaped every lightningDelay. |

## Render modes

| Value | Count | Status |
| --- | --- | --- |
| Normal | 7,965 | Done: camera-facing; drawn half its size nearer the camera (flames over brazier rims). |
| Billboard_to_World_Z | 470 | Done: flat on the ground (rings, webs, spots). |
| Linked | 419 | Partial: ordinary Fountain particles are joined into a camera-facing CPU ribbon; P2P motion is missing. With Lightning, the bolt. See [SPELL_PRESENTATION.md](SPELL_PRESENTATION.md). |
| Motion_Blur | 332 | Done: a streak along the velocity, size_y plus speed x blurlength x 0.02 long. |
| Aligned_to_World_Z | 164 | Done: upright, turning about the vertical to face the camera. |
| Billboard_to_Local_Z | 138 | Done: in the emitter node's XY plane (glyph domes, ground glows). |
| Aligned_to_Particle_Dir | 30 | Partial, *measured*: born in the emitter's X/Z plane, keeping their orientation as they age, honouring twosidedtex; direction and culling match fixed-axis and camera-reversal controls. Trail spacing, acceleration and whole-spell coverage are incomplete. |

## Blend

| Value | Count | Status |
| --- | --- | --- |
| Lighten | 5,122 | Incomplete: the original adds in display space; OpenWinter's texture and colour transfer and overlap do not yet match calibrated controls. |
| Normal | 4,396 | Done, *measured*: alpha blend; opacity raised to 2.2 to match display-space blending over dark ground. |
| Punch-Through | (text models) | Done: alpha cut-out. |

## Header fields and flags

| Field | Values seen (count) | Status |
| --- | --- | --- |
| texture | named 9,361; none 157 | Done (texture-less: white). |
| xgrid, ygrid | 1-8 (0 seen 390 times, treated as 1) | Done: flipbook grid. |
| spawntype | 0 6,327; 1 351; -1 409 | Partial, *measured*: non-local, non-P2P Fountain trails are born by distance travelled, matching speed and rotating-parent controls. Other trail routes, saturation and whole-spell coverage are open. |
| chunkName | 1,763 | Done: the chunk model's first mesh, tumbling, as each particle. |
| inherit | 3,978 | Done: particles move with the emitter (local coordinates). Single always does. |
| inherit_local | 2,050 | Parsed; not distinguished from no inheritance. |
| inheritvel | 2 | Parsed; not done. |
| inherit_part | 91 | Parsed; not done (meaning unknown). |
| random | 1,363 | Done: random start frame between frameStart and frameEnd. |
| bounce | 1,379 | Done: bounces on the height of the object the emitter belongs to, with bounce_co. |
| m_isTinted | 1,531 | Done, *measured*: lit by the scene's lights (full light colour, no facing term). |
| p2p | 193 | Partial: the GPU target path exists, but binding beam targets and P2P motion on Linked ribbons are missing (both show in the original's Fire Lash and flame beams). |
| p2p_sel (bezier/gravity) | bezier 6,846; gravity 241 | Partial (GPU): Bezier with p2p_bezier2/3 control distances, or attraction with grav. The Linked path is missing; exact motion is not measured. |
| affectedByWind | 381 | Parsed; not done (no wind or blast simulation). |
| blastRadius, blastLength | 0.2/1 on 849 debris emitters; up to 20/4 on WIND_Dummy | Parsed; not done (blasts push wind-affected particles). |
| deadspace | 0.175-0.262 (142) | Parsed; not done (meaning unclear: not an inner cone, seen with spread 0). |
| twosidedtex | 1,898 | Honoured for Aligned_to_Particle_Dir (*measured*); other modes are drawn two-sided until measured. |
| renderorder | 0-100 (460 non-zero) | Done: sorting offset. |
| splat | 36 | Parsed; not done. |
| loop | 2 | Parsed; not done. |
| update_sel, render_sel, blend_sel, p2p_type (text models) | | Done: used when the name is missing. |
| bytes 0x142-0x143 | 221 distinct values | Padding (unused). |

## Controllers

All of these are read from geometry and from animation keys (binary and text models) and drive the
emitter through Godot value tracks while animations play.

| Controller | Emitters / animated (emitter-animation pairs, multi-key) | Status |
| --- | --- | --- |
| birthrate | 9,518 / 4,679 (3,821) | Done (rate; explosion trigger). |
| lifeExp | 9,517 / 494 | Done. Pool sized for the largest keyed life. |
| velocity, randvel | 9,518 / 362, 172 | Done, *measured*: m/s, randvel symmetric. |
| spread | 9,518 / 204 | Done: full cone angle about the emitter's Z. |
| mass | 9,518 / 202 | Done, *measured*: mass x 9.8 m/s2 downward. |
| sizeStart, sizeEnd | 9,518 / 496, 330 | Done, *measured*: full width in metres. |
| sizeStart_y, sizeEnd_y | 9,492 / 10 | Done: height when not both 0. |
| colorStart, colorEnd | 9,518 / 182, 185 | Done (clamped to 0-1). |
| alphaStart, alphaEnd | 9,518 / 635, 604 | Done (clamped to 0-1; negative keys hide). |
| alphaMid, colorMid, sizeMid, sizeMid_y, percentStart/Mid/End | 0 in the base game | Done (three-point ramps). |
| fps, frameStart, frameEnd | 9,518 / 182, 159, 173 | Done: flipbook loops between the frames. |
| particleRot | 9,518 / 83 | Done: radians per second. |
| drag | 9,510 / 142 | Done: velocity /= 1 + drag x dt. |
| grav | 9,510 / 145 | Done for gravity-type p2p. |
| bounce_co | 9,518 / 152 | Done. |
| blurlength | 9,500 / 22 | Done (Motion_Blur). |
| lightningDelay, lightningRadius, lightningScale | 9,509 / 138-140 | Done (bolt reshaping, jitter). |
| lightningSubDiv (id 220), lightningZigZag (224) | 0 binary | Parsed; SubDiv adds segments. Ids unconfirmed. |
| detonate (id 228) | 12 | Done: fires an explosion. Id unconfirmed. |
| p2p_bezier2, p2p_bezier3 | 9,509 / 109 | Done. |
| combinetime, threshold | 9,509 / 138, 22 | Parsed; not done (meaning unknown). |

Meshes in effect models: alpha keys (3,006 animated, 925 in VFX) fade the mesh through its instance's
transparency; effect lights' colour, multiplier and radius keys are played (binary and text models). Uniform node scale keys are played (including Bigby's Interposing Hand).

## Effect playback

A one-shot effect plays its model's "impact" animation; a lasting effect plays "impact", then "duration" in
a loop, then "cessation" when it ends. An impact-only model applied as a lasting effect plays once (the HotU
dream cutscene's elemental protection snowfall). Persistent areas show their vfx_persistent.2da DurationVFX.

Animation-level `detonate` events trigger explosion pools independently of the birthrate and start-up, keeping
overlapping bursts until they drain. Coincident keys collapse to one burst (not yet measured against the original).
This is separate from the emitter `detonate` controller. See [SPELL_PRESENTATION.md](SPELL_PRESENTATION.md).
