# Atmospheres (daytimes)

## The sky system

Each `Daytime` value in `LevelDefinition.cs` maps to one self-contained static sky class
built entirely at runtime (no material/profile assets): `MorningSky`, `MiddaySky`,
`EveningSky`, `NightSky`. Each class owns four ingredients that only work together:

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
   into the sky. Retune fog colour and horizon band together or the seam shows.
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

- It computes the direction from the camera to the far-edge line (terrain mean height
  `ProceduralTerrain.BaseLevel` at z = `ProceduralTerrain.Depth`) and writes its y into
  the shader's `_HorizonLevel`, which recentres the gradient's horizon band on that line.
  Fog colour equals the band colour, so land and sky stay one seamless surface.
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
  now, so it flies at its Midday default; level 2 keeps its fixed morning definition.
- `GameManager.SetLevel1Daytime` / `SetCampaignDaytime` and their PlayerPrefs keys
  (`mr_level1_daytime`, `mr_campaign_daytime`) are intact but unwritten.

`Weather` (the enum) is still calm-only: what the player saw as "weather" was the `Daytime`
atmosphere. When real weather (storm, mist...) arrives, it plugs into the existing
`Weather` seam that every sky's `Apply` and the terrain already accept.
