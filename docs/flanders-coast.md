# Flanders Coast

The second endless map: the dry Yser plain behind the beach, the North Sea beyond it.
Career level 2 and the second entry in the custom battle map selector. Everything is built
at runtime like the rest of the game — no meshes, materials or scenes in the project.

```
z = 0    ────────────────────────────────  front edge (cut wall)
              dry Yser plain — shell craters, road embankments crossing it
z ≈ 50       ░░░░░  sand starts fading in over the flat  ░░░░░
z = 100  · · · · ·  FLIGHT LANE  · · · · ·
z = 170      ┈┈┈┈┈  sea mesh starts here, buried under the plain  ┈┈┈┈┈
z ≈ 260-345  ▁▂▃  beach slope (meanders along X)  ▃▂▁
              sand → waterline
z ≈ 360+     ≈≈≈≈≈  open North Sea, seabed dropping to −95  ≈≈≈≈≈
z = 1180     ═══════  fog closes; the water is pure haze from here out
             ───────  EYE LEVEL — the horizon line, where the sky starts climbing
```

**There is no water in the foreground.** The plain the plane flies over stands above the
waterline from the front edge all the way to the beach; the sea exists only behind it. That is
what the sea mesh's near edge at `z = 170` enforces — see *The sea*.

The shore sits well forward of where a horizon-filling sea would want it: the land is a band
across the bottom of the frame with the sea above it, and the sand reaches back onto the flat
so the beach has real width rather than being a lip on the edge of the drop. Two numbers set
that balance — the shore centre (`300`, how much sea) and the sand blend width (`250`, how much
of the land reads as beach). At those settings only the front strip of the plain is inland
earth; everything from roughly `z = 100` back is turning to sand.

## Where the pieces live

| File | Role |
| --- | --- |
| `CampaignTerrain.cs` | Abstract chunk streamer shared with Verdun (see docs/campaign.md). |
| `FlandersTerrain.cs` | This map's heightmap, its ground grain, painting and cut wall. |
| `SeaSurface.cs` | The faceted sea, camera-following and CPU-animated. |
| `CoastSky.cs` | The four coastal daytimes, their fog, and the sea colour. |
| `WaterSplash.cs` | The pale splash — shells landing in water, and the plane ditching. |

## The land

One terrain profile evaluated as a pure function of world position, so streamed chunks meet
seamlessly for the same reasons Verdun's do (docs/campaign.md).

The chunk terrain sits at **y = −115** with a **205** height range, because unlike Verdun this
map needs ground *below* zero — a Unity `Terrain` only stores heights in `[0, size.y]`, so the
seabed is bought by dropping the whole object rather than by allowing negative heights.
The world clamp is `[−110, 86]`.

**Sea level is 22** and the **water starts at `z = 170`** — two constants (`SeaSurface.Level`
and `SeaSurface.NearEdge`) shared by the water mesh, the terrain generator, the battlefield's
wet/dry test and the ditching gate. Height alone no longer decides what is water: the plain in
front of 170 has craters whose floors dip below 22 and they are dry, because there is nothing
there to be wet.

### Bands, front to back

- **Dry plain** — base `22 + 11` plus two Perlin octaves (±4.5 broad, ±1.6 fine), so it wanders
  roughly `27 … 39`, always clear of the waterline. This is what the plane actually flies over.
  The rise is chosen so the ground keeps its old on-screen height: the flooded version averaged
  about 20, so the land has come up by a dozen units, not by fifty.
- **Micro relief** — two more octaves on top, ±0.9 at a 38-unit wavelength and ±0.25 at 26,
  added *after* the band blend so they ride on whatever the ground is doing. See
  *Grain* — this is half of that answer.
- **Beach slope** — centre `300 ± 45`, half-width `30 ± 8`, both driven by long-wavelength X
  noise so the shore meanders and widens along the flight path instead of running straight.
  There is **no dune crest**: the plain simply smoothsteps down from its own height to the
  shelf at `22 − 14` between `centre − half` and `centre + half`, which puts the waterline
  within a few units of `centre` and leaves the sea behind it unobstructed.
- **Shelf and seabed** — from `centre + 60` the ground drops to a `−95 ± 12` seabed, fully
  submerged by roughly `centre + 320`. The opaque sea hides it from there back — which is why
  this map can push its fog much further out than Verdun without ever showing a map edge (see
  *Fog and horizon*).

### Why the beach is steep

