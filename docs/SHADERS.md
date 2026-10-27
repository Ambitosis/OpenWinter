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
- A body or armour part with a palette texture (PLT) uses the MTR named after its PLT. Its `texture0` is the part's
  tinted texture, which keeps the palette's alpha (`texture0.a`, the environment-map mask), so a shader can
  recognise texels by the palette row a colour index picked.
- The game's own MTRs (the stock ice of `tti01`, the old man's and old woman's heads) are used as well, as in the
  original (its ice walls are smooth grey, without the streaks the plain material shows). A module's MTR of the same name comes first.
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
- `texture0` is sampled as plain materials are (colour decoded before filtering) and handed to the shader in the
  usual encoding, so a translated surface is exactly as bright as the same surface without a shader (scene
  `shaderlit`).
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
| `vPos` | In the module's own vertex code, the vertex in its mesh's space, as in the original (a head's or an armour part's own coordinates). The model's space in the library's code, in a shader that assigns to `vPos`, and in a shader that writes `gl_Position` on a mesh that moves within its model (bone-attached and skinned meshes, whose mesh-space values would hold them in their rest pose) |
| `vNormal`, `vTcIn`, `vColor` | The vertex's normal, texture coordinates and colour (skinned meshes arrive skinned) |
| `m_mv`, `m_mvp` | In the module's own vertex code, on meshes that don't move within their model (tiles, placeables built still), the mesh's own matrices; otherwise the model's (bone-attached and skinned meshes, the library's code, shaders that assign to `vPos`) |
| `m_proj`, `m_proj_inv`, `m_view`, `m_view_inv`, `m_vp_inv`, `m_texture` | As in the original: world space is Z-up, projections are GL's (y up, depth -1 to 1) |
| `bboxMin`, `bboxMax` | The mesh's bounding box, in its own space |
| `gl_FragCoord`, `gl_FrontFacing`, `screenParams`, `pixelParams`, `clipParams` | As in the original (origin at the bottom left, depth growing away from the camera); `clipParams` are OpenWinter's camera planes |
| `texFBColor`, `texFBDepth` | What was drawn before, read at GL screen coordinates: for a surface the opaque scene, for the post-process pass the finished view (see below) |
| `scriptableFloat1..16`, `scriptableInt1..16`, `scriptableVec1..16` | Set by SetShaderUniformFloat / Int / Vec for this player |
| `worldtimerTimeOfDay`, `sceneCurrentFrame`, `moduleHour`, `moduleMinutesPerHour`, `moduleDawnHour`, `moduleDuskHour` | Milliseconds of real time (not the game clock: SetTime doesn't change it), frame count, game hour, the module's minutes per hour, dawn and dusk hours |
| `playerPosition`, `cameraPosition`, `cameraFocus`, `cameraPitch`, `cameraYaw`, `cameraViewAngle`, `playerCameraDist` | Kept up to date every frame; `cameraPitch` in degrees from straight down, as SetCameraFacing's |
| `areaFlags`, `fogColor`, `fogParams` | The area's flags and fog |
| `lightAreaAmbient`, `lightAreaDiffuse`, `lightAreaDiffuseDirection` | The area's ambient and sun or moon colours, linear as in the original, and the direction towards the sun or moon in view space. As in the original, the sun and the moon shine from one fixed direction up in the north-east (world (0.433, 0.487, 0.758), about 49 degrees up), at every hour and in every kind of area, until SetAreaLightDirection sets another (scene `sundir`) |
| `areaGlobalWind` | SetAreaWind's direction times its magnitude, as given; the area's own wind blows diagonally at its strength (medium (0.707, 0.707, 0)) |
| `areaWeatherType`, `areaWeatherDensity` | WEATHER_* shown, and how far it has faded in, from 0 to 100; after SetWeather clear the type stays while it fades out (the original was still at full density three seconds after clearing, OpenWinter fades over the weather's fade time) |
| `targetShape...`, `targetWorldCoords` | While a script's targeting mode with SetEnterTargetingModeData lasts: its shape, sizes, flags, range, spell and feat, and the ground under the mouse |
| `materialFrontDiffuse`, `fAlphaDiscardValue`, `texUnitEnv`, `texUnitNoise` | From the mesh and its texture; the noise is the game's `solid_noise` |

Not provided yet (they read as zero): the per-light arrays (`lightPosition` ...), the wind
point sources (`windPointSourcesCount` is 0) and the targeting shape of spells cast from the spellbook. A module's own vertex code that writes `gl_Position` places
the vertex; the installed library's placement is OpenWinter's own. A module's own fragment code that writes
`gl_FragDepth` sets the depth.

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
material, and `--capture-shaders <dir>` takes a few views of the area (and of the player up close) and writes
which programs were used; `--shader-script <name>` runs a script on the player first (from `--script-dir`, to set
material uniforms), `--shader-wait` / `--shader-after <seconds>` start later and wait longer after it.

## Replacing the engine's own programs

A module can replace two of the engine's programs by name.

**The post-process pass** (`fsfbpostpr`, with `vsfbpostpr`): a module that ships either draws it as a full-screen
pass over the finished 3D view and under the interface, every frame. `texFBColor` is the view as shown
(gamma-encoded), `texFBDepth` its depth: water writes its surface's depth, translucent surfaces only where they are
solid (as in the original, scenes waterfx and postfx); the library's `GetDepth`,
`GetViewPositionFromDepth` and `m_view_inv` give back world positions, and the colour the shader writes is what
the player sees. The original's own post-processing options (sharpen, depth of field, vibrance ...) are not part
of it: `FS_PostProcess()` returns the view's colour. Without a module pass, the view is drawn as usual.
`clipParams` and the depth are OpenWinter's camera planes (near 0.05, far 4000 m); the original's are near 0.1 and
far at the area's fog clip distance (45 m by default), so a shader that compares a depth with `farClip` to find
the sky sees the sky much further away. The darkening drawn while the player is blind is part of the view the
pass reads (where the original draws it relative to the pass is not measured).

**Water** (`fswater`, `vswater`): when a module ships either, meshes whose texture is the original's water (TXI
`proceduretype arturo`) are drawn with `vswater` and `fswater` (the module's, or the installed game's for the
other stage). Water's colour is taken as already lit (it is built from the view behind it): the renderer adds only
highlights and fog. `texFBColor` and `texFBDepth` are the opaque scene behind the water; the water writes its
surface's depth, as the original's does. `texUnitNoise` is the game's `solid_noise`, and `texUnitEnv` the water
texture's `bumpyshinytexture`.

(`parity/visual/scenes/postfx` and `waterfx` have test shaders for both; they draw the same on OpenWinter and on
the original, except where the water itself differs, below.)

## Not supported yet

- Water past the area's edge: the original continues the water of the edge tiles beyond the area (with its own
  water as with a module's, scenes watershadow and waterfx); OpenWinter draws water only on the area's tiles.
- Geometry shaders (`customshaderGS`).
- The highlights the original adds to every lit surface from the library's material values (a small specular
  term whose colour comes from the texture: the faint texture detail it keeps under a flat tint). OpenWinter lights
  such surfaces without highlights, and gives a shader that sets the values itself the renderer's own highlights.

## ReplaceObjectTexture

`ReplaceObjectTexture` on a mesh drawn with a custom shader works as in the original (scene `shadertex`): when the
new name has an MTR with shaders, the mesh is drawn with that material (whose `texture0` still defaults to the
mesh's own bitmap); otherwise the mesh keeps its material and the new texture becomes its `texture0`.
