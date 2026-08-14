# Dolomites

The third endless map: an Italian-front alpine valley — a green pasture floor scattered with
shell holes, with a wall of dark low-poly mountains standing across the back. Custom
battle only (the third entry in the map selector); no career level uses it. Everything is built
at runtime like the rest of the game — no meshes, materials or scenes in the project.

```
z = 0    ────────────────────────────────  front edge (cut wall)
              green meadow — shell craters scattered here and there, nothing else cut into it
z = 100  · · · · ·  FLIGHT LANE  · · · · ·
z = 400      the floor starts a gentle 30-unit rise toward the back
z ≈ 740      ◣◤◣  ridge A rises straight out of the pasture (crests 360-650)
z ≈ 950      ◢◣◤  ridge B (crests 480-760)
z ≈ 1180     ◤◢◣  ridge C (crests 620-900), the palest
              the valley mist thickens toward the back, burying the ridges' feet
             ───────  EYE LEVEL — the sky's horizon band sits behind all of it
```

**The mountains are not terrain.** The Unity `Terrain` chunks stay a flat-ish green valley from
the front edge to their back edge at `z = 800`; every mountain is a separate low-poly mesh. An
earlier version climbed the chunks' far band into textured foothills and it was the wrong call:
splat blending, grass billboards and heightmap normals on a near-vertical slope all break down
at that angle, and the seam between terrain rock and mesh rock was visible. There is nothing to
break now — the ridges simply stand behind a flat meadow.

**There is no water anywhere on this map**, the same as Verdun: no sea mesh, no sea level, no
ditching path.

## Where the pieces live

| File | Role |
| --- | --- |
| `CampaignTerrain.cs` | Abstract chunk streamer shared with Verdun and Flanders (docs/campaign.md). |
| `DolomitesTerrain.cs` | The valley floor: heightmap, craters, painting and grass. |
| `MountainRange.cs` | The three background ridges — camera-followed, streamed in chunk-aligned cells. |
| `DolomitesSky.cs` | The four alpine daytimes, their fog reach and valley mist, and the mountains' three colours. |
| `TerrainSurfaces.cs` | Terrain layer / flat material helpers, shared with Flanders. |

## The valley floor

Verdun's height scale and clamp exactly (`ProceduralTerrain.HeightScale` 90, `[4, 85]`) — this
is an ordinary inland surface again now that it carries no mountains.

- **Meadow** — `BaseLevel` (30) plus Verdun's octave stack at roughly half amplitude: ±7 at
  1100, ±4 at 470, ±2 at 190 along X, a ±6 2-D patch octave at 210, and ±1.2 of grain at 34.
  Gently rolling pasture rather than Verdun's chewed-up ground. The front strip rule is
  Verdun's: below `FrontStrip` (130) the profile is held constant in Z so the cut wall reads
  cleanly.
- **Back rise** — a further **30 units** smoothstepped in between `z = 400` and the back edge.
  Enough to keep the far pasture from reading as a flat table, far too little to hold rock or
  to fight the ridges standing in front of it.

### Craters, and nothing else

**Shell holes are the only thing cut into this floor.** Two earlier passes put linear features
on it — first three zigzag trench lines running along X, then cart ruts running into the screen
along Z — and both are gone. Long thin cuts read as *drawn lines* across a smooth green field
from a camera 420 units up, not as ground: whatever their profile, the eye picks up the
continuous edge, and a pasture covered in them looked ruled rather than grazed. The pasture is
plain now, and the craters are what interrupt it.

They are Verdun's craters at roughly a quarter of the density — **0.6 shells and 0.13 mines per
128-unit cell** against Verdun's 2.2 / 0.45, so about one hole every 175 units of flight rather
than one every 50 — spread uniformly over `z` 20–620 and stamped with Verdun's `zEff`
front-strip rule so a crater near the front edge reaches it exactly as its bowl does. Scattered
here and there, which is the point: a few shell holes in an otherwise intact alpine meadow.

### Painting and grass

