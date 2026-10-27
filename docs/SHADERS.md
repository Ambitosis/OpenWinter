# Module shaders

Neverwinter Nights: Enhanced Edition lets modules and persistent worlds ship their own GLSL shaders. OpenWinter
runs them: it translates a module's shaders into Godot shaders when a material needs them, and it keeps the
script uniforms scripts set. This page is for module authors: what works, what is approximated, and how to check
your shaders.

## How a mesh gets a custom shader

A mesh whose bitmap names a material file (MTR) in the module, its haks or the override folder is drawn with that
material. An MTR with `customshaderVS` and/or `customshaderFS` is drawn with those shaders:

```
customshaderVS vslit
customshaderFS my_wet
texture0 tno01_grass
parameter float wetDarken 0.20
parameter int mode 2
parameter float tint 1.0 0.5 0.25 1.0
```

- Shaders and include files are looked up by name in the module, its haks and the override folder (`.shd`),
  then in the player's installed game: the game's own shader library (`inc_standard`, `inc_lighting`, ...) and its
  standard programs (`vslit`, `fslit`, `vslit_nm`, ...) are read from the installation at run time, exactly as the
  game ships them. A missing stage is `vslit` / `fslit` (`_nm` with a normal-map render hint).
- `texture0` to `texture5` bind `texUnit0` to `texUnit5` (and set `texture0Bound` ...); `texture0` defaults to the
  mesh's bitmap.
- Each `parameter` sets the shader uniform of that name (`float` with one to four values, `int`).
- An MTR without custom shaders still gives the mesh its `texture0`.
- The game's own MTRs are not used yet: meshes that name one keep the material OpenWinter has always given them.
- Shaders are sent to players with the module's other presentation resources (`.shd` and `.mtr` in the module,
  or in haks).

If a shader can't be translated or compiled, the mesh keeps its normal material and the client logs one line
naming the material, the shaders and the reason. Sources that would include, nest or expand without end are
refused rather than followed.

## Writing shaders

Write shaders as for the original: define the switches, include `inc_standard`, and call the standard functions.

```glsl
#define SHADER_TYPE 2
#define FOG 1
#define KEYHOLING 1
#define LIGHTING 1
#include "inc_standard"
#include "inc_scriptable"

uniform float bandWidth;

void main()
{
    FragmentColor = vec4(1.0);
    SetupStandardShaderInputs();
    vec3 vWorld = (m_view_inv * vec4(vPosView, 1.0)).xyz;
    float fBand = mod(floor(vWorld.x / bandWidth), 2.0) * scriptableFloat1;
    FragmentColor.rgb = mix(FragmentColor.rgb, ApplyColorSpace(scriptableVec1.rgb), fBand);
    ApplyStandardShader();
    gl_FragColor = FragmentColor;
}
```

(`parity/visual/scenes/shaders` has this shader with its MTR and a script driving it; it draws the same on
OpenWinter and on the original.)

### What the renderer does for you

OpenWinter's renderer lights, fogs and keyholes every surface itself, from the scene's lights. The library
functions that do this work in the original (`SetupLights`, `ComputeLighting`, `ApplyFog`, `ApplyKeyhole` and
`ApplyTargetGroundHighlighting`) are replaced by OpenWinter's, which leave the colour alone; the rest of the
library runs as installed. A shader's fragment stage therefore describes the surface rather than its final colour:

- `FragmentColor` after `ApplyStandardShader()` (and whatever the shader does to it afterwards) is the surface
  colour the renderer lights.
- `vFragmentNormal` (view space) and `fRoughness` are used for the lighting. A shader whose own code sets
  `fSpecularity`, `fRoughness`, `fMetallicness` or `SpecularColor` (wet surfaces, puddles) gets highlights from
  them; others are lit as OpenWinter lights plain meshes, without highlights.
- Shaders with `LIGHTING 0` are drawn unlit, exactly as they set their colour. `FOG 0` turns the renderer's fog
  off for the material.
- A module's own functions are never replaced, even when they have one of these names.

So a shader that darkens, tints or masks the surface works as in the original. A shader that reads the lit,
fogged colour back (to mix in fog by hand, say) works on the unlit colour instead.

### Engine values

| Name | Meaning in OpenWinter |
| --- | --- |
| `vPos`, `vNormal`, `vTcIn`, `vColor` | The vertex in the mesh's space (skinned meshes arrive skinned) |
| `m_mv`, `m_mvp`, `m_proj`, `m_proj_inv`, `m_view`, `m_view_inv`, `m_vp_inv`, `m_texture` | As in the original; world space is Z-up |
| `gl_FragCoord`, `gl_FrontFacing`, `screenParams`, `pixelParams`, `clipParams` | As in the original (origin at the bottom left, depth growing away from the camera) |
| `scriptableFloat1..16`, `scriptableInt1..16`, `scriptableVec1..16` | Set by SetShaderUniformFloat / Int / Vec for this player |
| `worldtimerTimeOfDay`, `sceneCurrentFrame`, `moduleHour` | Milliseconds of real time (wrapping daily), frame count, game hour |
| `playerPosition`, `cameraPosition`, `cameraFocus`, `cameraPitch`, `cameraYaw`, `cameraViewAngle`, `playerCameraDist` | Kept up to date every frame |
| `areaFlags`, `fogColor`, `fogParams`, `lightAreaAmbient`, `lightAreaDiffuse`, `lightAreaDiffuseDirection` | The area's flags, fog and sun or moon |
| `materialFrontDiffuse`, `fAlphaDiscardValue`, `texUnitEnv` | From the mesh and its texture |

Not provided yet (they read as zero): the per-light arrays (`lightPosition` ...), `bboxMin` / `bboxMax`, the wind
point sources, `areaGlobalWind`, `areaWeatherType` and `areaWeatherDensity`, and the spell targeting shape.
Writes to `gl_Position` are ignored: the vertex is placed from `vPosView`.

## Script uniforms

`SetShaderUniformFloat`, `SetShaderUniformInt` and `SetShaderUniformVec` set one of three separate banks of 16
(`SHADER_UNIFORM_1` to `_16`) for one player; ints arrive exactly, so they can carry packed bit fields. When
OpenWinter's client joins an NWN:EE server, these three arrive the same way.

`SetMaterialShaderUniformInt` / `Vec4` set a parameter on one object's meshes that use the named material (MTR,
any case), and `ResetMaterialShaderUniforms` clears them. On OpenWinter's server they stay on the object, and
players who enter the area later get them too; how the original behaves there is not measured yet. They are not
mapped when joining an NWN:EE server (OpenWinter's description of that protocol has no message for them).

## Checking your shaders

```
dotnet run --project src/OpenWinter.Tools -c Release -- shader-check --module <module> --compile
```

translates every shader program your materials use (and every program the module ships) and compiles the result
with Godot, then lists each one as `ok` or `fallback` with the reason. `--show vs,fs` prints one translation;
`--out <dir>` keeps them. The translations contain the installed game's library code: keep them to yourself.

In the client, `--shader-uniforms "int9=65280;float1=0.5;vec2=1,0,0,1"` holds script uniforms at these values
(scripts can't change them) to preview a shader, `--module-shaders off` draws every mesh with its normal
material, and `--capture-shaders <dir>` takes a few views of the area and writes which programs were used.

## Not supported yet

- Replacing the engine's own programs by name: the post-process pass (`fsfbpostpr`) and water (`fswater`,
  `vswater`). These are reported by `shader-check` and drawn the usual way.
- The game's own MTRs (stock ice, some heads).
- Geometry shaders (`customshaderGS`).
- `ReplaceObjectTexture` on meshes drawn with a custom shader.
