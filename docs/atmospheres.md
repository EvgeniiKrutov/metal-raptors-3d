# Atmospheres (daytimes)

## The sky system

Each `Daytime` value in `LevelDefinition.cs` maps to one self-contained static sky class
built entirely at runtime (no material/profile assets): `MorningSky`, `MiddaySky`,
`EveningSky`, `NightSky`. Each class owns four ingredients that only work together:

> Flanders Coast has its own set of four, as `CoastSky` — one class with a palette table
> rather than four classes, since its daytimes differ only in numbers. It follows the same
> recipe with a colder, greyer, less saturated palette, a much longer fog reach, and its
> horizon at eye level rather than on a map edge (`SkyHorizon.AtEyeLevel`). Its table also
> owns that map's sea colour.
> See docs/flanders-coast.md.
>
> Dolomites has a third set, as `DolomitesSky`, built the same way — warmer, more saturated
> and more contrasty than the inland skies (sunny northern Italy at altitude), with the
> longest fog reach in the game (2000) because the background mountains *are* that map's
> distance, and its sun pinned to a fixed viewport point high enough to clear the peaks
> rather than riding the horizon. Its table also owns the mountains' rock colours.
> See docs/dolomites.md.

1. A gradient skybox (`Custom/GradientSkybox` in `Assets/Resources/Shaders`) with a
   two-part sun (HDR core + atmospheric halo), anchored to a fixed viewport spot — the
   camera never rotates, so a skybox direction is effectively a fixed point on screen.
   The shader also carries two night extensions whose defaults are no-ops for the day
   skies: a moon-disc mode (`_DiscRadius > 0` swaps the additive soft core for an opaque
   hard-edged disc with limb shading and noise-dark maria patches — a solid body, not a
   glow) and procedural stars (`_StarIntensity > 0`; hash-cell points with varied
   brightness and a slow twinkle, masked off the horizon band, the disc, and the
   moonglow patch so they live only in the dark upper sky).
2. Linear fog whose colour is exactly the skybox's horizon band (`HazeColor`, the one
   public value per sky — `ProceduralTerrain` reads it), so the land dissolves seamlessly
   into the sky. Retune fog colour and horizon band together or the seam shows. The
   shader's `_BottomColor` (below-horizon fill) matches too, for the same reason — from a
   high camera the sky past the terrain's far edge is visible, and any tint mismatch there
   reads as a seam at the map edge. A flat colour is only ever *most* of the sky, though:
   see *Aerial perspective* for the pass that closes the rest of the gap.
3. A directional key light that cannot shine out of the visible sun (that would backlight
   the planes into silhouettes, since the camera looks straight down +Z), so it shines
   into +Z from a plausible angle on the sun's side of the sky.
4. Restrained URP post FX (bloom for the HDR sun core, white balance, grade, vignette,
   neutral tonemapping).

## Horizon alignment (SkyHorizon)

The skybox renders at infinity, so its natural horizon (view-direction y = 0) sits at eye
level — screen centre under this never-rotating camera — while the map's fogged far edge
appears lower, and lower still the higher the player flies. `SkyHorizon` (a runtime
component each sky attaches in `BuildSkybox`) closes that gap every frame:

- It computes the **slope** from the camera to the far-edge line (by default terrain mean
  height `ProceduralTerrain.BaseLevel` at z = `ProceduralTerrain.Depth`; `Attach` takes an
  explicit `edgeY`/`edgeZ` for maps whose visible edge is elsewhere) and writes it into the
  shader's `_HorizonSlope`, which recentres the gradient's horizon band on that line. Fog
  colour equals the band colour, so land and sky stay one seamless surface.
- `AtEyeLevel` is the other mode: `_HorizonSlope = 0` and the sun anchored at viewport
  centre. It exists for the coast, and the difference is not cosmetic — see
  *The horizon is a plane, not a cone* in docs/flanders-coast.md. Use it for any map whose
  far surface is meant to read as unbounded; use `Attach` where the ground genuinely ends.