Two terrain layers: meadow green `(0.34, 0.50, 0.24)` with grain, and flat bare earth
`(0.40, 0.32, 0.23)`. The alphamap is just `earth = scar(x, z)` — the crater rings — with the
meadow taking the rest. There is no rock layer at all any more; nothing on the terrain is high
or steep enough to want one, and its steepness rule was what used to paint crater walls grey.

Grass is the shared billboard system (docs/campaign.md) retinted per map through the
`ProceduralTerrain.SetupGrassDetail` colour overload — a strong alpine green
`(0.36, 0.60, 0.26)` healthy / `(0.50, 0.62, 0.30)` dry, against Verdun's dry browns. A tuft is
refused on ground steeper than 30° or where the scar value exceeds 0.35, so crater bowls stay
bare earth.

## The mountains (`MountainRange`)

Three ridge layers standing behind the valley, each an opaque flat-shaded curtain running from
a buried skirt at `y = −60` up to a crest line that is a pure function of world X.

| Ridge | Skirt at z | Crest at z | Crest height |
| --- | --- | --- | --- |
| A | 700 | 980 ± 90 | 360 – 650 |
| B | 950 | 1180 ± 110 | 480 – 760 |
| C | 1180 | 1450 ± 130 | 620 – 900 |

Crest heights are squared ridged noise (`1 − |2n − 1|`, three octaves) at a different base
wavelength per layer, so the three read as separate ranges rather than three copies of one
silhouette. Ridged noise peaks where the underlying Perlin crosses its midpoint, which makes
crests and notches instead of round lumps; squaring spreads the distribution so the peaks are
not all at the same near-maximum height.

**Crest heights rise with distance** (A tops at a screen slope of ~0.31 from a typical camera,
B ~0.34, C ~0.37) and each layer's *minimum* is above the one in front. That is what keeps all
three visible: B shows through A's notches, C through B's, and C's minimum crest is high enough
that no sky can ever appear under the skyline.

### Low-poly on purpose

Columns are **`CellWidth / 12` (42.7 units) apart** — twelve facets across a 512-unit cell, so
the silhouette is visibly faceted at the ridges' 1000–1800 unit distance. The crest's **z**
wanders by ±90–130 through its own Perlin channel, which is what stops the curtain looking like
a cardboard flat: adjacent facets sit at different depths, so their normals differ and each
catches the key light on its own. That variation is the *only* thing modelling these surfaces —
see the colour below.

### Dark stone, green at the foot, pale summits

One shared `URP/Lit` material carrying a 1×96 vertical gradient texture. Vertex UVs map world Y
through `InverseLerp(20, 900)` — the ridges' **whole** height range, not just their lower slopes
— which is what lets a band mean a specific altitude. Three stops:

| Band | World Y | Colour |
| --- | --- | --- |
| Foot | 20 – 75 | slope green, matching the pasture |
| Body | 145 – 520 | **dark stone**, one flat colour: `(0.45, 0.46, 0.49)` at midday, down to `(0.24, 0.27, 0.34)` at night |
| Summits | 520 – 820 | pale rock: `(0.92, 0.93, 0.94)` at midday, warm `(0.94, 0.82, 0.74)` at evening |

The body is deliberately dark. These ridges are lit by one directional light with no texture and
no ambient occlusion, so a pale stone flattens into a single bright shape at distance; a dark
body gives the flat-shaded facets somewhere to vary, and the fog then lifts each ridge by its own
distance instead of starting them all near white.

**Only the tops go pale**, and the threshold is absolute height rather than a fraction of each
ridge — so the highest summits of every range catch it while their lower crests stay dark stone.
Ridge A tops out at 650 and only its tallest peaks reach into the band; ridge C at 900 is mostly
in it. That reads the way a snow or bright-limestone line does, and it needs no extra geometry:
the band cuts across the curtain's faces at the right world height because the face's own Y
varies linearly between its skirt and its crest.

The green foot is not decoration either: it hides the line where a ridge cuts through the flat
meadow. Ridge A's curtain crosses ground level at `z ≈ 740`, ahead of the terrain's own back edge
at 800, so the mountain grows out of the grass and the map's far edge is never visible.

Ridges carry **no colliders and cast no shadows** — they are scenery well past the shadow
distance, and the plane can never reach them.

