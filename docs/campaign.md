# Campaign mode

An endless side-scrolling flight over streamed terrain. Entry points: main menu → career →
World War 1 → continue (or → level select → a card), and main menu → custom battle → start
(scene `CampaignLevel1`, controller `CampaignLevelController`, definition registry
`CampaignLevels` in `CampaignDefinition.cs`). See docs/main-menu.md for the pages that lead
here and docs/level-select.md for the card page the levels are picked from.

## Levels and maps

`CampaignLevels` carries the **eight** levels of the World War 1 campaign. Their titles, dates,
terrains and skies come from the campaign's story source, `Assets/Resources/docs/campaign-ww1-scenario.md`
(untracked, work in progress — it is the authority when the two disagree):

| # | Title | Date | Terrain | Daytime |
| --- | --- | --- | --- | --- |
| 1 | FIRST LIGHT | 14 Apr 1916 | `Verdun` | Morning |
| 2 | THE NUMBERS | 22 Jun 1916 | `Verdun` | Midday |
| 3 | FIXED GROUND | 12 Feb 1917 | `Verdun` | Evening |
| 4 | THE RAVEN | 6 Apr 1917 | `Flanders` | Morning |
| 5 | NOTHING BURNS AT NIGHT | 19 Sep 1917 | `Flanders` | Night |
| 6 | HOHRUPT | 3 Oct 1917 | `Dolomites` | Morning |
| 7 | TWO FIRES | 24 Mar 1918 | `Dolomites` | Midday |
| 8 | IRON BIRDS OF PREY | 15 May 1918 | `Dolomites` | Evening |

The scenario's `Mountain` sector is `TerrainKind.Dolomites`, the alpine streamer we already have
(docs/dolomites.md) — so all three terrain kinds are now flown in career and none is
custom-battle-only.

**Everything below the title is placeholder.** Each level has a script (`level1` … `level8`) so
that it can be *finished*, which is what career progression is built on — but every script is the
same scroller shape (opening exchange → objective → waves → closing exchange → `finish`), its radio
lines are lorem ipsum, and its briefing `lore` is lorem ipsum. The scenario designs levels 3, 5, 6
and 7 as fixed-width, stealth, strike and time-attack levels and 4 and 8 as boss fights; none of
those modes exist yet, so those levels currently fly as ordinary scrollers.

