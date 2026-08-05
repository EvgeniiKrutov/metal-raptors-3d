# Terrain in standalone builds

Both terrain systems (`ProceduralTerrain`, `CampaignTerrain`) build their
`TerrainData`, `TerrainLayer` and material entirely at runtime — nothing about the
terrain exists as a scene asset. That works in the editor, where every shader in the
project is loaded, but it makes the terrain invisible in a player unless the build
is told to keep the shaders it needs.

## Why runtime terrain disappears

Unity only compiles a shader into a build if some scene or asset references it.
Because the terrain material is `new Material(Shader.Find(...))`, no reference
exists at build time, and the shader is stripped. `Shader.Find` then returns null in
the player — silently, with nothing in `Player.log`.

`Universal Render Pipeline/Terrain/Lit` was already listed under **Always Included
Shaders**, so the terrain surface itself survived. The rest of the URP terrain shader
family was not, which is why grass never rendered in a build.

## Always Included Shaders

`ProjectSettings/GraphicsSettings.asset` now pins the full set:

| Shader | Used for |
| --- | --- |
| `TerrainLit` | terrain surface inside `basemapDistance` |
| `TerrainLitBase` | terrain surface beyond `basemapDistance` |
| `TerrainLitAdd` | additional-light pass on terrain |
| `TerrainLitBasemapGen` | runtime basemap texture generation |
| `TerrainDetailLit` | mesh detail prototypes |
| `WavingGrass` | grass detail prototypes |
| `WavingGrassBillboard` | `DetailRenderMode.GrassBillboard`, which is what `CreateGrassPrototype` uses |

Anything else built from `Shader.Find` at runtime needs the same treatment. The
project's custom shaders avoid the problem by living in `Assets/Shaders/Resources/` —
a `Resources` folder is always included, which is why god rays, the searchlight beam
and the skybox already survived stripping.

## Why `drawInstanced` is off

Both terrains previously set `terrain.drawInstanced = true`. Instanced terrain
rendering needs the `INSTANCING_ON` variant of the terrain shader, and URP's variant
stripping (`m_StripUnusedVariants: 1` in the global settings) removes it, because no
material in the build declares it. The shader loads, the draw call issues, and no
geometry comes out — invisible terrain with a completely clean log.

Turning instancing off drops that dependency. The cost is CPU-side terrain patch
generation instead of GPU instancing, which is negligible here: three tiles at
heightmap resolution 1025, with the camera only 420 units out.

If instancing is ever wanted back, the fix is to stop creating the material from
`Shader.Find` and reference a real material asset from a `Resources` folder instead —
a material asset gives URP's shader preprocessor something concrete to compile
variants against.
