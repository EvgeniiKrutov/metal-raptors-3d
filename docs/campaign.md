# Campaign mode

An endless side-scrolling flight over streamed terrain. Entry points: main menu → career →
World War 1 → start (or → level select → a level), and main menu → custom battle → start
(scene `CampaignLevel1`, controller `CampaignLevelController`, definition registry
`CampaignLevels` in `CampaignDefinition.cs`). See docs/main-menu.md for the pages that lead
here.

## Levels and maps

`CampaignLevels` carries two levels, and a level names a **terrain kind** as well as a seed:

| Level | Terrain | Daytime | Page |
| --- | --- | --- | --- |
| 1 | `TerrainKind.Verdun` | Morning | this one |
| 2 | `TerrainKind.Flanders` | Morning | docs/flanders-coast.md |

**One scene serves every level.** `CampaignLevel1` is the only endless scene; the menu writes
the wanted level into the static `CampaignRun` before loading it, the same trick
`CustomBattle` already uses, and `CampaignLevelController` reads `CampaignRun.Level` in
`Start`. A third level is a registry entry and a list row, not another scene file. (The
scene still carries an orphan serialized `levelNumber` from before this change; nothing reads
it.)

`CampaignTerrain` is now an abstract streamer with a concrete land per kind —
`VerdunTerrain` and `FlandersTerrain`. `CampaignTerrain.Begin(kind, …)` picks one. The base
owns everything about *chunks* (the keep-window, time-slicing, terrain object assembly,
neighbour links, disposal); a subclass owns everything about *ground* (its height scale and
world Y offset, how a chunk's heights are filled, how it is painted, what it decorates with,
and what extra meshes ride along with it).

## Rules of the level

- The plane flies left to right; a level ends when its script says so (docs/campaign-scripts.md),
  and a level with no script flies forever. Touching the ground
  fails the run and the overlay shows the distance flown (RETRY / BACK TO MENU). Scoring is a
  separate future feature — nothing is persisted. On Flanders Coast, touching the *water*
  fails the run too, but by sinking rather than exploding (docs/flanders-coast.md).
- The ceiling is `WorldTop = 650` (it was 900), and the fixed challenge levels dropped to the
  same height, so the ground stays legible below the player on every Verdun surface.
- The camera follows the plane but its X only ever ratchets forward; it never scrolls back.
- **No turning back**: a hard invisible wall rides the camera's left view edge. Like the hard
  ceiling at the top, it blocks movement (slide along it, no damage, no crash) and never
  auto-turns the plane — the pilot keeps full control of the heading. Implemented as
  `CubeController.Initialize(..., hardLeftWall: true)` + `SetLeftWall`, which replaces the
  fixed levels' soft `FlightSteering.EdgeSteer` boundaries. The wall is armed only after the
  intro, since the plane flies in from behind it (docs/level-intro.md).
- A level **opens on an intro**: the frame holds still, the plane flies in from off the left
  edge with the controls dead, and the script's first radio call plays between two black film
  bars. Control comes back during the fly-in (docs/level-intro.md).
- The daytime is authored on the definition: level 1 flies at dawn (`Daytime.Morning`). Sky,
  fog and ambient reuse the same sky classes as the fixed terrain levels.
- A **custom battle** is the one exception. When `CustomBattle.Requested` is set (the menu's
  custom battle screen did it), the controller builds
  `CampaignLevels.Custom(map, daytime)` — the picked map's seed under the picked sky — in
  place of the authored definition. Career's start clears the request first, so the two
  entry points never bleed into each other.

## Streamed Verdun terrain (`VerdunTerrain`)

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

The keep-window is derived from Verdun's fog distance — for both lands, since what it really
measures is how far land stays visible, and Flanders hides its far ground under water rather
than under haze (docs/flanders-coast.md). Past that distance the land is
pure haze that matches the skybox's horizon band, so chunks beyond it are invisible either
way. Each frame the streamer drops chunks behind `camX − (fogEnd + 0.5·chunk)` (this is the
"removed once off camera" rule — the wall keeps the plane from ever reaching bare ground) and
builds missing ones up to `camX + (fogEnd + 1.5·chunk)`. Removal destroys the chunk's
`TerrainData` and wall mesh explicitly — they are assets, not scene objects.

## Level scripts, dialogue and enemy waves

`CampaignDefinition.script` names a text script that drives the level's pacing — dialogue at
the bottom of the screen, timed pauses, enemy waves, and the win condition. Level 1 runs
`level1`; level 2 and custom battles have none and stay endless. See
docs/campaign-scripts.md for the file format, the dialogue bar, and how the enemy AI was
adapted to a forward-scrolling world, and docs/campaign-ww1-scenario.md for the WW1 era's
plot, cast, loading-screen text and every radio line (story only — none of it is wired up).
docs/campaign-ww1-portraits.md holds the avatar generation prompts for those speakers.

## Pre-level briefing

`CampaignDefinition` also carries the briefing text — `title`, `dateline` and `lore` — shown on
a full-screen page before the level starts. Custom battles skip it. See docs/level-briefing.md.

## Shared pieces extracted in this change

- `PlaneFactory` — aircraft rig building (model orientation/mirroring, collider, propeller,
  muzzle mount), moved out of `LevelController` so both controllers spawn identical planes.
  The plane physics layer constant lives there too.
- `HealthBar` — the HUD health readout used by both level types.
- `ProceduralTerrain` now exposes its shared ingredients (land layer, grass prototype/detail
  setup, cut-wall mesh + material, crater bowl maths, per-daytime fog) for the streamer.

## Plane rig

Every plane, on both level types, is a bare physics-body `GameObject` that its controller
(`CubeController` or `EnemyController`) yaws directly to the heading each frame, carrying the
visible aircraft model as a child (built by `PlaneFactory`). Keeping the model a child rather
than yawing it directly lets the model's own built-in orientation fix (the stand-up/roll
correction baked into its export) compose with the heading instead of being overwritten by it.
See docs/flight-model.md for the collision and damage model shared by both controllers.

On the fixed slab levels (no procedural terrain), a backdrop wall sits a little behind the
play plane: the main directional light shines into `+Z`, so the wall catches the plane's
silhouette as a visible drop-shadow. It's purely visual — it receives shadows but casts none,
and has no collider, since the camera looks straight down `+Z` and would never actually be
occluded by it.

`PlaneFactory.BuildPlaneModel` reads its per-model rest orientation, scale and propeller node
names from a `PlaneModelConfig` (`PlaneModels.Fokker` / `.Sopwith`) rather than baking them in
as constants — different FBX exports can sit at different rest orientations and node names, so
a differently-exported or brand-new model is a new registry entry, not a code change. The same
config drives both the upright player and the mirrored enemy; the mirror-specific handling
(skipping the wheels-down roll, since the enemy's own ~180° heading spin already flips it
belly-down) stays in `BuildPlaneModel` rather than in the per-plane data.
