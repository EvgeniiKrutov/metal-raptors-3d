# Clouds (`Assets/Scripts/CloudSystem.cs`)

The drifting cloud layer for the terrain levels — a single layer at the play plane's
depth. Entirely code-built at runtime like the rest of the game: no prefabs, meshes or
materials in the project, no colliders and no shadows anywhere in the effect — it can never
touch gameplay.

## Where it runs

`CloudSystem.Begin(cam, daytime, weather, cloudsPart, playPlaneZ)` is called from
`LevelController` and `CampaignLevelController` at the end of camera setup whenever the
level's definition carries a non-null `CloudsPart`. Currently that is fixed Level 1 (Verdun)
and campaign Level 1; the FlatSlab placeholder level stays cloudless (`clouds = null`).
`weather` is the same future modulation seam the sky classes take — `Calm` changes nothing.

## Structure

Each cloud is a root GameObject carrying 5–9 blobs built from the shared `BlobMesh.Build()`
icosphere (the explosion's mesh family — see `docs/effects.md`). Cloud-like shapes come from
the transforms, not new geometry:

- blob offsets spread mostly along X (±0.5 × width) with a Y band (±0.22) and a slight Z
  scatter (±0.08), so the cluster reads as a wide but bulky puff;
- each blob is stretched horizontally (X/Z ≈ 1.1–1.7× of its base scale) but keeps most of
  its height (Y ≈ 0.8–1.15×) and is yawed randomly — yaw only, so the stretch stays
  horizontal;
- every blob slowly hovers around its base offset on X/Y (sine drift, random amplitude
  ≈ 4–12 % of cloud width, periods ~7–18 s, random phases), so blobs slide over each other
  and the cloud's silhouette keeps morphing.

All blobs of one cloud share one transparent URP Lit material (smoothness 0, no shadows
cast or received). Alpha is 0.5 ± ~12 % per cloud. Base colour is tinted per daytime and
then shaded by the level's actual sun/ambient light, so the same tints darken naturally at
night:

| Daytime | Tint |
|---------|------|
| Morning | warm cream (0.97, 0.92, 0.84) |
| Midday  | white (0.97, 0.98, 1.00) |
| Evening | apricot (0.98, 0.80, 0.66) |
| Night   | moonlit slate (0.62, 0.66, 0.82) |

## Placement and motion

Clouds occupy the 350–850 m altitude band and sit on the play plane's depth ± 60 m
(`DepthSpread`) — at the plane's level and directly behind it, so planes visibly pass both
in front of and behind them. The whole layer drifts right to left at the preset speed
(± 15 % per cloud).

## Presets

`CloudsPart` (defined with the other level parts in `LevelDefinition.cs`) holds three
`CloudLevel` values — `Low` / `Medium` / `High`, `Medium` being the default for all:

| Parameter | Low | Medium | High | Meaning |
|-----------|-----|--------|------|---------|
| `speed` | 6 | 12 | 24 | m/s leftward drift |
| `frequency` | 520 | 300 | 160 | average metres between clouds (lower = more clouds) |
| `size` | 45 | 80 | 130 | nominal cloud width in metres (± 30–40 % per cloud) |

## Spawning: the conveyor trick

All clouds drift left at (nearly) the same speed, so the whole field is static in
"conveyor space" `u = x + speed × t`. The system keeps one cursor, `_nextSpawnU`: whenever
the camera window's right edge (view edge + 300 m margin, measured at the layer's farthest
depth so margins cover every cloud) passes the cursor, a cloud spawns there and the cursor
advances by `frequency × random(0.55–1.45)`. This one rule handles everything —
pre-populating the first frame's whole window, drift feeding clouds in from the right on
the fixed levels, and the ratcheting campaign camera revealing clouds ahead no matter how
fast the player flies — with no pop-in, since the margin exceeds any cloud's half-width.
Clouds crossing the window's left edge are destroyed (mesh + material released, as in the
explosion).