The slope carries **25 units of drop over 60 of run** — around `0.4` of height per unit of Z,
peaking near `0.6` in the middle of the smoothstep. That is not a cosmetic choice.

An almost-flat beach and an almost-flat sea sheet meet at an almost-parallel angle, and the
waterline is then wherever two nearly-coincident surfaces happen to cross. The sea's flat-shaded
facets each cut the sand along their own straight line, the swell moves the crossing by
`amplitude / slope` — tens of units on a shallow beach — and the result is a visible sawtooth
edge of sea polygons lying on the sand. It gets worse the higher the plane flies, because the
view onto the junction gets steeper.

Three numbers keep that junction crisp, and they work together:

| | |
| --- | --- |
| Beach slope `≈ 0.4` | the crossing is a line, not a band; swell moves it under a unit |
| Near-shore swell 5 % (`SeaSurface.CalmFactor`) | ±0.3 of wave at the shore instead of ±0.55 |
| Shore rows every 22 units | smaller facets, and adjacent normals nearly equal where it matters |

The map used to hide that junction under a foam ribbon instead. That is the ordinary trick,
and it failed here for its own reasons — see *No foam*.

### Shell holes and embankments

Both are hashed per world X cell and gathered by influence, the same rule that keeps Verdun's
craters identical on both sides of a chunk seam.

| Feature | Cell | Rule |
| --- | --- | --- |
| Shell holes | 150 | ~1.6 per cell, radius 10–26, depth 30–45 % of radius, `z` 12–130 |
| Road embankments | 420 | 55 % chance, half-width 10–18, crest `ground + 5…9`, lean ±0.32 |

The craters are **Verdun-depth bowls**, not the shallow dishes of the flooded version: with no
water to fill them there is nothing to read the shape but the shading, so they need real depth.
Their `z` range stops at 130 so no crater bowl can reach the sea mesh's near edge at 170.

Embankment crests are **measured from the smooth ground under them** (`BaseHeight + 5…9`)
rather than from sea level, so they still stand proud now that the plain is ten units higher.
Sampling the pre-crater profile is also what keeps a causeway intact where it crosses a shell
hole instead of being breached by it.

Embankments run **into the screen** (along Z, with a small per-dyke lean) from the front edge
to `z = 190`, fading out over the last 70 units so they never climb the beach. Running them
along Z rather than along X is what makes them read: you fly over them one at a time, and their
perspective convergence sells the depth of the plain.

Nothing is *placed* on this map — no trees, houses or infantry. Every feature above is
heightmap geometry.

### Painting

Two terrain layers, pale khaki ground `(0.46, 0.42, 0.33)` and sand `(0.74, 0.70, 0.59)`,
blended on a 128² alphamap by distance from the shore centre: pure ground inland of
`centre − 250`, pure sand from `centre − 10` seaward. The 240-unit band is a **fixed width
rather than a multiple of the beach half-width**, so the sand fades in over the same distance
whether the local beach is narrow or wide.

Alphamap texels are sampled at `ix / (AlphaRes − 1)`, not `(ix + 0.5) / AlphaRes`. Unity's
terrain shader shifts splat UVs so the **first and last texels sit on the terrain's edges**
rather than half a texel inside them, so edge-aligned is the mapping that matches: chunk `i`'s
last column and chunk `i + 1`'s first column then evaluate the same world X and paint the same
value. Sampling at cell centres puts them 4 units apart and leaves a hairline at every seam.

The band is four times wider than the beach slope it ends on (60 units), and deliberately so:
the beach is meant to occupy most of the land, not to be a coloured lip on the edge of a drop.
The slope itself stays narrow and steep — that is what keeps the waterline clean (see *Why the
beach is steep*), so beach *area* is bought with the alphamap rather than by flattening the
profile.

The ground colour is the deliberate opposite of the old near-black mud: the map's features are
heightmap-only, so the only thing that can show a crater or a causeway is shading, and shading
on a `0.17` base is invisible. The submerged parts are painted too and never seen — the
alphamap is cheaper than deciding not to.

There is **no grass**: `DetailDistance` is 0 for this map, so the shared grass template
instantiated by `ProceduralTerrain.NewTerrainData` renders nothing.

### Grain

Both layers were 1×1 solid-colour textures, and a uniform albedo across a smooth heightmap is
exactly the recipe for ground that looks like moulded clay: nothing varies except the shading,
and the shading has nothing to vary over. The fix is two-sided, because the two sides answer
different distances.