Difficulty is a straight ramp across the eight: `enemyHealthScale` 0.50 → 1.00,
`enemyRotationScale` 0.80 → 1.18 (multipliers on each enemy role's own base, docs/enemies.md), and
`flak` climbing to 1.5 at Hohrupt, where the scenario puts guns on both valley walls.

## Progress

`GameManager.CampaignLevelsCompleted` (PlayerPrefs `mr_campaign_progress`) is the highest
career level cleared, and `CampaignProgress` in `CampaignRun.cs` is the null-safe facade the
menu reads: `IsCompleted(n)`, `IsUnlocked(n)` (`n <= completed + 1`), `NextLevel`. It is
written in `CampaignLevelController.CompleteLevel`, and **only outside a custom battle** — a
skirmish on a career map cannot advance the campaign.

It is deliberately a different key from `mr_highest_unlocked_level`, which belongs to the
fixed challenge levels (`Level1`/`Level2`) and is untouched by career.

Level 1 is always unlocked; every later card is locked until the one before it is cleared.
`continue` on the era page flies `CampaignProgress.NextLevel`, which is the first uncleared
level — and level 8 once the whole campaign is done, so `continue` replays the finale rather
than dead-ending.

**One scene serves every level.** `CampaignLevel1` is the only endless scene; the menu writes
the wanted level into the static `CampaignRun` before loading it, the same trick
`CustomBattle` already uses, and `CampaignLevelController` reads `CampaignRun.Level` in
`Start`. A ninth level is a registry entry and a card, not another scene file. (The
scene still carries an orphan serialized `levelNumber` from before this change; nothing reads
it.)

`CampaignTerrain` is now an abstract streamer with a concrete land per kind —
`VerdunTerrain`, `FlandersTerrain` and `DolomitesTerrain`. `CampaignTerrain.Begin(kind, …)`
switches over the kind. The base owns everything about *chunks* (the keep-window,
time-slicing, terrain object assembly, neighbour links, disposal); a subclass owns everything
about *ground* (its height scale and world Y offset, how a chunk's heights are filled, how it
is painted, what it decorates with, and what extra meshes ride along with it).

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
- The daytime is authored on the definition: level 1 flies at dawn (`Daytime.Morning`), and
  the eight spread over all four daytimes. Sky, fog and ambient reuse the same sky classes as
  the fixed terrain levels — `CoastSky` and `DolomitesSky` carry a palette per daytime, which
  is what lets levels 5 and 8 fly at night on the coast's and the alpine ground.
- A **custom battle** is the one exception. When `CustomBattle.Requested` is set (the menu's
  custom battle screen did it), the controller builds
  `CampaignLevels.Custom(map, daytime)` — the picked map's seed under the picked sky — in
  place of the authored definition. Career's start clears the request first, so the two
  entry points never bleed into each other. It flies no script, so its only enemies are the
  ones the dev console's spawn buttons launch (docs/dev-stats.md).

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
the bottom of the screen, timed pauses, enemy waves with their incoming warning, and the win
condition. `CampaignDefinition.enemyHealthScale` and `enemyRotationScale` scale what those waves
fly with, and the plane named in the wave decides whether it flies as a scout or a fighter
(docs/enemies.md). Level 1 runs
`level1` — six scouts in five waves, three cutscenes; level 2 and custom battles have none
and stay endless. See
docs/campaign-scripts.md for the file format, the dialogue bar, and how the enemy AI was
adapted to a forward-scrolling world, and docs/campaign-ww1-scenario.md for the WW1 era's
plot, cast, loading-screen text and every radio line (story only — none of it is wired up).
docs/campaign-ww1-portraits.md holds the avatar generation prompts for those speakers.

## Sky flak

`CampaignDefinition.flak` scales how heavily the level is shelled from the ground — `1` by
default on every level, `0` to fly a clean sky. The same field exists on the arena's
`LevelDefinition`. See docs/sky-flak.md.

## Supply drops

`CampaignDefinition.supplyDrops` lets a level parachute a health crate past the player when the
plane drops to `supplyHealthFraction` of its health. Level 1 sends one, worth 50 health; every
other level and the custom battle send none. See docs/supply-drops.md.

## The companion

A level can fly with a wingman: `CampaignDefinition.companion` turns it on and names the two
plane models involved. It holds formation while the film bars are up and peels 250 m back into
a background dogfight while the level is being played, swapping every time the cutscene state
does. See docs/companion.md.

## Pre-level briefing

`CampaignDefinition` also carries the briefing text — `title`, `dateline` and `lore` — shown on
a full-screen page before the level starts. Custom battles skip it. See docs/level-briefing.md.

## Shared pieces extracted in this change

- `PlaneFactory` — aircraft rig building (model orientation/mirroring, collider, propeller,
  muzzle mount), moved out of `LevelController` so both controllers spawn identical planes.
  The plane physics layer constant lives there too.
- `HealthBar` — the HUD health readout used by both level types, now built by the shared
  `LevelHud` along with the action column and the hint line (docs/hud.md).
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
names from a `PlaneModelConfig` (`PlaneModels.Albatros` / `.Sopwith`) rather than baking them in
as constants — different FBX exports can sit at different rest orientations and node names, so
a differently-exported or brand-new model is a new registry entry, not a code change. The same
config drives both the upright player and the mirrored enemy; the mirror-specific handling
(skipping the wheels-down roll, since the enemy's own ~180° heading spin already flips it
belly-down) stays in `BuildPlaneModel` rather than in the per-plane data.

### The nose trim

`BuildPlaneModel` pitches every model `ModelPitchDeg` (−10°) nose-down inside its body, so a
plane in level flight reads as flying rather than hanging. That constant assumes the model was
built in a **parked** attitude — nose up on the tail skid, which is how a WW1 biplane sits on
the ground — and it is the −10° that cancels it back to level. Measured off the fuselage,
tail-centroid to nose-centroid:

| plane | built in at | + `ModelPitchDeg` | in flight |
| --- | --- | --- | --- |
| Sopwith Camel | +7.3° nose-up | −10° | −2.7° |
| Fokker Dr.I | +5.9° nose-up | −10° | −4.1° |
| Albatros D.III | −2.1° nose-**down** | −10° | −12.1° |

The Albatros was exported in a level flying attitude instead, so there was nothing for the
−10° to cancel and it flew visibly nose-down. `PlaneModelConfig.pitchTrimDeg` absorbs the
difference — it is added to `ModelPitchDeg` before the mirror flip, so it corrects the enemy
build too — and the Albatros carries `+9.4°`, which lands it on the Camel's −2.7°.

The **garage is unaffected** by any of this: `GaragePlaneView.SolveRestingPitch` measures the
built model's contact points and pitches the *body* to stand them on the ground, so a change
to the model's own pitch is cancelled out by the solve. The trim is a flight-attitude
correction only.