**A slope, not a view-direction Y.** `_HorizonSlope` is the band plane's `dy/dz`, and the
shader's height term is `d.y − slope · d.z`. That is a *plane* through the eye, so its zero
set projects to a straight, level screen line, exactly like the map's far edge — which is
itself a straight world line at constant y and z. The parameter used to be a view-direction
Y (`_HorizonLevel`), which describes a *cone* around vertical: it sags toward the frame
edges, by 13 % of the frame height at the flight ceiling on Verdun. The land edge stayed
straight while the band curved away from it, so the two crossed and the map edge drew itself
as a hard line down both sides of the screen. Zero means eye level in either formulation, so
the coast is unaffected; only the anchored maps change, and they change by becoming correct.
- With `anchorSun` on (morning, evening) it also re-aims `_SunDirection` so the sun rides
  a fixed viewport fraction (`SunHorizonLift`) above the visible map edge — dawning or
  setting at the actual horizon, not the eye-level one behind it. Midday's overhead sun
  keeps its fixed screen anchor; only its band tracks the edge.

Fog start distance per daytime (set in `ProceduralTerrain.Build`; the far anchor is the
same for all — the last ~250 m of land sit in solid haze so the map edge never shows):

| Daytime | Fog start past camera | Air |
|---|---|---|
| Morning | +80 m | thick gold mist from just past the play line |
| Midday  | +300 m | clear; haze only toward the horizon |
| Evening | +260 m | warm haze held back to the far half — the golden air was drowning the land |
| Night   | +250 m | clear calm air; the distance is lost to darkness, not mist |

## Aerial perspective (`AerialHaze`)

Matching the fog colour to the horizon band gets the land *most* of the way into the sky, and
"most" is what shows. Unity's linear fog blends every fragment toward a single constant
colour, but the sky over that fragment is not a constant: the gradient skybox also carries
the sun's core and its wide atmospheric halo, and fog knows nothing about either. Where the
land is fully fogged it is exactly `HazeColor`, while the sky right above it is
`HazeColor + halo` — so the map's far edge draws itself as a brightness step, brightest
in the sun's screen column. On a morning coast that step is about **30 %**, which is not a
subtle seam; it is the edge of the world, drawn with a ruler.

`AerialHaze` is a fullscreen pass that adds back precisely the missing term:

```
scene += fogWeight * (skyColor(viewRay) - fogColor)
```

Since URP already wrote `lerp(scene, fogColor, w)`, this turns it into
`lerp(scene, skyColor, w)`. At full fog the land becomes **pixel-identical to the sky it is
hiding**, so the far edge cannot be seen at any camera height, at any sun position, on any
map — not hidden, but arithmetically absent. This is ordinary aerial perspective: haze takes
the colour of the light scattered through it, which near a low sun means the sun's glow.

Mechanics:

- The sky is evaluated by `Assets/Shaders/GradientSky.hlsl`, shared verbatim with
  `GradientSkybox.shader`, so the two cannot drift apart. Stars are the one term left out —
  they are masked off the horizon band anyway, and fogged land is always below it.
- Sky pixels are skipped (they already *are* the sky; adding the difference again would
  double the halo), as are pixels in front of the fog start.
- The addition is clamped to a brightening. Geometry only projects above the horizon band
  when it is close enough for fog to be near zero, and an additive pass cannot subtract from
  an unsigned colour target.
- `Blend One One`, so it never reads the colour target and needs no intermediate copy. It
  runs at `BeforeRenderingTransparents` — after the opaques and the skybox, before the
  clouds, and before `GodRays`.
- The per-pixel fog weight is rebuilt from `RenderSettings` each frame (`LinearEyeDepth`
  against `fogStartDistance`/`fogEndDistance`), so it tracks whatever the sky set, and the
  pass disables itself when fog is off or non-linear. All the sky parameters are copied from
  the live skybox material each frame, so `SkyHorizon`'s moving sun and horizon are followed
  automatically.

Every sky attaches one in `BuildSkybox`, next to `GodRays`, which is what makes it apply to
both maps and all four daytimes without a per-level switch.

## Ground haze (`GroundHaze`)

Distance fog cannot fog *part* of a thing. Every fragment of one distant object sits at almost
the same eye depth, so any ramp strong enough to bury the foot of a mountain greys its summit by
the same amount. That is fine for hiding a map's far edge, and useless for hiding a horizontal
seam — a place where two surfaces meet at one **world height** and the join draws a straight line
across the frame.

`GroundHaze` is the height-aware counterpart, and it is a fullscreen pass for the same reason
`AerialHaze` is: a seam is only invisible if both sides of it get *identical* haze, which
geometry standing in front of the join cannot guarantee and a depth-buffer pass gets for free.