**Height** — the micro octaves listed above. This is what actually kills the clay look: Unity
derives terrain normals from the heightmap, so ±1.15 units of bump at 26–38 unit wavelengths
turns a flat lit surface into one that breaks up under the key light. It matters most where
the ground is seen at a grazing angle, which is most of the time on this map. The wavelengths
have a floor for a reason — the heightmap samples every 2 units in X but every 3.1 in Z, so
anything much finer aliases along Z instead of reading as relief.

Micro relief is faded to **zero** across the beach (`MicroOnBeach`) and on the seabed. The
plain is the part that needs breaking up; the sand slope wants to be smooth, both because a
clean beach reads better and because that slope is the one surface whose crossing with the
water plane has to stay exact.

**Albedo** — `GrainLayer` builds a 128² tiling texture for the **ground layer only**:
low-frequency blotches (6 lattice cells, ±10 % brightness) and a gentle grit octave (12 cells,
±4.5 %), multiplied into the layer colour. The noise is a **wrapped value-noise lattice** rather
than `Mathf.PerlinNoise`, which does not tile — an untileable texture repeated every 85 units
would draw its own seam grid across the ground.

**The sand layer has no grain at all** (`FlatLayer`, a single pixel). Grain is there to stop
inland earth reading as clay; a beach is a smooth sheet of sand, and mottling it only adds
noise to the brightest, most glare-prone surface on the map.

**Tile sizes are divisions of `ChunkLength`** — ground at `512 / 6`, sand at `512 / 8` — and
that is not a rounding preference. A terrain layer's UV is the chunk's local position over the
tile size, and each chunk's local origin restarts at zero, so unless the chunk length is a whole
number of tiles the pattern jumps phase at every chunk boundary and draws a hard vertical stitch
down the map. At 90 units it was 5.69 tiles per chunk, and that stitch is exactly what showed.
The noise being wrap-tileable is what makes the whole-number case join invisibly.

### Why the grain is this soft

The first version of this had three octaves including **per-texel speckle**, at roughly twice
these amplitudes, tiled at 30 units. It was painful to look at, and the reason is spatial
frequency, not contrast: at 30 units of tiling a 128² texture puts one texel every 0.23 world
units, which at flight distance is well under one screen pixel. Sub-pixel albedo noise cannot
resolve — it aliases, and it crawls as the camera moves, which is what dazzles.

So the frequencies came down rather than the amplitudes alone:

| | Before | Now |
| --- | --- | --- |
| Tiling | 30 / 22 units | 85.3 / 64 units (`ChunkLength / 6` and `/ 8`) |
| Blotch | 8 cells, ±20 % | 6 cells, ±10 % |
| Grit | 26 cells, ±11 % | 12 cells, ±4.5 % |
| Speckle | ±6 % per texel | gone |
| Sand layer | grained | flat colour |

At 85 units of tiling the blotches are ~14 world units across — tens of pixels, well clear of
the resolution floor — so the ground reads as mottled rather than static. Mipmaps stay on and
aniso stays at 4, which matters at this map's grazing view angles.

**Roughness** stays at 0 for both layers, with `specular` forced to black. Fully rough, fully
matte: any gloss on sand and wet-looking earth becomes a broad specular sheet at exactly the
grazing angles this camera lives at, which is more glare on top of an already bright coast.

Both layers share the same material settings (`NewLayer`), so the only difference between them
is the texture each carries.

### The front face

The camera looks straight down `+Z` from `z = −320`, so the map's front edge at `z = 0` is a
visible cross-section and needs filling, exactly as Verdun's dirt cut wall does — the shared
`ProceduralTerrain.BuildCutWallMesh`, in `(0.38, 0.35, 0.28)` earth, from the terrain's own
front line down to −120.

The **water wall the flooded version needed is gone**. The front edge now stands above sea
level everywhere, and there is no sea geometry within 170 units of it in any case.

### No foam

There was a foam ribbon along the waterline — a strip of near-white quads 6–14 units deep,
one per column, a quarter of them empty to break the line up. **It is gone.**

A horizontal strip that reads as a hairline from a low camera does not stay a hairline: seen
from the flight ceiling, 14 units of depth at the shore project to about twenty pixels, and
the per-column widths that were meant to look like broken surf become a row of hard white
rectangles. What it actually looked like was a white gap between the beach and the sea.

