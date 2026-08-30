# Clouds (`Assets/Scripts/CloudSystem.cs`)

The drifting cloud field for the terrain levels — **three layers at three depths**, straddling
the play plane. Entirely code-built at runtime like the rest of the game: no prefabs, meshes or
materials in the project, no colliders and no shadows anywhere in the effect — it can never
touch gameplay.

## Where it runs

`CloudSystem.Begin(cam, daytime, weather, cloudsPart, playPlaneZ)` is called from
`LevelController` and `CampaignLevelController` at the end of camera setup whenever the
level's definition carries a non-null `CloudsPart`. Currently that is fixed Level 1 (Verdun),
campaign Level 1 (Verdun) and campaign Level 2 (Flanders Coast); the fixed Level 2 stays
cloudless (`clouds = null`) even though it now flies the same Verdun terrain.

A second `Begin` overload takes the **tint and glow colours directly** instead of a
`Daytime`. The daytime form computes them from the four inland sky classes and delegates to
it; Flanders Coast passes `CoastSky`'s own colours the same way, so a map with its own
atmosphere does not need a new branch inside the cloud system
(docs/flanders-coast.md).
`weather` is the same future modulation seam the sky classes take — `Calm` changes nothing.

## Structure

Each cloud is a root GameObject carrying 5–9 blobs taken from the shared `BlobMesh.Pick()`
pool (the explosion's mesh family — see `docs/effects.md`). Clouds spawn and scroll off
continuously, so they take pooled variants rather than building a mesh per blob, and
`DestroyCloud` releases only the material and the root. Cloud-like shapes come from the
transforms, not new geometry:

- blob offsets spread mostly along X (±0.5 × width) with a Y band (±0.22) and a slight Z
  scatter (±0.08), so the cluster reads as a wide but bulky puff;
- each blob is stretched horizontally (X/Z ≈ 1.1–1.7× of its base scale) but keeps most of
  its height (Y ≈ 0.8–1.15×) and is yawed randomly — yaw only, so the stretch stays
  horizontal;
- every blob slowly hovers around its base offset on X/Y (sine drift, random amplitude
  ≈ 4–12 % of cloud width, periods ~7–18 s, random phases), so blobs slide over each other
  and the cloud's silhouette keeps morphing.

All blobs of one cloud share one transparent URP Lit material (smoothness 0, no shadows
cast or received). Alpha is 0.5 ± ~12 % per cloud, times its layer's fade (below). Base colour is tinted per daytime and
then shaded by the level's actual sun/ambient light, so the same tints darken naturally at
night:

| Daytime | Tint |
|---------|------|
| Morning | warm cream (0.97, 0.92, 0.84) |
| Midday  | white (0.97, 0.98, 1.00) |
| Evening | apricot (0.98, 0.80, 0.66) |
| Night   | moonlit slate (0.62, 0.66, 0.82) |

Each material also carries an emissive lift: a fraction of the active sky's `HazeColor` —
the same colour the fog and horizon band are — scaled per daytime (`HazeGlow`, roughly
0.2–0.55, highest at night). This is what keeps a cloud's shaded side reading as air rather
than falling to flat grey under the key light and ambient alone.

## Depth layers

Clouds used to sit in one slab at the play plane's depth ± 60 m, which is barely a tenth of the
camera's distance to it — every cloud was effectively the same size, at the same place, and the
sky read flat. There are now **three layers**, placed as fractions of the camera-to-play-plane
distance (`LayerDepth`, so the numbers hold whatever a level's camera distance is) and each with
its own conveyor:

| Layer | Depth | Distance vs the play plane | Where it sits |
| --- | --- | --- | --- |
| near | −0.15 × | 0.85 × | In front of the plane — planes fly *behind* these. |
| mid | +0.50 × | 1.50 × | Behind the plane. |
| far | +1.25 × | 2.25 × | Well behind, still in front of the Dolomites ridge line at z 700. |

Every cloud also gets ± 0.08 × the same distance of **jitter** on top of its layer's depth, so a
layer is a band rather than a plane and no two clouds share a Z.

Depth is paid for in three places, all driven by the layer's distance ratio `r`:

- **size** scales by `√r`, so a far cloud is bigger in metres but still reads *smaller* on
  screen (0.67 × at the far layer). Full compensation would have cancelled the main depth cue;
  none at all turns the far layer into specks.
- **altitude** is placed as `camY + (band − camY) × √r`, which pulls the far layer's band toward
  the camera's own eye line — the horizon — by the same 0.67. That is the aerial-perspective
  squeeze you see in a real sky.
- **alpha** is multiplied by `LayerFade` (1 / 0.85 / 0.7). The depth fog in `AerialHaze` cannot
  do this for us: clouds are transparent and do not write depth, so the haze pass never sees
  them.

**Parallax comes free.** Every layer drifts right to left at the same world speed (± 15 % per
cloud), so a far cloud crosses the screen at 1/2.25 of the near one's rate purely through
perspective — no per-layer speed to keep in step with the spawner.

## Presets

`CloudsPart` (defined with the other level parts in `LevelDefinition.cs`) holds three
`CloudLevel` values — `Low` / `Medium` / `High`, `Medium` being the default for all:

| Parameter | Low | Medium | High | Meaning |
|-----------|-----|--------|------|---------|
| `speed` | 6 | 12 | 24 | m/s leftward drift |
| `frequency` | 440 | 250 | 135 | average metres between clouds **per layer**, before the layer's distance ratio widens it (lower = more clouds) |
| `size` | 45 | 80 | 130 | nominal cloud width in metres (± 30–40 % per cloud) |

## Spawning: the conveyor trick

All clouds drift left at (nearly) the same speed, so the whole field is static in
"conveyor space" `u = x + speed × t`. Each layer keeps its own cursor, `nextSpawnU`: whenever
that layer's window right edge (view edge + 300 m margin, both measured at the layer's farthest
depth so margins cover every cloud) passes the cursor, a cloud spawns there and the cursor
advances by `frequency × r × random(0.55–1.45)`. Scaling the step by the distance ratio is what
keeps the *apparent* spacing the same in all three layers while the far window is more than twice
as wide in metres. This one rule handles everything —
pre-populating the first frame's whole window, drift feeding clouds in from the right on
the fixed levels, and the ratcheting campaign camera revealing clouds ahead no matter how
fast the player flies — with no pop-in, since the margin exceeds any cloud's half-width.
Clouds crossing their own layer's left edge are destroyed (mesh + material released, as in the
explosion).

Three layers at a slightly tighter spacing put roughly **four times** as many clouds in the sky
as the single slab did — about 17 alive at `Medium` against 5 — which is the one number to pull
back on (`Spacing`, or a layer out of `LayerDepth`) if a device struggles: they are transparent,
overlapping and unbatched, so they are paid for in overdraw.