```
worldY = eye.y + viewRay.y * linearEyeDepth
alpha  = strength · (1 − smoothstep(bandTop, bandClear, worldY)) · smoothstep(fromZ, fullZ, depth)
```

- **The height term** is full at and below `bandTop` and gone by `bandClear`, so the mist fills
  the low ground and thins upward. It is the *world* height that matters, not a height above the
  surface — that is what pins the densest haze to a specific altitude and lets the crests above it
  stay legible.
- **The distance term** is what keeps the near ground clear. Without it a world-height band would
  fog the valley under the player as hard as the valley a kilometre back, since both are the same
  height. The two thresholds are given as world Z planes and turned into eye depth each frame
  against the camera's own Z; every camera in the game looks down `+Z` with an identity rotation,
  so the two differ only by the eye.

`Blend SrcAlpha OneMinusSrcAlpha` toward `RenderSettings.fogColor`, read live so the mist is always
the same value as the distance fog and the sky's horizon band. It runs one event **after**
`AerialHaze` (`BeforeRenderingTransparents + 1`): `AerialHaze` brightens fogged pixels back up to
the sky, and mist laid down first would be partly undone by it.

Only `DolomitesSky` attaches one — it is the only map whose mountains cut through the ground
inside the view (docs/dolomites.md). Sky pixels are skipped, as in `AerialHaze`.

## MorningSky design

Foggy dawn: a low sun still off to the side, thick warm haze, cool shade.

- **Palette**: warm haze `(0.90, 0.84, 0.75)` under a muted morning-blue zenith
  `(0.52, 0.62, 0.76)`; pale gold sun disc and halo; amber key light; cool blue-sky /
  warm-brown-ground trilight ambient, so shadows lean blue while the land bounces warmth
  back into them.
