# Battlefield life

Ambient life on the Verdun ground: random shell/mine blasts, burning smoke
columns, small squads of infantry crossing the mud, and the dead trees and burned
houses standing in it. Everything is built in code at runtime. Only the scenery
props carry colliders — blasts, smoke and infantry have none and can never damage
the player or soak a bullet. The gameplay-shaped interactions are that a ground
blast wipes out any figures standing in it, and that a plane which clips a tree
or a house scrapes itself on it.

`Battlefield` (`Assets/Scripts/Battlefield.cs`) is the coordinator. Both level
controllers start it once the camera exists:

- `LevelController` — only when `VerdunLand`, seeded from `_level.terrain.seed`,
  and passing the level's `MinX`/`MaxX`. The flat-slab terrain gets nothing.
- `CampaignLevelController` — always, seeded from `_level.seed`, with no bounds.

`Begin` needs the camera's half view width because everything streams around the
camera, and needs the map bounds because bounded and endless maps populate
themselves differently (see *Bounded maps vs scrollers* below). Both controllers
now keep the half view width in a `_halfViewWidth` field
(`LevelController.RandomEnemySpawn` reads the same field instead of recomputing
it). Passing no bounds means `±infinity`, which is what `Bounded` tests.

## Crater lookup

`Begin` also takes a `Func<float, float, bool> inCrater` — a world-space
"is this point inside a crater" test, exposed as `Battlefield.InCrater` and used
by the scenery to keep trees and houses out of the shell holes. Each terrain
supplies its own, because each knows its craters differently:

- `ProceduralTerrain.Build` now **returns** the test. It closes over the crater
  list it generated (`x`, `z`, bare radius) and maps world X back into the tile's
  local X with `Mathf.Repeat(x + width / 2, width)`, so the test follows the
  arena's three side-by-side copies of the same heightmap.
- `CampaignTerrain.InCrater(worldX, z)` re-derives the craters for the cells
  within `MaxCraterReach` of the point from the same `(seed, cell)` hashes used
  when stamping them, into a reusable scratch list. Nothing is cached per chunk,
  so the answer is the same whether or not that ground is currently streamed in.

Both use the same `zEff = max(z, FrontStrip)` rule as the heightmap stamping, so
a crater's exclusion zone reaches to the front edge exactly where its bowl does.
The radius is `CraterBareRadii` (1.35 × the crater radius) — the same ring the
terrain already keeps clear of grass, so a crater reads as one clean scar.

## Ground sampling

Nothing here knows how the terrain was generated — it only asks
`Battlefield.SampleGround(x, z, out y)`, which walks the live `Terrain` list
(refreshed every `LateUpdate` via `Terrain.GetActiveTerrains`), finds the tile
whose XZ footprint contains the point, and returns
`SampleHeight + terrain.transform.position.y`.