The waterline reads on its own now — sand against water, on a slope steep enough to make that
a line rather than a smear (see *Why the beach is steep*). If surf comes back it wants to be
a texture on the water, not geometry standing above it.

## The sea (`SeaSurface`)

An opaque, flat-shaded triangle grid that follows the camera. Opaque is the point: there is no
diving mechanic, so nothing below the surface is ever meant to be seen, and an opaque sheet is
both the cheaper and the more low-poly answer than a transparent one.

- **Near edge** — the grid **starts at `z = 170` (`SeaSurface.NearEdge`)**, not at the front
  edge. That single number is what keeps water out of the foreground: no sea geometry exists
  there, so a deep shell crater is a dry bowl rather than a pond, whatever its floor height.
  The edge itself is never seen — the plain stands 5 to 17 units above sea level at 170, and
  every sight line that could reach the first row has to pass through the plain in front of it.
  It sits 25-odd units inland of the closest the waterline can wander, which is what stops the
  two ever meeting.
- **Grid** — 65-unit columns across ±1850. Rows every **22 units from the near edge to 400**,
  which is the band the shoreline lives in, then every 45 out to `z = 772`, then growing ×1.34
  until they pass `z = 1700`. Shore and near water get the detail; the far water is fog.
  57 × 26 quads, ~8,900 vertices.
- **Flat shading** — vertices are unshared (six per quad) and each triangle is given a single
  face normal, so every facet catches the light on its own. That faceting *is* the low-poly
  look; there is no texture and no vertex colour anywhere in it.
- **Exaggerated normals** — the face normal is computed from edge vectors whose Y is
  multiplied by 3.5. The geometry stays a gentle swell while the shading varies as if it were
  three and a half times steeper, which is what keeps the facets legible at a 420-unit camera
  standoff without turning the sea into a choppy mess.
- **Waves** — three directional sines in world space (wavelengths 260 / 150 / 74, amplitudes
  3.4 / 1.9 / 0.85), summed and scaled by a **swell ramp**: 5 % of full amplitude inland of
  `z = 350`, full past `z = 780`. The shallows by the beach barely move — which keeps the swell
  from dragging the waterline back and forth across the sand — while the open sea visibly
  does.
- **Camera follow** — the root snaps to whole column widths, so grid points always land on the
  same world X and the facets never crawl sideways as the plane advances. Wave phase is world
  space anyway, so the snap is about the mesh, not the motion.

The mesh is rebuilt on the CPU every `LateUpdate` (~8,900 vertices) rather than displaced in a
shader. It is a few tenths of a millisecond, and it keeps the whole effect inside one plain
URP/Lit material with no custom shader to break.

The sea has **no collider**. It is scenery that the plane falls through — see *Ditching*.

### Why the sea can outrun the terrain

The sea mesh reaches past `z = 1900` while the terrain chunks stop at `z = 800`, and the
streamer keeps chunks over a ±1100 window in X. Neither gap shows:

- the terrain is submerged past `z ≈ 370`, so its far edge is under opaque water;
- visible land only spans about ±600 in X at that depth, well inside the streamed window;
- the sea's own side and far edges sit past the fog's close-out distance.

## Fog and horizon

Verdun's fog ends at 870 units, which puts the last 250 units of land in solid haze so the map
edge never shows. That would drown this map's whole reason for existing — the sea would be a
grey band. Because the coast hides its own edges under water instead, `CoastSky` can push the
fog end out to **1500** (world `z ≈ 1180`) and let a real stretch of open water be visible —
except at night, which pulls it back deliberately (see *The night mist bank*).

Fog start is per daytime: morning +330, midday +620, evening +520, night +420 past the camera.

### The horizon is a plane, not a cone

`SkyHorizon.AtEyeLevel` puts this map's sky gradient at **`_HorizonSlope = 0`** — eye level,
the vanishing line of a horizontal plane — rather than aiming it at a far-edge line the way
the inland maps do. That is the difference between a sea horizon that reads and one that does
not, and the reason is projection geometry.

The parameter used to be a **view-direction Y** (`_HorizonLevel`), so any non-zero value
described a *cone* around vertical. A cone does not project to a straight line: at −0.15
(roughly what the far-edge anchor gave here) the band sat 26 % of a half-frame below centre
in the middle of the screen and 38 % below at the edges, sagging about 6 % of the frame height
across the width. The sea's own fade-out is depth-based, so it *is* a straight horizontal
line. Pale haze bounded by a straight line below and a sagging curve above is thickest in the
middle — which is exactly what read as a mound of water sitting on the horizon.

