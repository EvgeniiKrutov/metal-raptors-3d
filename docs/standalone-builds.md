# What breaks in a standalone player

Almost everything in this project is built at runtime — meshes, materials, terrain,
textures, colliders. The editor is forgiving about that because every shader is loaded,
every mesh is CPU-readable and every variant compiles on demand. A player build is not:
it only keeps what something in the project *references*, and the failures are silent.
Nothing here shows up in `Player.log` except the mesh-collider one.

## Non-readable meshes lose their colliders

`PlaneFactory.AddPlaneCollider` assigns `MeshCollider.sharedMesh` at runtime. In a player,
mesh data is uploaded to the GPU and dropped from the CPU unless the model importer has
**Read/Write Enabled**, so PhysX cannot bake a collision mesh:

```
CollisionMeshData couldn't be created because the mesh has been marked as non-accessible. Mesh name "wingUpper"
```

The plane then had no collider at all — no scrapes, no bullet hits, no crashes.
`sopwith_camel.fbx`, `fokker_dr1.fbx` and `albatros_d3.fbx` (under
`Resources/objects/planes/world_war_1`) therefore set `isReadable: 1`. The prop models (`objects/trees/`, `objects/burned_houses/`)
stay non-readable on purpose: `BattlefieldProps` gives them capsule and box colliders sized
from renderer bounds, which needs no mesh data.

`AddPlaneCollider` also falls back to a box hitbox and logs a warning if it is ever handed
a non-readable mesh again, so the failure is loud instead of invisible.

## Fog variants are stripped when no scene enables fog

`ProceduralTerrain.ApplyFog` turns fog on from script. Every scene asset ships with
`m_Fog: 0`, and **Fog Modes** in Graphics settings was on *Automatic*, which keeps only the
fog modes some scene enables — none. All `FOG_LINEAR` variants were dropped from the build,
so the player rendered the battlefield with no haze at all and the terrain read as a hard
cut-out against the sky.

`ProjectSettings/GraphicsSettings.asset` now uses `m_FogStripping: 1` (Custom) with
`m_FogKeepLinear: 1` and both exponential modes off, since `ApplyFog` only ever asks for
`FogMode.Linear`.

## `shader_feature` keywords need a material asset to survive

`_SURFACE_TYPE_TRANSPARENT` and `_EMISSION` are `shader_feature_local_fragment` pragmas in
`Universal Render Pipeline/Lit`. Unity compiles those variants only for keyword combinations
that some **material asset in the build** actually uses. Every material here is
`new Material(Shader.Find(...))` plus `EnableKeyword` at runtime, so neither variant existed
in the player:

- `UIFactory.MakeTransparent` set `_Surface`, the blend modes and the render queue, but
  without the keyword URP's `OutputAlpha` returns a hard `1.0`. Smoke, smoke columns and
  blast dust rendered as solid boxes.
- `UIFactory.CreatePrimitive3D(emissive: true)` lost its glow, which is muzzle flashes,
  tracers, sparks, explosion cores, embers and clouds.

`Assets/Shaders/Resources/RuntimeTransparentLit.mat` and `RuntimeEmissiveLit.mat` exist only
to anchor those two keyword sets. Nothing loads them; being inside a `Resources` folder is
enough to put them in the build, which is what makes the shader preprocessor compile the
variants. **Do not delete them**, and if new runtime code enables a different `shader_feature`
keyword, add an anchor material for it too.

## Custom render passes and the back buffer

`GodRays.RayPass` reads the camera colour target and bails out when
`UniversalResourceData.isActiveTargetBackBuffer` is true. URP renders straight to the back
buffer whenever no pass needs an intermediate texture, which can happen in a player even
though the editor's Game view almost always forces one. The pass now sets
`requiresIntermediateTexture = true` so URP always allocates one.

It also declares `builder.UseAllGlobalTextures(true)`. `builder.UseTexture` on
`cameraDepthTexture` orders the pass correctly but does not bind the global
`_CameraDepthTexture` that the shader samples; without the declaration render graph is free
to hand the pass an unbound depth texture, and the sky mask the shafts are built from
collapses.