- **Sun placement**: screen column x = 0.80 — right edge, out of the player's sightline —
  riding `SunHorizonLift = 0.08` above the map-edge horizon via `SkyHorizon` (a sun already
  risen, unlike the evening's lower, still-setting rim at 0.04). Soft ~6° core
  (`_SunFalloff 300`, intensity 4.5, well past HDR white so bloom does the glow) with a
  broad scattered-light halo (`_HaloFalloff 7`, intensity 0.35).
- **Key light**: `Euler(30, -17, 0)`, intensity 1.25 — can't shine out of the visible sun
  itself (that would backlight the planes into silhouettes under the top-down camera), so it
  shines into `+Z` from over the camera's right shoulder, low enough for long morning
  shadows and on the sun's side of the sky so the angle still feels plausible. Shadow normal
  bias raised to 0.5 — low, grazing light is prone to acne otherwise.
- **Post FX**: warm push (white balance +12, a third-of-a-stop exposure lift, saturation
  +8), wide soft bloom scatter (0.75 — a foggy glow, not neon edges) since the light shafts
  render before post and bloom along with them.
- **Light shafts**: intensity 0.85, density 0.85 — shorter and paler than the evening's,
  since this sun is already risen and sits at the frame's edge rather than raking low across
  the play plane.
- **Horizon band**: `_HorizonFalloff 2.5` — wide, since the morning air reads as thick.

## MiddaySky design

Clear noon: a small hard overhead sun, thin pale-blue distance haze, short neutral shadows —
`MorningSky`'s counterpart, built the same way.

- **Palette**: thin pale-blue haze `(0.78, 0.85, 0.93)` under a deep saturated noon-blue
  zenith `(0.24, 0.46, 0.82)`; near-white sun disc and neutral daylight key light; trilight
  ambient graduating from noon-blue sky to dry warm ground.
- **Sun placement**: a fixed viewport anchor `(0.50, 0.85)` — top-centre of the frame, above
  the dogfight — rather than glued to the horizon like the low morning/evening suns; only
  the horizon band tracks the map edge (`SkyHorizon` with `anchorSun` off). Small hard ~4°
  core (`_SunFalloff 800`, intensity 6, far past HDR white for noon glare) with a tight halo
  (`_HaloFalloff 14`, intensity 0.22) — clear air scatters far less light than the morning's
  haze.
- **Key light**: `Euler(58, 0, 0)`, intensity 1.35 — the steepest and strongest of the four,
  pouring almost straight down into `+Z` from above and behind the camera for short noon
  shadows; with the sun dead ahead at the top of the frame the straight-on yaw still feels
  plausible.
- **Post FX**: a breath cool (white balance −4, next to the morning's gold), the hardest
  contrast (+10, letting the overhead light punch), tight bloom scatter (0.6, a glare around
  the disc rather than the morning's foggy spill), and the lightest vignette (0.15) — noon
  frames read open.
- **Light shafts**: intensity 0.45, density 0.65 — barely there; clear noon air scatters
  little and an overhead sun throws no rake across the play plane, just a short fan close
  around the disc.
- **Horizon band**: `_HorizonFalloff 1.8` — the narrowest of the four; clear air lets the
  blue zenith own most of the sky.

## EveningSky design

Golden hour: the sun low by the horizon, warm yellow-orange air, dusk closing in.

- **Palette**: peach-orange haze `(0.82, 0.63, 0.48)` under a dusky violet-blue zenith
  `(0.38, 0.34, 0.52)`; deep orange sun disc; amber key light; warm mauve ambient so
  shadows lean dusk-purple instead of the morning's blue.
- **Sun placement**: screen column x = 0.22 — left of frame (unlike the morning's right
  side; the setting sun is the centrepiece of this sky, so it sits where the player
  spawns and looks from), riding `SunHorizonLift = 0.04` above the map-edge horizon via
  `SkyHorizon` (the morning uses 0.08 — a sun already risen; the evening's lower rim
  stays in the haze, a sun mid-set). Big soft disc (`_SunFalloff 150`, intensity 6) with
  a halo (`_HaloFalloff 4.5`, intensity 0.4) — the most visible sun of the four skies,
  but no longer a white hole: the disc keeps its edge instead of blowing out into the sky.
- **Key light**: `Euler(16, 20, 0)`, intensity 1.05 — the lowest of the sun skies, for
  the longest shadows and the dimmest fill; yawed from the left so it feels cast by the
  visible sun.
- **Post FX**: a warm push (white balance +22, saturation +8) and the softest contrast
  (+4), over a bloom that only takes the brightest values (threshold 1.05, intensity 1.2)
  and a near-neutral exposure (+0.10). The earlier settings — sun 8, halo 0.6, bloom 1.5
  at threshold 0.9, exposure +0.50 — stacked into a glare that washed the whole frame;
  the "blooming" of dusk belongs to the light shafts and the low sun, not to every surface.
- **Light shafts**: intensity 0.8, density 0.85 — still the longest reach of the four
  (the low sun rakes across the play plane), but under the higher bloom threshold they no
  longer read as a sheet of light.
- **Horizon band**: `_HorizonFalloff 3.5`, the widest of the three — the warm glow climbs
  well up the sky before giving way to the violet zenith.

## NightSky design

A calm middle of night: moonlight instead of sunlight — different in colour, far less in
power — under dark-violet air.

- **Palette**: dark-violet haze `(0.25, 0.22, 0.37)` under a deep indigo zenith
  `(0.07, 0.07, 0.16)`; pale silver-blue moon disc `(0.85, 0.90, 1.00)`; cold blue-silver
  key light; violet ambient (sky `(0.34, 0.34, 0.56)`, ground `(0.21, 0.18, 0.28)`) —
  roughly half the daytime skies' fill, so the scene still reads as night but the terrain
  and the planes stay legible. It was a third of that before and the level went to mud.
- **Moon placement**: screen column x = 0.74 — off to the right like the morning sun, out
  of the dogfight's sightline — riding `MoonHorizonLift = 0.30` above the map-edge
  horizon via `SkyHorizon`: well up the sky, a moon at its height, not a moonrise (the
  sun skies use 0.04–0.08).
- **Moon body**: the shader's disc mode — `_DiscRadius 1.8°` with a `0.12°` edge, an
  opaque disc drawn over the sky rather than added to it, so it reads as an object with
  light, not a glow. Limb shading (18% darker toward the rim) rounds it into a sphere;
  `_MariaIntensity 0.25` stamps the dark noise patches that make it *the moon*. Disc
  brightness 1.2 — just past HDR white, so bloom rings it gently; the `_HaloFalloff 8` /
  intensity 0.22 halo is the moonlight scattered around the body.
- **Stars**: `_StarIntensity 1.4`, `_StarScale 80` — about a quarter of the hash cells
  carry a star, so a couple of thousand points land on screen. Each is a ~4 px point
  with a squared-smoothstep profile (crisp bright centre, soft edge — sized to survive
  the colour filter and vignette; single-pixel stars vanished). Most are moderate, a few
  bright (brightness is a 4th-power hash, floor 0.35), tinted from blue-white to
  warm-white per star, with a slow ±15% twinkle. They fade only right at the horizon
  band (`saturate(tUp * 2.5)`), vanish behind the moon disc, and dim inside the
  moonglow patch — so the field reads as depth, not noise.
- **Key light**: `Euler(50, -14, 0)` — steep, matching the high moon, falling into +Z
  from the moon's side; intensity 0.9, still well under any sun (morning 1.25,
  midday 1.35, evening 1.05) — moonlight, not daylight, but enough to model the land.
- **Post FX**: the defining move is a violet colour filter `(0.78, 0.72, 0.95)` on the
  colour grade — it tints the whole frame and cools it at once. Around it: cold white
  balance (-22, the only sky below zero), desaturation (-12, colours drain at night),
  mild contrast (+4 — more crushed the shadows into black), a light vignette (0.18) and
  the biggest exposure lift of the four (+1.7). The filter used to be darker
  `(0.65, 0.56, 0.85)` and the contrast/vignette heavier, which together made the level
  unreadable rather than nocturnal.
- **Horizon band**: `_HorizonFalloff 2.2` — a restrained band of violet glow low over
  the land, night's version of scattered horizon light.
- **Player searchlight**: night is the only daytime that mounts one on the player's plane
  (warm cone from the nose, toggled with T, off at spawn) — see docs/searchlight.md.

## Light shafts implementation (`GodRays`)

A fullscreen URP pass (`Hidden/StylizedGodRays`) that radially blurs the depth buffer's sky
mask outward from the sun's screen position, so the land, trees and planes carve the dark
gaps that read as light striking from behind them. `GodRays.Attach` hangs one instance per
camera (re-attaching first disables and destroys any stale instance, so re-applying a sky
can't stack a second set of shafts) and enqueues its `RayPass` via
`RenderPipelineManager.beginCameraRendering` — before post-processing, so bloom blows the
shafts out along with the HDR sun/moon core.

The pass is enqueued per frame from code rather than living on a Renderer Data asset, since
this project builds its whole rendering setup in code and the shafts belong to whichever sky
is currently applied. Because reading and writing the same render-graph texture is illegal,
the pass allocates a fresh intermediate colour target to render into and swaps
`resources.cameraColor` to it.

Sun tracking runs in `OnBeginCamera` rather than `LateUpdate`, so it always reads the sun
direction `SkyHorizon` settled on *this* frame rather than the previous one. It re-projects a
point far down the sun's direction to viewport space (the skybox renders at infinity, so that
projects to the same screen spot as the disc itself) and fades the shafts out smoothly as the
sun nears or crosses the view edge, plus a second fade on view alignment (`Vector3.Dot` of the
camera's forward and the sun direction) — the second fade exists so a fast camera swing can't
pop the shafts in abruptly as the sun crosses behind the view.

## Exposure

`ColorAdjustments.postExposure` per sky: +0.35 midday, +0.40 morning, +0.10 evening,
+1.70 night. The two ends were retuned after play: night was lifted (with its ambient,
key light and haze) because the level had gone unreadable, and evening was pulled down
(with its bloom, sun and fog) because the golden light was washing the frame out. The
relations still hold — night is plainly night, evening still golden — they just no longer
run past the point where the dogfight stops reading.

## Who picks the daytime

The level's definition, or the custom battle screen (see docs/main-menu.md):

- Campaign level 1 is authored at dawn: `CampaignLevels.Level1.daytime = Daytime.Morning`.
- A custom battle flies `CampaignLevels.Custom(map, daytime)` instead, built from whatever
  the menu's weather selector was left on. `CustomBattle` holds that pick in memory only.
- Challenge level 1 still composes from `GameManager.Level1Daytime`, which nothing writes
  now, so it flies at its Midday default; level 2 keeps its fixed morning definition — and
  now over Verdun terrain (seed 1916, 2000 wide, same as level 1), not the flat slab, so
  `TerrainKind.FlatSlab` is currently unused by any level.
- `GameManager.SetLevel1Daytime` / `SetCampaignDaytime` and their PlayerPrefs keys
  (`mr_level1_daytime`, `mr_campaign_daytime`) are intact but unwritten.

`Weather` (the enum) is still calm-only: what the player saw as "weather" was the `Daytime`
atmosphere. When real weather (storm, mist...) arrives, it plugs into the existing
`Weather` seam that every sky's `Apply` and the terrain already accept.