The shader now takes a **slope** instead, so the band is a plane and always projects to a
straight level line; the cone is gone for every map, not just this one (docs/atmospheres.md,
*A slope, not a view-direction Y*). Zero still means eye level, so nothing here changed.

At zero the band boundary is the true vanishing line: flat, edge to edge, and unmoving as the
plane climbs, which is how a real horizon behaves. Everything below it is uniform haze
(`_HorizonColor` and `_BottomColor` are both the haze), and the fogged sea reaches that same
colour before the mesh ends, so the water and the sky below the line are literally the same
value — there is no seam to see, and the horizon the eye picks out is where the sky starts
climbing toward the zenith.

That equality used to hold only for the *gradient*. The sun's halo is added to the sky and
not to the fogged water, so on a morning or evening coast the sea's far edge reappeared as a
bright step wherever the halo reached below the horizon line — worst in the sun's own screen
column, where it ran to about 30 %. `AerialHaze` now carries the halo onto the fogged water
too, so the two sides of the line are equal including the glow (docs/atmospheres.md,
*Aerial perspective*). The `haloFalloff` tightening that night needed for the same reason is
still worth keeping, but it is no longer the only thing holding that join together.

Two things fall out of this for free: the sun and moon now sit **above** the water instead of
half-buried in it (`anchorDisc` measures its lift from the horizon, which moved up with the
band), and the stars, masked by the same gradient, now fade out approaching the horizon
instead of carrying on down to the waterline.

### The night mist bank

Fog end is per daytime too, and only night differs: **1250** instead of 1500. On the three day
palettes the fog reaches full haze exactly at the horizon, so the water fades all the way out
and nothing reads as a distinct bank of mist. Ending 250 units *short* of the horizon leaves a
strip of sea — from roughly `z = 930` out to the horizon — sitting in solid haze, which is
what a mist bank on open water at night actually looks like: dark sea in front of you, a pale
band lying on the water at the horizon, sky above.

It works because the haze `(0.20, 0.25, 0.32)` is brighter than the moonlit sea beneath it, so
the band lightens rather than muddies. And because the fog colour is still the skybox's own
horizon band, the mist merges into the sky with no seam at the horizon line — pulling the fog
*end* in adds the bank without touching that match, which is why this is one number rather
than a second fog colour.

Night's fog start also comes in slightly, +470 → +420, so the ramp is 410 units instead of 610
and the mist visibly climbs the water. It still begins at `z ≈ 520`, well seaward of the
waterline, so the beach and the plain stay clear.

### Carrying the mist across the horizon line

Fogging the water is only half of it. The sky has to leave the horizon *slowly* too, or the
mist stops dead at eye level and the line itself becomes the edge.

`_HorizonFalloff` decides that, and the shader uses it as `tUp = pow(h, 1 / falloff)`. Any
value **above 1 puts an infinite slope at `h = 0`**: the sky starts darkening toward the zenith
the instant it leaves the horizon. That was invisible while the gradient was anchored below eye
level — the steep part sat behind the water — but with the band at eye level it lands exactly
on the horizon line, and at night, where haze `(0.20, 0.25, 0.32)` falls to zenith
`(0.05, 0.09, 0.16)`, a fourfold drop starting at full slope reads as a cut.

Night therefore runs `horizonFalloff = 0.65`, an exponent of about 1.5. Above 1, the curve
leaves the horizon **flat**, so the haze carries up into the sky and thins out gradually: the
fogged water, the horizon line and the low sky are one continuous value. The day palettes keep
their falloffs above 1 — their haze-to-zenith contrast is under 2×, so the same steepness there
is a crisp horizon rather than a seam.

Two things come with it. The moon's halo tightens (`haloFalloff` 8 → 12) because the halo is
added to the sky but not to the fogged sea, so any of it spilling onto the horizon line is a
brightness step on one side of a join that is otherwise exact. And the stars, masked by `tUp`,
now fade in over the lower sky instead of at the line — which is what haze does to stars near
the horizon anyway.

The chunk streamer's keep-window is deliberately **not** derived from this map's fog: it uses
the same land-visibility distance as Verdun, because what bounds it is where the land stops
being visible, and here that is the waterline, not the haze.

## Sky (`CoastSky`)