## Terrain shaders

Both terrain systems build their `TerrainData`, `TerrainLayer` and material entirely at
runtime, so no shader reference exists at build time and `Shader.Find` returns null in the
player. `ProjectSettings/GraphicsSettings.asset` pins the whole URP terrain family under
**Always Included Shaders**:

| Shader | Used for |
| --- | --- |
| `TerrainLit` | terrain surface inside `basemapDistance` |
| `TerrainLitBase` | terrain surface beyond `basemapDistance` |
| `TerrainLitAdd` | additional-light pass on terrain |
| `TerrainLitBasemapGen` | runtime basemap texture generation |
| `TerrainDetailLit` | mesh detail prototypes |
| `WavingGrass` | grass detail prototypes |
| `WavingGrassBillboard` | `DetailRenderMode.GrassBillboard`, which is what `CreateGrassPrototype` uses |

### Why `drawInstanced` is off

Both terrains previously set `terrain.drawInstanced = true`. Instanced terrain rendering
needs the `INSTANCING_ON` variant of the terrain shader, and URP's variant stripping
(`m_StripUnusedVariants: 1`) removes it, because no material in the build declares it. The
shader loads, the draw call issues, and no geometry comes out — invisible terrain with a
completely clean log.

Turning instancing off drops that dependency. The cost is CPU-side terrain patch generation
instead of GPU instancing, which is negligible here: three tiles at heightmap resolution
1025, with the camera only 420 units out.

### Grass needs a `TerrainData` template asset

Pinning the shaders was not enough, and stripping turned out not to be the cause at all.
`Hidden/TerrainEngine/Details/UniversalPipeline/BillboardWavingDoublePass` ships (it is in
`globalgamemanagers.assets`) and its `ForwardLit` pass keeps 2 vertex and 4 fragment variants
after stripping — and a missing variant renders magenta, not nothing. The player's
`UniversalRenderPipelineRuntimeTerrainShaders` container also survives with all three detail
shader pointers non-null, so the terrain engine can build its grass material. `Player.log`
carries none of the detail-database errors either (`Terrain has zero detail resolution`,
`Read/Write is disabled on the Texture referenced by the Terrain Detail Prototype`,
`Detail removed: invalid detail layer`), so the prototypes and detail layers are accepted.

What is left is the long-standing engine bug: a `TerrainData` built from `new TerrainData()`
at runtime renders no details in a player, however healthy its detail database looks. The
community workaround — the only one that reproducibly works — is to clone a `TerrainData`
**asset on disk** that already carries a detail prototype pointing at the same texture the
game uses:

- `Assets/Editor/GrassTemplateBuilder.cs` writes `Assets/Terrain/Resources/GrassBlades.asset`
  (the generated blade sprite, baked once from `GrassTextureSeed` instead of the level seed)
  and `Assets/Terrain/Resources/GrassTerrainTemplate.asset` (a bare `TerrainData` whose only
  content is that grass prototype and the waving parameters). It runs from
  **Metal Raptors ▸ Rebuild Grass Terrain Template**, and recreates the pair automatically on
  editor load if the template is missing.
- `ProceduralTerrain.NewTerrainData` loads the template and returns
  `Object.Instantiate(template)`. Both terrain systems go through it. If the template is gone
  it falls back to `new TerrainData()` and logs a warning, so the editor still works while
  builds stay broken loudly rather than silently.
- `SetupGrassDetail` no longer overwrites `detailPrototypes`; keeping the cloned template's
  prototype is what preserves the link to the on-disk texture. It only rebuilds the prototype
  when a `TerrainData` arrives without one, i.e. on the fallback path.

The blade texture is baked once rather than per level seed. Nine randomly placed blades in a
64×128 sprite is not variety anyone can see at the gameplay camera distance, and a shared
asset is what the workaround requires.