### Streaming

Cells are **`CampaignTerrain.ChunkLength` (512) wide and aligned to the terrain's own chunk
grid**, built from a deterministic function of world X sampled at fixed steps — 512 is a whole
number of columns, so chunk *k*'s last column and chunk *k+1*'s first evaluate the same X and
the ridges join invisibly.

They are streamed on their **own, wider window** than the terrain, and that is deliberate: the
mountains are 1000–1800 units further away than the land, and the frustum is far wider at that
depth. The keep distance is computed from the camera itself —
`(farCrestZ − camZ) · tan(fov/2) · aspect + one cell`, about 2300 — against the terrain's ~1550.
Sizing the ridges off the land's window would leave the frame's top corners showing sky. The
terrain keeps the shared window unchanged, because ridge A hides the ground from `z ≈ 740` back
and the visible land is well inside it.

## Sky (`DolomitesSky`)

Sunny northern Italy at altitude: clear air, a high sun, a deeply saturated zenith. Built the
same way as `CoastSky` — one class with a four-entry palette table, a `Custom/GradientSkybox`
material, linear fog matching the horizon band, a key light shining into `+Z`, and restrained
URP post FX (docs/atmospheres.md).

| Daytime | Haze | Zenith | Character |
| --- | --- | --- | --- |
| Morning | `(0.93, 0.88, 0.80)` | `(0.34, 0.52, 0.80)` | warm gold light on the peaks, valley still cool |
| Midday | `(0.82, 0.88, 0.94)` | `(0.17, 0.42, 0.82)` | the brightest sky in the game — hard sun, deep alpine blue |
| Evening | `(0.96, 0.78, 0.64)` | `(0.30, 0.36, 0.62)` | warm low light, the valley going to shade |
| Night | `(0.22, 0.26, 0.36)` | `(0.04, 0.07, 0.14)` | moon and a dense star field over pale rock |

### The sun has to clear the skyline

The disc is anchored at a **fixed viewport point** — morning `(0.80, 0.94)`, midday
`(0.50, 0.96)`, evening `(0.20, 0.93)`, moon `(0.74, 0.92)` — and every one of those is above
where the tallest peak can project (about 0.86 of the frame at the lowest camera). The skybox
draws behind all geometry, so a lower sun is not merely wrong-looking, it reads as *impossible*:
its halo, its bloom and its light shafts all land on top of the mountains, which is what made
an earlier version look like the sun was hanging in front of the range.

Three things carry that fix together, and they are why the sun is not simply parked higher:

- **The disc sits above the skyline** at every camera height in the map's range.
- **The halo is tightened and dimmed** (intensity 0.13–0.22, falloff 8–16 against the coast's
  0.20–0.42 / 5–12). `AerialHaze` deliberately adds the sky's halo onto *fogged geometry* so the
  land and sky match at the horizon (docs/atmospheres.md), and the ridges are 28–93 % fogged —
  so a wide bright halo paints itself onto the rock.
- **Light shafts are pulled back** (0.18–0.38 against 0.25–0.70). `GodRays` blurs the sky mask
  radially over everything in front of it, including the mountains.

**Nothing is darker for it.** The lost glare is paid back where it does not touch the peaks:
key lights up (morning 1.45, evening 1.35), the two low suns steepened to match their new height
(morning `Euler(42, −16)`, evening `Euler(36, −22)`) so the valley is lit rather than raked, the
ambient ground and equator terms lifted across the table, and evening's post exposure up to
0.42.

### Fog, and the seam it has to hide

Two separate things make distance on this map, and the split is the point.

**Distance fog** is ordinary `RenderSettings` linear fog, the same mechanism Verdun uses: start
at `cameraDistance + 180 / 300 / 250 / 220` per daytime, end at **2000** (1850 at night) against
Verdun's ~870. It does what it is good at — separating the three ranges by depth, ~28 % of haze on
ridge A at midday against ~45 % on B and ~82 % on C — and nothing else. It **cannot** be asked to
fog the bottom of a ridge, because the foot and the crest of one ridge are the same distance from
the camera, so any ramp strong enough to bury the foot greys the summit identically. An earlier
pass tried exactly that and washed the mountains out from foot to peak.