One class with a four-entry palette table rather than four classes, since the coastal
daytimes differ only in their numbers. It carries the same four ingredients as the inland
skies (docs/atmospheres.md): a `Custom/GradientSkybox` material, linear fog matching its
horizon band, a directional key light shining into `+Z`, and restrained URP post FX.

| Daytime | Haze | Sea | Character |
| --- | --- | --- | --- |
| Morning | `(0.88, 0.90, 0.89)` | `(0.34, 0.44, 0.43)` | pale grey-green mist, low sun off to the side |
| Midday | `(0.89, 0.92, 0.91)` | `(0.37, 0.48, 0.47)` | bright overcast North Sea daylight, the greyest of the four |
| Evening | `(0.92, 0.82, 0.72)` | `(0.30, 0.37, 0.40)` | warm amber low sun over cold water |
| Night | `(0.20, 0.25, 0.32)` | `(0.14, 0.19, 0.23)` | moon disc and stars over a blue-grey sea |

Every palette is pulled toward grey-green and desaturated relative to its Verdun counterpart
(negative `saturation`, cool white balance) — the same daytime, a colder coast.

### Exposure

All four daytimes were lifted by roughly a third, because the coast read as murky rather than
overcast. The lift is spread across four knobs rather than pushed into one, so nothing clips:

| Knob | Change |
| --- | --- |
| `postExposure` | +0.3 stop on the three day palettes (0.28→0.62, 0.30→0.62, 0.32→0.66), 1.9→2.1 at night |
| Ambient trilight | sky/equator/ground all raised; the ground term roughly doubles, which is what stops downward-facing surfaces going black |
| `lightIntensity` | +0.25 to +0.30 across the table |
| `vignette` / `contrast` | both eased back, so the lift reaches the frame edges and the shadows stay open |

Night gets the same treatment but keeps its identity: the haze, sea and ambient terms are
lifted about 1.7×, while the exposure is nudged only 0.2 of a stop and the palette stays
blue — a moonlit coast you can read, not a daylit one.

`CoastSky` also owns the **sea colour**, so the water is tinted from the same table as the sky
it sits under.

Clouds run unchanged; `CloudSystem` gained a `Begin` overload that takes the tint and glow
directly, so the coast can feed it `CoastSky`'s colours instead of the inland daytime lookup
(docs/clouds.md).

## Battlefield life

`Battlefield.BeginCoast` starts the same coordinator with three differences
(docs/battlefield.md). It takes both the sea level *and* the Z the water starts at:

- **Nothing is placed** — no scenery props, no infantry. Only blasts and smoke columns,
  as asked.
- **The sea shells itself.** A second blast stream, on its own 2.0–3.8 s timer, drops a
  `WaterSplash` somewhere in `z` 420–1080 across ±1.7 view widths — always out on open water,
  so no ground sampling is involved. Splash sizes run 60–130, larger than the land blasts',
  because everything in that band is far enough away to need the extra size to read.
- **It knows where the water is.** The land stream's wet test is `z ≥ waterFromZ && y < sea`,
  not `y < sea` alone: on a dry plain a deep crater floor sits below sea level too, and without
  the Z gate a shell landing in one would throw a splash on dry ground.
- **Smoke columns move forward.** The coast sites them in `z` 100–220 rather than Verdun's
  140–380, because past that the ground is beach and then water; burning sites belong on the
  plain in front of the plane.

The two streams together are the point — land blasts on the plain in front of the plane, and
a steady scatter of shell splashes on the sea behind it.

## Ditching

The plane flies at `z ≈ 100` and the water starts at 170, so on the reworked map **there is no
water to ditch into** — the plain always meets the plane first and `OnCrashed` handles it, as
on Verdun. The HUD hint is "don't hit the ground" on every map for that reason.

The machinery is still there and still correct, gated on the flight lane actually reaching the
water (`PlayPlaneZ >= SeaSurface.NearEdge`): a splash of size 75, `CubeController.Sink` keeping
15 % of horizontal speed at a constant 26 u/s descent with gravity off, and the fail screen two
seconds later. The gate is what keeps a plane that dips into a deep crater from "sinking" on
dry land.

## Flight ceiling

`WorldTop` dropped from 900 to **650** on both the endless campaign and the fixed challenge
levels, so the ground stays legible below the player everywhere. The camera clamp derives from
it, and on the challenge levels the enemy spawn band comes down with it.
