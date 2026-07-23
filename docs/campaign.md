# Campaign mode

An endless side-scrolling flight over streamed terrain. Entry point: main menu → CAMPAIGN →
LEVEL 1 (scene `CampaignLevel1`, controller `CampaignLevelController`, definition registry
`CampaignLevels` in `CampaignDefinition.cs`).

## Rules of the level

- The plane flies left to right forever; there is no win condition yet. Touching the ground
  fails the run and the overlay shows the distance flown (RETRY / BACK TO MENU). Scoring is a
  separate future feature — nothing is persisted.
- The camera follows the plane but its X only ever ratchets forward; it never scrolls back.
- **No turning back**: a hard invisible wall rides the camera's left view edge. Like the hard
  ceiling at the top, it blocks movement (slide along it, no damage, no crash) and never
  auto-turns the plane — the pilot keeps full control of the heading. Implemented as
  `CubeController.Initialize(..., hardLeftWall: true)` + `SetLeftWall`, which replaces the
  fixed levels' soft `FlightSteering.EdgeSteer` boundaries.
- The daytime (morning/midday/evening/night) is picked on the campaign menu panel and
  persisted separately from Air Fights Level 1 (`GameManager.CampaignDaytime`). Sky, fog and
  ambient reuse the same sky classes as the fixed terrain levels.

## Streamed terrain (`CampaignTerrain`)

Same Verdun look as `ProceduralTerrain` (rolling hills, shell + mine craters, grass, the
front dirt cut wall, daytime fog), but built as an endless strip of 512 m chunks, each one a
runtime Unity `Terrain` (257×257 heightmap, so X resolution is exactly 2 m — the fixed
level's fidelity) plus a cut-wall mesh. Shared per-level assets (terrain layer, materials,
grass texture/prototype) are created once and reused by every chunk.

### Why chunks are seamless without stitching

- **Heights are a pure function of world position.** The ridge line is world-space Perlin
  octaves (the fixed level's whole-cycle sines only tile a finite width), plus the same
  depth-drift and roughness noise, front-strip flattening and clamping as the fixed land.
  Columns are sampled at *global* sample indices (`worldX = (chunkIndex * 256 + ix) * 2 m`),
  so the seam column shared by two neighbours evaluates to bit-identical floats in both.
- **Craters come from hashed world cells.** World X is divided into fixed 128 m cells; each
  cell's craters (count, position, radius, depth) are generated from
  `System.Random(hash(seed, cellIndex, salt))`. A chunk gathers all cells within the widest
  crater influence of its span, so a crater overlapping a seam is stamped identically into
  both chunks.
- **Terrain LOD**: chunks share `groupingID` with auto-connect plus explicit `SetNeighbors`
  links, so neighbouring terrains tessellate compatibly at the seam.

### Why it never hitches

- Chunk builds are time-sliced: the build is an iterator of small checkpoints (16 heightmap
  rows, one crater stamp, one `SetHeights`, 40 grass rows, ...), and a coroutine advances it
  only while a ~3 ms per-frame budget lasts. A chunk finishes in a fraction of a second while
  consumption is one chunk per ~2 s of flight, with several chunks of lead distance.
- Grass uses a deterministic jittered grid (one tuft per ~4.5 m cell; the cell counts are
  rounded so the grid divides the chunk exactly and tufts reach the seam with no bare strip)
  instead of the fixed level's Bridson Poisson sampling: visually equivalent at this density,
  but sliceable row by row, where Poisson is one indivisible pass.
- The opening window around the spawn is built synchronously in `Begin` (a short scene-load
  beat instead of land popping in on the first frames).

### Chunk lifecycle

The keep-window is derived from the fog: past `RenderSettings.fogEndDistance` the land is
pure haze that matches the skybox's horizon band, so chunks beyond it are invisible either
way. Each frame the streamer drops chunks behind `camX − (fogEnd + 0.5·chunk)` (this is the
"removed once off camera" rule — the wall keeps the plane from ever reaching bare ground) and
builds missing ones up to `camX + (fogEnd + 1.5·chunk)`. Removal destroys the chunk's
`TerrainData` and wall mesh explicitly — they are assets, not scene objects.

## Enemy configuration (not yet spawned)

`CampaignDefinition.waves` is a list of `EnemyWave { distance, EnemyGroup[] }`: at N metres
flown, those formations become due. `CampaignLevelController.CheckWaves` already consumes the
list in order (logging each due wave), but **spawning is intentionally not implemented** —
the existing `EnemyController` AI assumes fixed world bounds and needs adapting to a moving
window before enemies can join the campaign. Level 1 ships with an empty wave list.

## Shared pieces extracted in this change

- `PlaneFactory` — aircraft rig building (model orientation/mirroring, collider, propeller,
  muzzle mount), moved out of `LevelController` so both controllers spawn identical planes.
  The plane physics layer constant lives there too.
- `HealthBar` — the HUD health readout used by both level types.
- `ProceduralTerrain` now exposes its shared ingredients (land layer, grass prototype/detail
  setup, cut-wall mesh + material, crater bowl maths, per-daytime fog) for the streamer.