**The seam.** The rock gradient maps a fixed world height (`InverseLerp(20, 900)`), so its
green-to-stone transition happens between `y = 73` and `y = 143` *everywhere along the map*. The
camera's rotation is identity — no tilt — so a constant world height projects to a constant screen
row, and that transition draws a ruler-straight horizontal line right across the frame where the
green ground appears to meet the grey mountain. Ridge A's own cut through the meadow sits just
below it, at `y ≈ 47–71`, `z ≈ 745–785`.

**The valley mist** is what hides it: a `GroundHaze` fullscreen pass (docs/atmospheres.md),
attached in `BuildSkybox` next to `AerialHaze`.

| Parameter | Value | Why |
| --- | --- | --- |
| Full below | `y = 150` | just above where the grey has fully taken over, so the whole green-to-stone ramp sits inside the dense part |
| Clear above | `y = 330` | below ridge A's lowest crest (360), so no crest is ever touched — and ridges B and C, visible only above A, are outside the band entirely |
| Strength | `0.30` | over the ~28 % of distance fog already there, about **50 %** haze at the seam — enough to soften the line into air without painting a stripe across the valley |
| Gathers from | `z = 500` | mist thickens across the last 250 units of pasture rather than appearing |
| Full from | `z = 760` | the ridge/ground cut itself |

The mist takes `RenderSettings.fogColor`, so it is the same value as the distance fog and the sky's
horizon band — one atmosphere, at every daytime.

The pass is height-aware but reads the depth buffer, which is the whole reason it works: the
meadow at `z = 760` and the ridge face directly above it get **the same** haze, computed the same
way, so there is no line left to see. A mist quad standing in front of the join could not do that —
it would tint everything behind it, including the pasture in front of the seam, and it would have
to sort against the ridge it is hiding. The band is also what fades the ridges' feet into the
distance without the fog reaching their tops: at `y = 240`, halfway up the fade, ridge A carries
half the mist; by its crest it carries none.

### The horizon is at eye level

`SkyHorizon.AtEyeLevel`, as on the coast — not the map-edge anchor the inland skies use. The
land's far edge is behind the mountains and never visible, so there is no edge to anchor to, and
the haze band belongs at the true vanishing line with the peaks standing above it in the blue.

Night keeps the coast's `horizonFalloff` trick (0.70, an exponent above 1) so the haze leaves the
horizon flat and the low sky, the far ridge and the band are one continuous value
(docs/flanders-coast.md, *Carrying the mist across the horizon line*).

## Battlefield life

`Battlefield.BeginValley` — the third entry point next to `Begin` and `BeginCoast`
(docs/battlefield.md). It runs the full inland battlefield **minus the scenery props**:

- **Infantry squads** and **random ground blasts**, both reused unchanged, plus the permanent
  **smoke columns** in their usual `z` 140–380 band.
- **No trees or houses.** `Battlefield`'s single `_populate` switch became `_placeProps` and
  `_placePeople`, since this map is the first that wants one without the other.
- **Squads stay in the valley.** Their `z` ceiling is `DolomitesTerrain.ValleyZMax` (520)
  instead of the usual 700, so squads keep to the pasture the player actually looks at.
- **Blasts are not capped** — they still reach `z = 700`, out to the foot of the mountains.

Craters feed `InCrater` exactly as Verdun's do. Nothing consumes it while the props are off, but
it is implemented, so adding scenery to this map later needs no new work.

## Custom battle entry

`BattleMaps.All` gains `dolomites` (seed 1915) as the third map; the selector's `weather` row
picks its `Daytime` as it does for every map (docs/main-menu.md). `TerrainKind.Dolomites` and
`TerrainNames.Dolomites` complete the wiring, and `CampaignTerrain.Begin` grew from a two-way
ternary into a switch over the kind.

The map is **not** available on the fixed-width challenge levels: those build their ground
through `ProceduralTerrain.Build`, which tiles one static heightmap sideways and has no
streaming. Flanders is absent there for the same reason.