That is what lets one system serve both Verdun surfaces: the arena's three
static tiles from `ProceduralTerrain.Build` and the campaign's streamed
`CampaignTerrain` chunks look identical through this call. When the answer is
"no terrain here" (a chunk not streamed in yet, or past the arena's tiled edge)
the caller simply skips that spawn and retries on a later frame, so nothing ever
appears floating in the void.

`Battlefield` drives its subsystems from its own `LateUpdate`, so it may run one
frame behind the controller's camera move. The camera smooth-follows anyway, so
the lag is invisible; everything is `Time.deltaTime`-based, which means the pause
menu's `Time.timeScale = 0` freezes the whole battlefield along with the game.

## Depth bands

The terrain is 800 deep and the planes fly at `z = 100`. The four subsystems
deliberately occupy different slices of it:

| System | Z range | Why |
| --- | --- | --- |
| Ground blasts | 15 – 700 | The whole map, foreground included — shells land under and in front of the plane as well as behind it. |
| Smoke columns | 140 – 380 | Just behind the play plane, close enough to read as part of the scene rather than horizon decoration. |
| People | 40 – 700 | The whole map, so squads are seen both in front of and behind the aircraft. |
| Scenery props | 20 – 700 | The whole map. Reaching in front of the play plane is deliberate: near trees sweep past the camera for parallax, and the handful that land in the flight lane are the ones a plane can hit. |

## Ground blasts (`GroundBlast.cs`)

One blast every 1.8–3.2 s at a random X across the visible strip
(`±1.15 × halfViewWidth` around the camera, so the whole terrain width gets
shelled as the camera travels) and a random Z from the band above. Blast `size`
is 45–90 world units and perspective does the rest — the same effect reads as a
big near burst or a distant thud depending on its depth. Blast X is never
clamped to the map bounds: the arena tiles its terrain sideways, so the ground
past `MinX`/`MaxX` is visible and must keep getting shelled too.

Each blast is three layers on one self-destructing root:

- **Flash** — a single emissive sphere half-buried at the impact point, hot
  `(1, 0.82, 0.45)`, alive 0.16 s, shrinking to 20 % while its emission fades.
- **Clods** — 7–12 dirt lumps punched almost straight up (a narrow 0.45 cone) at
  60–145 u/s, tumbling, pulled back down at 110 u/s². They hold full size until
  70 % of their 1.1–2.1 s life and then shrink away, which is what stops them
  sinking through the ground on the way back down instead of needing a real
  ground test. The lumps reuse `BlobMesh.Build()` (the same faceted rock shape as
  `Explosion` and the clouds), but from a **static pool of six pre-built meshes
  and one shared material** — a blast every couple of seconds forever would
  otherwise churn meshes and materials continuously.
- **Dust** — 5–8 transparent brown cubes drifting outward and up at 16–32 u/s,
  growing 2.6× and fading out over 1.8–3.2 s. These do need per-piece materials
  (each one is at a different point in its own alpha ramp) and are released in
  `OnDestroy`.

### Scaling a blast

Sizes, offsets and the dust's outward drift are all plain multiples of `size`,
but speeds cannot be: launch velocity is what sets how *high* debris goes, and
under a fixed 110 u/s² gravity an apex grows with the square of the speed. So the
clod and dust rise speeds — and the clod lifetime, which has to cover the longer
flight — are scaled by `SizeBoost = √(size / 40)`. That makes the throw height
grow in step with the blast's own width, and keeps the whole thing self-similar
whatever the size range is retuned to. Scaling the speeds linearly instead would
send clods five times higher at the top of the range.

`Battlefield` also kills any infantry within `size × BlastKillRadii` of the
impact. That factor is **1.0**, so the lethal circle is the blast's own footprint
rather than something wider than what you can see — at these sizes a direct hit
still takes most of a squad.

### Sound

Deliberately faint, and often silent. Volume is `0.2 × (1 − t)` where `t` ramps
the camera distance from 430 to 1250 units, and anything under 0.025 is not
played at all — so far-background blasts are purely visual and only the near ones
thump. Pitch is randomised low (0.5–0.8) so the shared
`Resources/Sounds/explosion_1..3` clips read as distant artillery rather than a
plane blowing up. Like `Explosion`, playback is 2D (`spatialBlend = 0`) from a
throwaway carrier object, because 3D rolloff would mute everything at the
camera's ~420 m standoff; the manual distance curve above replaces it.

## Smoke columns (`SmokeColumn.cs`)

Permanent burning sites, independent of the blasts. The X axis is divided into
600-unit cells; each cell deterministically hashes (seed, cell) into a 75 %
chance of holding a site, at a random X inside the cell and a Z in the band
above. Because the position comes from the hash and not from a running RNG, a
site is in the same place every time you pass it, which matters for the campaign
where chunks stream in and out.

Sites are created and destroyed as the camera moves, keeping a window of
`halfViewWidth + 500`. A cell that hashes to "no site" is cached as such; a cell
whose ground is not yet streamed is left undecided and retried next frame.

Each column emits a cube puff every 0.55 s that rises at 19–28 u/s, drifts
downwind (+X at ~7 u/s, jittered), tumbles slowly, grows 4.5× and fades out over
a 13 s life — about 23 live puffs making a column roughly 300 units tall. Puffs
start at 12–18 units and scatter ±5 around the base, so the plume is wide from
the ground up rather than a thin thread that only spreads near the top; by the
time one fades it is around 65 units across. Height and thickness come from the
rise speed and the life together: the life sets how long a puff keeps climbing,
so lengthening it raises the top and adds live puffs to fill the new span.

Puff and ember sizes are tuned for the near band: at `z ≈ 140–380` a column is
half the camera distance it would be at the horizon and therefore reads about
twice as large, so the source sizes are smaller than they would need to be
further back. A pulsing emissive slab sits at the base as the fire itself, which
is most of what you see of a column at night.

Columns are **pre-warmed** on creation: `Prewarm` emits the full set of puffs at
staggered ages and advances each one to that age, so a column enters the view
already at full height. Without it you would watch every column grow from the
ground as you flew past — the 500-unit lead-in is only a few seconds at flight
speed, nowhere near the 13 s a column needs to build itself. `Prewarm` derives
its puff count from `PuffLife / EmitInterval`, so it follows any retune of those
two on its own.

## Scenery props (`BattlefieldProps.cs`)

Dead trees and burned-out houses, the only solid things on the battlefield. The
models are the 12 FBX files in `Resources/trees` and the 6 in
`Resources/burned_houses`, each with a single flat Phong material — like the
plane models they live under `Assets/Resources`, which the repository does not
track.

### Placement

Two independent deterministic grids along X, built the same way as the smoke
columns: a hash of `(seed, cell, salt)` seeds a `System.Random` that picks the
object's X inside its cell, its Z, which model, its yaw and its size. Nothing is
stored between passes, so an object streamed out and back in returns to exactly
the same spot, and a level replays identically.

| Grid | Cell | Result |
| --- | --- | --- |
| Trees | 58 | ~12 on screen |
| Houses | 620 | ~1 on screen |

**Houses are updated first**, because a tree candidate that falls inside a house
footprint is dropped — the one overlap the two grids can produce on their own.

A candidate is refused, and its cell remembered as empty, when it is inside a
crater (bowl + rim, via `Battlefield.InCrater`) or standing on ground steeper
than 35°, measured from two extra `SampleGround` calls 6 units along X and Z. A
candidate whose ground is not streamed in yet is left **undecided** — no entry is
written, so it is retried next frame. Cells are created and destroyed with a
window of `halfViewWidth + 500` around the camera, on bounded and endless maps
alike: on the arena the deterministic hash rebuilds an identical object when the
camera comes back, so there is no reason for a second code path.

### Standing them up

The FBX files are authored **Z-up** — a trunk runs from `z = 0` at the roots to
`z ≈ 5` at the crown, and the export carries a −90° X rotation on its root node to
turn that into Unity's Y-up. Every prop therefore gets
`StandUp = Quaternion.Euler(-90, 0, 0)` as its local rotation, which maps the
model's +Z onto world +Y. Leaving the rotation at identity instead lays the model
flat on the ground and swings roughly half its geometry below the pivot — trees
disappear underground and houses sink to the windows. (The plane models need the
same correction; `PlaneFactory` folds it into each `PlaneModelConfig.standUpEuler`.)

The random yaw goes on the **parent** root, so `rootYaw × StandUp` spins a
standing model about the world Y axis.

### Scale, seating and colliders

The models are authored in metres (a tree is about 5 m, a house 7.5 m wide) and
the game runs at roughly **7.2 units per metre** — a 13-unit soldier is 1.8 m and
a 60-unit plane is an 8.5 m wingspan. A prop's root is scaled by `MetreScale`
7.2, then by an oversize factor of **1.5** (`TreeOversize` / `HouseOversize`,
tunable per kind), then by ±25 % per-instance jitter — which keeps the models'
relative sizes. Trees land around 45–65 units tall, near the plane's own 60-unit
length; houses around 70–90 wide. Deliberately larger than life, so they read at
the camera's 420-unit standoff.

Trees get one extra factor on top: a **depth boost** of
`1 + InverseLerp(200, 700, z) × 0.5 × rand`. It is zero in front of `z = 200` and
reaches at most +50 % at the back of the map, with the amount drawn per tree — so
the far band gets a mix of ordinary and noticeably taller trees rather than a
uniformly scaled-up row. Perspective shrinks a tree at `z = 700` to roughly half
the on-screen size it has in the flight lane, and this puts some of that back
without touching the trees the player actually flies through. `MaxPropRadius`
(the lookup reach used by `Nearest`) is 75 to cover the widest boosted tree.

Bounds are measured once per model and cached: the model is instantiated 5000
units below the world **with `StandUp` already applied**, its renderer bounds are
combined, and the probe is destroyed. The stored bounds are then shifted so
`min.y` is zero and the shift is kept as the view's local offset, which puts the
model's lowest point exactly on the root's origin whatever its pivot happens to
be. That is what makes a prop stand *on* the terrain rather than through it, and
it is why the same bounds can be handed straight to the collider.

Colliders are deliberately generous, one per object on the scaled root: a
Y-capsule enclosing trunk *and* branches for a tree, a box around the walls for a
house. Clipping a branch or a doorway counts as a hit; nothing can slip through a
collapsed wall.

### Scraping a prop

Both colliders are **triggers**, so a prop never blocks or deflects an aircraft —
a plane flies through it and comes out the other side, exactly as it does when it
scrapes an enemy. Hitting one is not a crash:

- `CubeController.OnTriggerEnter` calls the existing `Scrape()` — 10 damage out
  of the player's 100, the model's `ShakeEffect`, a burst of `Sparks` and a
  camera shake, all behind `Scrape`'s own 0.5 s cooldown.
- `EnemyController.OnTriggerEnter` does the same through its own `Scrape()`
  (damage and shake; sparks are a player-only flourish here, as they already are
  for plane-on-plane scrapes).

The camera shake is not the prop system's own doing: a successful
`CubeController.Scrape()` raises `OnScraped`, and both level controllers set
`_camShake = 1f` from it (7-unit jitter decaying over 0.3 s). That is the same
shake `LevelController` already ran for plane-on-plane scrapes — it used to be
driven from `CheckPlaneScrapes` by hand, and now comes from the event for both
causes, so a tree, a house or an enemy all shake the view identically.
`CampaignLevelController` had no camera shake at all before and now carries the
same state and decay.

Both handlers gate on `other.gameObject.layer == BattlefieldProps.Layer`, so
nothing else in the scene can drive them. The trigger fires on the aircraft's
collider, which sits on a child object, and Unity forwards the message up to the
GameObject holding the rigidbody — the same path the existing `OnCollisionEnter`
already relies on.

The explosion path is untouched and belongs to the terrain alone: flying into the
ground still ends the level. Props are triggers, so they raise no collision event
at all and can never reach it.

Props sit on **layer 9**, and `Begin` calls
`Physics.IgnoreLayerCollision(9, 0, true)` once. Layer 0 is where the bullets
are, so shots pass straight through trees and houses, the same as they pass
through infantry. Plane colliders are on layer 8 (`PlaneFactory.PlaneLayer`),
which still meets 9 — the layer matrix gates trigger events just as it gates
collisions.

The prop itself is never damaged: it does not fall, burn or disappear, and ground
blasts leave it standing. Enemy AI does not steer around props either — an enemy
that dives low through a tree takes the same 10-point scrape the player would.

## People (`BattlefieldPeople.cs`)

### The figure

13 units tall — roughly 1.8 m against the 60-unit planes — built from three
stacked boxes, top to bottom: **hat** (uniform colour), **face** (skin), **body**
(uniform colour). Uniform is one of two: French horizon blue `(0.40, 0.47, 0.56)`
or German feldgrau `(0.36, 0.39, 0.32)`. Skin is picked per figure from three
tones.

**A squad is always a single uniform colour**, and `PickFaction` hands each new
squad the colour that is currently in the minority among live squads, so the two
sides stay evenly represented and neighbouring squads read as distinct units
rather than one indistinguishable mass. The two sides share the terrain and
ignore each other — there is no combat between them.

All five materials (2 uniform + 3 skin) are **static and shared**, so every
figure in the level costs five materials in total and they batch together. Shadow
casting is off on every segment: at this size the shadows would not read, and the
extra casters would not pay for themselves.

Figures keep a **fixed orientation** and never yaw to face their heading. A
3-unit-thick slab turned side-on to the camera all but disappears, and since
figures wander freely in XZ they would spend much of their time doing exactly
that. Holding them broadside keeps the "rectangle figure" reading from the only
angle that matters.

### Movement

Each figure moves **independently** — its own heading, its own 6–11 u/s speed,
its own walk/halt timer, its own Perlin wander phase — so a squad fans out and
crosses in different directions instead of sliding around as one rigid block. A
figure walks 1.5–4.5 s, halts 0.4–1.8 s, and takes a random ±90° turn each time
it resumes; while walking its heading drifts on its own noise channel (±40°/s).

What holds a squad together is a **leash**, not a formation: the squad has its own
centre point that drifts slowly (4 u/s, its own longer walk/halt cycle), and any
figure more than 42 units from that centre steers back toward it at 220°/s until
it is inside again. Inside the leash a figure is completely free. The centre's
slow drift is what makes a squad migrate across the map at all — figures alone
would only mill about.

Both the centre and the individual figures are confined to the Z band and, on
bounded maps, to the populated X band; hitting a wall clamps the position and
reflects the heading (`-θ` off a Z wall, `180° - θ` off an X wall).

### Going around the scenery

Figures never walk through a tree or a house. `Deflect` asks
`BattlefieldProps.Blocks` for the nearest prop whose radius plus a clearance
contains the walker, and turns the heading away from that prop's centre at
300°/s. It is pure steering — no position clamp — so a squad flows around an
obstacle instead of sticking to it, and the wide clearance (18 units for a
figure, 34 for a squad centre) starts the turn before anything touches. It runs
**after** the leash, so a figure whose squad centre is behind a house goes around
the house rather than into it, and the squad centre itself is deflected too, so
squads do not settle on top of one. `SpawnGroup` also refuses a centre that would
start inside a prop and retries on a later frame, the same as when the ground is
not streamed in yet.

The lookup is cheap because the props are already in X-keyed grids: `Blocks`
only visits the cells within `MaxPropRadius + clearance` of the walker, which is
one or two cells per grid.

The step is sold entirely by a vertical hop: `|sin(π · (t · rate + phase))| × 0.9`
units while moving, zero while halted, with a per-figure phase offset so nobody
bobs in sync. The rate scales with the figure's own speed, so faster figures
visibly step faster.

Every figure samples its own ground height each frame, so a squad splays
naturally over the crater rims it walks across.

### Bounded maps vs scrollers

Squad count comes from a fixed spacing (one squad per 235 units, capped at 18),
so roughly three squads are visible at any time on either kind of map.

**Bounded maps** (`LevelController`, width 2000) are populated across their whole
extent plus a half-view-width of padding on each side — the arena tiles its
terrain sideways, so the ground past `MinX`/`MaxX` is on screen at the edges and
would otherwise be visibly empty. The initial squads are spread evenly across
that band with jitter. Squads are **never culled by camera distance** here; they
live for the whole level and wander. The only way one disappears is by losing its
last figure to a blast, and its replacement is placed at a random spot in the
band that is **outside the camera view** (a lead of 80 units past the view edge,
picking between the off-screen segments to the left and right in proportion to
their length), so squads never pop into existence in front of the player.

**Scroller maps** (`CampaignLevelController`) keep a window around the camera
instead, and new squads always enter **from the right**. The campaign camera can
only ever move right (`PositionCamera` clamps it with `Mathf.Max`), so a squad
spawned to the left would be behind the player forever and culled without ever
being seen. Squads are culled once they fall a full `halfViewWidth + 460` behind
the camera; the cull bound ahead of the camera is twice that, purely as a
backstop for a squad that wanders forward while the camera is stationary.

### Casualties

After spawning a blast, `Battlefield` calls `KillWithin(position, size × 1.0)`.
Any figure inside that XZ radius (45–90 units, comparable to a squad's own
spread, so a direct hit takes most of a squad) is destroyed outright — no
ragdoll, no corpse; the blast's own dirt and dust cover the moment. A squad that
loses its last figure is removed and replaced by the rules above. Bullets pass
through figures entirely.
